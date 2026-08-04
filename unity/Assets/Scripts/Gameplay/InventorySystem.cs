using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arsenal du joueur : armes équipées, niveaux, fusions — port du noyau de
/// <c>InventorySystem</c> (Lot 3).
///
/// <para><b>Les règles sensibles ne vivent pas ici</b> : elles sont dans
/// <see cref="WeaponTable"/> (lecture des données) et <see cref="WeaponFusion"/> (héritage de
/// niveau et dégâts). Ce composant ne fait que les orchestrer. Ce découpage n'est pas cosmétique :
/// sous Godot, ces règles étaient enfermées dans un <c>Node</c>, donc <b>intestables</b> — et c'est
/// exactement là que s'était logé le déséquilibre des fusions corrigé en 1.21.0.</para>
/// </summary>
public sealed class InventorySystem : MonoBehaviour
{
    public static InventorySystem? Instance { get; private set; }

    /// <summary>Nombre maximal d'armes portées simultanément (fusions comprises).</summary>
    public const int MaxWeapons = 6;

    private readonly Dictionary<string, int> _weaponLevels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WeaponBase> _weaponNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _passiveLevels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _overloadTakes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _appliedFusions = new(StringComparer.Ordinal);

    private Dictionary<string, WeaponTable.WeaponDef> _weapons = new();
    private Dictionary<string, WeaponTable.FusionDef> _fusions = new();
    private Dictionary<string, PassiveTable.PassiveDef> _passiveDefs = new();

    /// <summary>Émis à l'acquisition ou la montée d'une arme : <c>(id, niveau)</c>.</summary>
    public event Action<string, int>? WeaponChanged;

    /// <summary>Émis à l'acquisition ou la montée d'un passif : <c>(id, niveau)</c>.</summary>
    public event Action<string, int>? PassiveChanged;

    /// <summary>Émis quand une fusion est forgée : <c>(idFusion, niveauHérité)</c>.</summary>
    public event Action<string, int>? FusionApplied;

    /// <summary>Armes actuellement portées, fusions comprises.</summary>
    public int WeaponCount => _weaponLevels.Count;

    /// <summary>Fusions déjà forgées.</summary>
    public IReadOnlyCollection<string> AppliedFusions => _appliedFusions;

    /// <summary>Armes portées et leur niveau — lu tel quel par <see cref="LevelUpPool"/>.</summary>
    public IReadOnlyDictionary<string, int> WeaponLevels => _weaponLevels;

    /// <summary>Passifs portés et leur niveau — lu tel quel par <see cref="LevelUpPool"/>.</summary>
    public IReadOnlyDictionary<string, int> PassiveLevels => _passiveLevels;

    /// <summary>Prises de chaque carte de surcharge — sans plafond, par construction.</summary>
    public IReadOnlyDictionary<string, int> OverloadTakes => _overloadTakes;

    /// <summary>
    /// Point de montage des armes créées. Le joueur par défaut : une arme doit suivre son porteur.
    /// </summary>
    public Transform? Mount { get; set; }

    private void Awake()
    {
        Instance = this;
        LoadData();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LoadData()
    {
        string? json = DataFiles.Load("weapons.json");
        if (json == null) return;

        (_weapons, _fusions) = WeaponTable.Parse(json);
        _passiveDefs = PassiveTable.Parse(json);
        Debug.Log($"[InventorySystem] {_weapons.Count} armes, {_fusions.Count} fusions " +
                  $"et {_passiveDefs.Count} passifs charges.");
    }

    /// <summary>Toutes les armes de base connues des données (fusions exclues : elles se forgent).</summary>
    public IReadOnlyList<string> AllWeaponIds
    {
        get
        {
            var ids = new List<string>(_weapons.Count);
            foreach (string id in _weapons.Keys) ids.Add(id);
            return ids;
        }
    }

    /// <summary>Tous les passifs connus des données.</summary>
    public IReadOnlyList<string> AllPassiveIds
    {
        get
        {
            var ids = new List<string>(_passiveDefs.Count);
            foreach (string id in _passiveDefs.Keys) ids.Add(id);
            return ids;
        }
    }

    /// <summary>Niveau maximal d'une arme, tel que déclaré par les données.</summary>
    public int WeaponMaxLevel(string weaponId)
        => _weapons.TryGetValue(weaponId, out var def) ? def.MaxLevel : 20;

    /// <summary>Niveau maximal d'un passif, tel que déclaré par les données.</summary>
    public int PassiveMaxLevel(string passiveId)
        => _passiveDefs.TryGetValue(passiveId, out var def) ? def.MaxLevel : 20;

    /// <summary>Fusions actuellement déblocables — l'entrée « fusion » du choix de niveau.</summary>
    public IReadOnlyList<string> AvailableFusions
    {
        get
        {
            var ids = new List<string>();
            foreach (string id in _fusions.Keys)
                if (CanFuse(id)) ids.Add(id);
            return ids;
        }
    }

    // ─── Armes ────────────────────────────────────────────────────────────────

    /// <summary>Niveau d'une arme, ou 0 si elle n'est pas portée.</summary>
    public int LevelOf(string weaponId) => _weaponLevels.GetValueOrDefault(weaponId, 0);

    /// <summary>Le joueur porte-t-il cette arme ?</summary>
    public bool Has(string weaponId) => _weaponLevels.ContainsKey(weaponId);

    /// <summary>Déclare un passif acquis — condition de déblocage des fusions.</summary>
    public void AddPassive(string passiveId) => AddOrUpgradePassive(passiveId);

    /// <summary>Le joueur possède-t-il ce passif ?</summary>
    public bool HasPassive(string passiveId) => _passiveLevels.ContainsKey(passiveId);

    /// <summary>Niveau d'un passif, ou 0 s'il n'est pas porté.</summary>
    public int PassiveLevelOf(string passiveId) => _passiveLevels.GetValueOrDefault(passiveId, 0);

    /// <summary>
    /// Acquiert l'arme, ou la monte d'un niveau si elle est déjà portée. Renvoie le nouveau niveau,
    /// ou 0 si l'action est refusée (arsenal plein, arme inconnue, niveau maximum atteint).
    /// </summary>
    public int AcquireOrLevelUp(string weaponId, Transform? mount = null)
    {
        if (!_weapons.TryGetValue(weaponId, out var def)) return 0;

        int current = LevelOf(weaponId);
        if (current == 0 && WeaponCount >= MaxWeapons) return 0;
        if (current >= def.MaxLevel) return 0;

        int next = current + 1;
        _weaponLevels[weaponId] = next;

        if (_weaponNodes.TryGetValue(weaponId, out var weapon))
            ApplyWeaponStats(weaponId, next, weapon);
        else
            InstantiateWeapon(weaponId, next, mount);

        WeaponChanged?.Invoke(weaponId, next);
        return next;
    }

    /// <summary>Enregistre une arme déjà présente dans la scène (arme de départ du personnage).</summary>
    public void Register(string weaponId, WeaponBase weapon, int level = 1)
    {
        _weaponNodes[weaponId] = weapon;
        _weaponLevels[weaponId] = level;
        ApplyWeaponStats(weaponId, level, weapon);
    }

    /// <summary>
    /// Crée l'arme sur le porteur. Le point de montage vient de <see cref="Mount"/> à défaut d'être
    /// passé explicitement — sans quoi une carte prise en jeu montait le niveau <b>sans jamais</b>
    /// faire apparaître l'arme, un défaut parfaitement muet.
    /// </summary>
    private void InstantiateWeapon(string weaponId, int level, Transform? mount)
    {
        mount ??= Mount != null ? Mount : Player.Instance?.transform;
        if (mount == null)
        {
            Debug.LogError($"[InventorySystem] aucun point de montage pour '{weaponId}'.");
            return;
        }

        var weapon = WeaponRegistry.Create(weaponId, mount);
        if (weapon == null) return;

        _weaponNodes[weaponId] = weapon;
        ApplyWeaponStats(weaponId, level, weapon);
    }

    /// <summary>Applique les statistiques du niveau à une arme ordinaire.</summary>
    private void ApplyWeaponStats(string weaponId, int level, WeaponBase weapon)
    {
        // Une fusion n'a pas de tableau de niveaux : elle suit sa propre règle.
        if (_appliedFusions.Contains(weaponId)) { ApplyFusionStats(weaponId, level, weapon); return; }

        if (!_weapons.TryGetValue(weaponId, out var def)) return;

        var stats = WeaponTable.StatsAt(def, level);
        float mult = Player.Instance?.Stats.DamageMultiplier ?? 1f;

        weapon.Configure(level);
        weapon.BaseDamage = stats.Damage * mult;
        weapon.BaseCooldown = stats.Cooldown;
    }

    /// <summary>
    /// Applique les statistiques d'une <b>fusion</b>. Sa valeur de fiche vient de sa propre classe —
    /// sa mécanique (rafale perforante, aura continue, essaim orbital) ne se décrit pas dans le
    /// tableau de niveaux — et c'est <see cref="WeaponFusion"/> qui la porte au niveau hérité.
    /// </summary>
    private void ApplyFusionStats(string fusionId, int level, WeaponBase weapon)
    {
        float mult = Player.Instance?.Stats.DamageMultiplier ?? 1f;
        weapon.Configure(level);
        weapon.BaseDamage = WeaponFusion.EffectiveDamage(weapon.SheetDamage, level, mult);
    }

    // ─── Passifs ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Acquiert le passif, ou le monte d'un niveau. Renvoie le nouveau niveau, ou 0 si l'action est
    /// refusée (passif inconnu, niveau maximum, statistique déjà à son plafond).
    /// </summary>
    public int AddOrUpgradePassive(string passiveId)
    {
        if (!_passiveDefs.TryGetValue(passiveId, out var def)) return 0;

        int current = PassiveLevelOf(passiveId);
        if (current >= def.MaxLevel) return 0;

        int next = current + 1;
        _passiveLevels[passiveId] = next;

        ApplyPassiveDelta(def, next);
        PassiveChanged?.Invoke(passiveId, next);
        return next;
    }

    /// <summary>
    /// Écrit dans les statistiques du joueur le delta du niveau atteint. <b>Le calcul du delta n'est
    /// pas ici</b> : il vient de <see cref="PassiveTable.DeltaFor"/> (amortissement compris), seule
    /// source de vérité partagée avec Godot et couverte par les tests.
    /// </summary>
    private void ApplyPassiveDelta(PassiveTable.PassiveDef def, int level)
    {
        var player = Player.Instance;
        if (player == null) return;
        var stats = player.Stats;

        switch (def.Id)
        {
            case "thermal_core":
                stats.DamageMultiplier += PassiveTable.DeltaFor(def, PassiveStat.DamageMultiplier, level);
                RefreshWeaponStats();
                break;

            case "reinforced_plating":
            {
                float hpGain = PassiveTable.DeltaFor(def, PassiveStat.MaxHp, level);
                if (hpGain > 0f)
                {
                    stats.MaxHp += hpGain;
                    // Le soin qui accompagne le gain passe par HealFlat : c'est le seul chemin qui
                    // applique les crans de saturation et journalise. Les PV MAX, eux, y échappent.
                    player.HealFlat(hpGain);
                }

                stats.DamageReduction = StatCaps.CapDamageReduction(
                    stats.DamageReduction + PassiveTable.DeltaFor(def, PassiveStat.DamageReduction, level));
                break;
            }

            case "servo_motors":
                stats.Speed = StatCaps.CapSpeed(
                    stats.Speed + PassiveTable.DeltaFor(def, PassiveStat.Speed, level));
                break;

            case "capacitor":
                stats.CooldownReduction = StatCaps.CapCooldownReduction(
                    stats.CooldownReduction + PassiveTable.DeltaFor(def, PassiveStat.CooldownReduction, level));
                RefreshWeaponStats();
                break;
        }
    }

    /// <summary>
    /// Ce passif ne rapporte-t-il plus rien ? Un passif dont la statistique est <b>au plafond</b> doit
    /// sortir du pool de cartes : le proposer encore, c'est offrir un choix mort au moment où le
    /// joueur en a le plus besoin (défaut corrigé côté Godot au §30).
    /// </summary>
    public bool IsPassiveSaturated(string passiveId)
    {
        var stats = Player.Instance?.Stats;
        if (stats == null) return false;

        return passiveId switch
        {
            "servo_motors" => stats.Speed >= StatCaps.MaxSpeed,
            "capacitor"    => stats.CooldownReduction >= StatCaps.MaxCooldownReduction,
            // Le Noyau Thermique (dégâts) et la Plaque Renforcée (PV) n'ont pas de plafond dur : ils
            // rapportent toujours quelque chose, de moins en moins.
            _              => false,
        };
    }

    // ─── Cartes de surcharge ──────────────────────────────────────────────────

    /// <summary>
    /// Applique une carte de <b>surcharge</b> (progression de fin de partie). Aucun plafond de niveau
    /// et aucun amortissement : ces cartes répondent à une menace non bornée, les brider les
    /// ramènerait au défaut qu'elles corrigent (GDD §33).
    /// </summary>
    public int ApplyOverload(string cardId)
    {
        var card = OverloadCards.ById(cardId);
        var player = Player.Instance;
        if (card == null || player == null) return 0;

        int takes = _overloadTakes.GetValueOrDefault(cardId, 0) + 1;
        _overloadTakes[cardId] = takes;

        var stats = player.Stats;
        if (card == OverloadCards.Plating)
        {
            stats.MaxHp += card.Delta;
            // Soigne d'autant : sans cela, la carte prise à 20 % de vie ne donne qu'une plus grande
            // barre, tout aussi vide.
            player.HealFlat(card.Delta);
        }
        else if (card == OverloadCards.Regen)
        {
            stats.HpRegenPerSecond += card.Delta;
        }
        else if (card == OverloadCards.Damage)
        {
            stats.DamageMultiplier += card.Delta;
            RefreshWeaponStats();
        }

        return takes;
    }

    /// <summary>
    /// Retire une arme du jeu.
    ///
    /// <para>⚠ <b>Ne jamais détruire aveuglément son <c>GameObject</c>.</b> L'arme de départ est un
    /// composant posé sur le joueur lui-même : détruire son objet reviendrait à <b>supprimer le
    /// joueur</b> au moment de forger sa première fusion. On ne détruit l'objet que s'il a été créé
    /// pour porter cette arme et rien d'autre.</para>
    /// </summary>
    private void RemoveWeapon(WeaponBase? weapon)
    {
        if (weapon == null) return;

        bool ownsItsObject = weapon.gameObject != Player.Instance?.gameObject
                          && (Mount == null || weapon.gameObject != Mount.gameObject);

        if (ownsItsObject) Destroy(weapon.gameObject);
        else               Destroy(weapon);
    }

    /// <summary>
    /// Recalcule dégâts et cadence de toutes les armes portées. Indispensable après un passif ou une
    /// surcharge : sans lui, le bonus ne s'appliquerait qu'aux armes acquises <b>ensuite</b>.
    /// </summary>
    private void RefreshWeaponStats()
    {
        foreach (var (id, weapon) in _weaponNodes)
            if (weapon != null) ApplyWeaponStats(id, LevelOf(id), weapon);
    }

    // ─── Fusions ──────────────────────────────────────────────────────────────

    /// <summary>Cette fusion est-elle déblocable en l'état ?</summary>
    public bool CanFuse(string fusionId)
    {
        if (_appliedFusions.Contains(fusionId)) return false;
        if (!_fusions.TryGetValue(fusionId, out var f)) return false;

        return WeaponFusion.CanFuse(
            LevelOf(f.RequiredWeapon), f.RequiredWeaponLevel, HasPassive(f.RequiredPassive));
    }

    /// <summary>
    /// Forge une fusion : retire l'arme source et installe la fusion <b>au niveau hérité</b>.
    /// Renvoie ce niveau, ou 0 si la fusion est refusée.
    /// </summary>
    /// <remarks>
    /// ⚠ Le niveau hérité est le cœur du correctif 1.21.0 : repartir de 1 effaçait tous les niveaux
    /// investis, et la perte était <b>définitive</b> puisque l'arme de base quitte le pool de cartes
    /// et que la fusion n'y entre pas. Verrouillé par <c>WeaponFusionTests</c>.
    /// </remarks>
    public int ApplyFusion(string fusionId, Transform? mount = null)
    {
        if (!CanFuse(fusionId)) return 0;
        if (!_fusions.TryGetValue(fusionId, out var f)) return 0;

        int inherited = WeaponFusion.InheritedLevel(LevelOf(f.Replaces));

        if (_weaponNodes.TryGetValue(f.Replaces, out var old))
        {
            RemoveWeapon(old);
            _weaponNodes.Remove(f.Replaces);
        }
        _weaponLevels.Remove(f.Replaces);

        _appliedFusions.Add(fusionId);
        _weaponLevels[fusionId] = inherited;

        // Toujours instancier — le point de montage est retrouvé à défaut d'être fourni. Ne créer la
        // fusion que si un mount était passé revenait à DÉTRUIRE l'arme source sans rien mettre à la
        // place : la carte la plus spectaculaire du jeu faisait perdre une arme.
        InstantiateWeapon(fusionId, inherited, mount);

        FusionApplied?.Invoke(fusionId, inherited);
        Debug.Log($"[InventorySystem] Fusion forgee : {fusionId} (niveau herite {inherited}).");
        return inherited;
    }

    /// <summary>Remet l'arsenal à zéro pour une nouvelle run.</summary>
    public void ResetForRun()
    {
        foreach (var w in _weaponNodes.Values) RemoveWeapon(w);

        _weaponNodes.Clear();
        _weaponLevels.Clear();
        _appliedFusions.Clear();
        _passiveLevels.Clear();
        _overloadTakes.Clear();
    }
}
