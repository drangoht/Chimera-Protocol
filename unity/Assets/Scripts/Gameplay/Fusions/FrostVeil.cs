using UnityEngine;

/// <summary>
/// Voile de Givre — évolution de <see cref="CryoLance"/> : le couloir devient une <b>aura radiale</b>
/// qui gèle tout autour du porteur.
///
/// <para>Le ralentissement passe de −20 % à −45 %, ce qui en fait un outil de survie autant que de
/// dégâts : à ce niveau, la nuée cesse d'être capable d'encercler.</para>
/// </summary>
public sealed class FrostVeil : WeaponBase
{
    [Header("Aura")]
    public float Radius = 150f;
    public float SlowMult = 0.55f;
    public float SlowDuration = 1.2f;

    /// <summary>Ennemis givrés au dernier tic — observable pour les tests et le HUD.</summary>
    public int LastAuraHits { get; private set; }

    /// <summary>Cristaux en orbite dans le voile.</summary>
    private const int CrystalCount = 7;

    /// <summary>Vitesse de rotation du voile, en radians/s.</summary>
    private const float SpinSpeed = 1.4f;

    private float _veilSpin;

    /// <summary>
    /// ⚠ Générateur PRIVÉ : <c>UnityEngine.Random</c> décalerait les tirages de gameplay d'une
    /// campagne à graine fixe. Un effet visuel ne doit jamais toucher l'aléatoire de la partie.
    /// </summary>
    private readonly System.Random _frostRng = new(0x5F0257);

    protected override void Awake()
    {
        BaseDamage = 7f;
        BaseCooldown = 0.35f;
        Range = Radius;
        base.Awake();
    }

    protected override bool TryFire()
    {
        Vector2 center = transform.position;
        float sqr = Radius * Radius;
        float damage = EffectiveDamage;

        LastAuraHits = 0;

        DrawVeil(center);

        var snapshot = EnemyBase.Active.ToArray();

        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;
            if (((Vector2)e.transform.position - center).sqrMagnitude > sqr) continue;

            e.ApplySlow(SlowMult, SlowDuration);
            e.TakeDamage(damage);
            LastAuraHits++;

            // Le gel se voit SUR la cible : quelques cristaux à l'impact, en plus de la teinte
            // bleue que porte tout ennemi ralenti. Sans cela, « il est gelé » se déduit d'une
            // vitesse, c'est-à-dire de rien pendant un combat.
            if (_frostRng.NextDouble() < 0.5)
                Vfx.Burst(e.transform.position, new Color(0.85f, 0.97f, 1f, 0.9f),
                          new Color(0.85f, 0.97f, 1f, 0f), 3, 8f, 34f, 4f, 0.35f);
        }

        return LastAuraHits > 0;
    }

    /// <summary>
    /// Le voile lui-même : un anneau, une brume centrale et des <b>cristaux qui tourbillonnent</b>.
    ///
    /// <para>Le portage n'en dessinait qu'un anneau cyan — la même forme que le Champ de Surcharge,
    /// que la Nova, que la portée d'une zone. Rien n'y disait le <i>givre</i>. Les cristaux tournent
    /// lentement autour du porteur et scintillent : c'est ce mouvement qui distingue une aura
    /// glaçante d'un cercle de dégâts.</para>
    /// </summary>
    private void DrawVeil(Vector2 center)
    {
        var ice = new Color(0.6f, 0.92f, 1f);

        Vfx.Ring(center, Radius, ice, 3f, 0.25f);

        // Brume : un halo large et faible, qui donne du volume au voile plutôt qu'un contour nu.
        Vfx.Glow(center, new Color(0.55f, 0.85f, 1f), Radius * 0.85f, 0.22f, 0.25f,
                 VfxPrimitives.OrderGround);

        _veilSpin += SpinSpeed * BaseCooldown;

        for (int i = 0; i < CrystalCount; i++)
        {
            float angle = _veilSpin + i * 2f * Mathf.PI / CrystalCount;

            // Les cristaux ne sont pas tous sur le même cercle : alignés, ils se liraient comme un
            // second anneau, et l'on retomberait sur la forme qu'on cherche à quitter.
            float r = Radius * (0.55f + 0.4f * Mathf.Abs(Mathf.Sin(angle * 1.7f)));
            var at = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;

            Vfx.Burst(at, new Color(0.9f, 0.98f, 1f, 0.85f), new Color(0.9f, 0.98f, 1f, 0f),
                      2, 6f, 26f, 3.5f, 0.4f);
        }
    }
}
