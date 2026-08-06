using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Façade des effets visuels — le vocabulaire commun aux armes, aux capacités et aux morts.
///
/// <para><b>Ce qu'elle remplace.</b> La première version dessinait tout avec des chapelets de
/// points de sprite : un placeholder assumé, dont le seul but était que chaque arme <b>se voie</b>.
/// Elle a tenu ce rôle — plus aucune arme ne tue sans laisser de trace — mais un cône fait de 60
/// petits carrés ne se lit pas comme un souffle de flammes, et un anneau de points ne se lit pas
/// comme une aura. Les formes sont désormais de vraies polylignes, de vrais anneaux et de vraies
/// particules, avec le mélange <b>additif</b> qui donnait au jeu son allure sous Godot.</para>
///
/// <para><b>Tout est recyclé.</b> Une nuée de 300 entités produit des dizaines de morts par seconde,
/// et chaque arme demande sa trace à chaque tir. Créer puis détruire ces objets alimenterait un
/// ramassage de miettes permanent, exactement au moment où la cadence compte le plus. Les plafonds
/// ci-dessous sont durs : au-delà, l'effet est simplement <b>omis</b>. Une trace manquante dans une
/// mêlée illisible ne se remarque pas ; une chute de cadence, si.</para>
/// </summary>
public static class Vfx
{
    /// <summary>Durée de vie par défaut d'une trace, en secondes.</summary>
    public const float DefaultLife = 0.18f;

    private const int MaxTraces = 140;
    private const int MaxGlows = 220;
    private const int MaxBursts = 28;
    private const int MaxRings = 60;
    private const int MaxCrescents = 10;

    /// <summary>Points d'une polyligne — tampon partagé, pour n'allouer à aucun tir.</summary>
    private static readonly Vector3[] Points = new Vector3[64];

    /// <summary>
    /// Aléatoire <b>propre aux effets</b>. Surtout pas <c>UnityEngine.Random</c> : il partage son
    /// état avec le jeu, et une campagne de banc lancée sur une graine fixe verrait ses tirages de
    /// gameplay se décaler selon le nombre d'éclairs dessinés. Un effet purement décoratif ne doit
    /// jamais pouvoir changer le déroulé d'une run.
    /// </summary>
    private static readonly System.Random Jitter = new(20260805);

    private static readonly Stack<VfxTrace> PoolTraces = new();
    private static readonly Stack<VfxGlow> PoolGlows = new();
    private static readonly Stack<VfxBurst> PoolBursts = new();
    private static readonly Stack<VfxRing> PoolRings = new();
    private static readonly Stack<VfxCrescent> PoolCrescents = new();

    private static int _activeTraces;
    private static int _activeGlows;
    private static int _activeBursts;
    private static int _activeRings;
    private static int _activeCrescents;

    private static Transform? _root;

    /// <summary>Effets actuellement affichés — observable par le banc et le diagnostic.</summary>
    public static int ActiveEffects
        => _activeTraces + _activeGlows + _activeBursts + _activeRings + _activeCrescents;

    /// <summary>
    /// Effets émis depuis le début, cumulés. C'est cette valeur — et non le compte instantané — qui
    /// permet de vérifier qu'une arme <b>a laissé une trace</b> : les traces s'effacent en 0,2 s,
    /// donc un relevé ponctuel raterait presque toujours l'événement.
    /// </summary>
    public static int TracesCreated { get; private set; }

    // ─── Formes de base ───────────────────────────────────────────────────────

    /// <summary>Lueur additive ponctuelle — flash d'impact, nœud d'éclair, cœur de singularité.</summary>
    public static void Glow(Vector2 position, Color color, float radiusPx,
                            float intensity = 1f, float life = DefaultLife,
                            int order = VfxPrimitives.OrderOver)
    {
        var glow = RentGlow();
        if (glow != null) glow.Show(position, color, radiusPx, intensity, life, order);
    }

    /// <summary>Faisceau rectiligne — rayon cryo, lance vectorielle, lien de drone.</summary>
    public static void Beam(Vector2 from, Vector2 to, Color color,
                            float width = 3.5f, float life = DefaultLife,
                            int order = VfxPrimitives.OrderOver)
    {
        Points[0] = from;
        Points[1] = to;
        Polyline(2, color, width, width * 3f, life, order);
    }

    /// <summary>
    /// Éclair dentelé entre deux points. La dentelure n'est pas un ornement : un trait droit se lit
    /// comme un rayon continu, alors que la Bobine Tesla <b>saute</b> d'une cible à l'autre.
    /// </summary>
    public static void Bolt(Vector2 from, Vector2 to, Color color, float life = 0.16f)
    {
        var delta = to - from;
        float length = delta.magnitude;
        if (length < 0.01f) return;

        var perp = new Vector2(-delta.y, delta.x).normalized;
        int segments = Mathf.Clamp(Mathf.RoundToInt(length / 22f) + 2, 3, 14);

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float offset = (i == 0 || i == segments) ? 0f : (float)(Jitter.NextDouble() * 20.0 - 10.0);
            Points[i] = Vector2.Lerp(from, to, t) + perp * offset;
        }

        Polyline(segments + 1, color, 2.5f, 7f, life, VfxPrimitives.OrderOver);
        Glow(to, color, 35f, 1f, life, VfxPrimitives.OrderOver);
    }

    /// <summary>Arc centré sur <paramref name="direction"/> — portée d'une arme de mêlée, cône couvert.</summary>
    public static void Arc(Vector2 center, Vector2 direction, float halfAngleDeg, float radius,
                           Color color, float width = 4f, float life = DefaultLife,
                           int order = VfxPrimitives.OrderOver)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;

        float baseAngle = Mathf.Atan2(direction.y, direction.x);
        float half = halfAngleDeg * Mathf.Deg2Rad;
        int segments = Mathf.Clamp(Mathf.RoundToInt(halfAngleDeg / 5f), 4, 40);

        for (int i = 0; i <= segments; i++)
        {
            float a = baseAngle + Mathf.Lerp(-half, half, i / (float)segments);
            Points[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }

        Polyline(segments + 1, color, width, width * 2.6f, life, order);
    }

    /// <summary>Anneau fermé — aura, champ, portée d'une zone. Passe <b>sous</b> les entités.</summary>
    public static void Ring(Vector2 center, float radius, Color color,
                            float width = 3f, float life = 0.2f,
                            int order = VfxPrimitives.OrderGround)
    {
        var ring = RentRing();
        if (ring != null) ring.Show(center, radius, radius, color, width, life, order);
    }

    /// <summary>
    /// <b>Vortex</b> : cœur sombre, bras spiralés en rotation, horizon estompé et matière étirée.
    ///
    /// <para>Un puits de gravité dessiné en simple anneau ne dit rien de ce qu'il fait : il ressemble
    /// à une aire de dégâts de plus. Ce sont les <b>bras courbes</b> qui font lire l'aspiration, et
    /// leur <b>rotation</b> qui la fait lire comme un mouvement. La forme vient du jeu publié
    /// (<c>GravityWell</c>) : trois bras, resserrement de 3,2 rad sur la course, rotation continue.</para>
    ///
    /// <para><b>Les bras ne sont plus des traits.</b> Ils étaient tracés à la polyligne, donc avec le
    /// cœur presque blanc de <see cref="VfxTrace"/> — trois arcs <i>nets</i>, qui se lisent comme une
    /// décalcomanie posée sur le sol : quelque chose de <b>dessiné</b> par-dessus l'arène, et non
    /// l'arène elle-même en train de se tordre. Une singularité n'a aucun contour ; ce qu'elle a, ce
    /// sont des traînées floues qui s'écrasent en approchant du centre. D'où des chapelets de lueurs
    /// (même grammaire que <see cref="AuraCloud"/>, qui a réglé le même problème pour les auras) :
    /// plus larges et plus vives vers l'intérieur, sans jamais un seul bord franc.</para>
    ///
    /// <para>La <b>déformation du décor et des corps</b> ne se dessine pas ici : elle est portée par
    /// <see cref="SpaceDistortion"/>, qui travaille sur les sprites déjà à l'écran. C'est la
    /// différence entre peindre un tourbillon et en avoir un.</para>
    /// </summary>
    /// <param name="spin">Phase de rotation, en radians — l'appelant la fait avancer dans le temps.</param>
    public static void Vortex(Vector2 center, float radius, float innerRadius, float spin,
                              Color color, float life)
    {
        // ⚠ PAS d'anneau d'horizon, et pas de « cœur sombre » — les deux ont été retirés sur capture.
        //
        // L'anneau était posé à alpha 0,16, ce qui semblait raisonnable ; à l'image, c'était le trait
        // le plus net de toute la scène. La cause est le <b>cumul</b> : le vortex est redessiné à
        // chaque frame pour la durée d'une frame et demie, si bien que l'anneau se superpose à
        // lui-même en permanence — une opacité qui se lit comme une contribution unique en produit
        // deux, additionnées, indéfiniment. Baisser la valeur ne réglait rien : c'est la forme qui
        // était fausse (un cercle affirme une frontière, une singularité n'en a pas).
        //
        // Le « cœur sombre » ne faisait, lui, <b>rien du tout</b> : un anneau presque noir dessiné en
        // mélange ADDITIF n'ajoute quasiment aucune lumière — il était donc invisible depuis le
        // premier jour, tout en donnant l'impression que le centre était traité. On ne peut pas
        // creuser un trou en additif ; le cœur se dit par un halo bref, et le vide par l'absence de
        // matière autour.

        const int Arms = 3;
        const int Steps = 6;
        const float Tightness = 3.2f;   // resserrement de la spirale, en radians

        for (int arm = 0; arm < Arms; arm++)
        {
            float baseAngle = spin + arm * 2f * Mathf.PI / Arms;

            for (int i = 0; i < Steps; i++)
            {
                float t = i / (float)(Steps - 1);
                float r = Mathf.Lerp(radius, innerRadius, t);
                float a = baseAngle + t * Tightness;

                var at = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;

                // Le grain s'élargit ET s'éclaircit vers le centre : la matière paraît écrasée par
                // ce qu'elle approche. Constant, il redonnerait un trait — d'épaisseur régulière,
                // donc d'allure dessinée.
                float size = Mathf.Lerp(radius * 0.10f, radius * 0.26f, t);
                float alpha = Mathf.Lerp(0.10f, 0.34f, t);

                Glow(at, new Color(color.r, color.g, color.b, alpha), size, 1f, life,
                     VfxPrimitives.OrderGround);
            }
        }

        // Cœur : discret, pour la même raison de cumul que l'anneau. À 0,8 d'intensité il saturait au
        // blanc pur en une demi-seconde et donnait un projecteur au milieu du puits.
        Glow(center, color, innerRadius * 1.5f, 0.30f, life, VfxPrimitives.OrderGround);
    }

    /// <summary>
    /// Onde de choc : anneau qui se dilate depuis un point. Portage de <c>ShockwaveRing</c> — la mort
    /// d'un colosse doit s'annoncer par une expansion, pas par un cercle posé.
    /// </summary>
    public static void Shockwave(Vector2 center, float toRadius = 128f, float life = 0.4f,
                                 Color? color = null)
    {
        var ring = RentRing();
        if (ring != null)
            ring.Show(center, toRadius * 0.15f, toRadius, color ?? new Color(0.45f, 0.95f, 1f),
                      5f, life, VfxPrimitives.OrderOver);
    }

    /// <summary>
    /// Contour d'un cône plein : les deux bords et l'arc extérieur, d'un seul trait. C'est la forme
    /// qui dit <b>d'où part</b> l'attaque — un arc seul ne le dirait pas.
    /// </summary>
    public static void Cone(Vector2 origin, Vector2 direction, float halfAngleDeg, float radius,
                            Color color, float life = DefaultLife, float width = 3.5f)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;

        float baseAngle = Mathf.Atan2(direction.y, direction.x);
        float half = halfAngleDeg * Mathf.Deg2Rad;
        int segments = Mathf.Clamp(Mathf.RoundToInt(halfAngleDeg / 6f), 4, 30);

        int n = 0;
        Points[n++] = origin;

        for (int i = 0; i <= segments; i++)
        {
            float a = baseAngle + Mathf.Lerp(-half, half, i / (float)segments);
            Points[n++] = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }

        Points[n++] = origin;
        Polyline(n, color, width, width * 2.6f, life, VfxPrimitives.OrderOver);
    }

    /// <summary>Gerbe de particules — impact, mort, souffle, éclats.</summary>
    public static void Burst(Vector2 position, Color from, Color to, int count,
                             float speedMin, float speedMax, float sizePx, float life,
                             float dirDeg = 0f, float spreadDeg = 360f,
                             int order = VfxPrimitives.OrderOver)
    {
        var burst = RentBurst();
        if (burst != null)
            burst.Emit(position, from, to, count, speedMin, speedMax, sizePx, life,
                       dirDeg, spreadDeg, order);
    }

    // ─── Effets composés (portage de src/VFX) ─────────────────────────────────

    /// <summary>
    /// Impact d'un projectile — portage d'<c>ImpactBurst</c>. L'intensité suit le niveau de l'arme :
    /// un tir de niveau 8 ne doit pas cogner comme un tir de niveau 1, sans quoi la progression ne
    /// se voit que dans les chiffres.
    /// </summary>
    public static void Impact(Vector2 position, int power, Color color)
    {
        int p = Mathf.Clamp(power, 1, 8);

        Burst(position, color, new Color(color.r, color.g, color.b, 0f),
              6 + p * 4, 80f + p * 18f, 160f + p * 30f, 5f + p * 0.8f, 0.22f);

        // Rayon calibré sur capture : la transposition littérale de Godot (jusqu'à 130 px de RAYON,
        // soit 260 de diamètre) recouvrait un quart de l'écran à chaque tir d'une arme de haut
        // niveau. Voir la note de Bullet.AttachGlow — en additif, c'est le cumul qui décide.
        Glow(position, color, 8f + p * 2.5f, 0.7f, 0.18f);

        // À haut niveau, chaque impact secoue brièvement l'écran : ça « cogne ».
        if (p >= 4) ScreenShake.Shake(1.2f + p * 0.3f, 0.05f);
    }

    /// <summary>Flash de bouche — portage de <c>MuzzleFlash</c>.</summary>
    public static void Muzzle(Vector2 position, Vector2 direction)
    {
        var cyan = new Color(0.267f, 1f, 0.933f);
        float dirDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        Glow(position, cyan, 14f, 0.8f, 0.08f);
        Burst(position, cyan, new Color(cyan.r, cyan.g, cyan.b, 0f),
              6, 80f, 160f, 4f, 0.08f, dirDeg, 50f);
    }

    /// <summary>
    /// Mort d'un ennemi — portage d'<c>EnemyDeathBurst</c>. Le calibre va de 0 (fourrage) à 3 (boss),
    /// et les gros déclenchent en plus une onde de choc.
    /// </summary>
    public static void Death(Vector2 position, int tier, Color tint)
    {
        int t = Mathf.Clamp(tier, 0, 3);

        Burst(position, tint, new Color(tint.r * 0.6f, tint.g * 0.3f, tint.b * 0.2f, 0f),
              14 + t * 14, 80f + t * 45f, 150f + t * 80f, 6f + t * 2.5f, 0.3f + t * 0.1f);

        Glow(position, tint, 14f + t * 10f, 0.8f, 0.22f + t * 0.06f);

        if (t >= 2) Shockwave(position, 90f + t * 30f, 0.35f, tint);
    }

    /// <summary>Coup de Lame Plasma — portage de <c>PlasmaArcFlash</c>.</summary>
    public static void Crescent(Vector2 center, Vector2 direction, float halfAngleDeg, float radius)
    {
        var crescent = RentCrescent();
        if (crescent == null)
        {
            // Repli : le coup doit rester visible même quand le plafond est atteint. Une arme de
            // mêlée invisible passe pour une carte sans effet — le pire retour possible.
            Arc(center, direction, halfAngleDeg, radius, new Color(0.267f, 1f, 0.933f), 6f, 0.14f);
            return;
        }

        crescent.Show(center, direction, halfAngleDeg, radius);

        if (direction.sqrMagnitude > 0.0001f)
            Glow(center + direction.normalized * radius * 0.85f,
                 new Color(0.267f, 1f, 0.933f), radius * 0.22f, 0.6f, 0.18f);
    }

    /// <summary>
    /// Souffle de flammes — portage de <c>PyreFlame</c>, <b>sans aucun contour</b>.
    ///
    /// <para>Le jet était doublé du tracé de son cône (<see cref="Cone"/>), au motif que « lui seul dit
    /// exactement ce que l'arme couvre ». C'était vrai et c'était le mauvais arbitrage : un feu n'a
    /// pas d'arête, et deux segments droits partant du joueur donnent au souffle l'allure d'un
    /// gabarit de visée — on lit une <i>portée affichée</i>, pas une flamme. La couverture se lit
    /// désormais à ce qui brûle, ce qui est aussi la vérité du jeu.</para>
    ///
    /// <para>À la place, ce que le trait ne pouvait pas donner : la <b>fumée</b>. Elle prolonge le
    /// souffle après son extinction, et c'est elle qui fait lire un incendie entretenu plutôt qu'une
    /// gerbe d'étincelles répétée toutes les demi-secondes.</para>
    /// </summary>
    /// <param name="smoke">
    /// À couper quand plusieurs souffles partent <b>ensemble</b> (la couronne de la Colonne Solaire) :
    /// six bouffées par tir noient l'arène sous un voile laiteux — le piège de cumul déjà mesuré sur
    /// la fumée des ennemis en flammes. L'appelant émet alors sa propre dose, une fois.
    /// </param>
    public static void Flame(Vector2 origin, Vector2 direction, float coneAngleDeg, float range,
                             bool smoke = true)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction = direction.normalized;

        float dirDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        Burst(origin, new Color(1f, 0.9f, 0.45f, 0.9f), new Color(1f, 0.3f, 0.1f, 0f),
              36, range * 2.4f, range * 3.6f, 18f, 0.28f, dirDeg, coneAngleDeg);

        Glow(origin + direction * range * 0.4f, new Color(1f, 0.5f, 0.2f), range * 0.3f, 0.7f, 0.25f);

        if (smoke) Smoke(origin, direction, range);
    }

    /// <summary>Teinte de la fumée : grise et désaturée — une seule des trois composantes du feu.</summary>
    private static readonly Color SmokeTint = new(0.60f, 0.56f, 0.54f, 0.13f);

    /// <summary>
    /// Fumée d'un souffle : deux bouffées lâchées <b>dans</b> le jet, qui s'élèvent, dérivent vers
    /// l'avant et s'étalent en se diluant.
    /// </summary>
    /// <remarks>
    /// ⚠ Additive comme tout le reste : elle éclaircit là où de la vraie fumée assombrirait. Les
    /// arènes sont sombres, une volute sombre y serait invisible ; ce qui la fait lire comme de la
    /// fumée est sa teinte grise, sa lenteur et son étalement, pas sa direction de mélange.
    ///
    /// <para>⚠ <b>Alpha volontairement bas.</b> L'arme tire jusqu'à sept fois par seconde une fois la
    /// recharge réduite ; à 0,7 s de vie, une dizaine de bouffées coexistent en permanence devant le
    /// joueur. C'est leur cumul qui décide, jamais l'exemplaire isolé.</para>
    /// </remarks>
    public static void Smoke(Vector2 origin, Vector2 direction, float range)
    {
        var rise = new Vector2(direction.x * range * 0.30f, direction.y * range * 0.30f + 22f);

        for (int i = 0; i < 2; i++)
        {
            float t = 0.55f + i * 0.32f;
            var at = origin + direction * (range * t)
                   + new Vector2((float)(Jitter.NextDouble() * 18.0 - 9.0),
                                 (float)(Jitter.NextDouble() * 18.0 - 9.0));

            Puff(at, SmokeTint, range * (0.17f + i * 0.05f), 0.70f,
                 rise.y, rise.x, 2.3f, VfxPrimitives.OrderOver);
        }
    }

    /// <summary>
    /// <b>Éruption solaire</b> : une couronne de langues de feu qui part dans toutes les directions,
    /// plus le front chaud qui la propulse.
    ///
    /// <para>C'est la signature de la Colonne Solaire, et elle doit se distinguer <b>au premier
    /// coup d'œil</b> du souffle dirigé dont elle est l'évolution : celui-ci part vers une cible,
    /// celle-là part de partout. Le nombre de bras vient du jeu publié (six).</para>
    /// </summary>
    /// <param name="spin">Rotation de la couronne, en radians — décalée à chaque tir, sinon les six
    /// bras retombent toujours dans les mêmes six directions et l'éruption paraît figée.</param>
    public static void SolarFlare(Vector2 center, float radius, float coneAngleDeg, float spin)
    {
        const int Arms = 6;

        float dirDeg = spin * Mathf.Rad2Deg;

        for (int i = 0; i < Arms; i++)
        {
            // ⚠ Une langue de la couronne n'est PAS un appel à <see cref="Flame"/> avec un rayon plus
            // court. Calibré sur capture : le souffle dirigé est réglé pour une portée de 130 px et
            // des grains de 18 px, et six exemplaires réduits ne produisaient qu'un petit paquet de
            // braises collé au joueur — l'éruption la plus rare du jeu se lisait comme un tir raté.
            // Les langues portent donc jusqu'au rayon qui blesse, avec des grains une fois et demie
            // plus gros et une vie plus longue.
            Burst(center, new Color(1f, 0.92f, 0.55f, 0.95f), new Color(1f, 0.32f, 0.08f, 0f),
                  22, radius * 2.0f, radius * 3.2f, 27f, 0.42f,
                  dirDeg + i * 360f / Arms, coneAngleDeg);
        }

        // Le front : une onde chaude qui atteint exactement le rayon qui blesse. Sans elle, six
        // langues isolées ne disent pas qu'il s'est passé quelque chose *entre* elles.
        Shockwave(center, radius, 0.38f, new Color(1f, 0.62f, 0.22f));
        Glow(center, new Color(1f, 0.85f, 0.5f), radius * 0.34f, 0.9f, 0.26f);

        // Une seule bouffée pour toute la couronne — six auraient blanchi l'arène à chaque tir.
        Puff(center, SmokeTint, radius * 0.30f, 0.85f, 30f, 0f, 2.2f, VfxPrimitives.OrderOver);
    }

    /// <summary>Rayon glacé — portage de <c>CryoBeam</c> : faisceau + éclats de givre le long du tir.</summary>
    public static void Frost(Vector2 origin, Vector2 direction, float length, int power)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction = direction.normalized;

        var end = origin + direction * length;
        Beam(origin, end, new Color(0.55f, 0.92f, 1f), 3.5f, 0.18f);

        // Le givre naît le long du faisceau, pas à son origine : émis en un seul point, il se lirait
        // comme une explosion au pied du joueur.
        for (int i = 1; i <= 3; i++)
        {
            var at = Vector2.Lerp(origin, end, i / 4f);
            Burst(at, new Color(0.8f, 0.95f, 1f, 0.8f), new Color(0.8f, 0.95f, 1f, 0f),
                  6, 20f, 60f, 5f, 0.3f);
        }

        Glow(Vector2.Lerp(origin, end, 0.5f), new Color(0.5f, 0.85f, 1f),
             length * 0.12f, 0.6f, 0.18f);
    }

    /// <summary>Point lumineux isolé — conservé pour les effets qui ne marquent qu'une position.</summary>
    public static void Dot(Vector2 position, Color color, float size = 10f, float life = DefaultLife)
        => Glow(position, color, size, 1f, life);

    /// <summary>
    /// <b>Bouffée</b> : une lueur qui <i>dérive et s'étale</i> au lieu de rester posée — la fumée
    /// d'un ennemi qui brûle, et tout ce qui doit se dissiper plutôt que s'éteindre.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Elle est <b>additive</b> comme tout le reste, donc elle éclaircit là où de la vraie
    /// fumée assombrirait. C'est le bon choix ici et non un pis-aller : les arènes du jeu sont
    /// sombres, et une volute sombre y serait purement et simplement invisible. Ce qui la fait lire
    /// comme de la fumée est sa <b>teinte grise désaturée</b> — une seule des trois composantes du
    /// feu — sa lenteur et son étalement, pas sa direction de mélange.</para>
    /// </remarks>
    /// <param name="riseSpeed">Vitesse d'élévation, en pixels par seconde.</param>
    /// <param name="drift">Dérive latérale, en pixels par seconde.</param>
    public static void Puff(Vector2 position, Color color, float radiusPx, float life,
                            float riseSpeed = 26f, float drift = 0f, float growth = 2.1f,
                            int order = VfxPrimitives.OrderOver)
    {
        var glow = RentGlow();
        if (glow != null)
            glow.Show(position, color, radiusPx, 1f, life, order,
                      new Vector2(drift, riseSpeed), growth);
    }

    // ─── Plomberie ────────────────────────────────────────────────────────────

    private static void Polyline(int count, Color color, float coreWidth, float haloWidth,
                                 float life, int order)
    {
        var trace = RentTrace();
        if (trace != null) trace.Show(Points, count, color, coreWidth, haloWidth, life, order);
    }

    private static VfxTrace? RentTrace()
        => Rent(PoolTraces, ref _activeTraces, MaxTraces, CreateTrace);

    private static VfxGlow? RentGlow()
        => Rent(PoolGlows, ref _activeGlows, MaxGlows, CreateGlow);

    private static VfxBurst? RentBurst()
        => Rent(PoolBursts, ref _activeBursts, MaxBursts, CreateBurst);

    private static VfxRing? RentRing()
        => Rent(PoolRings, ref _activeRings, MaxRings, CreateRing);

    private static VfxCrescent? RentCrescent()
        => Rent(PoolCrescents, ref _activeCrescents, MaxCrescents, CreateCrescent);

    private static T? Rent<T>(Stack<T> pool, ref int active, int cap, Func<T> create)
        where T : MonoBehaviour
    {
        if (active >= cap) return null;

        // Un objet du vivier peut avoir été détruit avec sa scène. Le test passe par
        // UnityEngine.Object et NON par `item == null` : sur un paramètre générique, C# retombe sur
        // l'égalité de référence et ne voit pas le « faux null » d'un objet détruit — l'effet serait
        // alors silencieusement absent au premier tir de la run suivante.
        T? item = null;
        while (pool.Count > 0)
        {
            var candidate = pool.Pop();
            if ((UnityEngine.Object)candidate != null) { item = candidate; break; }
        }

        item ??= create();

        active++;
        TracesCreated++;
        return item;
    }

    private static readonly Func<VfxTrace> CreateTrace = () =>
    {
        var component = NewObject<VfxTrace>("VfxTrace");
        component.Build();
        return component;
    };

    private static readonly Func<VfxGlow> CreateGlow = () =>
    {
        var go = new GameObject("VfxGlow", typeof(SpriteRenderer), typeof(VfxGlow));
        go.transform.SetParent(Root, false);
        return go.GetComponent<VfxGlow>();
    };

    private static readonly Func<VfxBurst> CreateBurst = () =>
    {
        var component = NewObject<VfxBurst>("VfxBurst");
        component.Build();
        return component;
    };

    private static readonly Func<VfxRing> CreateRing = () =>
    {
        var component = NewObject<VfxRing>("VfxRing");
        component.Build();
        return component;
    };

    private static readonly Func<VfxCrescent> CreateCrescent = () =>
    {
        var component = NewObject<VfxCrescent>("VfxCrescent");
        component.Build();
        return component;
    };

    private static T NewObject<T>(string name) where T : Component
    {
        var go = new GameObject(name, typeof(T));
        go.transform.SetParent(Root, false);
        return go.GetComponent<T>();
    }

    private static Transform Root
    {
        get
        {
            if (_root != null) return _root;
            _root = new GameObject("[Vfx]").transform;
            return _root;
        }
    }

    internal static void Recycle(VfxTrace trace) { _activeTraces--; PoolTraces.Push(trace); }
    internal static void Recycle(VfxGlow glow) { _activeGlows--; PoolGlows.Push(glow); }
    internal static void Recycle(VfxBurst burst) { _activeBursts--; PoolBursts.Push(burst); }
    internal static void Recycle(VfxRing ring) { _activeRings--; PoolRings.Push(ring); }
    internal static void Recycle(VfxCrescent crescent) { _activeCrescents--; PoolCrescents.Push(crescent); }

    /// <summary>
    /// Oublie les viviers au changement de scène. Sans cela, les piles gardent des références sur des
    /// objets détruits, et les premiers tirs de la run suivante n'affichent rien.
    /// </summary>
    public static void Reset()
    {
        PoolTraces.Clear();
        PoolGlows.Clear();
        PoolBursts.Clear();
        PoolRings.Clear();
        PoolCrescents.Clear();

        _activeTraces = _activeGlows = _activeBursts = _activeRings = _activeCrescents = 0;
        _root = null;
    }
}
