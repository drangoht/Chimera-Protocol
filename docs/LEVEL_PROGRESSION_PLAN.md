# Plan — Fin de niveau, progression, high scores & arsenal à découverte

> Plan préparé le 2026-06-30, à réaliser **étape par étape**. Ancré sur les systèmes réels :
> `RunStatsTracker`, `GameSettings` (persistance `user://settings.cfg`), `RustedCore`,
> `EnemySpawner`/`SpawnCurve`, `LevelSelectScreen`, `ArsenalScreen`/`Codex`/`InventorySystem`.

## La nouvelle boucle de niveau (vue d'ensemble)

Aujourd'hui : battre le boss final = **fin de run « extraction réussie »** (la run s'arrête).

Demain :
1. **Temps imparti** : le timer compte jusqu'à l'arrivée du **boss de fin de niveau** (`rusted_core`, ~13 min).
2. **Fin du temps → escalade brutale (overtime)** : dès le temps imparti atteint, la difficulté grimpe **très vite** — vagues massives de monstres, **mini-boss et boss répétés**.
3. **Battre le boss de fin de niveau = niveau TERMINÉ** → débloque le niveau suivant + badge « VAINCU ». **La run NE s'arrête PAS** : l'escalade continue.
4. **La run se termine à la MORT du joueur.** Le **temps survécu** est enregistré comme **high score du niveau** (s'il bat le précédent).

→ On peut donc « terminer » un niveau (battre le boss) puis continuer à survivre pour le high score. Mourir avant le boss = pas de déblocage, mais un high score quand même.

## Décisions de design — VERROUILLÉES (2026-06-30)

- ✅ **D1 — Modèle de fin = survie sans fin** : battre le boss = **niveau TERMINÉ** (débloque le suivant + badge), mais la run **continue** en escalade jusqu'à la **mort**. High score = **temps survécu total**.
- ✅ **D2 — Ordre de déblocage** : **Sanctuaire → Aether → Givre → Fournaise → Néon** (Sanctuaire jouable d'office).
- ✅ **D3 — Arme non découverte** : nom masqué **« ??? »** + icône **grisée/silhouette** + description **« Arme non découverte »**. Les **armes de signature** des persos restent toujours découvertes.

## Étapes d'implémentation (lots livrables)

### Étape 1 — Déblocage progressif des niveaux
- `GameSettings` : ordre des biomes (`LevelOrder`) + **`IsUnlocked(biomeId)`** = 1er niveau OU précédent complété (`HasCompletedAny`). Dérivé des complétions déjà persistées — aucun nouveau stockage requis.
- `LevelSelectScreen` : cartes **verrouillées** grisées + cadenas, **non sélectionnables** (bouton « Jouer ici » désactivé) ; « Aléatoire » ne tire que parmi les débloqués. Le badge « VAINCU » (complété) reste.
- Test : seul Sanctuaire jouable au départ ; battre son boss débloque Aether ; etc.

### Étape 2 — Escalade « overtime » après le temps imparti
- `RunStatsTracker` : expose **`Overtime`** (true dès `ElapsedSeconds ≥ T_target`, où T_target = `spawnStartMinute` du boss).
- `EnemySpawner`/`SpawnCurve` : en overtime, **rampe agressive** — cap et cadence de spawn fortement relevés, scaling HP/dégâts accéléré, **spawn périodique de mini-boss puis de boss** (toutes les N s, N décroissant). Garde-fou perf (cap 300 maintenu).
- Test : à T_target l'arène devient brutale, mini-boss/boss reviennent en boucle.

### Étape 3 — Fin de run à la mort + complétion sans arrêt
- `RustedCore.FinishDeath` : remplace `EndRun("extraction_success")` par **`RunStatsTracker.OnLevelBossDefeated()`** → `RecordCompletion(biome)` + déblocage + **bannière « NIVEAU TERMINÉ »** ; la run **continue**.
- `RunStatsTracker.EndRun("death")` (mort joueur) : enregistre le **high score** (cf. étape 4) puis ouvre l'écran de fin.
- Écran de fin : affiche **temps survécu**, **niveau terminé ?** (oui/non), **high score** (+ « NOUVEAU RECORD ! » si battu).

### Étape 4 — High score (temps survécu) par niveau
- `GameSettings` : nouvelle section `highscores` dans `settings.cfg` (clé `biomeId` → secondes max). API `BestTime(biomeId)` / `RecordTime(biomeId, secs)` (garde le max).
- Mise à jour à `EndRun` (toute fin) avec `ElapsedSeconds`.
- Affichage : sur la carte du `LevelSelectScreen` (« Record : mm:ss ») + sur l'écran de fin.

### Étape 5 — Arsenal à découverte
- `GameSettings` : section `discovered` (set d'ids d'armes) persistée. **Armes de signature** des persos (impulse_cannon, drone_swarm, plasma_blade) **toujours considérées découvertes**.
- `InventorySystem.AddOrUpgradeWeapon` : marque l'arme **découverte** à la 1re acquisition (`GameSettings.Discover(id)`).
- `ArsenalScreen` : pour une arme non découverte → carte **verrouillée** (nom « ??? », icône grisée, desc « Arme non découverte » via clé `ARSENAL_LOCKED_*`). Passifs/fusions : à décider (probablement même règle, hors armes de base).
- Clés de localisation `ARSENAL_LOCKED_NAME`/`ARSENAL_LOCKED_DESC` (EN/FR/ES).

### Étape 6 — Doc + itch + build
- MAJ `CLAUDE.md`, `README.md`, `docs/ITCH_STORE_PAGE_EN.md` (boucle de survie + progression + high scores), ré-export exe + ZIP (cf. [[feedback-phase-docs-itch]]).

## Schéma de persistance (`user://settings.cfg`)
- `[progress] completions` *(existant)* — `"biomeId:difficulté"`.
- `[highscores] <biomeId>` *(nouveau)* — secondes max survécues.
- `[discovered] weapons` *(nouveau)* — `PackedStringArray` d'ids d'armes découvertes.

## Notes / risques
- Changer le modèle de fin touche `RustedCore` + `RunStatsTracker` + l'écran de fin : étape 3 = la plus sensible (tester la non-régression mort/complétion).
- L'escalade overtime doit rester **perf-safe** (cap 300) et **jouable** (la mort doit venir de la pression, pas d'un freeze).
- Compatibilité saves : les nouvelles sections sont optionnelles (absentes = valeurs par défaut) — pas de migration.
