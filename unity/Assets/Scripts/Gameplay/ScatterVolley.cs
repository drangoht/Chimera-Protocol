using UnityEngine;

/// <summary>
/// Volée Dispersée — archétype <b>salve multi-cibles</b> (Lot 3).
///
/// <para>Tire plusieurs projectiles d'un coup. Sous Godot, la volée vise les
/// <c>ProjectileCount</c> ennemis les plus proches et complète en éventail quand il y a moins de
/// cibles que de projectiles — c'est ce qui évite qu'une salve soit gaspillée face à un ennemi
/// isolé.</para>
/// </summary>
public sealed class ScatterVolley : WeaponBase
{
    [Header("Salve")]
    public int ProjectileCount = 2;

    [Tooltip("Écart angulaire entre deux projectiles de complément, en degrés.")]
    public float SpreadDeg = 14f;

    public GameObject? BulletPrefab;
    public float BulletSpeed = 500f;

    /// <summary>Projectiles émis lors de la dernière salve — observable pour les tests.</summary>
    public int LastVolleySize { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 7f;
        BaseCooldown = 1.1f;
        Range = 420f;

        // base.Awake() EN DERNIER : c'est lui qui fige la valeur de fiche, et il doit donc
        // voir celles posées ci-dessus. Même exigence d'ordre que le `base._Ready()` de Godot,
        // pour une raison différente — ici c'est la capture, là-bas l'initialisation.
        base.Awake();
    }

    protected override bool TryFire()
    {
        if (BulletPrefab == null) return false;

        var first = FindNearestEnemy();
        if (first == null) return false;

        Vector2 origin = transform.position;
        Vector2 baseDir = ((Vector2)first.transform.position - origin).normalized;

        LastVolleySize = 0;

        for (int i = 0; i < ProjectileCount; i++)
        {
            // Éventail symétrique autour de la direction de base : les indices pairs partent d'un
            // côté, les impairs de l'autre, ce qui garde la salve centrée sur la cible.
            int step = (i + 1) / 2;
            float sign = (i % 2 == 0) ? 1f : -1f;
            float angle = step * SpreadDeg * sign * (i == 0 ? 0f : 1f);

            Vector2 dir = Rotate(baseDir, angle * Mathf.Deg2Rad);

            var go = Instantiate(BulletPrefab, origin, Quaternion.identity);
            go.SetActive(true);   // sémantique Godot

            var bullet = go.GetComponent<Bullet>();
            if (bullet == null) { Destroy(go); continue; }

            bullet.Power = Level;
            bullet.Launch(dir * BulletSpeed, EffectiveDamage, Range);
            Vfx.Muzzle(origin, dir);
            LastVolleySize++;
        }

        return LastVolleySize > 0;
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
