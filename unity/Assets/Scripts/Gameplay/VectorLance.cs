using UnityEngine;

/// <summary>
/// Lance Vectorielle — archétype <b>dirigé par le joueur</b> (Lot 3).
///
/// <para><b>La seule arme qui ne vise pas toute seule.</b> Elle tire dans la direction de visée du
/// joueur (<see cref="Player.AimDirection"/> — souris ou stick droit), pas vers l'ennemi le plus
/// proche. C'est ce qui en fait une arme d'adresse, et c'est aussi pourquoi elle ne doit
/// <b>jamais</b> passer par <c>FindNearestEnemy</c> : le faire la transformerait silencieusement en
/// canon automatique et supprimerait tout son intérêt.</para>
///
/// <para>⚠ <b>Elle était signalée « ne fonctionne pas », et c'était juste — sans qu'un seul tir ne
/// manque.</b> Trois choses lui manquaient, chacune invisible au code : la visée du joueur suivait
/// la direction de <i>déplacement</i> (voir <c>Player.UpdateAim</c>), le trait ne <b>perforait
/// pas</b>, et l'<b>éventail des niveaux 4-5 n'était jamais appliqué</b>. Une arme d'adresse sans
/// visée, sans traversée et sans progression n'est plus une arme d'adresse.</para>
/// </summary>
public sealed class VectorLance : WeaponBase
{
    [Header("Projectile")]
    public GameObject? BulletPrefab;
    public float ProjectileSpeed = 520f;

    /// <summary>Projectiles par tir — 1 jusqu'au niveau 3, puis 2 et 3.</summary>
    public int ProjectileCount = 1;

    /// <summary>Le trait traverse-t-il ses cibles ? Vrai à tous les niveaux dans les données.</summary>
    public bool IsPiercing = true;

    /// <summary>Amplitude totale de l'éventail, en degrés. 0 = tir unique droit.</summary>
    public float SpreadDegrees;

    /// <summary>Tirs partis — observable pour les tests et le HUD.</summary>
    public int LastShots { get; private set; }

    /// <summary>Projectiles du dernier tir — observable, c'est ce que l'éventail change.</summary>
    public int LastVolleySize { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 16f;
        BaseCooldown = 0.75f;
        Range = 520f;

        base.Awake();
    }

    public override void ApplyLevelStats(WeaponTable.WeaponLevelStats stats)
    {
        ProjectileCount = Mathf.Max(1, stats.ProjectileCount);
        ProjectileSpeed = stats.ProjectileSpeed;
        IsPiercing = stats.Piercing;
        SpreadDegrees = stats.SpreadDegrees;
    }

    protected override bool TryFire()
    {
        var player = Player.Instance;
        if (player == null || BulletPrefab == null) return false;

        Vector2 aim = player.AimDirection;
        if (aim.sqrMagnitude < 0.001f) return false;

        aim = aim.normalized;

        int count = Mathf.Max(1, ProjectileCount);
        int fired = 0;

        for (int i = 0; i < count; i++)
        {
            // Éventail centré sur la visée : décalage nul quand un seul projectile part, sinon
            // réparti de −moitié à +moitié.
            float offset = count > 1
                ? -SpreadDegrees * 0.5f + SpreadDegrees * i / (count - 1)
                : 0f;

            Vector2 dir = Rotate(aim, offset * Mathf.Deg2Rad);

            var go = Instantiate(BulletPrefab, transform.position, Quaternion.identity);
            go.SetActive(true);

            var bullet = go.GetComponent<Bullet>();
            if (bullet == null) { Destroy(go); continue; }

            bullet.Power = Level;

            // ⚠ À poser AVANT `Launch` : le projectile résout sa première collision dès sa mise en
            // mouvement, et un trait « perforant » qui s'arrête sur le premier ennemi rencontré est
            // exactement ce qui faisait passer cette arme pour cassée dans une nuée.
            bullet.Piercing = IsPiercing;

            bullet.Launch(dir * ProjectileSpeed, EffectiveDamage, Range);

            // Arme DIRIGÉE : le flash de bouche est ici le seul retour immédiat sur la direction
            // visée, le projectile perforant n'ayant pas de cible à atteindre pour la confirmer.
            Vfx.Muzzle(transform.position, dir);

            fired++;
        }

        // Le son part de WeaponBase (WeaponSfx), sur le « vrai » renvoi de tir : une salve qui
        // n'envoie aucun projectile ne doit pas plus s'entendre qu'elle ne doit consommer sa recharge.
        if (fired == 0) return false;

        LastVolleySize = fired;
        LastShots++;
        return true;
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
