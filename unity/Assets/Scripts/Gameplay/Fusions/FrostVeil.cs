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

        // Voile permanent : l'anneau redessiné à chaque battement tient lieu d'aura continue.
        Vfx.Ring(center, Radius, new Color(0.6f, 0.92f, 1f), 3f, 0.25f);

        var snapshot = EnemyBase.Active.ToArray();

        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;
            if (((Vector2)e.transform.position - center).sqrMagnitude > sqr) continue;

            e.ApplySlow(SlowMult, SlowDuration);
            e.TakeDamage(damage);
            LastAuraHits++;
        }

        return LastAuraHits > 0;
    }
}
