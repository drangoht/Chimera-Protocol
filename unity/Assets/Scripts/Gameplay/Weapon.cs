using UnityEngine;

/// <summary>
/// Socle des armes — port de <c>WeaponBase</c> (Lot 2).
///
/// <para><b>Un piège de Godot disparaît ici, un autre le remplace.</b> Sous Godot,
/// <c>base._Ready()</c> devait impérativement être appelé <b>en dernier</b> dans les 19 armes, sans
/// quoi l'initialisation écrasait les réglages de la sous-classe. Unity n'a pas de chaîne d'appels
/// à la base : c'est l'ordre <c>Awake</c>/<c>OnEnable</c>/<c>Start</c> qui décide, et il n'est pas
/// garanti entre objets. La parade retenue est de n'avoir <b>aucune</b> initialisation implicite —
/// les sous-classes règlent leurs champs dans l'inspecteur ou dans <see cref="Configure"/>.</para>
/// </summary>
public abstract class WeaponBase : MonoBehaviour
{
    [Header("Réglages de base")]
    public float BaseDamage = 10f;

    [Tooltip("Secondes entre deux tirs, avant réduction de recharge.")]
    public float BaseCooldown = 1f;

    public float Range = 400f;

    /// <summary>Niveau courant (1 à 5 dans les données, extrapolé au-delà).</summary>
    public int Level { get; private set; } = 1;

    private float _cooldownLeft;

    /// <summary>
    /// Recharge effective, bornée par <see cref="StatCaps"/>. Le plancher est ce qui a empêché,
    /// côté Godot, qu'un passif porte toutes les armes à la cadence maximale.
    /// </summary>
    protected float EffectiveCooldown
    {
        get
        {
            var stats = Player.Instance?.Stats;
            float reduction = stats != null
                ? Mathf.Min(stats.CooldownReduction, StatCaps.MaxCooldownReduction)
                : 0f;
            return Mathf.Max(StatCaps.MinCooldown, BaseCooldown * (1f - reduction));
        }
    }

    /// <summary>Dégâts effectifs, multiplicateur global du joueur appliqué.</summary>
    protected float EffectiveDamage
    {
        get
        {
            float mult = Player.Instance?.Stats.DamageMultiplier ?? 1f;
            return BaseDamage * mult;
        }
    }

    /// <summary>Règle l'arme à un niveau donné.</summary>
    public virtual void Configure(int level) => Level = Mathf.Max(1, level);

    protected virtual void Update()
    {
        if (Player.Instance == null || Player.Instance.IsDead) return;

        _cooldownLeft -= Time.deltaTime;
        if (_cooldownLeft > 0f) return;

        if (TryFire()) _cooldownLeft = EffectiveCooldown;
    }

    /// <summary>Tente de tirer. Renvoie faux si rien n'était à portée (la recharge ne repart pas).</summary>
    protected abstract bool TryFire();

    /// <summary>
    /// Ennemi vivant le plus proche, dans la portée. Renvoie <c>null</c> s'il n'y en a aucun —
    /// c'est ce qui empêche l'arme de consommer sa recharge dans le vide.
    /// </summary>
    protected EnemyBase? FindNearestEnemy()
    {
        EnemyBase? best = null;
        float bestSqr = Range * Range;
        Vector2 me = transform.position;

        foreach (var e in EnemyBase.Active)
        {
            if (e == null || e.IsDead) continue;
            float sqr = ((Vector2)e.transform.position - me).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = e; }
        }
        return best;
    }
}

/// <summary>
/// Canon à Impulsion — arme de départ de la Chimère. Tire un projectile sur l'ennemi le plus
/// proche. Portée ici comme arme témoin du Lot 2 : elle valide toute la chaîne
/// « viser → tirer → toucher → tuer → créditer l'XP ».
/// </summary>
public sealed class ImpulseCannon : WeaponBase
{
    [Header("Projectile")]
    public GameObject? BulletPrefab;
    public float BulletSpeed = 600f;

    protected override bool TryFire()
    {
        var target = FindNearestEnemy();
        if (target == null || BulletPrefab == null) return false;

        Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;

        var go = Instantiate(BulletPrefab, transform.position, Quaternion.identity);
        go.SetActive(true);   // sémantique Godot : un nœud instancié est toujours actif

        var bullet = go.GetComponent<Bullet>();
        if (bullet == null) { Destroy(go); return false; }

        bullet.Launch(dir * BulletSpeed, EffectiveDamage, Range);
        return true;
    }
}

/// <summary>
/// Projectile — port de <c>Bullet</c>. Touche par <b>distance</b>, comme tout le reste du jeu :
/// aucun collider, donc aucun coût de physique pour les centaines de projectiles simultanés.
/// </summary>
public sealed class Bullet : MonoBehaviour
{
    [Tooltip("Rayon de collision avec un ennemi.")]
    public float HitRadius = 12f;

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

            e.TakeDamage(_damage);
            Destroy(gameObject);
            return;
        }
    }
}
