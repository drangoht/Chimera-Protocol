using UnityEngine;

/// <summary>
/// Lance Cryogénique — archétype <b>rayon en couloir</b> avec ralentissement (Lot 3).
///
/// <para>Frappe tout ce qui se trouve dans un <b>couloir</b> devant le porteur : un segment de
/// longueur <see cref="Range"/> et de demi-largeur <see cref="HalfWidth"/>. Contrairement à l'arc de
/// la lame, la zone ne s'élargit pas avec la distance — c'est ce qui en fait une arme de percée
/// plutôt que de nettoyage.</para>
///
/// <para>Le ralentissement est sa vraie valeur : il ne tue pas vite, il achète du temps.</para>
/// </summary>
public sealed class CryoLance : WeaponBase
{
    [Header("Couloir")]
    [Tooltip("Demi-largeur du couloir touché.")]
    public float HalfWidth = 16f;

    [Header("Givre")]
    [Tooltip("Multiplicateur de vitesse appliqué (0,80 = -20 %).")]
    public float SlowMult = 0.80f;

    public float SlowDuration = 1.5f;

    /// <summary>Ennemis touchés par le dernier tir — observable pour les tests et le HUD.</summary>
    public int LastBeamHits { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 9f;
        BaseCooldown = 1.4f;
        Range = 360f;

        base.Awake();   // en dernier : c'est lui qui fige la valeur de fiche
    }

    /// <summary>
    /// Applique la FORME du palier, et pas seulement ses chiffres.
    /// </summary>
    /// <remarks>
    /// ⚠ Sans cette lecture, la lance gardait sa portee et sa duree de gel du niveau 1 :
    /// l'arme montait en degats et gardait sa forme de depart. Le portage ne lisait que six
    /// des seize cles de palier — huit armes etaient concernees.
    /// </remarks>
    public override void ApplyLevelStats(WeaponTable.WeaponLevelStats stats)
    {
        Range        = stats.Shape("range", Range);
        SlowDuration = stats.Shape("slowDuration", SlowDuration);

        // ⚠ Ajoutée le 2026-08-13 : `slowMult` était déclarée par palier et lue par personne — la
        // lance ralentissait donc toujours de 20 %, quel que soit son niveau. C'est la statistique
        // qui *définit* l'arme, et la seule que le joueur ne pouvait pas voir progresser.
        SlowMult     = stats.Shape("slowMult", SlowMult);
    }

    protected override bool TryFire()
    {
        var target = FindNearestEnemy();
        if (target == null) return false;

        Vector2 origin = transform.position;
        Vector2 dir = ((Vector2)target.transform.position - origin).normalized;

        LastBeamHits = 0;
        float damage = EffectiveDamage;

        // Le couloir de gel se dessine sur toute sa portée : c'est un tir de zone, pas un projectile,
        // et rien d'autre n'en signale la largeur ni l'axe. Les éclats de givre naissent LE LONG du
        // faisceau, ce qui distingue un rayon glacé d'un simple trait bleu.
        Vfx.Frost(origin, dir, Range, Level);

        var snapshot = EnemyBase.Active.ToArray();

        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;

            Vector2 offset = (Vector2)e.transform.position - origin;

            // Projection sur l'axe du tir : derrière le porteur ou au-delà de la portée → hors couloir.
            float along = Vector2.Dot(offset, dir);
            if (along < 0f || along > Range) continue;

            // Distance perpendiculaire à l'axe : c'est elle qui définit la largeur du couloir.
            float across = Mathf.Abs(offset.x * dir.y - offset.y * dir.x);
            if (across > HalfWidth) continue;

            e.ApplySlow(SlowMult, SlowDuration);
            e.TakeDamage(damage);
            LastBeamHits++;
        }

        return LastBeamHits > 0;
    }
}
