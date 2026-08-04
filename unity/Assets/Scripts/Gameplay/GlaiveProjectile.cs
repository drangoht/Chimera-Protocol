using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Glaive lancé — projectile <b>boomerang</b> : il s'éloigne, revient, et peut toucher deux fois.
///
/// <para>Chaque ennemi n'est frappé qu'<b>une fois par phase</b> (aller, puis retour) : sans cet
/// ensemble, un glaive qui traverse lentement une nuée infligerait ses dégâts à chaque frame de
/// contact, ce qui en ferait de très loin l'arme la plus forte du jeu.</para>
/// </summary>
public sealed class GlaiveProjectile : MonoBehaviour
{
    public float Speed = 420f;
    public float HitRadius = 20f;

    private readonly HashSet<EnemyBase> _hitThisPhase = new();
    private Vector2 _dir = Vector2.right;
    private Vector2 _origin;
    private float _damage;
    private float _range;
    private bool _returning;

    /// <summary>Arme le glaive. À appeler juste après l'instanciation.</summary>
    public void Launch(Vector2 direction, float damage, float range)
    {
        _dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        _damage = damage;
        _range = range;
        _origin = transform.position;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector2 me = transform.position;

        if (!_returning)
        {
            transform.position = me + _dir * Speed * dt;

            if (Vector2.Distance(transform.position, _origin) >= _range)
            {
                // Demi-tour : nouvelle phase, donc chaque ennemi redevient touchable une fois.
                _returning = true;
                _hitThisPhase.Clear();
            }
        }
        else
        {
            var player = Player.Instance;
            Vector2 home = player != null ? player.transform.position : _origin;

            Vector2 toHome = home - me;
            if (toHome.magnitude < 24f) { Destroy(gameObject); return; }

            transform.position = me + toHome.normalized * Speed * dt;
        }

        DamageAround(transform.position);
    }

    private void DamageAround(Vector2 pos)
    {
        float sqr = HitRadius * HitRadius;

        // ⚠ Copie de sécurité, comme toutes les armes qui frappent en boucle : TakeDamage peut tuer,
        // une mort retire de EnemyBase.Active, et l'énumérateur lève alors « Collection was modified »
        // AU MILIEU de la passe — le reste de l'attaque est perdu, et l'arme paraît simplement rater
        // ses cibles. Observé au banc : le glaive est la seule arme qui poursuit sa course après avoir
        // touché, donc la seule à déclencher le défaut.
        foreach (var e in EnemyBase.Active.ToArray())
        {
            if (e == null || e.IsDead || _hitThisPhase.Contains(e)) continue;
            if (((Vector2)e.transform.position - pos).sqrMagnitude > sqr) continue;

            _hitThisPhase.Add(e);
            e.TakeDamage(_damage);
        }
    }
}
