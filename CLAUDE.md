# CLAUDE.md — Mémoire de projet

Ce fichier est chargé automatiquement par Claude Code au démarrage de chaque session dans ce
dépôt. Il doit rester court et stable ; le détail du design vit dans `docs/GDD.md`.

## Le projet

"Chimera Protocol" — survivor roguelite vue du dessus, univers fantaisie-science-fiction
(humains, cyborgs, robots), inspiré de Vampire Survivors et Everything is Crab.
Référence complète : **`docs/GDD.md`** — toujours le consulter avant toute tâche de design
ou d'implémentation, et le tenir à jour quand une décision est prise.

**Avant d'explorer le code** (localiser un système, écran, arme, ennemi, fichier de données,
outil…), invoquer le skill **`/carte-projet`** plutôt que de repartir de zéro avec Glob/Grep :
il indexe toute l'arborescence + les checklists de câblage. **Le maintenir à jour** dans le même
commit dès qu'un changement structurel modifie ce qu'il décrit (`.claude/skills/carte-projet/SKILL.md`).

## Équipe d'agents

Agents définis dans `.claude/agents/` : `game-designer`, `directeur-artistique`, `graphiste`,
`developpeur`, `musicien`, `story-teller`, `marketing`, `game-tester`. Délègue proactivement
à l'agent compétent — voir `GUIDE-CLAUDE-CODE.md` pour l'ordre de lancement recommandé.

**`game-tester`** : lance Godot (`C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe --rendering-driver d3d12`), joue le jeu, documente les bugs dans `docs/TEST_REPORT.md`. À invoquer après chaque implémentation majeure.

## README GitHub

`README.md` à la racine — mettre à jour à chaque changement de phase ou ajout majeur :
tableau des phases (✅/🔲), roadmap, captures d'écran.

## État du projet

- Pile technique : **Godot 4.7 .NET (C# / .NET 8 / GodotSharp)**
- **Phase actuelle : libre — refonte visuelle pseudo-3D + faune par biome livrée 2026-07-03**.
- **Ce qui est implémenté** :
  - Direction artistique **pseudo-3D avec ombres** (`docs/ART_BRIEF_PSEUDO3D.md`) appliquée à TOUS les sprites via `tools/pseudo3d_lib.py` (lumière fixe haut-gauche 45°, dérivation shadow/highlight HSV, ombre portée elliptique) : 3 persos joueurs, 8 ennemis/mini-boss/boss existants, 20 nouveaux ennemis, obstacles, tuiles de biome, icônes d'armes/UI (640 PNG régénérés, `.import` à jour). Validé game-tester PASS 2026-07-03 (cohérence lumière, lisibilité joueur en nuée).
  - 3 personnages (Chimera, Titan, Vagabond) redessinés, 5 biomes (Sanctuaire, Aether, Fournaise, Givre, Néon)
  - 11 armes actives + 7 fusions + 4 passifs ; power-ups temporaires (4 types)
    (fusions : fusion_blade, rail_overcharged, orbital_swarm, overload_aegis, ionic_storm,
    solar_column, hornet_swarm — chaque évolution = arme de base niv.5 + passif requis, remplace l'arme)
  - Fin de niveau complète : survie sans fin, overtime, boss en boucle, déblocage progressif, high scores (temps+difficulté), arsenal à découverte
  - Hub méta rééquilibré (2026-07-02) : 17 upgrades (7 rééquilibrés + 10 nouveaux ; `starting_weapon_alt` retiré 2026-07-04 car aucun sélecteur d'arme de départ n'est câblé), formule d'Échos plafonnée standard/overtime (`EchoFormula.Calculate`, caps + `overtimeDampening`/`overtimeBonusCap`), 5e composante "Bonus de Surcharge" sur `RunEndScreen`, `UpgradesList` scrollable
  - Cinématique d'intro (2026-07-03) : `src/UI/IntroScreen.cs` (scène de boot) — cut-scene 2D scriptée en 6 plans (noyau d'Aether, corruption d'un drone, nuée + colosse, sanctuaire, descente de l'Arpenteur, reveal du titre), sprites animés réutilisant les `SpriteFrames` existants + particules `CpuParticles2D` + zoom caméra via `Tween`, synchronisée sur la narration `INTRO_BEAT_1..5` (EN/FR/ES) et la musique dédiée `music_intro` (CC0, "Transmission"/SRG774, cf. `assets/audio/CREDITS.md`). Skippable. Reveal via clés `INTRO_TITLE`/`INTRO_TAGLINE`. Outil de capture : `tools/capture_intro.py`
  - Localisation EN/FR/ES (`localization/ui.csv` → clé `Loc.T("CLÉ")`) ; support manette complet
  - HUD thématisé par biome, atmosphère (brume/rais/parallaxe), scanlines CRT
  - Arènes : obstacles thématisés par biome (`BiomeObstacles.cs`), features de sol (`FloorFeatures.cs` — lave/rivières/chemin pavé/conduits), gabarits structurés, décor rouillé réservé au Sanctuaire ; flag `--biome=<id>` pour forcer un biome (tests/captures)
  - Faune par biome (2026-07-03) : **28 ennemis basiques au total** (8 d'origine + 20 nouveaux, 4/biome), câblés via sprite data-driven (`EnemyBase.SetSpriteFrames` + `EnemySpawnData.FramesPath`/`AiType`) — aucune nouvelle scène/sous-classe, réutilise les 4 scènes archétype existantes (cf. `docs/GDD.md` §21). Sprites générés (`tools/generate_new_enemies.py`). Densité par biome doublée (spawnWeight dilué mais compensé par les ids globaux toujours actifs) — validé game-tester PASS. Limite connue : les 5 variantes d'un même archétype partagent une silhouette recolorée (pas de nouvelle forme par ennemi) — à arbitrer si plus de variété visuelle est souhaitée
  - **Affixes d'élite (2026-07-04)** : une fraction des ennemis *basiques* (jamais mini-boss/boss) est promue élite avec 1 affixe parmi 5 (Blindé/Régénérant/Explosif/Frénétique/Vampirique) — cf. `docs/GDD.md` §22. Logique pure testée `src/Core/Rules/EliteAffixTable.cs` (fréquence `clamp(0.03+0.02×t, 0, 0.28)`), appliquée par `EnemyBase.ApplyElite` (stats après `ApplyScaling` + comportement + rendu teinté/agrandi + halo `EliteAura`), tirée dans `EnemySpawner.SpawnEnemy`. Répond à la limite « silhouettes recolorées » (variété = comportement). Flag debug `--force-elites`. Répond au brainstorm « inspirations d'autres jeux » (élites façon Risk of Rain 2 / Diablo)
  - Voir `docs/EXPANSION_PLAN.md` et `docs/LEVEL_PROGRESSION_PLAN.md` pour le détail

## Conventions

- Plateforme cible : Windows (.exe)
- Moteur : **Godot 4.7 .NET** (toujours la variante `.NET`, pas la version standard)
- Langage : C# (.NET 8), GodotSharp
- Build Windows : `"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe" --headless --export-release "Windows Desktop" "./build/ChimeraProtocol.exe"`
- Export templates : `C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\editor_data\export_templates\4.7.stable.mono\` ✓
- **CRITIQUE export .NET** : `ChimeraProtocol.sln` DOIT être présent à la racine. Sans lui, le .exe crashe immédiatement. Recréer : `dotnet new sln --name ChimeraProtocol --format sln && dotnet sln ChimeraProtocol.sln add ChimeraProtocol.csproj`
- L'export produit `build/ChimeraProtocol.exe` + `build/data_ChimeraProtocol_windows_x86_64/` (runtime .NET 8). Les deux sont nécessaires. `data_*/` est ignoré par git — régénéré à chaque export.
- **Publication & MAJ auto** : itch.io + Butler. Numéro de version dans `project.godot` (`config/version`). Script `tools/release_itch.ps1` (export → dossier `build/dist_windows/` propre → `butler push` versionné → channel `windows`). Un push = auto-update pour les joueurs de l'app itch (patch différentiel wharf, zéro code jeu). Runbook complet : `docs/RELEASE.md`. Butler fourni par l'app itch (dossier `broth`, détecté auto). Incrémenter `config/version` avant chaque release.
- Style de code : PascalCase classes/méthodes, `_camelCase` champs privés, `readonly` par défaut
- Architecture : `src/` (logique C#) / `scenes/` (.tscn) / `assets/` (raw) / `data/` (JSON tuning)
- **Logique pure testable** : `src/Core/Rules/` (classes statiques sans dépendance Godot — `XpCurve`, `EnemyScaling`, `SpawnCurve`, `WeaponLeveling`, `StatCaps`, `WeightedPicker`…). Les nœuds y délèguent (SRP).
- **Tests unitaires** : `tests/ChimeraProtocol.Tests.csproj` (xUnit). Lancer : `dotnet test tests/ChimeraProtocol.Tests.csproj`. **83 tests**.
- Singletons (AutoLoad) : `GameManager`, `XpSystem`, `InventorySystem`, `LevelUpSystem`, `SaveManager`, `MetaProgressionSystem`, `AudioSystem`, `FusionFlash`, `ScreenShake`
- Données de tuning : `data/*.json`, modifiables sans recompiler
- Sauvegarde : `user://save.json` (méta/Échos) + `user://settings.cfg` (préférences, high scores, complétions, armes découvertes)
- Sprites : PNG transparent, grille 32×32 px (Colosse 48×48 — exception), `texture_filter = Nearest` global. Style **pseudo-3D avec ombres** (`docs/ART_BRIEF_PSEUDO3D.md`) via `tools/pseudo3d_lib.py` — toujours dériver shadow/highlight avec `shade()`/`shade_sprite()`/`shade_tile()`/`shade_icon()` plutôt que des couleurs plates ad hoc pour tout nouveau sprite
- Audio : OGG musique (fallback WAV), WAV ou OGG SFX. Sources CC0 dans `assets/audio/CREDITS.md`.
- Performance cible : 200–300 entités simultanées ; I-frames joueur 0.45 s (CRITIQUE pour les nuées)
- Palette UI : fond `#1A1A2E`, cyan `#44FFEE`, violet `#AA44FF`, or `#FFCC44`, blanc cassé `#D9D9F2`
- Police principale : Share Tech Mono (AA on, `ui_theme.tres`, size 16) — VT323 en réserve (AA off)
- Python : `C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe` (pas dans le PATH)

## Pièges critiques (non-évidents)

**Godot C# — API manquante**
- `GpuParticles2D.DrawPass1` n'existe pas en C# Godot 4.7 → `particles.Set("draw_pass_1", mesh)`
- `Image.Create()` est obsolète Godot 4.7 → `Image.CreateEmpty()`
- `GetViewport().GetFinalTransform()` ≠ transform caméra 2D → utiliser `Camera2D.GetScreenCenterPosition()`
- Piège C# 12 : `Instance?.Signal += handler` non supporté → `if (Instance != null) Instance.Signal += handler`

**Godot C# — threading / callbacks**
- `AddChild` interdit dans un callback physique (`BodyEntered`, `AreaEntered`) → `CallDeferred(AddChild)` + `SetDeferred("global_position", pos)`
- `file sealed class` interdit dans signatures de membres `public partial` → utiliser `internal sealed class`
- `FileAccess` ambigu si `using System.Text.Json` → toujours qualifier `Godot.FileAccess.Open(...)`

**Armes — câblage (checklist 8 points)**
Ajouter une arme requiert : `weapons.json` (5 niveaux) · `levelup_config.json` rarityByCard · `InventorySystem` (WeaponScenePaths + ApplySpecializedStats) · `LevelUpSystem.AllWeaponIds` · `Codex.Weapons` + `IconById` · icône `ui_icon_*.png` + `.import` · clés `WPN_*` EN/FR/ES dans `localization/ui.csv`

**Ennemis basiques (variante d'un archétype existant) — câblage data-driven, PAS de nouvelle scène**
Un nouvel id qui réutilise straight_chase/erratic_chase/ranged_kiter/slow_hunter n'a besoin d'AUCUNE
nouvelle scène `.tscn` ni sous-classe C#. Requiert : entrée dans `data/enemies.json` (`ai.type` =
un des 4 archétypes, `framesPath` optionnel vers un `.tres` dédié) · `Codex.Enemies` + accent
couleur cohérent avec le biome · clés `ENEMY_*_NAME/_TAG/_DESC` EN/FR/ES dans `localization/ui.csv`
· sprite `.tres`/`.png` produits par `graphiste` au chemin référencé (le jeu tolère leur absence à
la compilation, pas au runtime). `EnemySpawner.PreloadScenes` résout la scène via `ScenePaths` (id
dédié) sinon `ArchetypeScenePaths` (fallback par `ai.type`) ; `EnemyBase.SetSpriteFrames` échange le
`SpriteFrames` de l'`AnimatedSprite2D` après `AddChild` (même principe que
`Player.SetCharacterFrames`/`CharacterDef.FramesPath`). Un vrai nouveau comportement d'IA continue
de nécessiter scène + sous-classe dédiées (inchangé).

**Affixes d'élite — comportement universel malgré les `Die()` surchargés**
`EnemyBase.ApplyElite` câble blindage/régén/vampirisme/explosion (cf. `EliteAffixTable`). L'explosion
(`TriggerEliteExplosion`) et le vampirisme (`ApplyLifesteal`) sont appelés depuis `EnemyBase.Die`/
`HandleContactDamage` MAIS `GraftedColossus` surcharge les deux sans appeler `base` → il doit appeler
`TriggerEliteExplosion()`/`ApplyLifesteal()` explicitement (déjà fait). Toute nouvelle sous-classe qui
surcharge `Die()` ou `HandleContactDamage()` doit faire de même sous peine que l'affixe soit silencieux.
`ApplyElite` teinte le `SelfModulate` du sprite (PAS le `Modulate` du corps, réservé au HitFlash).

**VFX/projectiles parentés à la racine — purge à la sortie de run**
Les entités éphémères de gameplay (balles, flammes, death bursts, anneaux de choc, explosions
d'élite…) sont parentées à `GetTree().Root`, PAS à la scène de jeu → `ChangeSceneToFile` ne les
libère pas. En temps normal elles s'auto-détruisent vite, mais **à la mort l'arbre est mis en pause**
(`RunStatsTracker`), ce qui gèle leurs timers/tweens : elles réapparaissent, figées, par-dessus le
menu/Hub. Correctif : `SceneCleanup.ClearWorldVfx(GetTree())` (libère les `Node2D` enfants directs de
la racine sauf `CurrentScene` — sûr car tous les AutoLoads sont `Node`/`CanvasLayer`) appelé avant
chaque `ChangeSceneToFile` qui quitte une run (`RunEndScreen` Hub/Rejouer, `PauseScreen` Quitter).
Tout nouveau chemin de sortie de run doit l'appeler aussi.

**Navigation clavier/manette**
- Listes non focalisables (simples `PanelContainer`) : aucun voisin de focus → scroll dans `_UnhandledInput` via `_scroll.ScrollVertical` (`allowEcho:true` pour maintien)
- Focus spatial de Godot ne traverse pas les `PanelContainer` → `SetupFocusChain` avec `FocusNeighborTop/Bottom` explicites après génération complète de la liste
- Listes focalisables qui débordent → `FocusEntered → ScrollContainer.EnsureControlVisible()`
- `GrabFocus()` toujours dans un callback de tween (après fade-in), jamais dans `_Ready()` directement
- `FocusEntered` = tween scale uniquement (pas de SFX) ; `MouseEntered` = scale + SFX

**UI — pièges StyleBox / focus**
- `theme_override_styles/focus` dans un `.tscn` écrase `AddThemeStyleboxOverride()` runtime → ne jamais poser les deux
- `StyleBoxFlat` 3 états : chaque bouton doit avoir ses **propres instances** (pas de sub_resource partagée — Godot les lie et casse l'état hover/pressed)
- `PivotOffset` pour hover scale : calculer dans `MouseEntered` (`btn.Size / 2f`), PAS dans `_Ready()` (size = Vector2.Zero à ce stade)
- `MouseFilter = Ignore` sur la racine d'un écran "attend n'importe quelle entrée" — sinon le clic est absorbé comme événement GUI avant `_UnhandledInput`

**Scènes / cycle de vie**
- `WeaponBase._Ready()` initialise `_timer = Cooldown` — chaque sous-classe DOIT appeler `base._Ready()` EN DERNIER (après avoir assigné `Cooldown`), sinon tir au frame 0
- `GraftedColossus.Die()` n'appelle PAS `base.Die()` (qui fait `QueueFree()` immédiatement, tuant le nœud avant l'anim death)
- `RunEndScreen` : ordre de fermeture = `ChangeSceneToFile()` PUIS `RemoveChild(this)` PUIS `QueueFree()` — inverser provoque `data.tree is null`
- `RunEndScreen._Ready()` force `GetTree().Paused = false` — au cas où la mort survient pendant le LevelUpScreen (qui met `Paused = true`)
- `FusionFlash` / tout tween pendant une pause arbre : `SetPauseMode(Tween.TweenPauseMode.Process)` impératif
- `LevelUpSystem.Reset()` avant chaque run (remet `_pendingFusionId = null`) — sinon fusion parasite run suivante

**Assets**
- `.import` des PNG générés par script DOIT être commité (BUG-301) — sinon Godot ignore les assets au runtime
- Musique WAV : `loop_mode=0` par défaut dans Godot 4.7 → reboucler via signal `Finished` dans `AudioSystem`
- `AudioSystem.LoadMusic()` tente `.ogg` en priorité, puis `.wav` fallback

**Tests headless**
- `LevelUpScreen` met l'arbre EN PAUSE → gèle le serveur physique en headless (neutraliser l'XP de départ pour tester le gameplay)
- `Area2D` ne détecte un corps que via vrai mouvement physique (`MoveAndSlide`) — pas un téléport ni un `Tween`
