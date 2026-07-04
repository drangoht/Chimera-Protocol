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
    private float _eliteTimer = 14f; // overtime : prochain spawn de mini-boss d'élite
    private float _bossTimer  = 4f;  // overtime : 1er boss ~4 s après le début (= fin du temps imparti)

    // Mini-boss spawnés en boucle pendant l'overtime.
    private static readonly string[] OvertimeElites =
        { "grafted_colossus", "rust_stalker", "master_sentinel", "aether_revenant" };

    // Boss de fin de niveau : ne spawn PLUS en ambiant (réservé à la boucle d'overtime).
    private static readonly System.Collections.Generic.HashSet<string> BossIds = new() { "rusted_core" };

    private readonly RandomNumberGenerator _rng = new();

    // Upgrade meta "overtime_stabilizer" : amortit la pente de scaling en overtime uniquement
    // (0 à -15%, 3 niveaux de -5%). Lu une fois au _Ready — n'affecte pas la difficulté standard.
    private float _overtimeStabilizerFactor = 1f;

    // Données de spawn chargées depuis enemies.json
    private readonly List<EnemySpawnData> _enemyPool = new();

    // Scènes pré-chargées
    private readonly Dictionary<string, PackedScene> _scenes = new();

    // Scènes dédiées (id → .tscn) pour les ennemis qui ont une scène/script propre : les 4
    // archétypes de base + les mini-boss/boss (comportements ou VFX de mort spécifiques).
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

    // Résolution de secours par archétype d'IA (« ai.type » du JSON) pour tout id SANS entrée dans
    // ScenePaths — c'est le mécanisme qui évite de créer une scène .tscn + une sous-classe C# par
    // nouvel ennemi « basique » (faune par biome, cf. docs/GDD.md §21) : plusieurs ids partagent la
    // même PackedScene/même comportement, seul leur SpriteFrames (EnemySpawnData.FramesPath) et
    // leurs stats (enemies.json) diffèrent.
    private static readonly Dictionary<string, string> ArchetypeScenePaths = new()
    {
        { "straight_chase", "res://scenes/entities/RustSwarm.tscn"        },
        { "erratic_chase",  "res://scenes/entities/CorruptedDrone.tscn"   },
        { "ranged_kiter",   "res://scenes/entities/CorruptedSentinel.tscn"},
        { "slow_hunter",    "res://scenes/entities/GraftedColossus.tscn"  },
    };

    public override void _Ready()
    {
        AddToGroup(Constants.GroupEnemySpawner);
        LoadEnemiesJson();
        PreloadScenes();
        _timer = 2f; // Premier spawn dans 2 s

        int stabilizerLevel = MetaProgressionSystem.Instance?.GetUpgradeLevel("overtime_stabilizer") ?? 0;
        _overtimeStabilizerFactor = 1f - 0.05f * stabilizerLevel;
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
        float tMinutes = _elapsed / 60f;

        // ── Overtime : le temps imparti est écoulé → escalade brutale ──────────────
        // « tMinutes effectif » fortement boosté (chaque minute d'overtime ≈ +4 min de scaling :
        // cap, cadence, PV et dégâts grimpent très vite) + vagues plus rapprochées + mini-boss
        // d'élite en boucle (bypass du cap simultané). Le cap dur 300 reste (perf).
        var tracker = RunStatsTracker.Instance;
        bool overtime = tracker?.Overtime ?? false;
        float otMin   = overtime ? tracker!.OvertimeSeconds / 60f : 0f;
        // overtime_stabilizer : amortit UNIQUEMENT la composante de temps overtime (pas tMinutes).
        float otMinEffectif = otMin * _overtimeStabilizerFactor;
        float tEff    = tMinutes + otMinEffectif * 4f;
        float waveReset = overtime ? Mathf.Max(8f, 18f - otMinEffectif * 2f) : 25f;

        float spawnInterval = SpawnCurve.SpawnInterval(tEff);
        _timer -= (float)delta;
        if (_timer <= 0f)
        {
            TrySpawnBatch(tEff, SpawnCurve.BatchCount(tEff));
            _timer = spawnInterval;
        }

        // Vagues : surcharge périodique d'un gros essaim (le « horde » de VS).
        _waveTimer -= (float)delta;
        if (_waveTimer <= 0f)
        {
            float spawnMult = GameSettings.Instance?.SpawnMult ?? 1f;
            TrySpawnBatch(tEff, SpawnCurve.WaveSize(tEff, spawnMult));
            _waveTimer = waveReset;
        }

        // Overtime : mini-boss d'élite + boss de fin de niveau en boucle (intervalles qui se resserrent).
        if (overtime)
        {
            _eliteTimer -= (float)delta;
            if (_eliteTimer <= 0f)
            {
                SpawnOvertimeElite(tEff);
                _eliteTimer = Mathf.Max(5f, 14f - otMinEffectif * 1.5f);
            }

            _bossTimer -= (float)delta;
            if (_bossTimer <= 0f)
            {
                SpawnOvertimeBoss(tEff);
                _bossTimer = Mathf.Max(28f, 50f - otMinEffectif * 2f);
            }
        }
    }

    /// <summary>Force le spawn d'un mini-boss d'élite (overtime) en ignorant son cap simultané,
    /// parmi ceux autorisés dans le biome courant.</summary>
    private void SpawnOvertimeElite(float tEff)
    {
        string? biome = GameManager.Instance?.CurrentBiomeId;
        var eligible = new List<EnemySpawnData>();
        foreach (var data in _enemyPool)
            if (System.Array.IndexOf(OvertimeElites, data.Id) >= 0 && data.IsAllowedInBiome(biome))
                eligible.Add(data);
        if (eligible.Count == 0) return;
        var chosen = eligible[(int)(_rng.Randi() % (uint)eligible.Count)];
        SpawnEnemy(chosen, tEff, ignoreMaxSimultaneous: true);
    }

    /// <summary>Force le spawn du boss de fin de niveau (overtime). Le 1er ≈ à l'instant où le timer
    /// atteint 0 (= « le boss arrive à la fin du temps imparti »), puis en boucle.</summary>
    private void SpawnOvertimeBoss(float tEff)
    {
        foreach (var data in _enemyPool)
            if (data.Id == "rusted_core")
            {
                SpawnEnemy(data, tEff, ignoreMaxSimultaneous: true);
                return;
            }
    }

    // -------------------------------------------------------------------------
    // Spawn
    // -------------------------------------------------------------------------

    private int CurrentMaxEnemies(float tMinutes)
        => SpawnCurve.MaxEnemies(tMinutes, GameSettings.Instance?.SpawnMult ?? 1f);

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
        // Filtre par temps d'apparition ET par biome courant (les ennemis sans champ `biomes`
        // restent disponibles partout — rétro-compatible). Le boss de fin de niveau est exclu
        // de l'ambiant : il n'apparaît qu'en overtime (boucle de boss).
        string? biome = GameManager.Instance?.CurrentBiomeId;
        var list = new List<EnemySpawnData>();
        foreach (var e in _enemyPool)
            if (tMinutes >= e.SpawnStartMinute && e.IsAllowedInBiome(biome) && !BossIds.Contains(e.Id))
                list.Add(e);
        return list;
    }

    private EnemySpawnData WeightedRandom(List<EnemySpawnData> pool)
    {
        var weights = new float[pool.Count];
        float total = 0f;
        for (int i = 0; i < pool.Count; i++) { weights[i] = pool[i].SpawnWeight; total += weights[i]; }
        return pool[WeightedPicker.PickIndex(weights, _rng.RandfRange(0f, total))];
    }

    private void SpawnEnemy(EnemySpawnData data, float tMinutes, bool ignoreMaxSimultaneous = false)
    {
        if (!_scenes.TryGetValue(data.Id, out var scene)) return;

        // Respect du cap simultané (mini-boss) — sauf en overtime (escalade : mini-boss en boucle).
        if (!ignoreMaxSimultaneous && data.MaxSimultaneous > 0 &&
            GetTree().GetNodesInGroup(data.Id).Count >= data.MaxSimultaneous)
            return;

        var node = scene.Instantiate<EnemyBase>();
        GetParent().AddChild(node);
        if (!string.IsNullOrEmpty(data.FramesPath))
            node.SetSpriteFrames(data.FramesPath);
        node.GlobalPosition = RandomSpawnPosition();

        // Effet de biome : modifie la vitesse de base de tous les ennemis.
        node.Speed *= GameManager.Instance?.BiomeEnemySpeedMult ?? 1f;

        // Application du scaling temporel APRÈS _Ready (qui a déjà initialisé _currentHp).
        // On utilise ApplyScaling pour synchroniser _currentHp avec le MaxHp scalé.
        float hpMult  = GameSettings.Instance?.EnemyHpMult ?? 1f;
        float dmgMult = GameSettings.Instance?.EnemyDamageMult ?? 1f;
        // La courbe non-linéaire (early grace + accélération late) cible les ennemis BASIQUES —
        // c'est là que le joueur devient « OP » en survivant. Les mini-boss (maxSimultaneous > 0) et
        // le boss de fin sont des gates de survie calibrés séparément (TTK, cf. GDD §17/§18/§20) :
        // ils gardent le scaling LINÉAIRE historique pour ne pas fausser leur fenêtre de victoire.
        bool isChampion = data.MaxSimultaneous > 0 || BossIds.Contains(data.Id);
        float scaledHp = isChampion
            ? EnemyScaling.Scaled(data.MaxHp, tMinutes, data.HpScalingPerMinute, hpMult)
            : EnemyScaling.ScaledCurved(data.MaxHp, tMinutes, data.HpScalingPerMinute, hpMult);
        float scaledDamage = isChampion
            ? EnemyScaling.Scaled(data.Damage, tMinutes, data.DamageScalingPerMinute, dmgMult)
            : EnemyScaling.ScaledCurved(data.Damage, tMinutes, data.DamageScalingPerMinute, dmgMult);
        node.ApplyScaling(scaledHp, scaledDamage);

        // Affixe d'élite : une fraction des ennemis BASIQUES (jamais mini-boss/boss) est promue.
        // La fréquence monte avec le temps (EliteAffixTable, plafonnée). Appliqué APRÈS ApplyScaling
        // pour multiplier les stats déjà scalées, et avant la 1re frame (capture de la vitesse de base).
        bool eliteEligible = data.MaxSimultaneous == 0 && !BossIds.Contains(data.Id);
        if (eliteEligible &&
            (DebugHooks.ForceElites || EliteAffixTable.ShouldBeElite(tMinutes, _rng.Randf())))
            node.ApplyElite(EliteAffixTable.Pick(_rng.Randf()));
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

            // Biomes autorisés (optionnel) : absent → tous biomes.
            string[] biomes = System.Array.Empty<string>();
            if (e.TryGetProperty("biomes", out var bs) && bs.ValueKind == JsonValueKind.Array)
            {
                var tmp = new List<string>();
                foreach (var b in bs.EnumerateArray())
                    if (b.GetString() is { } s) tmp.Add(s);
                biomes = tmp.ToArray();
            }

            // Archétype d'IA (optionnel) : sert à résoudre la scène de secours (ArchetypeScenePaths)
            // pour les ids sans entrée dans ScenePaths — cf. docs/GDD.md §21.
            string aiType = "";
            if (e.TryGetProperty("ai", out var aiObj) && aiObj.TryGetProperty("type", out var atProp))
                aiType = atProp.GetString() ?? "";

            // SpriteFrames dédié (optionnel) : vide = garder celui posé dans la scène.
            string framesPath = "";
            if (e.TryGetProperty("framesPath", out var fpProp))
                framesPath = fpProp.GetString() ?? "";

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
                Biomes                = biomes,
                AiType                = aiType,
                FramesPath            = framesPath,
            });
        }

        GD.Print($"[EnemySpawner] {_enemyPool.Count} types d'ennemis chargés.");
    }

    /// <summary>
    /// Résout une PackedScene par id : priorité à ScenePaths (scène dédiée), sinon repli sur
    /// ArchetypeScenePaths via EnemySpawnData.AiType (ennemis « basiques » qui réutilisent une
    /// scène archétype avec un sprite dédié posé au runtime, cf. EnemyBase.SetSpriteFrames).
    /// Une même PackedScene chargée n'est chargée qu'une fois (GD.Load la met déjà en cache, mais
    /// on évite les lookups répétés).
    /// </summary>
    private void PreloadScenes()
    {
        var loaded = new Dictionary<string, PackedScene>();

        foreach (var data in _enemyPool)
        {
            if (!ScenePaths.TryGetValue(data.Id, out var path) &&
                !ArchetypeScenePaths.TryGetValue(data.AiType, out path))
            {
                GD.PrintErr($"[EnemySpawner] Aucune scène pour l'id « {data.Id} » "
                           + $"(ni ScenePaths, ni ArchetypeScenePaths pour ai.type=« {data.AiType} »).");
                continue;
            }

            if (!loaded.TryGetValue(path, out var scene))
            {
                scene = GD.Load<PackedScene>(path);
                if (scene == null)
                {
                    GD.PrintErr($"[EnemySpawner] Scène introuvable : {path}");
                    continue;
                }
                loaded[path] = scene;
            }

            _scenes[data.Id] = scene;
        }
    }
}

// Le DTO EnemySpawnData est dans src/Systems/EnemySpawnData.cs.
