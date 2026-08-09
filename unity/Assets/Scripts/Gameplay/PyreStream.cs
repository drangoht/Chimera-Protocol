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

    /// <summary>
    /// Applique la FORME du palier, et pas seulement ses chiffres.
    /// </summary>
    /// <remarks>
    /// ⚠ Sans cette lecture, le cone gardait son ouverture et sa portee du niveau 1, et la brulure sa duree :
    /// l'arme montait en degats et gardait sa forme de depart. Le portage ne lisait que six
    /// des seize cles de palier — huit armes etaient concernees.
    /// </remarks>
    public override void ApplyLevelStats(WeaponTable.WeaponLevelStats stats)
    {
        Range        = stats.Shape("range", Range);
        ConeAngle    = stats.Shape("coneAngle", ConeAngle);
        BurnDuration = stats.Shape("burnDuration", BurnDuration);
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

        // Un JET de flammes, et RIEN d'autre : les particules chaudes (jaune → rouge) prolongées par
        // la fumée. Le contour du cône était dessiné par-dessus « parce que lui seul dit exactement
        // ce que l'arme couvre » — vrai, et c'était le mauvais arbitrage : deux segments droits
        // partant du joueur donnent au souffle l'allure d'un gabarit de visée. Un feu n'a pas
        // d'arête ; la couverture se lit à ce qui brûle.
        Vfx.Flame(origin, dir, ConeAngle, Range);

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
