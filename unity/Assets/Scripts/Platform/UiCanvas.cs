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
    /// <param name="enlargeForTouch">
    /// Grossir l'interface sur une petite dalle pour qu'elle reste touchable ?
    /// </param>
    /// <remarks>
    /// ⚠ <b>À laisser à <c>false</c> pour le HUD, et pour lui seul.</b> Le grossissement paie des
    /// cibles tactiles avec de la surface d'écran — c'est un bon marché pour un menu, dont la
    /// surface ne sert qu'à lui. Le HUD, lui, ne contient <b>aucune cible</b> et se superpose à
    /// l'arène : l'y appliquer double la place que prennent la barre de vie et l'arsenal au moment
    /// précis où l'écran est le plus petit. Sur un téléphone, le panneau de vitalité mangeait un
    /// tiers de la largeur du champ de bataille.
    /// </remarks>
    public static void Configure(GameObject canvasGo, int sortingOrder = 0,
                                 bool enlargeForTouch = true)
    {
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        if (sortingOrder != 0) canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = enlargeForTouch ? ReferenceFor(Screen.width, Screen.height)
                                                    : Reference;

        // Ni la largeur ni la hauteur seules : à 0, une fenêtre plus haute que 16/9 rognerait le bas
        // des écrans ; à 1, une fenêtre plus large en rognerait les côtés.
        scaler.matchWidthOrHeight = 0.5f;

        // ⚠ 100 comme le PPU d'import des textures d'interface. Une Image met ses bordures 9-slice à
        // l'échelle de referencePixelsPerUnit / spritePixelsPerUnit : laisser diverger les deux
        // valeurs multiplierait par cent les chanfreins des cadres « plaque blindée ».
        scaler.referencePixelsPerUnit = 100f;

        // La fenêtre d'un navigateur mobile change de taille toute seule — voir ScreenSizeWatcher.
        // Inutile sur un canevas non grossi : sa référence ne dépend pas de la taille de l'écran.
        if (enlargeForTouch && canvasGo.GetComponent<ScreenSizeWatcher>() == null)
            canvasGo.AddComponent<ScreenSizeWatcher>().Bind(scaler);
    }

    /// <summary>
    /// Résolution de référence à employer pour une fenêtre donnée : la maquette, rétrécie sur une
    /// petite dalle pour que l'interface y reste touchable au doigt.
    /// </summary>
    /// <remarks>
    /// <para>Le calcul vit dans <c>Rules/TouchZones.UiEnlargement</c> — pur et testé. Ici, seule la
    /// division par le facteur : réduire la <i>référence</i> plutôt que grossir chaque élément traite
    /// les onze écrans d'un coup, et n'a d'effet que là où le problème existe.</para>
    ///
    /// <para>⚠ Aucun test ne peut valider ce réglage jusqu'au bout : il se juge <b>sur image</b>, à
    /// la taille d'un téléphone. C'est la leçon « une UI ne se juge pas au code ».</para>
    /// </remarks>
    public static Vector2 ReferenceFor(float screenWidth, float screenHeight)
        => Reference / TouchZones.UiEnlargement(screenWidth, screenHeight, Reference.x, Reference.y);

    /// <summary>
    /// De combien la maquette est <b>rétrécie</b> par rapport à 1920 × 1080. 1 sur un écran de
    /// bureau, jusqu'à <c>TouchZones.MaxUiEnlargement</c> sur un téléphone.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>C'est aussi, exactement, le facteur de retour à la ligne.</b> Grossir l'interface
    /// et élargir le canevas sont le <i>même</i> réglage pris dans les deux sens : la largeur du
    /// canevas en unités vaut toujours <c>largeur d'écran ÷ échelle</c>. Un texte qui tenait sur une
    /// ligne à 1920 unités en prend donc jusqu'à deux fois plus ici — et <b>toute mise en page à
    /// hauteurs de ligne fixes se chevauche</b>. C'est arrivé du premier coup sur l'écran de choix du
    /// niveau, où la disposition documente pourtant « la place de ses deux lignes possibles ».</para>
    ///
    /// <para>Un écran concerné multiplie donc ses hauteurs de texte par ce facteur. Le lire ici
    /// plutôt que de recalculer l'échelle sur place évite que les deux dérivent.</para>
    /// </remarks>
    public static float Narrowing()
        => TouchZones.UiEnlargement(Screen.width, Screen.height, Reference.x, Reference.y);

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

    /// <summary>
    /// Taille d'un panneau posé en unités absolues, <b>bornée par le canevas courant</b>.
    /// </summary>
    /// <param name="desired">Taille dessinée pour la maquette 1920 × 1080.</param>
    /// <param name="margin">Marge laissée de chaque côté, en unités de canevas.</param>
    /// <remarks>
    /// <para><b>Ce qui rend cette borne nécessaire.</b> Le grossissement de l'interface sur petite
    /// dalle (<see cref="ReferenceFor"/>) rétrécit la <i>maquette</i> : à facteur 2, le canevas ne
    /// fait plus que 960 × 540 unités. Or l'écran de montée de niveau pose son panneau à
    /// <b>1420 × 680</b>, l'écran des options à 980 × 860 — des tailles choisies pour la maquette
    /// pleine. Sans borne, ils déborderaient de moitié : le joueur verrait la carte du milieu et
    /// devinerait les deux autres. <b>Un panneau tronqué serait un défaut pire que des boutons
    /// petits</b>, et c'est ce risque qui plafonnait le grossissement à un niveau presque inutile.
    /// </para>
    ///
    /// <para>Borner ici plutôt que baisser le plafond marche parce que ces panneaux ont un contenu
    /// <b>élastique</b> : la rangée de cartes est un <c>HorizontalLayoutGroup</c> qui répartit la
    /// largeur disponible, les listes défilent. Réduire le cadre y réduit les cartes, il ne les coupe
    /// pas.</para>
    ///
    /// <para>⚠ Lue à la <b>construction</b> du panneau, donc à sa première ouverture. C'est
    /// suffisant — un panneau est centré, et le seul changement de taille fréquent en web (la barre
    /// d'URL qui se rétracte) vaut environ 10 % de hauteur. Un panneau qui devrait se redimensionner
    /// en cours de vie demanderait de le reconstruire, ce que sa file de modales n'admet pas.</para>
    /// </remarks>
    public static Vector2 PanelSize(Vector2 desired, float margin = 32f)
    {
        Vector2 canvas = ReferenceFor(Screen.width, Screen.height);

        return new Vector2(
            Mathf.Min(desired.x, Mathf.Max(160f, canvas.x - margin * 2f)),
            Mathf.Min(desired.y, Mathf.Max(120f, canvas.y - margin * 2f)));
    }

    /// <summary>
    /// Suit les changements de taille de fenêtre et remet la référence à jour.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Indispensable en web, et invisible ailleurs.</b> Sur un téléphone, la taille du canevas
    /// change sans que le joueur touche à rien : la barre d'URL se rétracte au premier glissement, le
    /// clavier virtuel s'ouvre, l'appareil pivote. Un <c>CanvasScaler</c> réglé une seule fois à la
    /// construction garderait la référence calculée pour la taille de départ — l'interface
    /// resterait minuscule après le tout premier geste, c'est-à-dire exactement au moment où le
    /// joueur essaie de s'en servir.
    /// </remarks>
    private sealed class ScreenSizeWatcher : MonoBehaviour
    {
        private CanvasScaler? _scaler;
        private int _width;
        private int _height;

        internal void Bind(CanvasScaler scaler)
        {
            _scaler = scaler;
            _width = Screen.width;
            _height = Screen.height;
        }

        private void Update()
        {
            if (_scaler == null) return;
            if (Screen.width == _width && Screen.height == _height) return;

            _width = Screen.width;
            _height = Screen.height;
            _scaler.referenceResolution = ReferenceFor(_width, _height);
        }
    }
}
