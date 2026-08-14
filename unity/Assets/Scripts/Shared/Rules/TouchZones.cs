using System;

/// <summary>
/// Découpage de l'écran tactile : quelle partie de la dalle appartient au stick, au bouton d'esquive,
/// au bouton de pause. Logique pure — aucune dépendance moteur, donc vérifiable sans téléphone.
///
/// <para><b>Pourquoi une règle et non des constantes dans le HUD.</b> Ces zones sont lues deux fois,
/// par deux couches qui ne se parlent pas : le <i>dessin</i> (où poser le bouton) et la <i>lecture</i>
/// (ce doigt appuie-t-il dessus ?). Les laisser diverger produit exactement le défaut le plus
/// coûteux du tactile — un bouton qui se voit et ne répond pas, ou qui répond à côté. Un seul jeu de
/// nombres, consommé des deux côtés, rend l'écart impossible.</para>
///
/// <para><b>Repère : pixels écran, origine en bas à gauche</b>, comme <see cref="VirtualStick"/>.</para>
/// </summary>
public static class TouchZones
{
    /// <summary>
    /// Fraction gauche de l'écran réservée au joystick. Tout doigt qui s'y pose et n'est pris par
    /// aucun bouton crée ou pilote le stick.
    /// </summary>
    /// <remarks>
    /// La moitié exacte, et non un coin : le stick étant <b>flottant</b>, sa zone n'est pas l'endroit
    /// où il est dessiné mais l'endroit où le joueur a le droit de le faire naître. La restreindre à
    /// un quart en bas à gauche annulerait tout le bénéfice du flottant — le pouce devrait de nouveau
    /// viser.
    /// </remarks>
    public const float StickWidthFraction = 0.5f;

    /// <summary>
    /// Bandeau supérieur soustrait à la zone du stick, en fraction de la hauteur.
    /// </summary>
    /// <remarks>
    /// Le HUD y affiche la vie, le niveau et l'arsenal. Un pouce qui s'y pose ne veut pas se déplacer,
    /// il vient de manquer un bouton ; y faire naître un stick ferait partir le joueur au moment
    /// précis où il regarde ailleurs.
    /// </remarks>
    public const float TopBandFraction = 0.16f;

    /// <summary>Rayon du bouton d'esquive, en fraction de la <b>hauteur</b> de l'écran.</summary>
    /// <remarks>
    /// <para>De la hauteur et non de la largeur : en paysage, la hauteur est la dimension courte,
    /// c'est donc elle qui décide de la place réellement disponible sous le pouce. Un bouton
    /// dimensionné sur la largeur deviendrait énorme sur une tablette et minuscule sur un téléphone
    /// — l'inverse de ce qu'il faut.</para>
    ///
    /// <para>11 % de la hauteur vaut environ 10 mm sur un téléphone en paysage, soit la cible
    /// tactile confortable admise (9 mm). Le plancher en pixels de <see cref="DashRadius"/> couvre
    /// les dalles très basses.</para>
    /// </remarks>
    public const float DashRadiusFraction = 0.11f;

    /// <summary>Rayon minimal du bouton d'esquive, en pixels.</summary>
    public const float MinButtonRadiusPx = 44f;

    /// <summary>
    /// Multiplicateur appliqué au rayon <b>sensible</b> du bouton d'esquive par rapport à son rayon
    /// <b>dessiné</b>.
    /// </summary>
    /// <remarks>
    /// Le doigt masque ce qu'il touche : le joueur vise le bouton qu'il a vu il y a une demi-seconde,
    /// pas celui qu'il voit. Une cible sensible plus large que le dessin absorbe cette erreur, et
    /// c'est la correction la plus rentable du tactile. Elle reste bornée — au-delà, la moitié droite
    /// entière déclencherait l'esquive, y compris quand le joueur repose simplement sa main.
    /// </remarks>
    public const float DashTouchSlop = 1.5f;

    /// <summary>Marge des boutons au bord de l'écran, en fraction de la hauteur.</summary>
    /// <remarks>
    /// Un bouton collé au bord est en partie hors de portée sur un écran à coins arrondis, et se
    /// trouve dans la bande où le navigateur mobile capte les gestes système (retour, barre
    /// d'onglets). Une marge d'un rayon environ met la cible hors de ces deux pièges.
    /// </remarks>
    public const float EdgeMarginFraction = 0.06f;

    /// <summary>Rayon dessiné du bouton d'esquive, en pixels.</summary>
    public static float DashRadius(float screenHeight)
        => Math.Max(MinButtonRadiusPx, DashRadiusFraction * Math.Max(1f, screenHeight));

    /// <summary>Centre du bouton d'esquive — bas à droite, sous le pouce droit.</summary>
    public static (float X, float Y) DashCenter(float screenWidth, float screenHeight)
    {
        float margin = EdgeMarginFraction * Math.Max(1f, screenHeight);
        float radius = DashRadius(screenHeight);

        return (screenWidth - margin - radius, margin + radius);
    }

    /// <summary>
    /// Le point touché tombe-t-il sur le bouton d'esquive ? Utilise le rayon <b>sensible</b>
    /// (<see cref="DashTouchSlop"/>), pas le rayon dessiné.
    /// </summary>
    public static bool IsDashButton(float x, float y, float screenWidth, float screenHeight)
    {
        var (cx, cy) = DashCenter(screenWidth, screenHeight);
        float r = DashRadius(screenHeight) * DashTouchSlop;

        return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
    }

    /// <summary>Centre du bouton de pause — haut à droite, loin du pouce de jeu.</summary>
    /// <remarks>
    /// ⚠ Ce bouton n'est pas un confort : <b>sur mobile, il n'y a pas d'Échap</b>. Sans lui, une run
    /// ne peut pas être mise en pause ni abandonnée, et le joueur n'a d'autre issue que de fermer
    /// l'onglet — ce qui, en web, emporte aussi sa sauvegarde tant qu'elle n'est pas écrite.
    /// Il est volontairement placé à l'opposé du bouton d'esquive : une pause déclenchée par erreur
    /// pendant une nuée est un mort.
    /// </remarks>
    public static (float X, float Y) PauseCenter(float screenWidth, float screenHeight)
    {
        float margin = EdgeMarginFraction * Math.Max(1f, screenHeight);
        float radius = PauseRadius(screenHeight);

        return (screenWidth - margin - radius, screenHeight - margin - radius);
    }

    /// <summary>Rayon dessiné du bouton de pause — plus petit que l'esquive : on le presse hors combat.</summary>
    public static float PauseRadius(float screenHeight)
        => Math.Max(MinButtonRadiusPx, 0.075f * Math.Max(1f, screenHeight));

    /// <summary>Le point touché tombe-t-il sur le bouton de pause ? Sans marge : voir <see cref="PauseCenter"/>.</summary>
    public static bool IsPauseButton(float x, float y, float screenWidth, float screenHeight)
    {
        var (cx, cy) = PauseCenter(screenWidth, screenHeight);
        float r = PauseRadius(screenHeight);

        return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
    }

    /// <summary>
    /// Ce doigt a-t-il le droit de faire naître le joystick ?
    /// </summary>
    /// <remarks>
    /// L'ordre compte : les boutons sont testés <b>avant</b> la zone du stick, sinon un bouton posé
    /// dans la moitié gauche serait avalé par le stick. Ils sont aujourd'hui à droite, mais s'en
    /// remettre à cette coïncidence rendrait tout déplacement de bouton silencieusement fatal.
    /// </remarks>
    public static bool IsStickZone(float x, float y, float screenWidth, float screenHeight)
    {
        if (IsDashButton(x, y, screenWidth, screenHeight)) return false;
        if (IsPauseButton(x, y, screenWidth, screenHeight)) return false;

        return x <= screenWidth * StickWidthFraction &&
               y <= screenHeight * (1f - TopBandFraction);
    }

    /// <summary>Grossissement maximal de l'interface sur une petite dalle.</summary>
    /// <remarks>
    /// <para>Grossir l'interface, c'est rétrécir la <b>maquette</b> : à 2,3, le canevas ne fait plus
    /// que 835 × 470 unités. Les panneaux posés en unités absolues y sont ramenés à la taille du
    /// canevas (<c>UiCanvas.PanelSize</c>), ce qui compresse leur contenu — trois cartes de montée de
    /// niveau côte à côte finissent par ne plus rien pouvoir écrire. C'est cette densité, et non un
    /// débordement, qui fixe la borne.</para>
    ///
    /// <para>2,3 est la valeur qui amène un téléphone courant <b>exactement</b> à
    /// <see cref="MinUiScale"/> : au-delà, on paierait de la lisibilité sans rien gagner sur la
    /// taille des cibles.</para>
    /// </remarks>
    public const float MaxUiEnlargement = 2.3f;

    /// <summary>
    /// Échelle d'interface en deçà de laquelle les menus deviennent intouchables au doigt.
    /// </summary>
    /// <remarks>
    /// Les lignes cliquables du jeu font une soixantaine d'unités de haut ; à 0,75, elles retombent
    /// à ~45 pixels écran, soit les 9 mm admis comme cible tactile confortable.
    /// </remarks>
    public const float MinUiScale = 0.75f;

    /// <summary>
    /// Hauteur de fenêtre, en pixels, en dessous de laquelle on considère la dalle « petite ».
    /// </summary>
    /// <remarks>
    /// <para>C'est le <b>discriminant</b> du grossissement, et il porte tout le risque de ce réglage.
    /// La question à laquelle il faudrait répondre est physique — « ce bouton fait-il 9 mm ? » — et
    /// le jeu ne peut pas y répondre : <c>Screen.dpi</c> vaut <b>zéro</b> en WebGL.</para>
    ///
    /// <para>La hauteur en pixels logiques y répond par la bande : un téléphone en paysage tombe
    /// entre 300 et 450, une fenêtre de bureau descend rarement sous 600, et une tablette est
    /// au-dessus. Le seuil sépare donc « petit et tenu à la main » de « grand », sans jamais toucher
    /// à une plateforme qui marche — ce qui est l'exigence la plus importante des deux.</para>
    /// </remarks>
    public const float SmallScreenHeight = 600f;

    /// <summary>
    /// De combien faut-il grossir l'interface sur une fenêtre de <paramref name="screenWidth"/> ×
    /// <paramref name="screenHeight"/>, dessinée pour une maquette de
    /// <paramref name="referenceWidth"/> × <paramref name="referenceHeight"/> ? 1 = pas de
    /// grossissement.
    /// </summary>
    /// <remarks>
    /// <para><b>Le défaut que ceci corrige.</b> Un téléphone en paysage fait environ 800 × 360 pixels
    /// logiques. Rapportée à une maquette de 1920 × 1080, l'interface y tombe à <b>0,37</b> : un
    /// bouton de menu de 60 unités y mesure 22 pixels, soit à peu près 4 mm. Il est parfaitement
    /// dessiné, parfaitement centré, et <b>on ne peut pas le toucher</b> — le doigt en couvre trois.
    /// Rien ne le signale : sur un écran de bureau, la même interface est irréprochable.</para>
    ///
    /// <para>La formule reprend celle d'un <c>CanvasScaler</c> réglé à mi-chemin entre largeur et
    /// hauteur : moyenne géométrique des deux rapports. Diviser la maquette par le facteur rendu ici
    /// multiplie l'échelle d'autant.</para>
    /// </remarks>
    public static float UiEnlargement(float screenWidth, float screenHeight,
                                      float referenceWidth, float referenceHeight)
    {
        if (screenWidth <= 1f || screenHeight <= 1f) return 1f;
        if (referenceWidth <= 1f || referenceHeight <= 1f) return 1f;
        if (screenHeight >= SmallScreenHeight) return 1f;

        float natural = (float)Math.Sqrt((screenWidth / referenceWidth) * (screenHeight / referenceHeight));
        if (natural >= MinUiScale) return 1f;

        return Math.Min(MaxUiEnlargement, MinUiScale / natural);
    }

    /// <summary>
    /// L'écran est-il en portrait ? Le jeu s'y refuse (voir la garde d'orientation).
    /// </summary>
    /// <remarks>
    /// Comparer les deux dimensions plutôt que d'interroger <c>Screen.orientation</c> : en WebGL,
    /// l'orientation rapportée par le navigateur suit le verrouillage du système et <b>ment</b> quand
    /// l'utilisateur a bloqué la rotation, alors que la taille du canevas, elle, dit toujours la
    /// vérité sur ce que le joueur voit.
    /// </remarks>
    public static bool IsPortrait(float screenWidth, float screenHeight) => screenHeight > screenWidth;
}
