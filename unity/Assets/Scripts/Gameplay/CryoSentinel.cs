using UnityEngine;

/// <summary>
/// Sentinelle Cryo — champion du <b>Givre</b>. Garde ses distances et gèle : la réponse est de
/// <b>fermer l'écart</b>, à l'inverse du Colosse.
/// </summary>
public sealed class CryoSentinel : MiniBoss
{
    public float FreezeRange = 300f;

    /// <summary>Force du gel — valeurs du jeu d'origine (0,45 pendant 2,2 s).</summary>
    public float FreezeSlow = 0.45f;

    /// <summary>Secondes de gel infligées. ⚠ Sans durée, le ralentissement ne finit jamais.</summary>
    public float FreezeDuration = 2.2f;

    public float FreezeInterval = 2.5f;

    private float _timer;

    protected override void Awake()
    {
        base.Awake();
        Ai = EnemyTable.AiType.ConeKiter;
        ChampionContactRadius = 34f;
    }

    protected override void Update()
    {
        base.Update();
        if (IsDead) return;

        var player = Player.Instance;
        if (player == null || player.IsDead) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = FreezeInterval;

        Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;

        // Le gel se voit MÊME hors de portée : c'est ce qui apprend au joueur que la sentinelle
        // vient de tirer, et que le ralentissement qu'il subit vient d'elle et non d'un bug.
        Vfx.Cone(transform.position, toPlayer, 35f, FreezeRange, new Color(0.6f, 0.92f, 1f, 0.7f), 0.35f);
        Vfx.Burst(transform.position, new Color(0.8f, 0.97f, 1f, 0.9f), new Color(0.6f, 0.85f, 1f, 0f),
                  18, 60f, 200f, 7f, 0.35f,
                  Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg, 70f);

        // ⚠ ApplyChill, et surtout pas `SpeedMultiplier = ...` : le second n'a AUCUNE durée. Le
        // portage clouait ainsi le joueur à moitié vitesse pour le reste de la run, et tuer la
        // sentinelle n'y changeait rien — le ralentissement ne lui appartenait plus.
        if (toPlayer.magnitude < FreezeRange)
            player.ApplyChill(FreezeSlow, FreezeDuration);
    }
}
