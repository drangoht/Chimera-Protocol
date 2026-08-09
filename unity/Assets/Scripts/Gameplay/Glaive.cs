using UnityEngine;

/// <summary>
/// Glaive — archétype <b>boomerang</b> (Lot 3). Lance un projectile qui part, revient, et peut
/// toucher à l'aller comme au retour.
/// </summary>
public sealed class Glaive : WeaponBase
{
    [Header("Projectile")]
    public GameObject? GlaivePrefab;

    /// <summary>
    /// Glaives lancés par salve. Monte de 1 à 3 avec le niveau (<c>glaiveCount</c>).
    /// </summary>
    public int GlaiveCount = 1;

    /// <summary>Glaives lancés — observable pour les tests et le HUD.</summary>
    public int LastThrows { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 10f;
        BaseCooldown = 1.3f;
        Range = 240f;

        base.Awake();
    }

    /// <summary>
    /// Applique la FORME du palier, et pas seulement ses chiffres.
    /// </summary>
    /// <remarks>
    /// ⚠ Sans cette lecture, le glaive restait a UN exemplaire au niveau 20 au lieu de trois :
    /// l'arme montait en degats et gardait sa forme de depart. Le portage ne lisait que six
    /// des seize cles de palier — huit armes etaient concernees.
    /// </remarks>
    public override void ApplyLevelStats(WeaponTable.WeaponLevelStats stats)
    {
        GlaiveCount = Mathf.Max(1, stats.ShapeInt("glaiveCount", GlaiveCount));
        Range       = stats.Shape("range", Range);
    }

    protected override bool TryFire()
    {
        var target = FindNearestEnemy();
        if (target == null || GlaivePrefab == null) return false;

        Vector2 origin = transform.position;
        Vector2 toTarget = ((Vector2)target.transform.position - origin).normalized;

        // Les glaives supplémentaires partent en ÉVENTAIL et non superposés : trois trajectoires
        // confondues se lisent comme un seul glaive plus fort, ce qui perd toute la montée.
        int count = Mathf.Max(1, GlaiveCount);
        const float spread = 26f;
        float start = -spread * (count - 1) * 0.5f;

        bool any = false;

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(GlaivePrefab, origin, Quaternion.identity);
            go.SetActive(true);

            var glaive = go.GetComponent<GlaiveProjectile>();
            if (glaive == null) { Destroy(go); continue; }

            Vector2 dir = Quaternion.Euler(0f, 0f, start + spread * i) * toTarget;
            glaive.Launch(dir, EffectiveDamage, Range);

            LastThrows++;
            any = true;
        }

        return any;
    }
}
