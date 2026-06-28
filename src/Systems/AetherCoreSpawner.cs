using Godot;

/// <summary>
/// Node dans Game.tscn — spawn périodique de Noyaux d'Aether toutes les 45 s.
/// Position aléatoire à l'intérieur de l'arène, min 150 px des murs.
/// </summary>
public partial class AetherCoreSpawner : Node
{
    private static PackedScene? _coreScene;

    // Intervalle de spawn lu depuis meta_upgrades.json (fallback 45 s)
    private float _spawnInterval = 45f;
    private float _timer         = 0f;

    // Marge minimum par rapport aux bords intérieurs de l'arène
    private const float MinMargin = 150f;

    public override void _Ready()
    {
        _coreScene ??= GD.Load<PackedScene>("res://scenes/entities/AetherCore.tscn");
        LoadSpawnInterval();
        // Premier spawn décalé de l'intervalle complet pour ne pas démarrer immédiatement
        _timer = 0f;
    }

    private void LoadSpawnInterval()
    {
        const string path = "res://data/meta_upgrades.json";
        if (!Godot.FileAccess.FileExists(path)) return;

        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file == null) return;

        using var doc = System.Text.Json.JsonDocument.Parse(file.GetAsText());
        if (doc.RootElement.TryGetProperty("aetherCores", out var cores))
            if (cores.TryGetProperty("periodicSpawnIntervalSeconds", out var prop))
                _spawnInterval = prop.GetSingle();
    }

    public override void _Process(double delta)
    {
        if (RunStatsTracker.Instance?.RunEnded == true) return;

        _timer += (float)delta;
        if (_timer >= _spawnInterval)
        {
            _timer -= _spawnInterval;
            SpawnCore();
        }
    }

    private void SpawnCore()
    {
        if (_coreScene == null) return;

        float halfW = Constants.ArenaWidth  / 2f - MinMargin;
        float halfH = Constants.ArenaHeight / 2f - MinMargin;

        float x = (float)GD.RandRange(-halfW, halfW);
        float y = (float)GD.RandRange(-halfH, halfH);

        var core = _coreScene.Instantiate<AetherCore>();
        GetParent().CallDeferred(Node.MethodName.AddChild, core);
        core.SetDeferred("global_position", new Vector2(x, y));
    }
}
