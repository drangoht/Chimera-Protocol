using Godot;
using System.Collections.Generic;

/// <summary>
/// AutoLoad singleton — système de Défis / Succès. Charge les définitions depuis
/// data/challenges.json (via la couche pure <see cref="ChallengeTable"/>), les évalue à la fin de
/// chaque run et octroie les récompenses (Échos immédiats ; perks/cosmétiques enregistrés comme
/// débloqués pour les lots 3/4).
///
/// N'est PAS propriétaire de save.json : lit/mute le bloc méta via MetaProgressionSystem (unique
/// propriétaire en mémoire), puis persiste via <see cref="MetaProgressionSystem.PersistMeta"/>.
/// Voir docs/DESIGN_CHALLENGES.md.
/// </summary>
public partial class ChallengeSystem : Node
{
    public static ChallengeSystem Instance { get; private set; } = null!;

    /// <summary>Émis quand un défi est nouvellement accompli (pour un toast / la mise en avant écran).</summary>
    [Signal] public delegate void ChallengeUnlockedEventHandler(string challengeId);

    private readonly List<ChallengeTable.ChallengeDef> _defs = new();

    public IReadOnlyList<ChallengeTable.ChallengeDef> Defs => _defs;

    public override void _Ready()
    {
        Instance = this;
        LoadJson();
        GD.Print($"[ChallengeSystem] Prêt. {_defs.Count} défis chargés.");
    }

    private void LoadJson()
    {
        const string path = "res://data/challenges.json";
        if (!Godot.FileAccess.FileExists(path))
        {
            GD.PrintErr("[ChallengeSystem] challenges.json introuvable.");
            return;
        }
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr("[ChallengeSystem] Impossible de lire challenges.json.");
            return;
        }
        _defs.AddRange(ChallengeTable.Parse(file.GetAsText()));
    }

    // ---------------------------------------------------------------------------
    // Requêtes (utilisées par l'écran Défis — lot 2)
    // ---------------------------------------------------------------------------

    public bool IsUnlocked(string challengeId)
        => MetaProgressionSystem.Instance?.Meta.UnlockedChallenges.Contains(challengeId) ?? false;

    public int UnlockedCount()
    {
        var meta = MetaProgressionSystem.Instance?.Meta;
        if (meta == null) return 0;
        int n = 0;
        foreach (var d in _defs)
            if (meta.UnlockedChallenges.Contains(d.Id)) n++;
        return n;
    }

    // ---------------------------------------------------------------------------
    // Évaluation de fin de run
    // ---------------------------------------------------------------------------

    /// <summary>
    /// À appeler par RunStatsTracker.EndRun. Met à jour les compteurs cumulés, évalue tous les défis,
    /// octroie les récompenses des nouveaux accomplis et persiste une seule fois. Renvoie les ids
    /// nouvellement débloqués (pour affichage). Robuste à l'absence des singletons (tests headless
    /// partiels / boot dégradé).
    /// </summary>
    public List<string> EvaluateRunEnd(
        int runTimeSeconds, int runKills, int runCores, bool levelCompleted, string biomeId, int difficultyRank)
    {
        var newly = new List<string>();
        var meta = MetaProgressionSystem.Instance?.Meta;
        if (meta == null || _defs.Count == 0) return newly;

        // 1) Compteurs cumulés (avant évaluation : les défis lifetime_* voient le total à jour).
        meta.LifetimeKills += runKills;
        meta.LifetimeRuns  += 1;

        // 2) Contexte greffes/fusions et biomes complétés (sources annexes, tolérantes au null).
        int graftsEquipped = 0;
        bool fusionForged = false;
        var assim = AssimilationSystem.Instance;
        if (assim != null)
        {
            graftsEquipped = assim.EquippedGrafts.Count;
            foreach (var id in assim.EquippedGrafts)
            {
                foreach (var fus in assim.Fusions)
                    if (fus.Id == id) { fusionForged = true; break; }
                if (fusionForged) break;
            }
        }

        int biomesCompleted = 0;
        var settings = GameSettings.Instance;
        if (settings != null)
            foreach (var b in GameSettings.LevelOrder)
                if (settings.HasCompletedAny(b)) biomesCompleted++;

        var ctx = new ChallengeTable.ChallengeContext(
            runTimeSeconds, runKills, runCores, levelCompleted, biomeId, difficultyRank,
            graftsEquipped, fusionForged, meta.LifetimeKills, meta.LifetimeRuns, biomesCompleted);

        // 3) Défis nouvellement accomplis.
        var unlockedSet = new HashSet<string>(meta.UnlockedChallenges);
        newly = ChallengeTable.NewlyCompleted(_defs, in ctx, unlockedSet);

        // 4) Octroi des récompenses.
        int echoesGranted = 0;
        foreach (var id in newly)
        {
            meta.UnlockedChallenges.Add(id);
            var def = FindDef(id);
            if (def == null) continue;
            switch (def.RewardType)
            {
                case ChallengeTable.RewardKind.Echoes:
                    echoesGranted += def.RewardEchoes;
                    break;
                case ChallengeTable.RewardKind.Perk:
                    if (def.RewardId.Length > 0 && !meta.UnlockedPerks.Contains(def.RewardId))
                        meta.UnlockedPerks.Add(def.RewardId);
                    break;
                case ChallengeTable.RewardKind.Cosmetic:
                    if (def.RewardId.Length > 0 && !meta.UnlockedCosmetics.Contains(def.RewardId))
                        meta.UnlockedCosmetics.Add(def.RewardId);
                    break;
            }
        }

        // Échos versés en une fois (mutation directe : Persist ci-dessous couvre tout d'un seul write).
        if (echoesGranted > 0)
        {
            meta.CurrentEchoes     += echoesGranted;
            meta.TotalEchoesEarned += echoesGranted;
        }

        // 5) Persistance unique (compteurs + unlocks + échos), puis notification.
        MetaProgressionSystem.Instance!.PersistMeta();

        foreach (var id in newly)
        {
            GD.Print($"[ChallengeSystem] Défi accompli : {id}");
            EmitSignal(SignalName.ChallengeUnlocked, id);
        }

        return newly;
    }

    public ChallengeTable.ChallengeDef? FindDef(string id)
    {
        foreach (var d in _defs)
            if (d.Id == id) return d;
        return null;
    }
}
