using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Journal de la <b>courbe de puissance</b> d'une run : ce que le joueur inflige, ce qu'on lui
/// oppose, et l'état de son build, échantillonnés d'un bout à l'autre de la partie.
///
/// <para><b>Ce que ce journal a permis de trancher</b>, et qu'aucune session jouée n'aurait pu :
/// que le canal de soin dominant n'était pas celui qu'on réglait (soins ponctuels ×9,5 la
/// régénération) ; que le joueur <b>jette 80 % des soins reçus</b> ; qu'un passif atteignait 100 % de
/// réduction de recharge dès le niveau 8. Sans trace continue, chacune de ces conclusions demandait
/// de deviner.</para>
///
/// <para><b>Activé par <c>--power-curve</c> seulement.</b> Il écrit en continu et n'a aucune valeur
/// pour un joueur ; sans le drapeau, il ne coûte qu'un test booléen par coup porté.</para>
///
/// <para><b>Écrit au fil de l'eau</b>, jamais gardé en mémoire jusqu'à la fin : un banc d'overtime se
/// termine souvent par un processus tué au chronomètre, et une run sous <c>--invuln</c> ne se termine
/// pas du tout. Perdre le journal dans ces deux cas viderait la mesure de son intérêt.</para>
/// </summary>
public static class PowerTelemetry
{
    /// <summary>Période d'échantillonnage, en secondes de <b>temps de jeu</b> (accélération comprise).</summary>
    public const float SampleInterval = 15f;

    /// <summary>Nom du journal, dans le dossier de données du joueur.</summary>
    public const string FileName = "power_curve.log";

    private static bool _active;
    private static float _sinceSample;

    private static float _dealtWindow;      // dégâts infligés depuis le dernier échantillon
    private static float _takenWindow;      // dégâts encaissés
    private static float _regenWindow;      // PV réellement rendus par la régénération continue
    private static float _healWindow;       // PV réellement rendus par les soins ponctuels
    private static float _healRawWindow;    // PV OFFERTS par les soins ponctuels, gaspillage compris

    private static int _killsAtLastSample;
    private static string _lastBuild = "";
    private static bool _wasOvertime;

    /// <summary>
    /// Pression ressentie, relevée <b>à la frame</b>. Toutes les autres colonnes sont des débits
    /// moyennés sur quinze secondes, donc aveugles à un <b>pic</b> : un plongeon à 10 % des PV suivi
    /// d'une remontée ne déplace aucune moyenne, et c'est pourtant tout ce qu'un joueur retient.
    /// </summary>
    private static readonly PressureMeter _pressure = new();

    /// <summary>Le journal tourne-t-il ?</summary>
    public static bool Active => _active;

    /// <summary>Échantillons écrits depuis le début de la run — observable par les bancs.</summary>
    public static int SampleCount { get; private set; }

    /// <summary>Chemin complet du journal.</summary>
    public static string LogPath => Path.Combine(UserData.Root, FileName);

    // ─── Cycle de vie ─────────────────────────────────────────────────────────

    /// <summary>Ouvre le journal, si le drapeau est là. Appelé au démarrage de la run.</summary>
    public static void Begin()
    {
        if (!DebugHooks.PowerCurve) return;

        Reset();
        _active = true;

        Append(ComposeHeader() + "\n");
        Append("t_s;phase;niveau;indice_puissance;dps;degats_subis_ps;kills_fenetre;ennemis;" +
               "mult_degats;reduc_cd;reduc_degats;pv;pv_max;regen_ps;regen_eff_ps;soins_ps;" +
               "soins_bruts_ps;pv_min_pct;frolements;part_danger;vitesse;armes;passifs;greffes\n");
    }

    /// <summary>Dégâts infligés à un ennemi, résistances déjà appliquées.</summary>
    public static void NotifyDamageDealt(float amount)
    {
        if (_active) _dealtWindow += amount;
    }

    /// <summary>Dégâts réellement encaissés par le joueur (réduction faite, i-frames déjà filtrées).</summary>
    public static void NotifyDamageTaken(float amount)
    {
        if (_active) _takenWindow += amount;
    }

    /// <summary>
    /// PV <b>réellement</b> rendus par la régénération continue — c'est-à-dire après bornage aux PV
    /// max. La distinction avec le taux nominal est la seule réponse possible à « son effet ne se voit
    /// pas » : un joueur à PV pleins régénère nominalement beaucoup et effectivement zéro.
    /// </summary>
    public static void NotifyRegen(float amount)
    {
        if (_active && amount > 0f) _regenWindow += amount;
    }

    /// <summary>
    /// Soin <b>ponctuel</b> (orbe, vol de vie, carte de surcharge).
    /// </summary>
    /// <param name="applied">PV réellement rendus, après bornage.</param>
    /// <param name="attempted">PV <b>offerts</b> par la source, gaspillage compris.</param>
    /// <remarks>
    /// ⚠ <b>Les deux montants sont nécessaires, et les confondre inverse le diagnostic.</b> Le montant
    /// appliqué est borné par les PV manquants : à PV pleins, un soin vaut zéro. Il mesure donc une
    /// <i>conversion</i>, qui monte mécaniquement dès que le joueur prend plus de dégâts. Lu ainsi, un
    /// cran de saturation semblait rendre <b>+41 % de soins</b> quand il en donnait <b>−46 %</b> — et
    /// deux correctifs ont été écrits, mesurés puis annulés sur cette lecture fausse.
    /// </remarks>
    public static void NotifyHealed(float applied, float attempted)
    {
        if (!_active) return;

        if (applied > 0f) _healWindow += applied;
        if (attempted > 0f) _healRawWindow += attempted;
    }

    /// <summary>
    /// Avance l'horloge d'échantillonnage, et relève la pression.
    /// </summary>
    /// <remarks>
    /// Le delta passé est celui de la frame : sous <c>--timescale</c> il est déjà dilaté, donc la
    /// période reste exprimée en <b>secondes de jeu</b> — la même run échantillonne aux mêmes endroits
    /// quelle que soit l'accélération du banc.
    /// </remarks>
    public static void Tick(float delta)
    {
        if (!_active) return;

        // La pression se relève ICI, à la frame. Relevée dans l'échantillon (toutes les 15 s de jeu),
        // elle ne verrait qu'un instantané sur environ neuf cents et manquerait précisément les creux
        // qu'elle existe pour compter.
        var stats = Player.Instance?.Stats;
        if (stats != null) _pressure.Observe(stats.CurrentHp, stats.MaxHp, delta);

        _sinceSample += delta;
        if (_sinceSample < SampleInterval) return;

        Sample(_sinceSample);
        _sinceSample = 0f;
    }

    /// <summary>Fin de run : dernier échantillon partiel, puis clôture.</summary>
    public static void End(string outcome)
    {
        if (!_active) return;

        if (_sinceSample > 1f) Sample(_sinceSample);
        Append($"# fin de run : {outcome}\n");
        Reset();
    }

    // ─── Échantillon ──────────────────────────────────────────────────────────

    private static void Sample(float windowSeconds)
    {
        var gm = GameManager.Instance;
        var stats = Player.Instance?.Stats;

        float t = gm != null ? gm.RunTime : 0f;

        // ⚠ L'overtime retombe à faux dès que la run est terminée. Sans mémoire, le dernier
        // échantillon — celui de la clôture — serait étiqueté « run » en plein overtime, et tout
        // rapport entrée-overtime → fin serait faux.
        _wasOvertime |= gm != null && gm.Overtime;

        int kills = gm != null ? gm.Kills : 0;
        int level = XpSystem.Instance != null ? XpSystem.Instance.CurrentLevel : 1;

        bool measurable = windowSeconds > 0.1f;
        float dps = measurable ? _dealtWindow / windowSeconds : 0f;
        float dtps = measurable ? _takenWindow / windowSeconds : 0f;

        // Ramenés à la seconde comme les dégâts subis : la seule lecture qui intéresse le design est
        // « ce que la régénération rend » CONTRE « ce que le contenu retire », dans la même unité.
        float regenPs = measurable ? _regenWindow / windowSeconds : 0f;
        float healPs = measurable ? _healWindow / windowSeconds : 0f;
        float healRawPs = measurable ? _healRawWindow / windowSeconds : 0f;

        string build = ComposeBuild();
        float power = InventorySystem.Instance != null ? InventorySystem.Instance.PowerIndex() : 0f;

        Append($"{Num(t, "0")};{(_wasOvertime ? "OT" : "run")};{level};{Num(power, "0")};" +
               $"{Num(dps, "0")};{Num(dtps, "0.0")};{kills - _killsAtLastSample};{EnemyBase.Active.Count};" +
               $"{Num(stats?.DamageMultiplier ?? 0f, "0.00")};" +
               $"{Num(stats?.CooldownReduction ?? 0f, "0.00")};" +
               $"{Num(stats?.DamageReduction ?? 0f, "0.00")};" +
               $"{Num(stats?.CurrentHp ?? 0f, "0")};{Num(stats?.MaxHp ?? 0f, "0")};" +
               $"{Num(stats?.HpRegenPerSecond ?? 0f, "0.00")};{Num(regenPs, "0.0")};{Num(healPs, "0.0")};" +
               $"{Num(healRawPs, "0.0")};" +
               $"{Num(_pressure.LowestRatio * 100f, "0")};{_pressure.CloseCalls};" +
               $"{Num(_pressure.DangerFraction, "0.000")};" +
               $"{Num(stats?.Speed ?? 0f, "0")};" +
               build + "\n");

        _pressure.ResetWindow();
        SampleCount++;

        // Un build qui change entre deux échantillons est la seule explication possible d'un saut de
        // puissance une fois les cartes plafonnées : on le signale en clair, plutôt que d'obliger à
        // comparer deux lignes colonne par colonne.
        if (build != _lastBuild && _lastBuild.Length > 0)
            Append($"# t={t:0}s — le build a change\n");

        _lastBuild = build;

        _dealtWindow = _takenWindow = _regenWindow = _healWindow = _healRawWindow = 0f;
        _killsAtLastSample = kills;
    }

    /// <summary>Armes / passifs / greffes, en trois colonnes (séparateur interne : la virgule).</summary>
    private static string ComposeBuild()
    {
        var inv = InventorySystem.Instance;

        var weapons = new List<string>();
        var passives = new List<string>();

        if (inv != null)
        {
            foreach (var kv in inv.WeaponLevels) weapons.Add($"{kv.Key} L{kv.Value}");
            foreach (var kv in inv.PassiveLevels) passives.Add($"{kv.Key} L{kv.Value}");
        }

        var grafts = Assimilation.Equipped;
        string graftText = grafts == null || grafts.Count == 0 ? "-" : string.Join(",", grafts);

        return $"{(weapons.Count == 0 ? "-" : string.Join(",", weapons))};" +
               $"{(passives.Count == 0 ? "-" : string.Join(",", passives))};" +
               $"{graftText}";
    }

    private static string ComposeHeader()
    {
        var sb = new StringBuilder();

        string biome = GameManager.Instance?.CurrentBiomeId ?? RunConfig.BiomeId;
        int tier = LevelThreat.TierOf(biome);

        sb.AppendLine();
        sb.AppendLine($"=== Courbe de puissance — {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        // ⚠ Le CRAN DE SATURATION figure ici au même titre que le biome : il change les soins reçus,
        // la densité, la fréquence d'élite et l'heure d'entrée en overtime. Sans lui, deux campagnes
        // à des crans différents sont indistinguables dans le journal.
        sb.AppendLine($"# moteur unity · biome {biome} (palier {tier}) · difficulte {RunConfig.Difficulty} " +
                      $"· saturation {RunConfig.Saturation} " +
                      $"· echantillon toutes les {SampleInterval:0}s de jeu");

        // La graine est journalisée pour que le banc apparie les runs par leur CONTENU et non par leur
        // ordre d'apparition : une run qui plante sans rien écrire décalerait sinon toute la campagne,
        // et la comparaison appariée deviendrait fausse sans le dire.
        if (DebugHooks.Seed.HasValue) sb.AppendLine($"# seed {DebugHooks.Seed.Value}");

        if (DebugHooks.AutoPlay)
            sb.AppendLine("# --auto-play : cartes tirees AU HASARD (build non representatif d'un joueur)");
        if (DebugHooks.Invulnerable)
            sb.AppendLine("# --invuln : degats subis nuls, la colonne degats_subis_ps ne veut rien dire");
        if (DebugHooks.TimeScale > 0f)
            sb.AppendLine($"# --timescale={DebugHooks.TimeScale.ToString("0.##", CultureInfo.InvariantCulture)}");
        if (DebugHooks.StartAtMinutes > 0f)
            sb.AppendLine($"# --start-at={DebugHooks.StartAtMinutes.ToString("0.##", CultureInfo.InvariantCulture)}" +
                          " : personnage NU a cette minute — borne haute, pas un joueur reel");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    // ─── Écriture ─────────────────────────────────────────────────────────────

    private static void Append(string text)
    {
        Debug.Log("[PowerCurve] " + text.TrimEnd('\n'));

        try
        {
            File.AppendAllText(LogPath, text);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PowerTelemetry] ecriture impossible ({LogPath}) : {e.Message}");
        }
    }

    /// <summary>Nombre en culture INVARIANTE : un « 44,3 » casserait tout lecteur de ce CSV.</summary>
    private static string Num(float value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture);

    private static void Reset()
    {
        _active = false;
        _sinceSample = 0f;
        _dealtWindow = _takenWindow = _regenWindow = _healWindow = _healRawWindow = 0f;
        _killsAtLastSample = 0;
        _lastBuild = "";
        _wasOvertime = false;
        SampleCount = 0;
        _pressure.Reset();
    }
}
