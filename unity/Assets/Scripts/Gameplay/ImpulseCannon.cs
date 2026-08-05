using UnityEngine;

/// <summary>
/// Canon à Impulsion — arme de départ de la Chimère. Tire un projectile sur l'ennemi le plus
/// proche. Portée ici comme arme témoin du Lot 2 : elle valide toute la chaîne
/// « viser → tirer → toucher → tuer → créditer l'XP ».
/// </summary>
public class ImpulseCannon : WeaponBase
{
    [Header("Projectile")]
    public GameObject? BulletPrefab;
    public float BulletSpeed = 600f;

    [Tooltip("Le projectile traverse ses cibles au lieu de s'arrêter à la première.")]
    public bool Piercing;

    protected override void Awake()
    {
        BaseDamage = 10f;
        BaseCooldown = 0.8f;
        Range = 400f;

        base.Awake();   // en dernier : il fige la valeur de fiche
    }

    protected override bool TryFire()
    {
        var target = FindNearestEnemy();
        if (target == null || BulletPrefab == null) return false;

        Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;

        var go = Instantiate(BulletPrefab, transform.position, Quaternion.identity);
        go.SetActive(true);   // sémantique Godot : un nœud instancié est toujours actif

        var bullet = go.GetComponent<Bullet>();
        if (bullet == null) { Destroy(go); return false; }

        bullet.Piercing = Piercing;
        bullet.Power = Level;
        bullet.Launch(dir * BulletSpeed, EffectiveDamage, Range);

        // Le départ du coup se voit au canon, pas seulement à l'arrivée : sans flash, une arme à
        // tir rapide semble faire apparaître ses projectiles à côté du joueur.
        Vfx.Muzzle(transform.position, dir);

        AudioSystem.PlaySfx("sfx_weapon_impulse_shoot");
        return true;
    }
}
