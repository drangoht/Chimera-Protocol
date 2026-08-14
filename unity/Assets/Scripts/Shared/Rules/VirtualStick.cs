using System;

/// <summary>
/// Géométrie du <b>joystick flottant</b> tactile — logique pure, testable sans écran ni moteur.
///
/// <para><b>Pourquoi « flottant » et non « posé ».</b> Un stick dessiné à une place fixe oblige le
/// pouce à trouver un cercle qu'il ne voit pas : il est sous le pouce. Le joueur le manque, part en
/// diagonale, et le jeu paraît ne pas répondre. Un stick flottant naît <i>là où le doigt se pose</i>,
/// ce qui rend l'erreur de visée impossible par construction — c'est la seule raison de ce
/// choix.</para>
///
/// <para><b>Le recentrage est la partie qui n'est pas évidente.</b> Sans lui, un pouce qui glisse
/// au-delà du rayon voit sa course <i>saturer</i> : il continue de bouger, la direction ne change
/// plus, et surtout il ne peut plus revenir vers le centre sans traverser tout le rayon. En pratique
/// le joueur se retrouve à courir vers la droite en ayant le pouce collé au bord de l'écran. On
/// traîne donc l'origine derrière le doigt dès qu'il dépasse : la direction reste juste, et un demi-
/// centimètre de retour suffit toujours à ralentir.</para>
///
/// <para><b>Repère.</b> Toutes les coordonnées sont en <b>pixels écran, origine en bas à gauche</b>
/// — celui de <c>Touchscreen</c> et de <c>Mouse.position</c> dans le paquet Input System, et celui
/// des ancres uGUI. Le repère du DOM (origine en haut à gauche) ne doit jamais entrer ici : c'est
/// l'axe Y qui s'inverserait, et un joueur qui monte quand il pousse vers le bas est un défaut qu'on
/// ne lit pas dans du code.</para>
/// </summary>
public static class VirtualStick
{
    /// <summary>
    /// Course du pouce, en pixels, qui vaut « poussé à fond » — mesurée depuis l'origine du stick.
    /// </summary>
    /// <remarks>
    /// <para>Exprimée pour une hauteur d'écran de référence de 1080 px (cf.
    /// <see cref="RadiusFor"/>) : c'est un geste de pouce, il ne se règle pas en pixels absolus.
    /// 110 px sur 1080, soit environ 10 % de la hauteur, correspond à une course d'à peu près un
    /// centimètre sur un téléphone tenu à deux mains — assez pour doser une vitesse, assez court pour
    /// que le pouce ne quitte jamais sa position de repos.</para>
    /// </remarks>
    public const float ReferenceRadius = 110f;

    /// <summary>Hauteur d'écran pour laquelle <see cref="ReferenceRadius"/> est exprimé.</summary>
    public const float ReferenceHeight = 1080f;

    /// <summary>
    /// Rayon en deçà duquel le stick rend zéro : le doigt est posé, il ne pousse pas encore.
    /// </summary>
    /// <remarks>
    /// Sans zone morte, <b>poser le pouce fait partir le joueur</b> : un écran capacitif renvoie des
    /// positions qui bougent de quelques pixels sous un doigt immobile, et le joueur dérive pendant
    /// qu'il regarde son HUD. 14 px sur 1080 est en dessous du plus petit geste volontaire et
    /// au-dessus du bruit mesuré.
    /// </remarks>
    public const float ReferenceDeadZone = 14f;

    /// <summary>
    /// Rayon du stick pour un écran de <paramref name="screenHeight"/> pixels de haut.
    /// </summary>
    /// <remarks>
    /// Le plancher à 60 px n'est pas décoratif : sur un téléphone en paysage, la hauteur tombe à
    /// 360-420 px logiques, et un rayon strictement proportionnel y deviendrait plus petit que la
    /// zone de contact d'un pouce — le stick serait alors <i>toujours</i> à fond.
    /// </remarks>
    public static float RadiusFor(float screenHeight)
        => Math.Max(60f, ReferenceRadius * Math.Max(1f, screenHeight) / ReferenceHeight);

    /// <summary>Zone morte pour un écran de <paramref name="screenHeight"/> pixels de haut.</summary>
    public static float DeadZoneFor(float screenHeight)
        => Math.Max(8f, ReferenceDeadZone * Math.Max(1f, screenHeight) / ReferenceHeight);

    /// <summary>Ce que rend le stick à une frame donnée.</summary>
    public readonly struct Reading
    {
        /// <summary>Composante horizontale du déplacement, dans [-1, 1].</summary>
        public readonly float X;

        /// <summary>Composante verticale du déplacement, dans [-1, 1].</summary>
        public readonly float Y;

        /// <summary>Origine du stick <b>après</b> recentrage — à réécrire dans l'état appelant.</summary>
        public readonly float OriginX;

        /// <summary>Origine du stick après recentrage.</summary>
        public readonly float OriginY;

        public Reading(float x, float y, float originX, float originY)
        {
            X = x; Y = y; OriginX = originX; OriginY = originY;
        }

        /// <summary>Intensité de la poussée, dans [0, 1].</summary>
        public float Magnitude => (float)Math.Sqrt(X * X + Y * Y);
    }

    /// <summary>
    /// Lit le stick : un doigt en <paramref name="fingerX"/>/<paramref name="fingerY"/>, une origine
    /// posée au premier contact.
    /// </summary>
    /// <param name="radius">Course qui vaut « à fond » — voir <see cref="RadiusFor"/>.</param>
    /// <param name="deadZone">Course en deçà de laquelle on rend zéro — voir <see cref="DeadZoneFor"/>.</param>
    /// <remarks>
    /// <para>Le vecteur rendu est <b>dosé</b>, pas binaire : à mi-course le joueur avance à demi-
    /// vitesse. C'est ce qui permet de longer une nuée sans la percuter, et c'est le seul avantage
    /// réel du tactile sur le clavier, qui ne connaît que huit directions.</para>
    ///
    /// <para>La progression est <b>linéaire entre la zone morte et le rayon</b>, et non brute :
    /// sans cela, le premier pixel utile ferait déjà avancer à 13 % de la vitesse, un saut que le
    /// joueur ressent comme un à-coup au démarrage.</para>
    /// </remarks>
    public static Reading Read(float originX, float originY, float fingerX, float fingerY,
                               float radius, float deadZone)
    {
        radius = Math.Max(1f, radius);
        deadZone = Math.Max(0f, Math.Min(deadZone, radius - 1f));

        float dx = fingerX - originX;
        float dy = fingerY - originY;
        float distance = (float)Math.Sqrt(dx * dx + dy * dy);

        // Recentrage : au-delà du rayon, l'origine suit le doigt en restant à exactement un rayon
        // derrière lui. Le joueur garde donc toujours une course de retour disponible.
        if (distance > radius)
        {
            float k = (distance - radius) / distance;
            originX += dx * k;
            originY += dy * k;

            // ⚠ dx/dy visaient l'ANCIENNE origine : les laisser tels quels rendrait un vecteur de
            // norme distance/rayon, donc supérieur à 1 — le joueur dépasserait sa vitesse maximale
            // d'autant plus qu'il glisse loin, sans qu'aucun plafond ne le signale.
            dx -= dx * k;
            dy -= dy * k;
            distance = radius;
        }

        if (distance <= deadZone) return new Reading(0f, 0f, originX, originY);

        // Direction unitaire × intensité rééchelonnée sur [zone morte, rayon] → [0, 1].
        float scale = (distance - deadZone) / (radius - deadZone) / distance;

        return new Reading(dx * scale, dy * scale, originX, originY);
    }
}
