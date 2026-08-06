using UnityEngine;

/// <summary>
/// Rail Surchargé — évolution de <see cref="ImpulseCannon"/> : cadence doublée et projectiles
/// <b>perforants</b>, qui traversent la file au lieu de s'arrêter au premier corps.
///
/// <para><b>Il se voyait moins que l'arme dont il est l'évolution</b>, et pour une raison qui n'a
/// rien d'un oubli : son projectile va plus vite (800 px/s contre 600), donc il traverse l'écran en
/// moins d'images, et à halo égal l'œil n'en retient rien. Une montée en puissance se lisait comme
/// une perte. Un projectile rapide a besoin de <i>plus</i> de présence qu'un lent, pas d'autant.</para>
/// </summary>
public sealed class RailOvercharged : ImpulseCannon
{
    /// <summary>
    /// Renfort de halo et de traînée. <b>Discret à dessein</b> : l'arme tire toutes les 0,6 s et son
    /// trait perfore, donc plusieurs projectiles vivent ensemble à l'écran — c'est leur cumul qui
    /// décide de la lisibilité, jamais l'exemplaire isolé.
    /// </summary>
    private const float RailPresence = 1.7f;

    /// <summary>
    /// Violet du rail. La teinte n'est pas décorative : c'est elle qui permet de distinguer, dans une
    /// volée, ce qui vient du rail de ce qui vient du canon — les deux peuvent être portés ensemble.
    /// Elle est reprise du violet de la palette, donc de la même famille que le reste du HUD.
    /// </summary>
    private static readonly Color RailTint = new(0.78f, 0.44f, 1f);

    /// <summary>Projectiles par rafale — <c>burstCount</c> de <c>weapons.json</c>.</summary>
    private const int BurstCount = 3;

    /// <summary>Secondes entre deux coups d'une même rafale — <c>burstInterval</c>.</summary>
    private const float BurstInterval = 0.12f;

    private int _burstLeft;
    private float _burstTimer;

    /// <summary>Coups tirés dans la dernière rafale — observable pour les vérifications.</summary>
    public int LastBurstShots { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 22f;

        // ⚠ `cooldownBetweenBursts`, et non « cadence » : c'est l'attente entre deux RAFALES.
        BaseCooldown = 0.6f;
        BulletSpeed = 600f;   // valeur déclarée (`projectileSpeed`), et non les 800 improvisés
        Piercing = true;
        base.Awake();
    }

    /// <summary>
    /// Ouvre une rafale : un coup part tout de suite, les suivants sont dus.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>weapons.json</c> décrit cette arme comme « une rafale automatique de <b>3</b> projectiles
    /// perforants », avec <c>burstCount</c>, <c>burstInterval</c> et <c>cooldownBetweenBursts</c>.
    /// Le portage n'en tirait qu'<b>un</b>, et son unique différence avec le canon de base était sa
    /// cadence : la fusion la plus lisible du jeu ressemblait à une amélioration de statistique.
    /// C'est la troisième fois qu'une donnée déclarée n'est consommée par personne — après
    /// <c>projectileCount</c> et la table de fusions elle-même.
    /// </remarks>
    protected override bool TryFire()
    {
        if (!FireOne()) return false;

        _burstLeft = BurstCount - 1;
        _burstTimer = BurstInterval;
        LastBurstShots = 1;
        return true;
    }

    protected override void Update()
    {
        base.Update();

        if (_burstLeft <= 0) return;

        _burstTimer -= Time.deltaTime;
        if (_burstTimer > 0f) return;

        _burstTimer = BurstInterval;
        _burstLeft--;

        // Chaque coup revise : la rafale suit une nuée qui bouge, au lieu de vider trois projectiles
        // sur une cible déjà morte au premier.
        if (FireOne()) LastBurstShots++;
    }

    protected override void ConfigureBullet(Bullet bullet)
    {
        bullet.Presence = RailPresence;
        bullet.SetTint(RailTint);
    }
}
