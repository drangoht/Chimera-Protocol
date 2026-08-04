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
