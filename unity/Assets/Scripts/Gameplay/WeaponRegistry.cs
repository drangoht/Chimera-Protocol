using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Correspondance <c>id d'arme → composant</c>, et fabrication d'une arme montée sur le joueur
/// (Lot 5 bis, « la run complète »).
///
/// <para><b>Pourquoi un registre plutôt qu'un prefab par arme.</b> Sous Godot, chaque arme était une
/// <c>.tscn</c> chargée par <c>res://scenes/weapons/&lt;id&gt;.tscn</c>. Le port a conservé l'appel,
/// mais ces prefabs n'existaient pas : <see cref="InventorySystem"/> demandait 21 fichiers absents, et
/// le seul symptôme était une carte prise <b>sans effet</b> — l'arme montait en niveau dans le
/// dictionnaire, aucun objet n'apparaissait. Ici, une arme est <b>uniquement</b> un composant : il n'y
/// a plus rien à tenir synchronisé entre un id, un fichier et une classe.</para>
///
/// <para><b>Cette table est la liste de référence de l'arsenal.</b> Le banc s'en sert aussi
/// (critère « les 21 armes tirent ») : deux listes séparées auraient divergé au premier ajout, et une
/// arme absente du banc est une arme dont on ne sait pas si elle tire.</para>
/// </summary>
public static class WeaponRegistry
{
    /// <summary>Les 12 armes de base et les 9 fusions, dans l'ordre de <c>weapons.json</c>.</summary>
    public static readonly IReadOnlyList<(string Id, Type Type)> All = new (string, Type)[]
    {
        ("impulse_cannon",   typeof(ImpulseCannon)),
        ("plasma_blade",     typeof(PlasmaBlade)),
        ("drone_swarm",      typeof(DroneSwarm)),
        ("overload_field",   typeof(OverloadField)),
        ("tesla_coil",       typeof(TeslaCoil)),
        ("scatter_volley",   typeof(ScatterVolley)),
        ("glaive",           typeof(Glaive)),
        ("seeker_swarm",     typeof(SeekerSwarm)),
        ("cryo_lance",       typeof(CryoLance)),
        ("pyre_stream",      typeof(PyreStream)),
        ("vector_lance",     typeof(VectorLance)),
        ("singularity",      typeof(Singularity)),

        ("fusion_blade",     typeof(FusionBlade)),
        ("rail_overcharged", typeof(RailOvercharged)),
        ("orbital_swarm",    typeof(OrbitalSwarm)),
        ("overload_aegis",   typeof(OverloadAegis)),
        ("ionic_storm",      typeof(IonicStorm)),
        ("solar_column",     typeof(SolarColumn)),
        ("hornet_swarm",     typeof(HornetSwarm)),
        ("vector_beam",      typeof(VectorBeam)),
        ("frost_veil",       typeof(FrostVeil)),
    };

    private static readonly Dictionary<string, Type> _byId = BuildIndex();
    private static readonly Dictionary<Type, string> _byType = BuildReverseIndex();

    private static Dictionary<string, Type> BuildIndex()
    {
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var (id, type) in All) map[id] = type;
        return map;
    }

    private static Dictionary<Type, string> BuildReverseIndex()
    {
        var map = new Dictionary<Type, string>();
        foreach (var (id, type) in All) map[type] = id;
        return map;
    }

    /// <summary>
    /// Identifiant d'une arme d'après son type exact.
    ///
    /// <para><b>Le type exact, jamais la classe de base.</b> Trois armes en héritent d'une autre
    /// (<c>OverloadAegis : OverloadField</c>, <c>FusionBlade : PlasmaBlade</c>,
    /// <c>OrbitalSwarm : DroneSwarm</c>) : remonter la chaîne d'héritage donnerait à l'Égide
    /// l'identité du Champ de Surcharge, et toute règle indexée par identifiant — le son de tir, par
    /// exemple — s'appliquerait à la mauvaise arme.</para>
    /// </summary>
    public static string? IdOf(Type type)
        => type != null && _byType.TryGetValue(type, out var id) ? id : null;

    /// <summary>Composant correspondant à un id, ou <c>null</c> si l'id n'est pas une arme portée.</summary>
    public static Type? TypeOf(string weaponId)
        => _byId.TryGetValue(weaponId, out var type) ? type : null;

    /// <summary>Cet id désigne-t-il une arme connue du moteur ?</summary>
    public static bool Knows(string weaponId) => _byId.ContainsKey(weaponId);

    /// <summary>
    /// Crée l'arme sous <paramref name="mount"/> (le joueur) et lui injecte ses prefabs de projectile.
    /// Renvoie <c>null</c> si l'id est inconnu — et le <b>journalise</b> : une arme silencieusement
    /// absente est un défaut qu'on ne trouve qu'en jouant.
    /// </summary>
    public static WeaponBase? Create(string weaponId, Transform mount)
    {
        var type = TypeOf(weaponId);
        if (type == null)
        {
            Debug.LogError($"[WeaponRegistry] arme inconnue : '{weaponId}'.");
            return null;
        }

        var go = new GameObject("W_" + weaponId);
        go.transform.SetParent(mount, false);

        var weapon = (WeaponBase)go.AddComponent(type);
        InjectProjectilePrefabs(weapon);
        return weapon;
    }

    /// <summary>
    /// Injecte les prefabs attendus par chaque famille d'arme. Une arme à projectile <b>sans</b> son
    /// prefab ne lève aucune erreur : elle épuise sa recharge et ne tire rien.
    ///
    /// <para>Un prefab déjà posé (dans la scène, ou par un banc) n'est <b>jamais écrasé</b> : sinon
    /// un chargement raté depuis <c>Resources</c> rendrait muette une arme qui fonctionnait.</para>
    /// </summary>
    public static void InjectProjectilePrefabs(WeaponBase weapon)
    {
        switch (weapon)
        {
            case ImpulseCannon c: c.BulletPrefab  ??= ProjectilePrefabs.Bullet;  break;
            case ScatterVolley s: s.BulletPrefab  ??= ProjectilePrefabs.Bullet;  break;
            case VectorLance v:   v.BulletPrefab  ??= ProjectilePrefabs.Bullet;  break;
            case SeekerSwarm k:   k.MissilePrefab ??= ProjectilePrefabs.Missile; break;
            case Glaive g:        g.GlaivePrefab  ??= ProjectilePrefabs.Glaive;  break;
        }
    }
}

/// <summary>
/// Prefabs de projectiles partagés, chargés à la demande depuis <c>Resources/</c>.
///
/// <para>Ils sont volontairement résolus <b>par chemin Godot</b> (<see cref="Spawner"/>) : les mêmes
/// chaînes qu'à l'origine, donc aucune traduction à faire à la main.</para>
/// </summary>
public static class ProjectilePrefabs
{
    private static GameObject? _bullet, _missile, _glaive;

    public static GameObject? Bullet  => _bullet  ??= Spawner.Load("res://scenes/entities/Bullet.tscn");
    public static GameObject? Missile => _missile ??= Spawner.Load("res://scenes/entities/Missile.tscn");
    public static GameObject? Glaive  => _glaive  ??= Spawner.Load("res://scenes/entities/Glaive.tscn");

    /// <summary>Injecte des prefabs fabriqués à la volée — réservé aux bancs.</summary>
    public static void Override(GameObject? bullet, GameObject? missile, GameObject? glaive)
    {
        _bullet = bullet; _missile = missile; _glaive = glaive;
    }
}
