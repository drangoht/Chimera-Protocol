# CLAUDE.md — Mémoire de projet

Chargé automatiquement au démarrage de chaque session : **rester court et stable**. Le détail vit
dans des fichiers chargés **à la demande** (pointés ci-dessous) pour limiter le contexte par session.

## Le projet

"Chimera Protocol" — survivor roguelite vue du dessus, univers fantaisie-science-fiction (humains,
cyborgs, robots), inspiré de Vampire Survivors et Everything is Crab. **Moteur : Unity 6.5** (C#,
URP 2D). Le dépôt ne contient plus qu'un moteur : Godot a été retiré le **2026-08-10**.

- **Design complet → `docs/GDD.md`** : le consulter avant toute tâche de design/implémentation, et le tenir à jour à chaque décision.
- **Localiser du code** (système, écran, arme, ennemi, données, outil) → invoquer le skill **`/carte-projet`** plutôt que Glob/Grep à froid : il indexe l'arborescence + les checklists de câblage. Le maintenir à jour dans le même commit qu'un changement structurel.
- **Avant de coder** dans un domaine (armes, ennemis, UI/focus, VFX, assets, tests headless) → lire **`docs/PITFALLS_UNITY.md`**. Y ajouter tout nouveau piège découvert.
- **Comprendre l'architecture du portage** (principe logique-pure/moteur, ponts `Platform/`, contrats d'entités, cycle de vie d'une run) → **`docs/UNITY_MIGRATION_PLAN.md`**.
- **État d'implémentation détaillé & version courante → `docs/PROJECT_STATE.md`**.
- **Interroger un fichier trop gros pour être lu** → **MCP local** `mcp__local-llm__local_digest` (un ou plusieurs fichiers → une synthèse) / `local_map` (même question sur N fichiers). Outils **différés** : `ToolSearch` d'abord. Le serveur lit les fichiers côté LM Studio — **le contenu brut n'entre jamais en contexte** (mesuré : 83 173 tokens lus en local → 675 renvoyés).
  - **Les cibles** : `docs/TEST_REPORT.md` (~290 Ko) · `docs/GDD.md` (~220 Ko) · `docs/PITFALLS_UNITY.md` (~120 Ko) · `docs/DEVLOG.md` · `unity/Assets/StreamingAssets/localization/ui.csv` · `.../data/*.json`.
  - ⚠ **Lent** : ~6-7 min pour 290 Ko. Le lancer et continuer à travailler. Prévoir `max_tokens` large (1500-2500 pour un inventaire ; 900 a déjà été tronqué **sans le signaler**).
  - ⚠ **Bon sur du TEXTE, à proscrire sur des CHIFFRES.** Un LLM qui « lit » un CSV de mesures produit des nombres plausibles et faux. Règle : *s'il existe un outil déterministe, il gagne* (`tools/power_loop.py` pour `power_curve.log`).
  - ⚠ **Jamais pour du code qu'on s'apprête à éditer** — là, il faut le contenu réel. Ni pour localiser (`Grep` est instantané et exact).

## Phase actuelle

**Migration Unity terminée, 2.0.0 prête à publier.** Le jeu est jouable de bout en bout, avec son,
validé en jouant. **626 tests.** ▶ L'auteur veut **jouer avant de publier** — ne pas lancer
`tools/release_unity.ps1` sans `-DryRun` sans son accord explicite.

**Le dépôt est mono-moteur depuis le 2026-08-10.** Ce qui a changé :
- `src/`, `scenes/`, `project.godot`, le `.csproj`/`.sln` Godot, `assets/`, `data/` et
  `localization/` racine ont été **supprimés** — tout vit sous `unity/Assets/`.
- **Source unique** pour les données (`StreamingAssets/data`, `.../localization/ui.csv`) et pour les
  assets : plus de copie racine, donc plus de dérive possible.
- Les générateurs Python écrivent **directement là où le jeu lit**, via **`tools/unity_paths.py`**
  (table de destination) et **`tools/spriteframes.py`** (manifestes d'animation, ex-`.tres`).
- Doc de l'ère Godot conservée sous **`docs/archive-godot/`** (fond valable, chemins périmés).

⚠ **La leçon du portage, toujours en vigueur : déclaré n'est pas consommé.** Neuf fois une donnée,
une règle ou un système entier existait, était testé, et n'était appelé par rien — trouvé en jouant,
jamais par l'automatisation. Deux outils sont nés de là : `tools/audit_json_keys.py` et
`tools/audit_unused_members.py`. **Les lancer après tout ajout de données ou de règle.**

## Équipe d'agents

Agents dans `.claude/agents/` : `game-designer`, `directeur-artistique`, `graphiste`, `developpeur`,
`musicien`, `story-teller`, `marketing`, `game-tester`, `release-manager`. Déléguer proactivement à
l'agent compétent — **qui fait quoi et dans quel ordre : `GUIDE-CLAUDE-CODE.md`**.
⚠ **Un agent qui décrit un état périmé du projet donne des instructions fausses avec autorité** :
quand une phase se termine, relire les agents qu'elle concerne (dernière passe : 2026-08-10).

- **`game-tester`** : construit et lance le binaire Unity, joue le jeu, documente les bugs dans `docs/TEST_REPORT.md`. À invoquer après chaque implémentation majeure.
- **`release-manager`** : publie la release binaire de bout en bout (semver, release notes, `tools/release_unity.ps1`, MAJ doc) puis **rédige le devlog** — l'utilisateur le publie lui-même sur itch (l'agent ne pilote pas le navigateur). Source des notes : `docs/DEVLOG.md`.

## Maintenance de la doc

- `README.md` (racine) — MAJ à chaque changement de phase / ajout majeur.
- `docs/PROJECT_STATE.md` + `docs/GDD.md` + `/carte-projet` + `docs/PITFALLS_UNITY.md` — MAJ dans le commit qui change ce qu'ils décrivent.

## Conventions

- Plateforme cible : Windows (.exe). Moteur : **Unity 6.5** (`6000.5.6f1`), C#, URP 2D.
- **Build** : `Unity.exe -batchmode -quit -projectPath unity -executeMethod BuildBench.Windows64Game`
  → `unity/Build/game/ChimeraProtocol.exe` (ignoré par git, régénéré).
- **Publication (itch.io + Butler)** : skill **`/publier-itch`** ou `tools/release_unity.ps1 -Version X.Y.Z`
  (essayer d'abord avec `-DryRun`), ou déléguer à l'agent **`release-manager`**. Le script pose lui-même
  `bundleVersion` — ne pas l'éditer à la main. Runbook : `docs/RELEASE.md` ; notes cumulées : `docs/DEVLOG.md`.
- Style de code : PascalCase classes/méthodes, `_camelCase` champs privés, `readonly` par défaut.
- **Logique pure testable** : `unity/Assets/Scripts/Shared/Rules/` — classes statiques **sans
  dépendance moteur** (`XpCurve`, `EnemyScaling`, `SaturationTable`…). Les `MonoBehaviour` y délèguent.
  `Shared/PlatformCore/` porte le socle déterministe (`Pcg32`, `TimerWheel`, `Easing`).
- **Tests unitaires** : xUnit, `dotnet test tests/ChimeraProtocol.Tests.csproj` — **626 tests**.
  Ils compilent `Shared/` **par chemin** : aucun moteur, aucun build requis.
- ⚠ **`Art/` ≠ `Resources/`** : `Art/` est consommé par **GUID** (planches d'animation), `Resources/`
  **par chemin** (`Resources.Load`) et embarqué en entier dans le binaire. Se tromper de dossier ne
  lève rien — le jeu affiche l'ancienne image. La table qui fait autorité : `tools/unity_paths.py`.
- **Difficulté** : trois axes multiplicatifs — réglage du joueur (`DifficultyTuning`), **palier de
  menace du niveau joué** (`LevelThreat`, cf. `docs/GDD.md` §28) et escalade d'overtime — plus
  l'**échelle de saturation** (`SaturationTable`, §34), un cran = une règle nommée.
- Sauvegarde : `%USERPROFILE%\AppData\LocalLow\drangoht\Chimera Protocol\` (méta/Échos + préférences,
  records, complétions, découvertes). Les sauvegardes Godot des joueurs sont migrées (`GodotConfig`,
  `SaveMigration`) — **c'est le seul endroit du portage dont l'échec est irréversible pour le joueur**.
- Sprites : PNG transparent, grille 32×32 px (Colosse 48×48, mid-boss 72, boss 154). Import en
  `Point`, `spritePixelsPerUnit = 1` (1 px = 1 unité : toutes les valeurs du jeu se transposent telles
  quelles). Style **pseudo-3D avec ombres** (`docs/ART_BRIEF_PSEUDO3D.md`) via `tools/pseudo3d_lib.py`
  — toujours dériver shadow/highlight avec `shade()`/`shade_sprite()`/`shade_tile()`/`shade_icon()`.
- **Audio** : musique **générée sur Suno** (prompts : `docs/AUDIO_AI_PROMPTS.md`) — jamais éditer un
  `.ogg` à la main. Pour en remplacer une : regénérer, déposer dans `music_ai/`, puis
  `python tools/import_ai_music.py [--only <id>]`. Bande-son de secours synthétisée (sans contrainte
  de licence) : `tools/generate_music_v3.py`. SFX = WAV Kenney CC0. Crédits et **licence Suno (plan
  gratuit, non commercial)** : `docs/AUDIO_CREDITS.md`.
  ⚠ Une arme absente de la table **`WeaponSfx`** est **muette** — 14 l'ont été sans que rien ne le dise.
- Localisation EN/FR/ES : `unity/Assets/StreamingAssets/localization/ui.csv` → `Loc.T("CLÉ")`
  (lu tel quel, aucune étape d'import).
- Performance cible : 200–300 entités simultanées ; I-frames joueur 0,45 s (CRITIQUE pour les nuées).
- Palette UI : fond `#1A1A2E`, cyan `#44FFEE`, violet `#AA44FF`, or `#FFCC44`, blanc cassé `#D9D9F2`.
  Police : Share Tech Mono ; VT323 en réserve.
- **Cadres & couleurs d'UI** : toujours via `Scripts/UI/UiPalette.cs` (couleurs) et `UiStyle.cs`
  (cadres « plaque blindée »). Jamais de couleur en dur. Parti pris → `docs/ART_BRIEF_UI_FRAMES.md` ;
  assets → `tools/generate_ui_frames.py`.
- Python : `C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe` (pas dans le PATH).

## Pièges critiques → `docs/PITFALLS_UNITY.md`

Tous les pièges non évidents (import d'assets et `.meta`, `Resources` vs `Art`, cycle de vie des
scènes, navigation clavier/manette, calques d'UI, VFX, tests headless, export .NET, tampon de build)
sont dans **`docs/PITFALLS_UNITY.md`**. **Le consulter avant de coder dans le domaine concerné.**
Les pièges de **conception** hérités de Godot (câblage d'une arme, d'un ennemi, d'un affixe ; mixage ;
lisibilité) restent valables dans `docs/archive-godot/PITFALLS.md` — leurs chemins, non.
