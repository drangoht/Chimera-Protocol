using UnityEngine;

/// <summary>
/// Missile à tête chercheuse — projectile <b>guidé</b> qui corrige sa trajectoire vers sa cible.
///
/// <para>Le guidage est borné par <see cref="TurnRateDeg"/> : un missile qui tournerait
/// instantanément toucherait toujours, ce qui supprimerait toute notion de placement. La cible est
/// reverrouillée si elle meurt avant l'impact — sinon la moitié d'une salve se perdrait dès qu'une
/// autre arme tue en premier.</para>
/// </summary>
public sealed class SeekerMissile : MonoBehaviour
{
    public float Speed = 300f;
    public float HitRadius = 14f;

    [Tooltip("Vitesse de correction de trajectoire, en degrés par seconde.")]
    public float TurnRateDeg = 220f;

    public float Lifetime = 3.5f;

    private Vector2 _dir = Vector2.right;
    private float _damage;
    private float _timeLeft;
    private EnemyBase? _target;

    /// <summary>Arme le missile. À appeler juste après l'instanciation.</summary>
    public void Launch(Vector2 direction, float damage, EnemyBase? target)
    {
        _dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        _damage = damage;
        _target = target;
        _timeLeft = Lifetime;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        _timeLeft -= dt;
        if (_timeLeft <= 0f) { Destroy(gameObject); return; }

        // Reverrouillage : sans lui, une salve se perd dès qu'une autre arme tue la cible.
        if (_target == null || _target.IsDead) _target = FindNearest();

        if (_target != null)
        {
            Vector2 desired = ((Vector2)_target.transform.position - (Vector2)transform.position).normalized;
            float maxTurn = TurnRateDeg * Mathf.Deg2Rad * dt;

            float angle = Vector2.SignedAngle(_dir, desired) * Mathf.Deg2Rad;
            float clamped = Mathf.Clamp(angle, -maxTurn, maxTurn);

            float c = Mathf.Cos(clamped), s = Mathf.Sin(clamped);
            _dir = new Vector2(_dir.x * c - _dir.y * s, _dir.x * s + _dir.y * c).normalized;
        }

        transform.position += (Vector3)(_dir * Speed * dt);

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

    private EnemyBase? FindNearest()
    {
        EnemyBase? best = null;
        float bestSqr = float.MaxValue;
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
