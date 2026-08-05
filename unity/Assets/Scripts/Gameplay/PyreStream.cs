using UnityEngine;

/// <summary>
/// Flux de Braise — archétype <b>cône continu</b> avec brûlure (Lot 3).
///
/// <para>Cadence très rapide et dégâts directs faibles : l'essentiel du dommage vient de la
/// <b>brûlure</b> appliquée, qui continue après le tir. C'est une arme d'entretien, pas de pic.</para>
///
/// <para>⚠ La brûlure inflige des dégâts <b>continus</b> (PV/s × delta). Elle ne doit donc jamais
/// emprunter le chemin des coups discrets ni subir un plancher exprimé en pourcentage des PV max —
/// appliqué à chaque frame, un tel plancher tuerait en quelques images.</para>
/// </summary>
public class PyreStream : WeaponBase
{
    [Header("Cône")]
    [Tooltip("Ouverture totale du cône, en degrés.")]
    public float ConeAngle = 50f;

    [Header("Brûlure")]
    public float BurnDps = 6f;
    public float BurnDuration = 2.0f;

    /// <summary>Ennemis touchés par le dernier souffle — observable pour les tests et le HUD.</summary>
    public int LastConeHits { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 3f;
        BaseCooldown = 0.5f;
        Range = 130f;

        base.Awake();
    }

    protected override bool TryFire()
    {
        var target = FindNearestEnemy();
        if (target == null) return false;

        Vector2 origin = transform.position;
        Vector2 dir = ((Vector2)target.transform.position - origin).normalized;

        float half = ConeAngle * 0.5f;
        float sqr = Range * Range;
        float damage = EffectiveDamage;

        LastConeHits = 0;

        // Un JET de flammes, pas un contour : les particules chaudes (jaune → rouge) sont ce qui
        // distingue l'arme d'une aura conique. Le contour reste dessiné par-dessus, plus discret,
        // parce que lui seul dit exactement ce que l'arme couvre.
        Vfx.Flame(origin, dir, ConeAngle, Range);
        Vfx.Cone(origin, dir, half, Range, new Color(1f, 0.55f, 0.2f, 0.5f), 0.16f, 2f);

        var snapshot = EnemyBase.Active.ToArray();

        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;

            Vector2 offset = (Vector2)e.transform.position - origin;
            if (offset.sqrMagnitude > sqr) continue;
            if (Vector2.Angle(dir, offset.normalized) > half) continue;

            e.TakeDamage(damage);
            e.ApplyBurn(BurnDps, BurnDuration);
            LastConeHits++;
        }

        return LastConeHits > 0;
    }
}
