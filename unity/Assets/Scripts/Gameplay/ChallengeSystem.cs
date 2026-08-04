using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Défis et récompenses — le <b>quatrième levier de rétention</b>, après l'arsenal, le Hub et
/// l'Assimilation (Lot 6).
///
/// <para>Les conditions et le tableau vivent dans <see cref="ChallengeTable"/> (logique pure,
/// partagée avec Godot). Ce composant ne fait que trois choses, et c'est délibéré : construire le
/// <b>contexte</b> de fin de run, verser les récompenses, et persister.</para>
///
/// <para>⚠ <b>Un défi ne se paie qu'une fois.</b> C'est l'invariant de tout ce fichier : la liste des
/// défis déjà accomplis est consultée avant chaque évaluation, et écrite dans le même
/// <see cref="MetaProgression.Save"/> que les Échos — jamais dans une copie à part, qui écraserait le
/// solde en s'enregistrant.</para>
/// </summary>
public static class ChallengeSystem
{
    private static List<ChallengeTable.ChallengeDef>? _defs;

    /// <summary>Émis pour chaque défi nouvellement accompli — le HUD s'y branche.</summary>
    public static event Action<ChallengeTable.ChallengeDef>? Completed;

    /// <summary>Défis définis, dans l'ordre du fichier.</summary>
    public static IReadOnlyList<ChallengeTable.ChallengeDef> All
    {
        get
        {
            if (_defs != null) return _defs;

            string? json = DataFiles.Load("challenges.json");
            _defs = json != null ? ChallengeTable.Parse(json) : new List<ChallengeTable.ChallengeDef>();
            Debug.Log($"[ChallengeSystem] {_defs.Count} defis charges.");
            return _defs;
        }
    }

    /// <summary>Ce défi est-il déjà accompli ?</summary>
    public static bool IsUnlocked(string challengeId)
        => MetaProgression.Save.Meta.UnlockedChallenges.Contains(challengeId);

    /// <summary>Nombre de défis accomplis — la progression affichée « X / N ».</summary>
    public static int UnlockedCount()
    {
        int count = 0;
        foreach (var def in All)
            if (IsUnlocked(def.Id)) count++;
        return count;
    }

    /// <summary>Définition d'un id, ou <c>null</c>.</summary>
    public static ChallengeTable.ChallengeDef? FindDef(string id)
    {
        foreach (var def in All)
            if (def.Id == id) return def;
        return null;
    }

    /// <summary>
    /// Évalue la fin de run et verse les récompenses. Renvoie les défis nouvellement accomplis.
    ///
    /// <para>Appelé <b>après</b> <see cref="MetaProgression.RegisterRun"/> : les conditions cumulées
    /// (« 1 000 000 d'éliminations », « 100 runs ») doivent voir la run qui vient de se terminer,
    /// sinon elles se déclenchent toujours une partie trop tard.</para>
    /// </summary>
    public static IReadOnlyList<ChallengeTable.ChallengeDef> EvaluateRunEnd(
        int runSeconds, int kills, int cores, bool levelCompleted, string biomeId,
        int difficultyRank, int graftsEquipped, bool fusionForged)
    {
        var meta = MetaProgression.Save.Meta;

        var context = new ChallengeTable.ChallengeContext(
            RunTimeSeconds: runSeconds,
            RunKills: kills,
            RunCores: cores,
            LevelCompleted: levelCompleted,
            BiomeId: biomeId,
            DifficultyRank: difficultyRank,
            RunGraftsEquipped: graftsEquipped,
            RunFusionForged: fusionForged,
            LifetimeKills: meta.LifetimeKills,
            LifetimeRuns: meta.LifetimeRuns,
            BiomesCompletedCount: GameSettings.Current.Completions.Count);

        var already = new HashSet<string>(meta.UnlockedChallenges, StringComparer.Ordinal);
        var newlyDone = ChallengeTable.NewlyCompleted(All, in context, already);
        if (newlyDone.Count == 0) return Array.Empty<ChallengeTable.ChallengeDef>();

        var granted = new List<ChallengeTable.ChallengeDef>();
        int echoes = 0;

        foreach (string id in newlyDone)
        {
            var def = FindDef(id);
            if (def == null) continue;

            meta.UnlockedChallenges.Add(id);

            switch (def.RewardType)
            {
                case ChallengeTable.RewardKind.Echoes:
                    echoes += def.RewardEchoes;
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

            granted.Add(def);
            Completed?.Invoke(def);
        }

        // Les Échos passent par MetaProgression, qui persiste : une écriture directe ici créerait un
        // second point de vérité et perdrait ce que l'autre vient d'écrire.
        if (echoes > 0) MetaProgression.AddEchoes(echoes);
        else            MetaProgression.Persist();

        Debug.Log($"[ChallengeSystem] {granted.Count} defi(s) accompli(s), +{echoes} Echos.");
        return granted;
    }

    /// <summary>Oublie les définitions chargées — réservé aux bancs.</summary>
    public static void Reset() => _defs = null;
}
