using Godot;
using System.Collections.Generic;

/// <summary>
/// Réglages du jeu (audio, affichage, accessibilité), persistés dans
/// <c>user://settings.cfg</c>. Autoload : charge et applique au démarrage.
/// L'écran Options lit/écrit via les setters (qui appliquent + sauvegardent).
/// </summary>
public partial class GameSettings : Node
{
    public static GameSettings Instance { get; private set; } = null!;

    private const string Path = "user://settings.cfg";

    public enum GameDifficulty { Facile, Normal, Difficile }

    public float          Master       { get; private set; } = 1.0f;
    public float          Music        { get; private set; } = 0.8f;
    public float          Sfx          { get; private set; } = 0.9f;
    public bool           Fullscreen   { get; private set; } = false;
    public bool           ShakeEnabled { get; private set; } = true;
    public GameDifficulty Difficulty   { get; private set; } = GameDifficulty.Normal;

    /// <summary>Code de langue de l'UI : "en" (défaut), "fr", "es". Persisté.</summary>
    public string Language { get; private set; } = "en";
    public static readonly string[] Languages = { "en", "fr", "es" };

    // Multiplicateurs de difficulté lus par EnemySpawner (ennemis) — délégués à DifficultyTuning.
    public float EnemyDamageMult => DifficultyTuning.EnemyDamage((int)Difficulty);
    public float EnemyHpMult     => DifficultyTuning.EnemyHp((int)Difficulty);
    public float SpawnMult       => DifficultyTuning.Spawn((int)Difficulty);

    // Biomes vaincus (boss final battu), clés "biomeId:difficulté". Sert au badge de l'écran de sélection.
    private readonly HashSet<string> _completions = new();

    public override void _Ready()
    {
        Instance = this;
        Load();
        Apply();
    }

    // ── Complétion des biomes (badge sélection de niveau) ──────────────────────
    private static string CompletionKey(string biomeId, GameDifficulty d) => $"{biomeId}:{(int)d}";

    /// <summary>Marque un biome comme vaincu à la difficulté donnée (boss final battu) et persiste.</summary>
    public void RecordCompletion(string biomeId, GameDifficulty d)
    {
        if (biomeId.Length == 0) return;
        if (_completions.Add(CompletionKey(biomeId, d))) Save();
    }

    /// <summary>Le biome a-t-il été vaincu à cette difficulté précise ?</summary>
    public bool HasCompleted(string biomeId, GameDifficulty d) => _completions.Contains(CompletionKey(biomeId, d));

    /// <summary>Le biome a-t-il été vaincu à n'importe quelle difficulté ?</summary>
    public bool HasCompletedAny(string biomeId)
    {
        foreach (GameDifficulty d in System.Enum.GetValues<GameDifficulty>())
            if (HasCompleted(biomeId, d)) return true;
        return false;
    }

    // ── Déblocage progressif des niveaux ──────────────────────────────────────
    /// <summary>Ordre de déblocage des niveaux (biomes). Le 1er est jouable d'office ;
    /// chacun se débloque quand le précédent est complété (boss de fin de niveau battu).</summary>
    public static readonly string[] LevelOrder = { "sanctuaire", "aether", "givre", "fournaise", "neon" };

    /// <summary>Le niveau est-il débloqué ? (1er niveau ou id inconnu = oui ; sinon précédent complété)</summary>
    public bool IsUnlocked(string biomeId)
    {
        int idx = System.Array.IndexOf(LevelOrder, biomeId);
        if (idx <= 0) return true;
        return HasCompletedAny(LevelOrder[idx - 1]);
    }

    // ── High scores (temps survécu max par niveau + difficulté du record) ─────
    private readonly Dictionary<string, int> _bestTimes = new();
    private readonly Dictionary<string, int> _bestDiff  = new();   // biome → (int)GameDifficulty du record

    /// <summary>Meilleur temps survécu (secondes) sur ce niveau, ou 0 si jamais joué.</summary>
    public int BestTime(string biomeId) => _bestTimes.GetValueOrDefault(biomeId, 0);

    /// <summary>Difficulté à laquelle le meilleur temps a été réalisé (Normal par défaut).</summary>
    public GameDifficulty BestDifficulty(string biomeId)
        => (GameDifficulty)_bestDiff.GetValueOrDefault(biomeId, (int)GameDifficulty.Normal);

    /// <summary>Enregistre un temps survécu + la difficulté ; garde le max. True si nouveau record.</summary>
    public bool RecordTime(string biomeId, int secs, GameDifficulty diff)
    {
        if (biomeId.Length == 0 || secs <= _bestTimes.GetValueOrDefault(biomeId, 0)) return false;
        _bestTimes[biomeId] = secs;
        _bestDiff[biomeId]  = (int)diff;
        Save();
        return true;
    }

    /// <summary>Clé de localisation du nom court d'une difficulté (DIFF_EASY/NORMAL/HARD).</summary>
    public static string DifficultyKey(GameDifficulty d) => d switch
    {
        GameDifficulty.Facile    => "DIFF_EASY",
        GameDifficulty.Difficile => "DIFF_HARD",
        _                        => "DIFF_NORMAL",
    };

    // ── Armes découvertes (arsenal) ──────────────────────────────────────────
    private readonly HashSet<string> _discovered = new();

    /// <summary>Armes de signature des personnages : toujours considérées découvertes.</summary>
    public static readonly string[] SignatureWeapons = { "impulse_cannon", "drone_swarm", "plasma_blade" };

    /// <summary>L'arme a-t-elle été découverte (équipée au moins une fois) ou est-elle une arme de signature ?</summary>
    public bool IsDiscovered(string weaponId)
        => System.Array.IndexOf(SignatureWeapons, weaponId) >= 0 || _discovered.Contains(weaponId);

    /// <summary>Marque une arme comme découverte (1re acquisition) et persiste.</summary>
    public void Discover(string weaponId)
    {
        if (weaponId.Length == 0) return;
        if (_discovered.Add(weaponId)) Save();
    }

    // ── Setters (appliquent + sauvegardent) ───────────────────────────────────
    public void SetMaster(float v)     { Master = Mathf.Clamp(v, 0f, 1f); ApplyAudio(); Save(); }
    public void SetMusic(float v)      { Music  = Mathf.Clamp(v, 0f, 1f); ApplyAudio(); Save(); }
    public void SetSfx(float v)        { Sfx    = Mathf.Clamp(v, 0f, 1f); ApplyAudio(); Save(); }
    public void SetFullscreen(bool v)  { Fullscreen = v;   ApplyDisplay(); Save(); }
    public void SetShake(bool v)       { ShakeEnabled = v; ScreenShake.Enabled = v; Save(); }
    public void SetDifficulty(GameDifficulty d) { Difficulty = d; Save(); }

    /// <summary>Change la langue de l'UI, l'applique au TranslationServer et persiste.</summary>
    public void SetLanguage(string lang)
    {
        if (System.Array.IndexOf(Languages, lang) < 0) lang = "en";
        Language = lang;
        TranslationServer.SetLocale(lang);
        Save();
    }

    // ── Application ────────────────────────────────────────────────────────────
    public void Apply()
    {
        ApplyAudio();
        ApplyDisplay();
        ScreenShake.Enabled = ShakeEnabled;
        TranslationServer.SetLocale(Language);
    }

    private void ApplyAudio()
    {
        int master = AudioServer.GetBusIndex("Master");
        if (master >= 0) AudioServer.SetBusVolumeDb(master, Db(Master));
        if (AudioSystem.Instance != null)
        {
            AudioSystem.Instance.MusicVolume = Music;
            AudioSystem.Instance.SfxVolume   = Sfx;
        }
    }

    private void ApplyDisplay()
    {
        DisplayServer.WindowSetMode(Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    private static float Db(float linear) => linear <= 0.001f ? -80f : Mathf.LinearToDb(linear);

    // ── Persistance ────────────────────────────────────────────────────────────
    private void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return; // pas de fichier → défauts
        Master       = (float)cfg.GetValue("audio",   "master",     Master).AsSingle();
        Music        = (float)cfg.GetValue("audio",   "music",      Music).AsSingle();
        Sfx          = (float)cfg.GetValue("audio",   "sfx",        Sfx).AsSingle();
        Fullscreen   = cfg.GetValue("display", "fullscreen", Fullscreen).AsBool();
        ShakeEnabled = cfg.GetValue("gameplay","shake",      ShakeEnabled).AsBool();
        Difficulty   = (GameDifficulty)cfg.GetValue("gameplay", "difficulty", (int)Difficulty).AsInt32();
        Language     = cfg.GetValue("display", "language", Language).AsString();
        if (System.Array.IndexOf(Languages, Language) < 0) Language = "en";

        _completions.Clear();
        foreach (string key in cfg.GetValue("progress", "completions", new string[0]).AsStringArray())
            _completions.Add(key);

        _bestTimes.Clear();
        if (cfg.HasSection("highscores"))
            foreach (string biome in cfg.GetSectionKeys("highscores"))
                _bestTimes[biome] = cfg.GetValue("highscores", biome, 0).AsInt32();

        _bestDiff.Clear();
        if (cfg.HasSection("highscores_diff"))
            foreach (string biome in cfg.GetSectionKeys("highscores_diff"))
                _bestDiff[biome] = cfg.GetValue("highscores_diff", biome, (int)GameDifficulty.Normal).AsInt32();

        _discovered.Clear();
        foreach (string id in cfg.GetValue("discovered", "weapons", new string[0]).AsStringArray())
            _discovered.Add(id);
    }

    /// <summary>Réinitialise TOUTE la progression (complétions, high scores, armes découvertes) et
    /// persiste. Les Échos/améliorations méta sont réinitialisés séparément (MetaProgressionSystem).
    /// Les préférences (audio, langue, difficulté, plein écran) sont conservées.</summary>
    public void ResetProgress()
    {
        _completions.Clear();
        _bestTimes.Clear();
        _bestDiff.Clear();
        _discovered.Clear();
        Save();
    }

    private void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("audio",    "master",     Master);
        cfg.SetValue("audio",    "music",      Music);
        cfg.SetValue("audio",    "sfx",        Sfx);
        cfg.SetValue("display",  "fullscreen", Fullscreen);
        cfg.SetValue("display",  "language",   Language);
        cfg.SetValue("gameplay", "shake",      ShakeEnabled);
        cfg.SetValue("gameplay", "difficulty", (int)Difficulty);

        var keys = new string[_completions.Count];
        _completions.CopyTo(keys);
        cfg.SetValue("progress", "completions", keys);

        foreach (var (biome, secs) in _bestTimes)
            cfg.SetValue("highscores", biome, secs);
        foreach (var (biome, diff) in _bestDiff)
            cfg.SetValue("highscores_diff", biome, diff);

        var disc = new string[_discovered.Count];
        _discovered.CopyTo(disc);
        cfg.SetValue("discovered", "weapons", disc);
        cfg.Save(Path);
    }
}
