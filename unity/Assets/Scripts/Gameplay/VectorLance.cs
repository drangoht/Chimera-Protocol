using UnityEngine;

/// <summary>
/// Lance Vectorielle — archétype <b>dirigé par le joueur</b> (Lot 3).
///
/// <para><b>La seule arme qui ne vise pas toute seule.</b> Elle tire dans la direction de visée du
/// joueur (<see cref="Player.AimDirection"/> — souris ou stick droit), pas vers l'ennemi le plus
/// proche. C'est ce qui en fait une arme d'adresse, et c'est aussi pourquoi elle ne doit
/// <b>jamais</b> passer par <c>FindNearestEnemy</c> : le faire la transformerait silencieusement en
/// canon automatique et supprimerait tout son intérêt.</para>
/// </summary>
public sealed class VectorLance : WeaponBase
{
    [Header("Projectile")]
    public GameObject? BulletPrefab;
    public float ProjectileSpeed = 520f;

    /// <summary>Tirs partis — observable pour les tests et le HUD.</summary>
    public int LastShots { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 16f;
        BaseCooldown = 0.75f;
        Range = 520f;

        base.Awake();
    }

    protected override bool TryFire()
    {
        var player = Player.Instance;
        if (player == null || BulletPrefab == null) return false;

        Vector2 dir = player.AimDirection;
        if (dir.sqrMagnitude < 0.001f) return false;

        var go = Instantiate(BulletPrefab, transform.position, Quaternion.identity);
        go.SetActive(true);

        var bullet = go.GetComponent<Bullet>();
        if (bullet == null) { Destroy(go); return false; }

        bullet.Power = Level;
        bullet.Launch(dir.normalized * ProjectileSpeed, EffectiveDamage, Range);

        // Arme DIRIGÉE : le flash de bouche est ici le seul retour immédiat sur la direction visée,
        // le projectile perforant n'ayant pas de cible à atteindre pour la confirmer.
        Vfx.Muzzle(transform.position, dir.normalized);

        LastShots++;
        return true;
    }
}
