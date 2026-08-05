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

    /// <summary>
    /// Rayon du cœur : les ennemis s'y entassent sans le traverser. Repris de <c>GravityWell</c>.
    /// </summary>
    public float InnerRadius = 18f;

    /// <summary>Vitesse tangentielle de l'aspiration, en pixels/s — ce qui met les corps en spirale.</summary>
    public float SwirlStrength = 110f;

    /// <summary>Rotation des bras du vortex, en radians/s — la valeur du jeu publié.</summary>
    public const float SpinSpeed = 3.2f;

    /// <summary>Zones actuellement actives — observable pour les tests et le HUD.</summary>
    public int ActiveWells => _wells.Count;

    private sealed class Well
    {
        public Vector2 Center;
        public float TimeLeft;
        public float TickLeft;
        public float Spin;
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

                // Rien de visuel ici : le vortex se redessine à CHAQUE frame, plus bas. Un visuel
                // reposé au rythme des dégâts (0,4 s) ne tournerait pas — il sauterait.
            }

            var snapshot = EnemyBase.Active.ToArray();
            foreach (var e in snapshot)
            {
                if (e == null || e.IsDead) continue;

                Vector2 offset = w.Center - (Vector2)e.transform.position;
                if (offset.sqrMagnitude > sqr) continue;

                float dist = offset.magnitude;
                if (dist > InnerRadius)
                {
                    // ⚠ Aspiration en SPIRALE, et non en ligne droite. Une composante tangentielle
                    // s'ajoute à la composante radiale, d'autant plus forte qu'on approche du
                    // centre — c'est elle qui fait qu'un ennemi happé <b>tourne</b> au lieu de
                    // glisser droit. Sans elle, le puits se lit comme un aimant, pas comme un trou.
                    Vector2 radial = offset / dist;
                    var tangent = new Vector2(-radial.y, radial.x);

                    float closeness = 1f - Mathf.Clamp01(dist / Radius);
                    float swirl = SwirlStrength * (0.35f + closeness);

                    // Le pas radial ne dépasse jamais la distance restante : sinon un ennemi
                    // traverse le cœur et ressort de l'autre côté à chaque frame — ce qui se voit
                    // comme un tremblement, pas comme une aspiration.
                    float step = Mathf.Min(PullSpeed * dt, dist - InnerRadius);

                    e.transform.position = (Vector2)e.transform.position
                                         + radial * step
                                         + tangent * swirl * dt;
                }

                if (tick) e.TakeDamage(damage);
            }

            // Le vortex TOURNE : la phase avance avec le temps, et le visuel est reposé chaque
            // frame pour la durée d'une frame. C'est ce qui distingue un tourbillon d'un anneau.
            w.Spin += SpinSpeed * dt;
            Vfx.Vortex(w.Center, Radius, InnerRadius, w.Spin, new Color(0.72f, 0.42f, 1f), dt * 1.6f);

            if (w.TimeLeft <= 0f) _wells.RemoveAt(i);
        }
    }
}
