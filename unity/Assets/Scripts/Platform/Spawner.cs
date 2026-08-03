using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Instanciation de prefabs par chemin logique, remplaçant le couple
/// <c>GD.Load&lt;PackedScene&gt;("res://…") + Instantiate()</c> de Godot
/// (65 sites d'appel — docs/UNITY_MIGRATION_PLAN.md §4.2).
///
/// <para><b>Les chemins Godot sont conservés tels quels.</b> Le code écrit
/// <c>Spawner.Spawn("res://scenes/entities/XpOrb.tscn")</c> exactement comme avant, et la
/// correspondance vers <c>Resources/Prefabs/entities/XpOrb</c> se fait ici. Les 65 sites se portent
/// donc sans réfléchir, et — plus important — les chaînes restent <b>identiques entre les deux
/// moteurs</b>, ce qui permet de comparer les deux bases pendant tout le portage. Réécrire les
/// chemins à la main aurait été 65 occasions de se tromper sans que rien ne le signale.</para>
///
/// <para><b>Pas de recyclage d'objets (pooling).</b> Godot instancie et libère à chaque fois ; le
/// périmètre est la parité stricte. Le prototype de charge a mesuré 0,168 ms par pas pour 300
/// entités, donc rien ne le justifie aujourd'hui. Si le besoin apparaît, il se traitera ici, en un
/// seul point.</para>
/// </summary>
public static class Spawner
{
    private const string ResourceRoot = "Prefabs/";

    private static readonly Dictionary<string, GameObject> _cache = new();

    /// <summary>
    /// Traduit un chemin de scène Godot en chemin de ressource Unity.
    /// <c>res://scenes/entities/XpOrb.tscn</c> → <c>Prefabs/entities/XpOrb</c>.
    /// </summary>
    public static string ToResourcePath(string godotPath)
    {
        string p = godotPath;
        if (p.StartsWith("res://", System.StringComparison.Ordinal)) p = p.Substring(6);
        if (p.StartsWith("scenes/", System.StringComparison.Ordinal)) p = p.Substring(7);
        if (p.EndsWith(".tscn", System.StringComparison.Ordinal)) p = p.Substring(0, p.Length - 5);
        return ResourceRoot + p;
    }

    /// <summary>
    /// Charge un prefab (mis en cache) — équivalent de <c>GD.Load&lt;PackedScene&gt;</c>.
    /// Renvoie <c>null</c> et journalise une erreur si le prefab est introuvable : un spawn
    /// silencieusement absent est un bug qu'on ne trouve qu'en jouant.
    /// </summary>
    public static GameObject? Load(string godotPath)
    {
        if (_cache.TryGetValue(godotPath, out var cached)) return cached;

        string resPath = ToResourcePath(godotPath);
        var prefab = Resources.Load<GameObject>(resPath);

        if (prefab == null)
        {
            Debug.LogError($"[Spawner] prefab introuvable : '{godotPath}' -> '{resPath}'. " +
                           "Verifier qu'il existe sous Assets/Resources/.");
            return null;
        }

        _cache[godotPath] = prefab;
        return prefab;
    }

    /// <summary>Instancie un prefab à une position donnée.</summary>
    public static GameObject? Spawn(string godotPath, Vector2 position = default,
                                    Transform? parent = null)
    {
        var prefab = Load(godotPath);
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab, position, Quaternion.identity, parent);
        return go;
    }

    /// <summary>
    /// Instancie et renvoie directement le composant attendu — équivalent de
    /// <c>Instantiate&lt;T&gt;()</c>. Journalise si le composant manque sur le prefab.
    /// </summary>
    public static T? Spawn<T>(string godotPath, Vector2 position = default,
                              Transform? parent = null) where T : Component
    {
        var go = Spawn(godotPath, position, parent);
        if (go == null) return null;

        var component = go.GetComponent<T>();
        if (component == null)
        {
            Debug.LogError($"[Spawner] '{godotPath}' instancie, mais sans composant " +
                           $"{typeof(T).Name}.");
            Object.Destroy(go);
            return null;
        }
        return component;
    }

    /// <summary>
    /// Vide le cache de prefabs — à appeler sur un changement de scène si les prefabs proviennent
    /// de bundles rechargés. Sans effet sur les instances déjà créées.
    /// </summary>
    public static void ClearCache() => _cache.Clear();
}
