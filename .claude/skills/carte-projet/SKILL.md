---
name: carte-projet
description: Carte/index de Chimera Protocol (Godot 4.7 .NET / C#). À invoquer AVANT toute exploration du code pour localiser systèmes, écrans, armes, ennemis, données de tuning, singletons et outils sans repartir de zéro avec Glob/Grep. Contient aussi les checklists de câblage et les points d'entrée.
---

# Carte du projet — Chimera Protocol

Survivor roguelite vue du dessus, Godot 4.7 **.NET** (C# / .NET 8 / GodotSharp).
Détail design → `docs/GDD.md`. Mémoire stable → `CLAUDE.md` (racine dépôt).

> **Maintenir cette carte à jour** : dès que tu ajoutes / supprimes / renommes un
> système, un écran, une arme, un ennemi, un fichier `data/*.json`, un singleton
> AutoLoad ou un outil `tools/`, mets à jour la section concernée ci-dessous **dans
> le même commit**. Une carte périmée est pire qu'absente. En cas de doute sur un
> détail, vérifie le fichier avant de l'affirmer — ne recopie pas aveuglément.

## Arborescence

```
src/
  Core/            GameManager.cs, SaveManager.cs
  Core/Rules/      Logique PURE testable (aucune dépendance Godot) — voir §Rules
  Systems/         Singletons + systèmes runtime (spawn, audio, biome…) — voir §Systems
  UI/              Écrans & HUD (Control) — voir §UI
  Weapons/         Armes, projectiles, fusions — voir §Weapons
  Entities/        Player, Enemies, Boss, MiniBoss, Environment — voir §Entities
  VFX/             Effets visuels
scenes/            .tscn (ui/, entités, armes)
data/              JSON de tuning (modifiable sans recompiler) — voir §Data
localization/      ui.csv (source) → ui.{en,fr,es}.translation ; clé via Loc.T("CLÉ")
assets/            Raw (sprites PNG 32×32, audio OGG/WAV, themes)
tools/             Générateurs de sprites/audio + captures + release — voir §Outils
tests/             xUnit — ChimeraProtocol.Tests.csproj (83 tests). `dotnet test tests/...`
docs/              GDD.md + briefs/plans — voir §Docs
```

## Singletons AutoLoad (project.godot)
`GameManager` · `XpSystem` · `InventorySystem` · `LevelUpSystem` · `SaveManager` ·
`MetaProgressionSystem` · `AudioSystem` · `ScreenShake` · `GameSettings` ·
`FusionFlash` (scène). Accès partout via `NomSystem.Instance`.

## §Rules — `src/Core/Rules/` (logique pure, testée)
XpCurve · EnemyScaling · SpawnCurve · WeaponLeveling · StatCaps · WeightedPicker ·
EchoFormula · RarityWeights · CrowdControlCaps · DifficultyTuning · **VersionCompare**
(comparaison sémantique pour le bandeau de MAJ) · **EliteAffixTable** (affixes d'élite :
fréquence + tirage + `EliteModifiers`, cf. GDD §22). Les nœuds délèguent ici (SRP).

## §Systems — `src/Systems/`
- Spawn : `EnemySpawner` (+ `EnemySpawnData`), `PowerUpSpawner` (+ `PowerUp`), `MagnetSpawner`, `AetherCoreSpawner`
- Progression : `XpSystem`, `LevelUpSystem` (+ `LevelUpCardData`), `InventorySystem`, `MetaProgressionSystem` (+ `MetaUpgradeDefinition`)
- Biome/arène : `BiomeAtmosphere`, `BiomeObstacles`, `FloorFeatures`, `GroundRenderer`, `DeepMotifShape`, `VignetteFollow`
- Divers : `AudioSystem`, `GameSettings`, `Loc`, `FusionFlash`, `ScreenShake`, `RunStatsTracker`

## §UI — `src/UI/` (écrans Control)
`MainMenu` (+ **bandeau MAJ** → §MAJ) · `CharacterSelectScreen` · `LevelSelectScreen` ·
`HubScreen` · `BestiaryScreen` / `ArsenalScreen` / `CodexScreenBase` (+ `Codex`) ·
`OptionsScreen` · `PauseScreen` · `LevelUpScreen` · `RunEndScreen` · `IntroScreen`
(cinématique) · `HUD` · `BuffBar` · `Banner` · `BiomeCatalog` · `Characters`.

## §Weapons — `src/Weapons/`
Base : `WeaponBase` (⚠ `base._Ready()` EN DERNIER). 11 armes actives + 7 fusions.
Fusions : `FusionBlade`, `RailOvercharged`, `OrbitalSwarm`, `OverloadAegis`,
`IonicStorm`, `SolarColumn`, `HornetSwarm`. Projectiles : `Bullet`, `GlaiveProjectile`,
`SeekerMissile`, `DroneEntity`, etc.

## §Entities — `src/Entities/`
- Player : `Player` (+ `PlayerStats`)
- Enemies : `EnemyBase` (data-driven, `SetSpriteFrames`, **`ApplyElite`** — affixes d'élite), `EliteAura` (halo VFX), `EnemyBullet`, `CorruptedDrone`, `CorruptedSentinel`, `RustSwarm`, `RustStalker`
- MiniBoss : `AetherRevenant`, `MasterSentinel` · Boss : `GraftedColossus` (48×48, `Die()` custom)
- Environment : `AetherCore`, `RustedCore`, `AetherGeyser`, `HpOrb`, `XpOrb`, `MagnetPickup`, `PowerUpPickup`

## §Data — `data/*.json` (tuning sans recompiler)
`weapons.json` (5 niveaux/arme) · `enemies.json` + `enemies_biome_expansion.json` ·
`levelup_config.json` (rarityByCard) · `meta_upgrades.json` (hub) · `texts.json`.

## §Outils — `tools/`
- Sprites : `pseudo3d_lib.py` (⚠ toujours dériver ombre/highlight via ce lib), `generate_*` (sprites/icônes/tiles/vfx)
- Audio : `generate_music_synth.py`, `generate_audio_v2.py`, `integrate_kenney_audio.py`
- Captures : `screenshot_*.py`, `capture_*.py`, `window_capture.py`
- Tests/équilibrage : `boss_ttk_test.py`, `test_balance_v2.py`, `test_ui_keyboard.py`, `smoketest_exe.py`
- Release : **`release_itch.ps1`** (export → butler push → régénère & push `version.json`) — workflow complet via le skill **`/publier-itch`**
- Python : `C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe`

## §Docs — `docs/`
`GDD.md` (référence design) · `RELEASE.md` · `EXPANSION_PLAN.md` ·
`LEVEL_PROGRESSION_PLAN.md` · `ART_BRIEF_PSEUDO3D.md` · `STYLE_GUIDE.md` ·
`NARRATIVE.md` · `TEST_REPORT.md` (bugs game-tester) · pages store itch.

## §MAJ — Bandeau « nouvelle version » (joueurs web)
- Manifeste : `version.json` (racine) = `{version, url}`, poussé sur GitHub par `release_itch.ps1`.
- `MainMenu.StartUpdateCheck()` : `HttpRequest` vers `raw.githubusercontent.com/drangoht/Chimera-Protocol/main/version.json`, compare via `VersionCompare.IsNewer` à `config/version`, affiche un bandeau + bouton `OS.ShellOpen(url)`. Masqué si `ITCHIO_API_KEY` (app itch = auto-update). Clés loc `UPDATE_AVAILABLE`/`UPDATE_DOWNLOAD`.

## Checklists de câblage (résumé — détail + pièges non-évidents dans `docs/PITFALLS.md`)
> **Avant de coder** dans un domaine (armes, ennemis, UI, VFX, scènes…), lire `docs/PITFALLS.md`.
- **Arme** (8 pts) : `weapons.json` · `levelup_config.json` · `InventorySystem` (paths+stats) · `LevelUpSystem.AllWeaponIds` · `Codex` · icône `ui_icon_*.png`+`.import` · clés `WPN_*` EN/FR/ES.
- **Ennemi basique** (variante d'archétype, PAS de scène) : `enemies.json` (`ai.type` ∈ straight_chase/erratic_chase/ranged_kiter/slow_hunter, `framesPath` optionnel) · `Codex.Enemies` · clés `ENEMY_*` EN/FR/ES · sprite `.tres`/`.png`. Vrai nouveau comportement = scène + sous-classe.

## Commandes utiles
- Build .exe : `"…/Godot_v4.7…mono.exe" --headless --export-release "Windows Desktop" "./build/ChimeraProtocol.exe"` (⚠ `ChimeraProtocol.sln` requis à la racine)
- Tests : `dotnet test tests/ChimeraProtocol.Tests.csproj`
- Compil rapide C# : `dotnet build ChimeraProtocol.csproj`
- Forcer un biome (tests/captures) : flag `--biome=<id>`
- Forcer tous les ennemis basiques en élite (test des affixes) : flag `--force-elites` (`DebugHooks.ForceElites`)
