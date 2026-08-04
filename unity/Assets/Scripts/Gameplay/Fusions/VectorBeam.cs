using UnityEngine;

/// <summary>
/// Rayon Vectoriel — évolution de <see cref="VectorLance"/> : le tir devient un <b>rayon perforant
/// continu</b> dirigé, qui traverse tout dans l'axe de visée.
///
/// <para>C'est la fusion qui change le plus la nature de son arme : on ne tire plus des
/// projectiles, on balaie. Elle reste <b>dirigée par le joueur</b>, ce qui préserve l'intérêt de
/// l'arme d'origine — la transformer en visée automatique la viderait de son sens.</para>
/// </summary>
public sealed class VectorBeam : WeaponBase
{
    [Header("Rayon")]
    [Tooltip("Demi-largeur du rayon.")]
    public float HalfWidth = 34f;

    /// <summary>Ennemis touchés par le dernier balayage — observable pour les tests et le HUD.</summary>
    public int LastBeamHits { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 11f;
        BaseCooldown = 0.15f;   // quasi continu
        Range = 620f;
        base.Awake();
    }

    protected override bool TryFire()
    {
        var player = Player.Instance;
        if (player == null) return false;

        Vector2 dir = player.AimDirection;
        if (dir.sqrMagnitude < 0.001f) return false;
        dir.Normalize();

        Vector2 origin = transform.position;
        float damage = EffectiveDamage;

        LastBeamHits = 0;

        // Le faisceau part de la VISÉE du joueur, pas de la cible la plus proche : sans trait, rien
        // ne dit où il pointe, et l'arme paraît tirer au hasard.
        WeaponVfx.Line(origin, origin + dir * Range, new Color(1f, 0.85f, 0.35f), 13f, 0.18f, 14f);

        var snapshot = EnemyBase.Active.ToArray();

        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;

            Vector2 offset = (Vector2)e.transform.position - origin;

            float along = Vector2.Dot(offset, dir);
            if (along < 0f || along > Range) continue;

            float across = Mathf.Abs(offset.x * dir.y - offset.y * dir.x);
            if (across > HalfWidth) continue;

            e.TakeDamage(damage);
            LastBeamHits++;
        }

        return LastBeamHits > 0;
    }
}
