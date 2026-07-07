# CLAUDE.md — Mémoire de projet

Chargé automatiquement au démarrage de chaque session : **rester court et stable**. Le détail vit
dans des fichiers chargés **à la demande** (pointés ci-dessous) pour limiter le contexte par session.

## Le projet

"Chimera Protocol" — survivor roguelite vue du dessus, univers fantaisie-science-fiction (humains,
cyborgs, robots), inspiré de Vampire Survivors et Everything is Crab.

- **Design complet → `docs/GDD.md`** : le consulter avant toute tâche de design/implémentation, et le tenir à jour à chaque décision.
- **Localiser du code** (système, écran, arme, ennemi, données, outil) → invoquer le skill **`/carte-projet`** plutôt que Glob/Grep à froid : il indexe l'arborescence + les checklists de câblage. Le maintenir à jour dans le même commit qu'un changement structurel.
- **Avant de coder** dans un domaine (armes, ennemis, UI/focus, VFX, scènes, assets, tests headless) → lire **`docs/PITFALLS.md`** (pièges non-évidents Godot/C# + checklists de câblage). Y ajouter tout nouveau piège découvert.
- **État d'implémentation détaillé & version courante → `docs/PROJECT_STATE.md`** (évolutif). Résumé de phase ci-dessous.

**Phase actuelle : libre.** Dernière livraison majeure : **Assimilation en ligne** — 3e axe de
progression (« Ne tue pas les monstres. Deviens-les. ») publié pour la première fois : 5 greffes,
2 fusions (Charge Blindée, Ruche de Tourelles), nouvel écran Codex **Chimère**, lisibilité HUD des
greffes, 2026-07-07. Détail chiffré : `docs/DESIGN_ASSIMILATION.md`. Version publiée itch : **1.12.0**.
Détail dans `docs/PROJECT_STATE.md`.

## Équipe d'agents

Agents dans `.claude/agents/` : `game-designer`, `directeur-artistique`, `graphiste`, `developpeur`,
`musicien`, `story-teller`, `marketing`, `game-tester`, `release-manager`. Déléguer proactivement à
l'agent compétent (ordre de lancement : `GUIDE-CLAUDE-CODE.md`).

- **`game-tester`** : lance Godot (`--rendering-driver d3d12`), joue le jeu, documente les bugs dans `docs/TEST_REPORT.md`. À invoquer après chaque implémentation majeure.
- **`release-manager`** : publie la release binaire de bout en bout (bump semver, release notes, `tools/release_itch.ps1`, MAJ doc) puis **rédige le devlog** (titre + corps à coller) — l'utilisateur le publie lui-même sur itch (l'agent ne pilote pas le navigateur). Source des notes : `docs/DEVLOG.md`.

## Maintenance de la doc

- `README.md` (racine) — MAJ à chaque changement de phase / ajout majeur (tableau des phases, roadmap, captures).
- `docs/PROJECT_STATE.md` + `docs/GDD.md` + `/carte-projet` + `docs/PITFALLS.md` — MAJ dans le commit qui change ce qu'ils décrivent.

## Conventions

- Plateforme cible : Windows (.exe). Moteur : **Godot 4.7 .NET** (toujours la variante `.NET`, jamais la standard). Langage : C# (.NET 8), GodotSharp.
- Build Windows : `"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe" --headless --export-release "Windows Desktop" "./build/ChimeraProtocol.exe"`
- **CRITIQUE export .NET** : `ChimeraProtocol.sln` DOIT être présent à la racine (sinon le .exe crashe au lancement). Recréer : `dotnet new sln --name ChimeraProtocol --format sln && dotnet sln ChimeraProtocol.sln add ChimeraProtocol.csproj`. L'export produit `build/ChimeraProtocol.exe` + `build/data_ChimeraProtocol_windows_x86_64/` (runtime .NET 8, ignoré par git, régénéré).
- **Publication & MAJ auto (itch.io + Butler)** : incrémenter `config/version` dans `project.godot`, puis skill **`/publier-itch`** (ou `tools/release_itch.ps1 -Version X.Y.Z`), ou déléguer à l'agent **`release-manager`** (pipeline complet + devlog itch). Runbook : `docs/RELEASE.md`. Notes de version cumulées : `docs/DEVLOG.md`. Un push = auto-update pour les joueurs de l'app itch.
- Style de code : PascalCase classes/méthodes, `_camelCase` champs privés, `readonly` par défaut.
- Architecture : `src/` (logique C#) / `scenes/` (.tscn) / `assets/` (raw) / `data/` (JSON tuning modifiable sans recompiler).
- **Logique pure testable** : `src/Core/Rules/` (classes statiques sans dépendance Godot — `XpCurve`, `EnemyScaling`, `EliteAffixTable`…). Les nœuds y délèguent (SRP).
- **Tests unitaires** : xUnit, `dotnet test tests/ChimeraProtocol.Tests.csproj`. **119 tests**.
- Singletons (AutoLoad) : `GameManager`, `XpSystem`, `InventorySystem`, `LevelUpSystem`, `SaveManager`, `MetaProgressionSystem`, `AudioSystem`, `FusionFlash`, `ScreenShake`, `GameSettings`, `DiscordPresence` (Rich Presence), `VersionStamp` (tampon `v<ver>-<sha>` bas-droite).
- Sauvegarde : `user://save.json` (méta/Échos) + `user://settings.cfg` (préférences, high scores, complétions, armes découvertes).
- Sprites : PNG transparent, grille 32×32 px (Colosse 48×48 — exception), `texture_filter = Nearest` global. Style **pseudo-3D avec ombres** (`docs/ART_BRIEF_PSEUDO3D.md`) via `tools/pseudo3d_lib.py` — toujours dériver shadow/highlight avec `shade()`/`shade_sprite()`/`shade_tile()`/`shade_icon()`, jamais des couleurs plates ad hoc.
- Audio : OGG musique (fallback WAV), WAV ou OGG SFX. Sources CC0 dans `assets/audio/CREDITS.md`.
- Localisation EN/FR/ES : `localization/ui.csv` → `Loc.T("CLÉ")`.
- Performance cible : 200–300 entités simultanées ; I-frames joueur 0.45 s (CRITIQUE pour les nuées).
- Palette UI : fond `#1A1A2E`, cyan `#44FFEE`, violet `#AA44FF`, or `#FFCC44`, blanc cassé `#D9D9F2`. Police : Share Tech Mono (AA on, `ui_theme.tres`, size 16) ; VT323 en réserve (AA off).
- Python : `C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe` (pas dans le PATH).

## Pièges critiques → `docs/PITFALLS.md`

Tous les pièges non-évidents (API Godot C# manquante, callbacks/threading, checklists de câblage
armes/ennemis, affixes d'élite, VFX parentés à la racine, navigation clavier/manette, StyleBox/focus,
cycle de vie des scènes, assets `.import`, tests headless) sont dans **`docs/PITFALLS.md`**.
**Le consulter avant de coder dans le domaine concerné.**
