using UnityEngine;

/// <summary>
/// Lame Plasma — archétype <b>arc de mêlée</b> (Lot 3).
///
/// <para>Balaie un arc devant le joueur. Deux conditions doivent être réunies pour toucher : être
/// dans le <b>rayon</b> et dans l'<b>angle</b>. C'est ce second filtre qui distingue une lame d'une
/// aura — le porteur doit orienter son attaque, donc son déplacement.</para>
///
/// <para>L'arc est dirigé vers l'ennemi le plus proche, comme sous Godot : l'arme vise, elle ne
/// suit pas la direction de marche. Un joueur qui recule continue donc de frapper ce qui le
/// poursuit.</para>
/// </summary>
public class PlasmaBlade : WeaponBase
{
    [Header("Arc")]
    [Tooltip("Ouverture totale de l'arc, en degrés.")]
    public float ArcAngleDeg = 180f;

    public float ArcRadius = 80f;

    /// <summary>Ennemis touchés par le dernier coup — observable pour les tests et le HUD.</summary>
    public int LastSweepHits { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 18f;
        BaseCooldown = 1.2f;
        Range = ArcRadius;

        // base.Awake() EN DERNIER : c'est lui qui fige la valeur de fiche, et il doit donc
        // voir celles posées ci-dessus. Même exigence d'ordre que le `base._Ready()` de Godot,
        // pour une raison différente — ici c'est la capture, là-bas l'initialisation.
        base.Awake();
    }

    protected override bool TryFire()
    {
        var target = FindNearestEnemy();
        if (target == null) return false;

        Vector2 center = transform.position;
        Vector2 dir = ((Vector2)target.transform.position - center).normalized;

        float halfArc = ArcAngleDeg * 0.5f;
        float sqr = ArcRadius * ArcRadius;
        float damage = EffectiveDamage;

        LastSweepHits = 0;

        // L'arc se dessine même s'il ne touche personne : c'est lui qui dit au joueur où frappe son
        // arme, et une arme de mêlée invisible passe pour une carte sans effet. Le croissant balayé
        // (et non un arc figé) est ce qui le fait lire comme un COUP.
        Vfx.Crescent(center, dir, halfArc, ArcRadius);

        var snapshot = EnemyBase.Active.ToArray();

        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;

            Vector2 offset = (Vector2)e.transform.position - center;
            if (offset.sqrMagnitude > sqr) continue;

            // Angle non signé entre la direction d'attaque et la cible : un arc est symétrique.
            if (Vector2.Angle(dir, offset.normalized) > halfArc) continue;

            e.TakeDamage(damage);
            LastSweepHits++;
        }

        return LastSweepHits > 0;
    }
}
