# CLAUDE.md — Mémoire de projet

Ce fichier est chargé automatiquement par Claude Code au démarrage de chaque session dans ce
dépôt. Il doit rester court et stable ; le détail du design vit dans `docs/GDD.md`.

## Le projet

"Chimera Protocol" — survivor roguelite vue du dessus, univers fantaisie-science-fiction
(humains, cyborgs, robots), inspiré de Vampire Survivors et Everything is Crab.
Référence complète : **`docs/GDD.md`** — toujours le consulter avant toute tâche de design
ou d'implémentation, et le tenir à jour quand une décision est prise.

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
- **Phase actuelle : libre — expansion terminée 2026-06-30** (commit `2385a67` = dernier fix).
- **Ce qui est implémenté** :
  - 3 personnages (Chimera, Titan, Vagabond), 5 biomes (Sanctuaire, Aether, Fournaise, Givre, Néon)
  - 11 armes actives + 4 fusions + 4 passifs ; power-ups temporaires (4 types)
  - Fin de niveau complète : survie sans fin, overtime, boss en boucle, déblocage progressif, high scores (temps+difficulté), arsenal à découverte
  - Localisation EN/FR/ES (`localization/ui.csv` → clé `Loc.T("CLÉ")`) ; support manette complet
  - HUD thématisé par biome, atmosphère (brume/rais/parallaxe), scanlines CRT
  - Voir `docs/EXPANSION_PLAN.md` et `docs/LEVEL_PROGRESSION_PLAN.md` pour le détail

## Conventions

- Plateforme cible : Windows (.exe)
- Moteur : **Godot 4.7 .NET** (toujours la variante `.NET`, pas la version standard)
- Langage : C# (.NET 8), GodotSharp
- Build Windows : `"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe" --headless --export-release "Windows Desktop" "./build/ChimeraProtocol.exe"`
- Export templates : `C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\editor_data\export_templates\4.7.stable.mono\` ✓
- **CRITIQUE export .NET** : `ChimeraProtocol.sln` DOIT être présent à la racine. Sans lui, le .exe crashe immédiatement. Recréer : `dotnet new sln --name ChimeraProtocol --format sln && dotnet sln ChimeraProtocol.sln add ChimeraProtocol.csproj`
- L'export produit `build/ChimeraProtocol.exe` + `build/data_ChimeraProtocol_windows_x86_64/` (runtime .NET 8). Les deux sont nécessaires. `data_*/` est ignoré par git — régénéré à chaque export.
- Style de code : PascalCase classes/méthodes, `_camelCase` champs privés, `readonly` par défaut
- Architecture : `src/` (logique C#) / `scenes/` (.tscn) / `assets/` (raw) / `data/` (JSON tuning)
- **Logique pure testable** : `src/Core/Rules/` (classes statiques sans dépendance Godot — `XpCurve`, `EnemyScaling`, `SpawnCurve`, `WeaponLeveling`, `StatCaps`, `WeightedPicker`…). Les nœuds y délèguent (SRP).
- **Tests unitaires** : `tests/ChimeraProtocol.Tests.csproj` (xUnit). Lancer : `dotnet test tests/ChimeraProtocol.Tests.csproj`. **59 tests**.
- Singletons (AutoLoad) : `GameManager`, `XpSystem`, `InventorySystem`, `LevelUpSystem`, `SaveManager`, `MetaProgressionSystem`, `AudioSystem`, `FusionFlash`, `ScreenShake`
- Données de tuning : `data/*.json`, modifiables sans recompiler
- Sauvegarde : `user://save.json` (méta/Échos) + `user://settings.cfg` (préférences, high scores, complétions, armes découvertes)
- Sprites : PNG transparent, grille 32×32 px (Colosse 48×48 — exception), `texture_filter = Nearest` global
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
