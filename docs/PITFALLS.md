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

## Paliers de menace / Échos (`LevelThreat`, `EchoFormula`)
Trois pièges quand on touche à la difficulté par niveau ou à la formule d'Échos :
1. **Le palier se résout à la demande, pas au `_Ready`.** `GameManager.CurrentBiomeId` est posé par
   `GroundRenderer._Ready` et l'ordre des `_Ready` entre nœuds frères n'est **pas** garanti : le lire
   dans `EnemySpawner._Ready` renverrait `""` (palier 0) une fois sur deux. D'où la propriété
   `ThreatTier` recalculée à chaque usage (`Array.IndexOf` sur 5 entrées, coût nul).
2. **`RunEndScreen` RECALCULE les composantes d'Échos** à partir des stats brutes, il ne reçoit que
   le total. Tout multiplicateur ajouté dans `EchoFormula` doit donc être appliqué **à l'identique**
   côté écran (helper partagé `EchoFormula.ApplyTier`, même troncature), sinon la somme animée ne
   tombe plus sur le total crédité et l'écran de fin ment au joueur. Test de non-régression :
   `EchoFormulaTests.TierMult_SommeDesComposantesEgaleLeTotal`.
3. **Densité et scaling ont deux temps de référence distincts** dans `EnemySpawner._Process` :
   `tDensity` (cadence/lots/vagues/cap) et `tStat` (PV/dégâts + `spawnStartMinute` + fréquence
   d'élite, décalé par `LevelThreat.TimeOffsetMinutes`). Passer `tStat` aux fonctions de densité
   ferait démarrer un haut palier à la densité du mid-game (écran plein en 10 s).

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
- **Tous les cadres passent par `UiStyle`** (`src/UI/UiStyle.cs`) et toutes les couleurs par `UiPalette`. Un `new StyleBoxFlat` ad hoc dans un écran est une régression : c'est exactement ce qui avait produit ~300 sites divergents (rayons 3/4/6/8/10, deux « fonds officiels » concurrents, le cyan réécrit à la main dans 8 blocs). Cadres à texture 9-slice → `assets/sprites/ui/frames/`, régénérables par `tools/generate_ui_frames.py`.
- `theme_override_styles/focus` dans un `.tscn` écrase `AddThemeStyleboxOverride()` runtime → ne jamais poser les deux. **Corollaire** : quand un écran passe à `UiStyle`, il faut purger les `sub_resource StyleBoxFlat` de sa scène (`tools/strip_tscn_styleboxes.py`), sinon le nouveau style est posé mais jamais visible — l'ancien gagne, en silence.
- Un `Tween` de focus (pulsation) sur une modale a besoin de `SetPauseMode(Tween.TweenPauseMode.Process)` : `PauseScreen`, `LevelUpScreen` et `AssimilationScreen` tournent avec l'arbre en pause, où un tween par défaut est gelé. `UiStyle.AttachFocusPulse` le fait déjà.
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
- **Toute image posée dans `docs/` est importée par Godot et embarquée dans le `.pck`.** Nommer les
  planches de contrôle jetables `docs/ui_sheet_*.png` (motif déjà gitignoré) plutôt qu'un nom libre.

## Icône de l'application (`tools/generate_app_icon.py`)
- Câblage en 3 points, sinon le `.exe` garde l'icône Godot par défaut :
  `export_presets.cfg` → `application/icon` **et** `application/console_wrapper_icon` = `res://icon.ico` ;
  `project.godot` → `config/icon="res://icon.png"` (éditeur/fenêtre) + `config/windows_native_icon="res://icon.ico"`.
- **Pas besoin de rcedit** : `application/modify_resources=true` (déjà dans le preset) fait patcher le PE
  par Godot lui-même. L'export headless suffit donc — pas de réglage d'éditeur à configurer.
- Le `.ico` est embarqué **tel quel**, entrées PNG comprises : chaque taille peut donc porter un
  dessin DIFFÉRENT. C'est le but — un 256 réduit à 16 px est illisible. Le générateur produit trois
  niveaux de détail (`full` ≥48, `small` 32, `tiny` ≤24) et écrit l'ICO à la main (PIL ne sait
  qu'empiler des redimensionnements d'une image unique).
- `.ico` et `.png` d'icône ne s'« importent » pas comme le reste : `icon.png` a besoin de son
  `.import` commité (règle ci-dessus), `icon.ico` n'est pas un type reconnu et reste un fichier brut.
- Vérifier le résultat sur le binaire, pas sur le source : `PrivateExtractIcons` (user32) sur
  `build/ChimeraProtocol.exe` pour 16/32/48/256 — l'export peut réussir sans que l'icône soit patchée.

## Musique adaptative (pistes alternées) — `MusicDirector`
- **Ne JAMAIS superposer `calm` et `combat` en permanence.** Ce sont deux générations Suno
  distinctes du même morceau : même tempo *nominal*, mais pas la même horloge ni la même phase.
  Les mélanger donne deux batteries décalées. Une seule piste est audible à la fois, la bascule
  se fait par fondu croisé (`MusicIntensity.Select` + `Approach`). C'est ce qui distingue cette
  architecture de l'ancienne à 4 stems synchronisés (bed/pulse/lead/boss), abandonnée avec la
  bande-son Vangelis.
- **Le fondu croisé se fait à puissance constante, pas en amplitude linéaire.** Deux morceaux
  décorrélés voient leurs *puissances* s'additionner : croiser leurs amplitudes linéairement
  creuse un trou de volume audible au milieu. `MusicIntensity.WeightToDb` applique 10·log₁₀
  (racine de l'amplitude) et non 20·log₁₀.
- **Hystérésis obligatoire sur le choix de piste.** Avec un seuil unique, une intensité qui
  oscille autour de la valeur pivot fait basculer les pistes en permanence. Deux seuils
  (`CombatEnter` 0.42 / `CombatExit` 0.26) **plus** une durée de maintien minimale
  (`MinHoldSec`, 10 s) — le boss seul court-circuite ce délai, c'est un événement, pas une tendance.
- **Le thème de boss démarre à son premier temps.** Il n'est pas lancé avec les deux autres au
  début de la run : il est `Play()` au moment de la bascule et `Stop()` quand son poids retombe à
  zéro. Le laisser tourner en fond ferait entrer le boss au milieu de son propre morceau.
- **Le bouclage doit être NATIF, pas manuel.** Les `.ogg` sont importés avec `loop=false` :
  `MusicDirector.LoadTrack` force `((AudioStreamOggVorbis)stream).Loop = true` sur la ressource.
  Reboucler via le signal `Finished` (comme `AudioSystem` le fait pour les WAV) laisserait un
  blanc à chaque tour.
- **`ProcessMode = Always` obligatoire** (même raison que `AudioSystem`) : sans lui la musique se
  tait à l'ouverture de la moindre modale.
- **Une piste simple et la musique de run ne coexistent jamais** : `AudioSystem.PlayMusic` coupe le
  `MusicDirector`. C'est le point d'intégration unique — ne pas ajouter d'appels `Stop()` dans
  chaque écran. Corollaire : l'ancien système à paliers de `RunStatsTracker` ne doit tourner **que**
  si les pistes sont absentes (`_legacyMusic`), sinon son `PlayMusic` tuerait l'adaptatif en pleine run.
- **`calm` ET `combat` sont requis** pour qu'un biome démarre : s'il en manque un seul, `PlayBiome`
  refuse le biome entier et retombe sur la musique simple. Le thème de boss, lui, est facultatif.

## Intégration de musique générée par IA (`tools/import_ai_music.py`)
- **Un morceau généré n'est pas une boucle.** Il a une intro, une outro et un fade : le script
  cherche le meilleur point de raccord par corrélation, coupe là, et fond la suite sur le début.
- **Ne jamais boucler sur une baisse d'énergie.** Les générateurs terminent sur une outro qui
  retombe ; un raccord qui y tombe fait chuter le morceau à chaque tour. Le script écarte les
  candidats dont l'énergie locale est sous 70 % de la médiane.
- **Corrélation et enveloppe par FFT / somme cumulée, jamais `np.correlate` ou `np.convolve`.**
  En direct, c'est O(n·w) : plus de 10¹² opérations pour trois minutes d'audio contre une fenêtre
  de trois secondes — le script ne finit jamais.
- **À qualité de raccord égale, prendre la boucle la plus longue** (`--loop-tolerance`) : le
  meilleur score absolu tombe souvent sur le premier retour du riff et jetterait les deux tiers du
  morceau. Le joueur entend la répétition bien avant d'entendre une couture.
- **« On n'entend pas les SFX » : vérifier `user://settings.cfg` AVANT de toucher au mixage.**
  Les volumes sont persistés (`[audio] master/music/sfx`) et survivent à toute réinstallation.
  Un `sfx=0.0` laissé là — curseur déplacé à la main, ou script de test PyAutoGUI passé dans les
  Options — rend le jeu totalement muet côté effets, sans le moindre message : `PlaySfx` charge le
  stream, appelle `Play()`, `Playing` vaut `true`… à -80 dB. Vu réellement le 2026-07-27, après
  deux corrections de mixage inutiles.
  Chemin : `%APPDATA%\Godot\app_userdata\Chimera Protocol\settings.cfg`.
  Diagnostic en une ligne : logger `_sfxVolume` et `player.VolumeDb` dans `PlaySfx`.
- **Trois bus audio, pas un seul** (`default_bus_layout.tres`) : `Master`, `SFX`, `Music`. Le bus
  `Music` porte un **compresseur en sidechain sur `SFX`** — dès qu'un effet joue, la musique
  s'efface de quelques dB et remonte en 200 ms. Sans ce ducking, un tir de 0,2 s reste inaudible
  sous un mur de guitares **même à niveau égal** : c'est du masquage spectral, pas un problème de
  volume, et le baisser davantage ne ferait que rendre la musique timide à son tour.
  **L'ordre des bus compte** : celui utilisé comme sidechain (`SFX`, index 1) doit être déclaré
  AVANT celui qui l'écoute (`Music`, index 2). Vérification runtime : script headless sur
  `AudioServer.get_bus_effect(i, j).sidechain` — une référence non résolue ne lève aucune erreur,
  le son part simplement sans ducking.
- **Un `Bus` inconnu sur un `AudioStreamPlayer` ne fait pas échouer le jeu** : Godot retombe
  silencieusement sur Master. Après tout renommage de bus, revérifier au runtime.
- **La musique de jeu se cale BEAUCOUP plus bas que la musique d'écoute.** Les pistes sont du
  metal très compressé : leur RMS reste haut en permanence, alors que les SFX sont des transitoires
  courts (ramassage d'XP ≈ -30 dB RMS). À -16 LUFS — le niveau habituel d'une musique de jeu — la
  bande-son couvrait purement et simplement les SFX. Cible retenue : **-22 LUFS** (`MUSIC_LUFS`),
  -23 menu/hub, -21 intro.
- **Ne pas utiliser `loudnorm` de ffmpeg pour caler une piste déjà masterisée.** En une passe il
  travaille en mode **dynamique** : il recompresse et limite un master déjà fini, et rate sa cible
  (-14,3 LUFS mesurés pour -16 demandés). `apply_loudness` mesure avec `ebur128` puis applique un
  **gain constant** — exact, et la dynamique reste intacte.
- **`godot --headless --import` peut rester bloqué sans rien écrire** (0 % CPU, sortie vide) :
  tuer le process et relancer suffit. Ne pas passer sa sortie dans un pipeline qui la tronque
  (`| Select-Object -First n` ferme le pipe et tue Godot en cours d'import).

## Génération audio (`tools/synth_lib.py`, `generate_music_v3.py`)
- **`loudnorm` de ffmpeg travaille en interne à 192 kHz et sort à ce taux** si on ne passe pas
  `-ar 44100` : fichiers 4,35× trop gros et lus au ralenti par tout ce qui suppose 44,1 kHz.
  Toujours forcer le taux d'échantillonnage *après* le filtre.
- **Une IR de réverbération se normalise en énergie (norme L2), pas en crête** : normalisée en
  crête, une IR de bruit de 4 s multiplie le signal par ~30. `reverb()` recale en plus le wet sur
  le RMS du dry, sans quoi le paramètre `mix` ne veut pas dire la même chose d'une salle à l'autre.
- **Les stems s'exportent à -20 LUFS / -6 dBTP** (`STEM_LUFS`, `STEM_TRUE_PEAK`), pas au niveau
  d'une piste finale : la somme des 4 couches à pleine intensité écrête sinon (+1,2 dBFS mesuré).
  Les rapports entre couches sont décidés en jeu par `MusicIntensity`, pas gravés dans les fichiers.
- **`np.isscalar` et non `isinstance(x, (int, float))`** pour accepter une note : les tirages
  `numpy` produisent des `np.int64` qui ne sont ni `int` ni itérables.
- Contrôle qualité sans écouter : `tools/analyze_music.py --dir … --biome <id>` (niveaux,
  hiérarchie fréquentielle, raccord de boucle, écrêtage de la somme des stems). Le `raccord` d'une
  couche **rythmique** est normalement très négatif (la boucle finit sur un silence) — ce n'est un
  défaut que sur `bed`, qui doit tenir sa note d'un bout à l'autre.

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
  `--force-graft=<id|all>` ; capture par **PID** (`tools/capture_graft_silhouette.py`) — cf. §Captures.

## Captures d'écran (outils `tools/`)
- **Cibler la fenêtre par PID, jamais par titre.** `find_window("Chimera")` renvoie la **première**
  fenêtre visible dont le titre contient la sous-chaîne — un navigateur ouvert sur la page itch du jeu
  (« New devlog for Chimera Protocol… ») match avant Godot. Conséquences observées : toute une série de
  captures « d'UI » montrant en fait le navigateur, et — pire — `capture_assimilation.py`, qui envoie
  touches et clics au centre de la fenêtre ciblée pendant des dizaines de secondes, tapant **dans la
  page web**. Utiliser `wait_for_window_by_pid(proc.pid)` de `tools/window_capture.py` dès qu'on lance
  le process Godot soi-même (`find_window` par titre n'est plus qu'un repli pour un process externe).
- **Pas de repli plein écran silencieux.** Un `pyautogui.screenshot()` de secours écrit une capture du
  **bureau** en annonçant `SAVED` : le script sort en succès et le jeu de captures est faux sans que
  rien ne le signale. Échouer bruyamment (`sys.exit(1)`) est la bonne réponse.
- Vérifier une capture avant de s'en servir : le tampon `v<version>-<sha>` (autoload `VersionStamp`)
  en bas à droite prouve que l'image vient bien du jeu, et de la bonne build.

## Capture vidéo / trailer (`tools/record_trailer.py` + `tools/build_trailer.py`)
- **Movie Maker Godot (`--write-movie out.avi`) plutôt qu'un enregistreur d'écran** : rendu à
  framerate fixe (60 fps, `editor/movie_writer/fps`), aucun frame drop même quand la scène rame, et
  l'audio du jeu est écrit dans le même AVI (MJPEG vidéo + PCM 48 kHz).
- **La sortie fait la taille du VIEWPORT, pas de la fenêtre.** `--resolution 2560x1440` agrandit la
  fenêtre mais le film reste en 1280×720 (`window/size/viewport_*`). Passer en 1440p se fait au
  montage par un upscale **×2 en `flags=neighbor`** — facteur entier, donc pixel art net ; viser
  1080p imposerait un ×1,5 qui bave.
- **Le temps du jeu n'est pas le temps réel** en mode film : selon la charge, 300 s de pilotage
  PyAutoGUI ont donné 220–236 s de vidéo. Toute timeline d'inputs doit donc être tolérante
  (mouvements longs, validations répétées) — aucune action à la frame près, le découpage fin se
  fait au montage.
- **Fermer par `WM_CLOSE`, jamais par `terminate()`** : le MovieWriter doit finaliser l'index de
  l'AVI, sinon le fichier est illisible ou tronqué.
- Le flag `--trailer` (`DebugHooks.TrailerMode`) masque le tampon `VersionStamp` et l'invite
  « appuyer pour passer » de l'intro — sinon elles s'incrustent dans toutes les prises.
- **La langue du trailer se décide à la CAPTURE, pas au montage.** Tout le texte affiché par le jeu
  (narration de la cinématique, bannières de biome, cartes de level-up, menus, codex) est gravé dans
  les rushes : sans rien préciser, `record_trailer.py` hérite de la langue de `user://settings.cfg`
  — celle du poste, pas forcément celle voulue. Passer `--lang=<code>`
  (`DebugHooks.ForcedLanguage`), qui surcharge la locale de la session **sans** l'écrire dans
  settings.cfg (`GameSettings._persistedLanguage`), et donner la même langue à
  `build_trailer.py --lang=` pour les cartons incrustés. Se tromper coûte une recapture complète.
- **La 1re minute d'une run ne montre rien** (nuée clairsemée, armes niveau 1). Le spectacle est en
  mid/late game : prévoir des prises de 4–5 min et ne garder que la fin.
- Traverser les menus au clavier dérive vite (une prise « menu » a fini dans Options) : lancer
  **directement** la scène de l'écran voulu (`res://scenes/ui/BestiaryScreen.tscn`…).
- Repérer les points de coupe sur des planches-contact (`tools/trailer_sheets.py`) **et vérifier le
  clip extrait** : un écran modal ne dure que ~2 s, une erreur de lecture de la planche fait tomber
  le plan sur le level-up suivant.
- **Les timecodes de l'EDL ne survivent pas à une recapture.** Les runs sont randomisées : après un
  passage de `record_trailer.py`, chaque plan est à recaler sur de nouvelles planches, et une prise
  qui marchait peut échouer (le joueur est mort à 21 s dans le `boss_tank` de la 1.17.0, la fin du
  rush étant l'écran de Hub). Vérifier chaque source avant de monter.
- **Une planche au pas de 5 s ne voit pas les modales** (≈2 s à l'écran) : elle en rate, et surtout
  elle en cache — un plan « gameplay » calé entre deux vignettes propres peut tomber en plein écran
  de fusion. Repasser à `--step 1` (voire 0,5) sur la fenêtre retenue, ou vérifier le montage final
  par une planche du MP4.
- Mixage : l'audio des rushes porte déjà la musique du jeu. Empiler une 2e musique à volume plein
  donne deux thèmes qui se battent → garder les plans bas (`CLIP_GAIN`, 0,12 depuis la bande-son
  metal : deux musiques rythmiques se battent plus que deux nappes) sous une piste musicale
  continue, puis `loudnorm=I=-14:TP=-1.5` (la somme brute sortait à −8 LUFS, écrêtée).
- **Ne pas remettre en musique de montage la piste qui joue déjà dans les plans** (`music_intro`
  sur les plans de cinématique) : la même piste jouée deux fois avec un décalage donne un doublage
  sale. Choisir des morceaux absents — ou quasi absents — des rushes retenus (`MUSIC_EDL`).

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
