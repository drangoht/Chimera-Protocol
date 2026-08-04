using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Essaim de Drones — archétype <b>orbital</b> (Lot 3).
///
/// <para>Des drones tournent autour du joueur et blessent au contact, chacun avec sa propre
/// cadence. L'arme n'a donc <b>pas de recharge globale</b> : sous Godot, elle la neutralisait par
/// une valeur absurde (999 s) ; ici on surcharge <see cref="TryFire"/> pour ne jamais tirer, ce qui
/// dit la même chose sans valeur magique.</para>
///
/// <para>Les drones sont positionnés en <c>Update</c> plutôt que parentés au joueur : c'est ce qui
/// leur permet d'orbiter sans hériter de son miroir horizontal ni de ses effets de teinte.</para>
/// </summary>
public class DroneSwarm : WeaponBase
{
    [Header("Orbite")]
    public int   DroneCount     = 2;
    public float OrbitSpeedDeg  = 120f;
    public float OrbitRadius    = 70f;

    [Tooltip("Secondes entre deux dégâts d'un même drone.")]
    public float DamageInterval = 0.5f;

    [Tooltip("Rayon de contact d'un drone.")]
    public float DroneRadius = 20f;

    private readonly List<Transform> _drones = new();
    private readonly List<float> _cooldowns = new();
    private float _orbitAngle;

    protected override void Awake()
    {
        BaseDamage = 12f;
        BaseCooldown = 1f;   // inutilisée : voir TryFire

        // base.Awake() EN DERNIER : c'est lui qui fige la valeur de fiche, et il doit donc
        // voir celles posées ci-dessus. Même exigence d'ordre que le `base._Ready()` de Godot,
        // pour une raison différente — ici c'est la capture, là-bas l'initialisation.
        base.Awake();
    }

    /// <summary>Cette arme n'attaque jamais « d'un bloc » : chaque drone gère ses propres dégâts.</summary>
    protected override bool TryFire() => false;

    protected override void Update()
    {
        base.Update();

        var player = Player.Instance;
        if (player == null || player.IsDead) return;

        if (_drones.Count != DroneCount) RebuildDrones();

        float dt = Time.deltaTime;
        _orbitAngle += OrbitSpeedDeg * Mathf.Deg2Rad * dt;

        Vector2 center = player.transform.position;
        float step = _drones.Count > 0 ? Mathf.PI * 2f / _drones.Count : 0f;

        for (int i = 0; i < _drones.Count; i++)
        {
            float angle = _orbitAngle + i * step;
            Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * OrbitRadius;
            _drones[i].position = pos;

            _cooldowns[i] -= dt;
            if (_cooldowns[i] > 0f) continue;

            if (TryDamageAt(pos)) _cooldowns[i] = DamageInterval;
        }
    }

    private bool TryDamageAt(Vector2 pos)
    {
        float sqr = DroneRadius * DroneRadius;
        bool hit = false;

        // Copie de sécurité : un drone frappe tous les ennemis de son rayon, donc la boucle continue
        // après une mise à mort — et une mort retire de EnemyBase.Active pendant l'énumération.
        foreach (var e in EnemyBase.Active.ToArray())
        {
            if (e == null || e.IsDead) continue;
            if (((Vector2)e.transform.position - pos).sqrMagnitude > sqr) continue;

            e.TakeDamage(EffectiveDamage);
            hit = true;
        }
        return hit;
    }

    private void RebuildDrones()
    {
        foreach (var d in _drones) if (d != null) Destroy(d.gameObject);
        _drones.Clear();
        _cooldowns.Clear();

        for (int i = 0; i < DroneCount; i++)
        {
            var go = new GameObject($"Drone{i}");
            go.transform.SetParent(transform.parent, worldPositionStays: true);
            _drones.Add(go.transform);
            _cooldowns.Add(0f);
        }
    }

    private void OnDestroy()
    {
        foreach (var d in _drones) if (d != null) Destroy(d.gameObject);
        _drones.Clear();
    }
}
