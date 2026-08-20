---
name: carte-projet
description: Carte/index de Chimera Protocol (Unity 6.5 / C#). À invoquer AVANT toute exploration du code pour localiser systèmes, écrans, armes, ennemis, données de tuning, assets et outils sans repartir de zéro avec Glob/Grep. Contient aussi les checklists de câblage, les flags de banc et les points d'entrée.
---

# Carte du projet — Chimera Protocol

Survivor roguelite vue du dessus, **Unity 6.5** (C# / URP 2D). Le moteur Godot, sur lequel le jeu
a été écrit jusqu'à la 1.26.0, a été **retiré du dépôt le 2026-08-10** : ses documents survivent
sous `docs/archive-godot/`, son code non.

Cette carte dit **où** se trouve chaque chose. Pour le reste :
`docs/GDD.md` (**pourquoi** le jeu est réglé ainsi) · `docs/PITFALLS_UNITY.md` (**quels pièges**
guettent) · `docs/UNITY_MIGRATION_PLAN.md` (**comment** le portage est architecturé) ·
`CLAUDE.md` (phase courante, conventions).

> **Maintenir cette carte à jour** : dès que tu ajoutes / supprimes / renommes un système, un écran,
> une arme, un ennemi, un fichier `StreamingAssets/data/*.json` ou un outil `tools/`, mets à jour la
> section concernée **dans le même commit**. Une carte périmée est pire qu'absente. En cas de doute,
> vérifie le fichier avant de l'affirmer — ne recopie pas aveuglément.

## Arborescence

```
unity/
  Assets/
    Scripts/Shared/Rules/       Logique PURE testable (aucune dépendance moteur) — voir §Rules
    Scripts/Shared/PlatformCore/ Socle pur : Pcg32, TimerWheel, Easing, TweenTimeline, DeferredQueue
    Scripts/Platform/           Pont moteur : Spawner, AudioSystem, Loc, UiFrames, UserData… — voir §Platform
    Scripts/Gameplay/           Entités, armes, spawn, HUD, télémétrie — voir §Gameplay
    Scripts/UI/                 Écrans (Canvas) — voir §UI
    Scripts/Bench/              Banc headless : auto-play, smoke tests, tour de captures
    Editor/                     Scripts d'éditeur : build, SpriteFrames, import des assets
    Editor/spriteframes/        Manifestes JSON d'animation (écrits par tools/, lus par BuildSpriteFrames)
    WebGLTemplates/ChimeraMobile/ Page hôte du build web — ⚠ la MOITIÉ du portage tactile vit ici
                                (zoom, défilement, geste de retour, barre d'URL, devicePixelRatio)
    Art/sprites/                Sources importées par GUID (ennemis, joueur, armes, ramassages, décor)
    Art/branding/               icon.png — icône de l'exécutable (désignée par ProjectSettings)
    Resources/                  Chargé PAR CHEMIN à l'exécution : Ui, UiFrames, Vfx, Environment,
                                Audio/{music,sfx}, Fonts, Shaders, SpriteFrames, Prefabs
    StreamingAssets/data/       JSON de tuning — voir §Data
    StreamingAssets/localization/ ui.csv (source unique) ; clé via Loc.T("CLÉ")
  ProjectSettings/              bundleVersion (posée par le script de release), icônes, URP
tests/                          xUnit — compile Shared/ par chemin (673 tests). `dotnet test tests/…`
tools/                          Générateurs d'assets, banc, release — voir §Outils
docs/                           GDD.md + briefs/plans — voir §Docs
docs/archive-godot/             Documents de l'ère Godot (périmés sur les chemins, valides sur le fond)
```

⚠ **`Art/` et `Resources/` ne se valent pas.** `Art/` est consommé par **référence de GUID** (les
`SpriteFramesAsset` des ennemis et du joueur) ; `Resources/` est chargé **par chemin**
(`Resources.Load<Sprite>("Ui/…")`) et embarqué **en entier** dans le binaire. Écrire dans le mauvais
des deux ne lève aucune erreur : le générateur annonce « écrit », le jeu affiche l'ancienne image.
La table de destination fait autorité : **`tools/unity_paths.py`**.

## §Rules — `unity/Assets/Scripts/Shared/Rules/` (logique pure, testée)
`ArenaLayout` · `AutoPilotPolicy` · `BiomeUnlock` · **`BoomerangReturn`** (retour du glaive : sa
vitesse se calcule *contre* celle du lanceur, jumelle de `PickupMagnet`) · `BossIncarnations` ·
`BossPhases` ·
`CardRarityTable` · `ChallengeTable` · `CrowdControlCaps` · `DifficultyTuning` · `EchoFormula` ·
`EliteAffixTable` · `EnemyScaling` · `EnemyTable` · `FloorFeatureLayout` · `GodotConfig` (migration
des `settings.cfg` de joueurs venus de la 1.26.0) · `GraftTable` · `LevelThreat` · `LevelUpCharges` ·
`LevelUpPool` (⚠ une main est **toujours pleine** : le manque est comblé par les cartes de surcharge,
carte par carte) · `LocTable` · **`MagnetSchedule`** (fenêtres d'apparition de l'Aimant + `bonus_magnet`) ·
`MetaUpgradeTable` · `MusicIntensity` · `OverloadCards` ·
`OvertimeEscalation` · `PassiveScaling` · `PassiveTable` · **`PickupMagnet`** (aimantation des orbes
**et des Noyaux d'Aether** : sa vitesse se calcule *contre* celle du porteur, jamais dans l'absolu ;
`AttractRadius` porte le rayon du Noyau, que `core_magnetism` élargit de 100 à 150 px) ·
**`LaunchQuery`** (chaîne de requête d'URL → arguments de ligne de commande : `?biome=neon&invuln`
vaut `--biome=neon --invuln`, ce qui rend **tous les drapeaux utilisables dans un navigateur**) ·
**`TouchZones`** (decoupage de l'ecran tactile : zone du stick, boutons, portrait, et le
portrait) · **`VirtualStick`** (geometrie du joystick
flottant : origine posee au contact, dosage, **recentrage**) ·
`PressureMeter` · `RarityWeights` ·
`RegenReserve` · **`RustTide`** (la Marée de Rouille : l'arène se referme en overtime — fraction sûre,
profondeur d'enfoncement, taux de rongement en **fraction des PV max**, et la *submersion* qui garantit
qu'aucun point ne reste sûr passé `CloseMinutes` = 11 min ; rendu et application →
`Gameplay/RustTideZone`) · `SaturationTable` · `SaveData` / `SaveMigration` / `SettingsData`
(⚠ **deux** tables de records : `HighScores` par biome — historique, intacte — et `SurvivalRecords`
par biome **et cran**) · `SpawnCurve` ·
`StartingPerks` · `StatCaps` · `Titles` · `VersionCompare` · `WeaponFusion` · `WeaponLeveling` ·
`WeaponSfx` · `WeaponTable` · `WeightedPicker` · `XpCurve`

## §Platform — `unity/Assets/Scripts/Platform/`
`Spawner` (charge un prefab par son ancien chemin `res://scenes/…` traduit en `Prefabs/…`) ·
`AudioSystem` · `MusicDirector` (dans Gameplay) · `Loc` · `UserData` (sauvegardes) ·
**`StreamingText`** (lecture de `StreamingAssets` — **le seul fichier qui sait que le web n'a pas de
disque** : en WebGL ce chemin est une URL, donc tout est préchargé une fois par la scène `Boot`) ·
**`LaunchArgs`** (drapeaux de lancement — ligne de commande sur Windows, **chaîne de requête de
l'URL** en web ; point d'accès unique, cf. `Rules/LaunchQuery`) ·
`UiCanvas` / `UiFonts` / `UiFrames` / `UiIcons` / `UiNames` / `UiPrimitives` · `SceneRoot` ·
`PlatformHost` · `FrameAnimator` · `GTween` · `HitStop` · `DebugHooks` ·
`BuildInfo` (tampon `v<version>-<sha>`) · `DiscordPresence` · `DataFiles` · `Gd` (utilitaires
transposés) · `SpriteFramesAsset`

**Entrées — les TROIS seuls fichiers qui touchent aux périphériques** (paquet Input System depuis
le 2026-08-13) : `InputRemap` (actions de jeu remappables — déplacement, dash), `RawInput` (Échap,
« n'importe quelle touche », clic, curseur, stick droit, **demande de pause**) et **`TouchInput`**
(dalle tactile, depuis le 2026-08-14). Tout le reste passe par eux ; un écran qui lit une touche
directement est un défaut — `Keyboard.current` peut être **nul**, et l'exception qui s'ensuit saute
la fin de la méthode appelante sans rien signaler (→ `docs/PITFALLS_UNITY.md` §Entrées).
⚠ **Le tactile est à part parce qu'il a une MÉMOIRE** : un joystick flottant n'existe que par
l'endroit où le doigt s'est posé, là où clavier et manette se lisent sans état. `TouchInput` porte
cette machine à états et rien d'autre — la géométrie vit dans `Rules/VirtualStick` et
`Rules/TouchZones`. Son pompage est installé en `BeforeSceneLoad` sur un objet `DontDestroyOnLoad` :
un invariant porté par un écran, un tiers peut l'annuler (→ §Tactile).

## §Gameplay — `unity/Assets/Scripts/Gameplay/`
- **Joueur** : `Player`, `PlayerStats`, `ChimeraBody`, `RunCamera`, `Assimilation`, `GraftManager`
- **Ennemis** : `EnemyBase`, `EnemyAi`, `EnemySpawner`, `EnemyBullet`, `EnemyStatusFx`,
  `MiniBoss`, `RustedCore` (boss de fin), `MoltenColossus` / `CryoSentinel` / `NeonWarden` (mid-boss)
- **Armes** : `WeaponBase`, `WeaponRegistry`, `ImpulseCannon`, `ScatterVolley`, `TeslaCoil`,
  `PlasmaBlade`, `CryoLance`, `VectorLance`, `PyreStream`, `Singularity`, `OverloadField`,
  `SeekerMissile`, `SeekerSwarm`, `DroneSwarm`, `GraftTurret`, `Glaive` · fusions dans
  `Gameplay/Fusions/` (9)
- **Run** : `GameManager`, `RunBootstrap`, `RunConfig`, `XpSystem`, `XpOrb`, `InventorySystem`,
  `MetaProgression`, `ChallengeSystem`, `GameSettings`, `AetherCore*`
  (les paramètres **et** le calcul des Échos vivent dans `Rules/MetaUpgradeTable.EchoParams` —
  `Gameplay/EchoSettings.cs` en était un doublon, supprimé le 2026-08-11)
- **Ramassages** : `XpOrb`, `AetherCore` (aimanté depuis le 2026-08-12),
  **`MagnetPickup` / `MagnetSpawner` / `MagnetSprite`** — l'Aimant, porté le 2026-08-12 : il
  n'existait pas sous Unity alors que `bonus_magnet` restait achetable au Hub à 770 Échos. Silhouette
  dessinée à l'exécution, **aucun prefab**. Seul ramassage du jeu qui ne s'aimante pas.
- **Arène** : `ArenaRenderer`, `ArenaObstacles`, `FloorFeatures`, `BiomeAtmosphere`, **`RustTideZone`**
  (la marée d'overtime — ⚠ elle n'est **pas** posée dans `Game.unity` : `ArenaRenderer.Build` l'ajoute,
  comme `BiomeAtmosphere`, ce qui la fait exister dans toute run — jeu, banc, capture)
- **Mesure** : `PowerTelemetry`, `BossTelemetry`, `BenchAutoPilot`
- **VFX** : `Gameplay/Vfx/` (16 fichiers : `Vfx`, `VfxPrimitives`, `ChampionOverlay`, `ScreenShake`…),
  dont **`FusionFanfare`** — les trois ondes + le ralenti de la forge d'une fusion, déclenchés par
  `RunHud` sur `InventorySystem.FusionApplied` (l'inventaire n'appelle aucun écran : `Gameplay` ne
  référence pas `UI`) — et **`FusionMark`**, la signature **dorée** posée au tir de toute arme
  fusionnée. ⚠ Elle est appelée depuis **`WeaponBase`**, au même point que le son de tir : une marque
  écrite fusion par fusion ne se porterait jamais en entier (cf. les 14 armes muettes).

## §UI — `unity/Assets/Scripts/UI/`
**`BootScreen`** (⚠ **première scène du build**, `GameScenes.Boot` : elle charge les données avant que
quoi que ce soit puisse les lire — indispensable en web, où `StreamingAssets` est une URL. Elle
**n'affiche aucun texte traduit** : la table de traduction est ce qu'elle attend) ·
`MainMenuScreen` · `IntroScreen` · `LevelSelectScreen` · `HubScreen` ·
`CodexScreen` · `ChallengeScreen` · `AssimilationScreen` · `LevelUpScreen` · `PauseScreen` ·
`RunEndScreen` · `OptionsScreen` · `RunHud` (+ `HUD` côté Gameplay) · `UpdateBanner` ·
`GameScenes` · `ModalQueue` · `UiFocusGuard` / `UiFocusPulse` / **`UiRarityFlare`** / `UiVignette` ·
**`TouchHud`** (joystick flottant, bouton d'esquive, bouton de pause — **celui qui montre est
celui qui écoute** : c'est lui qui ouvre `TouchInput.SetGameControls`. Seul canevas du jeu en
`ConstantPixelSize` : ces contrôles se mesurent en pouces, pas en pixels de maquette) ·
**`OrientationGate`** (refus du portrait, hors scènes, `DontDestroyOnLoad`) ·
**`FusionBanner`** (annonce d'une fusion forgée — ordre 90 : au-dessus du HUD, sous les modales ;
aucun `GraphicRaycaster`, il n'intercepte rien) ·
⚠ **Aucun écran de sélection de personnage n'existe côté Unity** — il n'a pas été porté.
⚠ **La rareté d'une carte se joue sur trois signaux, pas un cadre** : cadre (`UiStyle.CardButton`),
aura respirante (`UiRarityFlare`, posée chez le PARENT pour passer sous la carte) et courbe d'arrivée
(`Back` pour l'épique, `Quad` pour le reste). Une **fusion** monte à `legendary` **à l'affichage
seulement** — `RarityWeights` ne connaît que trois crans, et y ajouter le quatrième réglerait par
accident la fréquence de la carte la plus rare du jeu.
**`UiPalette`** (couleurs) et **`UiStyle`** (cadres « plaque blindée ») — jamais de couleur en dur.
⚠ **Un écran de menu ne construit plus son canvas à la main** : `UiStyle.ScreenCanvas(...)` (canvas +
fond + panneau) et `UiStyle.VerticalList(...)` (fenêtre de défilement + colonne) sont les deux
fabriques à utiliser. Les quarante lignes qu'elles remplacent étaient recopiées dans quatre à cinq
écrans, commentaires compris — dont l'avertissement sur la largeur du contenu à remettre à zéro.

## §Data — `unity/Assets/StreamingAssets/data/*.json` (tuning sans recompiler)
`weapons.json` (5 niveaux/arme) · `enemies.json` + `enemies_biome_expansion.json` ·
`levelup_config.json` (rarityByCard) · `meta_upgrades.json` (hub) · **`grafts.json`** (Assimilation :
slots/gauges/grafts/fusions/biomeAffinities) · **`challenges.json`** (défis : condition + récompense)
Ces fichiers portent les **chiffres et les identifiants**. Le **texte affiché** vit ailleurs :
`StreamingAssets/localization/ui.csv` (EN/FR/ES), résolu par `Platform/ContentText.cs` qui déduit la
clé de l'identifiant (`tesla_coil` → `WPN_TESLA_COIL_NAME`). Le champ `name` d'un JSON n'est qu'un
**repli**, en français. (`texts.json` a été supprimé le 2026-08-11 : troisième source de texte,
branchée sur rien.)

⚠ **Une clé déclarée n'est pas une clé lue.** `tools/audit_json_keys.py` compare les clés du JSON
aux littéraux du code : il a trouvé 8 armes qui ne grandissaient que par leurs dégâts.
⚠⚠ **Et il a été aveugle deux fois** (2026-08-13) : comparer aux littéraux *globaux* ne relie une clé
ni à son fichier ni à son consommateur — `"knockbackPx"` lu par `GraftManager` pour une **greffe**
couvrait la clé homonyme des **armes**, et le Champ de Surcharge est resté 5 paliers durant à son
rayon de niveau 1. Le contrôle est désormais **arme par arme** (`Shape()` de sa classe et de ses
bases). **Tout durcissement se valide en réintroduisant le défaut.**
⚠ **Un texte affiché n'est pas un texte traduit.** `tools/audit_loc_keys.py` : tout le contenu nommé
sortait **en français dans les trois langues**, le repli étant silencieux. Il contrôle les deux sens
— clé absente et clé orpheline.

## §Outils — `tools/`
- **Destinations** : **`unity_paths.py`** (où écrit chaque famille d'assets) et **`spriteframes.py`**
  (manifestes d'animation). Tout générateur passe par eux.
- **Sprites** : `pseudo3d_lib.py` (⚠ toujours dériver ombre/highlight via ce lib) ·
  `generate_sprites.py` / `_v2` · `generate_new_enemies.py` · `generate_boss_sprites.py` ·
  `generate_midboss_sprites.py` (`--only=<id>`) · `generate_miniboss_sprites.py` ·
  `generate_character_sprites.py` · `generate_biome_tiles.py` · `generate_arena_obstacles.py` /
  `_extras` · `generate_glass_floor_tile.py` · `generate_backdrop_tile.py` ·
  `generate_vfx_sprites_polish.py` · `generate_splash.py`
- **UI** : `generate_ui_frames.py` + `generate_ui_widgets.py` (cadres, curseurs, interrupteurs →
  `Resources/UiFrames`) · `generate_weapon_icons.py` · `generate_graft_icons.py` ·
  `generate_reward_icons.py` · `generate_hud_assets.py` / `retouch_hud_assets.py` /
  `extract_hud_assets.py` · `gen_lang_flags.py` · `preview_ui_frames.py` · `ui_contact_sheet.py`
- **Icône de l'app** : `generate_app_icon.py` → `unity/Assets/Art/branding/icon.png` (Unity dérive
  lui-même les tailles ; pas de `.ico`). Contrôle visuel : `--sheet`.
- **Audio** : `synth_lib.py` + `synth_instruments.py` + `generate_music_v3.py` produisent la
  bande-son de **secours** (synthétisée, sans contrainte de licence). La musique **en jeu** vient de
  Suno : déposer dans `music_ai/` puis **`import_ai_music.py`** (`--only <id>`, `--keep-preview`) →
  `Resources/Audio/music`. Prompts : `docs/AUDIO_AI_PROMPTS.md`. Contrôle : `analyze_music.py`.
  SFX Kenney CC0 : `integrate_kenney_audio.py`.
- **Audits** (nés du portage — « déclaré n'est pas consommé ») : **`audit_json_keys.py`** (clés de
  données jamais lues, et « fantômes » lus mais absents) · **`audit_unused_members.py`** (membres C#
  déclarés que rien n'appelle).
- **Banc** : **`power_curve_multi.py`** (campagne de N runs : `--runs` / `--biome` / `--minutes` /
  `--seed-base` / `--saturate` / `--overtime` / `--out` / `--compare` / `--report-only`) ·
  **`power_loop.py`** (boucle de puissance : niveaux/min, pente des PV max, tests de signes).
- **Captures** : `capture_store.py` (lance le binaire Unity), `window_capture.py`.
- **Trailer** : `build_trailer.py` (montage) + `trailer_sheets.py` (planches-contact).
  ⚠ La **capture** des rushes n'existe plus : elle passait par le Movie Maker de Godot.
- **Release** : **`release_unity.ps1`** — workflow complet via le skill **`/publier-itch`**
  (`-Target web` → canal `html5` ; une release web ne touche pas `version.json`, qui décrit la
  version *téléchargeable*).
- **Servir le build web en local** : **`serve_web.py`** (port 8080). ⚠ Ne PAS utiliser
  `python -m http.server` : sans `Cache-Control`, le navigateur mélange deux builds et rend un crash
  wasm illisible (→ `PITFALLS_UNITY.md` §Web).
- Python : `C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe`

## §Docs — `docs/`
`GDD.md` (référence design) · `PROJECT_STATE.md` (état) · `PITFALLS_UNITY.md` (pièges) ·
`UNITY_MIGRATION_PLAN.md` (architecture du portage) · `TEST_REPORT.md` (mesures & bugs) ·
`DEVLOG.md` · `RELEASE.md` · **`AUDIO_AI_PROMPTS.md`** (direction sonore en vigueur) ·
`ART_BRIEF_AUDIO.md` / `_PSEUDO3D` / `_UI_FRAMES` · `STYLE_GUIDE.md` · `NARRATIVE.md` /
`lore-bible.md` · `DESIGN_ASSIMILATION.md` / `DESIGN_CHALLENGES.md` · `ENDGAME_PLAN.md` ·
pages store itch · `archive-godot/` (ère Godot).

## §MAJ — Bandeau « nouvelle version » (joueurs web)
- Manifeste : `version.json` (racine) = `{version, url}`, poussé sur GitHub par `release_unity.ps1`.
- `UpdateBanner` compare `Application.version` au manifeste lu sur `raw.githubusercontent` via
  `VersionCompare.IsNewer`. Clés loc `UPDATE_AVAILABLE` / `UPDATE_DOWNLOAD`.

## Checklists de câblage (résumé — détail + pièges dans `docs/PITFALLS_UNITY.md`)
> **Avant de coder** dans un domaine (armes, ennemis, UI, VFX, assets), lire `docs/PITFALLS_UNITY.md`.
- **Arme** : `weapons.json` · `levelup_config.json` · `InventorySystem` · `WeaponRegistry` ·
  `LevelUpPool` (ids) · Codex · **`WeaponSfx`** (une arme absente de cette table est MUETTE) ·
  icône dans `Resources/Ui` · clés `WPN_*` EN/FR/ES.
- **Ennemi basique** (variante d'archétype, pas de prefab) : `enemies.json`
  (`ai.type` ∈ straight_chase / erratic_chase / ranged_kiter / slow_hunter) · Codex ·
  clés `ENEMY_*` · sprites dans `Art/sprites/enemies/<id>/` + manifeste
  `Editor/spriteframes/<id>.json` + **reconstruction des SpriteFrames dans l'éditeur**.
- **Greffe** : `grafts.json` · effet dans `GraftManager` (avec retrait réversible) ·
  clés `GRAFT_<ID>_NAME/_DESC` · icône `Resources/Ui/<id>_icon.png`.
- **Incarnation de boss** : `BossIncarnations.All` · branche dans `RustedCore.FireSignature` ·
  clé `BOSS_<ID>_NAME` · palette dans `tools/generate_boss_sprites.py`.
- **Cran de saturation** : `SaturationTable` · clés `SAT_<n>_NAME` / `_RULE` · effet réellement
  branché côté moteur. ⚠ **3 crans sur 6 étaient inopérants** dans le portage : la table existait,
  rien ne la lisait. Vérifier au banc, pas sur la table.

## Commandes utiles
- **Tests** : `dotnet test tests/ChimeraProtocol.Tests.csproj` (759 tests, aucun moteur requis)
- **Build du jeu** :
  `Unity.exe -batchmode -quit -projectPath unity -executeMethod BuildBench.Windows64Game`
  (autres cibles : `Windows64PlatformSmoke`, `Windows64RunSmoke`, `Windows64Il2cpp`)
- **Build web** : `… -buildTarget WebGL -executeMethod BuildBench.WebGame` → `unity/Build/web/`.
  ⚠ Le **premier** build d'une plateforme réimporte tous les assets (~20 min, l'audio surtout).
  Réglages posés par le script, pas à la main (mémoire, Brotli + repli, stripping `Low`).
- ⚠⚠ **Un crash `memory access out of bounds` au démarrage en web ?** Avant de chercher dans le code :
  **servir sur un autre port**. Les fichiers de sortie portent toujours le même nom, et le cache HTTP
  du navigateur peut associer le `.data` d'un build au `.wasm` d'un autre (→ `PITFALLS_UNITY.md` §Web).
  Un jeton `__BUILD_ID__` posé par le build l'évite désormais ; s'il disparaît du gabarit, le build
  l'annonce dans son journal.
- **Regarder l'interface à la taille d'un téléphone** : servir `unity/Build/web/` en local
  (`python -m http.server`) et l'ouvrir dans un **iframe de 800 × 360** avec `?touch` — redimensionner
  la fenêtre du navigateur ne change pas ce que le jeu voit. ⚠ **Six défauts de mise en page sur six
  ont été trouvés là, aucun au code ni par un test.**
- **Publier** : `tools/release_unity.ps1 -Version X.Y.Z -DryRun` puis sans `-DryRun`
  (web : `-Target web`, qui pousse sur le canal **`html5`** — ⚠ c'est le NOM du canal qui décide,
  côté itch.io, si le jeu se lance dans le navigateur ou se télécharge)
- **Flags du jeu** (`DebugHooks`) : `--auto-play` · `--power-curve` · `--touch` · `--biome=<id>` ·
  `--timescale=<x>` (≤ 4) · `--run-limit=<s>` · `--seed=<n>` · `--start-at=<min>` ·
  `--saturate-arsenal` · `--saturation=<n>` · `--force-elites` · `--invuln` · `--lang=<en|fr|es>`
  ⚠ **Non portés depuis Godot** : `--debug-boss`, `--debug-enemy`, `--force-graft`,
  `--force-fusion`, `--trailer`. Les documents d'archive les mentionnent encore.
- **Trancher un RÉGLAGE** : `py tools/power_curve_multi.py --runs 5 --overtime --out avant.json`,
  modifier la valeur, relancer avec `--compare avant.json`. **Une run isolée ne tranche rien**
  (variance ×2,4 mesurée). Lire « temps soutenable » et le **test des signes**, pas la survie du bot.
- Journal du banc : `%USERPROFILE%\AppData\LocalLow\drangoht\Chimera Protocol\power_curve.log`
