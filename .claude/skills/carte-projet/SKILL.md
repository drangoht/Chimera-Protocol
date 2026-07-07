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
tests/             xUnit — ChimeraProtocol.Tests.csproj (119 tests). `dotnet test tests/...`
docs/              GDD.md + briefs/plans — voir §Docs
```

## Singletons AutoLoad (project.godot)
`GameManager` · `XpSystem` · `InventorySystem` · `LevelUpSystem` · **`AssimilationSystem`** ·
`SaveManager` · `MetaProgressionSystem` · `AudioSystem` · `ScreenShake` · `GameSettings` ·
`DiscordPresence` · `VersionStamp` · `FusionFlash` (scène). Accès partout via `NomSystem.Instance`.

## §Rules — `src/Core/Rules/` (logique pure, testée)
XpCurve · EnemyScaling (`Scaled` linéaire + `ScaledCurved`/`CurvedFactor` = courbe non-linéaire :
early grace + accélération late, cf. difficulté) · SpawnCurve · WeaponLeveling · StatCaps · WeightedPicker ·
EchoFormula · RarityWeights · CrowdControlCaps · DifficultyTuning · **VersionCompare**
(comparaison sémantique pour le bandeau de MAJ) · **EliteAffixTable** (affixes d'élite :
fréquence + tirage + `EliteModifiers`, cf. GDD §22) · **GraftTable** (Assimilation : parse
`grafts.json`, routage kill→jauge `RouteKill`, seuils `EffectiveThreshold`/`DeclinedThreshold`,
`SlotCount` ; cf. docs/DESIGN_ASSIMILATION.md §11-18). Les nœuds délèguent ici (SRP).

## §Systems — `src/Systems/`
- Spawn : `EnemySpawner` (+ `EnemySpawnData`), `PowerUpSpawner` (+ `PowerUp`), `MagnetSpawner`, `AetherCoreSpawner`
- Progression : `XpSystem`, `LevelUpSystem` (+ `LevelUpCardData`), `InventorySystem`, `MetaProgressionSystem` (+ `MetaUpgradeDefinition`)
- **Assimilation (greffes)** : `AssimilationSystem` (autoload — jauges par archétype, slots équipés, pause/reprise de jauge, émet `GaugeFilled` ; délègue les chiffres à `GraftTable`) ; effets côté Player → `GraftManager` (§Entities) ; écran → `AssimilationScreen` (§UI). Data → `grafts.json`. Meta : `graft_slots`/`graft_metabolism`. Action d'entrée `dash` (InputRemap). Cf. docs/DESIGN_ASSIMILATION.md.
- Biome/arène : `BiomeAtmosphere`, `BiomeObstacles`, `FloorFeatures`, `GroundRenderer`, `DeepMotifShape`, `VignetteFollow`
- Divers : `AudioSystem`, `GameSettings` (audio/affichage/diff/langue + **touches move_* rebindables**), `Loc`, `FusionFlash`, `ScreenShake`, `RunStatsTracker`
- Input : **`InputRemap`** (statique) — actions `move_up/down/left/right` (défaut ZQSD + flèches + manette), séparées des `ui_*` menu ; le Player lit `Input.GetVector(move_*)`, remap via l'écran Options, persisté dans `GameSettings`. Action **`dash`** (Maj gauche / RB, `EnsureExtraActions()` au boot via GameManager) pour la greffe Servos Erratiques
- Intégrations : **`DiscordPresence`** (autoload, NuGet `DiscordRichPresence` — statut « joue à Chimera Protocol », clés art `chimera`/`chimera_small`, tolérant à l'absence de Discord ; `SetInMenus`/`SetInRun` appelés par MainMenu/GameManager), **`VersionStamp`** (autoload, overlay `v<ver>-<sha>` bas-droite) ; **`BuildInfo`** (`src/Core/`, `GitSha` auto-généré par `tools/gen_build_info.ps1`, version lue de project.godot)

## §UI — `src/UI/` (écrans Control)
`MainMenu` (+ **bandeau MAJ** → §MAJ) · `CharacterSelectScreen` · `LevelSelectScreen` ·
`HubScreen` · `BestiaryScreen` / `ArsenalScreen` / `CodexScreenBase` (+ `Codex`) ·
`OptionsScreen` · `PauseScreen` · `LevelUpScreen` · **`AssimilationScreen`** (écran modal des
greffes, UI construite en code) · **`ChimeraCodexScreen`** (codex explicatif des greffes/fusions —
sous-classe `CodexScreenBase`, entrées dérivées de `AssimilationSystem.Config` ; accessible depuis le
bouton « Chimère » du MainMenu ; `CodexScreenBase.IntroText` = paragraphe d'intro optionnel) ·
**`ModalQueue`** (statique — coordonne LevelUpScreen +
AssimilationScreen : un SEUL `Paused`, level-up prioritaire ; jamais affichés simultanément) ·
`RunEndScreen` · `IntroScreen`
(cinématique) · `HUD` (+ rangée d'emplacements de greffe sous la barre XP) · `BuffBar` · `Banner` · `BiomeCatalog` · `Characters` (registre
des 4 persos jouables : chimera/impulse_cannon, titan/drone_swarm, vagabond/plasma_blade,
vecteur/vector_lance ; `CharacterSelectScreen` en fait les cartes).

## §Weapons — `src/Weapons/`
Base : `WeaponBase` (⚠ `base._Ready()` EN DERNIER). 12 armes actives + 9 fusions.
`VectorLance` = arme DIRIGÉE (tire vers `Player.AimDirection` = **souris** ou **stick droit**, pas l'ennemi le plus proche ; réticule `Player._aimIndicator`) ;
sa fusion `VectorBeam` (+ servo_motors) = rayon perforant CONTINU dirigé (`continuous_beam`).
`FrostVeil` (cryo_lance + reinforced_plating) = aura de givre CONTINUE (dégâts + slow radial).
Fusions : `FusionBlade`, `RailOvercharged`, `OrbitalSwarm`, `OverloadAegis`,
`IonicStorm`, `SolarColumn`, `HornetSwarm`. Projectiles : `Bullet`, `GlaiveProjectile`,
`SeekerMissile`, `DroneEntity`, etc.

## §Entities — `src/Entities/`
- Player : `Player` (+ `PlayerStats`, + **`GraftManager`** : applique les effets de greffe — stat mods avec retrait exact, mini-essaims orbitants/tourelle/thorns/onde en `_Process`, teinte additive `SelfModulate`, + **props de silhouette** Phase B `BuildPropFor`/`UpdateProps`/`Shade` : carapace/servos/œil/onde/proue de charge/cœur de ruche ancrés au corps, miroir via `Player.FacingLeft` ; le dash vit dans `Player` : `EnableDash`/`DisableDash`, `GraftSpeedMultiplier`, `HealFlat`, `SetGraftTint`, `FacingLeft`, `IsDashing`)
- Enemies : `EnemyBase` (data-driven, `SetSpriteFrames`, **`ApplyElite`** — affixes d'élite), `EliteAura` (halo VFX), `EnemyBullet`, `CorruptedDrone`, `CorruptedSentinel`, `RustSwarm`, `RustStalker`
- MiniBoss : `AetherRevenant`, `MasterSentinel` · Boss : `GraftedColossus` (48×48, `Die()` custom)
- Environment : `AetherCore`, `RustedCore`, `AetherGeyser`, `HpOrb`, `XpOrb`, `MagnetPickup`, `PowerUpPickup`

## §Data — `data/*.json` (tuning sans recompiler)
`weapons.json` (5 niveaux/arme) · `enemies.json` + `enemies_biome_expansion.json` ·
`levelup_config.json` (rarityByCard) · `meta_upgrades.json` (hub, 19 items — inclut `graft_slots`/`graft_metabolism`) ·
**`grafts.json`** (Assimilation : slots/gauges/grafts, cf. GraftTable) · `texts.json`.

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
- **Greffe (Assimilation)** : `grafts.json` (entrée `grafts[]` : gauge 1:1, sourceAiType, rarity, tint, effects/statMods) · effet appliqué dans `GraftManager` (Setup*/Update* + retrait via RebuildBehaviors/ReverseStatMods ; stat sur `PlayerStats` avec delta réversible et hardcaps `StatCaps`) · clés loc `GRAFT_<ID>_NAME/_DESC` EN/FR/ES (fallback FR du json via `TFallback`) · icône `assets/sprites/grafts/<id>_icon.png` (optionnelle, fallback carré teinté). Routage kill→jauge = pur (`GraftTable.RouteKill`, testé). Nouveau comportement moteur (dash-like lisant l'entrée) = côté `Player`, pas GraftManager.
- **Personnage jouable** (5 pts) : `Characters.All` (id, stats, `StartingWeaponId`, `Tint`, `FramesPath`) · sprite dédié via `tools/generate_character_sprites.py <id>` (+ `.tres` + import Godot) · clés `CHAR_<ID>_NAME/TAG/DESC` EN/FR/ES (l'écran lit les clés, pas les champs C#) · `GameSettings.SignatureWeapons` si l'arme de base doit être « découverte » d'office · aucune méca moteur (le pipeline `GameManager`/`InventorySystem` gère toute arme de départ).

## Commandes utiles
- Build .exe : `"…/Godot_v4.7…mono.exe" --headless --export-release "Windows Desktop" "./build/ChimeraProtocol.exe"` (⚠ `ChimeraProtocol.sln` requis à la racine)
- Tests : `dotnet test tests/ChimeraProtocol.Tests.csproj`
- Compil rapide C# : `dotnet build ChimeraProtocol.csproj`
- Forcer un biome (tests/captures) : flag `--biome=<id>`
- Forcer tous les ennemis basiques en élite (test des affixes) : flag `--force-elites` (`DebugHooks.ForceElites`)
- Forcer l'équipement d'une (ou des trois) fusion(s) de greffes sans grinder les jauges : flag `--force-fusion=<id|all>` (`DebugHooks.ForcedFusion`, équipe d'abord les 2 greffes prérequises). 3 fusions : `fusion_charge_blindee`, `fusion_ruche_tourelles`, `fusion_nova_rodeur` (Frappe Nova = dash-blink + nova ; partage `erratic_servos` avec Charge Blindée → exclusives)
- Forcer l'équipement d'une (ou des 5) greffe(s) de base pour valider les props de silhouette : flag `--force-graft=<id|all>` (`DebugHooks.ForcedGraft`) ; capture par PID via `tools/capture_graft_silhouette.py`
