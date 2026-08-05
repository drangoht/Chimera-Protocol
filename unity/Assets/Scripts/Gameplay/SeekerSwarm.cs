using UnityEngine;

/// <summary>
/// Essaim de Traqueurs — archétype <b>missiles guidés</b> (Lot 3).
///
/// <para>Tire plusieurs missiles qui corrigent leur trajectoire. Les cibles sont réparties entre les
/// missiles quand il y en a assez : concentrer toute la salve sur un seul ennemi gaspillerait les
/// dégâts en surtuant.</para>
/// </summary>
public class SeekerSwarm : WeaponBase
{
    [Header("Salve")]
    public int MissileCount = 2;
    public GameObject? MissilePrefab;
    public float ProjectileSpeed = 300f;

    /// <summary>Missiles émis lors de la dernière salve — observable pour les tests.</summary>
    public int LastSalvoSize { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 7f;
        BaseCooldown = 1.1f;
        Range = 460f;

        base.Awake();
    }

    public override void ApplyLevelStats(WeaponTable.WeaponLevelStats stats)
    {
        // Même omission que pour la Volée Dispersée : l'essaim restait à deux missiles à vie.
        MissileCount = Mathf.Max(1, stats.ProjectileCount);
        ProjectileSpeed = stats.ProjectileSpeed;
    }

    protected override bool TryFire()
    {
        if (MissilePrefab == null) return false;

        var first = FindNearestEnemy();
        if (first == null) return false;

        Vector2 origin = transform.position;
        LastSalvoSize = 0;

        for (int i = 0; i < MissileCount; i++)
        {
            var target = PickTarget(i) ?? first;

            // Départ en éventail : les missiles ne se superposent pas au tir, le guidage les
            // ramène ensuite. Sans cela, une salve ressemble à un seul projectile.
            float spread = (i - (MissileCount - 1) * 0.5f) * 25f * Mathf.Deg2Rad;
            Vector2 baseDir = ((Vector2)target.transform.position - origin).normalized;
            float c = Mathf.Cos(spread), s = Mathf.Sin(spread);
            Vector2 dir = new(baseDir.x * c - baseDir.y * s, baseDir.x * s + baseDir.y * c);

            var go = Instantiate(MissilePrefab, origin, Quaternion.identity);
            go.SetActive(true);

            var missile = go.GetComponent<SeekerMissile>();
            if (missile == null) { Destroy(go); continue; }

            missile.Speed = ProjectileSpeed;
            missile.Launch(dir, EffectiveDamage, target);
            LastSalvoSize++;
        }

        return LastSalvoSize > 0;
    }

    /// <summary>N-ième ennemi vivant à portée, pour répartir la salve.</summary>
    private EnemyBase? PickTarget(int index)
    {
        int seen = 0;
        float sqr = Range * Range;
        Vector2 me = transform.position;

        foreach (var e in EnemyBase.Active)
        {
            if (e == null || e.IsDead) continue;
            if (((Vector2)e.transform.position - me).sqrMagnitude > sqr) continue;
            if (seen++ == index) return e;
        }
        return null;
    }
}
