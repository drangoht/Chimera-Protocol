# ARCHITECTURE — documentation technique du code

Documentation **technique** de Chimera Protocol : comment le code est organisé, pourquoi il l'est
ainsi, et ce qu'il faut savoir avant d'y toucher.

**Ce document décrit le code.** Il ne remplace pas :

| Question | Document |
|---|---|
| *Pourquoi* le jeu est réglé ainsi (design, valeurs, courbes) | `docs/GDD.md` |
| *Où* se trouve tel système, arme, écran (index + checklists) | skill `/carte-projet` |
| *Quels pièges* guettent dans un domaine donné | `docs/PITFALLS.md` |
| *Où en est* l'implémentation, version par version | `docs/PROJECT_STATE.md` |
| *Ce qui a été mesuré*, et comment | `docs/TEST_REPORT.md` |

État au **2026-08-02** : 156 fichiers C#, ~26 600 lignes, **319 tests**, build sans avertissement.

---

## 1. Pile technique

| | |
|---|---|
| Moteur | **Godot 4.7 .NET** — toujours la variante `.NET`, jamais la standard |
| Langage | C# (.NET 8), GodotSharp |
| Cible | Windows (`.exe`), distribué sur itch.io via Butler |
| Tests | xUnit — `dotnet test tests/ChimeraProtocol.Tests.csproj` |

**Build export** :

```
"…/Godot_v4.7-stable_mono_win64.exe" --headless \
    --export-release "Windows Desktop" "./build/ChimeraProtocol.exe"
```

⚠ **`ChimeraProtocol.sln` DOIT exister à la racine**, sinon le `.exe` exporté crashe au lancement.
À recréer si perdu :

```
dotnet new sln --name ChimeraProtocol --format sln
dotnet sln ChimeraProtocol.sln add ChimeraProtocol.csproj
```

---

## 2. Le principe structurant : la logique pure est séparée du moteur

C'est **la** décision d'architecture du projet, et tout le reste en découle.

> Toute règle chiffrée — courbes, seuils, tables, formules — vit dans `src/Core/Rules/` sous forme de
> **classe statique sans aucune dépendance Godot**. Les nœuds de scène délèguent à ces classes.

**Pourquoi.** Un nœud Godot ne peut pas être instancié hors du moteur : le tester demanderait de
lancer le jeu. En sortant la décision chiffrée du nœud, elle devient testable en millisecondes — les
319 tests s'exécutent en **~25 ms** parce qu'ils ne touchent jamais Godot. Le projet de test ne
référence d'ailleurs même pas le projet du jeu ; il **compile directement les sources** de `Rules` :

```xml
<!-- tests/ChimeraProtocol.Tests.csproj -->
<Compile Include="..\src\Core\Rules\*.cs" />
```

**Conséquence pratique** : une classe de `Rules` qui aurait besoin de `using Godot` est le signe qu'un
mauvais découpage a eu lieu — c'est le nœud appelant qui doit faire le travail moteur.

**Conséquence de conception, plus importante encore** : c'est ce qui rend le jeu *réglable*. Un
équilibrage se change dans une classe pure couverte par des tests qui expliquent l'intention, pas au
milieu d'une boucle de rendu.

---

## 3. Cartographie

```
src/
  Core/          7 fichiers   1 025 l.   Amorçage, sauvegarde, constantes, hooks de debug
  Core/Rules/   25 fichiers   2 547 l.   ★ Logique pure testable — le cœur des règles
  Systems/      34 fichiers   7 437 l.   Singletons (autoloads) + systèmes runtime
  UI/           27 fichiers   7 216 l.   Écrans et HUD (Control)
  Entities/     27 fichiers              Player, Enemies, MiniBoss, Boss, Environment
  Weapons/      31 fichiers   2 725 l.   Armes, projectiles, fusions
scenes/                                  .tscn — arborescences de nœuds
assets/                                  Sprites, audio, polices (bruts)
data/          8 fichiers                JSON de tuning, modifiable sans recompiler
localization/  ui.csv                    EN / FR / ES
tools/                                   Scripts Python/PowerShell (génération, banc, release)
tests/         11 fichiers   2 630 l.    xUnit
```

### 3.1 `src/Core/` — amorçage et services transverses

| Fichier | Rôle |
|---|---|
| `GameManager.cs` | Autoload racine : état de run, biome courant, référence au `Player` |
| `SaveManager.cs` | Lecture/écriture de `user://save.json`. **Renvoie une copie fraîche à chaque `Load()`** |
| `SaveData.cs` | DTO de sérialisation |
| `Constants.cs` | Dimensions d'arène, noms de groupes de nœuds |
| `DebugHooks.cs` | Parsing des flags de ligne de commande (§9) |
| `SceneCleanup.cs` | Purge des VFX parentés à la racine entre deux runs |
| `BuildInfo.cs` | Version + SHA git, injectés au build par `tools/gen_build_info.ps1` |

### 3.2 `src/Core/Rules/` — les 25 règles pures

Regroupées par ce qu'elles décident :

**Progression du joueur**
`XpCurve` · `WeaponLeveling` · `PassiveScaling` (extrapolation des passifs au-delà des 3 niveaux
définis, à rendements décroissants) · `OverloadCards` (les 3 cartes sans plafond de fin de partie) ·
`StatCaps` (plafonds durs : DR 0,40 · vitesse 380 · réduction de recharge 0,75) · `RarityWeights` ·
`WeightedPicker`

**Menace et difficulté** — quatre axes multiplicatifs, à ne pas confondre :
`DifficultyTuning` (réglage du joueur) · `LevelThreat` (palier du biome joué) ·
`OvertimeEscalation` (escalade après la 13ᵉ minute) · `SaturationTable` (échelle de fin de partie) ·
`EnemyScaling` · `SpawnCurve` · `EliteAffixTable` · `CrowdControlCaps`

**Boss** — `BossPhases` (3 phases, progression **irréversible**) · `BossIncarnations` (5 variantes
par biome)

**Méta-progression** — `EchoFormula` · `ChallengeTable` · `GraftTable` (Assimilation)

**Survie** — `RegenReserve` (le surplus de régénération perdu à PV pleins devient un tampon anti-pic)

**Instrumentation & outillage** — `PressureMeter` (§10) · `AutoPilotPolicy` (pilote du banc) ·
`MusicIntensity` · `VersionCompare`

### 3.3 `src/Systems/` — les singletons

Déclarés en `[autoload]` dans `project.godot`, accessibles partout via `NomSystem.Instance` :

`GameManager` · `XpSystem` · `InventorySystem` · `LevelUpSystem` · `AssimilationSystem` ·
`SaveManager` · `MetaProgressionSystem` · `ChallengeSystem` · `AudioSystem` · `MusicDirector` ·
`ScreenShake` · `GameSettings` · `DiscordPresence` · `VersionStamp` · `FusionFlash` (scène)

⚠ **L'ordre des `_Ready()` entre nœuds frères n'est pas garanti.** Un réglage qui dépend du biome joué
doit se résoudre **à la demande**, jamais être figé dans un `_Ready` — c'est le piège qui a fait lire à
`Player._Ready` le cran de saturation d'un biome pas encore résolu (cf. `GameSettings.Saturation`).

---

## 4. Anatomie d'une run

```
MainMenu → LevelSelectScreen (biome + cran de saturation)
    ↓
Game.tscn
    ├── GroundRenderer      pose CurrentBiomeId  ⚠ avant tout lecteur de réglage
    ├── Player              + WeaponManager + GraftManager
    ├── EnemySpawner        vagues, élites, mini-boss, boucle de boss en overtime
    ├── RunStatsTracker     horloge de run, overtime, fin de run, télémétrie
    └── HUD
    ↓
XpSystem → LevelUpSystem → LevelUpScreen (met le jeu en PAUSE)
    ↓
13ᵉ minute : OVERTIME — le timer ne termine plus la run
    ↓
RunEndScreen → Échos → Hub / méta-progression
```

**Points de cycle de vie à connaître :**

- La run ne se termine **jamais** au chronomètre. À l'expiration du timer on entre en **overtime** ;
  la run finit à la mort du joueur (ou sur `--run-limit` au banc). Battre le boss marque la
  complétion du niveau sans arrêter la partie.
- Les écrans modaux **mettent la physique en pause**. Un test headless qui attend un déplacement
  pendant qu'un `LevelUpScreen` est ouvert attendra indéfiniment.
- Les VFX parentés à la **racine** (pour survivre à la mort de leur émetteur) doivent être purgés par
  `SceneCleanup.ClearWorldVfx` en sortie de run, sinon ils fuient d'une run à l'autre.

---

## 5. Entités

### 5.1 `EnemyBase` — la souche commune

Tous les ennemis en héritent : faune, mini-boss, champions de biome, boss de fin. Elle porte les PV,
la mise à l'échelle temporelle, les affixes d'élite, le lifesteal, le ralentissement et la mort.

**`TakeDamage` est `virtual`** pour une seule raison : `RustedCore` doit ignorer les PV pendant sa
surcharge de bascule de phase **tout en conservant le flash de coup** — sans quoi le joueur croit ses
armes cassées.

**`DealDiscreteDamage(player, raw)` est le chemin unique des coups portés au joueur.** Il applique,
dans l'ordre : le plancher du cran de saturation VI si l'attaquant est un champion, puis la réduction
de dégâts, puis le lifesteal de l'affixe Vampirique.

⚠ **Ce chemin ne concerne que les coups DISCRETS** — contact à intervalle, projectile, attaque
télégraphiée. Les dégâts **continus** (faisceau du boss, flaques de magma, geysers), exprimés en
PV/seconde × delta, ne l'empruntent pas : un plancher en pourcentage des PV max appliqué à chaque tick
tuerait le joueur en quelques frames et changerait une zone au sol en mort instantanée.

*Effet de bord voulu de cette centralisation* : la réduction de dégâts était auparavant recopiée par
huit appelants ; un neuvième qui l'aurait oubliée n'aurait été visible nulle part.

### 5.2 `Player`

`TakeDamage` applique une **chaîne de filtres ordonnée**, et l'ordre est significatif :

```
--invuln  →  i-frames de dash  →  Plaque Adaptative  →  Égide  →  i-frames (0,45 s)
                                                                      ↓
                                        réserve de régénération (RegenReserve)
                                                                      ↓
                                                                     PV
```

⚠ **Les i-frames de 0,45 s sont marquées CRITIQUE** : c'est le seul levier qui rend les nuées de
200-300 entités jouables. La réserve de régénération vient **après** — elle est un dernier rempart,
jamais un substitut aux i-frames.

⚠ **Un soin ne s'écrit JAMAIS dans `CurrentHp`.** Il passe par `Player.Heal(percent)` ou
`HealFlat(amount)` — seuls chemins qui appliquent le multiplicateur de saturation *et* notifient
`PowerTelemetry`. Deux régressions ont été causées par des écritures directes (`OverloadCards.Plating`
et le passif `reinforced_plating`), rendant invisible à l'instrument le canal de soin **dominant**.

### 5.3 `GraftManager` — et pourquoi il est en deux fichiers

Applique les effets des greffes équipées : stats (avec retrait exact malgré les plafonds),
comportements (essaims orbitants, tourelles, épines, ondes), teinte cumulée.

Il est découpé en **classe partielle** :

| Fichier | Contenu |
|---|---|
| `GraftManager.cs` (680 l.) | Stats, comportements, cycle de vie |
| `GraftManager.Props.cs` (310 l.) | **Props de silhouette** : géométrie procédurale pure |

Les props sont des assemblages de `Polygon2D` ancrés au corps du joueur et ombrés en pseudo-3D. Ils ne
touchent ni les stats, ni les comportements, ni l'état des greffes : les garder dans le fichier
principal noyait la logique de combat sous du dessin. Une classe partielle partage les mêmes champs —
le découpage n'a **aucun effet** sur le comportement.

### 5.4 `WeaponBase` — le contrat des 30 armes

```csharp
public abstract partial class WeaponBase : Node2D
{
    protected abstract void Attack();     // seul membre à implémenter
}
```

Le socle gère le timer, la cadence (`FireRateMultiplier`) et l'acquisition de cibles.

⚠ **`CaptureBaseDamage()` est le mécanisme qui empêche l'empilement.** Les dégâts « de fiche » sont
mémorisés **une fois** (idempotent) parce que `RefreshWeaponDamages` est rappelé plusieurs fois par
run (Noyau Thermique, améliorations du Hub) : sans cette référence, chaque recalcul se cumulerait au
précédent.

⚠ **`FireRateMultiplier` est `static`** — il doit être réinitialisé à chaque run par `Player._Ready`,
sinon il fuit d'une partie à l'autre.

⚠ **Les fusions posent leurs dégâts en dur dans leur `_Ready`** (mécaniques trop spécifiques pour le
JSON), contrairement aux armes de base qui les lisent dans `weapons.json`. C'est la source d'un bug
majeur corrigé en 1.21.0 : les 9 fusions ne recevaient ni niveau ni multiplicateurs, divisant le DPS
de fin de run par 3 à 6.

---

## 6. Les quatre axes de difficulté

Ils sont **multiplicatifs** et se cumulent. Les confondre rend tout diagnostic impossible — c'est
arrivé, et il a fallu trois sessions jouées pour isoler une cause.

| Axe | Classe | Portée |
|---|---|---|
| Réglage du joueur | `DifficultyTuning` | Assistance (« Facile ») — hors échelle de challenge |
| Palier du niveau | `LevelThreat` | Croît avec l'ordre de déblocage du biome (PV, dégâts, densité, Échos) |
| Escalade d'overtime | `OvertimeEscalation` | Après la 13ᵉ min — densité ×4 et stats ×1,5, **découplées** |
| Saturation | `SaturationTable` | Échelle de fin de partie choisie par le joueur, **par niveau** |

**Deux principes de conception à ne pas casser :**

1. **Les PV des champions sont amortis** (`LevelThreat.ChampionHpSoftening = 0,55`). Battre le boss
   conditionne la progression et son TTK est calibré sur une **session jouée** : lui appliquer le
   bonus plein en ferait un mur de patience.
2. **Un cran de saturation ajoute une RÈGLE, pas un multiplicateur.** Les statistiques ne montent plus
   après le cran II, et un test le verrouille. Empiler des chiffres est précisément l'échange que le
   joueur gagne toujours.

### Ajouter un cran — cinq points

1. `SaturationTable` : `MaxRank`, l'entrée dans `Ranks`, la fonction de règle.
2. `localization/ui.csv` : `SAT_n_NAME` / `SAT_n_RULE` en **EN/FR/ES** (la règle se lit *avant* de
   lancer — c'est elle qui rend une mort interprétable).
3. Le test `Chaque_Cran_Ajoute_Exactement_Une_Regle` : y déclarer la nouvelle dimension, sinon il
   passe au vert en ignorant la règle qu'on vient d'écrire.
4. `LevelSelectScreen.RomanNumeral` : table courte.
5. Inventorier les **écritures directes** de la ressource visée (`grep`) — une règle correctement
   implémentée sur un canal incomplet se mesure très bien, et faux.

---

## 7. UI

### Conventions non négociables

- **Jamais de `StyleBoxFlat` ad hoc ni de couleur en dur**, ni en C# ni dans les `.tscn`. Tout passe
  par `UiPalette` (couleurs) et `UiStyle` (fabrique des cadres « plaque blindée »).
- **Localisation systématique** via `Loc.T("CLÉ")` → `localization/ui.csv`.

### Calques (`layer`) — l'ordre compte

Valeurs effectives (relevées dans le code et les scènes) :

| Calque | Nœud |
|---|---|
| 80 | Sol (`GroundRenderer`) |
| 85 | `Banner` |
| 89 / 99 | `FusionFlash` (deux couches) |
| **90** | **`PostFX`** — vignette, dans `Game.tscn` |
| 95 | `HUD` |
| 96 | `BuffBar` |
| 97 | `LevelUpScreen`, `AssimilationScreen` |
| 98 | `RunEndScreen` |
| 100 | `PauseScreen` |
| 110 | `OptionsScreen` |
| 128 | `VersionStamp` |

⚠ **`PostFX` est à 90, et c'est le piège** : les modales étaient sous ce calque (10, 20, 60) et se
retrouvaient **assombries par la vignette**. Elles ont été remontées **au-dessus de 90**. Toute
nouvelle modale doit l'être aussi.

⚠ **`AssimilationScreen.tscn` déclare `layer = 60`, mais le C# force `Layer = 97` dans `_Ready`.**
Lire la scène seule induit en erreur — c'est le code qui fait foi.

⚠ **Le HUD passe au-dessus des écrans qui mettent en pause, et gèle avec eux** — il ne peut donc pas
se masquer lui-même. Un widget large ou centré (la barre de boss fait 520 px) doit être retiré **par
l'appelant**, au moment où l'écran prend la main.

### Navigation clavier / manette

- Une liste **non focalisable** doit être scrollée à la main dans `_UnhandledInput`.
- Une liste **focalisable** qui déborde exige une chaîne de focus explicite + `EnsureControlVisible`.
- Un panneau qui peut grandir (build de fin de run) a besoin d'un `ScrollContainer` **et d'un
  plafond** — et ce plafond se **mesure** (`GetCombinedMinimumSize().Y − budget`), il ne se devine
  pas. Centré sans plafond, un panneau déborde **des deux côtés** et les boutons sortent de l'écran.
- Le défilement clavier utilise `ui_page_up/down` **seuls** : `ui_up/down` appartiennent à la chaîne
  de focus des boutons.

---

## 8. Données et persistance

### `data/*.json` — tuning sans recompilation

`weapons.json` · `enemies.json` + `enemies_biome_expansion.json` · `grafts.json` ·
`levelup_config.json` · `meta_upgrades.json` · `challenges.json` · `texts.json`

### Deux fichiers de sauvegarde, deux propriétaires

| Fichier | Contenu | Propriétaire |
|---|---|---|
| `user://save.json` | Échos, upgrades méta, défis, greffes, perks | `MetaProgressionSystem` |
| `user://settings.cfg` | Préférences, records, complétions, découvertes | `GameSettings` |

⚠ **`SaveManager.Load()` renvoie une copie fraîche** — ce n'est pas un singleton.
`MetaProgressionSystem` détient l'**unique** copie en mémoire du bloc méta. Tout autre système qui
doit y écrire (ex. `ChallengeSystem`) **mute `MetaProgressionSystem.Meta` puis appelle
`PersistMeta()`** — jamais charger sa propre `SaveData`, la muter et la sauvegarder : les deux copies
divergent et la dernière écriture écrase les Échos gagnés dans l'autre.

### Migrations de `settings.cfg`

Versionnées par un entier **`gameplay/save_version`** : **0** = avant la saturation · **1** = cran
global · **2** = cran par niveau.

- **Ne jamais détecter un ancien format par « clé absente »** : ça ne marche qu'une fois.
- **Écrire `save_version` inconditionnellement** — sinon la migration se rejoue à chaque démarrage et
  réécrit des choix que le joueur a faits depuis.
- **Une migration ne retire jamais un accès**, et n'en ouvre jamais un de trop (le choix reste borné
  par le déblocage).
- **Migrer après les données dont la migration dépend.**

---

## 9. Hooks de debug (`DebugHooks`)

Flags de ligne de commande, sans effet en build joueur :

| Flag | Usage |
|---|---|
| `--auto-play` | Bot de banc (`AutoPilotPolicy`) — kite, ramasse, dashe, **meurt pour de vrai** |
| `--seed=<n>` | Graine déterministe → **comparaison appariée** |
| `--saturation=<n>` | Cran de saturation (le bot ne traverse pas l'écran de sélection) |
| `--start-at=<min>` · `--saturate-arsenal` (= `--overtime`) | Démarrer à l'entrée en overtime, état standardisé |
| `--run-limit=<s>` | Termine la run (issue `bench_limit`). **Sans lui, une run headless ne s'arrête jamais** |
| `--power-curve` | Active `PowerTelemetry` |
| `--timescale=<x>` · `--invuln` · `--biome=<id>` · `--debug-boss` · `--debug-enemy=<id>` · `--force-elites` · `--force-graft=all` · `--force-fusion` · `--force-buff` · `--lang` · `--trailer` | |

⚠ Les flags à valeur prennent un **`=`** (`--seed=42`).

---

## 10. Instrumentation — comment le jeu se mesure

Trois instruments, trois usages :

| Instrument | Activation | Ce qu'il produit |
|---|---|---|
| `BossTelemetry` | **toujours** | Un bloc par combat de boss dans `user://boss_ttk.log` (TTK, phases) |
| `PowerTelemetry` | `--power-curve` | Un échantillon toutes les 15 s dans `user://power_curve.log` — 23 colonnes |
| `PressureMeter` | via `PowerTelemetry` | Pression **ressentie**, échantillonnée **à la frame** |

**Les journaux s'écrivent au fil de l'eau**, pas à la fin : un banc interrompu garde tout ce qui a été
mesuré.

### ⚠ Deux colonnes se lisent de travers — et les deux ont produit un faux diagnostic

**① `soins_ps` compte le soin RETENU**, borné par les PV manquants : à PV pleins, un soin vaut
**zéro**. C'est une mesure de *conversion*, qui monte mécaniquement dès que le joueur prend plus de
dégâts. Pour « ce réglage soigne-t-il plus ? », lire **`soins_bruts_ps`** (offert). Lu à l'envers, le
cran V paraissait rendre **+41 %** de soins ; il en donne **−46 %**. Deux correctifs ont été écrits et
annulés sur cette lecture.

**② Toutes les colonnes de débit sont moyennées sur 15 s**, donc **aveugles aux pics**. Un plongeon à
10 % des PV suivi d'une remontée ne déplace aucune moyenne — et c'est pourtant ce qu'un joueur appelle
« difficile ». Pour « ce réglage se sentira-t-il ? », lire `frolements` / `pv_min_pct` /
`part_danger`.

### `PressureMeter` — compter des événements, pas des débits

Observe la barre de vie **à la frame** et relève par fenêtre : le nombre de **frôlements** (passages
sous 30 % des PV max), le **plus bas ratio** atteint, la **part du temps** en zone critique.

Deux propriétés qui ne sont pas des détails :

- **Hystérésis** (entrée 30 %, sortie 55 %) : sans elle, un joueur qui oscille autour du seuil
  compterait un frôlement **par frame**, et la métrique mesurerait la fréquence de rafraîchissement.
- **L'état d'hystérésis survit à la clôture d'une fenêtre** : un creux à cheval sur deux échantillons
  est un seul épisode. Le remettre à zéro ferait dépendre le total du réglage de l'instrument.

### Le banc

```
py tools/power_curve_multi.py --overtime --runs 1 --minutes 20 --saturation <n> --seed-base <s>
py tools/power_loop.py --paired <cranA> <cranB>
```

⚠ **Lancer les runs au premier plan, une par une** — les tâches de fond ont été tuées en pleine
campagne. Le journal est cumulatif, donc les runs s'agrègent.

⚠ **Comparer un cran cumulatif au cran PRÉCÉDENT**, jamais au cran 0.

⚠ **`--min-samples` est un biais de survie** : une run courte parce que le joueur **meurt vite** est
le meilleur résultat du réglage, pas un déchet. L'outil signale désormais les runs mortelles qu'il
écarte, et affiche le **taux de morts** en tête de rapport — la seule mesure qu'aucune convention de
résumé ne peut déformer.

---

## 11. Tests

**319 tests**, ~25 ms, aucune dépendance Godot.

| Fichier | Tests | Objet |
|---|---|---|
| `RulesTests.cs` | 68 | XP, scaling, plafonds, passifs, Échos, surcharge |
| `SaturationTableTests.cs` | 35 | Les 6 crans + migrations de difficulté |
| `GraftTableTests.cs` | 26 | Assimilation : parsing, jauges, affinités |
| `MusicIntensityTests.cs` | 25 | Intensité, lissage, fondu croisé |
| `RegenReserveTests.cs` | 16 | Réserve anti-pic |
| `BossPhasesTests.cs` | 14 | Phases, irréversibilité |
| `ChallengeTableTests.cs` | 13 | Défis |
| `AutoPilotPolicyTests.cs` | 12 | Pilote du banc |
| `BossIncarnationsTests.cs` | 11 | 5 incarnations |
| `PressureMeterTests.cs` | 11 | Frôlements, hystérésis, indépendance à la frame |
| `AudioAssetReferenceTests.cs` | 1 | Tout id passé à `PlaySfx` a un `.wav` |

**Ce que ces tests cherchent à empêcher.** Ils ne visent pas la couverture de lignes mais les
**régressions d'intention**. Exemples représentatifs :

- `Les_Statistiques_Ne_Montent_Plus_Apres_Le_Rang2` — casse si quelqu'un « rééquilibre » en remontant
  des facteurs, ce que le design interdit.
- `Chaque_Cran_Ajoute_Exactement_Une_Regle` — garantit qu'une mort reste interprétable par le joueur.
- `Le_Plancher_Laisse_Au_Joueur_Une_Marge_De_Reaction` — au moins 6 coups pour vider la barre, sinon
  le cran VI cesse d'être « ne tanke plus le boss » pour devenir « ne l'approche jamais ».
- `Le_Compte_Ne_Depend_Pas_De_La_Frequence_De_Frame` — sans quoi deux campagnes de banc lancées à des
  `--timescale` différents seraient incomparables.

`AudioAssetReferenceTests` est d'une autre nature : il scanne le **source** à la recherche des
littéraux passés à `PlaySfx`/`PreloadSfx` et vérifie que le `.wav` existe. Un id inventé ne se voyait
autrement qu'en ouvrant l'écran concerné.

**Audit du 2026-08-02** : aucun test sans assertion, tautologique ou redondant. Les 25 règles pures
ont toutes au moins un test.

---

## 12. Dette technique connue

Constats de l'audit du 2026-08-02, **non corrigés**, avec la raison.

### 12.1 Données persistées jamais lues — `_discoveredGrafts`

`GameSettings.DiscoverGraft()` est appelé à chaque première assimilation d'une greffe, **écrit sur
disque** (`Save()`), et la donnée est sérialisée dans `settings.cfg`. Or `IsGraftDiscovered()`, son
unique lecteur, **n'est appelé nulle part** : `ChimeraCodexScreen` affiche **toutes** les greffes sans
filtre.

L'asymétrie est nette avec les armes : `ArsenalScreen` masque bien les armes non découvertes via
`GameSettings.IsDiscovered`.

**Non corrigé** parce que les deux issues sont des décisions de **design**, pas de technique :
soit brancher le filtre sur le Codex Chimère (les greffes deviennent une collection à découvrir), soit
retirer la collecte (les greffes sont un écran explicatif, et on cesse d'écrire sur disque pour rien).

### 12.2 Couleurs de palette sans appelant

`UiPalette.PanelBg`, `PanelSunken` et `SteelContact` n'ont plus d'appelant depuis la refonte des
cadres « plaque blindée » (1.16.0), où les panneaux sont devenus des `StyleBoxTexture` pré-rendus.

**Conservées** : ce fichier est la **charte de couleurs** du jeu, pas un utilitaire. `SteelContact`
complète notamment les *quatre faces* de l'ombrage pseudo-3D et sert de référence à
`tools/generate_ui_frames.py`. Une teinte retirée ici se retrouverait réinventée en dur au premier
écran qui en aurait besoin — ce que les conventions du projet interdisent. Le commentaire de chaque
constante l'indique désormais, pour qu'un futur audit ne les re-signale pas.

### 12.3 Scripts temporaires dans `tools/`

`tools/_tmp_audio_meter.py` et `tools/_tmp_music_drive.py` (144 lignes) ne sont **pas suivis par git**
et ne sont référencés nulle part. Reliquats locaux : à supprimer ou à ignorer explicitement.

### 12.4 Fichiers volumineux restants

Après le découpage de `GraftManager` :

| Fichier | Lignes |
|---|---|
| `Player.cs` | 812 |
| `HUD.cs` | 765 |
| `GameSettings.cs` | 734 |
| `InventorySystem.cs` | 703 |
| `GraftManager.cs` | 680 |
| `RustedCore.cs` | 608 |

Aucun n'est en soi problématique : ce sont des façades cohérentes (l'entité joueur, l'affichage, les
réglages). `HUD.cs` est le plus découpable — ses widgets (barre de boss, indicateurs de greffes,
jauges d'assimilation) sont indépendants les uns des autres.

---

## 13. Conventions

- **Nommage** : PascalCase classes/méthodes · `_camelCase` champs privés · `readonly` par défaut.
- **Commentaires** : en français, et ils expliquent le **pourquoi**, pas le quoi. Un commentaire qui
  paraphrase le code est du bruit ; un commentaire qui dit « sans ce garde-fou, un 2ᵉ boss spawnait
  toutes les 28 s » évite une régression.
- **Sprites** : PNG transparent, grille 32×32 (exceptions documentées), `texture_filter = Nearest`
  global, pseudo-3D via `tools/pseudo3d_lib.py` — toujours dériver shadow/highlight avec
  `shade()`/`shade_sprite()`, jamais des couleurs plates ad hoc.
- **Hiérarchie de taille des ennemis**, alignée sur le rôle : faune 32 · mini-boss globaux 64 ·
  champions de biome 72 · boss 154.
- **Audio** : musique générée sur Suno (plan gratuit = **usage non commercial**) — ne jamais éditer un
  `.ogg` à la main. SFX = WAV Kenney CC0. Mixer selon la **polyphonie réelle** (N sentinelles contre
  1 arme), pas seulement selon le niveau du fichier.
- **Performance cible** : 200-300 entités simultanées.
- **Python** : `C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe` (hors PATH).

---

## 14. Ajouter quelque chose — points d'entrée

| Ajout | Chemin |
|---|---|
| **Arme** | `src/Weapons/` (hériter `WeaponBase`, implémenter `Attack()`) + `weapons.json` + `AllWeaponIds` + Codex |
| **Ennemi** | `enemies.json` (+ biome) ; comportement propre → sous-classe d'`EnemyBase` + scène + `EnemySpawner.ScenePaths` |
| **Cran de saturation** | §6 (cinq points) |
| **Carte de level-up** | `levelup_config.json` + `LevelUpSystem` |
| **Greffe / fusion** | `grafts.json` + `GraftTable` + effet dans `GraftManager` (+ prop dans `GraftManager.Props.cs`) |
| **Écran** | `src/UI/` + `scenes/ui/` — `UiStyle`/`UiPalette` obligatoires, calque à choisir (§7) |
| **Règle chiffrée** | `src/Core/Rules/` + tests. **Toujours ici**, jamais dans un nœud |

**Avant de coder dans un domaine, lire `docs/PITFALLS.md`** — il recense les pièges non évidents
(API Godot manquantes, callbacks, threading, checklists de câblage, cycle de vie des scènes, tests
headless) qui ont chacun coûté au moins une régression.
