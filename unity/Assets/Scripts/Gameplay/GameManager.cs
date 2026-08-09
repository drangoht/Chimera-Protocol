using System;
using UnityEngine;

/// <summary>
/// Cycle de vie d'une run — port du noyau de <c>GameManager</c> (Lot 2).
///
/// <para>Sous Godot, c'était un AutoLoad ; ici, un composant créé par le bootstrap dans un ordre
/// <b>explicite</b> (§4.6). Le contrat d'accès <c>GameManager.Instance</c> est conservé tel quel :
/// c'est ce qui permet aux systèmes portés de garder leurs appelants inchangés.</para>
/// </summary>
public sealed class GameManager : MonoBehaviour
{
    public static GameManager? Instance { get; private set; }

    /// <summary>Durée de la run en cours, en secondes.</summary>
    public float RunTime { get; private set; }

    /// <summary>Ennemis tués pendant la run.</summary>
    public int Kills { get; private set; }

    /// <summary>
    /// Noyaux d'Aether ramassés pendant la run. Ils comptent pour les <b>Échos</b> gagnés et pour le
    /// défi « Moissonneur de Noyaux » — une valeur laissée à zéro rendrait ce défi inaccomplissable.
    /// </summary>
    public int CoresCollected { get; private set; }

    /// <summary>La run est-elle terminée (mort du joueur ou limite atteinte) ?</summary>
    public bool RunEnded { get; private set; }

    /// <summary>Multiplicateur d'XP du biome courant.</summary>
    public float BiomeXpMult { get; set; } = 1f;

    /// <summary>Biome joué — décide de l'incarnation du boss et du pool de faune.</summary>
    public string? CurrentBiomeId { get; set; }

    /// <summary>
    /// Temps imparti, en secondes : la fin du décompte fait <b>arriver le boss</b>, elle ne termine
    /// pas la run. Lu depuis <c>meta_upgrades.json</c> (780 s), même source que sous Godot.
    /// </summary>
    public int RunDurationSeconds { get; private set; } = 780;

    /// <summary>
    /// « Overtime » : le temps imparti est écoulé. Déclenche l'escalade — vagues massives, champions
    /// en boucle et arrivée du Noyau Rouillé.
    /// </summary>
    public bool Overtime => !RunEnded && RunTime >= RunDurationSeconds;

    /// <summary>Secondes écoulées depuis l'entrée en overtime (0 avant).</summary>
    public float OvertimeSeconds => Mathf.Max(0f, RunTime - RunDurationSeconds);

    /// <summary>
    /// Le boss de fin a-t-il été vaincu ? <b>C'est la condition de victoire du niveau</b> — et non la
    /// fin de la run, qui n'arrive qu'à la mort du joueur.
    /// </summary>
    public bool BossDefeated { get; private set; }

    /// <summary>Émis à l'entrée en overtime — une seule fois par run.</summary>
    public event Action? OvertimeStarted;

    /// <summary>Émis quand le Noyau Rouillé tombe.</summary>
    public event Action? BossDown;

    /// <summary>Émis à la fin de la run, avec sa durée et le nombre de victimes.</summary>
    public event Action<float, int>? RunFinished;

    private bool _overtimeAnnounced;

    private void Awake()
    {
        Instance = this;
        LoadRunDuration();
    }

    /// <summary>
    /// Lit la durée impartie dans les données de tuning. En cas d'absence, la valeur par défaut tient
    /// — un fichier manquant ne doit pas produire une run d'une durée nulle, donc en overtime immédiat.
    /// </summary>
    private void LoadRunDuration()
    {
        string? json = DataFiles.Load("meta_upgrades.json");
        if (json == null) return;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("runDurationSeconds", out var v) &&
                v.TryGetInt32(out int seconds) && seconds > 0)
                // Le cran « Compte à rebours » raccourcit le temps imparti : il attaque le temps de
                // CONSTRUCTION du build, pas la puissance — le boss arrive face à un arsenal amputé.
                RunDurationSeconds = RunConfig.RunDurationSeconds(seconds);
        }
        catch (System.Text.Json.JsonException e)
        {
            Debug.LogError($"[GameManager] meta_upgrades.json illisible : {e.Message}");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Démarre une run : remet à zéro tout ce qui est état de partie.</summary>
    public void StartRun()
    {
        // ⚠ L'horloge peut démarrer AVANCÉE (--start-at) : la fenêtre à instruire est l'overtime, où
        // le bot n'arrive pas de lui-même. Tout ce qui dépend du temps — montée en puissance des
        // ennemis, densité, heure d'entrée en overtime — suit, puisque tout se dérive de RunTime.
        RunTime = Mathf.Max(0f, DebugHooks.StartAtMinutes) * 60f;
        Kills = 0;
        CoresCollected = 0;
        RunEnded = false;
        BossDefeated = false;
        _overtimeAnnounced = false;

        XpSystem.Instance?.ResetForRun();
        Player.Instance?.Stats.ResetForRun();

        if (Player.Instance != null) Player.Instance.Died += OnPlayerDied;

        PowerTelemetry.Begin();
    }

    /// <summary>Comptabilise une victime — appelé par les ennemis à leur mort.</summary>
    public void RegisterKill() => Kills++;

    /// <summary>Comptabilise un Noyau d'Aether ramassé.</summary>
    public void RegisterCoreCollected() => CoresCollected++;

    /// <summary>
    /// Raccourcit le temps imparti — réservé aux <b>bancs et au débogage</b> : attendre 13 minutes
    /// réelles pour observer l'arrivée du boss rendrait cette partie du jeu invérifiable.
    /// </summary>
    public void OverrideRunDuration(int seconds)
    {
        RunDurationSeconds = Mathf.Max(1, seconds);
        _overtimeAnnounced = false;
    }

    /// <summary>
    /// Enregistre la chute du boss. <b>Idempotent</b> : plusieurs Noyaux se succèdent en overtime, et
    /// la complétion du niveau ne s'obtient qu'une fois.
    /// </summary>
    public void RegisterBossDefeated()
    {
        if (BossDefeated) return;
        BossDefeated = true;
        BossDown?.Invoke();
        Debug.Log("[GameManager] Noyau Rouille vaincu — niveau complete.");
    }

    private void Update()
    {
        if (RunEnded) return;
        RunTime += Time.deltaTime;

        // Le delta est celui du jeu : sous --timescale il est déjà dilaté, donc la période
        // d'échantillonnage reste exprimée en secondes de JEU et une même run est relevée aux mêmes
        // endroits, accélérée ou non.
        PowerTelemetry.Tick(Time.deltaTime);

        if (!_overtimeAnnounced && Overtime)
        {
            _overtimeAnnounced = true;
            OvertimeStarted?.Invoke();
            Debug.Log("[GameManager] Overtime — le Noyau Rouille arrive.");
        }

        // --run-limit : sans elle, un banc d'overtime ne s'arrête jamais — la survie est sans fin, et
        // sous --invuln le joueur ne meurt pas. L'issue est nommée pour que l'outil de dépouillement
        // ÉCARTE ces runs : leur survie n'est pas mesurée, elle est minorée par le chronomètre.
        if (DebugHooks.RunLimit > 0f && RunTime >= DebugHooks.RunLimit) EndRun("bench_limit");
    }

    private void OnPlayerDied() => EndRun("mort");

    /// <summary>Clôt la run. Idempotent : une double fin ne doit pas doubler les récompenses.</summary>
    public void EndRun() => EndRun("fin");

    /// <summary>Clôt la run en nommant son issue — c'est elle qui décide si le relevé est exploitable.</summary>
    public void EndRun(string outcome)
    {
        if (RunEnded) return;
        RunEnded = true;

        PowerTelemetry.End(outcome);

        // Un combat de boss en cours est écrit quand même : « mort du joueur à 40 % des PV du boss »
        // est un résultat, et c'est même celui qui désigne un boss trop long.
        BossTelemetry.NotifyRunEnd(outcome);

        RunFinished?.Invoke(RunTime, Kills);
    }
}
