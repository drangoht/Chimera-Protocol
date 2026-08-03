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
    private readonly HashSet<string> _passives = new(StringComparer.Ordinal);
    private readonly HashSet<string> _appliedFusions = new(StringComparer.Ordinal);

    private Dictionary<string, WeaponTable.WeaponDef> _weapons = new();
    private Dictionary<string, WeaponTable.FusionDef> _fusions = new();

    /// <summary>Émis à l'acquisition ou la montée d'une arme : <c>(id, niveau)</c>.</summary>
    public event Action<string, int>? WeaponChanged;

    /// <summary>Émis quand une fusion est forgée : <c>(idFusion, niveauHérité)</c>.</summary>
    public event Action<string, int>? FusionApplied;

    /// <summary>Armes actuellement portées, fusions comprises.</summary>
    public int WeaponCount => _weaponLevels.Count;

    /// <summary>Fusions déjà forgées.</summary>
    public IReadOnlyCollection<string> AppliedFusions => _appliedFusions;

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
        Debug.Log($"[InventorySystem] {_weapons.Count} armes et {_fusions.Count} fusions chargees.");
    }

    // ─── Armes ────────────────────────────────────────────────────────────────

    /// <summary>Niveau d'une arme, ou 0 si elle n'est pas portée.</summary>
    public int LevelOf(string weaponId) => _weaponLevels.GetValueOrDefault(weaponId, 0);

    /// <summary>Le joueur porte-t-il cette arme ?</summary>
    public bool Has(string weaponId) => _weaponLevels.ContainsKey(weaponId);

    /// <summary>Déclare un passif acquis — condition de déblocage des fusions.</summary>
    public void AddPassive(string passiveId) => _passives.Add(passiveId);

    /// <summary>Le joueur possède-t-il ce passif ?</summary>
    public bool HasPassive(string passiveId) => _passives.Contains(passiveId);

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
        else if (mount != null)
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

    private void InstantiateWeapon(string weaponId, int level, Transform mount)
    {
        var prefab = Spawner.Load($"res://scenes/weapons/{weaponId}.tscn");
        if (prefab == null) return;

        var go = Instantiate(prefab, mount.position, Quaternion.identity, mount);
        go.SetActive(true);

        var weapon = go.GetComponent<WeaponBase>();
        if (weapon == null)
        {
            Debug.LogError($"[InventorySystem] '{weaponId}' n'a pas de composant WeaponBase.");
            Destroy(go);
            return;
        }

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
            if (old != null) Destroy(old.gameObject);
            _weaponNodes.Remove(f.Replaces);
        }
        _weaponLevels.Remove(f.Replaces);

        _appliedFusions.Add(fusionId);
        _weaponLevels[fusionId] = inherited;

        if (mount != null) InstantiateWeapon(fusionId, inherited, mount);

        FusionApplied?.Invoke(fusionId, inherited);
        Debug.Log($"[InventorySystem] Fusion forgee : {fusionId} (niveau herite {inherited}).");
        return inherited;
    }

    /// <summary>Remet l'arsenal à zéro pour une nouvelle run.</summary>
    public void ResetForRun()
    {
        foreach (var w in _weaponNodes.Values)
            if (w != null) Destroy(w.gameObject);

        _weaponNodes.Clear();
        _weaponLevels.Clear();
        _appliedFusions.Clear();
        _passives.Clear();
    }
}
