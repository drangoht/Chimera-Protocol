# Design — Système de Défis / Succès (rétention méta)

> 4e levier de rétention, après l'arsenal (level-up), la baseline (Hub/Échos) et le corps
> (Assimilation). But : donner des **objectifs explicites** au-delà de « bats le boss » / « achète
> tout l'arbre », dans le moule de la toile de déblocages de Vampire Survivors. Décidé le 2026-07-08.

## Principe

Chaque **défi** est une condition évaluée **à la fin d'une run** à partir de données déjà suivies
(temps, kills, noyaux, complétion, greffes/fusions, compteurs cumulés). Accompli une fois → verse une
**récompense** puis reste débloqué. Trois types de récompense :

| Type | Effet | Lot |
|---|---|---|
| `echoes` | Pactole d'Échos versé immédiatement (accélère l'arbre méta existant) | **1** |
| `perk` | Débloque un **perk de départ** équipable au Hub (arme alt., greffe offerte, +1 slot) | 3 |
| `cosmetic` | Débloque un **titre / cosmétique** sélectionnable | 4 |

Le schéma de données supporte les 3 types dès le Lot 1 (pas de migration ultérieure). Les perks et
cosmétiques débloqués sont **enregistrés** dès le Lot 1 ; leur **équipement/application** vient aux
lots 3-4.

## Architecture (SRP)

- **`src/Core/Rules/ChallengeTable.cs`** — logique **pure** testable (aucune dépendance Godot, modèle
  `GraftTable`). Parse `data/challenges.json` ; `ChallengeContext` (instantané plat de fin de run) ;
  `IsMet(def, ctx)` ; `NewlyCompleted(defs, ctx, alreadyUnlocked)`. **16 tests xUnit.**
- **`src/Systems/ChallengeSystem.cs`** — autoload. Charge les définitions, agrège le `ChallengeContext`
  (RunStatsTracker + AssimilationSystem + GameSettings + compteurs sauvegardés), octroie les
  récompenses, persiste, émet `ChallengeUnlocked`.
- **`data/challenges.json`** — 13 défis (source de vérité, modifiable sans recompiler).
- **Persistance** : `MetaSaveData` (save.json) étendu — `UnlockedChallenges`, `UnlockedPerks`,
  `UnlockedCosmetics`, `LifetimeKills`, `LifetimeRuns`.

### Propriété de save.json — piège important

`SaveManager.Load()` renvoie une **copie fraîche** à chaque appel. `MetaProgressionSystem` détient
l'**unique** copie en mémoire du bloc méta. `ChallengeSystem` **ne charge jamais sa propre SaveData** —
il mute `MetaProgressionSystem.Meta` puis appelle `MetaProgressionSystem.PersistMeta()` (unique point
d'écriture). Sinon deux copies divergentes s'écrasent (perte d'Échos). Cf. `docs/PITFALLS.md`.

## Types de condition (`condition.type`)

| Type | Satisfait si | Source |
|---|---|---|
| `survive_seconds` | temps de run ≥ `value` | RunStatsTracker |
| `kills_in_run` | kills de la run ≥ `value` | RunStatsTracker |
| `cores_in_run` | noyaux de la run ≥ `value` | RunStatsTracker |
| `grafts_in_run` | greffes équipées ≥ `value` | AssimilationSystem |
| `fusion_in_run` | ≥ 1 fusion équipée | AssimilationSystem |
| `complete_level` | boss de fin de niveau vaincu | RunStatsTracker.LevelCompleted |
| `complete_biome` (`param`=biomeId) | niveau `param` terminé cette run | + GameManager.CurrentBiomeId |
| `complete_difficulty` (`value`=rang 0-2) | niveau terminé à difficulté ≥ rang | + GameSettings.Difficulty |
| `lifetime_kills` | kills cumulés (toutes runs) ≥ `value` | MetaSaveData.LifetimeKills |
| `lifetime_runs` | runs jouées ≥ `value` | MetaSaveData.LifetimeRuns |
| `biomes_completed` | nb de biomes complétés ≥ `value` | GameSettings.HasCompletedAny |

Type inconnu → jamais satisfait (dégradation sûre). L'évaluation passe **après** `RecordCompletion`
pour que les défis de complétion voient la run à jour.

## Défis livrés (Lot 1) — 13

Combat : First Blood (100 kills), Core Harvester (15 noyaux), Exterminator (10 000 kills cumulés →
titre). Survie : Field Tested (5 min), The Long Watch (13 min), Veteran (25 runs). Assimilation :
First Mutation (1 greffe), Full Chimera (3 greffes → perk `start_graft_swarm`), Fusion Forged (→ titre
`title_chimera`). Maîtrise : Sector Cleared (1er boss), Neon Conqueror (→ perk `start_weapon_glaive`),
No Mercy (Difficile → titre `title_apex`), World Eater (5 biomes → perk `start_extra_slot`).

Loc EN/FR/ES : clés `CHAL_*` dans `localization/ui.csv`.

## Feedback de fin de run

`RunEndScreen` affiche une ligne dorée « ★ Défi accompli : … » sous le total d'Échos (résumé ;
détail complet sur l'écran Défis du lot 2). SFX `sfx_core_collect`.

## Roadmap des lots

1. **Socle** (✅ 2026-07-08) — couche pure + data + persistance + autoload + hook fin de run + toast.
   Récompense `echoes` active ; perks/cosmétiques enregistrés.
2. Écran Défis (`ChallengesScreen`, sous-classe `CodexScreenBase`) + bouton menu.
3. Perks de départ : UI d'équipement au Hub + application au démarrage de run.
4. Cosmétiques / titres : sélection + application (teinte joueur / titre affiché).
5. Équilibrage seuils + doc + build/ZIP + publication itch.
