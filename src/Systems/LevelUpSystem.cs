using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// S'abonne à XpSystem.LevelUp et construit les 3 cartes proposées au joueur.
/// Règles (cf. GDD §6 et levelup_config.json) :
///  - Pool = armes non au max + passifs non au max + fusions disponibles
///  - Jamais 2 cartes du même id
///  - Fusion forcée si conditions remplies depuis ≥ 1 niveau sans avoir été proposée
///  - Si pool < 3, compléter avec XP_BONUS
/// </summary>
public partial class LevelUpSystem : Node
{
    public static LevelUpSystem Instance { get; private set; } = null!;

    [Signal] public delegate void ShowLevelUpScreenEventHandler(Godot.Collections.Array cards);

    private readonly RandomNumberGenerator _rng = new();
    private JsonDocument? _weaponsData;
    private JsonDocument? _levelupData;

    // Suivi des fusions disponibles non encore proposées
    private int _lastFusionAvailableLevel = -1;
    private string? _pendingFusionId = null;

    // true = drop déclenché par un mini-boss, pas un level-up XP normal
    private bool _isWeaponDrop = false;

    // Consommables par run, initialisés depuis les améliorations méta dans Reset().
    /// <summary>Nombre de renouvellements de cartes restants pour la run.</summary>
    public int RerollsLeft { get; private set; }
    /// <summary>Nombre de sélections « passées » restantes pour la run.</summary>
    public int SkipsLeft   { get; private set; }

    // Tous les ids connus (pour le pool complet)
    private static readonly string[] AllWeaponIds  = { "impulse_cannon", "plasma_blade", "drone_swarm", "overload_field", "tesla_coil", "scatter_volley", "glaive", "seeker_swarm", "cryo_lance", "pyre_stream", "singularity" };
    private static readonly string[] AllPassiveIds = { "thermal_core", "reinforced_plating", "servo_motors", "capacitor" };
    private static readonly string[] AllFusionIds  = { "fusion_blade", "rail_overcharged", "orbital_swarm", "overload_aegis", "ionic_storm", "solar_column", "hornet_swarm" };

    public override void _Ready()
    {
        Instance = this;
        LoadJson();
        XpSystem.Instance.LevelUp += OnLevelUp;
    }

    /// <summary>Réinitialise l'état de suivi de fusion entre deux runs.</summary>
    public void Reset()
    {
        _pendingFusionId          = null;
        _lastFusionAvailableLevel = -1;
        _isWeaponDrop             = false;

        // Consommables de la run = niveau des améliorations méta achetées (max 3 chacun).
        var meta    = MetaProgressionSystem.Instance;
        RerollsLeft = meta?.GetUpgradeLevel("reroll") ?? 0;
        SkipsLeft   = meta?.GetUpgradeLevel("skip")   ?? 0;
    }

    /// <summary>Consomme un renouvellement si disponible. Retourne true si réussi.</summary>
    public bool TryConsumeReroll()
    {
        if (RerollsLeft <= 0) return false;
        RerollsLeft--;
        return true;
    }

    /// <summary>Consomme un « passer » si disponible. Retourne true si réussi.</summary>
    public bool TryConsumeSkip()
    {
        if (SkipsLeft <= 0) return false;
        SkipsLeft--;
        return true;
    }

    /// <summary>
    /// Régénère un nouveau jeu de 3 cartes pour la sélection EN COURS (sans changer de niveau).
    /// Tient compte du contexte : drop de mini-boss ou montée de niveau normale.
    /// </summary>
    public Godot.Collections.Array RerollCurrentCards()
    {
        if (_isWeaponDrop)
            return BuildWeaponCards(3);

        var inv    = InventorySystem.Instance;
        int level  = XpSystem.Instance.CurrentLevel;
        var pool   = BuildPool(inv);
        var chosen = PickCards(pool, level, inv);

        var arr = new Godot.Collections.Array();
        foreach (var card in chosen)
            arr.Add(card.ToGodotDict());
        return arr;
    }

    private void LoadJson()
    {
        using var wf = Godot.FileAccess.Open("res://data/weapons.json", Godot.FileAccess.ModeFlags.Read);
        if (wf != null) _weaponsData = JsonDocument.Parse(wf.GetAsText());

        using var lf = Godot.FileAccess.Open("res://data/levelup_config.json", Godot.FileAccess.ModeFlags.Read);
        if (lf != null) _levelupData = JsonDocument.Parse(lf.GetAsText());
    }

    private void OnLevelUp(int newLevel)
    {
        var inv = InventorySystem.Instance;

        // Vérifie si une fusion est disponible
        UpdatePendingFusion(newLevel, inv);

        var pool   = BuildPool(inv);
        var chosen = PickCards(pool, newLevel, inv);

        // Convertit en Godot.Collections.Array de LevelUpCard
        var godotArr = new Godot.Collections.Array();
        foreach (var card in chosen)
            godotArr.Add(card.ToGodotDict());

        EmitSignal(SignalName.ShowLevelUpScreen, godotArr);
    }

    // -------------------------------------------------------------------------
    // Pool & sélection
    // -------------------------------------------------------------------------

    private List<LevelUpCardData> BuildPool(InventorySystem inv)
    {
        var pool = new List<LevelUpCardData>();

        // Armes actives non au niveau max
        foreach (var id in AllWeaponIds)
        {
            if (inv.AppliedFusions.Count > 0 && IsReplacedByFusion(id, inv)) continue;
            int lvl    = inv.WeaponLevels.GetValueOrDefault(id, 0);
            int maxLvl = inv.GetWeaponMaxLevel(id);
            if (lvl >= maxLvl) continue;
            // Limite de 5 armes : une arme NON possédée n'est plus proposée si l'inventaire est plein.
            if (lvl == 0 && inv.EquippedWeaponCount >= InventorySystem.MaxEquippedWeapons) continue;
            pool.Add(MakeWeaponCard(id, lvl + 1));
        }

        // Passifs non au niveau max
        foreach (var id in AllPassiveIds)
        {
            int lvl    = inv.PassiveLevels.GetValueOrDefault(id, 0);
            int maxLvl = inv.GetPassiveMaxLevel(id);
            if (lvl < maxLvl)
                pool.Add(MakePassiveCard(id, lvl + 1));
        }

        // Fusions disponibles
        foreach (var id in AllFusionIds)
        {
            if (!inv.AppliedFusions.Contains(id) && inv.CanFuse(id))
                pool.Add(MakeFusionCard(id));
        }

        return pool;
    }

    private List<LevelUpCardData> PickCards(List<LevelUpCardData> pool, int currentLevel, InventorySystem inv)
    {
        const int CardCount = 3;
        var result = new List<LevelUpCardData>();

        // Règle fusion forcée
        if (_pendingFusionId != null &&
            _lastFusionAvailableLevel >= 0 &&
            currentLevel > _lastFusionAvailableLevel + 1)
        {
            var forced = pool.Find(c => c.Id == _pendingFusionId);
            if (forced != null)
            {
                result.Add(forced);
                pool.Remove(forced);
                _pendingFusionId = null;
            }
        }

        // Pondéré par rareté
        while (result.Count < CardCount && pool.Count > 0)
        {
            var card = WeightedPickAndRemove(pool);
            // Vérifie unicité d'id
            if (result.Exists(c => c.Id == card.Id)) continue;
            result.Add(card);
        }

        // Compléter avec XP bonus si pool insuffisant
        while (result.Count < CardCount)
            result.Add(XpBonusCard());

        return result;
    }

    private LevelUpCardData WeightedPickAndRemove(List<LevelUpCardData> pool)
    {
        var weights = new float[pool.Count];
        float total = 0f;
        for (int i = 0; i < pool.Count; i++) { weights[i] = RarityWeight(pool[i].Rarity); total += weights[i]; }

        int idx = WeightedPicker.PickIndex(weights, _rng.RandfRange(0f, total));
        var card = pool[idx];
        pool.RemoveAt(idx);
        return card;
    }

    // -------------------------------------------------------------------------
    // Suivi des fusions
    // -------------------------------------------------------------------------

    private void UpdatePendingFusion(int currentLevel, InventorySystem inv)
    {
        foreach (var id in AllFusionIds)
        {
            if (!inv.AppliedFusions.Contains(id) && inv.CanFuse(id))
            {
                if (_pendingFusionId != id)
                {
                    _pendingFusionId            = id;
                    _lastFusionAvailableLevel   = currentLevel;
                }
                return;
            }
        }
        _pendingFusionId = null;
    }

    // -------------------------------------------------------------------------
    // Factories de cartes
    // -------------------------------------------------------------------------

    // Nom = source unique Codex (accentué, cohérent avec Arsenal/notifs).
    // Description = weapons.json (concise, adaptée au format carte).
    private LevelUpCardData MakeWeaponCard(string id, int nextLevel)
    {
        string rarity = GetRarity(id);
        return new LevelUpCardData(id, Codex.DisplayName(id),
            Codex.Description(id) + Loc.T("CARD_LEVEL", nextLevel), rarity, "weapon");
    }

    private LevelUpCardData MakePassiveCard(string id, int nextLevel)
    {
        string rarity = GetRarity(id);
        return new LevelUpCardData(id, Codex.DisplayName(id),
            Codex.Description(id) + Loc.T("CARD_LEVEL", nextLevel), rarity, "passive");
    }

    private LevelUpCardData MakeFusionCard(string id)
    {
        return new LevelUpCardData(id, Codex.DisplayName(id), Codex.Description(id), "epic", "fusion");
    }

    private static LevelUpCardData XpBonusCard() =>
        new("xp_bonus", Codex.DisplayName("xp_bonus"), Loc.T("CARD_XP_BONUS"), "common", "xp_bonus");

    // -------------------------------------------------------------------------
    // Helpers JSON
    // -------------------------------------------------------------------------

    private string GetRarity(string id)
    {
        if (_levelupData == null) return "common";
        if (_levelupData.RootElement.GetProperty("rarityByCard").TryGetProperty(id, out var r))
            return r.GetString() ?? "common";
        return "common";
    }

    private string GetDescription(string id, string section)
    {
        if (_weaponsData == null) return "";
        foreach (var item in _weaponsData.RootElement.GetProperty(section).EnumerateArray())
            if (item.GetProperty("id").GetString() == id)
                return item.GetProperty("description").GetString() ?? "";
        return "";
    }

    private string GetFusionDesc(string id)
    {
        if (_weaponsData == null) return "";
        foreach (var f in _weaponsData.RootElement.GetProperty("fusions").EnumerateArray())
            if (f.GetProperty("id").GetString() == id)
                return f.GetProperty("description").GetString() ?? "";
        return "";
    }

    private static float RarityWeight(string rarity) => RarityWeights.Weight(rarity);

    private bool IsReplacedByFusion(string weaponId, InventorySystem inv)
    {
        if (_weaponsData == null) return false;
        foreach (var f in _weaponsData.RootElement.GetProperty("fusions").EnumerateArray())
        {
            if (!inv.AppliedFusions.Contains(f.GetProperty("id").GetString()!)) continue;
            if (f.GetProperty("replaces").GetString() == weaponId) return true;
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // Weapon drop (mini-boss)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Construit N cartes d'armes/passifs non maxés pour un drop de mini-boss.
    /// Réutilise les mêmes factories que le level-up normal.
    /// </summary>
    public Godot.Collections.Array BuildWeaponCards(int count)
    {
        var inv = InventorySystem.Instance;
        var available = new System.Collections.Generic.List<LevelUpCardData>();

        // Armes non maxées
        foreach (var id in AllWeaponIds)
        {
            if (inv.AppliedFusions.Count > 0 && IsReplacedByFusion(id, inv)) continue;
            int curLv = inv.WeaponLevels.GetValueOrDefault(id, 0);
            int maxLv = inv.GetWeaponMaxLevel(id);
            if (curLv >= maxLv) continue;
            if (curLv == 0 && inv.EquippedWeaponCount >= InventorySystem.MaxEquippedWeapons) continue;
            available.Add(MakeWeaponCard(id, curLv + 1));
        }

        // Si pas assez, compléter avec passifs non maxés
        if (available.Count < count)
        {
            foreach (var id in AllPassiveIds)
            {
                int curLv = inv.PassiveLevels.GetValueOrDefault(id, 0);
                int maxLv = inv.GetPassiveMaxLevel(id);
                if (curLv < maxLv)
                    available.Add(MakePassiveCard(id, curLv + 1));
            }
        }

        // Fusions disponibles si on manque encore
        if (available.Count < count)
        {
            foreach (var id in AllFusionIds)
            {
                if (!inv.AppliedFusions.Contains(id) && inv.CanFuse(id))
                    available.Add(MakeFusionCard(id));
            }
        }

        // Shuffle et prendre count cartes
        var shuffled = new System.Collections.Generic.List<LevelUpCardData>(available);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = (int)GD.Randi() % (i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var cards = new Godot.Collections.Array();
        int take = Mathf.Min(count, shuffled.Count);
        for (int i = 0; i < take; i++)
            cards.Add(shuffled[i].ToGodotDict());

        // Compléter avec XP bonus si pool insuffisant
        while (cards.Count < count)
            cards.Add(XpBonusCard().ToGodotDict());

        return cards;
    }

    /// <summary>true pendant un drop de mini-boss, false sinon.</summary>
    public bool IsWeaponDrop => _isWeaponDrop;

    /// <summary>Appelé par LevelUpScreen.OnCardChosen() pour signaler la fin d'un drop.</summary>
    public void ResolveWeaponDrop() => _isWeaponDrop = false;

    /// <summary>
    /// Déclenche un écran de sélection de carte suite à la mort d'un mini-boss.
    /// N'incrémente pas le niveau XP.
    /// </summary>
    public void ShowWeaponDrop(int cardCount = 3)
    {
        var cards = BuildWeaponCards(cardCount);
        if (cards.Count == 0) return;

        _isWeaponDrop = true;
        // Réutilise le signal existant — LevelUpScreen.Show() gère la pause
        EmitSignal(SignalName.ShowLevelUpScreen, cards);
    }

    public override void _ExitTree()
    {
        if (XpSystem.Instance != null)
            XpSystem.Instance.LevelUp -= OnLevelUp;
    }
}
// Le DTO LevelUpCardData est dans src/Systems/LevelUpCardData.cs.
