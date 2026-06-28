using Godot;

/// <summary>
/// Node dans Game.tscn — fait apparaître l'item Aimant à des moments aléatoires de la run.
/// Au maximum 3 apparitions : deux réparties tôt/milieu, et une « proche de la fin » (autour
/// de l'arrivée du boss final, ~13 min). Position aléatoire dans l'arène, min 150 px des murs.
/// Le spawner est un nœud de scène (non-AutoLoad) : il est recréé — donc reprogrammé — à chaque run.
/// </summary>
public partial class MagnetSpawner : Node
{
    private static PackedScene? _magnetScene;

    // Fenêtres d'apparition (secondes). Le boss final arrive à ~780 s (13 min) : la 3ᵉ tombe juste avant.
    private static readonly (int Min, int Max)[] SpawnWindows =
    {
        (120, 300),  // 1re : 2–5 min
        (360, 600),  // 2e  : 6–10 min
        (700, 760),  // 3e  : ~11.7–12.7 min — proche de la fin
    };

    private const float MinMargin = 150f;

    private float[] _spawnTimes = System.Array.Empty<float>();
    private int     _nextIndex  = 0;

    public override void _Ready()
    {
        _magnetScene ??= GD.Load<PackedScene>("res://scenes/entities/Magnet.tscn");

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
            SpawnMagnet();
        }
    }

    private void SpawnMagnet()
    {
        if (_magnetScene == null) return;

        float halfW = Constants.ArenaWidth  / 2f - MinMargin;
        float halfH = Constants.ArenaHeight / 2f - MinMargin;

        float x = (float)GD.RandRange(-halfW, halfW);
        float y = (float)GD.RandRange(-halfH, halfH);

        var magnet = _magnetScene.Instantiate<MagnetPickup>();
        GetParent().CallDeferred(Node.MethodName.AddChild, magnet);
        magnet.SetDeferred("global_position", new Vector2(x, y));
    }
}
