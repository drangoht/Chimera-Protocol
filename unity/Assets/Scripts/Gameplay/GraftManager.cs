using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applique les effets des greffes portées (Lot 6).
///
/// <para><b>Une greffe sans effet visible est pire qu'une greffe absente</b> : le joueur a payé une
/// jauge entière pour elle. C'est la leçon des armes invisibles, et elle vaut doublement ici, où
/// l'Assimilation est censée transformer le personnage.</para>
///
/// <para>Les effets sont <b>déclarés dans les données</b> (groupes nommés + paramètres numériques) et
/// implémentés ici. Un groupe non implémenté est <b>journalisé bruyamment</b> — c'est le seul moyen
/// qu'une greffe inerte se remarque autrement qu'en jouant longtemps et en doutant de soi.</para>
/// </summary>
[RequireComponent(typeof(Player))]
public sealed class GraftManager : MonoBehaviour
{
    /// <summary>Groupes d'effets non encore portés — observable par le banc et le diagnostic.</summary>
    public static readonly List<string> UnsupportedGroups = new();

    /// <summary>Vitesse des projectiles de tourelle, alignée sur celle du canon du joueur.</summary>
    private const float TurretBulletSpeed = 520f;

    private Player? _player;
    private readonly List<Transform> _orbiters = new();
    private readonly List<float> _orbiterCooldowns = new();

    private float _orbitAngle;
    private float _orbitRadius = 48f;
    private float _orbitSpeedDeg = 165f;
    private float _orbitDamage;
    private float _orbitInterval = 0.45f;
    private float _orbitLifesteal;

    private float _thornsFraction;
    private float _shockwaveTimer;
    private float _shockwaveInterval;
    private float _shockwaveDamage;
    private float _shockwaveRadius;

    private float _turretTimer;
    private float _turretInterval;
    private float _turretDamage;
    private int   _turretCount;
    private float _turretRange;

    private void Awake()
    {
        _player = GetComponent<Player>();
        Assimilation.GraftEquipped += Apply;
    }

    private void OnDestroy() => Assimilation.GraftEquipped -= Apply;

    /// <summary>Applique une greffe fraîchement équipée.</summary>
    public void Apply(GraftTable.GraftDef def)
    {
        ApplyStatMods(def);

        foreach (var group in def.Effects)
        {
            switch (group.Key)
            {
                case "orbitingAllies": ApplyOrbiters(def); break;
                case "thorns":         ApplyThorns(def);   break;
                case "shockwave":      ApplyShockwave(def); break;
                case "autoTurret":
                case "turrets":        ApplyTurrets(def, group.Key); break;

                default:
                    // ⚠ Trois groupes (dash, novaDash, charge) dépendent d'une esquive que le joueur
                    // ne possède pas encore sous Unity : les porter demanderait d'abord de porter le
                    // dash lui-même. Ils sont donc INERTES, et le disent.
                    if (!UnsupportedGroups.Contains(group.Key)) UnsupportedGroups.Add(group.Key);
                    Debug.LogWarning($"[GraftManager] effet non porte : '{group.Key}' " +
                                     $"(greffe {def.Id}) — la greffe est equipee mais SANS cet effet.");
                    break;
            }
        }
    }

    /// <summary>
    /// Modificateurs de statistiques. Les plafonds de <see cref="StatCaps"/> s'appliquent, comme pour
    /// les passifs et le Hub : une greffe ne doit pas être une porte dérobée vers l'invulnérabilité.
    /// </summary>
    private void ApplyStatMods(GraftTable.GraftDef def)
    {
        if (_player == null) return;
        var stats = _player.Stats;

        float hp = (float)def.Stat("maxHpAdd");
        if (hp != 0f)
        {
            stats.MaxHp += hp;
            _player.HealFlat(hp);
        }

        float dr = (float)def.Stat("damageReductionAdd");
        if (dr != 0f) stats.DamageReduction = StatCaps.CapDamageReduction(stats.DamageReduction + dr);

        float speedMult = (float)def.Stat("speedMult", 1.0);
        if (!Mathf.Approximately(speedMult, 1f))
        {
            stats.Speed = StatCaps.CapSpeed(stats.Speed * speedMult);
            stats.BaseSpeed = stats.Speed;
        }
    }

    // ─── Alliés en orbite ─────────────────────────────────────────────────────

    private void ApplyOrbiters(GraftTable.GraftDef def)
    {
        int count = (int)def.Effect("orbitingAllies", "count", 4);
        _orbitRadius   = (float)def.Effect("orbitingAllies", "orbitRadiusPx", 48);
        _orbitSpeedDeg = (float)def.Effect("orbitingAllies", "angularSpeedDegPerSec", 165);
        _orbitDamage   = (float)def.Effect("orbitingAllies", "contactDamage", 8);
        _orbitInterval = (float)def.Effect("orbitingAllies", "rehitIntervalSec", 0.45);
        _orbitLifesteal = (float)def.Effect("orbitingAllies", "lifestealFraction", 0.0);

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Symbiote{i}", typeof(SpriteRenderer));

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = UiPrimitives.White;
            sr.color = new Color(def.Tint[0], def.Tint[1], def.Tint[2]);
            sr.sortingOrder = 17;
            go.transform.localScale = new Vector3(11f, 11f, 1f);

            _orbiters.Add(go.transform);
            _orbiterCooldowns.Add(0f);
        }
    }

    private void ApplyThorns(GraftTable.GraftDef def)
        => _thornsFraction += (float)def.Effect("thorns", "reflectFraction",
                              def.Effect("thorns", "damageFraction", 0.25));

    private void ApplyShockwave(GraftTable.GraftDef def)
    {
        _shockwaveInterval = (float)def.Effect("shockwave", "intervalSec", 6.0);
        _shockwaveDamage   = (float)def.Effect("shockwave", "damage", 30.0);
        _shockwaveRadius   = (float)def.Effect("shockwave", "radiusPx", 160.0);
        _shockwaveTimer    = _shockwaveInterval;
    }

    private void ApplyTurrets(GraftTable.GraftDef def, string group)
    {
        _turretCount    = Mathf.Max(_turretCount, (int)def.Effect(group, "count", 1));
        _turretInterval = (float)def.Effect(group, "fireIntervalSec", def.Effect(group, "cooldown", 1.2));
        _turretDamage   = (float)def.Effect(group, "damage", 12.0);
        _turretRange    = (float)def.Effect(group, "rangePx", 420.0);
        _turretTimer    = _turretInterval;
    }

    // ─── Boucle ───────────────────────────────────────────────────────────────

    private void Update()
    {
        float dt = Time.deltaTime;
        UpdateOrbiters(dt);
        UpdateShockwave(dt);
        UpdateTurrets(dt);
    }

    private void UpdateOrbiters(float dt)
    {
        if (_orbiters.Count == 0) return;

        _orbitAngle += _orbitSpeedDeg * Mathf.Deg2Rad * dt;
        Vector2 center = transform.position;

        for (int i = 0; i < _orbiters.Count; i++)
        {
            var orbiter = _orbiters[i];
            if (orbiter == null) continue;

            float angle = _orbitAngle + i * Mathf.PI * 2f / _orbiters.Count;
            Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _orbitRadius;
            orbiter.position = pos;

            _orbiterCooldowns[i] -= dt;
            if (_orbiterCooldowns[i] > 0f) continue;

            // Copie de sécurité : mordre peut tuer, et une mort retire de la liste vivante.
            foreach (var enemy in EnemyBase.Active.ToArray())
            {
                if (enemy == null || enemy.IsDead) continue;
                if (((Vector2)enemy.transform.position - pos).sqrMagnitude > 22f * 22f) continue;

                enemy.TakeDamage(_orbitDamage);
                if (_orbitLifesteal > 0f) _player?.HealFlat(_orbitDamage * _orbitLifesteal);

                _orbiterCooldowns[i] = _orbitInterval;
                break;
            }
        }
    }

    private void UpdateShockwave(float dt)
    {
        if (_shockwaveInterval <= 0f) return;

        _shockwaveTimer -= dt;
        if (_shockwaveTimer > 0f) return;
        _shockwaveTimer = _shockwaveInterval;

        Vector2 center = transform.position;
        WeaponVfx.Ring(center, _shockwaveRadius, new Color(1f, 0.5f, 0.25f), 12f, 0.3f);

        foreach (var enemy in EnemyBase.Active.ToArray())
        {
            if (enemy == null || enemy.IsDead) continue;
            if (((Vector2)enemy.transform.position - center).sqrMagnitude > _shockwaveRadius * _shockwaveRadius) continue;

            enemy.TakeDamage(_shockwaveDamage);
        }
    }

    private void UpdateTurrets(float dt)
    {
        if (_turretCount <= 0) return;

        _turretTimer -= dt;
        if (_turretTimer > 0f) return;
        _turretTimer = _turretInterval;

        var prefab = ProjectilePrefabs.Bullet;
        if (prefab == null) return;

        Vector2 origin = transform.position;

        for (int i = 0; i < _turretCount; i++)
        {
            var target = NearestEnemy(origin, _turretRange);
            if (target == null) return;

            var go = Instantiate(prefab, origin, Quaternion.identity);
            go.SetActive(true);

            var bullet = go.GetComponent<Bullet>();
            if (bullet == null) continue;

            // Launch attend une VITESSE, pas une direction : passer un vecteur unitaire donnerait un
            // projectile avançant d'un pixel par seconde — visible, mais inoffensif.
            Vector2 dir = ((Vector2)target.transform.position - origin).normalized;
            bullet.Launch(dir * TurretBulletSpeed, _turretDamage, _turretRange);
        }
    }

    private static EnemyBase? NearestEnemy(Vector2 from, float range)
    {
        EnemyBase? best = null;
        float bestSqr = range * range;

        foreach (var enemy in EnemyBase.Active)
        {
            if (enemy == null || enemy.IsDead) continue;
            float sqr = ((Vector2)enemy.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = enemy; }
        }
        return best;
    }

    /// <summary>
    /// Renvoie les dégâts à réfléchir sur l'agresseur — appelé par le joueur quand il encaisse.
    /// Sans greffe d'épines, vaut zéro.
    /// </summary>
    public float ThornsDamageFor(float incoming) => incoming * _thornsFraction;

    /// <summary>Le porteur a-t-il des épines ?</summary>
    public bool HasThorns => _thornsFraction > 0f;

    /// <summary>Retire tout ce que les greffes ont créé — fin de run.</summary>
    public void ClearAll()
    {
        foreach (var orbiter in _orbiters) if (orbiter != null) Destroy(orbiter.gameObject);
        _orbiters.Clear();
        _orbiterCooldowns.Clear();

        _thornsFraction = 0f;
        _shockwaveInterval = 0f;
        _turretCount = 0;
    }
}
