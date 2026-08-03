using UnityEngine;

/// <summary>
/// Glaive — archétype <b>boomerang</b> (Lot 3). Lance un projectile qui part, revient, et peut
/// toucher à l'aller comme au retour.
/// </summary>
public sealed class Glaive : WeaponBase
{
    [Header("Projectile")]
    public GameObject? GlaivePrefab;

    /// <summary>Glaives lancés — observable pour les tests et le HUD.</summary>
    public int LastThrows { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 10f;
        BaseCooldown = 1.3f;
        Range = 240f;

        base.Awake();
    }

    protected override bool TryFire()
    {
        var target = FindNearestEnemy();
        if (target == null || GlaivePrefab == null) return false;

        Vector2 origin = transform.position;
        Vector2 dir = ((Vector2)target.transform.position - origin).normalized;

        var go = Instantiate(GlaivePrefab, origin, Quaternion.identity);
        go.SetActive(true);

        var glaive = go.GetComponent<GlaiveProjectile>();
        if (glaive == null) { Destroy(go); return false; }

        glaive.Launch(dir, EffectiveDamage, Range);
        LastThrows++;
        return true;
    }
}
