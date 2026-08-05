using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Réglage d'un canevas d'interface — <b>source unique</b> de quatre réglages qui étaient recopiés
/// dans les onze écrans.
///
/// <para>Il vit dans <c>Platform</c> et non dans <c>UiStyle</c> pour une raison de dépendances : le
/// HUD appartient à <c>Gameplay</c>, que <c>UI</c> référence déjà — l'y placer créerait un cycle
/// d'assemblages, et le HUD garderait sa copie des réglages.</para>
///
/// <para><b><c>pixelPerfect</c> est le réglage qui rend le texte net.</b> uGUI place ses sommets en
/// coordonnées flottantes : une colonne centrée, une hauteur de bouton impaire ou une marge de mise
/// en page suffisent à poser une ligne de texte sur un demi-pixel, et la police est alors
/// <b>rééchantillonnée</b> — les glyphes en ressortent baveux, sans qu'aucun réglage de police n'y
/// change quoi que ce soit. <c>pixelPerfect</c> aligne les sommets sur la grille de l'écran. Le
/// défaut est invisible au code et saute aux yeux sur une capture agrandie.</para>
/// </summary>
public static class UiCanvas
{
    /// <summary>Résolution de référence de toute l'interface.</summary>
    public static readonly Vector2 Reference = new(1920f, 1080f);

    /// <summary>
    /// Règle le canevas porté par <paramref name="canvasGo"/>.
    /// </summary>
    /// <param name="sortingOrder">
    /// Ordre d'empilement. Zéro laisse la valeur par défaut : le menu et le HUD sont au fond, les
    /// modales par-dessus.
    /// </param>
    public static void Configure(GameObject canvasGo, int sortingOrder = 0)
    {
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        if (sortingOrder != 0) canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Reference;

        // Ni la largeur ni la hauteur seules : à 0, une fenêtre plus haute que 16/9 rognerait le bas
        // des écrans ; à 1, une fenêtre plus large en rognerait les côtés.
        scaler.matchWidthOrHeight = 0.5f;

        // ⚠ 100 comme le PPU d'import des textures d'interface. Une Image met ses bordures 9-slice à
        // l'échelle de referencePixelsPerUnit / spritePixelsPerUnit : laisser diverger les deux
        // valeurs multiplierait par cent les chanfreins des cadres « plaque blindée ».
        scaler.referencePixelsPerUnit = 100f;
    }
}
