using Godot;

/// <summary>
/// Node de Game.tscn — fait apparaître des power-ups temporaires à des moments répartis de la run
/// (max 4), de type aléatoire, à une position aléatoire de l'arène. Modèle <see cref="MagnetSpawner"/>.
/// Nœud de scène (non-AutoLoad) : reprogrammé à chaque run. Respecte RunEnded.
/// </summary>
public partial class PowerUpSpawner : Node
{
    private static PackedScene? _powerUpScene;

    // Fenêtres d'apparition (secondes) — réparties tôt → tard.
    private static readonly (int Min, int Max)[] SpawnWindows =
    {
        (90, 180),
        (240, 360),
        (420, 540),
        (600, 700),
    };

    private const float MinMargin = 150f;

    private readonly RandomNumberGenerator _rng = new();
    private float[] _spawnTimes = System.Array.Empty<float>();
    private int     _nextIndex  = 0;

    public override void _Ready()
    {
        _powerUpScene ??= GD.Load<PackedScene>("res://scenes/entities/PowerUp.tscn");
        _rng.Randomize();

        var times = new float[SpawnWindows.Length];
        for (int i = 0; i < SpawnWindows.Length; i++)
            times[i] = (float)GD.RandRange(SpawnWindows[i].Min, SpawnWindows[i].Max);
        System.Array.Sort(times);
        _spawnTimes = times;
    }

    public override void _Process(double delta)
    {
        var tracker = RunStatsTracker.Instance;
        if (tracker == null || tracker.RunEnded) return;
        if (_nextIndex >= _spawnTimes.Length) return;

        if (tracker.ElapsedSeconds >= _spawnTimes[_nextIndex])
        {
            _nextIndex++;
            SpawnPowerUp();
        }
    }

    private void SpawnPowerUp()
    {
        if (_powerUpScene == null) return;

        float halfW = Constants.ArenaWidth  / 2f - MinMargin;
        float halfH = Constants.ArenaHeight / 2f - MinMargin;
        float x = (float)GD.RandRange(-halfW, halfW);
        float y = (float)GD.RandRange(-halfH, halfH);

        var pickup = _powerUpScene.Instantiate<PowerUpPickup>();
        pickup.Type = PowerUps.Random(_rng);
        GetParent().CallDeferred(Node.MethodName.AddChild, pickup);
        pickup.SetDeferred("global_position", new Vector2(x, y));
    }
}
