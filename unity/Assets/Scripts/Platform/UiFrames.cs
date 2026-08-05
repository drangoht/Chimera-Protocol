using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Accent d'un cadre — détermine sa couleur de liseré.</summary>
public enum FrameAccent { Cyan, Violet, Gold, Steel, Danger }

/// <summary>
/// Chargement et pose des cadres « plaque blindée » — chanfreins, biseau, rivets, découpés en neuf
/// zones.
///
/// <para>Il vit dans <c>Platform</c> et non dans <c>UiStyle</c> pour une raison de dépendances : le
/// HUD appartient à <c>Gameplay</c>, que <c>UI</c> référence déjà, et il a besoin des mêmes cadres.
/// Les laisser côté interface obligerait le HUD à charger ses textures à la main — c'est-à-dire à
/// rouvrir la porte au style ad hoc que la fabrique existe pour fermer.</para>
///
/// <para>⚠ Ces textures étaient importées, découpées et vérifiées au banc depuis le lot 5 sans
/// qu'<b>aucun écran ne les utilise</b> : la fabrique dessinait un liseré plat. Une vérification qui
/// n'observe pas le résultat final ne prouve rien.</para>
/// </summary>
public static class UiFrames
{
    private static readonly Dictionary<string, Sprite?> Cache = new();

    /// <summary>
    /// Charge un cadre par son nom de fichier. Le résultat est mis en cache, <b>y compris
    /// l'échec</b> : un cadre manquant est demandé par chaque bouton de chaque écran, et retenter le
    /// chargement coûterait un accès disque par bouton.
    /// </summary>
    public static Sprite? Get(string name)
    {
        if (Cache.TryGetValue(name, out var cached)) return cached;

        var sprite = Resources.Load<Sprite>("UiFrames/" + name);
        if (sprite == null)
            Debug.LogWarning($"[UiFrames] cadre introuvable : UiFrames/{name} — repli en liseré plat.");

        Cache[name] = sprite;
        return sprite;
    }

    /// <summary>Suffixe de fichier d'un accent. Le doré s'écrit « or », comme sous Godot.</summary>
    public static string Slug(FrameAccent accent) => accent switch
    {
        FrameAccent.Violet => "violet",
        FrameAccent.Gold   => "or",
        FrameAccent.Danger => "danger",
        _                  => "cyan",
    };

    /// <summary>Cadre de bouton d'un accent, dans sa variante normale ou de focus.</summary>
    public static Sprite? Button(FrameAccent accent, bool focus = false)
        => Get($"ui_frame_button_{Slug(accent)}{(focus ? "_focus" : "")}");

    /// <summary>
    /// Applique un cadre 9 zones à une image. Renvoie <c>false</c> si la texture manque, à charge de
    /// l'appelant de dessiner son repli — une texture absente donnerait sinon des panneaux
    /// invisibles, c'est-à-dire des écrans qui paraissent vides.
    /// </summary>
    /// <remarks>
    /// La teinte reste <b>blanche</b> : ces PNG portent déjà leur couleur d'accent. Les teinter une
    /// seconde fois donnait des cadres saturés et faux.
    /// </remarks>
    public static bool Apply(Image image, string frameName)
    {
        var sprite = Get(frameName);
        if (sprite == null) return false;

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;

        // Sans cela, une Image dont le sprite est plus petit que ses bordures cumulées disparaît
        // purement et simplement — c'est le cas de tout bouton de moins de 32 px de large.
        image.fillCenter = true;
        return true;
    }
}
