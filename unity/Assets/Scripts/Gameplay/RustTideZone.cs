using UnityEngine;

/// <summary>
/// <b>La Marée de Rouille</b> à l'écran — la bordure qui avance en overtime, et les dégâts continus
/// qu'elle inflige. La géométrie et les taux vivent dans <see cref="RustTide"/> (règle pure, testée) ;
/// ce composant ne fait que les dessiner et les appliquer.
///
/// <para><b>Elle se voit ou elle ne sert à rien.</b> La caméra suit le joueur et ne montre jamais
/// toute l'arène : un joueur qui meurt sans avoir vu le bord arriver conclut que le jeu est cassé,
/// pas qu'il s'est laissé enfermer. D'où une nappe franchement teintée plutôt qu'un assombrissement,
/// un liseré pulsant sur la limite du terrain sûr — la seule information qui compte, « à partir d'ici
/// ça fait mal » — et une teinte <b>identique sur tous les biomes</b> : c'est la même menace, elle
/// doit se reconnaître d'un niveau à l'autre, là où le sol, lui, dit où l'on joue.</para>
///
/// <para>Design : <c>docs/GDD.md</c> §38.</para>
/// </summary>
public sealed class RustTideZone : MonoBehaviour
{
    /// <summary>Débord des nappes au-delà de l'arène : la rouille ne doit pas s'arrêter net au bord.</summary>
    private const float OverhangPx = 400f;

    /// <summary>Épaisseur du liseré qui marque la limite du terrain sûr.</summary>
    private const float RimThickness = 7f;

    /// <summary>Sous les entités, au-dessus du sol et des motifs (cf. ArenaRenderer).</summary>
    private const int NappeOrder = -88;
    private const int RimOrder   = -87;

    private static readonly Color NappeColor = new(0.58f, 0.22f, 0.09f, 0.45f);
    private static readonly Color RimColor   = new(1f,    0.48f, 0.16f, 1f);

    private SpriteRenderer? _top, _bottom, _left, _right;
    private SpriteRenderer? _rimTop, _rimBottom, _rimLeft, _rimRight;

    /// <summary>Fraction sûre courante — lue par le HUD et par les vérifications de banc.</summary>
    public float SafeFraction { get; private set; } = 1f;

    /// <summary>Le joueur est-il dans la rouille en ce moment ? (retour d'écran, télémétrie)</summary>
    public bool PlayerInTide { get; private set; }

    // Les deux annonces de la marée. Chacune ne part qu'UNE fois par run : répéter « vous brûlez »
    // à chaque incursion transformerait le seul canal d'information du HUD en bruit de fond, et un
    // bandeau qu'on apprend à ignorer ne dit plus rien quand il compte.
    private bool _arrivalAnnounced;
    private bool _burnAnnounced;

    private void Awake()
    {
        _top    = MakeBand("MareeHaut");
        _bottom = MakeBand("MareeBas");
        _left   = MakeBand("MareeGauche");
        _right  = MakeBand("MareeDroite");

        _rimTop    = MakeRim("LisereHaut");
        _rimBottom = MakeRim("LisereBas");
        _rimLeft   = MakeRim("LisereGauche");
        _rimRight  = MakeRim("LisereDroite");

        Show(false);
    }

    private SpriteRenderer MakeBand(string name) => MakeSprite(name, NappeColor, NappeOrder);
    private SpriteRenderer MakeRim(string name)  => MakeSprite(name, RimColor,   RimOrder);

    private SpriteRenderer MakeSprite(string name, Color color, int order)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = UiPrimitives.White;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    private void Update()
    {
        var gm = GameManager.Instance;

        // Hors overtime la marée n'existe pas — pas même à l'état de décor éteint : une bordure
        // visible pendant le temps imparti annoncerait un danger qui n'arrive pas encore.
        if (gm == null || !gm.Overtime)
        {
            SafeFraction = 1f;
            PlayerInTide = false;
            Show(false);
            return;
        }

        float otMinutes = gm.OvertimeSeconds / 60f;
        SafeFraction = RustTide.SafeFraction(otMinutes);

        Show(true);
        Layout(Arena.HalfWidth * SafeFraction, Arena.HalfHeight * SafeFraction);

        // L'annonce part quand la marée SE MET EN MARCHE, pas à l'entrée en overtime : pendant la
        // minute de grâce il n'y a rien à voir, et une alerte sans objet visible s'oublie avant que
        // la chose n'arrive.
        if (!_arrivalAnnounced && SafeFraction < 1f)
        {
            _arrivalAnnounced = true;
            HUD.Instance?.Announce(Loc.T("BANNER_RUST_TIDE"), 5f);
        }

        ApplyToPlayer(otMinutes);

        if (!_burnAnnounced && PlayerInTide)
        {
            _burnAnnounced = true;
            HUD.Instance?.Announce(Loc.T("BANNER_RUST_TIDE_BURN"), 3f);
        }
    }

    /// <summary>
    /// Place les quatre nappes et le liseré autour du rectangle sûr.
    ///
    /// <para>Les nappes horizontales couvrent toute la largeur, les verticales seulement la hauteur
    /// <i>sûre</i> : sans ce découpage, les quatre se chevaucheraient aux coins et la transparence
    /// s'y additionnerait — quatre coins deux fois plus sombres que les bords, qu'aucune règle du
    /// design ne justifie.</para>
    /// </summary>
    private void Layout(float safeHalfW, float safeHalfH)
    {
        float outerW = Arena.HalfWidth + OverhangPx;
        float outerH = Arena.HalfHeight + OverhangPx;

        // Bandes horizontales : toute la largeur, du bord du terrain sûr jusqu'au débord.
        float bandH = Mathf.Max(0f, outerH - safeHalfH);
        Place(_top,    new Vector2(0f,  (safeHalfH + outerH) * 0.5f), new Vector2(outerW * 2f, bandH));
        Place(_bottom, new Vector2(0f, -(safeHalfH + outerH) * 0.5f), new Vector2(outerW * 2f, bandH));

        // Bandes verticales : bornées à la hauteur sûre, pour ne pas doubler la teinte aux coins.
        float bandW = Mathf.Max(0f, outerW - safeHalfW);
        Place(_left,  new Vector2(-(safeHalfW + outerW) * 0.5f, 0f), new Vector2(bandW, safeHalfH * 2f));
        Place(_right, new Vector2( (safeHalfW + outerW) * 0.5f, 0f), new Vector2(bandW, safeHalfH * 2f));

        // Liseré : il pulse, parce qu'une ligne fixe se fond dans un décor déjà chargé. La pulsation
        // est en temps NON mis à l'échelle — la marée continue d'avancer quand le jeu ralentit, et
        // un repère de danger qui se fige pendant un ralenti se lit comme éteint.
        float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f));
        var rim = RimColor;
        rim.a = pulse;

        bool visibleRim = safeHalfW > 0f && safeHalfH > 0f;
        Place(_rimTop,    new Vector2(0f,  safeHalfH), new Vector2(safeHalfW * 2f, RimThickness), rim, visibleRim);
        Place(_rimBottom, new Vector2(0f, -safeHalfH), new Vector2(safeHalfW * 2f, RimThickness), rim, visibleRim);
        Place(_rimLeft,   new Vector2(-safeHalfW, 0f), new Vector2(RimThickness, safeHalfH * 2f), rim, visibleRim);
        Place(_rimRight,  new Vector2( safeHalfW, 0f), new Vector2(RimThickness, safeHalfH * 2f), rim, visibleRim);
    }

    private static void Place(SpriteRenderer? sr, Vector2 center, Vector2 size)
    {
        if (sr == null) return;
        bool visible = size.x > 0.5f && size.y > 0.5f;
        sr.enabled = visible;
        if (!visible) return;

        sr.transform.position = center;
        sr.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    private static void Place(SpriteRenderer? sr, Vector2 center, Vector2 size, Color color, bool allowed)
    {
        if (sr == null) return;
        if (!allowed) { sr.enabled = false; return; }

        sr.color = color;
        Place(sr, center, size);
    }

    /// <summary>
    /// Ronge le joueur s'il est dans la rouille.
    ///
    /// <para>Le montant est calculé par la règle pure, puis remis au joueur par
    /// <see cref="Player.TakeContinuousDamage"/> — le point d'entrée des <i>débits</i>, qui écarte les
    /// i-frames. Passer par <c>TakeDamage</c> reviendrait à plafonner la marée à 2,2 coups par
    /// seconde, c'est-à-dire à reproduire exactement le plafond qu'elle existe pour contourner.</para>
    /// </summary>
    private void ApplyToPlayer(float otMinutes)
    {
        var player = Player.Instance;
        if (player == null || player.IsDead)
        {
            PlayerInTide = false;
            return;
        }

        Vector3 p = player.transform.position;
        float damage = RustTide.DamageOverTime(p.x, p.y, player.Stats.MaxHp,
                                               otMinutes, Time.deltaTime,
                                               Arena.HalfWidth, Arena.HalfHeight);

        PlayerInTide = damage > 0f;
        if (damage > 0f) player.TakeContinuousDamage(damage);
    }

    private void Show(bool visible)
    {
        if (visible) return;   // la mise en page rallume ce qui doit l'être

        Hide(_top); Hide(_bottom); Hide(_left); Hide(_right);
        Hide(_rimTop); Hide(_rimBottom); Hide(_rimLeft); Hide(_rimRight);
    }

    private static void Hide(SpriteRenderer? sr)
    {
        if (sr != null) sr.enabled = false;
    }
}
