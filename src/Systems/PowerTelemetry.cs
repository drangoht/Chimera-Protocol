using System.Collections.Generic;
using System.Text;
using Godot;

/// <summary>
/// Journal de la <b>courbe de puissance</b> d'une run — mesure du rapport entre ce que le joueur
/// inflige et ce que le contenu lui oppose, échantillonné tout au long de la partie.
///
/// Motivation (cf. <c>docs/TEST_REPORT.md</c>, run jouée du 2026-07-28) : le DPS mesuré contre le
/// boss passe de 617 (boss de fin) à 2322 (deuxième boss d'overtime) en deux minutes, alors que
/// **toutes les cartes sont déjà au plafond L20** — la cause n'est donc pas l'arbre de level-up, et
/// rien dans le jeu ne permettait de dire d'où venait le reste. <see cref="BossTelemetry"/> ne relève
/// qu'un instantané par combat de boss ; il fallait une trace continue de l'état du build.
///
/// Un échantillon toutes les <see cref="SampleInterval"/> secondes de jeu : DPS et dégâts subis sur
/// la fenêtre, population d'ennemis, stats effectives du joueur, et composition complète du build
/// (armes, passifs, greffes). Toute apparition de greffe / fusion / arme entre deux échantillons est
/// visible par simple diff des colonnes — aucun hook à poser dans les systèmes de progression.
///
/// **Activée par le flag <c>--power-curve</c> uniquement** : contrairement à
/// <see cref="BossTelemetry"/> (un bloc par combat de boss), ce journal écrit en continu et n'a
/// aucune valeur pour un joueur. Coût nul sans le flag (un test booléen par coup porté).
/// </summary>
public static class PowerTelemetry
{
    private const string LogPath = "user://power_curve.log";

    /// <summary>Période d'échantillonnage, en secondes de temps de jeu (temps accéléré compris).</summary>
    public const float SampleInterval = 15f;

    private static bool  _active;
    private static float _sinceSample;
    private static float _dealtWindow;      // dégâts infligés aux ennemis depuis le dernier échantillon
    private static float _takenWindow;      // dégâts encaissés par le joueur depuis le dernier échantillon
    private static int   _killsAtLastSample;
    private static string _lastBuild = "";  // pour ne marquer que les changements de build
    private static bool  _wasOvertime;      // l'overtime a été atteint (il ne se quitte jamais)

    /// <summary>Vrai si le journal tourne (flag <c>--power-curve</c> et run en cours).</summary>
    public static bool Active => _active;

    // -------------------------------------------------------------------------
    // Cycle de vie
    // -------------------------------------------------------------------------

    /// <summary>
    /// Ouvre un journal si le flag est présent. Appelé par <see cref="RunStatsTracker"/> au début
    /// de la run — un relevé par run, écrit au fil de l'eau puis clôturé à la mort du joueur.
    /// </summary>
    public static void Begin()
    {
        if (!DebugHooks.PowerCurve) return;
        Reset();
        _active = true;
        // Écriture au fil de l'eau (et non à la fin) : un banc d'overtime se termine souvent par un
        // processus tué au chronomètre, et une run sous --invuln ne se termine jamais. Perdre le
        // journal dans ces deux cas viderait la mesure de son intérêt.
        Append(ComposeHeader() + "\n");
        Append("t_s;phase;niveau;indice_puissance;dps;degats_subis_ps;kills_fenetre;ennemis;" +
               "mult_degats;reduc_cd;reduc_degats;pv_max;vitesse;armes;passifs;greffes\n");
    }

    /// <summary>Dégâts infligés à un ennemi (déjà modulés par ses propres résistances).</summary>
    public static void NotifyDamageDealt(float amount)
    {
        if (_active) _dealtWindow += amount;
    }

    /// <summary>Dégâts réellement encaissés par le joueur (après réduction, i-frames déjà filtrées).</summary>
    public static void NotifyDamageTaken(float amount)
    {
        if (_active) _takenWindow += amount;
    }

    /// <summary>
    /// Avance l'horloge d'échantillonnage. Appelé depuis <c>RunStatsTracker._Process</c> avec le
    /// delta de la frame : sous <c>--timescale</c> les deltas sont déjà dilatés, la période reste
    /// donc exprimée en secondes de jeu — la même run échantillonne au même endroit quelle que
    /// soit l'accélération du banc.
    /// </summary>
    public static void Tick(float delta)
    {
        if (!_active) return;
        _sinceSample += delta;
        if (_sinceSample < SampleInterval) return;
        Sample(_sinceSample);
        _sinceSample = 0f;
    }

    /// <summary>Fin de run : dernier échantillon partiel puis clôture du journal.</summary>
    public static void End(string outcome)
    {
        if (!_active) return;
        if (_sinceSample > 1f) Sample(_sinceSample);
        Append($"# fin de run : {outcome}\n");
        Reset();
    }

    // -------------------------------------------------------------------------
    // Échantillon
    // -------------------------------------------------------------------------

    private static void Sample(float windowSeconds)
    {
        var tracker = RunStatsTracker.Instance;
        var player  = GameManager.Instance?.PlayerInstance;
        var stats   = player?.Stats;

        float t       = tracker?.ElapsedSeconds ?? 0f;
        // `Overtime` retombe à false dès que la run est terminée : sans mémoire, le dernier
        // échantillon (celui de End) se retrouverait étiqueté « run » en plein overtime et fausserait
        // tout calcul de ratio entrée-overtime → fin.
        _wasOvertime |= tracker?.Overtime ?? false;
        bool overtime = _wasOvertime;
        int kills     = tracker?.KillCount ?? 0;
        int level     = XpSystem.Instance?.CurrentLevel ?? 1;

        float dps   = windowSeconds > 0.1f ? _dealtWindow / windowSeconds : 0f;
        float dtps  = windowSeconds > 0.1f ? _takenWindow / windowSeconds : 0f;
        int killsWin = kills - _killsAtLastSample;

        var tree     = Engine.GetMainLoop() as SceneTree;
        int enemies  = tree?.GetNodesInGroup(Constants.GroupEnemies).Count ?? 0;

        string build = ComposeBuild();

        float powerIndex = InventorySystem.Instance?.PowerIndex() ?? 0f;

        Append($"{Num(t, "0")};{(overtime ? "OT" : "run")};{level};{Num(powerIndex, "0")};" +
               $"{Num(dps, "0")};{Num(dtps, "0.0")};{killsWin};{enemies};" +
               $"{Num(stats?.DamageMultiplier ?? 0f, "0.00")};" +
               $"{Num(stats?.CooldownReduction ?? 0f, "0.00")};" +
               $"{Num(stats?.DamageReduction ?? 0f, "0.00")};" +
               $"{Num(stats?.MaxHp ?? 0f, "0")};{Num(stats?.Speed ?? 0f, "0")};" +
               build + "\n");

        // Un build qui change entre deux échantillons est la seule explication possible d'un saut de
        // DPS une fois les cartes plafonnées : on le signale en clair plutôt que d'obliger à differ.
        if (build != _lastBuild && _lastBuild.Length > 0)
            Append($"# t={t:0}s — le build a changé\n");
        _lastBuild = build;

        _dealtWindow = 0f;
        _takenWindow = 0f;
        _killsAtLastSample = kills;
    }

    /// <summary>Armes / passifs / greffes, en trois colonnes CSV (séparateur interne : virgule).</summary>
    private static string ComposeBuild()
    {
        var inv = InventorySystem.Instance;
        var weapons  = new List<string>();
        var passives = new List<string>();
        if (inv != null)
        {
            foreach (var kv in inv.WeaponLevels)  weapons.Add($"{kv.Key} L{kv.Value}");
            foreach (var kv in inv.PassiveLevels) passives.Add($"{kv.Key} L{kv.Value}");
        }

        var grafts = AssimilationSystem.Instance?.EquippedGrafts;
        string graftText = grafts == null || grafts.Count == 0 ? "-" : string.Join(",", grafts);

        return $"{(weapons.Count  == 0 ? "-" : string.Join(",", weapons))};" +
               $"{(passives.Count == 0 ? "-" : string.Join(",", passives))};" +
               $"{graftText}";
    }

    private static string ComposeHeader()
    {
        var sb = new StringBuilder();
        string biome = GameManager.Instance?.CurrentBiomeId ?? "?";
        int tier = LevelThreat.TierOf(GameManager.Instance?.CurrentBiomeId);
        var diff = GameSettings.Instance?.Difficulty ?? GameSettings.GameDifficulty.Normal;

        sb.AppendLine();
        sb.AppendLine($"=== Courbe de puissance — {Time.GetDatetimeStringFromSystem()}");
        sb.AppendLine($"# version v{BuildInfo.Version}-{BuildInfo.GitSha} · biome {biome} (palier {tier}) " +
                      $"· difficulté {diff} · échantillon toutes les {SampleInterval:0}s de jeu");
        if (DebugHooks.AutoPlay)  sb.AppendLine("# --auto-play : choix de cartes ALÉATOIRES (build non représentatif d'un joueur)");
        if (DebugHooks.Invulnerable) sb.AppendLine("# --invuln : dégâts subis nuls, la colonne degats_subis_ps ne veut rien dire");
        if (DebugHooks.TimeScale > 0f) sb.AppendLine($"# --timescale={DebugHooks.TimeScale:0.##}");
        return sb.ToString().TrimEnd('\n', '\r');
    }

    // -------------------------------------------------------------------------
    // Écriture
    // -------------------------------------------------------------------------

    private static void Append(string text)
    {
        GD.Print("[PowerCurve] " + text.TrimEnd('\n'));

        // ReadWrite ne crée pas le fichier : premier lancement = Write. Godot n'a pas de mode append.
        using var file = Godot.FileAccess.FileExists(LogPath)
            ? Godot.FileAccess.Open(LogPath, Godot.FileAccess.ModeFlags.ReadWrite)
            : Godot.FileAccess.Open(LogPath, Godot.FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"[PowerTelemetry] Écriture impossible : {Godot.FileAccess.GetOpenError()}");
            return;
        }
        file.SeekEnd();
        file.StoreString(text);
    }

    /// <summary>Nombre en culture INVARIANTE : un « 44,3 » casserait tout parseur de ce CSV.</summary>
    private static string Num(float value, string format)
        => value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    private static void Reset()
    {
        _active = false;
        _sinceSample = 0f;
        _dealtWindow = 0f;
        _takenWindow = 0f;
        _killsAtLastSample = 0;
        _lastBuild = "";
        _wasOvertime = false;
    }
}
