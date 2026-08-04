using UnityEngine;

/// <summary>
/// Sentinelle Cryo — champion du <b>Givre</b>. Garde ses distances et gèle : la réponse est de
/// <b>fermer l'écart</b>, à l'inverse du Colosse.
/// </summary>
public sealed class CryoSentinel : MiniBoss
{
    public float FreezeRange = 300f;
    public float FreezeSlow = 0.5f;
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

        if (Vector2.Distance(transform.position, player.transform.position) < FreezeRange)
            player.SpeedMultiplier = FreezeSlow;
    }
}
