using UnityEngine;

/// <summary>
/// Gardien Néon — champion du <b>Néon</b>. Son bouclier orbital n'absorbe que les dégâts venus du
/// secteur qu'il couvre : la réponse est de <b>tourner autour</b> pour frapper à découvert.
/// </summary>
public sealed class NeonWarden : MiniBoss
{
    [Tooltip("Part des dégâts absorbés quand le bouclier couvre l'angle d'arrivée.")]
    public float ShieldAbsorption = 0.8f;

    [Tooltip("Vitesse de rotation du bouclier, en degrés par seconde.")]
    public float ShieldSpeedDeg = 90f;

    [Tooltip("Demi-ouverture du secteur couvert, en degrés.")]
    public float ShieldHalfArc = 70f;

    /// <summary>Angle courant du bouclier, en degrés — lu par l'affichage.</summary>
    public float ShieldAngle { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        Ai = EnemyTable.AiType.ShieldedChaser;
        ChampionContactRadius = 36f;
    }

    protected override void Update()
    {
        base.Update();
        ShieldAngle = Mathf.Repeat(ShieldAngle + ShieldSpeedDeg * Time.deltaTime, 360f);
    }

    public override void TakeDamage(float amount)
    {
        var player = Player.Instance;

        if (player != null)
        {
            Vector2 from = (Vector2)player.transform.position - (Vector2)transform.position;
            float incoming = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            float delta = Mathf.Abs(Mathf.DeltaAngle(incoming, ShieldAngle));

            // Absorbe seulement dans le secteur couvert : c'est ce qui récompense le déplacement.
            if (delta < ShieldHalfArc) amount *= 1f - ShieldAbsorption;
        }

        base.TakeDamage(amount);
    }
}
