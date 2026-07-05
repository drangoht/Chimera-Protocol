# Pièges critiques (non-évidents) — Chimera Protocol

> Référence chargée **à la demande** (pas à chaque session). À consulter **avant de coder** dans
> le domaine concerné : armes, ennemis, UI/focus, VFX, scènes/cycle de vie, assets, tests headless.
> Pointé depuis `CLAUDE.md` et le skill `/carte-projet`. Tenir à jour dès qu'un nouveau piège
> non-évident est découvert.

## Godot C# — API manquante
- `GpuParticles2D.DrawPass1` n'existe pas en C# Godot 4.7 → `particles.Set("draw_pass_1", mesh)`
- `Image.Create()` est obsolète Godot 4.7 → `Image.CreateEmpty()`
- `GetViewport().GetFinalTransform()` ≠ transform caméra 2D → utiliser `Camera2D.GetScreenCenterPosition()`
- Piège C# 12 : `Instance?.Signal += handler` non supporté → `if (Instance != null) Instance.Signal += handler`

## Godot C# — threading / callbacks
- `AddChild` interdit dans un callback physique (`BodyEntered`, `AreaEntered`) → `CallDeferred(AddChild)` + `SetDeferred("global_position", pos)`
- `file sealed class` interdit dans signatures de membres `public partial` → utiliser `internal sealed class`
- `FileAccess` ambigu si `using System.Text.Json` → toujours qualifier `Godot.FileAccess.Open(...)`

## Couches de collision (bits) — ne pas casser tirs/pickups
Schéma : **bit 1** = joueur (layer) + ennemis (layer) ; **bit 2** = obstacles bloquants. Le **joueur**
a `collision_mask = 2` (dans `Player.tscn`) → il traverse les ennemis (bit 1 seul) mais reste bloqué
par les obstacles `BiomeObstacles` (layer 3 = bits 1+2). Les **ennemis** ont `CollisionMask = 2`
(bloqués par les obstacles, jamais par le joueur). L'arène est bornée par `Player.ClampToArena()`
(clamp de position), PAS par des murs physiques. **Piège** : les armes touchent les ennemis via
`Area2D.BodyEntered` + `body is EnemyBase` (détection PHYSIQUE, pas par groupe pour les projectiles) —
NE PAS déplacer la couche (layer) des ennemis hors du bit 1 sans mettre à jour le masque de tous les
projectiles/zones. Pour changer le blocage du joueur, agir sur son **masque** (Player.tscn), pas sur la
couche des ennemis. Dégâts de contact = check de distance dans le code, indépendants de la collision.

## Armes — câblage (checklist 8 points)
Ajouter une arme requiert : `weapons.json` (5 niveaux) · `levelup_config.json` rarityByCard · `InventorySystem` (WeaponScenePaths + ApplySpecializedStats) · `LevelUpSystem.AllWeaponIds` · `Codex.Weapons` + `IconById` · icône `ui_icon_*.png` + `.import` · clés `WPN_*` EN/FR/ES dans `localization/ui.csv`

## Ennemis basiques (variante d'un archétype existant) — câblage data-driven, PAS de nouvelle scène
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

## Affixes d'élite — comportement universel malgré les `Die()` surchargés
`EnemyBase.ApplyElite` câble blindage/régén/vampirisme/explosion (cf. `EliteAffixTable`). L'explosion
(`TriggerEliteExplosion`) et le vampirisme (`ApplyLifesteal`) sont appelés depuis `EnemyBase.Die`/
`HandleContactDamage` MAIS `GraftedColossus` surcharge les deux sans appeler `base` → il doit appeler
`TriggerEliteExplosion()`/`ApplyLifesteal()` explicitement (déjà fait). Toute nouvelle sous-classe qui
surcharge `Die()` ou `HandleContactDamage()` doit faire de même sous peine que l'affixe soit silencieux.
`ApplyElite` teinte le `SelfModulate` du sprite (PAS le `Modulate` du corps, réservé au HitFlash).
Le **rendu « gelé »** (ennemi ralenti → bleu glacé) passe par un **shader**
(`assets/shaders/enemy_frost.gdshader`), PAS par un multiply sur `SelfModulate` : un multiply ne peut
qu'ASSOMBRIR, jamais AJOUTER du bleu absent d'un sprite chaud (orange → terne, pas bleu). Le shader
`mix(texture, bleu·luminance, frost)` lerpe la couleur du pixel. `EnemyBase.EnsureFrostMaterial()` pose
un `ShaderMaterial` (shader partagé) sur le sprite au 1er gel (lazy → batching préservé hors Givre) ;
`UpdateStatusEffects` bascule le uniform `frost` 0↔1 au seul changement d'état (pas d'écriture par frame).
**Piège Godot critique** : un fragment canvas_item custom qui écrit `COLOR` doit **terminer par `* COLOR`**
— sous le batching 2D, le `Modulate` du nœud (HitFlash) et le `SelfModulate` (teinte d'élite) sont bakés
dans le `COLOR` ENTRANT, et NE sont PAS ré-appliqués automatiquement après un fragment custom (référencer
`MODULATE` ne les restaure pas non plus). Écraser `COLOR` casse donc HitFlash + teinte d'élite ; `* COLOR`
les préserve. À `frost=0`, `mix(...,0)=texture` puis `* COLOR` = strictement identique à l'absence de
shader. (Limite connue, non bloquante : l'éclairage 2D d'un biome chaud — Fournaise — désature le bleu
vers un gris froid ; le rendu reste lisible « gelé » mais moins bleu que dans un biome neutre.)

## VFX/projectiles parentés à la racine — purge à la sortie de run
Les entités éphémères de gameplay (balles, flammes, death bursts, anneaux de choc, explosions
d'élite…) sont parentées à `GetTree().Root`, PAS à la scène de jeu → `ChangeSceneToFile` ne les
libère pas. En temps normal elles s'auto-détruisent vite, mais **à la mort l'arbre est mis en pause**
(`RunStatsTracker`), ce qui gèle leurs timers/tweens : elles réapparaissent, figées, par-dessus le
menu/Hub. Correctif : `SceneCleanup.ClearWorldVfx(GetTree())` (libère les `Node2D` enfants directs de
la racine sauf `CurrentScene` — sûr car tous les AutoLoads sont `Node`/`CanvasLayer`) appelé avant
chaque `ChangeSceneToFile` qui quitte une run (`RunEndScreen` Hub/Rejouer, `PauseScreen` Quitter).
Tout nouveau chemin de sortie de run doit l'appeler aussi.

## Navigation clavier/manette
- Listes non focalisables (simples `PanelContainer`) : aucun voisin de focus → scroll dans `_UnhandledInput` via `_scroll.ScrollVertical` (`allowEcho:true` pour maintien)
- Focus spatial de Godot ne traverse pas les `PanelContainer` → `SetupFocusChain` avec `FocusNeighborTop/Bottom` explicites après génération complète de la liste
- Listes focalisables qui débordent → `FocusEntered → ScrollContainer.EnsureControlVisible()`
- `GrabFocus()` toujours dans un callback de tween (après fade-in), jamais dans `_Ready()` directement
- `FocusEntered` = tween scale uniquement (pas de SFX) ; `MouseEntered` = scale + SFX

## UI — pièges StyleBox / focus
- `theme_override_styles/focus` dans un `.tscn` écrase `AddThemeStyleboxOverride()` runtime → ne jamais poser les deux
- `StyleBoxFlat` 3 états : chaque bouton doit avoir ses **propres instances** (pas de sub_resource partagée — Godot les lie et casse l'état hover/pressed)
- `PivotOffset` pour hover scale : calculer dans `MouseEntered` (`btn.Size / 2f`), PAS dans `_Ready()` (size = Vector2.Zero à ce stade)
- `MouseFilter = Ignore` sur la racine d'un écran "attend n'importe quelle entrée" — sinon le clic est absorbé comme événement GUI avant `_UnhandledInput`

## Scènes / cycle de vie
- `WeaponBase._Ready()` initialise `_timer = Cooldown` — chaque sous-classe DOIT appeler `base._Ready()` EN DERNIER (après avoir assigné `Cooldown`), sinon tir au frame 0
- `GraftedColossus.Die()` n'appelle PAS `base.Die()` (qui fait `QueueFree()` immédiatement, tuant le nœud avant l'anim death)
- `RunEndScreen` : ordre de fermeture = `ChangeSceneToFile()` PUIS `RemoveChild(this)` PUIS `QueueFree()` — inverser provoque `data.tree is null`
- `RunEndScreen._Ready()` force `GetTree().Paused = false` — au cas où la mort survient pendant le LevelUpScreen (qui met `Paused = true`)
- `FusionFlash` / tout tween pendant une pause arbre : `SetPauseMode(Tween.TweenPauseMode.Process)` impératif
- `LevelUpSystem.Reset()` avant chaque run (remet `_pendingFusionId = null`) — sinon fusion parasite run suivante

## Assets
- `.import` des PNG générés par script DOIT être commité (BUG-301) — sinon Godot ignore les assets au runtime
- Musique WAV : `loop_mode=0` par défaut dans Godot 4.7 → reboucler via signal `Finished` dans `AudioSystem`
- `AudioSystem.LoadMusic()` tente `.ogg` en priorité, puis `.wav` fallback

## Tests headless
- `LevelUpScreen` met l'arbre EN PAUSE → gèle le serveur physique en headless (neutraliser l'XP de départ pour tester le gameplay)
- `Area2D` ne détecte un corps que via vrai mouvement physique (`MoveAndSlide`) — pas un téléport ni un `Tween`

## Export .NET & release itch (course dotnet publish)
- **Godot 4.7 .NET rend la main à PowerShell AVANT la fin de `dotnet publish`** : `tools/release_itch.ps1` peut alors stager un runtime `data_*/` INCOMPLET (DLL manquantes) et le pousser via butler **sans erreur visible** → build amputée en ligne. Symptôme vécu : `DiscordRPC.dll`/`Newtonsoft.Json.dll` absentes (183 DLL au lieu de 185), Discord non fonctionnel.
- Garde-fous en place : `Wait-DirStable` (attend nb fichiers + taille stables) + `Assert-CriticalDlls` (vérifie `$CriticalDlls` sur le DataDir source ET le staging avant push, Fail sinon). **Ajouter toute nouvelle dépendance NuGet critique à `$CriticalDlls`** dans `release_itch.ps1`.
- Un re-push sûr après coup se fait avec `-SkipExport` (repart du DataDir déjà complet, sans ré-exporter).
