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
    /// <remarks>
    /// ⚠ <b>La maquette ne se rétrécit PAS sur une petite dalle</b>, et c'est un choix vérifié sur
    /// un vrai téléphone (Pixel 9, 2026-08-14). Grossir l'interface pour agrandir les cibles
    /// tactiles paraissait juste sur le papier — un bouton de menu ne fait que 22 pixels logiques
    /// en paysage — mais un pixel logique de téléphone est minuscule : à l'échelle doublée, les
    /// textes <b>sortaient de leurs cadres</b> et le menu principal devenait énorme, y compris sur
    /// le premier écran du jeu. <b>Le raisonnement en pixels logiques, mené depuis un poste de
    /// bureau, ne dit rien de la taille réelle</b> — seul un vrai téléphone tranche.
    /// </remarks>
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

    /// <summary>
    /// Échelle qu'un <c>CanvasScaler</c> réglé à mi-chemin applique pour une maquette donnée.
    /// </summary>
    public static float ScaleFactor(Vector2 reference)
        => Mathf.Sqrt((Screen.width / Mathf.Max(1f, reference.x)) *
                      (Screen.height / Mathf.Max(1f, reference.y)));

    /// <summary>
    /// Convertit une longueur en <b>pixels écran</b> vers les unités d'un canevas de maquette
    /// <paramref name="reference"/>.
    /// </summary>
    /// <remarks>
    /// Sert à réserver, dans une mise en page exprimée en unités de maquette, la place d'un élément
    /// mesuré en pixels réels — typiquement les contrôles tactiles, qui se dimensionnent en pouces
    /// (cf. <c>UI/TouchHud</c>). Sans cette conversion, les deux repères se croisent et l'un
    /// recouvre l'autre à une taille d'écran et pas à une autre.
    /// </remarks>
    public static float PixelsToCanvas(float pixels, Vector2 reference)
        => pixels / Mathf.Max(0.0001f, ScaleFactor(reference));

}
