using UnityEngine;

/// <summary>
/// Champ de Surcharge — archétype <b>aura pulsée</b> (Lot 3).
///
/// <para>Frappe périodiquement <b>tout</b> ce qui se trouve dans un rayon autour du joueur, avec un
/// recul. Contrairement à une aura continue, les dégâts sont <b>discrets</b> : une impulsion toutes
/// les <c>Cooldown</c> secondes.</para>
///
/// <para>⚠ La distinction discret/continu n'est pas cosmétique dans ce jeu : le cran de saturation VI
/// applique un plancher de dégâts en pourcentage des PV max qui ne doit <b>jamais</b> toucher un
/// dégât continu — appliqué à chaque tick, il tuerait en quelques frames. Une arme pulsée comme
/// celle-ci relève bien du chemin discret.</para>
/// </summary>
/// <remarks>
/// <para><b>Signalée « trop discrète » en jouant le 2026-08-13, pour deux raisons superposées.</b></para>
///
/// <para><b>1. Elle ne grandissait pas.</b> <c>weapons.json</c> déclare <c>radius</c> 100 → 200 px et
/// <c>knockbackPx</c> 40 → 60 sur cinq paliers ; cette classe n'implémentait pas
/// <see cref="ApplyLevelStats"/>, donc les deux clés n'étaient <b>lues par personne</b>. Un joueur
/// qui la montait au niveau 5 gardait la zone du niveau 1 — la moitié du rayon promis, sur la seule
/// arme du jeu dont le rayon <i>est</i> la mécanique. Onzième occurrence de « déclaré n'est pas
/// consommé », et la première où le défaut se plaint de lui-même : une arme qui ne grandit pas
/// <b>se voit</b>, contrairement à un cran de saturation inopérant.</para>
///
/// <para><b>2. Elle n'existait que 9 % du temps.</b> Une onde de 0,22 s toutes les 2,5 s, et rien
/// entre les deux — pas même sur les ennemis frappés, qui encaissaient et reculaient de 40 px sans
/// qu'aucun trait ne relie la cause à l'effet. D'où l'aura <b>permanente</b> qui se charge au rythme
/// de la recharge, et l'arc électrique tiré vers chaque cible : c'est lui qui dit « c'est MOI qui
/// t'ai touché », dans une mêlée où dix choses infligent des dégâts en même temps.</para>
/// </remarks>
public class OverloadField : WeaponBase
{
    [Header("Champ")]
    public float Radius = 100f;

    [Tooltip("Distance de recul appliquée aux ennemis touchés.")]
    public float Knockback = 40f;

    /// <summary>Ennemis touchés par la dernière impulsion — observable pour les tests et le HUD.</summary>
    public int LastPulseHits { get; private set; }

    /// <summary>Nuage porté, ou <c>null</c> avant la première image — observable pour les captures.</summary>
    public AuraCloud? Field { get; private set; }

    /// <summary>Violet #AA44FF — la teinte de l'énergie dans tout le jeu.</summary>
    private static readonly Color Violet = new(0.667f, 0.267f, 1.000f);

    /// <summary>Cœur de la décharge, tiré vers le blanc : une surcharge sature.</summary>
    private static readonly Color Core = new(0.870f, 0.640f, 1.000f);

    /// <summary>
    /// Nombre maximal d'arcs dessinés par impulsion.
    /// </summary>
    /// <remarks>
    /// L'arme frappe <b>tout</b> ce qui est à portée, et une nuée en compte facilement soixante. Un
    /// arc par cible viderait le vivier partagé d'effets en une impulsion — les traces des autres
    /// armes disparaîtraient alors sans erreur ni symptôme. Dix suffisent à dire « ça frappe le
    /// groupe » ; au-delà, l'information ne s'ajoute plus, elle se superpose.
    /// </remarks>
    private const int MaxArcs = 10;

    /// <summary>Bouffées du nuage. Assez pour une masse, pas au point d'en faire un disque plein.</summary>
    private const int PuffCount = 10;

    protected override void Awake()
    {
        BaseDamage = 8f;
        BaseCooldown = 2.5f;
        Range = Radius;

        // base.Awake() EN DERNIER : c'est lui qui fige la valeur de fiche, et il doit donc
        // voir celles posées ci-dessus. Même exigence d'ordre que le `base._Ready()` de Godot,
        // pour une raison différente — ici c'est la capture, là-bas l'initialisation.
        base.Awake();
    }

    /// <summary>
    /// Applique la <b>géométrie</b> du palier. Sans elle, l'arme montait en dégâts et en cadence sans
    /// jamais gagner un pixel de portée ni de recul.
    /// </summary>
    /// <remarks>
    /// ⚠ <see cref="Range"/> suit <see cref="Radius"/> : c'est lui que <c>WeaponBase</c> emploie pour
    /// chercher une cible, et les laisser diverger ferait chercher dans un rayon et frapper dans un
    /// autre. ⚠ Cette méthode n'est <b>pas</b> appelée pour l'Égide de Surcharge : une fusion passe
    /// par <c>ApplyFusionStats</c>, qui ne touche qu'aux dégâts — l'Égide garde donc la géométrie
    /// posée dans son propre <c>Awake</c>.
    /// </remarks>
    public override void ApplyLevelStats(WeaponTable.WeaponLevelStats stats)
    {
        Radius = stats.Shape("radius", Radius);
        Knockback = stats.Shape("knockbackPx", Knockback);
        Range = Radius;
    }

    protected override void Update()
    {
        base.Update();
        DriveField();
    }

    /// <summary>
    /// Entretient l'aura permanente : elle <b>se charge</b> entre deux impulsions.
    /// </summary>
    /// <remarks>
    /// <para>L'opacité suit le carré de l'avancement — donc reste basse longtemps, puis monte
    /// franchement sur le dernier tiers. Une montée linéaire se lit comme une lueur qui varie sans
    /// raison ; une montée tardive se lit comme une <b>charge</b>, et annonce l'impulsion assez tôt
    /// pour qu'on décide de rester ou de fuir.</para>
    ///
    /// <para>⚠ <c>Configure</c> à effectif <b>constant</b> ne recrée rien : il se contente de
    /// reteinter les bouffées existantes. L'appeler à chaque image est donc bon marché — et c'est ce
    /// qui permet au rayon de suivre la montée de niveau sans code de synchronisation.</para>
    /// </remarks>
    private void DriveField()
    {
        if (Field == null)
        {
            var go = new GameObject("ChampDeSurcharge");
            go.transform.SetParent(transform, false);
            Field = go.AddComponent<AuraCloud>();
        }

        float charge = ChargeRatio;

        // ⚠ Opacité d'UNE bouffée, pas du nuage : dix disques additifs se recouvrent largement.
        //
        // ⚠⚠ Premières valeurs 0,05 → 0,19, par report direct du Voile de Givre (0,22 constant) : à
        // la capture, le nuage était bien là pendant la décharge — où les lueurs des arcs s'y
        // ajoutent — et **invisible entre deux impulsions**, c'est-à-dire pendant les 90 % du temps
        // qu'il est censé couvrir. Troisième fois que la prudence sur l'opacité produit un effet
        // absent. Le plancher compte ici plus que le sommet : c'est lui qu'on regarde le plus
        // longtemps.
        float alpha = Mathf.Lerp(0.09f, 0.26f, charge * charge);

        Field.Configure(Radius, Violet, alpha, PuffCount, seed: 0x0FE1D);
    }

    protected override bool TryFire()
    {
        Vector2 center = transform.position;
        float sqr = Radius * Radius;
        float damage = EffectiveDamage;

        LastPulseHits = 0;
        int arcs = 0;

        // Copie de sécurité : TakeDamage peut tuer, donc retirer des éléments de la liste pendant
        // qu'on la parcourt.
        var snapshot = EnemyBase.Active.ToArray();

        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;

            Vector2 offset = (Vector2)e.transform.position - center;
            if (offset.sqrMagnitude > sqr) continue;

            float dist = offset.magnitude;
            Vector2 dir = dist > 0.01f ? offset / dist : Vector2.right;
            var landing = (Vector2)e.transform.position + dir * Knockback;
            e.transform.position = landing;

            // L'arc vise le point d'ARRIVÉE : il montre du même trait qui est frappé et où il est
            // repoussé. Visant le départ, il désignerait une case que l'ennemi a déjà quittée.
            if (arcs < MaxArcs) { Vfx.Bolt(center, landing, Core); arcs++; }

            e.TakeDamage(damage);
            LastPulseHits++;
        }

        // L'onde n'est dessinée que si l'impulsion part vraiment : TryFire est appelée à chaque
        // frame une fois la recharge prête, donc dessiner ici sans condition afficherait une aura
        // permanente qui ne dirait plus rien du rythme de l'arme.
        if (LastPulseHits > 0) DrawPulse(center);

        // Une impulsion dans le vide ne doit pas consommer la recharge : sinon l'arme se déclenche
        // sans effet pendant les creux et se retrouve en recharge quand la nuée revient.
        return LastPulseHits > 0;
    }

    /// <summary>
    /// La décharge : deux ondes, un éclat, une secousse — le tout à l'échelle du <b>niveau</b>.
    /// </summary>
    /// <remarks>
    /// <para>Deux ondes plutôt qu'une, et de rayons différents : une seule onde qui se dilate se lit
    /// comme un cercle de portée, deux qui se suivent se lisent comme une <b>détonation</b>. La plus
    /// grande atteint exactement <see cref="Radius"/> — c'est elle qui enseigne au joueur jusqu'où
    /// porte son champ, une information que rien d'autre ne donne.</para>
    ///
    /// <para>La secousse suit le niveau, comme <c>Vfx.Impact</c> : un tir de niveau 5 ne doit pas
    /// cogner comme un tir de niveau 1, sans quoi la progression ne se voit que dans les chiffres.
    /// Elle reste courte (0,09 s) — l'arme part toutes les 1,5 s en fin de partie, et une secousse
    /// longue à cette cadence rendrait l'écran illisible en permanence.</para>
    /// </remarks>
    private void DrawPulse(Vector2 center)
    {
        int p = Mathf.Clamp(Level, 1, 8);

        Vfx.Shockwave(center, Radius, 0.30f, Violet);
        Vfx.Shockwave(center, Radius * 0.55f, 0.20f, Core);
        Vfx.Glow(center, Core, Radius * 0.30f, 0.9f, 0.20f);

        Vfx.Burst(center, Core, new Color(Violet.r, Violet.g, Violet.b, 0f),
                  10 + p * 3, Radius * 1.2f, Radius * 2.6f, 5f + p * 0.6f, 0.28f);

        ScreenShake.Shake(1.4f + p * 0.5f, 0.09f);
    }
}
