using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Journal de combat du boss de fin — le <b>temps de mise à mort</b>, mesuré pendant une vraie partie.
///
/// <para><b>Pourquoi ce relevé ne peut pas venir d'un bot.</b> Le boss se calibre sur un temps de mise
/// à mort <i>joué</i> : un bot kite en cercle et ne produit pas le DPS d'un build construit par un
/// humain. C'est en lisant ce journal qu'on a corrigé les PV du Noyau — d'abord 12 000, puis 8 000,
/// puis 5 000 — parce que la valeur calculée donnait à chaque fois un combat hors de la fenêtre visée.
/// Ce que la classe automatise, c'est le <b>chronométrage</b>, pas le jugement.</para>
///
/// <para>Le chrono part au <b>premier dégât encaissé</b>, jamais à l'apparition : le boss arrive à
/// distance, et le temps d'approche n'appartient pas au combat.</para>
///
/// <para><b>Toujours active</b>, coût nul hors combat de boss. Un joueur qui bat le Noyau en partie
/// normale laisse donc lui aussi un relevé — et le champ « mode » signale les drapeaux de banc, parce
/// qu'un combat sous <c>--invuln</c> reste valide en durée mais ne dit plus rien de la survie.</para>
/// </summary>
public static class BossTelemetry
{
    /// <summary>Nom du journal, dans le dossier de données du joueur.</summary>
    public const string FileName = "boss_ttk.log";

    private static bool _active;
    private static bool _damaged;

    private static float _tSpawn;
    private static float _tFirstHit;
    private static float _maxHp;
    private static float _lastHpRatio = 1f;
    private static float _surchargeSeconds;

    private static string _header = "";
    private static readonly List<string> _events = new();
    private static readonly List<string> _phaseTimes = new();

    /// <summary>Combats écrits depuis le lancement — observable par les bancs.</summary>
    public static int CombatsLogged { get; private set; }

    /// <summary>Chemin complet du journal.</summary>
    public static string LogPath => Path.Combine(UserData.Root, FileName);

    private static float Now => GameManager.Instance != null ? GameManager.Instance.RunTime : Time.time;

    /// <summary>Secondes écoulées depuis le premier dégât — le chrono du combat.</summary>
    private static float Elapsed => _damaged ? Now - _tFirstHit : 0f;

    // ─── Cycle de vie d'un combat ─────────────────────────────────────────────

    /// <summary>
    /// Ouvre un relevé.
    /// </summary>
    /// <remarks>
    /// ⚠ À appeler une fois les PV <b>effectifs</b> posés, jamais depuis l'initialisation du boss : la
    /// montée en puissance du palier s'applique après, et lire trop tôt journaliserait les PV de fiche
    /// à la place de ceux du combat — l'erreur serait invisible, le nombre étant plausible.
    /// </remarks>
    public static void Begin(RustedCore boss)
    {
        Reset();

        _active = true;
        _tSpawn = Now;
        _maxHp = boss.MaxHp;
        _header = ComposeHeader(boss);

        _events.Add("  apparition      (le chrono ne demarre qu'au 1er degat)");
    }

    /// <summary>Premier dégât encaissé : démarre le chrono. Les appels suivants sont ignorés.</summary>
    public static void NotifyFirstDamage()
    {
        if (!_active || _damaged) return;

        _damaged = true;
        _tFirstHit = Now;
        _events.Add($"  1er degat       t=0.0s (approche : {Num(_tFirstHit - _tSpawn, "0.0")}s apres l'apparition)");
    }

    /// <summary>Bascule de phase, horodatée avec les PV restants.</summary>
    public static void NotifyPhase(int phase, float hpRatio)
    {
        if (!_active) return;

        _lastHpRatio = hpRatio;
        float t = Elapsed;

        _phaseTimes.Add(Num(t, "0.0"));
        _events.Add($"  phase {BossPhases.RomanNumeral(phase),-3}       t={Num(t, "0.0")}s ({Pct(hpRatio)} PV)");

        // La surcharge est un temps mort imposé : la retrancher donne la durée où le joueur a
        // réellement pu frapper, seule comparable d'une version à l'autre si ce télégraphe change.
        _surchargeSeconds += BossPhases.TransitionSeconds;
    }

    /// <summary>Boss vaincu : clôt le relevé et l'écrit.</summary>
    public static void NotifyKill()
    {
        if (!_active) return;

        float ttk = Elapsed;
        _lastHpRatio = 0f;
        _events.Add($"  BOSS VAINCU     t={Num(ttk, "0.0")}s");

        Flush("kill", ttk);
    }

    /// <summary>
    /// Fin de run alors qu'un combat était en cours.
    /// </summary>
    /// <remarks>
    /// Le relevé est écrit <b>quand même</b> : un combat perdu à 40 % de PV restants dit autant qu'une
    /// victoire — davantage, même, puisqu'il désigne un boss trop long. N'écrire que les victoires
    /// ferait disparaître du journal exactement les cas qui posent problème.
    /// </remarks>
    public static void NotifyRunEnd(string outcome)
    {
        if (!_active) return;

        float t = Elapsed;
        _events.Add($"  fin de run      t={Num(t, "0.0")}s — boss vivant a {Pct(_lastHpRatio)} PV ({outcome})");

        Flush("abort:" + outcome, t);
    }

    /// <summary>PV courants du boss, relevés à chaque coup — pour le cas d'une run interrompue.</summary>
    public static void NotifyHpRatio(float hpRatio)
    {
        if (_active) _lastHpRatio = hpRatio;
    }

    // ─── Composition ──────────────────────────────────────────────────────────

    private static string ComposeHeader(RustedCore boss)
    {
        var sb = new StringBuilder();

        string biome = GameManager.Instance?.CurrentBiomeId ?? RunConfig.BiomeId;
        int tier = LevelThreat.TierOf(biome);
        int sat = RunConfig.Saturation;

        sb.AppendLine("Moteur       : unity");
        sb.AppendLine($"Biome        : {biome} (palier {tier}, Echos x{Num((float)LevelThreat.EchoMult(tier), "0.00")}) " +
                      $"— incarnation « {boss.Incarnation.Id} »");

        // La saturation change le combat (« Meute » gonfle les PV du champion, « Hemorragie » ce qu'on
        // peut encaisser) : sans elle au relevé, deux durées à des crans différents sont
        // indistinguables — or ce journal existe pour calibrer le boss.
        sb.AppendLine($"Difficulte   : {RunConfig.Difficulty} · saturation {sat}" +
                      (sat > 0 ? $" (x{Num(SaturationTable.ChampionHpMult(sat), "0.00")} PV de champion)" : ""));

        sb.AppendLine($"Mode         : {ComposeMode()}");
        sb.AppendLine($"PV effectifs : {Num(boss.MaxHp, "0")}");
        sb.AppendLine($"Build        : {ComposeLoadout()}");

        return sb.ToString();
    }

    private static string ComposeMode()
    {
        var flags = new List<string>();

        if (DebugHooks.AutoPlay) flags.Add("--auto-play (⚠ build tire au hasard, pas un joueur)");
        if (DebugHooks.Invulnerable) flags.Add("--invuln (⚠ duree valide, survie NON mesuree)");
        if (DebugHooks.SaturateArsenal) flags.Add("--saturate-arsenal");
        if (DebugHooks.StartAtMinutes > 0f) flags.Add("--start-at (⚠ personnage nu)");

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

        var grafts = Assimilation.Equipped;
        string graftText = grafts == null || grafts.Count == 0 ? "—" : string.Join(", ", grafts);

        int level = XpSystem.Instance != null ? XpSystem.Instance.CurrentLevel : 1;

        return $"niv.{level} · armes: {(weapons.Count == 0 ? "—" : string.Join(", ", weapons))} " +
               $"· passifs: {(passives.Count == 0 ? "—" : string.Join(", ", passives))} · greffes: {graftText}";
    }

    // ─── Écriture ─────────────────────────────────────────────────────────────

    private static void Flush(string outcome, float seconds)
    {
        var sb = new StringBuilder();
        string stamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        sb.AppendLine();
        sb.AppendLine($"=== Combat du Noyau Rouille — {stamp}");
        sb.Append(_header);
        sb.AppendLine("Chronologie (t = secondes depuis le 1er degat) :");

        foreach (string e in _events) sb.AppendLine(e);

        float dealt = _maxHp * (1f - _lastHpRatio);
        float dps = seconds > 0.1f ? dealt / seconds : 0f;
        float net = Mathf.Max(0f, seconds - _surchargeSeconds);

        sb.AppendLine($"→ {(outcome == "kill" ? "TTK" : "duree")} = {Num(seconds, "0.0")}s " +
                      $"(net hors surcharges : {Num(net, "0.0")}s) · DPS moyen = {Num(dps, "0")} · degats infliges = {Num(dealt, "0")}");

        // Ligne agrégeable : date;biome;palier;difficulté;saturation;PV;durée;DPS;t_phaseII;t_phaseIII;issue
        // Les deux colonnes de bascule restent présentes, vides si la phase n'a pas été atteinte.
        string phaseTwo = _phaseTimes.Count > 0 ? _phaseTimes[0] : "";
        string phaseThree = _phaseTimes.Count > 1 ? _phaseTimes[1] : "";

        string biome = GameManager.Instance?.CurrentBiomeId ?? RunConfig.BiomeId;

        sb.AppendLine($"CSV;{stamp};{biome};{LevelThreat.TierOf(biome)};{RunConfig.Difficulty};" +
                      $"{RunConfig.Saturation};" +
                      $"{Num(_maxHp, "0")};{Num(seconds, "0.0")};{Num(dps, "0")};" +
                      $"{phaseTwo};{phaseThree};{outcome}");

        string text = sb.ToString();
        Debug.Log(text);

        try
        {
            File.AppendAllText(LogPath, text);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BossTelemetry] ecriture impossible ({LogPath}) : {e.Message}");
        }

        CombatsLogged++;
        Reset();
    }

    /// <summary>
    /// Nombre en culture INVARIANTE — pour <b>tout</b> le relevé, pas seulement pour sa ligne CSV.
    /// </summary>
    /// <remarks>
    /// ⚠ La culture d'un processus headless n'est pas celle qu'on croit, et le premier relevé produit
    /// l'a montré : les durées sortaient en « 9,8s » et le pourcentage de PV en <b>« 66 ٪ »</b> — le
    /// signe pourcent <i>arabe</i>. Un journal illisible ne se distingue pas d'un journal absent, et
    /// une virgule décimale casse en silence tout ce qui lit ce fichier.
    /// </remarks>
    private static string Num(float value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>Pourcentage écrit à la main : le format « P0 » emprunte son signe à la culture.</summary>
    private static string Pct(float ratio)
        => Num(ratio * 100f, "0") + " %";

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
