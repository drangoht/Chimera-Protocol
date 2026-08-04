using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singularité — archétype <b>zone persistante</b> qui attire et blesse par paliers (Lot 3).
///
/// <para>Longue recharge (6 s), zone qui vit ensuite pour son propre compte : elle <b>aspire</b> les
/// ennemis vers son centre et les blesse à intervalle fixe. C'est la seule arme dont l'effet
/// survit largement à son déclenchement, et c'est ce qui en fait un outil de contrôle plutôt que de
/// dégâts.</para>
///
/// <para>Les dégâts sont infligés par <b>tics</b> espacés et non en continu : cela les garde sur le
/// chemin des coups discrets, et rend l'arme lisible (on voit chaque pulsation).</para>
/// </summary>
public sealed class Singularity : WeaponBase
{
    [Header("Zone")]
    public float Radius = 120f;
    public float PullSpeed = 90f;
    public float Duration = 2.2f;
    public float TickInterval = 0.4f;

    /// <summary>Zones actuellement actives — observable pour les tests et le HUD.</summary>
    public int ActiveWells => _wells.Count;

    private sealed class Well
    {
        public Vector2 Center;
        public float TimeLeft;
        public float TickLeft;
    }

    private readonly List<Well> _wells = new();

    protected override void Awake()
    {
        BaseDamage = 6f;
        BaseCooldown = 6.0f;
        Range = 320f;

        base.Awake();
    }

    protected override bool TryFire()
    {
        var target = FindNearestEnemy();
        if (target == null) return false;

        _wells.Add(new Well
        {
            Center = target.transform.position,
            TimeLeft = Duration,
            TickLeft = 0f,   // première pulsation immédiate : la zone doit se voir agir tout de suite
        });
        return true;
    }

    protected override void Update()
    {
        base.Update();

        float dt = Time.deltaTime;
        float damage = EffectiveDamage;
        float sqr = Radius * Radius;

        for (int i = _wells.Count - 1; i >= 0; i--)
        {
            var w = _wells[i];
            w.TimeLeft -= dt;
            w.TickLeft -= dt;

            bool tick = w.TickLeft <= 0f;
            if (tick)
            {
                w.TickLeft = TickInterval;

                // Le puits est une zone qui PERSISTE : redessiné à chaque battement, il reste visible
                // toute sa durée sans qu'on ait à gérer un objet d'effet séparé.
                WeaponVfx.Ring(w.Center, Radius, new Color(0.67f, 0.27f, 1f), 10f, TickInterval);
                WeaponVfx.Dot(w.Center, new Color(0.85f, 0.6f, 1f), 16f, TickInterval);
            }

            var snapshot = EnemyBase.Active.ToArray();
            foreach (var e in snapshot)
            {
                if (e == null || e.IsDead) continue;

                Vector2 offset = w.Center - (Vector2)e.transform.position;
                if (offset.sqrMagnitude > sqr) continue;

                float dist = offset.magnitude;
                if (dist > 1f)
                {
                    Vector2 dir = offset / dist;
                    e.transform.position = (Vector2)e.transform.position + dir * PullSpeed * dt;
                }

                if (tick) e.TakeDamage(damage);
            }

            if (w.TimeLeft <= 0f) _wells.RemoveAt(i);
        }
    }
}
