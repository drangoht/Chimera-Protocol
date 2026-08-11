using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Résout un jeu d'animations par identifiant, à l'exécution (Lot 4 bis).
///
/// <para><b>Le bug que cette classe corrige.</b> Les 40 jeux d'animations étaient bien convertis et
/// présents dans le projet, mais rangés hors de <c>Resources/</c> : donc introuvables à l'exécution.
/// Résultat en jeu — des ennemis parfaitement fonctionnels qui se déplaçaient, frappaient et
/// mouraient, <b>totalement invisibles</b>. Aucune erreur, aucun avertissement.</para>
///
/// <para>C'est le mode de défaillance typique du portage d'assets : la chaîne de conversion réussit,
/// et c'est le <i>dernier maillon</i> — l'accès à l'exécution — qui manque.</para>
/// </summary>
public static class SpriteFramesLibrary
{
    private const string ResourceFolder = "SpriteFrames/";

    private static readonly Dictionary<string, SpriteFramesAsset?> _cache = new();

    /// <summary>Charge un jeu d'animations par identifiant (ex. <c>aether_golem</c>).</summary>
    public static SpriteFramesAsset? Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_cache.TryGetValue(id, out var cached)) return cached;

        var asset = Resources.Load<SpriteFramesAsset>(ResourceFolder + id);
        _cache[id] = asset;   // on mémorise aussi l'absence, pour ne pas retenter à chaque spawn
        return asset;
    }

    /// <summary>
    /// Résout un <c>framesPath</c> de Godot en jeu d'animations.
    /// <c>res://assets/sprites/enemies/x/x_frames.tres</c> → identifiant <c>x</c>.
    /// </summary>
    public static SpriteFramesAsset? FromGodotPath(string framesPath)
    {
        if (string.IsNullOrEmpty(framesPath)) return null;

        int slash = framesPath.LastIndexOf('/');
        string file = slash >= 0 ? framesPath.Substring(slash + 1) : framesPath;

        if (file.EndsWith(".tres", System.StringComparison.Ordinal))
            file = file.Substring(0, file.Length - 5);
        if (file.EndsWith("_frames", System.StringComparison.Ordinal))
            file = file.Substring(0, file.Length - 7);

        return Get(file);
    }

    /// <summary>
    /// Jeu d'animations d'un ennemi, avec <b>repli</b>. Onze ennemis du bestiaire n'ont pas de
    /// <c>framesPath</c> — sous Godot, ils portaient leur sprite dans leur propre scène. Sans repli
    /// ils seraient invisibles, ce qui est pire qu'un visuel approximatif.
    /// </summary>
    public static SpriteFramesAsset? ForEnemy(string enemyId, string framesPath)
        => FromGodotPath(framesPath)
        ?? Get(enemyId)
        ?? (Aliases.TryGetValue(enemyId, out string? alias) ? Get(alias) : null)
        ?? Get(FallbackId);

    /// <summary>
    /// Identifiants dont le jeu d'animations porte un <b>autre nom</b>.
    ///
    /// <para>⚠ Ces quatre-là sont la faune de base du jeu — celle qu'on voit dès la première
    /// seconde. Aucun n'a de <c>framesPath</c> (sous Godot, ils portaient leur sprite dans leur
    /// propre scène), et leur identifiant ne correspond pas au nom de leur asset : tous les quatre
    /// tombaient donc sur le <b>repli</b>, et le Drone, la Sentinelle et le Colosse s'affichaient
    /// avec le sprite de l'Essaim de Rouille. Trois ennemis aux comportements distincts,
    /// indiscernables à l'écran — et rien ne le signalait, puisqu'un sprite <i>était</i> bien
    /// affiché.</para>
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> Aliases =
        new(System.StringComparer.Ordinal)
        {
            { "rust_swarm",         "rustswarm" },
            { "corrupted_drone",    "drone"     },
            { "corrupted_sentinel", "sentinel"  },
            { "grafted_colossus",   "colossus"  },
        };

    /// <summary>Jeu d'animations utilisé quand aucun autre ne convient.</summary>
    public const string FallbackId = "rustswarm";
}
