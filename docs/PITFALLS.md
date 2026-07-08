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

## Profondeur pseudo-3D (ZIndex) — obstacles vs joueur
Pas de Y-sort dans le projet : la profondeur est en **ZIndex fixes**. Le **joueur** a `ZIndex = 5`
(`Player.cs`, pour passer au-dessus de ses VFX d'armes). Un obstacle dont le corps est sous 5 est
**survolé graphiquement** par le joueur (bug « infranchissable mais transparent »), même si la collision
au pied bloque bien. Règle : le **corps** d'un obstacle infranchissable doit être à `ZIndex ≥ 6`
(`BiomeObstacles` body = 6 → enfants relatifs 7-10 ; colonnes de `GroundRenderer` sprite = 6), et son
**ombre au sol** doit être ré-ancrée en `ZAsRelative = false` (sinon elle hérite du Z du corps et flotte
au-dessus des entités). Compromis assumé : l'obstacle occulte aussi le joueur quand il est « devant »
(en dessous à l'écran) — négligeable pour des silhouettes hautes/fines, à remplacer par un vrai Y-sort
si gênant (attention : Y-sort casse la relation ZIndex joueur↔VFX).

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
**Poussée (game feel)** : le joueur ne collisionne PAS physiquement avec les ennemis (il n'est jamais
bloqué), mais `Player.PushEnemiesAside()` déplace chaque ennemi chevauchant son corps hors d'un anneau
(`sep = max(PlayerBodyRadius, enemy.PushRadius − 6)`). La séparation reste **sous** le rayon de contact
de l'ennemi → les dégâts de contact continuent de s'appliquer. Ne PAS pousser jusqu'au rayon de contact
plein, sinon plus aucun dégât de contact.

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
- Les touches de déplacement (ZQSD, `move_*`) sont **séparées** des `ui_*` (nav focus des menus/modals). Pour que ZQSD navigue aussi les menus, `InputRemap.SetKey` **miroite** chaque `move_*` vers son `ui_*` (`BuildDirectional(UiNav[action], …)`). Un menu qui repose sur le focus natif de Godot lit les `ui_*` : sans ce miroir, seules les flèches fonctionnent. Tout nouvel écran doit donc s'appuyer sur les `ui_*` (focus natif) et non lire `move_*` en dur.
- Le **dash** (`dash`) est une action à part (Maj/RB), rebindable via Options (`GameSettings.SetDashKey` → `InputRemap.SetDashKey`, persistée sous `[input] dash`). Non miroitée vers `ui_*` (ce n'est pas une direction).
- Listes non focalisables (simples `PanelContainer`) : aucun voisin de focus → scroll dans `_UnhandledInput` via `_scroll.ScrollVertical` (`allowEcho:true` pour maintien)
- Focus spatial de Godot ne traverse pas les `PanelContainer` → `SetupFocusChain` avec `FocusNeighborTop/Bottom` explicites après génération complète de la liste
- Listes focalisables qui débordent → `FocusEntered → ScrollContainer.EnsureControlVisible()`
- `GrabFocus()` toujours dans un callback de tween (après fade-in), jamais dans `_Ready()` directement
- `FocusEntered` = tween scale uniquement (pas de SFX) ; `MouseEntered` = scale + SFX

## UI — pièges StyleBox / focus
- **`TextureRect` dans un petit conteneur clippé** : `ExpandMode` par défaut = `KeepSize` → le `TextureRect` prend la **taille de sa texture** (ex. 32 px) comme taille minimale, qui l'emporte sur un rect d'ancrage plus petit (ex. 20 px). L'icône déborde et, si le parent a `ClipContents=true`, on n'en voit qu'un coin (BUG icônes de greffe tronquées, slots 26 px). Poser `ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize` pour que `KeepAspectCentered` respecte le rect et recentre l'icône entière.
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
- **Tester l'existence d'un asset : `ResourceLoader.Exists("res://…png")`, jamais `FileAccess.FileExists`.** En build exporté le PNG source n'est **pas** dans le `.pck` (seule la texture importée `.ctex` l'est) → `FileExists` renvoie toujours `false` et masque l'asset en jeu alors qu'il marche dans l'éditeur (BUG icônes de greffes absentes du HUD). `GD.Load` seul suffit souvent (renvoie null proprement si absent).
- Musique WAV : `loop_mode=0` par défaut dans Godot 4.7 → reboucler via signal `Finished` dans `AudioSystem`
- `AudioSystem.LoadMusic()` tente `.ogg` en priorité, puis `.wav` fallback

## Assimilation / greffes (écrans modaux, routage, effets)
- **Deux écrans modaux qui togglent `GetTree().Paused` se marchent dessus** (LevelUpScreen +
  AssimilationScreen) → passer par **`ModalQueue`** (statique, `src/UI/`) : chaque écran *soumet*
  une présentation (`Submit(tree, show, highPriority)`) et signale la résolution (`Done()`). UN SEUL
  `Paused=false` rendu quand la file est vide ; le level-up est prioritaire (highPriority) ; les deux
  écrans ne sont donc **jamais affichés en même temps** (pas de conflit de focus/clic). `ModalQueue.Reset()`
  au début de chaque run (`GameManager.RegisterPlayer`) sinon un état bloqué (mort en plein écran modal)
  fuit sur la run suivante. Tout NOUVEL écran modal pausant doit utiliser ModalQueue (ne PAS toggler `Paused` en direct).
- **`AssimilationSystem.GaugeFilled` peut être émis depuis un callback physique** (`EnemyBase.Die` ←
  `Bullet.OnBodyEntered`). Donc `AssimilationScreen` **pré-construit TOUTE son UI dans `_Ready`** (aucun
  `AddChild` à la présentation — sinon crash « AddChild interdit en callback physique ») ; la présentation
  ne fait que configurer/afficher des nœuds existants, et `GrabFocus` est **différé** (`Callable.From(...).CallDeferred()`).
- **Effets de greffe (mini-essaims, tourelle, thorns, onde) dans `GraftManager._Process`** (pas
  `_PhysicsProcess`) → `AddChild` de balles/anneaux sûr, et gel automatique pendant une pause modale
  (GraftManager est enfant du Player, `ProcessMode` hérité). Le **dash** est l'exception : il lit l'entrée
  et déplace le corps → il vit dans `Player._PhysicsProcess` (burst via override de `Velocity`, i-frames
  propres qui court-circuitent `TakeDamage`, jamais soumis à `MaxSpeed`).
- **Retrait propre d'un stat mod malgré les hardcaps** : à l'équipement, stocker le **delta réellement
  appliqué** (post-`StatCaps.CapDamageReduction`) et le soustraire au retrait (`GraftManager.StatDelta`) —
  soustraire la valeur brute (0,15) après écrêtage donnerait un résultat faux. Le malus de vitesse passe par
  `Player.GraftSpeedMultiplier` (produit des `speedMult` actifs), **jamais** par `Stats.Speed`/`MaxSpeed`.
- **Routage kill→jauge = logique pure** (`GraftTable.RouteKill`, testé xUnit) : le kill est notifié via
  `GameManager.NotifyEnemyKilled(this)` (les 9 `Die()`/overrides passent `this`). Les métadonnées
  (`AssimArchetype`/`AssimIsMiniBoss`/`AssimIsBoss`) sont posées par `EnemySpawner.SpawnEnemy`. Une jauge
  d'une greffe équipée est **en pause** ; refus → seuil ×1,5 pour le cycle (`_declined`).
- **Fusions de greffes (§15)** : une jauge `fusion_<id>` n'accumule (`AssimilationSystem.RouteFusionKill`)
  QUE si **les 2 greffes `requires` sont équipées** ET que le kill est un basique/élite d'un archétype
  source (mini-boss/boss exclus). À l'acceptation, `AssimilateFusion` **retire les 2 greffes sources et
  équipe la fusion** → occupation 2→1 (jamais d'écran de remplacement). La `FusionDef` **hérite** de
  `GraftDef` → `GraftById`/HUD/pause/écran la traitent comme une greffe ; son seuil est injecté dans
  `Thresholds` au parse pour qu'`EffectiveThreshold` marche uniformément. La **charge** (fusion Charge
  Blindée) réutilise le dash (`Player.EnableDash` avec params de charge : couloir `_chargeWidth`, un hit
  par ennemi via `_chargeHit`, knockback ; contourne `MaxSpeed`, i-frames en **max** avec celles de
  dégât, pas cumul). Les **tourelles** (Ruche) vivent dans `GraftManager._Process` (suivi lerp + `Bullet`).
- **Fusions qui partagent une greffe (3e fusion Frappe Nova, §15.8)** : `fusion_nova_rodeur`
  (`stalker_wave`+`erratic_servos`) partage `erratic_servos` avec `fusion_charge_blindee`. **Ne PAS**
  ajouter de règle d'éligibilité spéciale : l'infra existante suffit — `RouteFusionKill` n'accumule que
  si TOUS les `requires` sont dans `_equipped`, et `AssimilateFusion` re-garde `ready` avant de
  consommer. Équiper une des deux fusions retire les servos → l'autre devient inéligible (exclusion
  mutuelle = choix de build, voulu). Les deux redéfinissant le dash, cette exclusion évite aussi tout
  conflit de dash. La **Nova** détone au **front descendant** de `Player.IsDashing` (`GraftManager.
  UpdateNova`, pas un timer) → réutilise le helper partagé `EmitShockwave` (onde périodique ET nova de
  dash passent par lui ; ne pas dupliquer la logique anneau+dégâts).
- **Affinités de biome (§21)** : une greffe **capture le biome à l'assimilation** (`GameManager.
  CurrentBiomeId` lu dans `AssimilationSystem.EquipOnPlayer`) et le garde même si le joueur change de
  biome ensuite → stocké par greffe dans `GraftManager._affById`, PAS relu chaque frame. Appliquer
  l'affinité **dans les `Setup*`** (damage/radius/cooldown ×mult) et les **boucles de hit**
  (`ApplyAffinityOnHit` pour Nuée/thorns/onde/nova ; `SetBulletAffinity` pour les balles Œil/Ruche).
  Piège : `def.Tint` est un multiplicateur, mais `BiomeAffinity.Accent` est une **couleur** (rgb 0-1) —
  l'accent est baké (22 %) dans la couleur de matière du prop, PAS dans `def.Tint`. Biome inconnu/null →
  `GetAffinity` renvoie `Neutral` (tout à 1, pas de burn/slow) : ne jamais supposer qu'une affinité
  existe. La **charge** (Charge Blindée) ne porte pas burn/slow (dégâts côté `Player`), seulement les
  mults numériques via `SetupCharge`.
- **Nouvelles clés `ui.csv` non prises en compte au runtime** : les `.translation` compilés ne sont PAS
  régénérés par un simple `--headless` ; lancer **`godot --headless --import`** (ou l'éditeur) pour
  recompiler la CSV. En attendant, `AssimilationScreen.TFallback` retombe sur le texte FR du `grafts.json`
  (l'écran reste lisible), mais `HubScreen` (Loc.T direct) afficherait la clé brute.
- **Props de silhouette (Phase B volet 2, `GraftManager` § « Props de silhouette »)** : nœuds visuels
  procéduraux attachés au joueur, **construits/purgés dans `RebuildBehaviors`** (comme les essaims/
  tourelles) et animés dans `UpdateProps` (`_Process`, pas physique). **Espace local** : les props sont
  enfants du GraftManager (à l'origine du joueur) → position en LOCAL (suit le joueur gratuitement),
  contrairement aux essaims/tourelles qui utilisent `GlobalPosition`. **Miroir** du facing via
  `Player.FacingLeft` (négation `Anchor.X` + `Scale.X=-1` pour les props directionnels `Mirror=true` ;
  props centrés = `Mirror=false`). **`ZIndex` relatif** (ZAsRelative) : le joueur est à z=5 ; un prop
  z=+1 rend AU-DESSUS du sprite, z=−1 EN DESSOUS (un thruster à z=−1 disparaît derrière les jambes —
  le mettre à z=+1 pour qu'il déborde et lise). **Teinte** : `def.Tint` est un MULTIPLICATEUR (canaux
  &gt; 1 possibles) → passer par `BaseColorFromTint` (normalise en couleur de matière) avant d'ombrer
  via `Shade(color, Face)` (dérivation HSV du brief pseudo-3D, PAS de noir/blanc pur). Flag debug
  `--force-graft=<id|all>` ; capture par **PID** (`tools/capture_graft_silhouette.py`) car
  `find_window("Chimera")` attrape un navigateur/éditeur titré « Chimera » (devlog) au lieu du jeu.

## Tests headless
- `LevelUpScreen` met l'arbre EN PAUSE → gèle le serveur physique en headless (neutraliser l'XP de départ pour tester le gameplay)
- `Area2D` ne détecte un corps que via vrai mouvement physique (`MoveAndSlide`) — pas un téléport ni un `Tween`

## Propriété partagée de save.json (méta) — ne pas charger deux copies
- `SaveManager.Load()` renvoie une **copie fraîche** à chaque appel (pas un singleton). `MetaProgressionSystem` détient l'**unique** copie en mémoire du bloc méta (Échos, upgrades, défis, compteurs). Tout autre système qui doit écrire dans save.json (ex. `ChallengeSystem`) doit **muter `MetaProgressionSystem.Meta`** puis appeler `MetaProgressionSystem.PersistMeta()` — **jamais** charger sa propre `SaveData`, la muter et la sauvegarder : les deux copies divergent et la dernière écriture écrase les Échos gagnés dans l'autre. Un seul propriétaire, un seul point d'écriture.

## Casse de fichier C# sur Windows — `HUD.cs` réécrit en `Hud.cs` casse l'instanciation from-source
- **État canonique (ne pas dévier)** : fichier `src/UI/HUD.cs` (majuscules) + classe `public partial class HUD` + sidecar `src/UI/HUD.cs.uid` + ext_resource `path="res://src/UI/HUD.cs"` dans `scenes/ui/HUD.tscn`. Tout doit être en **`HUD`** — Godot exige que le nom de classe corresponde **exactement** au nom de fichier (sensible à la casse).
- **Piège** : sur FS Windows insensible à la casse, écrire le fichier via l'outil Write avec un chemin `src/UI/Hud.cs` (minuscule) **n'échoue pas** et écrase le même fichier, mais **change la casse NTFS réelle sur disque** en `Hud.cs`. La build incrémentale C# enregistre alors la classe `HUD` sous le chemin `res://src/UI/Hud.cs`, en désaccord avec le `.tscn` (`res://src/UI/HUD.cs`). Symptôme au lancement from-source (`godot ... res://scenes/Game.tscn`) : `ERROR: Cannot instantiate C# script because the associated class could not be found. Script: 'res://src/UI/HUD.cs'` → le nœud `HUD` (CanvasLayer) existe **sans script** → aucune barre HP/XP/timer/greffe (le reste tourne : biome, joueur, `LevelUpScreen`, `Banner`, autoloads OK). **N'affecte PAS l'export Windows** (git tracke `HUD.cs`, un checkout/export propre a la bonne casse) — c'est un artefact du working tree local.
- **Résolution (appliquée 2026-07-06)** : (1) restaurer la casse disque via double rename (FS insensible) — `mv src/UI/Hud.cs src/UI/_tmp.cs && mv src/UI/_tmp.cs src/UI/HUD.cs` ; (2) **rebuild propre** obligatoire pour reconstruire le mapping classe→chemin : `rm -rf obj bin .godot/mono && dotnet build` ; (3) `godot --headless --import`. Vérif : `godot --headless res://scenes/Game.tscn --quit-after 150` ne doit plus émettre l'erreur. **Règle de prévention** : ne jamais référencer ce fichier en `Hud.cs` dans un outil d'édition — toujours `src/UI/HUD.cs`.

## Export .NET & release itch (course dotnet publish)
- **Godot 4.7 .NET rend la main à PowerShell AVANT la fin de `dotnet publish`** : `tools/release_itch.ps1` peut alors stager un runtime `data_*/` INCOMPLET (DLL manquantes) et le pousser via butler **sans erreur visible** → build amputée en ligne. Symptôme vécu : `DiscordRPC.dll`/`Newtonsoft.Json.dll` absentes (183 DLL au lieu de 185), Discord non fonctionnel.
- Garde-fous en place : `Wait-DirStable` (attend nb fichiers + taille stables) + `Assert-CriticalDlls` (vérifie `$CriticalDlls` sur le DataDir source ET le staging avant push, Fail sinon). **Ajouter toute nouvelle dépendance NuGet critique à `$CriticalDlls`** dans `release_itch.ps1`.
- Un re-push sûr après coup se fait avec `-SkipExport` (repart du DataDir déjà complet, sans ré-exporter).
