using System.Collections.Generic;
using System.Text;
using Godot;

/// <summary>
/// Journal de combat du boss de fin — mesure du TTK **par un testeur humain**.
///
/// Le bot PyAutoGUI (<c>tools/boss_ttk_test.py</c>) kite en cercle et ne représente pas le DPS d'un
/// vrai build : ses relevés ne valent pas validation d'équilibrage (cf. GDD §20.2, cible ~20-30 s
/// pour un build de référence). Cette classe fait le chronométrage à la place du testeur : elle
/// horodate le premier dégât, chaque bascule de phase et la mort, puis écrit un bloc lisible
/// (+ une ligne <c>CSV;</c> agrégeable) dans <c>user://boss_ttk.log</c>.
///
/// Le chrono part au **premier dégât encaissé par le boss**, pas à son apparition : le boss
/// apparaît à distance et le temps d'approche n'appartient pas au TTK.
///
/// Toujours active (coût nul hors combat de boss) — un joueur qui bat le boss en run normale laisse
/// donc lui aussi un relevé, ce que le mode <c>--debug-boss</c> seul ne permettait pas. Le champ
/// « mode » du relevé signale les flags de debug : un combat sous <c>--invuln</c> reste mesurable en
/// TTK mais ne dit rien de la survivabilité, il est marqué comme tel.
/// </summary>
public static class BossTelemetry
{
    private const string LogPath = "user://boss_ttk.log";

    private static bool  _active;          // un combat est en cours d'enregistrement
    private static bool  _damaged;         // le boss a encaissé au moins un coup
    private static float _tSpawn;
    private static float _tFirstHit;
    private static float _maxHp;
    private static float _lastHpRatio = 1f;
    private static float _surchargeSeconds;
    private static string _header = "";
    private static readonly List<string> _events = new();
    private static readonly List<string> _phaseTimes = new();

    private static float Now => RunStatsTracker.Instance?.ElapsedSeconds
                                ?? (float)Time.GetTicksMsec() / 1000f;

    /// <summary>Secondes depuis le premier dégât (le chrono du TTK). 0 avant celui-ci.</summary>
    private static float Elapsed => _damaged ? Now - _tFirstHit : 0f;

    // -------------------------------------------------------------------------
    // Cycle de vie d'un combat
    // -------------------------------------------------------------------------

    /// <summary>
    /// Ouvre un relevé. À appeler en différé depuis <c>RustedCore._Ready</c> : les PV réels sont
    /// posés par <c>EnemySpawner.ApplyScaling</c> APRÈS <c>_Ready</c>, les lire trop tôt journaliserait
    /// les 12000 PV de base au lieu des PV effectifs du palier et du temps de spawn.
    /// </summary>
    public static void Begin(RustedCore boss)
    {
        Reset();
        _active = true;
        _tSpawn = Now;
        _maxHp  = boss.MaxHp;
        _header = ComposeHeader(boss);
        _events.Add("  apparition      (le chrono ne démarre qu'au 1er dégât)");
    }

    /// <summary>Premier dégât encaissé : démarre le chrono. Appels suivants ignorés.</summary>
    public static void NotifyFirstDamage()
    {
        if (!_active || _damaged) return;
        _damaged   = true;
        _tFirstHit = Now;
        _events.Add($"  1er dégât       t=0.0s (approche : {_tFirstHit - _tSpawn:0.0}s après l'apparition)");
    }

    /// <summary>Bascule de phase (I→II→III), horodatée avec les PV restants.</summary>
    public static void NotifyPhase(int phase, float hpRatio)
    {
        if (!_active) return;
        _lastHpRatio = hpRatio;
        float t = Elapsed;
        _phaseTimes.Add(Num(t, "0.0"));
        _events.Add($"  phase {BossPhases.RomanNumeral(phase),-3}       t={t:0.0}s ({hpRatio:P0} PV)");
        _surchargeSeconds += BossPhases.TransitionSeconds;
    }

    /// <summary>Boss vaincu : clôt le relevé et l'écrit sur disque.</summary>
    public static void NotifyKill()
    {
        if (!_active) return;
        float ttk = Elapsed;
        _lastHpRatio = 0f;
        _events.Add($"  BOSS VAINCU     t={ttk:0.0}s");
        Flush("kill", ttk);
    }

    /// <summary>
    /// Fin de run alors qu'un combat était en cours (mort du joueur, abandon) : le relevé est écrit
    /// quand même — un combat perdu à 40 % de PV restants dit autant qu'un TTK qu'une victoire.
    /// </summary>
    public static void NotifyRunEnd(string outcome)
    {
        if (!_active) return;
        float t = Elapsed;
        _events.Add($"  fin de run      t={t:0.0}s — boss vivant à {_lastHpRatio:P0} PV ({outcome})");
        Flush("abort:" + outcome, t);
    }

    /// <summary>PV courants du boss, échantillonnés à chaque coup (pour le cas « run interrompue »).</summary>
    public static void NotifyHpRatio(float hpRatio)
    {
        if (_active) _lastHpRatio = hpRatio;
    }

    // -------------------------------------------------------------------------
    // Composition du relevé
    // -------------------------------------------------------------------------

    private static string ComposeHeader(RustedCore boss)
    {
        var sb = new StringBuilder();

        string biome = GameManager.Instance?.CurrentBiomeId ?? "?";
        int tier = LevelThreat.TierOf(GameManager.Instance?.CurrentBiomeId);
        var diff = GameSettings.Instance?.Difficulty ?? GameSettings.GameDifficulty.Normal;

        sb.AppendLine($"Version      : v{BuildInfo.Version}-{BuildInfo.GitSha}");
        sb.AppendLine($"Biome        : {biome} (palier {tier}, Échos ×{LevelThreat.EchoMult(tier):0.00}) " +
                      $"— incarnation « {boss.IncarnationId} »");
        sb.AppendLine($"Difficulté   : {diff}");
        sb.AppendLine($"Mode         : {ComposeMode()}");
        sb.AppendLine($"PV effectifs : {boss.MaxHp:0}");
        sb.AppendLine($"Build        : {ComposeLoadout()}");
        return sb.ToString();
    }

    private static string ComposeMode()
    {
        var flags = new List<string>();
        if (DebugHooks.BossDebug)    flags.Add("--debug-boss (loadout de référence, boss isolé)");
        if (DebugHooks.Invulnerable) flags.Add("--invuln (⚠ TTK valide, survie NON mesurée)");
        if (!string.IsNullOrEmpty(DebugHooks.ForcedFusion)) flags.Add("--force-fusion");
        if (!string.IsNullOrEmpty(DebugHooks.ForcedGraft))  flags.Add("--force-graft");
        return flags.Count == 0 ? "run normale" : string.Join(" + ", flags);
    }

    private static string ComposeLoadout()
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return "?";

        var weapons = new List<string>();
        foreach (var kv in inv.WeaponLevels) weapons.Add($"{kv.Key} L{kv.Value}");
        var passives = new List<string>();
        foreach (var kv in inv.PassiveLevels) passives.Add($"{kv.Key} L{kv.Value}");

        var grafts = AssimilationSystem.Instance?.EquippedGrafts;
        string graftText = grafts == null || grafts.Count == 0 ? "—" : string.Join(", ", grafts);

        int level = XpSystem.Instance?.CurrentLevel ?? 1;
        return $"niv.{level} · armes: {(weapons.Count == 0 ? "—" : string.Join(", ", weapons))} " +
               $"· passifs: {(passives.Count == 0 ? "—" : string.Join(", ", passives))} · greffes: {graftText}";
    }

    /// <summary>
    /// Écrit le bloc dans <c>user://boss_ttk.log</c> (append) ET dans la console. Le fichier est
    /// rouvert à chaque combat : une session qui crashe garde tous les combats déjà terminés.
    /// </summary>
    private static void Flush(string outcome, float seconds)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"=== Combat du Noyau Rouillé — {Time.GetDatetimeStringFromSystem()}");
        sb.Append(_header);
        sb.AppendLine("Chronologie (t = secondes depuis le 1er dégât) :");
        foreach (var e in _events) sb.AppendLine(e);

        float dealt = _maxHp * (1f - _lastHpRatio);
        float dps   = seconds > 0.1f ? dealt / seconds : 0f;
        float net   = Mathf.Max(0f, seconds - _surchargeSeconds);
        sb.AppendLine($"→ {(outcome == "kill" ? "TTK" : "durée")} = {seconds:0.0}s " +
                      $"(net hors surcharges : {net:0.0}s) · DPS moyen = {dps:0} · dégâts infligés = {dealt:0}");

        // Ligne agrégeable : date;biome;palier;difficulté;PV;durée;DPS;t_phaseII;t_phaseIII;issue
        // Les deux colonnes de bascule sont toujours présentes (vides si la phase n'a pas été atteinte).
        // Nombres en culture INVARIANTE : sur un poste FR, un « 44,3 » casserait tout tableur et tout
        // parseur qui lit ce fichier (la virgule y est un séparateur de champ usuel).
        string phaseTwo   = _phaseTimes.Count > 0 ? _phaseTimes[0] : "";
        string phaseThree = _phaseTimes.Count > 1 ? _phaseTimes[1] : "";
        sb.AppendLine($"CSV;{Time.GetDatetimeStringFromSystem()};" +
                      $"{GameManager.Instance?.CurrentBiomeId ?? "?"};" +
                      $"{LevelThreat.TierOf(GameManager.Instance?.CurrentBiomeId)};" +
                      $"{GameSettings.Instance?.Difficulty ?? GameSettings.GameDifficulty.Normal};" +
                      $"{Num(_maxHp, "0")};{Num(seconds, "0.0")};{Num(dps, "0")};" +
                      $"{phaseTwo};{phaseThree};{outcome}");

        string text = sb.ToString();
        GD.Print(text);
        Append(text);
        Reset();

        QuitIfHeadlessBench();
    }

    /// <summary>
    /// En banc d'essai automatisé (<c>--headless</c> + <c>--debug-boss</c>), le relevé est le seul
    /// livrable : la run continue sinon indéfiniment (survie sans fin) et il faudrait tuer le
    /// processus au chronomètre. Aucun effet en jeu normal — la fenêtre ne se ferme jamais toute
    /// seule pour un joueur.
    /// </summary>
    private static void QuitIfHeadlessBench()
    {
        if (!DebugHooks.BossDebug && !DebugHooks.AutoPlay) return;
        if (DisplayServer.GetName() != "headless") return;
        (Engine.GetMainLoop() as SceneTree)?.Quit();
    }

    /// <summary>Formate un nombre en culture invariante (point décimal), pour la ligne CSV.</summary>
    private static string Num(float value, string format)
        => value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    private static void Append(string text)
    {
        // ReadWrite ne crée pas le fichier : premier lancement = Write. Godot n'a pas de mode append.
        using var file = Godot.FileAccess.FileExists(LogPath)
            ? Godot.FileAccess.Open(LogPath, Godot.FileAccess.ModeFlags.ReadWrite)
            : Godot.FileAccess.Open(LogPath, Godot.FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"[BossTelemetry] Écriture impossible : {Godot.FileAccess.GetOpenError()}");
            return;
        }
        file.SeekEnd();
        file.StoreString(text);
    }

    private static void Reset()
    {
        _active = false;
        _damaged = false;
        _tSpawn = 0f;
        _tFirstHit = 0f;
        _maxHp = 0f;
        _lastHpRatio = 1f;
        _surchargeSeconds = 0f;
        _header = "";
        _events.Clear();
        _phaseTimes.Clear();
    }
}
