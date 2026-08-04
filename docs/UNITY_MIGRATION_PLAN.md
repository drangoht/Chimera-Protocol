# Plan de migration Godot 4.7 .NET → Unity 6.5

**Statut** : plan validé, non commencé · **Rédigé le** 2026-08-03 · **Version de référence** : 1.26.0
**Éditeur cible** : `C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor` (Unity 6.5, Windows)

> Ce document est la **source de vérité** de la migration. Il se met à jour à chaque lot terminé
> (état, mesures réelles, points ouverts résolus). Le plan de design (`docs/GDD.md`) et l'équilibrage
> ne sont **pas** concernés : la migration ne change aucune règle de jeu.

---

## 1. Les quatre arbitrages retenus

| Question | Décision | Conséquence directe |
|---|---|---|
| **Périmètre** | **Parité stricte 1.26.0** | Aucune nouveauté, aucune refonte de gameplay. On sait dire « c'est fini » : voir §8. |
| **Emplacement** | **`chimera-protocol/unity/`** | Un seul dépôt, un seul historique ; le code Godot reste lisible côté à côte comme référence vivante. |
| **Stack UI** | **uGUI + code procédural** | Les 7 381 lignes d'UI se **traduisent** (RectTransform/LayoutGroup ↔ Control/Container) au lieu de se réinventer. |
| **Godot pendant le port** | **Gel complet** | La 1.26.0 devient une cible **fixe**. Sans gel, la parité n'est jamais atteignable. |

**Conséquence du gel, à assumer explicitement** : les points laissés ouverts par la 1.26.0 — le
**cran III** (boss à la 8ᵉ minute face à un arsenal amputé, et le battre débloque le cran suivant) et
les **IPS au cran V** — ne seront **pas corrigés côté Godot**. S'ils s'avèrent bloquants pour les
joueurs, le gel doit être rompu volontairement, pas subi : voir §10, risque R1.

---

## 2. Inventaire : ce qui survit, ce qui est à réécrire

Mesuré sur l'arbre au 2026-08-03.

### 2.1 Ce qui traverse la migration **sans être réécrit**

| Actif | Volume | Pourquoi il survit |
|---|---|---|
| **Logique pure** `src/Core/Rules/` | **25 fichiers, 2 693 lignes** | Zéro `using Godot` — vérifié fichier par fichier. |
| **Tests xUnit** | **331 tests, 2 868 lignes** | Le `.csproj` de tests ne référence **pas** Godot : il inclut `src/Core/Rules/*.cs` et compile seul. Il valide déjà les deux moteurs. |
| Autres fichiers sans dépendance moteur | 7 fichiers (`Constants`, `SaveData`, `EnemySpawnData`, `LevelUpCardData`, `MetaUpgradeDefinition`, `StartingPerks`, `Titles`) | ~800 lignes de données/registres purs. |
| **Sprites** | **905 PNG** | Format neutre. Réglages d'import à rejouer (§7.1), pas les images. |
| **Audio** | **41 OGG/WAV** | Format neutre. |
| **Tuning** | **8 `data/*.json`** | Format neutre ; le chemin de lecture change (§7.4). |
| **Localisation** | `localization/ui.csv` | Le CSV est la source ; seul le *lecteur* change (§7.5). |
| **Générateurs Python** | ~40 outils de `tools/` | Ils produisent des PNG/OGG : **strictement moteur-agnostiques**, zéro travail. |
| **Événements C#** | tout le projet | **0 occurrence de `[Signal]`/`EmitSignal`** — la communication passe par des `event` C# standard. Portables tels quels. |

**Total qui survit : ~3 500 lignes de C# + la totalité des assets + la totalité des tests.**

### 2.2 Ce qui est à réécrire

| Dossier | Lignes | Nature du travail |
|---|---|---|
| `src/Systems/` | 7 450 | Singletons AutoLoad → bootstrap `DontDestroyOnLoad` ordonné ; spawn, audio, biome, télémétrie. |
| `src/UI/` | 7 381 | 18 écrans Control → uGUI. Le plus gros lot en volume, mais le plus **mécanique**. |
| `src/Entities/` | 5 364 | `CharacterBody2D` → MonoBehaviour ; joueur, 11 ennemis, 6 mini-boss, boss. |
| `src/Core/` | 3 718 − 2 693 = **1 025** | `GameManager`, `SaveManager` (hors Rules). |
| `src/Weapons/` | 2 725 | 12 armes + 9 fusions + projectiles. |
| `src/VFX/` | 326 | Effets. |
| **Total** | **~24 300** | |
| `scenes/*.tscn` | **65 scènes** | → Prefabs + Scenes Unity. Une bonne partie de l'UI étant construite **en code**, beaucoup de `.tscn` sont des coquilles quasi vides : à vérifier scène par scène (§4.6). |
| `assets/**/*.tres` | **41 SpriteFrames** | **Mécanisable** : format régulier (1 PNG par frame + manifeste). Convertisseur automatique (§7.2). |

### 2.3 Densité d'API Godot — ce qui pilote la méthode

C'est **cette table** qui dicte l'existence de la couche d'adaptation du §4. Cinq idiomes couvrent la
grande majorité des points de contact avec le moteur :

| Idiome Godot | Occurrences | Réparties surtout dans |
|---|---:|---|
| **`Tween`** | **502** | UI 280 · Entities 79 · Weapons 73 · Systems 54 · VFX 16 |
| `Button` / `Label` / `Control` / `TextureRect` | 807 | UI |
| `Timer` | 163 | partout |
| `GD.*` (`Print`, `Randf`, `Randi`, `Seed`) | 162 | partout |
| `GetTree()` | 112 | partout |
| `Node2D` / `Sprite2D` / `PackedScene` / `AnimatedSprite2D` | 301 | Entities, Weapons, VFX |
| `CallDeferred` | 57 | partout |
| `CharacterBody2D` / `Area2D` / `CollisionShape2D` | 33 | Entities |

**Le fait marquant** : `Tween` est le premier point de contact du projet avec Godot, devant la
physique d'un facteur 15. Une migration qui se concentre sur « le moteur physique » se tromperait de
chantier.

---

## 3. Structure cible du dépôt

```
chimera-protocol/
├── src/  scenes/  project.godot        ← Godot, GELÉ, référence de parité
├── tests/                              ← INCHANGÉ, valide les DEUX moteurs
├── unity/                              ← nouveau
│   ├── .gdignore                       ← ⚠ CRITIQUE : voir §3.1
│   ├── Assets/
│   │   ├── Scripts/
│   │   │   ├── Shared/Rules/           ← src/Core/Rules DÉPLACÉ ici (§3.2)
│   │   │   ├── Platform/               ← couche d'adaptation (§4)
│   │   │   ├── Core/ Systems/ UI/ Weapons/ Entities/ VFX/
│   │   │   └── *.asmdef
│   │   ├── Art/  Audio/  Prefabs/  Scenes/  Resources/
│   │   └── StreamingAssets/data/       ← les 8 JSON de tuning
│   ├── Packages/  ProjectSettings/
│   └── Build/                          ← ignoré par git
├── tools/
│   ├── unity/                          ← convertisseurs & build (§7, §9)
│   └── (les ~40 générateurs Python : inchangés)
└── docs/
```

### 3.1 Le piège de la cohabitation, à traiter au premier commit

Deux importeurs vont scanner le même arbre. Chacun ignore l'autre, mais **il faut le leur dire** :

- **Godot descendrait dans `unity/`** et tenterait d'importer 905 PNG + les `.meta` Unity comme des
  ressources → un fichier **`unity/.gdignore`** (vide) arrête l'importeur Godot net.
- **`Godot.NET.Sdk` globbe `**/*.cs`** et compilerait donc le code Unity dans l'assembly du jeu Godot
  → ajouter `<Compile Remove="unity/**/*.cs" />` dans `ChimeraProtocol.csproj`. Le projet **applique
  déjà exactement ce motif** pour `tests/**` : c'est une ligne à dupliquer, pas une invention.
- Unity, lui, ne regarde que `unity/Assets/` : l'arbre Godot lui est **invisible**, rien à faire.

### 3.2 Un seul exemplaire de la logique pure

`src/Core/Rules/` **déménage** vers `unity/Assets/Scripts/Shared/Rules/`. Les deux moteurs et les
tests le compilent **par chemin**, sans copie :

- Unity le compile nativement (il est sous `Assets/`), isolé dans un `.asmdef`
  `ChimeraProtocol.Rules` **sans référence à `UnityEngine`** — ainsi le compilateur *interdit
  physiquement* qu'une dépendance moteur s'y glisse. C'est un garde-fou plus fort que la convention
  actuelle.
- `ChimeraProtocol.csproj` (Godot) : `<Compile Include="unity/Assets/Scripts/Shared/Rules/*.cs" />`.
- `tests/ChimeraProtocol.Tests.csproj` : même changement de chemin, une ligne.

**Zéro duplication, donc zéro dérive possible.** Les 331 tests continuent de couvrir la seule et
unique copie des règles, quel que soit le moteur qui la consomme.

---

## 4. La couche d'adaptation (`Assets/Scripts/Platform/`) — le cœur de la méthode

**Le principe** : plutôt que de traduire 24 300 lignes au cas par cas, on écrit ~1 200 lignes qui
**rejouent la forme des API Godot les plus utilisées** sur des fondations Unity. Les 24 300 lignes se
portent alors par recherche/remplacement mécanique plutôt que par réinterprétation.

Ce n'est pas une couche d'abstraction pérenne : c'est un **échafaudage de migration**, assumé comme
tel. Il peut être démantelé plus tard, ou pas.

### 4.1 `GTween` — le morceau qui décide du coût du port

502 sites d'appel. Deux voies :

| Voie | Coût | Verdict |
|---|---|---|
| **Shim `GTween` reproduisant l'API Godot** (`CreateTween()`, `TweenProperty`, `SetParallel`, `Chain`, `SetEase/SetTrans`, `TweenCallback`) | ~350 lignes à écrire **une fois** | ✅ **Recommandé.** Les 502 sites se portent quasi sans édition. |
| **DOTween** (mature, gratuit) | 0 ligne d'infra, mais **502 sites à réécrire** dans un autre idiome | ❌ Coût déplacé au mauvais endroit. |

DOTween reste un bon choix pour un projet neuf ; ici il transforme un travail d'infrastructure borné
en un travail de traduction diffus sur 5 dossiers.

### 4.2 Les autres shims

| Shim | Remplace | Volume visé | Note |
|---|---|---|---|
| `Gd` | `GD.Print/Randf/Randi/Seed` | 162 | ⚠ **La RNG est un sujet à part entière** : voir §4.3. |
| `GTimer` | `Timer`, `SceneTreeTimer` | 163 | Coroutines + file de timers en `Update`. |
| `SceneRoot` | `GetTree()`, changement de scène, **pause** | 112 | ⚠ La pause est un piège majeur : §4.5. |
| `Deferred` | `CallDeferred` | 57 | File vidée en fin de frame (`LateUpdate`). |
| `ScenePaths` + `Spawner` | `PackedScene` + `Instantiate()` | 65 | Registre de prefabs adressés par chemin logique. |
| `FrameAnimator` | `AnimatedSprite2D` + `SpriteFrames` | 55 | §7.2. |
| `UiKit` | `Label`, `Button`, `Container`, `TextureRect` | 807 | Fabriques uGUI aux signatures des fabriques `UiStyle` existantes. |

### 4.3 La RNG : le point technique le plus lourd de conséquences

Le banc de mesure **entier** repose sur `--seed=<n>` → `GD.Seed` : c'est ce qui rend deux campagnes
comparables (méthode appariée, test des signes). Trois faits :

1. Un shim naïf sur `UnityEngine.Random` donne une reproductibilité **interne** (Unity vs Unity) —
   suffisant pour continuer à régler le jeu **après** la migration.
2. Mais il rend **impossible** de rejouer une graine Godot sous Unity, donc de comparer les deux
   moteurs run à run. Or c'est exactement l'outil de validation de parité le plus puissant dont on
   dispose (§8.2).
3. Godot 4 utilise **PCG32**, spécification publique, ~40 lignes de C#.

→ **Recommandation : réimplémenter PCG32 dans le shim `Gd`.** Coût dérisoire, et cela convertit la
validation de parité de « comparer des distributions sur N graines » à « comparer deux runs sur la
même graine ».

> **✅ FAIT au Lot 1** — `Pcg32` (logique pure, 21 tests). La fidélité a été établie **par mesure**
> et non par lecture des sources : `tools/unity/dump_godot_rng.gd` extrait des valeurs du moteur, et
> des formulations candidates leur sont confrontées. Trois résultats qu'aucune lecture de code
> n'aurait donnés :
>
> 1. **L'amorçage n'est pas `state = seed`** mais `pcg32_srandom_r(seed, PCG_DEFAULT_INC_64)` :
>    l'état part de 0 et absorbe la graine *entre deux avancements*. La version naïve produit une
>    toute autre suite (et un premier tirage **nul** pour `seed = 1`).
> 2. **`randf()` de Godot est en simple précision.** Le même calcul mené en `double` — qui paraît
>    strictement meilleur — diverge d'environ **1e-8 dès le premier tirage**. Un test verrouille ce
>    piège, qui serait sinon « corrigé » un jour par quelqu'un cherchant de la précision.
> 3. **`randi_range` procède par modulo** sur le tirage brut (`from + rand() % span`), pas via
>    `randf()`.
>
> ⚠ **Une lacune assumée** : `randf_range` (surcharge `double` de `GD.RandRange`) **n'a pas pu être
> reproduite** — onze formulations candidates ont été testées, aucune ne correspond ; les mesures
> montrent que Godot consomme *plusieurs* tirages par appel. Poursuivre serait disproportionné : les
> **8 sites d'appel** ne pilotent que des positions et instants d'apparition de **ramassables**
> (`PowerUpSpawner`, `MagnetSpawner`, `AetherCoreSpawner`) — ni tirages de cartes, ni tables
> d'ennemis. **Conséquence à rappeler avant toute comparaison inter-moteurs (§8.2) : les runs Unity
> restent parfaitement reproductibles entre elles, mais une comparaison Godot↔Unity sur une même
> graine divergera dès le premier appel à cette fonction.** Un test verrouille la divergence connue
> plutôt que de la laisser se découvrir en production.

⚠ **Limite à dire tout de suite** : même RNG identique, les runs ne seront pas *identiques*. Le
nombre d'appels à la RNG dépend du nombre de frames, et la cadence de simulation diffère entre les
deux moteurs. Ce qui devient comparable, ce sont les **tirages** (cartes de level-up, tables de
spawn, affixes d'élite) — c'est-à-dire la source de variance que le banc cherchait justement à
neutraliser. C'est beaucoup, ce n'est pas tout.

### 4.4 Physique : ce que la mesure autorise à **ne pas** porter

Lecture du code réel :

- `Player` et `EnemyBase` sont des `CharacterBody2D` avec `MoveAndSlide()`.
- **Mais les dégâts de contact sont calculés par distance**, pas par collision :
  `GlobalPosition.DistanceTo(player.GlobalPosition) < ContactRadius` (`EnemyBase.cs:223`).
- **Et la séparation entre ennemis est manuelle** : `Player.cs:446-455` repositionne les ennemis à la
  main.

→ La physique Godot ne sert donc, pour les ennemis, qu'à la collision avec les **obstacles de biome**
et les murs. **Recommandation : mouvement par transform + la séparation manuelle existante, et
`Rigidbody2D`/colliders uniquement là où les obstacles l'exigent.** À 200-300 entités, éviter le
moteur physique n'est pas une optimisation prématurée mais la reproduction fidèle de ce que le jeu
fait déjà.

> **✅ P1 TRANCHÉ au Lot 1** — et le code Godot le dit lui-même, en commentaire de
> `EnemyBase._Ready` : « *les ennemis traversent les murs (layer 1) mais sont BLOQUÉS par les
> obstacles infranchissables (bit 2). mask = 2 → collision avec les obstacles uniquement* ».
>
> Donc : les ennemis **ne collisionnent ni entre eux, ni avec le joueur** — uniquement avec les
> `StaticBody2D` de `BiomeObstacles` (`CollisionLayer = 3`). Il n'y a **aucune physique dynamique à
> n corps** à reproduire. Combiné au fait que les dégâts de contact se calculent par **distance** et
> que la séparation est **manuelle et en O(n)** (joueur↔ennemi seulement, `PushEnemiesAside`), la
> charge à 300 entités est **linéaire et légère** — ce que le prototype confirme (§6.2, R3).
> Le mouvement Unity peut donc se faire **par transform**, avec des colliders sur les seuls
> obstacles statiques.

### 4.5 La pause : un piège connu du projet qui change de forme

Godot : `GetTree().Paused` + `ProcessMode` par nœud. Unity : `Time.timeScale = 0f`.

Ce n'est pas équivalent. Les conséquences concrètes, toutes déjà présentes dans le projet :

- `ModalQueue` coordonne `LevelUpScreen` + `AssimilationScreen` avec **un seul** `Paused` → sous
  Unity, l'écran modal doit tourner en `unscaledDeltaTime`, sinon ses `GTween` se figent avec le jeu.
  **`GTween` doit donc porter un drapeau `IgnoreTimeScale`** (l'équivalent de `ProcessMode.Always`).
- Le projet documente déjà que « la pause du `LevelUpScreen` gèle la physique » dans les tests
  headless. Sous Unity le symptôme diffère mais la classe de bug est la même.
- `PauseScreen` ouvre `OptionsScreen` en surcouche **sans changer de scène** : à conserver tel quel,
  c'est plus simple sous Unity que sous Godot.

### 4.6 Autoloads → bootstrap ordonné

15 AutoLoads. Godot garantit l'ordre de déclaration ; **Unity ne garantit rien** entre MonoBehaviours
sans configuration.

→ **Une scène `Boot` unique**, avec un seul `MonoBehaviour` qui instancie les 15 systèmes **dans
l'ordre exact du `project.godot`** puis charge le menu. Ordre explicite, lisible, testable — plutôt
que 15 entrées de *Script Execution Order* dispersées dans les ProjectSettings.

⚠ Le contrat `NomSystem.Instance` utilisé partout dans le code est **conservé tel quel** : c'est ce
qui permet aux 7 450 lignes de `src/Systems/` de se porter sans réécrire leurs appelants.

⚠ Piège inversé à noter : Godot impose `base._Ready()` **en dernier** dans `WeaponBase` (documenté
dans `PITFALLS.md`). Ce piège **disparaît**, remplacé par le couple `Awake`/`OnEnable`/`Start`
d'Unity, dont l'ordre relatif entre objets est différent. Ne pas supposer que la traduction est
neutre : les 19 armes héritant de `WeaponBase` sont le premier endroit où ça se verra.

---

## 5. Contraintes du code partagé — **mesurées** au Lot 0, non supposées

> **Cette section a été réécrite le 2026-08-03 après exécution du Lot 0.** Sa version initiale
> annonçait deux blocages ; **les deux étaient faux**, et le vrai blocage n'y figurait pas. La leçon
> vaut d'être notée : ces trois points se tranchent par une compilation de 30 secondes, et aucun
> n'était devinable en lisant le code.

### 5.1 ~~`ImplicitUsings`~~ — **non-problème** (hypothèse réfutée)

Hypothèse initiale : les 7 fichiers de `Rules/` sans directive `using` dépendraient de
`<ImplicitUsings>enable` et ne compileraient pas sous Unity.

**Faux.** Compilé avec `ImplicitUsings=disable` : **0 erreur, 0 avertissement**, DLL de 45 Ko
produite. Ces fichiers n'ont besoin d'aucun `using` — ils n'emploient que des types primitifs.
Aucune action.

### 5.2 `System.Text.Json` — **fourni par Unity 6.5** (hypothèse réfutée)

Hypothèse initiale : Unity ne fournit pas `System.Text.Json`, donc bascule vers Newtonsoft.

**Faux depuis Unity 6.** Unity 6.5 embarque un jeu d'**extensions BCL** référencées par défaut :

```
Editor/Data/BCLExtensions/runtime/netstandard2.1/System.Text.Json.dll
Editor/Data/BCLExtensions/TargetingPacks/netstandard2.1/ref/System.Text.Json.dll
```

(`AllAssemblies.txt` liste aussi `System.Collections.Immutable`, `System.Text.Encodings.Web`,
`Microsoft.Extensions.Logging.Abstractions`…) **Vérifié à la compilation** : les 25 fichiers de
`Rules/`, dont `ChallengeTable` et `GraftTable` qui utilisent `JsonElement`, compilent sous Unity
sans la moindre erreur JSON.

→ **Aucune migration de bibliothèque JSON.** `SaveManager`, `GraftTable`, `ChallengeTable` et les
5 systèmes concernés restent inchangés, et le risque de casser la désérialisation des sauvegardes
existantes disparaît. C'est la plus grosse économie du Lot 0.

⚠ **Ce qui reste vrai, et qui est un point de vigilance et non un blocage** : `SaveManager` sérialise
par **réflexion** (`JsonSerializer.Serialize<SaveData>`). En **IL2CPP**, la réflexion sur génériques
peut échouer à l'exécution — et l'échec ne se voit qu'au build final, jamais dans l'éditeur. Parade
si le cas se présente : les **générateurs de source** de `System.Text.Json`
(`JsonSerializerContext`), compatibles AOT. À traiter au Lot 6 avec la persistance, à valider sur un
build IL2CPP réel.

### 5.3 **Le vrai blocage : Unity 6.5 compile en C# 9**

Non anticipé, découvert à la première compilation :

```
error CS8773: Feature 'record structs' is not available in C# 9.0. Please use language version 10.0 or greater.
error CS0518: Predefined type 'System.Runtime.CompilerServices.IsExternalInit' is not defined or imported
```

Unity 6.5 livre pourtant le SDK .NET 8.0.318 et son Roslyn, mais **fige le langage à C# 9** et cible
le profil d'API **netstandard2.1** — où `IsExternalInit` (requis par tout accesseur `init`, donc par
tout `record`) n'existe pas.

**C'est une contrainte de portée projet, pas un détail de `Rules/`** : les ~24 300 lignes restant à
porter doivent elles aussi être C# 9. Audit du code réel :

| Fonctionnalité | Version | Occurrences dans `src/` |
|---|---|---:|
| `record struct` | C# 10 | **2** |
| `namespace X;` (fichier) | C# 10 | 0 |
| `global using` | C# 10 | 0 |
| `required` | C# 11 | 0 |
| chaînes brutes `"""` | C# 11 | 0 |
| motifs de liste `is [..]` | C# 11 | 0 |
| expressions de collection `= []` | C# 12 | 0 |
| accesseurs `{ get; init; }` | C# 9 + polyfill | **0** |
| `record class` | C# 9 ✅ | 8 (compatibles) |

**Bilan : 2 occurrences à traiter sur tout le projet.** Le code est, de fait, déjà quasi C# 9.

**Correctif appliqué (Lot 0)** — `ChallengeTable.ChallengeContext` :
`readonly record struct` → `readonly struct` + constructeur explicite, **noms de paramètres
conservés à l'identique** (les appels par argument nommé continuent de compiler).
Conversion vérifiée sans effet de bord : les sémantiques de record (égalité par valeur, `with`,
déconstruction) ne sont utilisées **nulle part** — `ChallengeContext` n'a que **2 sites de
construction**. 331 tests verts après conversion.

⚠ **La seconde occurrence de `record struct` est hors `Rules/`** et sera traitée au lot qui porte son
fichier. Recenser à ce moment-là, ne pas y toucher maintenant (Godot est gelé, pas le code à porter).

**Alternative écartée** : forcer `-langversion:10` via un `csc.rsp` + un polyfill `IsExternalInit`.
Non supporté officiellement par Unity, et pour **2 occurrences** le remède serait plus risqué que le
mal.

---

## 6. Les lots

Chaque lot a un **critère de sortie vérifiable**. Un lot n'est pas « fini » parce qu'il compile.

| # | Lot | Poids | Critère de sortie |
|---|---|---:|---|
| **0** ✅ | **Socle partagé** — §5, déménagement de `Rules/` (§3.2), `.gdignore` + `Compile Remove` (§3.1), projet Unity qui compile | 3 % | **✅ TERMINÉ le 2026-08-03** — voir §6.1 |
| **1** ✅ | **Couche Platform** (§4) — `GTween`, `Gd`+PCG32, `GTimer`, `SceneRoot`, `Deferred`, `Spawner`, `FrameAnimator` | 8 % | **✅ TERMINÉ le 2026-08-03** — voir §6.2 |
| **2** | **Cœur de run** — `GameManager`, `Player`, `EnemyBase`, spawn, XP, un ennemi, une arme | 14 % | Une run se joue : bouger, tuer, ramasser, monter de niveau. **P1 (§4.4) tranché et documenté** |
| **3** ⬤ | **Arsenal complet** — 12 armes, 9 fusions, projectiles, VFX | 12 % | **Logique ✅** — 21 armes tirent, fusions verrouillées (§6.4). Restent les visuels et sons. |
| **4** | **Bestiaire complet** — 11 ennemis, affixes d'élite, 6 mini-boss, `RustedCore` (3 phases × 5 incarnations) | 14 % | Chaque entité apparaît, agit et meurt correctement ; les 5 incarnations tirent leur signature |
| **5** | **UI & écrans** — 18 écrans, HUD, `UiStyle`/`UiPalette`, navigation clavier/manette, `ModalQueue` | 22 % | **Parité visuelle prouvée par captures avant/après** sur les 18 écrans (§8.3) ; navigation manette complète sans souris |
| **6** | **Méta & persistance** — Hub, Assimilation, Défis, Codex, `SaveManager`, `GameSettings`, localisation | 13 % | Une **sauvegarde 1.26.0 réelle** se charge sans perte (§9.3) ; les 3 langues s'affichent |
| **7** | **Banc & télémétrie** — flags CLI, `PowerTelemetry`, `BossTelemetry`, `PressureMeter`, `BenchAutoPilot` | 9 % | Une campagne headless tourne et produit un `power_curve.log` exploitable par `tools/power_loop.py` **sans modifier l'outil** |
| **8** | **Build & release** — build Unity par script, `release_itch.ps1` adapté, `version.json`, icône | 5 % | Un `.exe` exporté démarre, joue une run complète et se pousse sur itch en canal de test |

**Les deux lots les plus lourds sont l'UI (22 %) et le duo bestiaire/cœur (28 %).** L'UI est le plus
volumineux mais le plus mécanique ; le bestiaire est celui où le *comportement* peut diverger sans
que rien ne le signale.

### 6.2 Lot 1 — prototypes de risque : les trois verdicts

Exécutés le 2026-08-03 **avant** d'écrire le moindre shim, conformément au §13. Prototype :
`unity/Assets/Scripts/Bench/BenchProto.cs` (reproduit la charge réelle établie au §4.4) ; build par
`unity/Assets/Editor/BuildBench.cs` (`-executeMethod`, scène générée par code).

| Risque | Question | Verdict |
|---|---|---|
| **R2** | Le banc tourne-t-il headless plus vite que le temps réel ? | ✅ **×94,8** — 60 s simulées en **0,63 s**, 300 entités, `-batchmode -nographics`, sans fenêtre, sortie 0. Godot plafonnait à **×1,0**. |
| **R3** | 300 entités tiennent-elles la cadence ? | ✅ **0,168 ms/pas** de simulation, soit ~**5 960 IPS** pour la simulation seule ; avec 300 sprites rendus, **×42,5** le temps réel (~2 500 images/s). Coût de simulation **identique** avec et sans rendu. |
| **R7** | Un build IL2CPP passe-t-il, et la sérialisation survit-elle à l'AOT ? | ✅ **RETIRÉ** — build IL2CPP réussi et **14/14 vérifications passent**, `System.Text.Json` compris. |

**Contrôle de sanité** : le compte de contacts est **identique** (785) avec et sans rendu, ce qui
vérifie que le pas de simulation est bien fixe et indépendant de la cadence d'affichage — sans quoi
les deux mesures ne seraient pas comparables.

⚠ **Ce que ces chiffres ne disent pas.** Le prototype simule le **déplacement, la séparation et les
dégâts de contact** — pas les armes, les projectiles, les VFX, l'IA par archétype, l'UI ni l'audio.
C'est donc une **borne haute**, à la manière de `--start-at` (§4.3). Ce qu'il établit vraiment :
le mode headless fonctionne et n'est pas bridé, et le coût par entité est linéaire et faible.
La marge (×42 sur la cadence requise) est assez large pour retirer R2 et R3 de la liste des risques
structurants, pas pour promettre une cadence finale.

**Couche `Platform/` — livrée et vérifiée**

Noyaux **purs** (assembly `ChimeraProtocol.PlatformCore`, `noEngineReferences`), couverts par la
suite xUnit — **457 tests** au total :

| Noyau | Remplace | Sites | Fidélité |
|---|---|---:|---|
| `Pcg32` | `GD.Randi/Randf/Seed` | 162 | bit-exact (§4.3) |
| `Easing` | courbes de `Tween` | — | 48 courbes à 1e-6 |
| `TweenTimeline` | séquencement de `Tween` | 502 | étapes, parallèle, boucles, valeur finale exacte |
| `TimerWheel` | `CreateTimer`, nœuds `Timer` | 163 | 3 règles de sémantique testées |
| `DeferredQueue` | `CallDeferred` | 57 | drainage jusqu'à épuisement, borné |

Adaptateurs moteur (assembly `ChimeraProtocol.Platform`) : `PlatformHost` (ordonnancement explicite —
minuteries et interpolations en `Update`, différé en `LateUpdate`), `GTween`, `Gd`, `SceneRoot`.

⚠ **« Ça compile » n'est pas « ça marche »** : les tests purs ne peuvent rien dire des adaptateurs,
qui dépendent du cycle de vie Unity (`Update`/`LateUpdate`, `timeScale`, destruction d'objets) —
c'est-à-dire précisément de ce qui casse lors d'un portage. D'où
`unity/Assets/Scripts/Bench/PlatformSmokeTest.cs`, qui tourne **en build headless** :
**23/23 vérifications passent** (Mono **et** IL2CPP), dont la plus importante — pendant la pause,
l'interpolation d'UI atteint 1,000 pendant que celle du jeu reste à 0,000 (le piège du §4.5, prouvé
traité).

**Pipeline d'assets (§7.1, §7.2) — livré et vérifié**

- **905 PNG** importés sous `unity/Assets/Art/sprites/`, réglages appliqués automatiquement par
  `SpriteImportPostprocessor` — vérifié sur les `.meta` : `filterMode: 0` (Point),
  **`spritePixelsToUnits: 1`**, `alignment: 0` (centre), compression désactivée.
- **40 `SpriteFrames`** converties : `tools/unity/convert_spriteframes.py` produit des manifestes
  JSON neutres, puis `BuildSpriteFrames.Run` (côté Unity) construit les `ScriptableObject` en
  résolvant les références. **141 animations, 724 images**, comptes identiques des deux côtés, zéro
  référence manquante. ⚠ La résolution des références **doit** rester côté Unity : un `.asset`
  référence ses sprites par GUID géré par l'AssetDatabase, et les fabriquer depuis Python
  reviendrait à deviner des identifiants — faux en silence, avec pour seul symptôme des animations
  à trous visibles en jouant.

> **✅ Le `.gdignore` est enfin VRAIMENT testé** (point laissé ouvert au §6.1). Avec 905 PNG présents
> sous `unity/Assets/`, un réimport complet de Godot laisse son cache **inchangé — 3 720 entrées
> avant et après, zéro venant de `unity/`**, et aucun chemin `unity/Assets` nulle part dans
> `.godot/`. La cohabitation des deux importeurs est prouvée, plus supposée.

**Deux écarts assumés par rapport à Godot**, tous deux documentés dans le code :
① `GTween` désigne les propriétés par **lambda** et non par chaîne (`"modulate:a"`) — la réflexion
est fragile sous IL2CPP et invisible au compilateur ; un renommage devient une erreur de compilation
au lieu d'une animation qui cesse silencieusement de fonctionner.
② `Gd.RandRange(double, double)` ne reproduit pas les valeurs de Godot (§4.3).

**R7 — IL2CPP : résolu, après trois causes distinctes**

Le chemin mérite d'être noté, parce que chaque étape ressemblait à un échec définitif et n'en était
pas une :

1. **`ToolchainNotFoundException`** — IL2CPP compile du C++ et exige Visual Studio avec les
   compilateurs C++ et le Windows SDK, absents au départ. Installation des **Build Tools 2022**.
2. **`vcruntime.h` introuvable** — deux toolsets MSVC coexistaient et Unity retient **le plus
   récent** : celui de VS 2026 (14.51), dont l'installation C++ était **incomplète** (`bin` et `lib`,
   mais **aucun dossier `include`**). Complété via l'installeur Visual Studio.
3. **Échec du cache de build** — Bee signalait des en-têtes implicites « non connus statiquement »
   pour la chaîne VS 2026. **Purger `Library/Bee` a suffi** : le build passe alors en 39 s. ⚠ Piège à
   retenir : après ce genre d'échec, Bee **met l'échec en cache** et les tentatives suivantes
   « échouent » en 1,3 s sans rien recompiler. Un échec instantané n'est pas un échec, c'est un
   cache — le réflexe est de purger `Library/Bee` avant de conclure quoi que ce soit.

**Vérifié à l'exécution, backend IL2CPP** : **14/14**, dont l'aller-retour `System.Text.Json` par
réflexion (collections, dictionnaire, imbrication) **et** la préservation de la convention camelCase
— celle qui décide si les sauvegardes existantes des joueurs restent lisibles (§9.3). La même
vérification passe **14/14 en Mono**.

→ **Le risque AOT du §5.2 est donc levé, mesuré et non supposé.** Les deux backends restent ouverts.
Mono conserve un avantage de principe pour la parité (l'export .NET de Godot est lui aussi en JIT),
mais ce n'est plus une contrainte : le choix peut se faire sur d'autres critères (temps de build,
performances, cap console/mobile).

### 6.5 Lot 4 — bestiaire : logique complète (46/46)

**Bestiaire data-driven conservé** : `EnemyTable` lit `enemies.json` — **31 ennemis pour 9
comportements**. C'est une propriété délibérée du jeu ; la reproduire par 31 classes aurait multiplié
par sept la surface de code pour un résultat identique.

⚠ **Piège de données trouvé et verrouillé** : `enemies_biome_expansion.json` ressemble à un fichier
à charger — **aucun code du jeu ne le lit**. Ses 20 entrées existent déjà dans `enemies.json`, mais
**sans leur `framesPath`** : le fusionner « pour être complet » aurait rendu 20 ennemis invisibles,
sans la moindre erreur. Cas général → `docs/PITFALLS_UNITY.md`.

**Affixes d'élite** (5) branchés sur `EliteAffixTable`. Deux détails séparent « l'affixe existe » de
« l'affixe joue » : le Régénérant ne se soigne qu'après un délai **sans être frappé** (sinon ce n'est
qu'un sac de PV), et l'explosion passe par `Player.TakeDamage`, donc **respecte les i-frames**.
La promotion a lieu **après** le scaling, pour porter sur les valeurs de la minute courante.

**Boss** : `RustedCore` — 3 phases × 5 incarnations, adossé à `BossPhases`/`BossIncarnations`.
Vérifié : incarnation par biome, repli sur la souche pour un biome inconnu, bascule sous 66 %,
**irréversibilité de la phase** (se soigner ne fait pas reculer — sinon un combat long oscillerait
autour du seuil et rejouerait la surcharge en boucle), phase III, renforts, signature.
**Mini-boss** : socle `MiniBoss` + les 3 champions de biome, chacun demandant le réflexe **inverse**
de l'incarnation finale de son biome.

⚠ **Trois échecs de banc, aucun du code** — tous instructifs : échantillon de variété mesuré sur
2 ennemis ; explosion absorbée par des i-frames déclenchées par la nuée ; et l'ennemi explosif
**posé sur le joueur**, qui le frappait au contact avant d'exploser — on mesurait alors l'inverse de
l'effet visé. Dernier en date : compter les renforts *présents* alors que les armes du joueur les
tuent à mesure — c'est le nombre de **vagues** qui est le signal.

⚠ **Reste au Lot 4** : les 3 mini-boss globaux (`AetherRevenant`, `MasterSentinel`, `RustStalker`) et
tous les visuels (sprites, overlays de champion, télégraphes).

### 6.4 Lot 3 — arsenal : machinerie livrée, comportements en cours

**Le critère de sortie a été traité en premier** — « les fusions héritent bien du niveau, le bug de
la 1.21.0 ne doit pas réapparaître » — et à deux niveaux :

- **En logique pure** : `WeaponFusion` (`Shared/Rules`), **16 tests**. Sous Godot, la règle vivait
  dans un `Node`, donc hors de portée des tests — c'est exactement là que le déséquilibre s'était
  logé. Un portage est le moment idéal pour réintroduire ce bug, **la valeur fautive (1) étant aussi
  une valeur parfaitement plausible**.
- **De bout en bout**, en build headless : arme montée au niveau 5 → fusion → **niveau hérité 5, pas
  1**, arme source retirée, fusion non reforgeable. C'est la différence entre « la règle est juste »
  et « le jeu l'utilise ».

**Données** : `WeaponTable` lit `weapons.json` sans rien recopier — **7 tests portant sur le vrai
fichier du jeu** (12 armes, 9 fusions, chaque fusion désigne une arme existante, aucune cadence
nulle, et au-delà des paliers décrits les dégâts montent tandis que les **mécaniques plafonnent**).
`DataFiles` charge depuis **`StreamingAssets`** : le tuning doit rester modifiable sans recompiler.

**Les 12 armes et les 9 fusions sont portées.** Critère de sortie **atteint** : « les 21 armes
tirent — aucune silencieuse » (30/30 headless). Chaque arme est montée seule face à des cibles et
doit franchir sa recharge : **une arme muette ne lève aucune erreur**, elle rate simplement sa cible
ou attend une condition qui n'arrive jamais, et seul un compteur de tirs le détecte.

La **géométrie de visée** de chaque archétype est vérifiée séparément — arc orienté, rebonds de
chaîne, couloir, cône, rayon, éventail, orbite — parce que c'est elle, et non la boucle de tir, qui
casse silencieusement lors d'un portage.

**Effets de statut** ajoutés à `EnemyBase` (ralentissement, brûlure), avec deux règles de cumul qui
évitent des dérives : un ralentissement plus fort remplace un plus faible au lieu de s'y multiplier
(sinon deux sources de gel immobilisent totalement), et la brûlure retient la source la plus forte.
⚠ La brûlure inflige des dégâts **continus** : elle ne doit jamais emprunter le chemin des coups
discrets ni subir un plancher en pourcentage des PV max.

⚠ **Reste au Lot 3** : les visuels et sons des armes (VFX, sprites de projectiles, mixage) —
la logique est complète, la présentation appartient aux lots VFX/UI.

### 6.3 Lot 2 — cœur de run : tranche verticale livrée

**Rendu tranché par la mesure, pas par préférence** : le jeu Godot utilise **108 `PointLight2D`** et
26 `ShaderMaterial`. Le pipeline intégré d'Unity n'a **aucune** notion de lumière 2D → **URP 17.5.0
avec le renderer 2D**, installé et actif. ⚠ Les **11 `.gdshader`** devront être réécrits en
ShaderLab/Shader Graph, aux lots VFX et UI — chantier identifié, non entamé.

**Porté** (assembly `ChimeraProtocol.Gameplay`) : `PlayerStats`, `Player`, `EnemyBase`, `XpSystem`,
`EnemySpawner`, `GameManager`, `WeaponBase` + `ImpulseCannon` + `Bullet`.

**Vérifié headless — 8/8** (`RunSmokeTest`, critère de sortie) : apparition, tir, mort, crédit d'XP,
**2 montées de niveau**, dégâts de contact, clôture de run.

> **Le résultat le plus parlant** : le joueur collé à un ennemi de 7 dégâts perd **exactement
> 21 PV en 1 seconde**, soit **3 coups** — précisément ce que la fenêtre d'i-frames de 0,45 s
> autorise (t ≈ 0 / 0,45 / 0,90). La constante la plus critique du projet pour la survie en nuée est
> donc fidèle **au chiffre près**, pas seulement « présente ».

**Aucune constante d'équilibrage n'a été recopiée** : `XpCurve`, `SpawnCurve`, `EnemyScaling`,
`StatCaps` et `RegenReserve` sont appelés depuis la logique pure **partagée avec Godot**. Cela exclut
par construction la classe de bugs la plus vicieuse d'une migration — une valeur retranscrite de
travers, qui produit un jeu « qui marche » mais n'est plus le même.

⚠ **Divergence de sémantique trouvée en route, et corrigée en trois endroits** : Godot rend
**toujours actif** un nœud instancié puis ajouté à l'arbre ; Unity **conserve l'état du gabarit**.
Symptôme observé au premier essai : « 4 ennemis créés, 0 vivants » — des objets présents en
hiérarchie, absents du jeu, et qui ne signalent rien. `Spawner`, `EnemySpawner` et `ImpulseCannon`
alignent désormais explicitement sur la sémantique Godot.

**Reste pour clore le Lot 2** : scènes et prefabs authorés (tout est assemblé par code pour l'instant),
HUD, entrées rebindables via `InputRemap`, et le second `record struct` hors `Rules/`.

### 6.1 Lot 0 — terminé le 2026-08-03

**Livré**

- `src/Core/Rules/` → **`unity/Assets/Scripts/Shared/Rules/`** (25 fichiers, via `git mv`, historique
  préservé). Les 25 `.cs.uid` orphelins de Godot ont été retirés — aucune scène ne référençait ces
  classes, elles ne sont jamais attachées à un nœud.
- **`ChimeraProtocol.Rules.asmdef`** avec **`noEngineReferences: true`**.
- `ChimeraProtocol.csproj` : `<Compile Remove="unity/**/*.cs" />` + réinclusion explicite de `Rules/`.
- `tests/ChimeraProtocol.Tests.csproj` : repointé sur le nouveau chemin.
- `unity/.gdignore` + section Unity du `.gitignore` (⚠ les `.meta` **ne sont pas** ignorés — ils
  portent les GUID d'assets ; les perdre casse toutes les références de prefabs et de scènes).
- Projet Unity 6.5 créé (`-createProject`, licence OK).
- **Correctif C# 9** : `ChallengeContext` en `readonly struct` (§5.3).

**Critères de sortie — tous vérifiés**

| Critère | Résultat |
|---|---|
| Les 331 tests passent depuis le nouveau chemin | ✅ **331/331**, 0 échec |
| Le projet Godot compile | ✅ 0 erreur, 0 avertissement |
| Le jeu Godot **tourne** encore | ✅ run headless de 25 s : 15 autoloads démarrés, `grafts/meta/challenges/enemies` chargés, 7 kills, Échos crédités, sauvegarde écrite, sortie 0 |
| Unity compile `Rules/` | ✅ **`ChimeraProtocol.Rules.dll`, 42 Ko**, 0 erreur |
| L'`asmdef` interdit vraiment le moteur | ✅ **prouvé par sonde** : un fichier `using UnityEngine;` déposé dans `Rules/` échoue en `CS0246 — 'UnityEngine' could not be found`. Sonde retirée. |

**L'invariant central du projet est désormais imposé par le compilateur.** Sous Godot, « la logique
pure ne dépend pas du moteur » reposait sur la discipline et se vérifiait par un `grep -L "using
Godot"`. Il est maintenant **impossible à violer** : le code ne compile pas.

**Ce que le Lot 0 n'a pas prouvé, et qu'il ne faut pas croire acquis**

- **`.gdignore` n'a pas été testé pour de bon.** `unity/Assets/` ne contient encore que du `.cs` et
  un `.asmdef` — rien d'importable par Godot. Le vrai test arrive au **Lot 1**, quand 905 PNG y
  atterriront. Vérifier alors que `.godot/imported/` ne contient **rien** venant de `unity/`.
- **Aucun `record struct` hors `Rules/` n'a été traité** (il en reste **1**) : Godot est gelé, le code
  à porter ne l'est pas — il se traitera au lot qui porte son fichier.
- Les avertissements `ObjectDB instances were leaked` / `resources still in use at exit` en fin de
  processus headless sont **antérieurs** à ce lot : ils sont déjà documentés comme bruit d'arrêt
  bénin dans `docs/TEST_REPORT.md`.
- Le projet Unity est **volontairement nu** : ni URP, ni 2D, ni TextMeshPro. Le rendu se choisit au
  Lot 2, quand il aura une conséquence observable.

---

## 7. Pipeline d'assets — ce qui est mécanisable

### 7.1 Sprites (905 PNG) — un `AssetPostprocessor`, pas 905 clics

Le projet impose `texture_filter = Nearest` global, grille 32×32. Sous Unity, ces réglages sont
**par fichier** et le défaut (bilinéaire + compression) **détruirait le pixel art**.

→ `Assets/Editor/SpriteImportPostprocessor.cs` : `filterMode = Point`,
`textureCompression = Uncompressed`, `spritePixelsPerUnit = 32`, pivot centre, `mipmapEnabled = false`.
Un script, appliqué à tout `Assets/Art/`, exécuté à chaque import. **C'est le tout premier fichier
Unity à écrire du Lot 1** : importer 905 sprites avant lui, c'est les réimporter après.

### 7.2 Les 41 `SpriteFrames` — convertisseur automatique

Le format est régulier et lisible : un `ext_resource` par PNG, puis un tableau d'animations
(`name`, `speed`, `loop`, liste de frames). → `tools/unity/convert_spriteframes.py` génère un
`ScriptableObject` `SpriteFramesAsset` par ennemi.

**Recommandation : ne pas passer par Mecanim.** Le code appelle `PlayAnim("attack")` de façon
data-driven, avec repli si l'animation manque (le projet a déjà connu 144 erreurs/session sur une
animation `attack` absente). Un `FrameAnimator` de ~120 lignes lisant le `ScriptableObject` reproduit
exactement ce contrat, là où 41 `AnimatorController` seraient à la fois plus lourds et moins
tolérants.

### 7.3 Audio (41 fichiers)

Copie directe. Réglages d'import : musique en `Streaming`, SFX en `Decompress on Load`.
⚠ `MusicDirector` fait des **fondus croisés à puissance constante** entre deux `AudioStreamPlayer` :
sous Unity, deux `AudioSource` + la même courbe. La table `AudioSystem.MixGainDb` (dont le −12 dB des
tirs de sentinelle, réglé à l'oreille sur trois itérations) se porte **telle quelle** — elle est en dB,
donc indépendante du moteur.

### 7.4 Les 8 JSON de tuning

Destination : **`Assets/StreamingAssets/data/`** — et non `Resources/`. Raison : la convention du
projet est explicite, « tuning modifiable **sans recompiler** ». `StreamingAssets` conserve des
fichiers lisibles sur disque dans le build ; `Resources` les empaquette. Shim `DataFiles.Load(name)`.

### 7.5 Localisation

Unity n'a pas d'équivalent au `TranslationServer`. Bonne nouvelle : **tout le jeu passe par
`Loc.T(key)`**, un unique point d'entrée d'une ligne. → un lecteur CSV de ~60 lignes chargeant
`ui.csv`, plus le flag `--lang=<en|fr|es>` (utilisé par le trailer et les captures). Le paquet
officiel `com.unity.localization` serait **surdimensionné** ici et imposerait de reconstruire les
tables.

### 7.6 Polices

Share Tech Mono (AA on, size 16), VT323 en réserve → assets **TextMeshPro** à générer. ⚠ La police
étant utilisée à taille fixe sur une UI pixel, l'atlas TMP doit être généré en conséquence, sans quoi
le rendu du texte sera flou là où le reste est net.

---

## 8. Comment on **prouve** la parité

Le périmètre choisi (parité stricte) n'a de sens que s'il est mesurable. Quatre niveaux, du moins
coûteux au plus décisif.

### 8.1 Les 331 tests — nécessaire, très loin de suffisant

Ils passent dès le Lot 0 et couvrent **2 693 lignes sur 27 000, soit 10 %**. Ils garantissent que les
*règles* sont intactes ; ils ne disent **rien** des 24 300 lignes portées. Les prendre pour une
validation de migration serait la faute de raisonnement la plus facile à commettre ici.

### 8.2 Comparaison de bancs sur graines appariées — l'outil principal

Conditionné à PCG32 (§4.3). On lance la même campagne (`--overtime`, mêmes graines) sous Godot puis
sous Unity, et on compare les colonnes de `power_curve.log` avec `tools/power_loop.py --paired` —
**l'outil existant, sans le modifier**.

⚠ **Ce qui est comparable et ce qui ne l'est pas** : les tirages (cartes, spawns, affixes) le sont ;
la cadence de simulation ne l'est pas. Un écart de quelques pourcents sur un débit ne prouve pas une
régression. Le projet dispose déjà du bon critère : le **test des signes** sur graines appariées, et
le **plus petit écart détectable** annoncé par `power_curve_multi.py`.

⚠ **Toutes les références de banc existantes sont invalidées** —
`docs/bench/ref_overtime_1251_sat0.json` mesure un moteur qui ne sera plus le moteur cible. **Une
nouvelle référence Unity doit être établie (≈28 min de banc) avant la première décision de réglage
post-migration.** À défaut, on réglerait le jeu contre une base fausse — exactement le piège qui a
déjà coûté au projet une campagne entière et un diagnostic inversé (« le soin du Blindage n'était pas
notifié à `PowerTelemetry` »).

### 8.3 Captures avant/après sur les 18 écrans

Le projet a déjà cette pratique et son outillage (`tools/screenshot_*.py`, planches-contact). Deux
séries d'images, même résolution, même langue, écran par écran. C'est le seul contrôle praticable
pour un lot d'UI de 7 381 lignes.

### 8.4 Une run jouée — le juge de dernière instance

L'historique du projet est sans ambiguïté sur ce point : **chaque conclusion de banc a été précisée,
déplacée ou réfutée par une session jouée** (le découplage d'overtime réfuté, le cran I « aucune
difficulté », la régénération « immobile sans mourir », les vies de secours indistinguables). Le banc
mesure des débits ; il ne mesure ni le ressenti, ni la lisibilité, ni la latence d'entrée.

→ La migration n'est **déclarée finie** qu'après une run complète jouée jusqu'au boss, sur les deux
moteurs, par le testeur.

---

## 9. Points à ne pas oublier (et qui se paient cher si on les oublie)

### 9.1 Le banc de mesure est ~30 % de la valeur d'ingénierie du projet

`PowerTelemetry` (23 colonnes), `BossTelemetry`, `PressureMeter`, `BenchAutoPilot`,
`AutoPilotPolicy`, les flags `--seed`/`--saturation`/`--start-at`/`--saturate-arsenal`/`--auto-play`,
`power_loop.py`, `power_curve_multi.py`. C'est ce qui permet de **régler le jeu sans deviner**. Un
port qui livre un jeu jouable mais sans banc livre un jeu qu'on ne saura plus équilibrer.

Bonne nouvelle technique : `AutoPilotPolicy` est déjà de la **logique pure** (elle survit intacte),
et les arguments de ligne de commande fonctionnent en build Unity standalone
(`Environment.GetCommandLineArgs()`). Le travail est de rebrancher, pas de reconcevoir.

⚠ **Risque à vérifier tôt** : `-batchmode -nographics` sous Unity n'est pas l'équivalent exact de
`--headless` Godot. Le banc doit tourner **sans fenêtre et plus vite que le temps réel**. À
**prototyper dès le Lot 1**, pas à découvrir au Lot 7 — si c'est impossible, toute la méthodologie de
mesure change de forme.

### 9.2 `--timescale` a une limite connue

Le projet documente : au-delà de ×4, les projectiles traversent leurs cibles. Sous Unity, avec un
mouvement par transform (§4.4), **la même limite existera à un seuil différent**, à re-mesurer.

### 9.3 Les sauvegardes des joueurs existants

`user://save.json` (Godot) vit dans `%APPDATA%\Godot\app_userdata\Chimera Protocol\`.
`Application.persistentDataPath` (Unity) vit dans `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\`.

**Chemins différents.** Sans action, un joueur qui met à jour via l'app itch perd Échos, greffes,
perks, défis, records et complétions. → **une migration ponctuelle au premier lancement Unity** :
détecter l'ancien chemin, lire, réécrire au nouveau. `settings.cfg` (format Godot `ConfigFile`, avec
son `save_version=2` et sa table `biome:cran`) demande en plus un **parseur dédié** — il n'existe pas
sous Unity.

C'est le point le plus facile à oublier et le seul dont l'échec est **visible par les joueurs** et
**irréversible pour eux**. À traiter dans le Lot 6, avec la sauvegarde du testeur comme cas de test.

### 9.4 Ce qui disparaît ou change de nature

- **Discord Rich Presence** : le paquet NuGet `DiscordRichPresence` fonctionne sous Unity (Mono), à
  vérifier en IL2CPP.
- **`VersionStamp`** lit `config/version` de `project.godot` → devient `Application.version`.
- **`BuildInfo.GitSha`** est généré par `tools/gen_build_info.ps1` → à rebrancher sur le build Unity.
- **Le bandeau de mise à jour web** (`version.json` sur GitHub) est **indépendant du moteur** : seul
  le `HttpRequest` Godot devient `UnityWebRequest`. `VersionCompare` est déjà de la logique pure.

### 9.5 Le trailer et les captures store

`record_trailer.py` pilote le **Movie Maker de Godot** (`--write-movie`), qui n'a **pas
d'équivalent direct** sous Unity — il faudra soit Unity Recorder, soit une capture externe. Les
timecodes de l'EDL (`build_trailer.py`) ne survivront pas au changement de moteur : le trailer est à
**recapturer intégralement**, pas à reporter. Hors périmètre du port, mais à budgéter avant la
prochaine publication de la page itch.

---

## 10. Risques

| # | Risque | Impact | Parade |
|---|---|---|---|
| **R1** | Un bug bloquant de la 1.26.0 (cran III inbattable, IPS au cran V) apparaît **pendant** le gel | Les joueurs restent bloqués des semaines | Rompre le gel **volontairement** pour ce seul correctif, le porter immédiatement côté Unity, et le noter ici |
| ~~**R2**~~ | ~~Le banc Unity ne tourne pas headless plus vite que le temps réel~~ | — | ✅ **RETIRÉ** (§6.2) — mesuré **×94,8** |
| ~~**R3**~~ | ~~Les 200-300 entités ne tiennent pas les IPS sous Unity~~ | — | ✅ **RETIRÉ** (§6.2) — 0,168 ms/pas ; borne haute, marge ×42 |
| **R4** | Divergence de comportement silencieuse (une arme, un affixe, une phase de boss) | Un jeu qui « marche » mais n'est plus le même | §8.2 + §8.3 + §8.4. C'est précisément pourquoi la parité doit être **prouvée**, pas supposée |
| **R5** | Perte des sauvegardes joueurs (§9.3) | Irréversible, visible, et le pire retour possible | Migration au premier lancement, testée sur une vraie save |
| **R6** | Le port s'arrête à mi-chemin | Un dépôt à deux moteurs, aucun des deux fini | Les lots 0-4 livrent un jeu **jouable** ; en cas d'arrêt, Godot reste publiable puisqu'il est intact |
| ~~**R7**~~ | ~~Sérialisation JSON cassée en IL2CPP~~ | — | ✅ **RETIRÉ** (§6.2) — build IL2CPP réussi, **14/14** à l'exécution, aller-retour `System.Text.Json` et camelCase compris |

**R6 mérite d'être souligné** : le gel de Godot est un choix de méthode, pas une destruction. À tout
instant, la 1.26.0 reste exportable et publiable. La migration est réversible tant qu'elle n'est pas
publiée.

---

## 11. Documentation des agents et skills — la nouvelle stack

Mesure de l'empreinte moteur dans la doc existante :

| Fichier | Mentions moteur | Traitement |
|---|---:|---|
| `.claude/skills/carte-projet/SKILL.md` | **25** | **Réécriture complète** → nouvelle carte de `unity/` (arborescence, prefabs, asmdef, checklists de câblage revues) |
| `.claude/skills/publier-itch/SKILL.md` | 6 | Réécriture du pipeline (build Unity par `-executeMethod`, butler inchangé) |
| `.claude/agents/release-manager.md` | 7 | Réécriture de la procédure d'export |
| `.claude/agents/game-tester.md` | 5 | Réécriture (lancement Unity, flags, chemins de logs) |
| `.claude/agents/developpeur.md` | 4 | Réécriture (conventions Unity, couche Platform, pièges) |
| `.claude/agents/musicien.md` | 2 | Retouche (import audio Unity) |
| `.claude/agents/graphiste.md` | 1 | Retouche (postprocessor d'import, §7.1) |
| `.claude/agents/directeur-artistique.md` | 1 | Retouche |
| `.claude/agents/story-teller.md` | 1 | Retouche |
| **`.claude/agents/game-designer.md`** | **0** | ✅ **Aucun changement** — le design est moteur-agnostique |
| **`.claude/agents/marketing.md`** | **0** | ✅ **Aucun changement** |

Docs projet : `docs/ARCHITECTURE.md` (23) et `docs/PITFALLS.md` (54) sont à **doubler**, pas à
remplacer — les pièges Godot restent vrais tant que la branche Godot existe et sert de référence.

→ **`docs/ARCHITECTURE_UNITY.md`** et **`docs/PITFALLS_UNITY.md`**, neufs, qui se remplissent **au fil
des lots** et non à la fin. Un piège Unity se documente le jour où il coûte une heure, pas trois
semaines plus tard.

`docs/GDD.md` (91 mentions) : ce sont presque toutes des **références de chemins de code** dans un
document de design. Le design lui-même ne bouge pas. → passe de mise à jour des chemins en fin de
Lot 6, pas de réécriture.

⚠ **Règle du projet à appliquer ici** : « un agent qui décrit un état périmé du projet donne des
instructions fausses avec autorité ». Pendant la migration, **les agents décriront forcément un état
périmé** — soit Godot alors qu'on code en Unity, soit l'inverse. → chaque agent réécrit doit porter,
en tête, **quel moteur il décrit et à quel lot il a été mis à jour**.

---

## 12. Ce que cette migration coûte, et ce qu'elle rapporte

Dit franchement, pour que la décision reste éclairée à mi-parcours :

**Coût** : ~24 300 lignes réécrites, 65 scènes reconstruites, 18 écrans revalidés visuellement, tout
l'outillage de banc rebranché, toutes les références de mesure refaites, 9 agents et 2 skills
réécrits, un trailer à recapturer.

**Gain de gameplay** : **aucun.** À la fin, le joueur doit voir *exactement* le même jeu — c'est la
définition même du périmètre choisi.

Ce que la migration apporte réellement est ailleurs : écosystème Unity, portabilité (console, mobile),
disponibilité des assets et de la main-d'œuvre, et — spécifique à ce projet — la **contrainte
d'`asmdef` qui rend l'invariant « la logique pure ne dépend pas du moteur » impossible à violer, là
où il repose aujourd'hui sur la discipline.

Ces raisons sont valables. Elles ne sont simplement pas des raisons de *gameplay*, et le plan ne
prétend pas le contraire.

---

## 13. Prochaine action — Lot 1

**Lot 0 est terminé (§6.1).** Le Lot 1 construit la couche `Platform/` (§4), mais il commence par
**deux prototypes qui peuvent invalider la méthode entière**. Les faire *avant* d'écrire du code
qu'ils rendraient inutile :

1. **R2 — le banc headless** (§9.1). Un projet Unity nu, `-batchmode -nographics`, qui simule une
   boucle fixe et écrit un log. Question à trancher : **tourne-t-il sans fenêtre et plus vite que le
   temps réel ?** Si non, toute la méthodologie de mesure change de forme, et cela doit se savoir
   maintenant, pas au Lot 7.
2. **R3 — 300 entités** (§10). Prototype nu : 300 sprites qui se déplacent vers une cible avec la
   séparation manuelle du jeu (§4.4). Mesurer les **IPS**. Le jeu est déjà tendu au cran V ; si Unity
   ne tient pas la charge, la conception du mouvement se décide ici.
3. **Le postprocessor d'import** (§7.1) — *premier fichier Unity à écrire*, avant tout import
   d'assets, sinon les 905 sprites seront à réimporter.
4. **Les shims**, par ordre de dépendance : `Gd` (+ **PCG32**, §4.3) → `GTimer` → `Deferred` →
   `SceneRoot` → `GTween` → `Spawner` → `FrameAnimator`.
5. **Un build IL2CPP de contrôle**, même sur un projet quasi vide — il coûte peu et fait remonter tôt
   les surprises d'AOT (R7).

**Critère de sortie** : des tests unitaires **neufs** sur les shims — PCG32 comparé à des valeurs de
référence produites par Godot, ordonnancement de `Deferred`, et `GTween` qui progresse bien à
`timeScale = 0`.
