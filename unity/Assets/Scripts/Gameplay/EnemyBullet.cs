using UnityEngine;

/// <summary>
/// Projectile <b>ennemi</b> — rideau radial du boss, éventails de signature, tir de la Sentinelle.
///
/// <para>⚠ <b>Rien de tout cela n'existait dans le portage</b> (trouvé le 2026-08-09 par
/// <c>tools/audit_unused_members.py</c>). Aucun ennemi ne tirait : les archétypes <c>ranged_kiter</c>
/// et <c>cone_kiter</c> gardaient leur distance sans jamais faire feu, et le boss avait perdu deux de
/// ses trois cadences d'attaque. Le défaut ne se signalait nulle part — un ennemi qui kite sans tirer
/// a l'air d'un ennemi prudent, et un boss qui ne tire pas a l'air d'un boss de corps-à-corps. Deux
/// indices dormaient pourtant dans le dépôt : <c>sfx_enemy_sentinel_projectile.wav</c>, jamais joué,
/// et <c>SaturationTable.ChampionDamage</c> qui documente un plancher « posé au tir ».</para>
///
/// <para><b>Construit à l'exécution, sans prefab</b> : sa forme est un dégradé analytique
/// (<see cref="VfxPrimitives.Glow"/>), comme tous les effets du portage. Un asset de plus n'aurait
/// rien apporté qu'une chaîne d'import à tenir synchronisée.</para>
///
/// <para><b>Touche par distance, sans collider</b>, comme le projectile du joueur et comme les
/// dégâts de contact : le jeu vise 200-300 entités simultanées et n'a aucune physique dynamique.</para>
/// </summary>
public sealed class EnemyBullet : MonoBehaviour
{
    /// <summary>Vitesse de la Sentinelle Corrompue — la référence du jeu d'origine.</summary>
    public const float SentinelSpeed = 180f;

    /// <summary>Vitesse du rideau radial du boss.</summary>
    public const float BossSpeed = 210f;

    /// <summary>Secondes de vol avant disparition.</summary>
    private const float Lifetime = 3f;

    /// <summary>Rayon d'impact — le corps du joueur (13 px) plus la taille du projectile.</summary>
    private const float HitRadius = 16f;

    private Vector2 _velocity;
    private float _damage;
    private float _left = Lifetime;
    private bool _fromChampion;
    private Color _tint;
    private SpriteRenderer? _sprite;

    /// <summary>
    /// Tire un projectile. <paramref name="fromChampion"/> est posé <b>ici</b> et pas lu à l'impact :
    /// le projectile survit à son tireur, et un boss mort ne peut plus dire qu'il en était un.
    /// </summary>
    public static EnemyBullet Fire(Vector2 origin, Vector2 direction, float speed, float damage,
                                   bool fromChampion, Color tint)
    {
        var go = new GameObject("EnemyBullet", typeof(SpriteRenderer), typeof(EnemyBullet));
        go.transform.position = origin;

        var bullet = go.GetComponent<EnemyBullet>();
        bullet._velocity = direction.normalized * speed;
        bullet._damage = damage;
        bullet._fromChampion = fromChampion;
        bullet._tint = tint;
        bullet.Build();

        return bullet;
    }

    private void Build()
    {
        _sprite = GetComponent<SpriteRenderer>();
        _sprite.sprite = VfxPrimitives.Glow;
        _sprite.material = VfxPrimitives.Additive;
        _sprite.color = _tint;
        // Au-dessus de la nuée : un projectile masqué par ce qu'il traverse ne s'esquive pas.
        _sprite.sortingOrder = VfxPrimitives.OrderOver;
        transform.localScale = Vector3.one * 0.30f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        _left -= dt;
        if (_left <= 0f) { Destroy(gameObject); return; }

        Vector2 pos = (Vector2)transform.position + _velocity * dt;
        transform.position = pos;

        // Hors arène : inutile de le suivre plus loin, il ne reviendra pas.
        if (Mathf.Abs(pos.x) > Arena.HalfWidth + 64f || Mathf.Abs(pos.y) > Arena.HalfHeight + 64f)
        {
            Destroy(gameObject);
            return;
        }

        var player = Player.Instance;
        if (player == null || player.IsDead) return;
        if (((Vector2)player.transform.position - pos).sqrMagnitude > HitRadius * HitRadius) return;

        // Impact PONCTUEL, donc coup discret : éligible au plancher du cran VI quand le tireur était
        // un champion. La réduction de dégâts, elle, s'applique dans TakeDamage.
        float raw = _fromChampion
            ? SaturationTable.ChampionDamage(_damage, player.Stats.MaxHp, RunConfig.Saturation)
            : _damage;

        player.TakeDamage(raw);

        Vfx.Burst(pos, _tint, new Color(_tint.r, _tint.g, _tint.b, 0f), 8, 30f, 110f, 6f, 0.25f);
        Destroy(gameObject);
    }
}
