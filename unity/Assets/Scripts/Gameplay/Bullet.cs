using UnityEngine;

/// <summary>
/// Projectile — port de <c>Bullet</c>. Touche par <b>distance</b>, comme tout le reste du jeu :
/// aucun collider, donc aucun coût de physique pour les centaines de projectiles simultanés.
/// </summary>
public sealed class Bullet : MonoBehaviour
{
    [Tooltip("Rayon de collision avec un ennemi.")]
    public float HitRadius = 12f;

    /// <summary>
    /// Le projectile traverse ses cibles au lieu de s'arrêter à la première.
    /// </summary>
    /// <remarks>
    /// Un projectile perforant garde la liste de ce qu'il a déjà touché : sans elle, il infligerait
    /// ses dégâts <b>à chaque frame</b> tant qu'il chevauche un ennemi, ce qui en ferait de très
    /// loin l'arme la plus forte du jeu.
    /// </remarks>
    public bool Piercing;

    private readonly System.Collections.Generic.HashSet<EnemyBase> _pierced = new();
    private Vector2 _velocity;
    private float   _damage;
    private float   _rangeLeft;

    /// <summary>Arme le projectile. À appeler juste après l'instanciation.</summary>
    public void Launch(Vector2 velocity, float damage, float range)
    {
        _velocity  = velocity;
        _damage    = damage;
        _rangeLeft = range;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector2 step = _velocity * dt;

        transform.position += (Vector3)step;

        _rangeLeft -= step.magnitude;
        if (_rangeLeft <= 0f) { Destroy(gameObject); return; }

        Vector2 me = transform.position;
        float sqr = HitRadius * HitRadius;

        foreach (var e in EnemyBase.Active)
        {
            if (e == null || e.IsDead) continue;
            if (((Vector2)e.transform.position - me).sqrMagnitude > sqr) continue;

            if (Piercing)
            {
                if (!_pierced.Add(e)) continue;   // déjà traversé : pas de second coup
                e.TakeDamage(_damage);
                continue;                          // le projectile poursuit sa course
            }

            e.TakeDamage(_damage);
            Destroy(gameObject);
            return;
        }
    }
}
