# Pièges Unity — migration Chimera Protocol

Pendant du `docs/PITFALLS.md` de la branche Godot, qui **reste valide** tant que Godot sert de
référence de parité. Ce fichier se remplit **au fil des lots**, jamais après coup : un piège se
documente le jour où il coûte une heure, pas trois semaines plus tard.

Chaque entrée dit **le symptôme** avant la cause — c'est par le symptôme qu'on le rencontrera.

---

## Scripts et scènes

### Un `MonoBehaviour` par fichier, portant son nom — sinon la référence est vide, en silence

**Symptôme** : un composant est bien dans le prefab (ses champs sérialisés sont là), mais
`GetComponent<T>()` renvoie `null` à l'exécution. Aucune erreur, aucun avertissement. Dans notre cas,
l'arme tirait 0 projectile alors que tout — cible à portée, prefab assigné, `Update` appelé
715 000 fois — était correct.

**Cause** : Unity ne peut associer un `MonoBehaviour` à un asset de script que si **la classe porte
le nom de son fichier**. `WeaponBase`, `ImpulseCannon` et `Bullet` cohabitaient dans `Weapon.cs` :
le prefab contenait `m_Script: {fileID: 0}` — une référence **vide** — avec seulement
`m_EditorClassIdentifier: ChimeraProtocol.Gameplay::Bullet` comme trace.

**Parade** : un fichier par `MonoBehaviour`. Les classes **statiques** et les classes ordinaires
(non-`MonoBehaviour`) échappent à la règle et peuvent cohabiter.

⚠ **Sans équivalent Godot** : là-bas, un `.cs` peut contenir n'importe quel nom de classe. C'est un
piège *créé* par la migration, invisible à la compilation et invisible aux tests unitaires.

**Diagnostic express** : `grep -A1 m_Script <prefab>` — si le `fileID` vaut 0, la référence est morte.

### `Instantiate` conserve l'état actif du gabarit ; Godot rend toujours actif

**Symptôme** : « 4 ennemis créés, 0 vivants ». Des objets présents en hiérarchie, absents du jeu, qui
ne signalent rien. `OnEnable` n'est jamais appelé, donc les inscriptions aux listes globales n'ont
pas lieu et `Update` ne tourne pas.

**Cause** : sous Godot, `Instantiate()` + `AddChild()` produit **toujours** un nœud actif. Unity
recopie l'état du prefab.

**Parade** : `go.SetActive(true)` après chaque `Instantiate` de gabarit — fait dans `Spawner`,
`EnemySpawner` et `ImpulseCannon`.

### `base.Awake()` doit rester **en dernier** — la règle Godot revient, pour une autre raison

**Symptôme** : une arme inflige les dégâts du socle (10) au lieu des siens (18, 14, 8…), et la
progression des fusions part d'une valeur fausse.

**Cause** : `WeaponBase.Awake()` **fige la valeur de fiche** (`SheetDamage`). Appelé *avant* que la
sous-classe ne pose ses valeurs, il capture le défaut.

**Parade** : dans chaque sous-classe, régler les champs puis appeler `base.Awake()` **en dernier**.
C'est exactement la contrainte d'ordre que Godot imposait sur `base._Ready()` — mais là-bas il
s'agissait d'initialisation, ici de **capture**. La règle survit à la migration, sa justification
non : ne pas supposer qu'un piège disparu ne revient pas sous un autre visage.

### Une exception dans `Awake` rend le composant **entièrement inerte** — le pire piège rencontré

**Symptôme observé en jouant** : le joueur ne se déplace plus du tout, ne perd aucun PV, sa
régénération ne tourne pas — mais il est **visible et animé**, et les orbes continuent de l'attirer.
Aucun message à l'écran.

**Cause** : `Player.Awake()` appelait `_animator.Play("idle")` alors que `FrameAnimator.Awake()`
n'avait pas encore tourné — son `SpriteRenderer` valait `null`. L'exception interrompt `Awake`, et
**Unity cesse alors d'appeler `Update` sur ce composant**. Comme `Instance = this` était affecté
*avant* le point de rupture, tout ce qui interroge le joueur de l'extérieur (orbes, ennemis, HUD)
continuait de fonctionner : d'où un faisceau de symptômes qui ne désigne aucune cause commune.

**Parade** : ne jamais résoudre une dépendance de composant dans `Awake` pour la consommer depuis un
autre `Awake`. Résoudre **à la demande** :

```csharp
private SpriteRenderer? _cache;
private SpriteRenderer? Renderer => _cache != null ? _cache : _cache = GetComponent<SpriteRenderer>();
```

⚠ **Ce qu'il faut en retenir pour la méthode** : ce bug est passé au travers de **506 tests
unitaires et 67 vérifications à l'exécution**. Les vérifications testaient que `FrameAnimator` joue
une animation qu'on lui **donne** ; jamais qu'un `Player` **issu de la scène réelle** s'initialise
sans exception. Un banc qui assemble ses objets par code ne rencontre pas les ordres d'initialisation
d'une scène authorée. **Le premier lancement humain reste irremplaçable.**

### `Destroy(weapon.gameObject)` **supprime le joueur** quand l'arme est un composant du joueur

**Le cas** : l'arme de départ n'est pas un objet créé pour elle — c'est un composant posé sur le
`GameObject` du joueur, dans la scène. Une fusion remplace l'arme source par la fusion, et le port
détruisait l'objet de l'arme remplacée : forger sa **première fusion** aurait donc détruit le joueur
au milieu de la run.

**Parade** : ne détruire l'objet que s'il a été créé pour porter cette arme — sinon détruire le
**composant** seul (`InventorySystem.RemoveWeapon`). La question à se poser à chaque `Destroy` porté
depuis Godot : *cet objet m'appartient-il, ou est-ce que je partage celui de quelqu'un d'autre ?*
Sous Godot, une arme était toujours un nœud enfant, donc la question ne se posait pas.

### Une destruction Unity n'est effective qu'à la **fin de la frame**

Deux conséquences opposées, rencontrées le même jour :

- **Mesurer trop tôt** : compter des composants juste après un `Destroy` renvoie l'objet condamné.
  Une vérification pourtant correcte échouait pour cette seule raison — il manquait un
  `yield return null` avant la mesure.
- **Compter sur la destruction** pour retirer d'une liste : `OnDisable`/`OnDestroy` n'ont pas encore
  tourné, donc l'objet est toujours dans `EnemyBase.Active`, `null`-comparable mais présent.

### Une boucle qui **inflige des dégâts** ne parcourt jamais `EnemyBase.Active` directement

**Symptôme** : `InvalidOperationException: Collection was modified` au milieu d'une passe d'arme —
le reste de l'attaque est perdu et l'arme paraît simplement **rater ses cibles**, sans que rien ne
saute aux yeux en jouant.

**Cause** : frapper peut tuer, et une mort retire de la liste statique pendant l'énumération.

**Parade** : `EnemyBase.Active.ToArray()` avant toute boucle qui frappe **et continue** (aura, arc,
essaim, glaive, projectile perforant). Les boucles qui *cherchent* une cible sans frapper, ou qui
sortent au premier impact, n'en ont pas besoin. Idiome déjà en place dans `OverloadField`,
`PlasmaBlade`, `CryoLance`, `PyreStream`, `Singularity` — il a fallu l'étendre à `Bullet`,
`DroneSwarm` et `GlaiveProjectile`.

### Une arme est un **composant**, pas un prefab — le registre remplace `res://scenes/weapons/`

Le port avait conservé l'appel `GD.Load("res://scenes/weapons/<id>.tscn")`, mais ces 21 prefabs
n'existaient pas : le seul symptôme était une carte prise **sans effet**. `WeaponRegistry` associe
l'id au composant et fabrique l'arme sur le porteur — plus rien à tenir synchronisé entre un id, un
fichier et une classe. **Le banc consomme la même table**, sinon une arme absente du banc est une
arme dont on ne sait pas si elle tire.

### Juger une interface sans la **regarder** ne marche pas

Verdict en jouant : « le design est horrible ». Toutes les vérifications d'UI écrites jusque-là
répondaient à « l'écran s'ouvre-t-il ? » et « la ligne existe-t-elle ? » — **aucune** à « à quoi ça
ressemble ? ». D'où `--screenshots` : le jeu se photographie lui-même (8 écrans). Ce qui a été trouvé
**en regardant les images**, et qu'aucun test n'aurait vu :

| Symptôme sur l'image | Cause |
|---|---|
| Cadres énormes, coins en bouillie | Une `Image` uGUI met ses bordures 9-slice à l'échelle de `referencePixelsPerUnit / spritePixelsPerUnit`. Les sprites du projet sont importés en **PPU 1** (1 px = 1 unité) → bordures ×100 |
| Liserés fluo, fonds teintés | La texture portait **déjà** son accent, et le code la multipliait par la même couleur |
| Réglages qui se chevauchent | 8 lignes de 64 px dans un panneau de 660 sans défilement |
| Le menu par-dessus la partie | Les outils (`SceneDiagnostic`, `ScreenshotTour`) étaient posés **sur l'objet du menu** : leur `DontDestroyOnLoad` faisait survivre le menu entier |
| Sol uni là où on attend des tuiles | `SpriteDrawMode.Tiled` exige un maillage **`FullRect`** ; sinon Unity **étire** une tuile de 32 px sur toute l'arène |
| Sol noir, motif disparu | La couleur d'un `SpriteRenderer` **multiplie** : une teinte sombre sur une tuile déjà sombre l'écrase |
| Tout rendu aux ⅔, joueur hors champ | `orthographicSize` figée à 540 (1080 unités) quelle que soit la fenêtre, et **aucune caméra de suivi** |

⚠ Un réglage d'import ne s'applique pas à un asset **déjà importé** : `touch` ne suffit pas, il faut
supprimer le `.meta` (ou forcer la réimportation). Le symptôme est un postprocessor qui « ne fait
rien ».

### Sans **`AudioListener`**, tout le système audio tourne et le jeu reste muet

**Signalé en jouant** : « je n'ai rien entendu ». Pourtant les 41 clips étaient importés, les
`AudioSource` créées, `PlaySfx` appelée à chaque tir et à chaque mort, le compteur de lectures
montait, et le `MusicDirector` annonçait la bonne piste. **Il manquait l'oreille** : les scènes sont
générées par code (`new GameObject("MainCamera", typeof(Camera))`) et personne n'avait ajouté
d'`AudioListener`. Unity ne lève aucune erreur.

**Ce que cela dit du banc** : compter les lectures ne prouve **rien** sur l'audibilité — même famille
que `Image.Type.Filled` sans sprite, où `fillAmount` se réglait sans effet. Le seul relevé qui vaut
est *« un `AudioListener` existe-t-il dans la scène ? »*, désormais dans `SceneDiagnostic` avec le
nombre de sources et la piste courante.

**Parade** : `typeof(AudioListener)` sur la caméra des deux scènes générées, et **un seul** — plusieurs
listeners produisent un avertissement et un mixage imprévisible.

### Un appel de son écrit **arme par arme** ne se porte jamais en entier

**Signalé en jouant (2026-08-09)** : « la Bobine Tesla n'émet aucun son ». Elle n'était pas seule —
le jeu publié appelle `PlaySfx` depuis **seize** classes d'armes, chacune dans sa propre méthode de
tir ; le portage en avait repris **deux** (`ImpulseCannon`, `VectorLance`). Quatorze armes tiraient en
silence **depuis le premier jour du portage**, dont onze des douze de base.

**Pourquoi rien ne l'a vu.** Une arme muette blesse, monte de niveau, s'affiche et laisse ses traces
visuelles : elle passe tous les critères existants, y compris « les 21 armes tirent ». Une capture
d'écran est muette par nature. Et le compteur global `PlayedCount` montait *quand même*, alimenté par
les ramassages d'XP et les coups encaissés — il affichait des centaines de sons pendant que l'arsenal
entier ne produisait rien. C'est la forme sonore de la règle des **données déclarées non consommées** :
la moitié d'un système peut manquer sans qu'un seul indicateur ne bouge.

**Parade structurelle** — la même que pour les icônes et les fusions : **une table**, pas des appels
dispersés.
- `WeaponSfx` (logique pure, testée) associe un son à chaque identifiant d'arme, ou la déclare
  **muette à dessein** (`Silent`) — un essaim en orbite et un voile de givre frappent en continu, un
  son par tick serait un bourdonnement qui masquerait les sons porteurs d'information.
- `WeaponBase.Update` le joue en **un point unique**, après un tir effectif : une arme qui ne trouve
  pas de cible renvoie faux, ne consomme pas sa recharge et ne doit pas s'entendre non plus.
- L'identifiant se résout par le **type exact** (`WeaponRegistry.IdOf`), jamais en remontant
  l'héritage : trois armes dérivent d'une autre (`OverloadAegis : OverloadField`,
  `FusionBlade : PlasmaBlade`, `OrbitalSwarm : DroneSwarm`) et hériteraient de son identité.

**Trois relevés, parce qu'ils tombent en panne séparément** : l'arme est-elle **couverte** par la
table (sinon elle est muette sans que rien ne le dise) · le clip **se charge-t-il** (`CanLoad` — la
cause) · le son **est-il parti** pour cette arme (`PlayedCountOf`, par identifiant : le total global
ne permet aucune attribution). Le diagnostic de scène réelle les imprime arme par arme, **en fin** de
relevé — posé au début, il tombait à la troisième seconde de run et annonçait « 0 » pour tout le monde.

### Le niveau d'un son **ne se règle pas dans le fichier** — Unity renormalise à l'import

**Signalé en jouant (2026-08-10)** : « le son de la volée multiple est trop présent, limite saturé ».
Le réflexe — réencoder le WAV plus bas — **n'aurait rien changé** : tous les clips de la banque sont
importés avec `forceToMono: 1` **et `normalize: 1`** (voir n'importe quel `.wav.meta` de
`Resources/audio/sfx/`), donc Unity ramène leur crête à pleine échelle et efface toute atténuation
gravée dans l'asset. Seuls survivent le **caractère** du son (spectre, enveloppe, durée) et son
rapport RMS/crête.

**Conséquence pratique** : un fichier se conçoit pour ce qu'il *dit* (sourd ou claquant, court ou
traînant) et se **cale à −1 dBFS comme le reste de la banque** ; le **niveau** se règle exclusivement
dans `AudioSystem.MixGainDb`. Le réglage y est de surcroît lisible et comparable, là où il serait
invisible dans un binaire. C'est la même conclusion que côté Godot (où la raison est autre :
conserver l'asset CC0 intact).

**Et le gain se choisit sur la polyphonie réelle, pas sur le niveau du fichier.** La Volée Multiple
partageait exactement le claquement du Canon à Impulsions — deux armes souvent équipées ensemble,
tirant sur le *même* transitoire, la salve enchaînant plusieurs tirs par seconde à recharge réduite.
Ce n'est alors plus deux tirs qui s'entendent, mais une saturation. Corrigé par un son propre
(`sfx_weapon_scatter_shoot`, dérivé Kenney passé au passe-bas et raccourci) **et** −11 dB de mixage.
Troisième cas après le tir de sentinelle et son projectile : **quand un son gêne, compter combien de
fois il part par seconde avant de regarder son niveau**.

### Une réserve de voix **plus généreuse** que celle d'origine sature là où l'original tenait

**Signalé en jouant (2026-08-10)**, dans la foulée du son de la salve : « ça sature aussi avec les
ennemis qui arrivent vers 2 min 30 - 3 min, dès qu'ils sont touchés ». Ce ne sont pas *des* ennemis
mais **une cohorte** : à 2:00 le drone corrompu, puis à **3:00 six espèces de faune d'un coup**
(éclaireur, spectre, étincelle, éclat de glace, glitch), toutes en `erratic_chase`, toutes entre
**11 et 17 PV** — donc mortes au premier coup. Et l'archétype `erratic_chase` joue
`sfx_enemy_drone_die` : **1,36 s et −12,0 dB RMS**, le son le **plus long** et le **deuxième plus
fort** de toute la banque, quand l'autre son de mort de fourrage (`sfx_enemy_swarm_die`) fait 0,16 s
à −21,0 dB. Le plus encombrant sert le plus grand nombre.

**Ce n'est pas une infidélité au portage** — le spawner de Godot résout lui aussi `erratic_chase` vers
`CorruptedDrone.tscn`, donc vers ce son. **La divergence est ailleurs, dans la réserve de voix :**

| | Godot publié | Portage Unity (avant) |
|---|---|---|
| Canaux | **8** | **24** |
| Réserve pleine | vole le plus ancien | ignore le son |

Vingt-quatre voix « pour ne perdre aucun son » ont **triplé l'amplitude atteignable** (+9,5 dB de
somme cohérente), et le vol du plus ancien — que Godot fait par nécessité — coupait au passage les
queues de 1,36 s avant qu'elles ne s'accumulent. Le portage a donc supprimé un garde-fou **que
personne n'avait écrit comme tel** : il était un effet de bord de l'avarice du pool d'origine.

**Parade** : un plafond **par son** (`MaxVoicesPerSfx = 3`) et non un plafond global. C'est la
répétition du *même* clip qui s'additionne — vingt sons différents ne saturent pas, trois copies du
même y suffisent. Au-delà du plafond, la voix la plus ancienne portant ce son est **réutilisée** et
non ignorée : la mort la plus récente s'entend toujours, le son cesse seulement de s'empiler.
⚠ **L'ordre des cas compte** : le plafond doit passer **avant** la recherche d'une voix libre. Vingt
morts simultanées trouvent toujours une voix libre au moment où on regarde, et un plafond consulté en
dernier ne mordrait qu'une fois les vingt-quatre voix prises — trop tard.

⚠ **Un relevé de son ne conclut pas s'il n'y a pas de son.** Sans sortie audio, aucune voix ne
« joue », le comptage rend zéro et un plafond jamais éprouvé passerait pour respecté. Le banc
distingue donc « 3 voix pour 12 déclenchements » de **« NON CONCLUANT »**, au lieu d'afficher vert
dans les deux cas. Vérifié sur `bench.exe` (build réel, audio actif) : **3 voix pour 12
déclenchements**.

**Et la mesure sépare deux choses** : `PlayedCountOf` compte des *déclenchements* — il ne bouge pas
d'un pouce quand le son sature —, `VoicesPlaying` mesure l'*empilement*, c'est-à-dire ce qui
s'additionne réellement à l'oreille. Le premier ne pouvait pas voir ce défaut.

**⚠ Le plafond n'a pas suffi, et la suite dit pourquoi.** Retour suivant, même run : « le son
continue à être empilé au même moment, il faut le modifier — peut-être enlever le reverb aussi ».
L'enveloppe du fichier, tracée par tranches de 50 ms, donne la réponse : `sfx_enemy_drone_die` tient
**−9 à −13 dB pendant 1,1 s puis coupe net**. Il ne décroît pas. Ce n'est pas une explosion avec une
queue de réverbération, c'est un **grondement à niveau constant**, et trois copies d'un grondement
d'une seconde suffisent à faire un mur — le plafond bornait l'empilement à trois, mais trois de
*ça* est encore une nappe continue. À comparer avec `sfx_enemy_colossus_die`, qui chute de −8 à −25
dB en 300 ms : lui est une vraie percussion, et il ne pose aucun problème malgré sa seconde de durée.

**Trois leçons de méthode, et la troisième est la plus chère :**
1. **Un plafond de voix ne rattrape pas une enveloppe plate.** Borner la répétition suppose que
   chaque copie *finit* ; un son qui ne décroît pas ne finit qu'à sa dernière frame.
2. **Ce qu'un joueur appelle « du reverb » est une mesure à faire, pas une métaphore** — ici la
   traînée était réelle et lisible dans l'enveloppe, là où RMS et crête (les deux chiffres regardés
   au premier tour) la manquaient complètement. **Tracer l'enveloppe avant de toucher au gain.**
3. **Corriger l'enveloppe change le niveau** : couper à 0,30 s avec décroissance exponentielle a
   rendu **5 dB de RMS** à lui seul (−12,0 → −17,1). Le gain de mixage a donc dû *remonter*
   (−9 → −6 dB), sans quoi les deux corrections se seraient cumulées et auraient enterré le son.
   Deux réglages qui visent la même grandeur ne s'additionnent pas, ils se recalculent.

**Dernier levier, sur la répétition elle-même** : les morts partent avec ±18 % de variation de
hauteur (`PlaySfx(sfx, pitchVariation: 0.18f)`) et non les 6 % par défaut. À des dizaines de morts
par seconde, c'est l'identité stricte de l'échantillon — autant que son volume — qui s'entend comme
une bouillie au lieu d'événements distincts.

### Un bouton se dimensionne sur son libellé **le plus long**, pas sur celui du repos

Le bouton de remise à zéro change trois fois d'intitulé : « Tout réinitialiser », puis l'avertissement
d'armement (« ⚠ Confirmer ? (irréversible — efface Échos & progression) »), puis « ✓ Réinitialisé ».
Calé sur le premier, il coupait le second — **667 px de texte pour 536 utiles**, relevé au banc — et
demandait donc une confirmation sans dire de quoi. Une capture au repos ne montre pas ce défaut : le
cas où la largeur compte est justement celui qu'on ne photographie pas. → vérification de banc :
`label.preferredWidth` contre la largeur du cadre, **dans l'état armé**.

### Une arme qui tue **sans laisser de trace** se lit « la carte n'a rien fait »

**Signalé en jouant** : « je ne vois pas les autres armes, ni leurs projectiles ». Le relevé en scène
réelle a montré qu'elles fonctionnaient **toutes** — 95 éliminations en 30 s contre 1 avec le seul
canon — mais que **8 sur 12 n'affichaient rien** : arcs, chaînes, auras, faisceaux, cônes et zones
étaient de la logique pure, et les drones de l'Essaim étaient des `new GameObject("Drone0")` sans le
moindre `SpriteRenderer`.

**Ce n'est pas de la finition.** Une progression dont l'effet est invisible est indiscernable d'une
progression cassée, et c'est le pire retour possible sur un choix de carte. → `WeaponVfx` (points de
sprite recyclés : ni shader ni matériau, donc rien qui puisse être supprimé du build) + un critère au
banc : **toute arme qui ne se voit ni par un projectile ni par un drone doit laisser une trace**.

### Un réglage **sans système derrière** survit à son système

Le champ `Discord` vivait dans la sauvegarde, était migré depuis Godot, écrit et relu — et piloté par
personne : `DiscordPresence` n'avait jamais été porté, et l'interrupteur ne figurait même pas dans les
options. Une préférence orpheline ne lève rien et ne se voit pas ; c'est la variante « réglage » des
données déclarées jamais consommées, et la plus discrète, puisque la sauvegarde donne l'illusion que
la fonctionnalité existe.

**Deux règles en sont sorties** :
- une intégration facultative se **trace même quand elle réussit**. Un journal muet ne distingue pas
  « connectée » de « jamais installée » — et c'est la seconde qui était vraie ;
- la préférence est **poussée**, jamais tirée : `DiscordPresence` vit dans `Platform`, qui ne peut pas
  lire les réglages du jeu sans créer un cycle (4ᵉ occurrence). Et il faut un **champ statique** en
  plus de l'appel sur l'instance, les réglages étant lus dès le menu, parfois avant que le composant
  n'existe — sinon la préférence est poussée dans le vide.

⚠ `DiscordRPC` (MIT, donc conforme au « aucun plugin payant ») tire **Newtonsoft.Json** : sans le
paquet `com.unity.nuget.newtonsoft-json`, tout compile et c'est l'**éditeur de liens** qui échoue,
avec un message parlant d'assemblages du projet et non de la dépendance manquante.

### Une règle de difficulté portée sans consommateur rend le cran **payant et vide**

`SaturationTable.HealingMult` et `SaturationTable.LevelUpHealsEnabled` étaient portés, documentés,
couverts par leurs tests unitaires — et **aucune ligne de code de jeu ne les lisait**. Le cran I
« Hémorragie » s'affichait donc dans le sélecteur, se gagnait au déblocage, coûtait un palier au
joueur, et ne retirait **rien**. Trouvé en jouant, 2026-08-09 : « il manque la regen de vie lors d'un
level up ».

Le soin de passage de niveau lui-même n'existait pas non plus : rien, côté Unity, n'écoutait
`XpSystem.LevelUp` sur le joueur. Or c'est la **plus grosse source de soin du jeu** — indexée sur les
PV max, donc sans plafond, et déclenchée en rafale en overtime (~18 niveaux/min × 25 % des PV max).
Le portage tournait avec un joueur amputé de ce filet, sans erreur, sans avertissement, et sans
qu'aucune lecture du code ne puisse le révéler : le fichier qui gouverne la règle existe bel et bien.

**Ce qui rend cette famille invisible** : un test de logique pure vérifie que la règle *dit* la bonne
chose, jamais qu'elle est *appelée*. Le seul contrôle qui l'attrape part d'un **joueur réel** et d'un
**vrai passage de niveau** (`RunSmokeTest.RunHealingChecks`) — c'est-à-dire du seul niveau où « la
règle existe » et « la règle s'applique » cessent d'être la même phrase.

⚠ **Corollaire de nommage** : sous Godot, `Player.Heal(0.25f)` soigne d'un **quart de la barre** ;
sous Unity la méthode homonyme prenait un montant **absolu**. Un appel recopié tel quel aurait rendu
un quart de point de vie — sans erreur, sans avertissement, invisible à la relecture. La méthode
s'appelle désormais `HealFraction`.

### Un cran qui coupe un système **absent** fonctionne parfaitement, sur du vide

L'audit exhaustif de `SaturationTable` face à ses consommateurs Unity a montré que **3 des 6 crans**
de l'échelle étaient totalement inopérants. Le I pour la raison ci-dessus ; les deux autres pour une
raison pire :

- **Cran IV « Sans filet »** coupe trois achats du Hub — `extra_life`, `damage_absorb`,
  `overtime_stabilizer`. **Aucun des trois n'était porté** : ils s'affichaient au Hub, se payaient en
  Échos, et ne faisaient rien. `PlayerStats` n'avait pas de charges, `EnemySpawner` n'amortissait pas
  la pente d'overtime. Le cran fonctionnait donc *exactement comme écrit*, sur un système qui
  n'existait pas.
- **Cran VI « Purificateur »** : `SaturationTable.ChampionDamage` n'était appelé nulle part, alors
  que `EnemyBase.DealDiscreteDamage` — écrit **pour lui**, et le documentant dans son propre
  commentaire — existait déjà.

**Ce qui rend cette variante indétectable en jouant** : un filet absent ne se manque qu'au moment où
il aurait dû servir, c'est-à-dire à une mort qu'on attribue à soi-même ; et un plancher de dégâts
absent se lit « ce cran est mal dosé ». Aucun des deux ne produit d'anomalie.

**La parade est une table, pas une relecture** : pour chaque membre public d'une règle de gameplay
partagée, un consommateur nommé — et un contrôle de banc qui part d'un objet **réel**
(`RunSafetyNetChecks`, `RunPurgeChecks`). Un `Grep` sur `SaturationTable\.\w+` dans `Assets/Scripts`
répond en une seconde à « qui lit quoi », et c'est lui qui a trouvé les trois.

⚠ **Divergence assumée sur la Plaque Adaptative** : sous Godot la charge est consommée **avant** le
test d'i-frames. Or `TakeDamage` est appelé à chaque frame par chaque ennemi au contact — trois
charges partent donc en trois frames, sans que le joueur ait encaissé trois coups. Sous Unity la
charge est consommée **après** les i-frames : au plus une par fenêtre de 0,45 s, ce que la carte
promet (« les premiers **coups** reçus »).

### Appeler la **variante de compatibilité** d'une règle est indétectable là où on regarde

`EnemySpawner` appelait `EnemyScaling.Scaled` — la formule **linéaire historique, conservée pour les
tests de référence** — au lieu de `ScaledCurved`, celle du jeu, dont le fichier dit pourtant en toutes
lettres « utilisé par EnemySpawner ». Le commentaire juste au-dessus de l'appel affirmait « le scaling
vient de la logique pure : mêmes chiffres que sous Godot, par construction ».

Ce qui rend l'erreur si durable est la **forme de l'écart** : ×1,00 à la 5ᵉ minute, ×1,07 à la 8ᵉ,
×1,3 à la 13ᵉ — puis **×2,4 à la 30ᵉ et ×4,6 à la 60ᵉ**. Nul là où on teste, énorme là où le jeu se
joue. Et la partie quadratique existe précisément parce que le build du joueur croît bien plus vite
qu'une droite : la retirer, c'est garantir le décrochage qu'elle corrige.

**Règle** : quand une classe pure expose deux formules dont l'une est dite « historique »,
« compat » ou « de référence », l'appel du jeu se vérifie une fois explicitement. Aucun test ne le
fera : les deux fonctions sont justes, c'est le **choix** entre elles qui est faux.

### Un garde-fou recopié de travers **annule** ce qu'il protège

`EnemyBase.ApplySlow` bornait à `Mathf.Clamp(mult, 0.05f, 1f)` là où `CrowdControlCaps.CapSlowMult`
impose **0,60**. Un ralentissement de 95 % au lieu de 40 % ne se lit pas comme un bug : il se lit
comme « l'arme de givre est très forte ». Idem pour la brûlure, sans plafond du tout face aux 60 dps
de la table. Ces classes existent *pour que slow et DoT ne trivialisent jamais le jeu* — les remplacer
par un clamp local supprime exactement la garantie qu'elles portent.

### Un ennemi qui **kite sans tirer** ressemble à un ennemi prudent

Le portage n'avait **aucun projectile ennemi**. Les archétypes `ranged_kiter` et `cone_kiter`
exécutaient scrupuleusement leur danse — approche, recul, orbite — et ne faisaient jamais feu ; le
boss avait perdu deux de ses trois cadences (`BossPhases.BurstInterval` et `ShockInterval`, portées et
appelées nulle part), ne gardant que sa signature. Un boss qui ne tire pas passe pour un boss de
corps-à-corps, qu'il suffit de contourner.

Deux indices dormaient dans le dépôt sans que rien ne les relie : `sfx_enemy_sentinel_projectile.wav`,
importé et jamais joué, et `SaturationTable.ChampionDamage` documentant un plancher « posé **au
tir** » pour un projectile qui n'existait pas.

⚠ Le drapeau `FromChampion` se pose **au tir**, jamais lu à l'impact : le projectile survit à son
tireur, et un boss mort ne peut plus dire qu'il en était un.

### Un **débit absolu** ne se mesure pas dans une arène que le banc a déjà peuplée

Vérifier que la brûlure est plafonnée à 60 PV/s a demandé **cinq tours de banc**, et le correctif
n'était jamais en cause :

1. **86 PV/s** — le canon du joueur tirait sur la cible de mesure. Joueur écarté à l'autre bout.
2. **90 PV/s** — soupçon d'un `WaitForSeconds(0,5)` qui durerait plus longtemps. Faux : mesuré,
   `Time.time` disait bien 0,50 s.
3. **89 PV/s** avec un témoin **jamais brûlé** à `-0 PV` — donc rien ne frappait la *zone*.
4. **87 puis 101 PV/s** sur deux fenêtres successives : le rapport **variait**. C'est ce fait-là qui
   tranche — un `deltaTime` mal compté donne toujours le *même* facteur, une arme à cadence propre
   non.
5. **68 PV/s** une fois les armes du joueur désactivées. Le banc instancie les vingt et une armes
   dans un contrôle antérieur, et celles qui visent par **liste de cibles** plutôt que par distance
   continuent de frapper — ce qu'aucun témoin *ambiant* ne peut voir, puisqu'elles touchent leurs
   cibles et pas la zone.

**Trois règles en sortent** :
- une mesure de **débit** exige de neutraliser explicitement les sources actives (`w.enabled = false`
  sur les armes), pas seulement d'éloigner le joueur ;
- **deux fenêtres successives** valent mieux qu'une longue : un facteur constant et une pollution à
  cadence propre ne se distinguent que par la *stabilité* du rapport ;
- privilégier la mesure **comparative** — deux cibles, l'une au plafond, l'autre bien au-delà —
  qui prouve la règle sans rien supposer de l'arène. C'est elle qui a été juste du premier coup.

⚠ Et la leçon de méthode : j'ai écrit « c'est un vrai défaut » au tour 3, sur la foi d'un témoin qui
ne pouvait pas voir la pollution. Un témoin ne vaut que pour la classe de perturbation qu'il est
capable de capter.

### Un test qui **se désactive lui-même** est pire qu'un test absent

`UnEnnemiDeBiomeNApparaitQueDansLeSien` cherchait un ennemi dont le champ biome était renseigné, n'en
trouvait aucun — la table lisait `biome` au singulier là où `enemies.json` déclare `biomes` au
pluriel — et sortait sur `if (biomeSpecific == null) return;   // rien à vérifier`.

Il **passait**, comptait dans les 626, et rassurait. Pendant ce temps le filtre de biome ne
s'appliquait à personne : **26 ennemis sur 31** portent un tag, les cinq champions apparaissaient dans
les cinq biomes, et tout le chantier « faune par biome » était inopérant.

**Règle** : une sortie anticipée « rien à vérifier » dans un test est une **assertion déguisée**. Si
l'absence de données rend le test vide, c'est cette absence qu'il faut affirmer
(`Assert.True(tagged > 20, …)`). Et un test de filtrage doit **comparer deux cas** : compter les
éligibles d'un seul biome ne voit rien, puisque le pool n'était pas vide — il était trop large.

### Deux ramassables qui partagent un sprite produisent un rapport de bug sur une **autre** mécanique

Le gabarit de l'orbe d'XP prenait « le premier sprite trouvé » du dossier `pickups` — qui se trouve
être `pickup_noyau_idle_01`. Tous les orbes portaient donc l'apparence d'un **Noyau d'Aether**, et les
trois `pickup_xporb_*` ne servaient à personne.

Le symptôme rapporté n'a rien à voir avec les sprites : « *je ne vois pas le nombre de noyaux
évoluer… ça incrémente de 1 après en avoir ramassé un certain nombre* ». Le compteur était **exact
depuis le début** ; c'est ce que le joueur croyait ramasser qui ne l'était pas. Le Noyau est le seul
objet du jeu qui ne s'aspire pas — le confondre avec l'orbe qui vient tout seul supprime la décision
qu'il porte.

⚠ **Un chargement « premier trouvé » est une bombe à retardement** dès qu'un second asset entre dans
le dossier. Nommer le sprite (`LoadSpriteNamed`) coûte une ligne ; le banc vérifie désormais que deux
ramassables n'ont jamais le même.

### Un état qui n'a **pas de durée** appartient à sa victime, pas à sa source

La Sentinelle Cryo posait `player.SpeedMultiplier = 0.5f`. Rien ne le restaurait : le joueur restait à
moitié vitesse **pour le reste de la run**, et tuer le champion n'y changeait rien — le
ralentissement ne lui appartenait plus. Rapporté comme « en tuant un mid-boss le joueur perd beaucoup
de vitesse ».

Le jeu d'origine a un canal dédié, `ApplyChill(mult, durée)`, **volontairement séparé** du
multiplicateur de vitesse : le power-up Célérité écrit dans ce dernier, donc un gel effaçait la
Célérité — et une Célérité qui s'achève aurait effacé le gel. **Deux sources qui s'écrasent au lieu de
se multiplier.** Le plancher (0,35) existe pour qu'une nappe de plaques ne cloue jamais sur place.

**Règle** : tout effet temporaire porte sa durée dans le même appel que sa valeur. Un `= valeur` nu
sur un multiplicateur partagé est un bug en attente, et il ne se voit qu'en jouant longtemps.

### Un tampon de fraîcheur écrit par la **release** ment à tous les builds suivants

`build_sha.txt` — la ressource dont le jeu tire son tampon `v2.0.0-<sha>`, et que le script de
release relit avant de pousser — n'était écrite **que** par `release_unity.ps1`. Elle **restait là**
ensuite. Tout build local ultérieur affichait donc le SHA de la **dernière release** : pas « dev »,
pas un avertissement, le numéro d'un commit qui n'était pas celui du binaire.

Constaté le **2026-08-10** : un binaire reconstruit à l'instant annonçait un commit de la veille,
précisément pendant qu'on cherchait à savoir si le build testé contenait un correctif audio — la
question exacte que ce tampon existe pour trancher. **Un garde-fou de fraîcheur qui se trompe est
pire que pas de garde-fou : on lui fait confiance.** Même famille que « la stabilité d'un artefact ne
prouve pas sa fraîcheur » (`docs/PITFALLS.md` §Export .NET), à un cran de plus : ici l'artefact
*affirmait* une identité.

**Parade** : `BuildBench.StampGitSha` pose l'identité **avant chaque build**, donc l'acte qui produit
le binaire est le seul à pouvoir le nommer. Trois valeurs, trois sens distincts :

| Tampon | Signification |
|---|---|
| `4b1c37c` | commit exact, binaire reproductible |
| `4b1c37c+` | arbre de travail modifié — **ne correspond à aucun commit** |
| `dev` | git indisponible : une ignorance avouée, pas un SHA périmé |

⚠ **Le suffixe doit exclure ce que la release pose elle-même.** Au moment où `release_unity.ps1`
construit, il vient d'écrire `ProjectSettings.asset` (numéro de version) et `build_sha.txt`, et les
commitera juste après : ces deux-là sont en avance sur le commit courant **par construction**. Sans
l'exclusion, toute release — même parfaitement propre — s'auto-déclarerait sale. C'est ce qui était
arrivé à l'avertissement `$dirty` de l'étape 2, déclenché à chaque fois et donc jamais lu ; il a été
supprimé au profit d'un contrôle à l'étape 4, sur le tampon du binaire **réellement produit**.

⚠ Et le nouveau signal doit être **consommé** : un `+` fait désormais avertir le script de release.
Poser une information que rien ne lit est le défaut le plus fréquent de ce portage.

### L'icône de l'exécutable se pose **au build**, pas dans l'éditeur

Le binaire sortait avec l'icône **d'Unity** : dans la barre des tâches, l'explorateur et le raccourci,
le jeu portait le nom du moteur qui l'a construit — la première image que voit un joueur, avant même
le menu. Un réglage fait à la main dans l'éditeur ne vaudrait que sur un poste et se perdrait au
premier clone ; l'icône est donc un asset versionné, appliqué par le script de build.

⚠ Unity exige une texture **lisible** (`isReadable`) : sans cela l'appel est **silencieusement
ignoré** et le binaire garde l'icône du moteur, sans erreur ni avertissement. Le réglage d'import est
donc forcé par le script lui-même, un `.meta` qu'on croirait suffisant ne l'étant pas.

### Un drapeau de banc appliqué **à moitié** ne produit pas du bruit, il produit un résultat flatteur

`--start-at=13` avançait l'horloge de la run mais **pas celle du spawner**. La campagne d'overtime a
donc rendu **0,0 dégât subi** et **100 % de temps soutenable** sur trois minutes de jeu : le
personnage était bien à la treizième minute, arsenal saturé — et la faune en face était celle de la
**première seconde**, quelques ennemis de niveau zéro balayés par un build de niveau 20. Résultat
net, reproductible, cohérent, et entièrement faux.

C'est la variante moteur de la constante recopiée dans un outil de banc. **Toute horloge dérivée doit
suivre le drapeau**, et le seul contrôle qui l'aurait vu est de comparer deux grandeurs qui devraient
bouger ensemble : le niveau du joueur *et* la population d'ennemis.

⚠ Corollaire de lecture : `--start-at` mesure une **borne haute**. Le personnage y démarre nu, sans
les PV ni les cartes qu'un joueur aurait accumulés — sauf qu'en overtime il regagne cent niveaux en
trois minutes, ce qui le rend rapidement **intouchable**. Un « zéro dégât » sous ce drapeau est un
fait de gameplay à confirmer sur un biome dur, pas une preuve d'équilibrage.

### La culture d'un processus headless n'est pas celle qu'on croit

Le premier relevé de boss est sorti avec des durées en « 9,8s » et un pourcentage de PV écrit
**« 66 ٪ »** — le signe pourcent *arabe*. Le format `P0` et l'interpolation `{x:0.0}` empruntent tous
deux leur séparateur et leurs symboles à la culture courante, laquelle n'a rien à voir avec la langue
du jeu ni avec celle du poste.

**Tout ce qui s'écrit dans un journal se formate en culture invariante** — pas seulement la ligne CSV.
Une virgule décimale casse en silence chaque outil qui relit le fichier, et un signe pourcent exotique
rend le bloc illisible ; dans les deux cas le journal ne se distingue plus d'un journal absent. ⚠ Unity
fige le langage à C# 9 : `string.Create(CultureInfo.InvariantCulture, $"…")` n'est pas disponible, il
faut passer par une fonction de formatage.

### Un journal se choisit **par moteur**, et lire le mauvais ne lève rien

Godot écrit sous `%APPDATA%\Godot\app_userdata\`, Unity sous `%USERPROFILE%\AppData\LocalLow\`. Les
deux fichiers coexistent, portent le même nom et le même format : un outil pointé sur le mauvais ne
produit pas d'erreur, il produit **les chiffres de l'autre campagne**, tout aussi plausibles. D'où le
choix explicite (`--engine`) plutôt qu'une détection.

Et les deux moteurs **ne se comparent pas** : leurs générateurs divergent dès le premier tirage, donc
une même graine n'y donne pas la même run. Une campagne appariée à cheval sur les deux mesurerait
l'écart entre les moteurs.

### Un banc doit ouvrir lui-même la porte qu'il vient franchir

Le pilote automatique s'installait, annonçait qu'il prenait la main… et le jeu attendait au **menu
principal** qu'on clique sur « Jouer ». Le journal restait vide, et la seule trace était une ligne de
log disant que tout allait bien. Un drapeau de banc doit lancer la run, et la **quitter** : une
campagne enchaîne des dizaines de runs, et tuer un processus au chronomètre est exactement ce qui
tronque un journal en cours d'écriture.

### Un décor se pose **dans l'ordre** — la structure d'abord, ce qui l'évite ensuite

Les motifs de sol (`FloorFeatures`) rendent l'ensemble des **cellules qu'ils occupent**, et c'est
cette liste qui décide où les obstacles et les fenêtres vitrées n'ont pas le droit d'aller. Les poser
avant la structure — ou ne pas se servir de sa sortie — donne un pilier planté au milieu d'une coulée
de lave et un puits de parallaxe ouvert dans une rivière : deux défauts qu'aucun test ne peut voir,
et qui sautent aux yeux sur la première capture.

**Trois pièges de placement, tous trouvés à l'image et tous de la même famille** — *une forme n'est
pas un point* :
- **Tester le seul centre ne suffit pas.** Une fenêtre de 128 px dont le centre tombe à côté d'une
  rivière la chevauche quand même sur la moitié de sa surface. → contrôle sur l'**emprise**.
- **Écarter d'une structure large demande plus de marge qu'on ne croit.** Le jeu publié décale un
  obstacle de ±192 px au plus ; une rivière de trois tuiles flanquée d'une poche en couvre près de
  250, si bien qu'un pilier sur trois restait dans le courant. Recopier la constante d'origine
  reconduit son défaut.
- **Écarter d'une chose rapproche des autres.** En éloignant les fenêtres des motifs, trois d'entre
  elles se sont retrouvées superposées : elles ne font alors pas trois ouvertures mais une tache
  d'hexagones enchevêtrés. Une contrainte ajoutée sur un placement aléatoire en demande souvent une
  seconde.

⚠ **Un anneau à quatre segments est un losange, pas un carré** : ses sommets tombent sur les axes. Le
contour des nœuds du Néon pivotait donc de 45° par rapport à la boîte sombre qu'il devait border.

⚠ **L'axe Y s'inverse entre les moteurs.** La ligne 0 de la grille est en haut ; reprendre le calcul
de Godot tel quel retourne toutes les structures — sans que cela se voie, une rivière renversée
restant une rivière, jusqu'au jour où un tracé asymétrique le trahit.

**Ce que la séparation géométrie / rendu apporte** : sous Godot, tracé et dessin vivaient dans la
même classe de nœuds, et « la rivière traverse-t-elle vraiment l'arène ? », « reste-t-elle dans la
bande que la caméra montre ? », « laisse-t-elle la place au reste ? » ne se vérifiaient qu'à l'œil.
`FloorFeatureLayout` est pur, donc tout cela est devenu des tests — le rendu n'a plus qu'à colorier.

### Un commentaire affirmatif n'est pas une lecture du code d'origine

Le port avait figé le boss sur place — `case BossCore: return self;` — avec le commentaire « le boss
ne poursuit pas : il tient sa position ». **C'était faux.** `src/Entities/Boss/RustedCore.cs` fait
`Velocity = toPlayer * Speed` à 46 px/s (×1,18 en phase III) et ne s'arrête que pendant la surcharge.
Symptôme en jouant : « le boss se voit mais n'approche pas, il reste bloqué en haut de l'écran ».

Rien ne le contredisait : phases, incarnations, signatures et adds fonctionnaient tous, et
`enemies.json` déclarait pourtant une vitesse de **46** — une donnée qui n'aurait aucun sens pour une
entité immobile. **Une valeur de données inutilisée est un indice de portage manquant.**

**Parade** : rayon d'apparition dédié (`BossSpawnRadius`, 380 px — son arrivée est un événement) et
un repère au HUD (phase, cap, distance). La barre seule dit « il existe » sans dire « où ».

### Raccourcir le temps imparti n'abrège pas la construction du build

« La barre de vie du boss ne baisse pas » — mesuré : elle descend de **99,6 % à 95,3 % en 20 s**. Le
Noyau a 5 115 PV et son TTK a été calibré côté Godot sur ~488 DPS (trois armes L20 + fusion) ;
affronté à la 60ᵉ seconde avec un build de niveau 9, il encaisse ~11 PV/s, soit **sept minutes**.

`--run-duration` doit donc s'employer **avec** `--saturate-arsenal` : avec un arsenal réel, le même
boss tombe en moins de 15 s. Un outil qui abrège une dimension du jeu sans abréger les autres ne
mesure plus ce qu'on croit — même famille que `--start-at`, qui donne un arsenal saturé sur un
personnage nu, donc une **borne haute**.

Et une barre qui descend de 0,2 %/s se lit « elle ne bouge pas » : afficher le **pourcentage en
chiffres** ne relève pas du confort, c'est ce qui distingue « lent » de « cassé ».

### L'ordre d'initialisation n'est pas garanti

Godot garantit l'ordre des AutoLoads (déclaré dans `project.godot`) ; Unity ne garantit rien entre
`MonoBehaviour`s. → un **hôte unique** (`PlatformHost`, `RunBootstrap`) qui ordonne explicitement,
plutôt que des réglages d'ordre d'exécution dispersés que personne ne pense à consulter.

⚠ Piège **inverse** de Godot : là-bas, `base._Ready()` devait être appelé **en dernier** dans les
19 armes. Ce piège disparaît, remplacé par le couple `Awake`/`OnEnable`/`Start`.

---

## Langage et bibliothèques

### Unity 6.5 fige le langage à **C# 9**

Malgré un SDK .NET 8 embarqué. `record struct` (C# 10) est refusé, et `IsExternalInit` — requis par
tout accesseur `init`, donc par tout `record` — n'existe pas en netstandard2.1.

**Audit du projet** : 2 occurrences en tout. Le code est de fait déjà quasi C# 9.

### `System.Text.Json` **est** fourni par Unity 6.5

Contrairement à ce qu'on croit souvent : `Editor/Data/BCLExtensions/` l'embarque, avec
`System.Collections.Immutable` et `System.Text.Encodings.Web`. **Aucune migration vers Newtonsoft
n'est nécessaire.** Vérifié jusqu'en IL2CPP, sérialisation par réflexion comprise (14/14).

### uGUI est un **paquet**, pas un module

`com.unity.modules.ui` ne fournit que le module bas niveau. `Canvas`, `Image`, `Text` viennent de
**`com.unity.ugui`**, absent d'un projet créé par `-createProject`.

**Symptôme** : `error CS0234: The type or namespace name 'UI' does not exist in the namespace
'UnityEngine'`. Ajouter le paquet **et** référencer l'assembly `UnityEngine.UI` dans l'`.asmdef`.

---

## Build et outillage

### Bee met les **échecs** en cache — un échec instantané n'est pas un échec

**Symptôme** : après un premier échec de build IL2CPP, les tentatives suivantes « échouent » en
**1,3 seconde**, sans rien recompiler, avec le même message.

**Parade** : supprimer `unity/Library/Bee` avant de conclure quoi que ce soit. Dans notre cas, le
build passait ensuite en 39 s — le diagnostic « Unity 6.5 ne supporte pas VS 2026 » était faux.

### Unity retient le toolset MSVC **le plus récent**, même incomplet

**Symptôme** : `fatal error C1083: Cannot open include file: 'vcruntime.h'`.

**Cause** : deux installations Visual Studio coexistaient ; celle de VS 2026 avait `bin` et `lib`
mais **aucun dossier `include`**. Unity l'a préférée à des Build Tools 2022 complets.

**Diagnostic** : comparer `VC\Tools\MSVC\<version>\include\vcruntime.h` sur chaque toolset.

### Un script d'éditeur ne peut pas installer le paquet dont il utilise les types

Chicken-and-egg : un fichier référençant `UniversalRenderPipelineAsset` ne compile pas tant qu'URP
est absent — donc il ne peut pas être celui qui installe URP. → **deux fichiers** :
`SetupUrpPackage` (API du gestionnaire de paquets uniquement) puis `SetupUrp` (configuration).

⚠ Ne jamais épingler la version d'un paquet en dur : laisser le gestionnaire résoudre celle qui
correspond à l'éditeur, sinon la première montée de version d'Unity casse le projet.

### Le pipeline doit être affecté **dans Graphics ET Quality**

Sinon Unity continue silencieusement d'utiliser le pipeline intégré, et les lumières 2D n'ont
**aucun effet, sans le moindre message**.

---

## Assets

### `spritePixelsPerUnit = 1` — la décision la plus structurante du portage

Godot travaille en **pixels** comme unité de monde. Choisir 1 px = 1 unité Unity fait que toutes les
valeurs du jeu — vitesses (380), rayons de contact (24), demi-arène (960) — se transposent **telles
quelles** sur ~24 300 lignes. Toute autre valeur imposerait un facteur de conversion à chaque
coordonnée, c'est-à-dire une classe entière de bugs silencieux.

### Le postprocessor d'import doit exister **avant** le premier import

Les défauts d'Unity (filtrage bilinéaire, compression avec perte) **détruisent** du pixel art.
Importer 905 PNG avant d'écrire l'`AssetPostprocessor`, c'est les réimporter après.
⚠ Incrémenter `GetVersion()` force le réimport quand les réglages changent — sinon les assets déjà
importés gardent les anciens.

### Un `.asset` référence ses sprites par **GUID**

Générer du YAML Unity depuis un script externe reviendrait à deviner des identifiants gérés par
l'AssetDatabase : faux en silence, avec pour seul symptôme des animations à trous visibles en
jouant. → conversion en **deux temps** : Python produit des manifestes neutres, un script d'éditeur
Unity résout les références.

### `enemies_biome_expansion.json` **n'est pas un fichier de données** — ne pas le charger

**Symptôme potentiel** : 20 ennemis de biome deviennent **invisibles**, sans la moindre erreur.

**Cause** : le fichier ressemble à un jeu de données à fusionner avec `enemies.json`. Il n'en est
pas un — **aucun code du jeu ne le lit** ; il sert de document de conception à
`tools/generate_new_enemies.py`. Ses 20 entrées existent **déjà** dans `enemies.json`, à une
différence près : elles n'y portent **pas** de `framesPath`. Le fusionner « pour être complet »
écrase donc les chemins de sprites du fichier principal.

**Parade** : ne charger que `enemies.json`. Un test verrouille la découverte
(`EnemyTableTests.LeFichierDExtensionNeDoitPasEtreFusionne`) et signalera si le fichier change de
nature.

⚠ Cas général à retenir : dans `data/`, **tous les fichiers ne sont pas des entrées du moteur**.
Vérifier qui les lit (`grep -rn <fichier> src/`) avant de les porter.

### Cohabitation des deux importeurs

`unity/.gdignore` (Godot n'y descend pas) **et** `<Compile Remove="unity/**/*.cs" />` dans le
`.csproj` Godot (le SDK globbe `**/*.cs`). Vérifié avec 905 PNG en place : le cache Godot reste
inchangé, 3 720 entrées avant comme après.

---

## Fidélité au moteur d'origine

### `randf()` de Godot est en **simple précision**

Mener le même calcul en `double` — ce qui paraît strictement meilleur — fait diverger le port
d'environ **1e-8 dès le premier tirage**. Un test verrouille le piège, qui serait sinon « corrigé »
un jour par quelqu'un cherchant de la précision.

### Les courbes d'interpolation de Godot ont des singularités **voulues**

`Expo/In` vaut **0,999** à t=1, pas 1. `Expo/InOut` vaut **0,50025** à mi-course, pas 0,5. Elastic et
Back **sortent** de [0, 1]. Les « corriger » romprait la parité visuelle sur 502 sites d'appel.

### Ce qui reste **non reproduit**, et qu'il faut savoir

`randf_range` (surcharge `double` de `GD.RandRange`) : 11 formulations testées, aucune ne
correspond. Les runs Unity restent reproductibles entre elles, mais une comparaison Godot↔Unity sur
une même graine **divergera** dès le premier appel. Concerne 8 sites, tous des positions de
ramassables.

---

## Interface

### Un réglage d'import ne s'applique **pas** à un asset déjà importé

Les cadres « plaque blindée » étaient importés à **1 px/unité** comme le reste du projet. Or une
`Image` uGUI met ses bordures 9 zones à l'échelle `referencePixelsPerUnit / spritePixelsPerUnit`,
soit **×100** : les coins d'un cadre de 48 px se dessinaient sur 4 800, il ne restait rien à étirer,
et l'interface était méconnaissable. C'est ce qui avait fait **abandonner** les textures.

Le postprocessor qui corrige ce réglage existait **et n'a jamais rien corrigé** : il ne s'exécute
qu'à l'import, et les fichiers étaient déjà en place. Réparer demande de toucher le `.meta` —
l'éditer conserve les GUID, le supprimer les change (`touch` ne suffit pas).

⚠ **Corollaire sur les vérifications.** Le banc contrôlait que les cadres existaient et portaient une
bordure. Les deux étaient vrais. Il ne contrôlait ni leur **échelle**, ni qu'un bouton les
**affiche** — donc une fabrique d'interface qui dessinait un rectangle plat passait tous les tests.
Les trois contrôles vivent maintenant ensemble : présence, échelle (100 px/unité), et sprite
réellement posé sur un bouton construit.

### `RectTransform` naît en 100 × 100 — et un conteneur de défilement en hérite

Étiré entre deux ancres horizontales sans remise à zéro de `sizeDelta`, un contenu vaut « largeur du
parent **+ 100** » : il déborde de 50 px de chaque côté de sa fenêtre, et le masque rogne les
premières lettres de chaque ligne. Le symptôme se lit comme une **faute de texte**, jamais comme un
défaut de mise en page. Touchait les cinq écrans à liste.

### Une ancre en pourcentage ne se résout pas dans un conteneur dont la largeur est calculée

Positionner des colonnes avec `anchorMax.x = 0.62` à l'intérieur d'un défilement donne un résultat
faux tant que la largeur du parent n'est pas connue. Les colonnes se placent par **disposition
explicite** (`HorizontalLayoutGroup` + `LayoutElement`), avec `preferredWidth = 0` et
`flexibleWidth = 1` sur la colonne extensible — sinon la largeur *préférée* de son texte (une
description de 1 500 px sur une ligne) pousse tout le reste hors du cadre.

### Seul `Resources/` est atteignable à l'exécution

L'illustration de couverture et les drapeaux de langue étaient bien dans le projet, sous `Art/` —
donc hors de portée d'un `Resources.Load`. Le menu s'affichait sur un aplat uni avec un titre en
police monospace, et rien dans le code ne signalait l'absence.

**Rejoué à l'identique avec les 43 icônes d'armes** (2026-08-05) : mêmes fichiers présents sous
`Art/sprites/ui/`, même absence sous `Resources/`, et **aucune table** ne reliait un identifiant à
son pictogramme. Cartes de montée de niveau, Codex et arsenal du HUD n'affichaient que du texte. La
parade est désormais vérifiée au banc : *chaque arme et chaque greffe a son icône* (`UiIcons`), et
elle échoue si l'on ajoute une arme sans son entrée.

### Le focus clavier doit **se voir** — sinon il n'existe pas

Signalement : « on ne peut pas se déplacer dans les menus au clavier ». Le relevé dit l'inverse : la
sélection passait bien de `Button_Jouer` à `Button_Hub` puis `Button_DÉFIS` à chaque flèche. **C'est
le signal visuel qui manquait**, et l'effet pour le joueur est rigoureusement le même.

Godot superpose **trois** signaux (`src/UI/UiStyle.cs`, §3.2), et le dit explicitement : « le focus
ne repose ainsi jamais sur la seule teinte ».

1. **la teinte** — cadre **violet**, *quel que soit l'accent du bouton* ;
2. **la forme** — le cadre **déborde de 3 px** (`SetExpandMarginAll`) ;
3. **le mouvement** — l'opacité pulse de 60 % à 100 % sur 0,6 s.

Le portage n'avait qu'une variante `_focus` **de la couleur du bouton**, plus ±18 % de luminosité.
Aggravé par un menu où chaque entrée portait une couleur différente : « plus lumineux que son
voisin » ne se compare pas entre deux teintes. Le jeu publié met tout le menu en cyan sauf
« Quitter » — cette uniformité **est** ce qui rend le focus lisible.

Côté Unity, l'anneau vit sur **son propre objet** enfant : peint sur l'image du bouton, il subirait
le `SpriteSwap` des états (survol, pressé) et disparaîtrait au moment précis où il sert.

### La molette n'atteint une zone de défilement que par un **`Graphic` raycastable**

Signalement : « la molette ne fonctionne pas dans le menu options ». La sensibilité était pourtant
juste (`UiStyle.ScrollSensitivity = 160`, réglée précisément parce que le défaut d'uGUI — *un* pixel
par cran — se lit « cassé »). Ce n'est pas le réglage qui manquait, c'est la **chaîne** qui l'amène.

uGUI ne route un cran de molette que vers ce que le rayon du pointeur **touche**, et un rayon ne
touche que des `Graphic` dont `raycastTarget` est vrai. Une fenêtre de défilement construite **par
code** n'a aucun graphique : elle ne porte qu'un `RectMask2D`. Le pointeur tombait donc :

- sur un **libellé** → l'événement remontait la hiérarchie jusqu'au `ScrollRect`, et ça marchait ;
- sur le **vide** entre deux lignes → il touchait le fond d'écran, qui n'est pas dans la hiérarchie
  du `ScrollRect` : rien.

Aux Options, faites surtout d'espace libre entre un libellé à gauche et son contrôle à droite, cela
revenait à « ça ne marche pas ». **Les six écrans à liste étaient touchés** (pause, hub, niveaux,
options, codex, défis).

Parade : `UiStyle.ConfigureScroll` pose une **`Image` transparente** (alpha 0, `raycastTarget = true`)
sur la fenêtre — exactement le rôle de l'`Image` du *Viewport* dans la zone de défilement fournie par
l'éditeur, que le portage avait omise. Une `Image` d'alpha nul reste une cible de rayon tant qu'on ne
règle pas `alphaHitTestMinimumThreshold`.

⚠ **Ce qu'une vérification doit regarder** : contrôler `scrollSensitivity` n'aurait rien montré. Le
banc vérifie donc, pour chaque écran, que la **fenêtre** porte un graphique raycastable
(`CheckScrollWheel`). Même famille que les cadres 9-slice « présents et jamais affichés ».

### `StandaloneInputModule` **désélectionne** à chaque clic dans le vide

Un clic qui ne tombe sur aucun élément appelle `SetSelectedGameObject(null)`. Le joueur qui clique
une fois à côté d'un bouton n'a plus **aucune** sélection : les flèches ne font alors plus rien du
tout jusqu'à ce qu'il reclique sur un bouton. Godot conserve le focus. D'où `UiFocusGuard`
(`RuntimeInitializeOnLoadMethod`, un objet persistant) : il rétablit la dernière sélection valide,
et traite au passage le cas où l'élément sélectionné **disparaît** (écran fermé, liste reconstruite).

### Le texte est flou tant que le canevas n'est pas en `pixelPerfect`

uGUI place ses sommets en coordonnées flottantes. Une colonne centrée, une hauteur de bouton impaire
ou une marge de mise en page suffisent à poser une ligne de texte sur un **demi-pixel** : la police
est rééchantillonnée et les glyphes bavent. Aucun réglage de police n'y change rien — c'est le
canevas qu'il faut corriger (`Canvas.pixelPerfect = true`, cf. `UiCanvas.Configure`, source unique
des quatre réglages de canevas qui étaient recopiés dans les onze écrans). En complément,
`fontRenderingMode: HintedSmooth` sur la police.

Le défaut est **invisible au code** et saute aux yeux sur une capture agrandie ×3.

### Deux colonnes parallèles ne s'alignent pas : il faut une ligne par entrée

L'arsenal du HUD posait une colonne d'icônes **à côté** d'un bloc de texte multiligne, en devinant
l'interligne d'uGUI. Le décalage était invisible sur une arme et valait **deux lignes entières** sur
dix. Une mise en page ne se devine pas : une ligne = un conteneur portant son icône *et* son
libellé, empilés par un `VerticalLayoutGroup`.

Corollaire trouvé au même endroit : `HUD.Place()` pose un pivot **haut-gauche**. Réutiliser les
mêmes ancre et position avec un pivot différent décale l'élément de la **hauteur entière** du bloc —
soit, ici, hors du champ où on le cherchait.

### Un contrôle doit avoir la **nature** de ce qu'il règle

Symptôme : l'écran d'options « ne ressemble pas au jeu » alors que chaque libellé est juste. Le
portage y empilait des boutons pleine largeur « Étiquette : valeur », qu'on clique pour faire défiler.
Trois pertes, toutes invisibles au code :

- un **volume** n'a plus de course : on ne voit pas où l'on se trouve entre 0 et 100 %, seulement le
  palier courant ;
- un **état** ne se lit plus par sa forme mais par un mot (« Activé »), donc plus d'un coup d'œil ;
- rien ne distingue plus **ce qui se règle** de **ce qui s'annonce** : toutes les lignes ont la même
  silhouette.

Le jeu publié emploie un curseur, un interrupteur et une liste déroulante, avec le **libellé à gauche
et le contrôle à droite**. Les textures existaient déjà dans `Resources/UiFrames`
(`ui_slider_grabber`, `ui_toggle_on/off`) — comme les cadres blindés et les icônes avant elles,
elles étaient importées et inutilisées.

⚠ Deux pièges d'assemblage rencontrés en les posant :

- **La poignée d'un `Slider` ne fixe que sa largeur.** Unity pilote son ancrage horizontal ; lui
  donner une hauteur en dur l'étire en rectangle. Elle s'ancre en `(0,0)-(0,1)` avec un `sizeDelta`
  de `(largeur, 0)`, et sa zone se rétrécit d'une demi-poignée à chaque bout, sinon elle déborde de
  la piste aux extrémités.
- **Le `graphic` d'un `Toggle` est ce qu'Unity affiche quand la case est cochée.** Le laisser vide
  donne un interrupteur qui ne bouge jamais — coché ou non, la même image.

### Une `Image` en mode `Tiled` ne se répète pas quand les PPU divergent

La vignette d'un biome (tuile de sol répétée) s'affichait en **aplat uni**. Une `Image` uGUI
dimensionne sa tuile d'après `referencePixelsPerUnit / spritePixelsPerUnit` : à 100 pour 1 — les
valeurs du projet —, une tuile de 32 px se dessine sur 3 200 et une seule remplit tout. Même famille
que les bordures 9-slice à ×100. Ici la parade est de **ne pas répéter** : agrandie au filtre point,
la tuile montre son motif, ce qui est exactement ce que la vignette doit dire.

### Des textes posés à hauteur fixe doivent réserver leur cas le plus long

Sur la carte d'un biome, quatre lignes sont posées à des ordonnées fixes. La description du Secteur
Néon et la règle d'un cran passent toutes deux **à la ligne** : serrées, les deux derniers textes se
chevauchaient. Le défaut n'apparaît que sur un contenu particulier — celui qu'on ne regarde pas en
premier sur la capture.

### Masquer une entrée non acquise n'a de sens que si la découverte est l'objet de l'écran

Le Codex anonymise ce qu'on n'a pas croisé (« ? ? ? ») : c'est justement ce qu'il apporte. Appliquer
la même règle aux **perks de départ** aurait été une erreur de portage — un perk verrouillé est un
**but**, et cacher sa description revient à cacher au joueur ce qu'il gagnerait à aller le chercher.
Le jeu publié le dit dans son `PerksScreen` : « aucune entrée masquée, la description reste visible
pour donner un but ». D'où le paramètre `hideWhenLocked` plutôt qu'un quatrième onglet qui recopierait
la mise en page.

⚠ Piège de données au passage : `StartingPerks` porte des chemins d'icône en **`res://`**, hérités de
Godot et sans aucun sens ici. C'est la table d'identifiants (`UiIcons`) qui fait foi, et rien ne le
signalait — la vérification de banc couvre désormais les perks en plus des armes et des greffes.

### Un zéro en dur est une mécanique morte que rien ne signale

En branchant les Noyaux d'Aether, trois arguments passés à **zéro littéral** sont apparus dans
`RunHud`, chacun posé faute de source à brancher au moment du portage :

| Argument | Conséquence, entièrement muette |
|---|---|
| `cores: 0` (défis) | « Moissonneur de Noyaux » **inaccomplissable** |
| `cores: 0` (fin de run) | « Noyaux d'Aether : 0 » toujours affiché, **part des Échos perdue** |
| `graftsEquipped: 0` | « Pleine Chimère » **inaccomplissable** |

Rien ne les distingue d'une valeur juste : le jeu tourne, les écrans s'affichent, les défis restent
gris. C'est le même mode de défaillance que l'arme de départ absente du HUD — un appel qui *existe*
et ne transporte rien. **Quand un port laisse un argument à zéro « en attendant », il faut le noter
avec ce qui le débloquera** : sans cela, la dette devient invisible le jour même.

### 60 s de jeu ne sont pas 60 s de montre

Vérification du spawn périodique des Noyaux (45 s) : **zéro Noyau après 62 s de jeu réel**. Le
spawner paraissait cassé. Le chrono à l'écran donnait la réponse — **33 s de temps de jeu** seulement
s'étaient écoulées : chaque montée de niveau ouvre une modale à `Time.timeScale = 0`, et le bot,
qui ne validait aucune carte, laissait le jeu figé.

Le compteur n'avance donc que pendant le jeu, et **c'est juste** : un Noyau ne doit pas apparaître
pendant qu'on choisit une carte. Mais toute vérification chronométrée doit en tenir compte — soit en
validant les modales, soit en lisant le chrono du jeu plutôt que sa propre montre. Relancé avec
`Entrée` périodique : **t = 45 s, 90 s, 135 s**, à la seconde.

⚠ Ce que la même session a montré et qui reste ouvert : en **150 s de jeu, aucun Noyau ramassé**. La
règle est celle de Godot (position aléatoire dans l'arène, aucune aspiration), mais un objet qui
apparaît à 800 px du joueur toutes les 45 s peut passer une run entière inaperçu. À juger en jouant,
pas au banc.

### Un ramassable qui ne s'aspire pas ne se teste pas en attendant

Le Noyau d'Aether se prend **au contact** (20 px, jusqu'à 70 avec `core_magnetism`), là où les orbes
d'XP viennent au joueur. Un banc qui pose le Noyau et laisse tourner ne prouverait donc rien — il
mesurerait l'absence d'aspiration, pas la présence du ramassage. La vérification **fait marcher le
joueur** vers l'objet, et contrôle les deux faces : le compteur reste à zéro à 200 px, puis monte au
contact.

C'est le piège des ramassables « walk-over » déjà connu du projet, sous une forme un peu différente :
là c'était l'`Area2D` qui exigeait un mouvement physique, ici c'est la mécanique elle-même.

### Un sprite d'interface chargé dans le monde est cent fois trop petit

Le Noyau d'Aether — sujet de **deux plans sur six** de la cinématique — était invisible : on ne voyait
que ses particules. Même image que l'icône du HUD, mais importée depuis `Resources/Ui/` à **100 px par
unité** ; posée dans le monde à l'échelle 5,5, l'icône de 32 px mesurait **moins de deux pixels**.

La parade est une copie sous `Resources/Vfx/`, dossier importé à 1 px = 1 unité comme le reste du
monde. C'est la troisième forme du même piège de PPU, après les bordures 9-slice à ×100 et la tuile
`Tiled` qui ne se répétait pas : **le PPU d'un sprite dépend de l'endroit où il sera affiché, pas de
ce qu'il représente**.

### Une caméra 2D laissée à l'origine ne voit rien

Les sprites de la cinématique n'apparaissaient pas. Aucune erreur, et le texte — qui vit dans le
canevas — s'affichait parfaitement par-dessus : le symptôme se lit donc comme « les sprites ne se
chargent pas », alors qu'ils étaient là. La caméra était restée en `z = 0`, dans le même plan qu'eux,
donc derrière son plan de coupe. La scène de jeu la reculait à `-10` ; la scène d'intro, écrite
séparément, ne le faisait pas.

### Une cinématique doit ignorer les entrées de sa première demi-seconde

L'intro se passait toute seule avant d'avoir affiché une image : l'activation de la fenêtre par le
système suffit à déclencher `Input.anyKeyDown`. Pour un joueur, c'est une touche restée enfoncée au
lancement — et il ne comprend pas ce qu'il vient de rater. Une garde de 0,6 s l'évite sans rendre le
saut moins immédiat.

⚠ Corollaire pour le banc : **ne pas activer la fenêtre** avant de capturer une cinématique, et caler
les clichés sur le rythme réel des plans plutôt que sur une estimation — un log par plan
(`[Intro] plan n/6 à t = …`) coûte six lignes et évite de conclure à un défaut de rythme qui n'existe
pas.

### `ScrollRect.scrollSensitivity` vaut 1 par défaut — soit un pixel par cran

Sur une liste de trente entrées, il faut alors des dizaines de tours de molette pour descendre, et le
défilement se lit comme **cassé**, pas comme lent. Une ligne du Codex mesure une cinquantaine de
pixels de référence : trois lignes par cran (160) est le geste attendu. Les six écrans qui défilent
recopiaient les mêmes lignes de configuration — et la sensibilité manquait dans les six, ce qui est
la signature d'un réglage qu'on ne pense pas à écrire parce qu'il ne s'écrit nulle part.

### Une valeur RENVOYÉE et ignorée finit par produire un chevauchement

`UiStyle.Header` renvoie l'ordonnée du bas de son liseré, précisément pour que l'appelant s'y cale.
Le Codex l'ignorait et posait ses onglets à une ordonnée devinée : le trait passait **derrière** eux,
à deux pixels près. Le symptôme — « les onglets sont mal calés par rapport à la ligne » — est
exactement ce qu'on obtient quand deux éléments se positionnent chacun de leur côté au lieu de se
chaîner.

Depuis, tout ce qui suit l'en-tête part de cette mesure : onglets, bandeau d'introduction, haut de la
liste.

### Une liste sans séparateur se lit comme un bloc

Trente entrées de deux lignes chacune, empilées sans filet : l'œil ne sait plus où finit une créature
et où commence la suivante, et les **deux lignes d'une même entrée** paraissent appartenir à deux
entrées différentes. Le jeu publié règle cela par un cadre autour de chaque carte ; sur une liste de
trente, un filet d'un pixel coûte moins cher à l'œil qu'un cadre — mais l'absence des deux ne se
répare pas par de l'espacement.

### Deux écrans qui nomment la même chose doivent lire la même table

Le HUD écrivait `IMPULSE CANNON` — l'identifiant technique rendu lisible — là où l'écran de pause
disait « Canon à Impulsions ». Deux noms pour la même arme, sur deux écrans que le joueur ouvre à
quelques secondes d'intervalle. Le repli sur l'identifiant avait été écrit « en attendant la table de
localisation » ; elle était déjà chargée ailleurs. D'où `UiNames`, dans `Platform` pour que le HUD
(`Gameplay`) et la pause (`UI`) y accèdent tous deux.

⚠ Piège au passage : `collection.Contains(chaîne)` résout vers `MemoryExtensions.Contains` et exige
un `StringComparison` — c'est bien la collection qu'on interroge, pas le texte. `Enumerable.Contains`
lève l'ambiguïté.

### L'ordre d'empilement se raisonne par CHEMIN d'ouverture, pas par écran

Les Options portaient l'ordre 96, l'écran de pause 110. Ouvertes depuis le menu principal, elles
s'affichaient correctement ; ouvertes **depuis la pause** — ce que le jeu publié permet — elles
apparaissaient *derrière*. Le joueur voyait le voile s'assombrir sans savoir que l'écran demandé
s'était bien ouvert, dessous.

La règle : un ordre d'empilement se choisit d'après **tous** les écrans depuis lesquels on peut
ouvrir celui-là, jamais d'après sa place « logique » dans une liste.

### Un panneau dont la hauteur suit le contenu chevauche son propre cadre

Le bloc vital du HUD faisait 152 px pour un contenu qui s'arrêtait à 140 : la dernière rangée — les
emplacements de greffes — mordait sur le liseré. Un cadre « plaque blindée » porte une bordure
9-slice de **16 px** : la hauteur utile n'est pas celle du panneau. Elle se calcule
(`SlotsTop + SlotSize + marge`) plutôt que de s'ajuster à l'œil, sinon toute modification du contenu
rouvre le défaut.

### Une forme géométrique ne remplace pas un sprite qui existe

Les orbiteurs de la Nuée Symbiotique étaient quatre **carrés blancs teintés**. La greffe promet
« 4 mini-essaims de rouille vivante » : c'est une créature arrachée à un ennemi, et c'est tout le
propos de l'Assimilation — le joueur doit reconnaître ce qu'il porte. Le sprite de l'ennemi source
existait déjà dans le projet.

⚠ Le jeu publié dessine ici des losanges, faute d'y avoir accès autrement ; reproduire la *forme*
plutôt que l'*intention* aurait raté le sujet. Le repli, lui, reste un losange — un carré tourné d'un
quart de tour — parce qu'à défaut de sprite, la forme ne doit pas être celle d'un bloc de décor.

### La parallaxe se lit dans l'écart entre les couches, pas dans leur contenu

Le sol glissait d'un bloc sous le joueur, et l'arène paraissait plate. Trois couches aux facteurs du
jeu publié suffisent à la profondeur : motif très lointain (0,06), poussière lointaine (0,55),
poussière de premier plan (**1,35**, qui devance la caméra — un facteur supérieur à 1 n'est pas une
erreur, c'est ce qui place une couche *devant* le plan de jeu).

⚠ Deux points d'implémentation :

- **`LateUpdate`, jamais `Update`** : la caméra suit le joueur dans son propre `LateUpdate`, et lire
  sa position trop tôt fait *trembler* les couches d'une frame de retard — défaut qui ne se voit
  qu'en mouvement.
- **La couche la plus lointaine doit rester discrète** (7 % d'opacité ici). Plus appuyée, ses grandes
  formes se lisent comme des dalles posées sur le sol : l'œil les prend pour du terrain et cherche à
  les contourner.

⚠ **Non porté** : la brume animée et les rais de lumière, qui demandent un échantillonnage par
fragment. La parallaxe est la part qui produit la profondeur.

### Une table parsée n'est pas une table branchée

`GraftTable` lisait les **fusions de greffes** — recette, jauge dédiée, points par archétype — depuis
le premier jour du portage. Rien ne les consommait à l'exécution : ni routage de kill, ni proposition,
ni consommation des deux sources. Résultat en jeu : les greffes s'**empilaient** côte à côte et leurs
effets s'additionnaient, là où le design en fait fusionner deux en une (**occupation 2 → 1**, un
emplacement libéré, et la seule raison de viser un couple).

Le symptôme se lit comme un choix d'équilibrage, pas comme un manque : le jeu tourne, les greffes
marchent, elles sont juste trop nombreuses. Même famille que les icônes présentes mais non reliées,
et que le `cores: 0` en dur — **du code qui existe et ne sert pas est indiscernable d'un code absent,
sauf en jouant**.

Deux pièges d'assemblage sont apparus en le branchant :

- `GraftForGauge` renvoie `null` pour une jauge de fusion. L'écran d'assimilation, qui s'en servait
  seul, se **refermait aussitôt en refusant tout seul** : la fusion, plus long objectif du jeu, était
  perdue à l'instant où elle s'offrait.
- Le message « les emplacements sont pleins, la plus ancienne cédera sa place » est **faux** pour une
  fusion, qui en libère un. L'afficher ferait refuser la meilleure offre du jeu par crainte de perdre
  autre chose.

### Un repli silencieux qui sert quatre fois n'est plus un repli

`SpriteFramesLibrary.ForEnemy` tombe sur un jeu d'animations de secours quand l'ennemi n'a pas le
sien — sécurité voulue : un asset manquant ne doit rendre personne invisible. Sauf que la **faune de
base** (Essaim, Drone, Sentinelle, Colosse Greffé) n'a pas de `framesPath`, et que son identifiant ne
correspond pas au nom de son asset (`corrupted_drone` contre `drone`). Les quatre y tombaient : trois
ennemis aux comportements distincts s'affichaient avec le sprite de l'Essaim, dès la première seconde
de jeu.

Rien ne le signalait — **un sprite était bien affiché**. La parade est une table d'alias, et une
vérification qui compare l'asset retenu au repli : « 31 ennemis, aucun sur le repli ».

### Les séquences échappées du CSV doivent être converties à la lecture

L'importeur de traductions de Godot transforme `
` en saut de ligne. Le portage lit le CSV **brut**
et ne le faisait pas : les deux caractères s'affichaient littéralement au milieu des phrases — sur les
six lignes de la cinématique d'ouverture, c'est-à-dire sur le **seul texte narratif du jeu**. Aucune
erreur, aucun test rouge : une faute de frappe apparente, dans trois langues.

Un test balaye désormais la table entière, pas une entrée choisie : c'est la seule façon de couvrir
les lignes qu'on n'a pas pensé à regarder.

### Le HUD est un écran comme les autres — il se compare aussi à la référence

Les huit écrans avaient été confrontés à `docs/ui_v1160_*.png` ; le **HUD**, non — parce qu'il n'a
pas de capture dédiée. Il s'en trouve pourtant une dans `ui_v1160_levelup.png`, et la comparaison a
montré quatre manques, dont aucun n'est décoratif :

- **pas de panneau** : les barres étaient posées à nu sur le sol de l'arène, illisibles dès qu'une
  tuile claire passait dessous ;
- **pas de PV chiffrés** : une barre dit une *proportion*, jamais une *marge* — or « il me reste un
  coup » ne se lit pas sur une fraction ;
- **pas d'emplacements de greffes** : la chimère est le troisième axe de progression du jeu et
  n'apparaissait nulle part pendant la run ;
- **barre de vie ROUGE** au lieu de verte : une barre pleine y paraît déjà critique, et il ne reste
  plus de couleur disponible pour dire « je vais mal ».

### Un assemblage ne peut pas remonter vers celui qui le référence

`UI` référence `Gameplay`. Dès que le HUD (dans `Gameplay`) a eu besoin des cadres blindés, l'appel à
`UiStyle` est devenu impossible — le compilateur ne dit pas « cycle », il dit `The name 'UiStyle'
does not exist`, ce qui envoie chercher une faute de frappe.

C'est la **deuxième fois** dans la même journée (après `UiCanvas`). La règle qui s'en dégage : ce que
plusieurs assemblages partagent descend dans `Platform` — d'où `UiFrames` (chargement, cache, pose du
9-slice) dont `UiStyle` n'est plus qu'une façade. La règle « aucun style ad hoc » y gagne : elle
tient maintenant aussi pour le HUD, qui aurait sinon chargé ses textures à la main.

### Un `Register` silencieux prive le HUD de l'arme de départ

`InventorySystem.Register` remplissait la table sans émettre `WeaponChanged`. Or `RunBootstrap`
enregistre l'arme de départ **après** que le HUD a lu l'inventaire — vide à cet instant. L'arme avec
laquelle le joueur commence sa run n'apparaissait donc nulle part de toute la partie, et le HUD ne se
peuplait qu'à la première carte prise.

---

## Effets visuels

### Une valeur d'effet **ne se transpose pas** d'un moteur à l'autre — elle se recalibre sur capture

Les lueurs ont été portées avec les nombres de Godot : `PointLight2D` de texture 32 px à
`TextureScale = 2,2 + niveau × 0,45`, soit 185 px de diamètre à niveau 8. À l'écran, en Unity,
**d'énormes flaques cyan noyaient l'arène** — le mélange additif de Godot passait par son éclairage
2D, celui d'Unity est un sprite additionné directement à l'image.

Le vrai piège n'est pas le facteur, c'est **le cumul** : une arme à tir rapide envoie un *flux* de
projectiles, et dix halos qui se superposent en additif saturent au blanc. **Un effet ne se juge
jamais sur un exemplaire isolé.** Valeurs retenues, calibrées sur capture : lueur de projectile
`14 + niveau × 2,5` px de diamètre à alpha `0,12 + niveau × 0,02` ; flash d'impact `8 + niveau × 2,5`
px de **rayon** (contre 130 en transposition littérale).

### Sur un sprite en **PPU 1**, `localScale` est un facteur — pas une taille en pixels

Troisième occurrence, et la plus visible : `EnemyStatusFx.FlameSize = 18f` était commenté « côté
d'une langue de feu, **en pixels** » et servait de multiplicateur à `VfxPrimitives.Spark`, un disque
de **16 px**. Chaque langue couvrait donc **288 px** — sur un essaim de 16 px, le joueur ne voyait
plus l'ennemi, seulement « de grosses explosions permanentes ». Les deux précédentes : les drones
tracés au runtime, et les motifs de sol.

**Un effet porté par une entité se dimensionne en fractions de son corps**, jamais en pixels
absolus : le même code doit habiller un essaim de 16 px, un champion de 72 et un boss de 154.

- La mesure est `SpriteRenderer.bounds` — en espace monde, elle tient déjà compte de l'échelle et du
  sprite courant.
- ⚠ Elle peut être **muette** : tant qu'aucune image d'animation n'est posée, elle rend zéro. Le
  repli n'est pas une constante devinée mais le **rayon de contact** de l'entité (`PushRadius`), et
  la mesure est **retentée** tant qu'elle a échoué — la figer au premier appel donnerait la même
  taille de flammes à tout le bestiaire.
- ⚠ Un enfant hérite de l'échelle de son porteur : une mesure en pixels du monde doit être divisée
  par `lossyScale` avant d'être posée en local, sinon un champion à 1,5 porte des flammes 1,5 fois
  trop grandes — la même confusion, un cran plus loin.

### Un état qui dure a besoin d'un signal qui dure — la fumée, pas le flash

La brûlure ne se lisait que par ses langues de feu, qui se confondent avec les impacts d'armes. Une
**traînée de fumée** émise pendant tout le poison de chaleur dit ce qu'aucun flash ne dit : *ça brûle
encore*. À la différence de la traînée de givre, elle **ne demande pas que la cible avance** — les
seules cibles qui portent un état assez longtemps sont les plus lentes, c'est-à-dire les grosses.

⚠ Elle est **additive** comme tout le reste, donc elle éclaircit là où de la vraie fumée
assombrirait. C'est le bon choix et non un pis-aller : les arènes sont sombres, une volute sombre y
serait invisible. Ce qui la fait lire comme de la fumée est sa teinte **grise désaturée**, sa lenteur
et son étalement (`Vfx.Puff` : dérive + croissance sur la durée de vie).

⚠ **Réglée sur capture, à la baisse.** À 0,3 s d'intervalle et alpha 0,34, une nuée en flammes
couvrait le sol d'un **voile laiteux continu** : chaque bouffée est discrète, leur cumul ne l'est
pas. Retenu : 0,45 s, alpha 0,20, rayon 0,18 × la largeur du corps. Et son budget est **plus serré**
que celui des éclats de givre (90 contre 150) : une bouffée vit trois fois plus longtemps, elle
mangerait seule le vivier partagé et ferait disparaître les traces d'armes.

⚠ **Photographier un état demande de le maintenir.** Une nuée enflammée « une fois pour toutes »
donne une capture pleine d'arrivants **intacts** au premier plan — ce qui se lit « l'effet ne marche
pas ». `ScreenshotTour.KeepBurning` réapplique à chaque image *pendant* la pose, et le relevé
accompagne l'image (`72/72 en flammes — corps 32 px (mesure), flammes 28 px`) : sans lui, on doserait
un effet qui n'a jamais été appliqué.

### Un effet qui dit un ÉTAT doit dire sa FORCE — sinon deux armes se ressemblent

Le gel était binaire : l'ennemi virait au bleu, point. La Lance Cryogénique (−20 %) et le Voile de
Givre (−45 %) produisaient donc **exactement la même image**, alors que l'écart entre les deux est
tout ce qui justifie de prendre la seconde. Un état visuel booléen répond à « qui est touché ? » et
laisse sans réponse « qu'est-ce que ça change ? ».

`EnemyStatusFx.Render` reçoit désormais le **multiplicateur de vitesse**, pas un booléen, et en tire
l'intensité du givre : `FrostFloor + (1 − FrostFloor) × InverseLerp(1, 0,5, mult)` — ×0,80 → 0,70,
×0,55 → 0,95.

⚠ **Le plancher n'est pas du confort.** Doser « à proportion » (−20 % → 20 % de teinte) rendrait
l'effet invisible pour l'arme qui en a le plus besoin : la Lance touche peu de cibles à la fois. Le
dosage sert à *distinguer* deux forces, pas à en effacer une.

⚠ **Prise sèche, fonte lente.** Le givre monte instantanément (c'est le seul retour qui dise « ce tir
a porté sur celle-ci ») et redescend en ~0,35 s. Une extinction aussi sèche que la prise ferait
**clignoter** la nuée entière au rythme des recharges, les deux armes cryo réappliquant leur gel en
boucle. Même raison pour la gerbe de prise, émise au **front montant** seulement.

⚠ **La cadence, elle, suit le modèle et jamais la fonte** : la décoration peut survivre à l'état, pas
l'information tactique.

### Un ralentissement se lit d'abord dans la CADENCE, pas dans la couleur

Le signal le plus parlant du gel ne dessine rien : `FrameAnimator.SpeedScale` suit le multiplicateur
de vitesse de la victime. Sans lui, un sprite qui s'agite à pleine cadence tout en avançant deux fois
moins vite se lit « **il glisse** » — un défaut d'animation — et non « il est ralenti ». Il coûte une
affectation, ne prend rien au vivier partagé d'effets, et reste lisible sur un ennemi de 16 px au fond
d'une nuée, là où quatre pixels de givre ne le sont pas.

C'est aussi la **seule part d'un effet d'état qu'un banc puisse constater** : le reste est en pixels.
D'où `CadenceScale`, et une vérification que le gel **relâche** sa victime — une cadence jamais rendue
serait un ralentissement permanent, c'est-à-dire un bug de gameplay déguisé en effet visuel.

⚠ Un plancher est nécessaire (0,35) : `ApplySlow` borne le multiplicateur à 0,05, et recopié tel quel
il donnerait un sprite **immobile**, que le joueur lit « l'animation est cassée ».

### Le feu court, la glace tient — deux états ne se distinguent que par leur MOUVEMENT

À teinte égale de luminosité, deux nuages de particules se lisent comme le même effet. Ce qui sépare
la brûlure du gel est leur grammaire : les langues de feu **montent et se renouvellent**, les cristaux
sont **immobiles et ne font que scintiller** ; la fumée **s'élève et s'étale**, la vapeur froide
**retombe et se dépose** (`Vfx.Puff` avec une vitesse d'élévation *négative* et une croissance faible).
Choisir la même dynamique pour les deux reviendrait à ne porter qu'un seul état en deux couleurs.

⚠ **Du givre pousse SUR un corps.** Poser la base du cristal à sa distance d'orbite et le laisser
croître vers l'extérieur le place entièrement **hors** de la silhouette : la capture montrait des
planches blanches flottant à côté d'ennemis intacts. Il est centré sur l'orbite, donc à cheval sur le
bord du corps.

⚠ **Un signal ajouté oblige à rebaisser les anciens.** La traînée d'éclats avait été calibrée quand
elle était le *seul* signe de mouvement du gel (alpha 0,9, 11 px). Avec les cristaux et la vapeur, une
nuée gelée devenait un tapis de taches blanches où l'on ne distinguait plus les ennemis → 0,32 et
7 px. C'est le piège de cumul de la fumée, une couche plus loin : **on ne dose jamais un effet seul,
on dose la scène**.

### Un relevé qui CITE au lieu de COMPTER cache exactement ce qu'on lui demande

Le relevé joint aux captures d'état décrivait le **premier** ennemi venu : `corps 48 px (REPLI)`.
Vrai pour lui seul, et impossible à distinguer d'un échec de mesure généralisé. Passé au compte
(`4/18 corps mesures (32-48 px)`), il a révélé en une ligne que **la nuée posée à la main n'avait
aucun sprite** : `ScreenshotTour` instanciait le gabarit du spawner sans lui poser son jeu
d'animations — méthode privée du spawner — si bien qu'on photographiait des effets d'état portés par
des ennemis **inexistants à l'image**, et qu'on jugeait leur taille là-dessus.

*Quatrième occurrence de « un asset présent n'est pas un asset affiché », et la première où c'est
l'instrument de jugement lui-même qui était en cause.*

### Un anneau affirme une FRONTIÈRE — une aura n'en a pas

Toutes les auras du jeu étaient rendues par un cercle (`Vfx.Ring`) plus un halo. Le cercle a un
défaut de fond : il dessine une limite, donc il donne à un Voile de Givre exactement la forme d'un
Champ de Surcharge, d'une Nova, ou de n'importe quelle portée d'arme. Rien n'y dit le *givre*, dont
le propre est justement de ne pas avoir de bord.

`AuraCloud` superpose des bouffées (`VfxPrimitives.Glow`, additif, `OrderGround`) sans jamais tracer
de trait : réparties en spirale (un tirage uniforme fait des trous et des grumeaux), à rayon
**respirant** (sans quoi elles tournent sur des orbites fixes et le nuage redevient un anneau), et
d'autant plus pâles qu'elles sont extérieures — ce dégradé remplace le contour.

⚠ **Persistant, jamais repeint à chaque tir.** À 0,35 s de recharge, une aura redessinée à chaque
coup *clignote* : le joueur lit « ça se déclenche » là où l'effet est permanent. Et elle ne prend
rien au vivier partagé, qu'une aura continue viderait à elle seule.

⚠ **Une aura de zone n'est pas un croissant.** `FusionBlade` héritait du balayage de la lame avec un
demi-angle de 180° — soit un cercle complet, quatre fois par seconde, autour d'un champ qui n'a pas
de bord, et tournant au hasard de la cible la plus proche alors que l'arme frappe partout. Le tracé
du coup est donc redéfinissable (`PlasmaBlade.DrawSweep`) : ce qui est juste à 80° cesse de l'être à
360.

### La prudence sur l'opacité peut rendre un effet **invisible**

Les deux nuages ont été calibrés à 0,07-0,085 par bouffée, par report direct de la leçon de la fumée
(« en additif, c'est le cumul qui décide »). Sur capture : **rien**. La leçon ne s'appliquait pas —
la fumée se cumule parce qu'elle est **réémise en continu** et finit par couvrir le sol, un nuage à
effectif **fixe** n'a que ses recouvrements. Retenu : 0,22 (givre) et 0,17 (irradiation).

⚠ Et le relevé doit dire lequel des deux cas on regarde. « Je ne vois pas le nuage » ne distingue pas
*trop pâle* de *jamais créé* — deux causes opposées dont une seule se corrige en montant l'opacité.
D'où `AuraCloud.PuffCount`/`RadiusPx` joints à la capture (`nuage de givre 12 bouffees sur 150 px`,
ou `ABSENT`).

### Un ombrage cuit suppose une lumière FIXE — donc ce qui tourne ne peut pas être ombré

Le brief pseudo-3D pose une lumière venue du haut-gauche, une ombre en bas, un contact assombri au
sol. Une pièce qui pivote emporte cette lumière avec elle et trahit l'illusion à chaque changement de
cible.

La parade n'est pas un contournement mais la bonne lecture de l'objet : séparer la **matière** de
l'**énergie**. Le châssis d'une tourelle ne tourne pas, il est ombré et porte une ombre au sol ; son
canon pivote librement parce qu'il est lumineux — une émission n'a pas de face éclairée.

⚠ En espace texture, `y` croît vers le **haut** : le contact est en `y - minY` et la lumière en
`maxY - y`. Inverser les deux donne un objet éclairé par le sol — discret, et immédiatement « faux »
à l'œil sans qu'on puisse dire pourquoi.

⚠ Les coefficients agissent en **TSV**, sur la valeur et la saturation, jamais en RVB : la contrainte
dure du brief est que la *teinte* ne bouge pas. Un assombrissement RVB désature vers le gris et fait
virer un objet cyan au bleu sale, ce qui se lit « mauvaise couleur » et non « dans l'ombre ».

### Un effet qui écrit `Time.timeScale` doit le **rendre**, et depuis un objet qui survit

Signalé en jouant : « le ralenti reste actif après la mort du boss au lieu de revenir à la vitesse
normale ». Cette famille de bug ne casse rien, ne lève rien, et rend le jeu injouable pour le reste
de la session.

Quatre règles, toutes nécessaires (`HitStop`) :

1. l'avance est portée par **`PlatformHost`**, qui survit aux changements de scène — pas par la
   caméra ni par le boss, tous deux détruits *pendant* l'effet qu'ils déclenchent ;
2. elle se compte en temps **non mis à l'échelle** : compter en temps de jeu pendant qu'on ralentit
   le jeu allonge l'effet dans les mêmes proportions — à 8 %, il dure douze fois trop longtemps ;
3. la vitesse rendue est **`SceneRoot.ResumeScale`**, jamais `1,0` : une campagne de banc en temps
   accéléré retomberait sinon à la vitesse normale, silencieusement ;
4. rien n'est écrit **pendant une pause** : la pause possède `timeScale`, et en sortir restaure déjà
   la vitesse nominale.

⚠ Et la valeur d'origine ne se recopie pas : 0,1 s à 5 % (le `HitStop` de Godot) fait **5 ms** de
temps de jeu — un hoquet, pas un ralenti. Il faut une **tenue** puis une **remontée progressive** ;
un ralenti sans remontée n'est qu'un blocage.

### Une donnée déclarée et jamais consommée, **troisième et quatrième fois**

Après `projectileCount` et la table de fusions, deux nouvelles :

- **`RailOvercharged`** : `weapons.json` déclare `burstCount: 3`, `burstInterval`,
  `cooldownBetweenBursts` et `projectileSpeed: 600`. Le portage tirait **un** projectile à 800 px/s.
  Sa seule différence avec le canon de base était sa cadence — la fusion la plus lisible du jeu
  ressemblait à une amélioration de statistique. Le symptôme rapporté était « il se voit mal ».
- **La Ruche de Tourelles** : le portage lisait `fireIntervalSec` et `rangePx`, deux clés qui
  **n'existent pas** dans `grafts.json`. La greffe tournait donc entièrement sur les valeurs par
  défaut du code, et tout ce que le fichier déclarait (`cooldownSec`, `targetRangePx`,
  `anchorRadiusPx`, `followSpeedPx`, `lifestealFraction`, `contactDamage`) était ignoré en silence.

**Rien ne peut le signaler** : des valeurs plausibles sortent quand même. La seule parade est de lire
la donnée en face du code qui la consomme, clé par clé.

⚠ Et un effet **collapsé** est encore plus discret qu'un effet absent : les quatre tourelles étaient
réduites à « tirer quatre projectiles depuis le joueur, vers la même cible, à la même image ». La
greffe *fonctionnait*, elle coûtait une jauge entière, et rien à l'écran ne disait qu'on la portait.

### L'aléatoire d'un effet ne doit **jamais** venir de `UnityEngine.Random`

`Random.Range` partage son état avec le jeu. Une campagne de banc lancée sur une graine fixe verrait
ses tirages de gameplay **se décaler selon le nombre d'éclairs dessinés** — un décor qui change le
déroulé d'une run. `Vfx` porte donc son propre `System.Random`.

### Sur un paramètre générique, `item == null` ne voit pas le « faux null » d'Unity

Dans `Rent<T>(...) where T : MonoBehaviour`, C# résout `==` en égalité de **référence** : un objet
détruit avec sa scène passe pour vivant. Il faut convertir explicitement
(`(UnityEngine.Object)candidate != null`) pour retrouver l'opérateur d'Unity. Symptôme sinon : les
premiers effets de la run suivante ne s'affichent pas, sans la moindre erreur.

### Un shader atteint seulement par `Shader.Find` peut être **retiré du build**

Le nettoyage de shaders ne garde que ce qui est référencé. Un shader d'effet chargé dynamiquement
doit vivre dans `Resources/` et se charger par `Resources.Load` — sinon il disparaît **uniquement
dans le jeu exporté**, jamais dans l'éditeur, donc jamais pendant les tests. Vérification : le
journal de build doit contenir `Compiling shader "Chimera/VfxAdditive"`.

### Une mécanique invisible n'est pas une mécanique

Le bouclier orbital du Gardien Néon n'absorbe les dégâts que dans le secteur qu'il couvre, et toute
sa réponse est de tourner autour de lui. Sans arc affiché, le joueur ne constate qu'un ennemi qui
encaisse irrégulièrement, **sans pouvoir en déduire quoi que ce soit**. Même défaut sur le sillage de
magma du Colosse (zone de dégâts sans marque au sol) et sur les cinq signatures du boss, qui
frappaient sans rien afficher. Une marque permanente vit sur **son propre objet**
(`ChampionOverlay`) : dessinée par le champion, elle serait saturée au blanc par le clignotement de
dégât, précisément quand le joueur regarde.

### Un anneau se dessine avec `LineRenderer.loop`, jamais avec une polyligne refermée

Une polyligne dont on répète le premier point laisse une **encoche** visible à la fermeture —
immédiatement lisible sur une aura affichée en permanence.

### La parallaxe est une propriété de la CAMÉRA avant d'être une propriété des couches

`RunCamera` calait `orthographicSize` sur `Screen.height / 2` pour tenir le « 1 px = 1 unité » du
portage. En 1920 × 1080 cela cadre **1920 unités de large**, soit l'arène entière (1920 × 1216) : le
cadrage caméra se borne alors à **zéro déplacement horizontal**. Toutes les couches de parallaxe
étaient construites, peuplées et correctement décalées — elles n'avaient simplement **jamais
l'occasion de défiler**. Le symptôme (« il manque l'effet parallaxe ») se diagnostiquait dans la
caméra, pas dans l'atmosphère.

Godot est en `stretch/mode = "canvas_items"` sur un viewport de 1280 × 720 : il montre **toujours
720 unités de haut**, quelle que soit la résolution, et étire le rendu. La caméra de partie fait
désormais pareil (`WorldViewHeight = 720`), ce qui rend aussi au monde sa taille apparente d'origine
— il était rendu aux deux tiers. Le « 1 px = 1 unité » reste vrai pour les **sprites** ; ce qui
change est le facteur d'affichage. Un contrôle du banc verrouille la demi-hauteur à 360.

### `localScale` n'est pas une taille : diviser par `rect.width` seul est faux, et silencieux

La taille monde d'un sprite vaut `rect.width / pixelsPerUnit`. Sur les tuiles du jeu (PPU 1) les
deux coïncident, ce qui rend l'erreur invisible — jusqu'au premier sprite fabriqué au runtime.
`UiPrimitives.White` fait 4 px pour un PPU de 4, soit **1 unité** : un masque dimensionné en divisant
par `rect.width` mesurait **48 px pour 190 demandés**. Toujours passer par un helper
(`ArenaRenderer.ScaleToPixels`).

Le même oubli dans l'autre sens donne l'effet inverse : `localScale = size` sur la tuile « vitre »
(64 px, PPU 1) produit un masque de **6 144 unités**, plus large que l'arène. Les motifs devenaient
alors visibles **partout**, et le défaut se lisait « le masque ne marche pas » alors qu'il marchait
trop.

### Un `SpriteMask` ne retient que les pixels OPAQUES de son sprite

Prendre `tile_floor_glass` comme forme de masque paraît naturel — c'est la tuile que la fenêtre
représente. Mais c'est un **reflet posé sur du vide** : elle est transparente presque partout, donc
le masque se réduisait au seul trait de reflet. Un masque est une **forme**, pas une illustration :
carré plein, toujours.

Rappel des deux autres réglages sans lesquels un masque ne fait rien d'utile :
`renderer.maskInteraction = VisibleInsideMask` sur ce qu'on veut confiner, et surtout
`isCustomRangeActive` + `front/backSortingOrder` sur le masque — sans bornes il découpe **tous** les
ordres de tri, y compris les entités.

### Unity ne sait pas percer un sprite : un « trou » se dessine PAR-DESSUS

Sous Godot le sol est une grille de tuiles dont quelques amas sont remplacés par une tuile vitrée ;
la couche profonde se voit littéralement **au travers**. Le premier portage a traduit ça en plaçant
les motifs sous le sol dans l'ordre de tri — invisibles, le sol étant une surface pleine (une seule
sprite `Tiled`, précisément pour ne pas instancier 2 200 tuiles).

Le trou se fabrique donc à l'envers : un aplat sombre redessiné **au-dessus** du sol aux dimensions
de l'amas (le « fond de puits »), les motifs juste au-dessus, confinés par un masque à cette même
forme, puis le reflet de vitre. Le résultat se lit comme une ouverture parce que l'œil reconnaît un
**cadre + un fond plus sombre + une structure dedans**, pas parce que quelque chose a été découpé.

### Une valeur d'effet ne se recopie pas d'un moteur à l'autre — c'est l'EFFET qu'on porte

Le glyphe profond de Godot trace ses hexagones à `alpha 0.6 / 0.3`, sur le fond parallaxé du monde.
Portés tels quels, ces mêmes traits se lisent **à travers une vitre teintée et sur un puits sombre**,
qui leur mangent la moitié de leur contraste : la valeur juste ici est `0.9 / 0.55`. Même famille que
le cumul additif des VFX — deux moteurs qui composent différemment ne rendent pas la même chose de la
même donnée.

Corollaire de taille : le glyphe mesure ~100 px, donc son `localScale` est un **facteur** (1,0-1,45)
et non une taille. Écrit comme une taille (`Vector3.one * 46..72`), il donnait fortuitement le bon
ordre de grandeur sur un sprite de 3 px — et un motif de 7 000 px sur celui-ci.

### Ce qui se voit par une fenêtre se calibre sur ce qu'il y a derrière

Une ouverture de 96 px devant un glyphe de 175-250 px n'en montre qu'un fragment de trait au milieu
du vide : cela se lit « la fenêtre est vide », jamais « il y a une structure au fond ». Les deux
dimensions se règlent **ensemble** (ici 128-192 px de fenêtre pour ~100-145 px de glyphe).

Et parce que la couche profonde dérive presque avec la caméra (parallaxe 0,06), un motif posé au
centre d'une fenêtre en **sort** dès que le joueur traverse l'arène : il faut un fond dispersé
(22 motifs, comme le jeu publié) sans quoi les fenêtres se vident définitivement au premier
déplacement.

### Une brume se fait au shader, pas en sprites — et deux couches doivent avoir des vitesses distinctes

Une brume faite de sprites doux se trahit par ses bords : on compte les taches. Le bruit fbm
procédural n'a pas de bord et s'anime sans qu'aucun objet ne bouge. Les rais de lumière se posent en
**additif** (`Blend SrcAlpha One`) : en mélange normal, une bande éclaircit le sol vers sa propre
couleur en aplat, ce qui se lit comme de la peinture et non comme un faisceau.

Ce qui donne l'épaisseur n'est pas leur densité mais l'**écart** entre leurs parallaxes (0,35 pour la
brume, 0,15 pour les rais, 0,06 pour les motifs) : au même facteur, elles ne feraient qu'un seul
voile plus dense.

⚠ Ces shaders vivent dans `Resources/Shaders/` et se chargent par `Resources.Load`. Un shader
seulement atteint par `Shader.Find` peut être **retiré du build** par le nettoyage de shaders :
l'effet disparaîtrait uniquement dans le jeu exporté, jamais dans l'éditeur — donc jamais pendant les
tests.

### Ne jamais déplacer le parent après avoir fixé la position MONDE de ses enfants

`BiomeAtmosphere.LateUpdate` fixe `layer.Root.position` (coordonnées monde) pour chaque couche. Une
ligne qui repositionnait ensuite le `transform` parent les décalait toutes d'autant. Elle était
inoffensive parce qu'elle écrivait toujours zéro — le genre de code qui ne casse qu'au premier
déplacement du parent, des mois plus tard.

### Une arme peut « ne pas fonctionner » sans qu'un seul tir ne manque

La Lance Vectorielle tirait, à la bonne cadence, avec les bons dégâts — et le signalement « elle ne
fonctionne pas » était **exact**. Trois manques, aucun visible dans son code :

1. **`Player.AimDirection` suivait la direction de DÉPLACEMENT** (`Velocity.normalized`). La seule
   arme dirigée du jeu tirait donc là où l'on courait : impossible de viser un ennemi sans lui foncer
   dessus, et rien à l'écran n'annonçait où le trait partirait. Godot lit le **curseur souris**, ou le
   **stick droit** dès qu'il sort de sa zone morte, avec mémoire du dernier périphérique — sans cette
   mémoire, une souris immobile ramènerait sans cesse la visée manette vers le curseur. Et il affiche
   un **réticule**, visible seulement quand une arme dirigée est équipée.
2. Le trait ne **perforait** pas, alors que ses données le disent perforant à tous les niveaux.
   `Bullet.Piercing` se pose **avant** `Launch` : le projectile résout sa première collision dès sa
   mise en mouvement.
3. Son **éventail** (2 puis 3 projectiles aux niveaux 4-5) n'était jamais appliqué — voir ci-dessous.

⚠ Le stick droit demande deux axes déclarés dans `ProjectSettings/InputManager.asset`
(`RightStickX` = axe 3, `RightStickY` = axe 4, type joystick). `Input.GetAxisRaw` sur un axe non
déclaré **lève une exception**, à chaque image.

### `WeaponTable` lisait `projectileCount` — et personne ne le consommait

`ApplyWeaponStats` ne reportait que **les dégâts et la recharge**. Les mécaniques du palier — nombre
de projectiles, vitesse, perforation, éventail — étaient analysées depuis `weapons.json` puis jetées.
Conséquence : la Volée Dispersée restait à **2 projectiles au niveau 20**, l'Essaim Traqueur à
2 missiles, la Lance Vectorielle sans éventail. La moitié de la progression de ces armes n'existait
pas — et comme leurs **dégâts** montaient bien, ni un relevé ni un test ne pouvaient le voir.

D'où `WeaponBase.ApplyLevelStats(stats)`, appelé par l'inventaire, que chaque famille d'arme
surcharge pour lire ce qui la concerne. *Une donnée analysée n'est pas une donnée appliquée* — même
famille que la table de fusions parsée et jamais consommée.

### Un repli silencieux sur la donnée fait passer un jeu à moitié traduit pour un jeu qui marche

Tout le contenu nommé (armes, greffes, améliorations du Hub, ennemis) vit dans les JSON de
`StreamingAssets/data`, **écrit en français**. Les écrans affichaient ce texte tel quel. Un joueur
anglophone voyait donc un Hub dont l'en-tête était traduit et les dix-neuf lignes ne l'étaient pas,
un Codex entièrement français, des noms d'armes français dans son HUD — dans un jeu qui se déclare
localisé EN/FR/ES. **Trouvé en regardant les rushes du trailer, pas par un test.**

Trois couches du même défaut, et c'est la troisième qui compte :

1. Le texte affiché venait de la donnée au lieu de `ui.csv`.
2. `ui.csv` portait déjà **109 clés traduites que rien ne lisait** (16 `GRAFT_`, 93 `ENEMY_`), plus
   `LEVELUP_TITLE`, `RUNEND_VICTORY`, `RUNEND_TIME`, `HUD_DASH_HINT`… pendant que les écrans
   écrivaient le français **en dur** juste à côté.
3. Et `UiNames` appliquait déjà la bonne parade — **aux quatre passifs seulement**. Deux règles dans
   une même méthode, et seule la moins visible était la bonne.

Le repli sur la donnée est **volontaire** : mieux vaut un nom français qu'un blanc ou une clé brute.
Mais il est silencieux, et un défaut silencieux dans du texte ne se voit qu'en lisant l'écran dans
la langue concernée — ce que personne ne fait. La parade n'est donc pas dans le code :
**`tools/audit_loc_keys.py`** déclare toute clé absente comme une erreur, et `Platform/ContentText.cs`
construit exactement les mêmes clés. Les deux bougent ensemble.

⚠ Corollaire, payé le jour même : l'audit ne voyait que les **absences**. En ajoutant les clés
manquantes des armes, un préfixe `WEAPON_` complet a été créé… alors que les vingt-et-une entrées
existaient déjà sous **`WPN_`**, lues par les cartes de montée de niveau. Deux tables pour les mêmes
douze armes, et rien pour s'en plaindre. L'audit contrôle depuis les deux sens : toute clé qui porte
un préfixe de la convention sans correspondre à un contenu est signalée **orpheline**.

### Une primitive de dépannage finit toujours par arriver à l'écran

`UiPrimitives.White` — un carré — servait de silhouette aux **drones orbitaux**. Rien ne cassait :
l'essaim tournait, frappait, tuait. Mais cinq carrés blancs en orbite ne se lisent pas comme des
drones, ils se lisent comme un placeholder oublié. Godot dessine un `Polygon2D` à quatre sommets ;
le portage a désormais `DroneSprite` (losange à cœur clair). C'est la **deuxième** occurrence après
les motifs de sol : un repli visuel posé « en attendant » n'est jamais repéré par un test, seulement
par l'œil.

⚠ Corollaire de taille, déjà rencontré : ces sprites tracés au runtime sont en **PPU 1**, donc
`localScale` est un **facteur**, pas une taille. `localScale = 12` sur un sprite de 16 px donne
192 px.

### Un état de combat ne se vérifie pas en regardant l'écran : il se compte

Le gel et la brûlure étaient signalés invisibles. Le premier réflexe — capturer et chercher un ennemi
bleu — n'a **rien donné en une trentaine de tentatives**, et cela ne prouvait rien : l'échec pouvait
venir de l'affichage comme de l'effet.

Le relevé a tranché en deux lignes. Sur 30 s et 88 éliminations :

```
geles=  0/  39   brulent=  0/   9      (instantané / cumulé)
```

**39 gels appliqués, jamais aucun porté par un ennemi vivant** — dans la première minute, la cible
meurt du même coup que celui qui la gèle. L'effet marchait, il n'avait simplement aucun porteur.
Même leçon que la pression ressentie sous Godot : *un événement rare et bref ne se mesure jamais par
échantillonnage*. D'où `EnemyBase.SlowsApplied` / `BurnsApplied`, cumulés, affichés par `--diagnostic`.

Conséquence pratique : ces états ne sont **observables** que sur une cible qui survit — boss,
champion, ou faune de fin de run.

### Teinter ne peut qu'assombrir ; superposer en additif délave

Deux approches ont été essayées pour « bleuir » un ennemi gelé, et aucune ne peut marcher sur une
faune majoritairement **rouge** :

- `SpriteRenderer.color` **multiplie** : un ennemi rouge gelé reste rouge, en plus sombre. Cela se lit
  « il est dans l'ombre ».
- un calque **additif** bleu par-dessus ajoute du bleu au rouge et donne du **rose délavé**.

Il faut *remplacer* la couleur en conservant la luminance (qui porte le relief pseudo-3D), donc un
shader — c'est exactement pourquoi le jeu d'origine en utilise un.

⚠ Le shader avait été écarté par prudence (« il pourrait ne pas rendre sous URP, et son échec
donnerait un ennemi invisible »). La prudence était mal placée : le projet **charge déjà** un shader
maison, `VfxAdditive`, dont tous les effets visibles prouvent le rendu. *Une inquiétude sur une
capacité déjà exercée ailleurs dans le même build se vérifie en trente secondes plutôt que de se
contourner.* Un `Fallback "Sprites/Default"` couvre le cas restant : l'ennemi reste visible,
simplement non givré.

### `Start` n'a pas d'ordre garanti entre objets — ne pas y lire ce qu'un autre `Start` écrit

`RunHud.Start` lisait les améliorations du Hub pour construire les charges de Renouveler / Passer ;
`RunBootstrap.Start` applique les améliorations imposées en ligne de commande. Selon l'ordre — non
spécifié par Unity — les charges valaient tantôt la sauvegarde, tantôt le drapeau. Le symptôme se lit
« le drapeau ne marche pas », alors qu'il marche une frame sur deux.

Parade : lire au **premier usage** (ici, au premier passage de niveau), pas au démarrage.

### Une modale à `timeScale = 0` fige aussi le banc de captures

Un script qui « joue 8 secondes puis capture » produit **deux fois la même image** si une montée de
niveau s'est ouverte entre-temps : rien n'a bougé. Toute vérification qui repose sur un déplacement
(parallaxe, suivi de caméra, spawn périodique) doit fermer les modales — et la même cause avait déjà
fait conclure à tort que les Noyaux d'Aether n'apparaissaient jamais.

⚠ **Et elle revient toute seule** passé la 12ᵉ minute de run : les éliminations font monter de niveau
en continu. Fermer une fois ne suffit pas — `ScreenshotTour.Play()` écarte l'écran **à chaque image**
pendant la pose (`LevelUpScreen.DismissForBench`, et non le bouton « Passer », qui dépend des charges
du joueur et échoue en silence). Pour un portrait, le plus sûr reste de **vider la faune** : plus
d'éliminations, plus d'XP, plus de modale.

### Un effet redessiné à chaque image se cumule **avec lui-même**

L'anneau d'horizon de la Singularité était posé à alpha **0,16** — une valeur qu'on lit comme discrète.
À l'écran, c'était le trait le plus net de toute la scène, un cercle parfait tracé sur l'arène. Le
vortex est reposé **à chaque frame** pour la durée d'une frame et demie : l'anneau se superpose donc
en permanence à lui-même, et l'opacité écrite dans le code n'est pas celle qu'on voit.

La leçon du cumul additif était connue pour des effets **émis en flux** (fumée, impacts). Elle vaut
aussi, et plus fort, pour un effet **persistant redessiné**. Et baisser la valeur n'était pas la
réponse : un cercle affirme une **frontière**, or une singularité n'en a pas. Les bras spiralés sont
passés de polylignes (dont le cœur est tiré à 55 % vers le blanc par `VfxTrace`, donc toujours net) à
des chapelets de lueurs, plus larges et plus vives vers le centre — la même grammaire qu'`AuraCloud`.

### On ne creuse pas un trou en mélange **additif**

Le « cœur sombre » du vortex était un anneau épais de couleur `(0.06, 0.02, 0.12)` censé donner
l'impression d'un trou. En additif, une couleur presque noire n'ajoute presque rien : **il n'a jamais
rien dessiné**, depuis le premier jour, tout en donnant à la lecture du code l'impression que le
centre était traité. Le vide se dit par l'**absence** de matière autour, pas par du noir peint.

### Un contour dit une portée — donc il fait lire un gabarit de visée, pas une arme

Le Flux de Braise doublait son souffle du tracé de son cône, « parce que lui seul dit exactement ce
que l'arme couvre ». C'était vrai, et c'était le mauvais arbitrage : deux segments droits partant du
joueur donnent au feu l'allure d'un **indicateur d'interface**. Un feu n'a pas d'arête ; la couverture
se lit à ce qui brûle, ce qui est aussi la vérité du jeu. Ce que le trait ne pouvait pas donner et qui
le remplace : la **fumée**, qui prolonge le souffle après son extinction et fait lire un incendie
entretenu plutôt qu'une gerbe répétée deux fois par seconde.

### `localScale` normalisé par la **largeur** seule — troisième variante, sur une forme non carrée

Les appendices de chimère se dimensionnent en fractions du corps : `Size × corps / sprite`. Le
diviseur était `rect.width`. L'antenne du Rôdeur est dessinée dans un **6 × 16** : elle sortait donc
**2,7 fois trop haute** — deux tiges roses de 39 px plantées sur un corps de 32. Aucun test ne pouvait
le voir (le compte d'appendices était juste, la mesure du corps aussi) ; la capture l'a montré en une
seconde. Normaliser par `Mathf.Max(width, height)`.

### Une fusion écrite `: SonArmeSource` hérite de son **archétype**

`SolarColumn : PyreStream` ne relevait que quatre champs. La fusion héritait donc du **cône dirigé**,
alors que `weapons.json` déclare pour elle `"type": "radial_burn"` et `"radius": 155` — deux clés
qu'aucune ligne ne lisait. Résultat : la carte la plus rare du jeu était le souffle qu'elle remplace,
un peu plus long. **Cinquième occurrence de « une donnée déclarée n'est pas une donnée consommée »**,
et la plus muette : l'arme tirait, blessait, brûlait, montait de niveau. Rien n'était en panne —
c'était simplement une autre arme. L'héritage est légitime quand la fusion *est* la même mécanique en
plus fort (`IonicStorm`, `FrostVeil`) ; il ne l'est pas quand la fiche annonce un autre archétype.

### Déformer le décor : le rendu se tord, le modèle de collision ne bouge pas

Un vortex dessiné sur une arène parfaitement droite reste une **image posée** : on voit un tourbillon,
on ne voit pas l'espace tourbillonner. `SpaceDistortion` penche, écrase et fait glisser les sprites
d'obstacles — mais le blocage vit dans `ArenaObstacles.Centers`, une liste de points que rien n'y
touche : un pilier penché bloque exactement là où il bloquait. Sans cette séparation, le décor
mentirait, ce que le projet tient pour pire qu'une absence de décor.

⚠ Le champ **déborde** franchement le rayon qui blesse (×1,6), avec une décroissance quadratique. À
la limite exacte, la déformation redessinerait en creux le cercle dont on vient de retirer le contour ;
en décroissance linéaire, elle pencherait tout le décor de l'arène du même petit angle — ce qui se lit
comme un défaut de rendu global, pas comme une source locale.

⚠ La pose de repos se capture à l'**activation** du composant : l'ajouter avant d'avoir posé la
position figerait l'origine de la scène comme place d'origine, et l'objet y retournerait à la première
déformation. Et **pas de `Reset()` global** appelé au démarrage d'une run, contrairement aux viviers
d'effets : le registre se vide seul (`OnDisable` part avec la scène), et une remise à zéro depuis
`RunBootstrap.Start` effacerait les piliers si l'arène s'était construite avant lui — l'ordre des
`Start` n'est pas garanti.

### Une greffe qui ne se voit pas sur le **corps** se lit comme un bonus de Hub

L'Assimilation promet de *devenir une chimère*, et ne se voyait nulle part sur le personnage : la
carapace ajoutait des épines invisibles, les servos une esquive sans jambes, la ruche des tourelles
posées *à côté*. Les greffes se lisaient dans un panneau de statistiques, c'est-à-dire au même endroit
que n'importe quel achat permanent — alors que tout leur propos est d'être d'une autre nature.
`ChimeraBody` fait pousser des appendices sur la silhouette, un vocabulaire de sept formes partagé par
les huit greffes.

⚠ **Reconstruit depuis la liste des équipées, jamais accumulé** : une fusion *absorbe* ses deux
sources et un remplacement en retire une. Ajouter au fil des équipements laisserait à l'écran les
appendices de greffes que le joueur ne porte plus.

⚠ **Matière ou énergie, jamais entre les deux** — l'œil de visée pivote avec la direction de tir, il
ne peut donc pas porter d'ombrage cuit (cf. le canon de tourelle). Et il n'est **pas** retourné avec
le corps : le miroir de l'échelle s'annule dans son angle (`180° − θ`), sans quoi il vise à l'opposé
une fois sur deux.

---

## Méthode

### Extraire du moteur, puis confronter — plutôt que lire les sources

Cette méthode a payé **trois fois** : amorçage réel de PCG32, `randf` en simple précision, et les 48
courbes d'interpolation. Aucune n'était devinable en lisant du code. Le motif :
écrire un `.gd` de relevé → le lancer en headless → **figer la sortie brute dans le test**.
Voir `tools/unity/dump_godot_rng.gd` et `dump_godot_easing.gd`.

### « Ça compile » n'est pas « ça marche »

Les tests unitaires couvrent la logique pure — **10 % du code**. Ils n'auraient vu **aucun** des
pièges de scène ci-dessus. D'où les vérifications qui tournent en **build headless**
(`PlatformSmokeTest`, `RunSmokeTest`) : c'est là que les erreurs de portage se voient.

### Un diagnostic qui s'arrête à la première modale ne diagnostique que le début

`SceneDiagnostic` plafonnait à ~5 s : la première montée de niveau ouvre une modale qui met le jeu en
pause, et **personne ne la ferme en headless**. Tout ce qui vient après — l'arsenal qui se construit,
l'overtime, l'arrivée du boss — restait donc invérifiable hors session jouée. Il choisit désormais la
première carte automatiquement et **kite en cercle** (immobile, le joueur meurt en ~15 s et le relevé
s'arrête avant ce qu'on cherche à voir). Il relève en fin de course : cartes choisies, armes portées,
overtime, boss vu, boss vaincu.

⚠ Corollaire : `--run-duration=<secondes>` existe pour la même raison. Sans elle, **vérifier
l'arrivée du boss coûte treize minutes de jeu réel** — c'est-à-dire qu'on ne la vérifie jamais.

### Une capture d'écran répond à la question qu'on lui pose, pas à celle qu'on croit

Les premiers clichés de combat montraient « aucune arme de zone ne s'affiche ». C'était exact et sans
rapport avec les armes : la tournée photographiait la **première minute** d'une run, où le joueur est
seul au centre d'une arène vide — aucune arme de mêlée, de zone ou de chaîne n'a de cible à portée.
`ScreenshotTour` pose désormais lui-même une nuée autour du joueur avant les clichés.

Deux conséquences apprises dans la foulée : ces ennemis sont posés **sans orbe d'XP** (28 morts d'un
coup ouvrent l'écran de montée de niveau, modal, qui recouvre le combat qu'on photographie), et les
clichés s'enchaînent **vite** (0,35 s) — l'Assimilation se déclenche vers la 35ᵉ élimination et pose
la même modale. Un effet d'arme dure 0,2 s : **un cliché unique tombe presque toujours entre deux
tirs**, d'où trois clichés et non un.

### Un banc qui simule des touches doit envoyer des **scancodes**, pas des codes virtuels

`pyautogui.press("down")` envoie `VK_DOWN` **sans** `KEYEVENTF_EXTENDEDKEY`. Unity lit les scancodes
(`m_UsePhysicalKeys: 1`) et y voit alors le **pavé numérique** : `Input.anyKey` est vrai,
`Input.GetKey(KeyCode.DownArrow)` est faux, et l'axe `Vertical` rend 0. Le banc a ainsi produit un
verdict net et entièrement faux — « les flèches ne naviguent pas » — sur un jeu où elles naviguaient.

Deux pièges s'y ajoutent, indépendants du scancode :

- **`GetAxisRaw` lit un ÉTAT, pas un événement.** Une pression de quelques millisecondes est vue par
  `GetButtonDown` (latché sur la frame) mais **pas** par l'axe : `Entrée` semblait marcher et les
  flèches non. Il faut **maintenir** la touche au moins une frame (≥ 0,2 s en pratique).
- **La fenêtre doit avoir le focus applicatif** ; un clic simulé peut le lui retirer, et toute la
  suite du relevé devient muette sans un seul message d'erreur.

Parade : envoyer les touches par `SendInput` avec le scancode et le drapeau *extended* pour les
flèches, et **journaliser l'état lu côté jeu** (`--input-probe`, `InputProbe`) plutôt que de conclure
depuis l'extérieur. C'est ce relevé qui a montré que la sélection se déplaçait bel et bien.
