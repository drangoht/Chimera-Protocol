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
