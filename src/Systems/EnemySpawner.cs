using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Gère le spawn des ennemis avec scaling temporel, façon Vampire Survivors.
/// - maxEnemies   = min(300, 16 + t_minutes * 38)
/// - spawnInterval = max(0.3, 1.0 - t_minutes * 0.06)
/// - batchCount   = clamp(2 + t_minutes * 2, 1, 10)  (spawn par lots)
/// - vagues       = toutes les 25 s, un essaim de (16 + t_minutes*5) ennemis d'un coup
/// Filtre les ennemis selon spawnStartMinute, tire selon spawnWeight.
/// Applique le scaling HP/dommages avant de relâcher l'ennemi dans la scène.
/// </summary>
public partial class EnemySpawner : Node
{
    private float _timer     = 0f;
    private float _waveTimer = 30f;  // première vague à t=30 s
    private float _elapsed   = 0f;   // secondes depuis le début du run

    private const int MaxAlive = 300; // cap simultané (perf cible 200-300)

    private readonly RandomNumberGenerator _rng = new();

    // Données de spawn chargées depuis enemies.json
    private readonly List<EnemySpawnData> _enemyPool = new();

    // Scènes pré-chargées
    private readonly Dictionary<string, PackedScene> _scenes = new();

    private static readonly Dictionary<string, string> ScenePaths = new()
    {
        { "rust_swarm",          "res://scenes/entities/RustSwarm.tscn"                   },
        { "corrupted_drone",     "res://scenes/entities/CorruptedDrone.tscn"              },
        { "corrupted_sentinel",  "res://scenes/entities/CorruptedSentinel.tscn"           },
        { "grafted_colossus",    "res://scenes/entities/GraftedColossus.tscn"             },
        { "rust_stalker",        "res://scenes/entities/MiniBoss/RustStalker.tscn"        },
        { "master_sentinel",     "res://scenes/entities/MiniBoss/MasterSentinel.tscn"     },
        { "aether_revenant",     "res://scenes/entities/MiniBoss/AetherRevenant.tscn"     },
        { "rusted_core",         "res://scenes/entities/Boss/RustedCore.tscn"             },
    };

    public override void _Ready()
    {
        AddToGroup(Constants.GroupEnemySpawner);
        LoadEnemiesJson();
        PreloadScenes();
        _timer = 2f; // Premier spawn dans 2 s
    }

    // -------------------------------------------------------------------------
    // Debug (--debug-boss) — voir DebugHooks. Aucun effet sans le flag.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Quand false, le spawn ambiant (lots + vagues) est suspendu. Utilisé par le hook
    /// --debug-boss pour isoler le boss (pas d'ennemis parasites, pas d'XP/level-up qui
    /// faussent la mesure de TTK). N'a aucun effet hors debug.
    /// </summary>
    public bool AmbientEnabled { get; set; } = true;

    /// <summary>
    /// Force le spawn d'un ennemi par id, en lui appliquant le scaling temporel
    /// de <paramref name="tMinutes"/> (réutilise <see cref="SpawnEnemy"/> et
    /// donc <c>ApplyScaling</c>). Permet au hook --debug-boss de faire apparaître
    /// le boss final à son PV réel sans attendre 13 min ni éditer enemies.json.
    /// </summary>
    public void DebugSpawnById(string id, float tMinutes)
    {
        foreach (var data in _enemyPool)
        {
            if (data.Id != id) continue;
            SpawnEnemy(data, tMinutes);
            return;
        }
        GD.PrintErr($"[EnemySpawner] DebugSpawnById : id introuvable « {id} ».");
    }

    public override void _Process(double delta)
    {
        // Mode --debug-boss : aucun spawn ambiant, seul le boss est présent.
        if (!AmbientEnabled) return;

        _elapsed += (float)delta;
        float tMinutes      = _elapsed / 60f;
        float spawnInterval = Mathf.Max(0.3f, 1.0f - tMinutes * 0.06f);

        _timer -= (float)delta;
        if (_timer <= 0f)
        {
            int batch = Mathf.Clamp(2 + (int)(tMinutes * 2f), 1, 10);
            TrySpawnBatch(tMinutes, batch);
            _timer = spawnInterval;
        }

        // Vagues : surcharge périodique d'un gros essaim (le « horde » de VS).
        _waveTimer -= (float)delta;
        if (_waveTimer <= 0f)
        {
            float spawnMult = GameSettings.Instance?.SpawnMult ?? 1f;
            int waveSize = (int)((12 + tMinutes * 4f) * spawnMult);
            TrySpawnBatch(tMinutes, waveSize);
            _waveTimer = 25f;
        }
    }

    // -------------------------------------------------------------------------
    // Spawn
    // -------------------------------------------------------------------------

    private int CurrentMaxEnemies(float tMinutes)
    {
        // Base assouplie (Normal) + multiplicateur de difficulté.
        float mult = GameSettings.Instance?.SpawnMult ?? 1f;
        return Mathf.Min(MaxAlive, (int)((12 + tMinutes * 30f) * mult));
    }

    private void TrySpawnBatch(float tMinutes, int count)
    {
        int maxEnemies = CurrentMaxEnemies(tMinutes);
        int alive = GetTree().GetNodesInGroup(Constants.GroupEnemies).Count;
        int room  = maxEnemies - alive;
        if (room <= 0) return;
        count = Mathf.Min(count, room);

        var available = GetAvailableEnemies(tMinutes);
        if (available.Count == 0) return;

        for (int i = 0; i < count; i++)
        {
            var chosen = WeightedRandom(available);
            SpawnEnemy(chosen, tMinutes);
        }
    }

    private List<EnemySpawnData> GetAvailableEnemies(float tMinutes)
    {
        var list = new List<EnemySpawnData>();
        foreach (var e in _enemyPool)
            if (tMinutes >= e.SpawnStartMinute)
                list.Add(e);
        return list;
    }

    private EnemySpawnData WeightedRandom(List<EnemySpawnData> pool)
    {
        float totalWeight = 0f;
        foreach (var e in pool) totalWeight += e.SpawnWeight;

        float roll = _rng.RandfRange(0f, totalWeight);
        float acc  = 0f;
        foreach (var e in pool)
        {
            acc += e.SpawnWeight;
            if (roll <= acc) return e;
        }
        return pool[pool.Count - 1];
    }

    private void SpawnEnemy(EnemySpawnData data, float tMinutes)
    {
        if (!_scenes.TryGetValue(data.Id, out var scene)) return;

        // Respect du cap simultané (mini-boss)
        if (data.MaxSimultaneous > 0 &&
            GetTree().GetNodesInGroup(data.Id).Count >= data.MaxSimultaneous)
            return;

        var node = scene.Instantiate<EnemyBase>();
        GetParent().AddChild(node);
        node.GlobalPosition = RandomSpawnPosition();

        // Effet de biome : modifie la vitesse de base de tous les ennemis.
        node.Speed *= GameManager.Instance?.BiomeEnemySpeedMult ?? 1f;

        // Application du scaling temporel APRÈS _Ready (qui a déjà initialisé _currentHp).
        // On utilise ApplyScaling pour synchroniser _currentHp avec le MaxHp scalé.
        float hpMult  = GameSettings.Instance?.EnemyHpMult ?? 1f;
        float dmgMult = GameSettings.Instance?.EnemyDamageMult ?? 1f;
        float scaledHp     = EnemyScaling.Scaled(data.MaxHp,  tMinutes, data.HpScalingPerMinute,     hpMult);
        float scaledDamage = EnemyScaling.Scaled(data.Damage, tMinutes, data.DamageScalingPerMinute, dmgMult);
        node.ApplyScaling(scaledHp, scaledDamage);
    }

    private Vector2 RandomSpawnPosition()
    {
        int side = _rng.RandiRange(0, 3);
        float hw = Constants.ArenaWidth  / 2f - 48f;
        float hh = Constants.ArenaHeight / 2f - 48f;

        return side switch
        {
            0 => new Vector2(_rng.RandfRange(-hw, hw), -hh),
            1 => new Vector2(_rng.RandfRange(-hw, hw),  hh),
            2 => new Vector2(-hw, _rng.RandfRange(-hh, hh)),
            _ => new Vector2( hw, _rng.RandfRange(-hh, hh)),
        };
    }

    // -------------------------------------------------------------------------
    // Chargement JSON
    // -------------------------------------------------------------------------

    private void LoadEnemiesJson()
    {
        using var file = Godot.FileAccess.Open("res://data/enemies.json", Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr("[EnemySpawner] Impossible de lire data/enemies.json");
            return;
        }

        string json = file.GetAsText();
        var doc = JsonDocument.Parse(json);

        foreach (var e in doc.RootElement.GetProperty("enemies").EnumerateArray())
        {
            // Gestion de damagePerSecond vs damagePerProjectile
            float dmg = 0f;
            if (e.TryGetProperty("damagePerSecond",    out var dps)) dmg = dps.GetSingle();
            if (e.TryGetProperty("damagePerProjectile",out var dpp)) dmg = dpp.GetSingle();

            int maxSim = 0;
            if (e.TryGetProperty("maxSimultaneous", out var ms)) maxSim = ms.GetInt32();

            _enemyPool.Add(new EnemySpawnData
            {
                Id                    = e.GetProperty("id").GetString()!,
                MaxHp                 = e.GetProperty("maxHp").GetSingle(),
                Speed                 = e.GetProperty("speed").GetSingle(),
                Damage                = dmg,
                XpValue               = e.GetProperty("xpValue").GetInt32(),
                SpawnStartMinute      = e.GetProperty("spawnStartMinute").GetSingle(),
                SpawnWeight           = e.GetProperty("spawnWeight").GetSingle(),
                HpScalingPerMinute    = e.GetProperty("hpScalingPerMinute").GetSingle(),
                DamageScalingPerMinute= e.GetProperty("damageScalingPerMinute").GetSingle(),
                MaxSimultaneous       = maxSim,
            });
        }

        GD.Print($"[EnemySpawner] {_enemyPool.Count} types d'ennemis chargés.");
    }

    private void PreloadScenes()
    {
        foreach (var (id, path) in ScenePaths)
        {
            var scene = GD.Load<PackedScene>(path);
            if (scene != null)
                _scenes[id] = scene;
            else
                GD.PrintErr($"[EnemySpawner] Scène introuvable : {path}");
        }
    }
}

// Le DTO EnemySpawnData est dans src/Systems/EnemySpawnData.cs.
