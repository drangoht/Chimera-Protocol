using UnityEngine;

/// <summary>
/// <b>La Marée de Rouille</b> à l'écran — la nappe qui avance en overtime, et les dégâts continus
/// qu'elle inflige. La géométrie et les taux vivent dans <see cref="RustTide"/> et
/// <see cref="RustErosion"/> (règles pures, testées) ; ce composant ne fait que les dessiner et les
/// appliquer.
///
/// <para><b>Elle se voit ou elle ne sert à rien.</b> La caméra suit le joueur et ne montre jamais
/// toute l'arène : un joueur qui meurt sans avoir vu le bord arriver conclut que le jeu est cassé,
/// pas qu'il s'est laissé enfermer. D'où une nappe franchement teintée plutôt qu'un assombrissement,
/// un liseré pulsant sur la limite du terrain sûr — la seule information qui compte, « à partir d'ici
/// ça fait mal » — et une teinte <b>identique sur tous les biomes</b> : c'est la même menace, elle
/// doit se reconnaître d'un niveau à l'autre, là où le sol, lui, dit où l'on joue.</para>
///
/// <para><b>⚠ Pourquoi tout le rendu tient dans un shader depuis le 2026-08-22.</b> Il tenait
/// jusque-là dans une vingtaine de <c>SpriteRenderer</c> : quatre nappes, quatre halos de front,
/// quatre liserés, douze vagues. Signalé en jouant : « la marée est un peu trop carrée, dans la
/// vraie vie la rouille n'est pas nette comme ça ». Ce n'était pas un défaut de réglage — <b>une
/// arête de sprite est droite par construction</b>. On peut la découper en segments, mais alors on
/// compte les segments, exactement comme on compte les taches d'une brume faite de sprites doux
/// (<c>docs/PITFALLS_UNITY.md</c>, § brume). Un champ de distance évalué par pixel n'a ni segment ni
/// tache, et son bord peut être aussi mangé qu'on veut sans qu'aucun objet ne bouge : c'est le même
/// arbitrage que celui déjà tranché pour la brume atmosphérique, au même endroit du moteur.</para>
///
/// <para><b>⚠ Pourquoi des vagues, alors que la géométrie avance déjà en continu.</b> Signalé en
/// jouant (2026-08-21) : « on la voit avancer par à-coups ». Ce n'était <i>pas</i> un défaut de code —
/// <see cref="RustTide.SafeFraction"/> est continue, elle est relue à chaque image et rien n'arrondit
/// une position. C'est la <b>vitesse elle-même</b> qui est le problème : le bord recule de
/// <b>1,6 unité par seconde</b> sur les côtés et <b>1,0 sur le haut et le bas</b> (960 px et 608 px
/// avalés en dix minutes). À l'écran, cela fait <b>un pixel toutes les 0,7 à 0,9 seconde</b> — sous le
/// seuil de perception du mouvement. L'œil ne voit donc jamais le bord bouger ; il constate, entre
/// deux regards, qu'il a bougé. La correction ne pouvait pas être d'accélérer la marée : la date de
/// fermeture (<see cref="RustTide.CloseMinutes"/>) <i>est</i> la garantie de fin, tout le §38 en
/// dépend. Il fallait <b>découpler le signal visuel de la vitesse géométrique</b> — les vagues
/// courent vers l'intérieur à ~110 unités/s, près de cent fois le recul du bord, et le grignotement
/// du front (<see cref="RustErosion"/>) creuse et comble des échancrures sur place. L'œil lit « ça
/// avance sur moi » en continu pendant que le bord, lui, rampe.</para>
///
/// <para>Design : <c>docs/GDD.md</c> §38.</para>
/// </summary>
public sealed class RustTideZone : MonoBehaviour
{
    /// <summary>
    /// Débord du quad au-delà de l'arène, sur chaque axe.
    ///
    /// <para>Il doit couvrir ce que la caméra voit quand le joueur est collé à un bord, submersion
    /// comprise : à ce moment-là le quad est la seule chose qui teinte l'écran, et un quad trop juste
    /// montrerait sa propre arête — la seule arête droite que ce rendu existe pour supprimer.</para>
    /// </summary>
    private const float OverhangPx = 1300f;

    /// <summary>
    /// Espacement des vagues dans la nappe, en pixels. <b>Constant</b>, et c'est le point : la phase
    /// se divise par lui, là où l'ancien rendu la divisait par la profondeur de la nappe — une
    /// grandeur qui grandit sans cesse et faisait sauter la phase d'un demi-cycle pour un centième
    /// d'unité (cf. <see cref="TideWaves"/>). Le piège disparaît au lieu d'être contourné.
    /// </summary>
    private const float WaveSpacingPx = 210f;

    /// <summary>
    /// Vitesse des vagues vers l'intérieur, en unités par seconde. Sans commune mesure avec le recul
    /// du bord (1,6 u/s) — c'est <b>tout l'intérêt</b> : le mouvement perçu vient d'ici, la garantie
    /// de fin vient de la géométrie, et les deux n'ont pas à avancer à la même vitesse.
    /// </summary>
    private const float WaveSpeed = 110f;

    /// <summary>Sous les entités, au-dessus du sol et des motifs (cf. ArenaRenderer, qui va jusqu'à -90).</summary>
    private const int NappeOrder = -89;

    private static readonly Color NappeColor = new(0.58f, 0.22f, 0.09f, 0.45f);
    private static readonly Color RimColor   = new(1f,    0.48f, 0.16f, 1f);
    private static readonly Color FrontColor = new(0.95f, 0.42f, 0.14f, 0.26f);
    private static readonly Color SmokeColor = new(0.72f, 0.34f, 0.16f, 1f);

    private static readonly int SafeHalfId   = Shader.PropertyToID("_SafeHalf");
    private static readonly int ArenaHalfId  = Shader.PropertyToID("_ArenaHalf");
    private static readonly int TideTimeId   = Shader.PropertyToID("_TideTime");
    private static readonly int WavePhaseId  = Shader.PropertyToID("_WavePhase");
    private static readonly int RimPulseId   = Shader.PropertyToID("_RimPulse");
    private static readonly int SubmersionId = Shader.PropertyToID("_Submersion");

    private SpriteRenderer? _nappe;
    private Material? _material;

    // Phase des vagues, dans [0,1). ACCUMULÉE, jamais recalculée depuis l'horloge : voir
    // WaveSpacingPx. Le shader ne fait que la lire — il n'a pas d'état, et une phase qu'il
    // recalculerait depuis _Time.y repartirait de zéro à chaque rechargement de scène.
    private float _wavePhase;

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
        BuildNappe();
        Show(false);
    }

    /// <summary>
    /// Monte l'unique quad de la marée.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Le shader se charge par <c>Resources.Load</c>, jamais par <c>Shader.Find</c> : un
    /// shader seulement atteint par <c>Shader.Find</c> peut être retiré du build par le nettoyage de
    /// shaders, et la marée serait invisible <i>dans le jeu exporté seulement</i> — jamais dans
    /// l'éditeur, donc jamais pendant les tests.</para>
    /// <para>Son absence est signalée en <b>erreur</b>, pas en avertissement comme celle de la brume :
    /// une brume manquante coûte de l'ambiance, une marée manquante tue le joueur sans rien
    /// afficher.</para>
    /// </remarks>
    private void BuildNappe()
    {
        var shader = Resources.Load<Shader>("Shaders/RustTide");
        if (shader == null)
        {
            Debug.LogError("[RustTideZone] shader de marée introuvable — la marée rongera SANS SE VOIR.");
            return;
        }

        _material = new Material(shader);
        _material.SetColor("_NappeColor", NappeColor);
        _material.SetColor("_RimColor",   RimColor);
        _material.SetColor("_FrontColor", FrontColor);
        _material.SetColor("_SmokeColor", SmokeColor);

        var go = new GameObject("MareeDeRouille", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        go.transform.position = Vector3.zero;   // le champ de distance est en coordonnées MONDE
        go.transform.localScale = new Vector3(Arena.Width  + OverhangPx * 2f,
                                              Arena.Height + OverhangPx * 2f, 1f);

        _nappe = go.GetComponent<SpriteRenderer>();
        _nappe.sprite = UiPrimitives.White;
        _nappe.sharedMaterial = _material;
        _nappe.sortingOrder = NappeOrder;
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
        AdvanceWaves();
        PushUniforms(gm.OvertimeSeconds, otMinutes);

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
    /// Fait avancer la phase des vagues, à vitesse constante <i>en unités du monde</i>.
    ///
    /// <para>Le temps est <b>non mis à l'échelle</b> : la marée n'attend pas la fin d'un ralenti, et
    /// un front qui se fige pendant une fusion se lirait comme éteint.</para>
    /// </summary>
    private void AdvanceWaves()
        => _wavePhase = TideWaves.AdvancePhase(_wavePhase, Time.unscaledDeltaTime,
                                               WaveSpeed, WaveSpacingPx);

    /// <summary>
    /// Pousse au shader tout ce qui change d'une image à l'autre.
    ///
    /// <para>⚠ <c>_TideTime</c> est l'horloge de l'<b>overtime</b>, pas celle du moteur : c'est elle
    /// qui pilote le grignotement du front, et <see cref="RustTide.DepthAt"/> la lit sous le même nom
    /// pour décider des dégâts. Passer <c>Time.time</c> ferait dériver le contour dessiné du contour
    /// qui ronge, d'autant plus que la partie dure — un mensonge lentement croissant, donc invisible
    /// tant qu'on ne joue pas une run entière.</para>
    /// </summary>
    private void PushUniforms(float overtimeSeconds, float otMinutes)
    {
        if (_material == null) return;

        // La pulsation est en temps NON mis à l'échelle : un repère de danger qui se fige pendant un
        // ralenti se lit comme éteint.
        float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f));

        _material.SetVector(SafeHalfId, new Vector4(Arena.HalfWidth  * SafeFraction,
                                                    Arena.HalfHeight * SafeFraction, 0f, 0f));

        // L'arène nominale : c'est l'écart avec le rectangle sûr qui dit combien la rouille a déjà
        // mangé, donc quelle profondeur de dentelure elle a le droit d'avoir. Sans lui, le bord
        // serait mordu de 72 px dès la première seconde d'overtime, en pleine minute de grâce.
        _material.SetVector(ArenaHalfId, new Vector4(Arena.HalfWidth, Arena.HalfHeight, 0f, 0f));
        _material.SetFloat(TideTimeId,  overtimeSeconds);
        _material.SetFloat(WavePhaseId, _wavePhase);
        _material.SetFloat(RimPulseId,  pulse);
        _material.SetFloat(SubmersionId,
                           Mathf.Clamp01(RustTide.FloorFractionPerSecond(otMinutes)
                                         / RustTide.MaxFractionPerSecond));
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
        if (_nappe != null) _nappe.enabled = visible;
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }
}
