using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Glaive lancé — projectile <b>boomerang</b> : il s'éloigne, revient, et peut toucher deux fois.
///
/// <para>Chaque ennemi n'est frappé qu'<b>une fois par phase</b> (aller, puis retour) : sans cet
/// ensemble, un glaive qui traverse lentement une nuée infligerait ses dégâts à chaque frame de
/// contact, ce qui en ferait de très loin l'arme la plus forte du jeu.</para>
///
/// <para><b>Le retour se mesure contre le joueur</b>, jamais dans l'absolu — <see cref="BoomerangReturn"/>.
/// La lame partait et revenait à 420 px/s fixes, quand un joueur équipé atteint 380 : elle ne gagnait
/// alors que 40 px/s sur lui et mettait <b>six secondes</b> à rentrer. Comme la recharge de l'arme
/// attend le retour, le Glaive paraissait tout simplement cesser de tirer à mesure que le joueur
/// achetait de la vitesse — la punition exacte de ce qu'il venait d'améliorer.</para>
///
/// <para><b>Il TOURNE, et c'est une croix.</b> Le portage lui donnait le sprite d'un tir de sentinelle
/// ennemie, teinté violet, parfaitement immobile — soit la forme et la couleur d'un projectile
/// hostile pour l'arme que l'icône, le Codex et l'écran de montée de niveau montrent tous comme une
/// croix cyan tournoyante. Le tournoiement n'est pas une décoration : c'est ce qui distingue une lame
/// <i>lancée</i> d'un tir, et il annonce qu'elle va revenir.</para>
/// </summary>
public sealed class GlaiveProjectile : MonoBehaviour
{
    public float Speed = 420f;
    public float HitRadius = 20f;

    /// <summary>Vitesse de rotation, en degrés par seconde.</summary>
    /// <remarks>
    /// ⚠ <b>Pas les 16 rad/s du jeu publié</b>, et ce n'est pas une transposition ratée. Godot fait
    /// tourner un <b>losange</b>, une forme de symétrie d'ordre 2 : son image se répète tous les 180°.
    /// Une croix est d'ordre 4 — elle se répète tous les <b>90°</b> — donc à vitesse angulaire égale
    /// elle paraît tourner deux fois plus vite, et à 916 °/s elle frôle le stroboscope (une période
    /// apparente de six images à 60 IPS). 515 °/s rend la même <i>vitesse perçue</i> : c'est l'effet
    /// qu'on porte d'un moteur à l'autre, jamais le nombre.
    /// </remarks>
    private const float SpinDegPerSec = 515f;

    private readonly HashSet<EnemyBase> _hitThisPhase = new();
    private Vector2 _dir = Vector2.right;
    private Vector2 _origin;
    private float _damage;
    private float _range;
    private bool _returning;

    private void Awake() => Dress();

    /// <summary>
    /// Pose la silhouette et le halo. Fait <b>par code</b> et non dans le gabarit : le sprite est
    /// dessiné à l'exécution, donc aucun prefab ne peut le référencer.
    /// </summary>
    /// <remarks>
    /// ⚠ Le gabarit garde donc une référence au sprite qu'il servait avant — inerte, mais trompeuse
    /// à la lecture. Ce qui fait foi est ici.
    /// </remarks>
    private void Dress()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        sr.sprite = GlaiveSprite.Get();
        sr.color = Color.white;   // la teinte vit dans le sprite : un multiplicateur l'assombrirait

        // Halo : le portage du PointLight2D cyan du jeu publié. Sans lui, la lame se perd dans une
        // nuée dès qu'elle passe devant un ennemi clair — et c'est l'arme dont il faut suivre la
        // trajectoire, puisqu'elle revient. Un disque radial est invariant par rotation : il peut
        // rester enfant d'un objet qui tourne.
        var halo = new GameObject("Halo", typeof(SpriteRenderer));
        halo.transform.SetParent(transform, false);

        var hr = halo.GetComponent<SpriteRenderer>();
        hr.sprite = VfxPrimitives.Glow;
        hr.sharedMaterial = VfxPrimitives.Additive;
        hr.color = new Color(GlaiveSprite.Blade.r, GlaiveSprite.Blade.g, GlaiveSprite.Blade.b, 0.5f);
        hr.sortingOrder = sr.sortingOrder - 1;

        // Le sprite de lueur mesure 64 unités : l'échelle est le rapport du diamètre voulu à 64.
        float diameter = HitRadius * 2.6f;
        halo.transform.localScale = Vector3.one * (diameter / 64f);
    }

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

        transform.Rotate(0f, 0f, SpinDegPerSec * dt);

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

            // ⚠ PAS `Speed` : la vitesse de retour se calcule CONTRE celle du porteur, sans quoi une
            // lame lancée par un joueur rapide ne le rattrape plus (cf. `BoomerangReturn`).
            float back = BoomerangReturn.SpeedAgainst(Speed, player != null ? player.CurrentSpeed : 0f);
            transform.position = me + toHome.normalized * back * dt;
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
