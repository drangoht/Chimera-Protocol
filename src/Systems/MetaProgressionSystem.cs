using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// AutoLoad singleton — gestion de la progression meta (Échos d'Aether, upgrades permanents).
/// Charge data/meta_upgrades.json au démarrage.
/// Expose les opérations d'achat et d'application des bonus aux PlayerStats.
/// </summary>
public partial class MetaProgressionSystem : Node
{
    public static MetaProgressionSystem Instance { get; private set; } = null!;

    // ---------------------------------------------------------------------------
    // Données chargées depuis meta_upgrades.json
    // ---------------------------------------------------------------------------

    private readonly List<MetaUpgradeDefinition> _upgradeDefs = new();

    // Cache des formules d'Échos
    private int _echoTimeDiv   = 20;
    private int _echoKillDiv   = 10;
    private int _echoCoreMult  = 5;
    private int _echoBaseBonus = 10;

    // ---------------------------------------------------------------------------
    // État runtime (synchronisé avec SaveManager)
    // ---------------------------------------------------------------------------

    private SaveData _saveData = new();

    public int CurrentEchoes => _saveData.Meta.CurrentEchoes;

    // ---------------------------------------------------------------------------
    // Cycle de vie
    // ---------------------------------------------------------------------------

    public override void _Ready()
    {
        Instance = this;
        LoadUpgradesJson();
        _saveData = SaveManager.Instance.Load();
        GD.Print($"[MetaProgressionSystem] Prêt. Échos disponibles : {CurrentEchoes}");
    }

    private void LoadUpgradesJson()
    {
        const string path = "res://data/meta_upgrades.json";
        if (!Godot.FileAccess.FileExists(path))
        {
            GD.PrintErr("[MetaProgressionSystem] meta_upgrades.json introuvable.");
            return;
        }

        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr("[MetaProgressionSystem] Impossible de lire meta_upgrades.json.");
            return;
        }

        string json = file.GetAsText();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Formule d'Échos
        if (root.TryGetProperty("echoesFormula", out var formula))
        {
            if (formula.TryGetProperty("timeDiv",   out var td)) _echoTimeDiv   = td.GetInt32();
            if (formula.TryGetProperty("killDiv",   out var kd)) _echoKillDiv   = kd.GetInt32();
            if (formula.TryGetProperty("coreMult",  out var cm)) _echoCoreMult  = cm.GetInt32();
            if (formula.TryGetProperty("baseBonus", out var bb)) _echoBaseBonus = bb.GetInt32();
        }

        // Définitions d'upgrades
        if (root.TryGetProperty("upgrades", out var upgrades))
        {
            foreach (var upg in upgrades.EnumerateArray())
            {
                var def = new MetaUpgradeDefinition
                {
                    Id          = upg.GetProperty("id").GetString()!,
                    Name        = upg.GetProperty("name").GetString()!,
                    Description = upg.GetProperty("description").GetString()!,
                    MaxLevel    = upg.GetProperty("maxLevel").GetInt32(),
                    StatTarget  = upg.GetProperty("statTarget").GetString()!,
                };

                var costs   = upg.GetProperty("costPerLevel");
                var effects = upg.GetProperty("effectPerLevel");

                for (int i = 0; i < costs.GetArrayLength(); i++)
                    def.CostPerLevel.Add(costs[i].GetInt32());

                for (int i = 0; i < effects.GetArrayLength(); i++)
                    def.EffectPerLevel.Add(effects[i].GetDouble());

                _upgradeDefs.Add(def);
            }
        }

        GD.Print($"[MetaProgressionSystem] {_upgradeDefs.Count} upgrades chargés.");
    }

    // ---------------------------------------------------------------------------
    // Accès publics
    // ---------------------------------------------------------------------------

    public List<MetaUpgradeDefinition> GetAllUpgrades() => _upgradeDefs;

    public int GetUpgradeLevel(string upgradeId)
    {
        _saveData.Meta.Upgrades.TryGetValue(upgradeId, out int level);
        return level;
    }

    public bool CanPurchase(string upgradeId)
    {
        var def = FindDef(upgradeId);
        if (def == null) return false;

        int currentLevel = GetUpgradeLevel(upgradeId);
        if (currentLevel >= def.MaxLevel) return false;

        int cost = def.CostPerLevel[currentLevel];
        return _saveData.Meta.CurrentEchoes >= cost;
    }

    /// <summary>
    /// Tente l'achat de la prochaine amélioration pour <paramref name="upgradeId"/>.
    /// Retourne true si l'achat a réussi.
    /// </summary>
    public bool TryPurchase(string upgradeId)
    {
        if (!CanPurchase(upgradeId)) return false;

        var def          = FindDef(upgradeId)!;
        int currentLevel = GetUpgradeLevel(upgradeId);
        int cost         = def.CostPerLevel[currentLevel];

        _saveData.Meta.CurrentEchoes   -= cost;
        _saveData.Meta.TotalEchoesSpent += cost;
        _saveData.Meta.Upgrades[upgradeId] = currentLevel + 1;

        SaveManager.Instance.Save(_saveData);
        GD.Print($"[MetaProgressionSystem] Acheté {upgradeId} → niveau {currentLevel + 1}. Échos restants : {CurrentEchoes}");
        return true;
    }

    /// <summary>Ajoute des Échos, met à jour les totaux et sauvegarde.</summary>
    public void AddEchoes(int amount)
    {
        if (amount <= 0) return;
        _saveData.Meta.CurrentEchoes    += amount;
        _saveData.Meta.TotalEchoesEarned += amount;
        SaveManager.Instance.Save(_saveData);
        GD.Print($"[MetaProgressionSystem] +{amount} Échos. Total disponible : {CurrentEchoes}");
    }

    /// <summary>
    /// Applique les bonus meta permanents à <paramref name="stats"/> avant le début d'une run.
    /// Respecte les hardcaps de PlayerStats.
    /// </summary>
    public void ApplyMetaBonusesToStats(PlayerStats stats)
    {
        stats.MaxHp            += GetUpgradeLevel("hp_boost")            * 20f;
        stats.CurrentHp         = stats.MaxHp;

        stats.DamageMultiplier += GetUpgradeLevel("damage_boost")        * 0.10f;

        float speedBonus        = GetUpgradeLevel("speed_boost")         * 15f;
        stats.Speed             = Mathf.Min(stats.Speed + speedBonus, PlayerStats.MaxSpeed);
        stats.BaseSpeed         = stats.Speed;

        stats.CooldownReduction += GetUpgradeLevel("cooldown_reduction") * 0.05f;

        float newDR             = stats.DamageReduction + GetUpgradeLevel("damage_reduction") * 0.05f;
        stats.DamageReduction   = Mathf.Min(newDR, PlayerStats.MaxDamageReduction);
    }

    /// <summary>
    /// Retourne la liste des armes de départ déverrouillées.
    /// "impulse_cannon" toujours présent ; "drone_swarm" si starting_weapon_alt >= 1.
    /// </summary>
    public List<string> GetUnlockedStartingWeapons()
    {
        var weapons = new List<string> { "impulse_cannon" };
        if (GetUpgradeLevel("starting_weapon_alt") >= 1)
            weapons.Add("drone_swarm");
        return weapons;
    }

    /// <summary>
    /// Réinitialise TOUTES les améliorations achetées et rembourse l'intégralité des Échos
    /// dépensés pour les niveaux possédés. Retourne le montant remboursé (0 si rien à reset).
    /// </summary>
    public int ResetUpgrades()
    {
        int refund = 0;
        foreach (var def in _upgradeDefs)
        {
            int level = GetUpgradeLevel(def.Id);
            for (int i = 0; i < level && i < def.CostPerLevel.Count; i++)
                refund += def.CostPerLevel[i];
        }

        if (refund == 0 && _saveData.Meta.Upgrades.Count == 0) return 0;

        _saveData.Meta.Upgrades.Clear();
        _saveData.Meta.CurrentEchoes    += refund;
        _saveData.Meta.TotalEchoesSpent  = Mathf.Max(0, _saveData.Meta.TotalEchoesSpent - refund);
        SaveManager.Instance.Save(_saveData);
        GD.Print($"[MetaProgressionSystem] Reset des améliorations. Remboursé : {refund} Échos. Total : {CurrentEchoes}");
        return refund;
    }

    // ---------------------------------------------------------------------------
    // Accesseurs formule (utilisés par RunStatsTracker)
    // ---------------------------------------------------------------------------

    public int EchoTimeDiv   => _echoTimeDiv;
    public int EchoKillDiv   => _echoKillDiv;
    public int EchoCoreMult  => _echoCoreMult;
    public int EchoBaseBonus => _echoBaseBonus;

    // ---------------------------------------------------------------------------
    // Helpers privés
    // ---------------------------------------------------------------------------

    private MetaUpgradeDefinition? FindDef(string id)
    {
        foreach (var def in _upgradeDefs)
            if (def.Id == id) return def;
        return null;
    }
}

// ---------------------------------------------------------------------------
// DTO définition d'upgrade (interne au projet)
// ---------------------------------------------------------------------------

public sealed class MetaUpgradeDefinition
{
    public string Id          { get; set; } = "";
    public string Name        { get; set; } = "";
    public string Description { get; set; } = "";
    public int    MaxLevel    { get; set; } = 1;
    public string StatTarget  { get; set; } = "";
    public List<int>    CostPerLevel   { get; set; } = new();
    public List<double> EffectPerLevel { get; set; } = new();
}
