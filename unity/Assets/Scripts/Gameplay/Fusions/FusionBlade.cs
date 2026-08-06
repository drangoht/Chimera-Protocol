using UnityEngine;

/// <summary>
/// Lame de Fusion — évolution de <see cref="PlasmaBlade"/> : arc complet et bien plus large
/// (130 contre 80), donc une arme de nettoyage là où la lame était une arme de front.
///
/// <para><b>Ce n'est plus une lame, et cela doit se voir.</b> À 360°, l'arc n'a plus de direction :
/// il ne reste qu'un rayon. Le croissant balayé hérité de la lame mentait donc sur ce que fait
/// l'arme — il montrait un coup porté <i>quelque part</i> quand elle frappe partout. Le portage
/// affichait, quatre fois par seconde, un demi-cercle qui tournait au hasard des cibles.</para>
///
/// <para>Ce qu'elle est vraiment : un <b>champ irradiant</b> permanent, qui ronge ce qui s'en
/// approche. D'où un nuage sans contour (jamais d'anneau : un cercle affirme une frontière, une
/// contamination n'en a pas) et, surtout, une <b>déformation des corps qui y entrent</b>. C'est le
/// seul effet du jeu qui travaille la silhouette de sa victime au lieu de poser quelque chose
/// dessus — et c'est ce qui le rend reconnaissable en une image.</para>
/// </summary>
public sealed class FusionBlade : PlasmaBlade
{
    /// <summary>Teinte du champ — un vert de radioactivité, absent du reste de la palette.</summary>
    private static readonly Color Radiation = new(0.55f, 1f, 0.28f);

    /// <summary>Secondes entre deux motes de contamination.</summary>
    private const float MoteInterval = 0.22f;

    /// <summary>Plafond d'effets simultanés au-delà duquel les motes s'abstiennent.</summary>
    private const int MoteBudget = 110;

    /// <summary>
    /// Part du rayon à l'intérieur de laquelle la déformation est <b>maximale</b>. Au-delà, elle
    /// décroît jusqu'au bord : un ennemi qui approche se tord de plus en plus, ce qui rend le champ
    /// lisible <i>sans</i> qu'on ait à en tracer la limite.
    /// </summary>
    private const float FullWarpFraction = 0.55f;

    /// <summary>
    /// ⚠ Générateur PRIVÉ : <c>UnityEngine.Random</c> décalerait les tirages de gameplay d'une
    /// campagne à graine fixe. Un effet décoratif ne change jamais le déroulé d'une run.
    /// </summary>
    private readonly System.Random _moteRng = new(0x2AD10);

    private float _moteTimer;

    /// <summary>Nuage irradiant porté — créé au premier passage, il vit ensuite tout seul.</summary>
    public AuraCloud? Field { get; private set; }

    /// <summary>Entités déformées à l'instant — observable pour les vérifications.</summary>
    public int WarpedCount { get; private set; }

    protected override void Awake()
    {
        ArcAngleDeg = 360f;   // plus d'angle mort : la fusion frappe tout autour
        ArcRadius = 130f;
        base.Awake();
    }

    protected override void Update()
    {
        base.Update();

        EnsureField();
        Irradiate(Time.deltaTime);
    }

    /// <summary>
    /// Entretient la déformation de tout ce qui séjourne dans le champ.
    /// </summary>
    /// <remarks>
    /// <para>Fait à chaque image, et non au tir : le champ est <b>permanent</b>, alors que les dégâts
    /// tombent par à-coups. Les aligner ferait clignoter la déformation au rythme de la recharge, et
    /// l'effet se lirait « ça vient de frapper » au lieu de « c'est en train de fondre ».</para>
    ///
    /// <para>Le composant est posé <b>à la demande</b>, comme les états : la faune atteint 300
    /// entités et la plupart ne croiseront jamais ce champ.</para>
    /// </remarks>
    private void Irradiate(float dt)
    {
        Vector2 center = transform.position;
        float sqr = ArcRadius * ArcRadius;

        WarpedCount = 0;

        foreach (var enemy in EnemyBase.Active)
        {
            if (enemy == null || enemy.IsDead) continue;

            float d2 = ((Vector2)enemy.transform.position - center).sqrMagnitude;
            if (d2 > sqr) continue;

            float t = Mathf.Sqrt(d2) / ArcRadius;
            float intensity = Mathf.Clamp01(Mathf.InverseLerp(1f, FullWarpFraction, t));

            var warp = enemy.GetComponent<IrradiationWarp>();
            if (warp == null) warp = enemy.gameObject.AddComponent<IrradiationWarp>();

            warp.Sustain(intensity);
            WarpedCount++;
        }

        EmitMotes(dt, center);
    }

    /// <summary>
    /// Motes de contamination : de rares grains qui montent du sol à l'intérieur du champ.
    /// </summary>
    /// <remarks>
    /// Ce sont eux qui font lire « radioactif » plutôt que « vert ». Un nuage seul est une teinte ;
    /// ce sont des particules qui s'en détachent une à une qui évoquent une émission. Rares à
    /// dessein — en additif, leur cumul déciderait de tout, et un champ actif en permanence a tout
    /// le temps d'en accumuler.
    /// </remarks>
    private void EmitMotes(float dt, Vector2 center)
    {
        _moteTimer += dt;
        if (_moteTimer < MoteInterval) return;

        _moteTimer = 0f;
        if (Vfx.ActiveEffects >= MoteBudget) return;

        // Racine du tirage : sans elle, les points s'agglutinent au centre du disque, la surface
        // croissant comme le carré du rayon.
        float r = Mathf.Sqrt((float)_moteRng.NextDouble()) * ArcRadius * 0.9f;
        float a = (float)_moteRng.NextDouble() * Mathf.PI * 2f;

        Vfx.Puff(center + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r),
                 new Color(Radiation.r, Radiation.g, Radiation.b, 0.3f),
                 radiusPx: 5f,
                 life: 0.8f,
                 riseSpeed: 34f,
                 drift: ((float)_moteRng.NextDouble() * 2f - 1f) * 12f,
                 growth: 0.7f,
                 order: VfxPrimitives.OrderGround);
    }

    /// <summary>
    /// Le coup ne se dessine pas comme un coup : c'est le champ entier qui <b>monte d'un cran</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ Le croissant hérité de la lame serait ici un <b>cercle complet</b> (demi-angle 180) : un
    /// contour net, quatre fois par seconde, autour d'un champ dont tout le propos est de ne pas
    /// avoir de bord. Et il tournait au hasard de la cible la plus proche, montrant une direction
    /// que l'arme n'a plus.
    /// </remarks>
    protected override void DrawSweep(Vector2 center, Vector2 direction, float halfArcDeg)
    {
        // Une bouffée large et brève au centre : le champ « respire » à chaque salve, sans qu'aucun
        // trait ne soit tracé.
        Vfx.Glow(center, Radiation, ArcRadius * 0.72f, 0.16f, 0.22f, VfxPrimitives.OrderGround);
    }

    private void EnsureField()
    {
        if (Field != null) return;

        var go = new GameObject("ChampIrradiant");
        go.transform.SetParent(transform, false);

        Field = go.AddComponent<AuraCloud>();

        // ⚠ L'alpha est celui d'UNE bouffée, et 0,07 rendait le champ **invisible** sur capture (voir
        // la note jumelle du Voile de Givre). Un peu plus bas que lui malgré tout : le vert est la
        // teinte la plus lumineuse de l'écran, et à opacité égale il pèse davantage.
        Field.Configure(ArcRadius, Radiation, alpha: 0.17f, count: 14, seed: 0x2AD10);
    }
}
