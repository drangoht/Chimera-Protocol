# Design — Le Protocole Chimère : l'Assimilation

> **Statut : DESIGN CHIFFRÉ — Phase A prête à coder (2026-07-06, `game-designer`).** Différenciateur
> retenu au brainstorm 2026-07-06 (« assimilation d'ennemis ») puis **validé par l'utilisateur** avec
> le cadrage suivant : écran dédié « ASSIMILATION », jauge de kills PAR archétype d'ennemi
> (déterministe, pas de drop à ramasser), 3 slots fixes + remplacement au-delà pour le MVP (fusions
> de greffes reportées en phase B).
>
> La **Partie I** ci-dessous est la proposition d'origine (conservée telle quelle : pitch, axes,
> plan par phases). La **Partie II** (nouvelle) est le design chiffré, équilibré et prêt à
> implémenter : greffes, jauge, écran, `data/grafts.json`, intégration méta. Quand la Phase A sera
> livrée, ce contenu migre en section §27 du `GDD.md` et les pièges d'implémentation dans
> `docs/PITFALLS.md`.
>
> **Convention de lecture pour `developpeur`** : les nombres de la Partie II priment sur les effets
> « à équilibrer » esquissés en Partie I §4-5. Toute greffe qui touche une stat de `PlayerStats`
> respecte les hardcaps de `StatCaps` (DR ≤ 0,40, cooldown ≥ 0,15 s, vitesse ≤ 380 px/s).

---

# PARTIE I — Proposition d'origine

## 1. Le problème qu'on résout

Le jeu est complet et propre, mais son différenciateur affiché (« l'évolution raconte la fusion »,
GDD §2) est **devenu la norme du genre** : Vampire Survivors, Everything is Crab, Halls of Torment,
Brotato ont tous des évolutions/fusions. Un nouveau joueur voit « un bon survivor de plus », pas
« un survivor que je n'ai jamais vu ».

Or le nom du jeu — **Chimère** — promet une créature faite de morceaux d'autres créatures, mais la
boucle ne le tient pas : les ennemis meurent et **disparaissent**. Personne ne devient une chimère.

## 2. Le pitch en une phrase

> Chaque run, tu **absorbes des parties des ennemis que tu tues** pour te muter en une chimère
> unique : les cartes de niveau construisent ton **arsenal**, les greffes construisent ton **corps**.

C'est un **troisième axe de progression**, orthogonal aux deux existants :

| Axe | Ce qu'il construit | Source | Existe déjà ? |
|---|---|---|---|
| Cartes de level-up | Ton **arsenal** (armes/passifs/fusions) | Orbes XP → montée de niveau | ✅ |
| Améliorations Hub | Ta **baseline permanente** | Échos d'Aether entre runs | ✅ |
| **Greffes (Assimilation)** | Ton **corps / ton identité de chimère** | Assimiler des ennemis tués | ❌ **NOUVEAU** |

## 3. Boucle de l'assimilation

1. **Remplir la jauge d'assimilation.** Chaque *type* d'ennemi a une jauge propre : tuer des ennemis
   de ce type la remplit (ex. 12 Drones Corrompus → jauge Drone pleine). Optionnellement, les élites
   et mini-boss remplissent plus vite (biomasse plus riche).
2. **Proposition de greffe.** Quand une jauge se remplit, une **carte de greffe** est proposée — soit
   sur l'écran de level-up existant (nouvelle catégorie de carte), soit sur un mini-écran dédié
   « ASSIMILATION » (choix à trancher, cf. §7).
3. **Slots limités.** Le joueur n'a que **3 slots de greffe** (extensible via le Hub, cf. §6). Passé
   3 greffes, prendre une nouvelle greffe **remplace** une greffe existante (choix cornélien) — ou on
   propose une **fusion de greffes** si deux sont compatibles (cf. §5).
4. **Effet immédiat.** La greffe modifie `PlayerStats` + ajoute un comportement + change le rendu du
   perso (a minima teinte/halo/icône HUD ; à terme, couche de silhouette pseudo-3D, cf. §8).

Résultat : **chaque run construit un monstre différent**, et le joueur *choisit* sa chimère au lieu
de subir un tirage. C'est la rejouabilité et l'identité que le nom promet.

## 4. Les greffes (une par archétype de base)

Chaque archétype d'ennemi donne une greffe thématiquement cohérente avec **son** comportement (le
`AiType` de `EnemySpawnData`). Le comportement de l'ennemi devient ton pouvoir.

| Ennemi source | Greffe | Effet proposé (à équilibrer) |
|---|---|---|
| Essaim de Rouille (`straight_chase`) | **Nuée symbiotique** | À ta mort d'un coup fatal évitable : libère 3 mini-essaims alliés ; +lifesteal léger au contact |
| Drone Corrompu (`erratic_chase`) | **Servos erratiques** | Dash court à cooldown (esquive) ; +i-frames pendant le dash |
| Sentinelle Corrompue (`ranged_kiter`) | **Œil de visée** | Tire un projectile auto à distance toutes les N s, indépendant de tes armes |
| Colosse Greffé (`slow_hunter`) | **Carapace greffée** | +armure lourde (DamageReduction), dégâts de contact au corps, −vitesse |
| Rôdeur de Rouille *(mini-boss)* | **Greffe rare** | Effet fort à débloquer tard (ex. onde de choc périodique) |

Les **28 ennemis basiques par biome** (4/biome) donnent, à terme, des **variantes** de ces 5 greffes
de base → énorme variété sans nouveau système (même logique data-driven que les sprites d'ennemis).

## 5. Synergies : la vraie chimère

Deux greffes compatibles peuvent **fusionner** en une forme composée (rappelle le flash de fusion
`FusionFlash` déjà existant — on réutilise le VFX) :

- **Carapace greffée + Servos erratiques → « Charge blindée »** : dash qui devient une charge qui
  encaisse et projette les ennemis.
- **Œil de visée + Nuée symbiotique → « Ruche de tourelles »** : les mini-essaims deviennent des
  tourelles fixes qui tirent.

Les synergies transforment 5 greffes en un espace de builds bien plus large et donnent une raison de
viser des combos précis — moteur de théorycraft, comme les fusions d'armes actuelles.

## 6. Intégration méta (Hub)

- **+1 slot de greffe** (upgrade Hub, cher — change fondamentalement le pouvoir).
- **Jauge d'assimilation plus rapide** (upgrade).
- **Codex des greffes** (comme le Codex des armes existant) : découvrir chaque greffe la documente.
- Aucune greffe débloquée d'office : elles se **découvrent en jouant** (aligné avec l'arsenal à
  découverte déjà en place).

## 7. Décisions ouvertes (à trancher avec toi)

1. **UI de la greffe** : nouvelle catégorie sur l'écran de level-up existant (moins de code, mais
   dilue les cartes d'arme) **ou** mini-écran dédié « ASSIMILATION » quand une jauge se remplit
   (plus lisible, plus « signature », plus de code). → *reco : écran dédié, c'est un différenciateur,
   il mérite son moment.*
2. **Déclencheur** : jauge par type (déterministe, lisible) **ou** drop de « biomasse » à ramasser
   (plus de tension spatiale, façon Noyaux). → *reco : jauge par type pour le MVP, plus simple à
   équilibrer et à tester en headless.*
3. **Slots** : 3 fixes + extension Hub, remplacement au-delà **ou** fusion forcée. → *reco : 3 +
   remplacement pour le MVP, fusions en phase B.*

## 8. Coût & pourquoi c'est abordable

Le plus dur est déjà construit :

| Brique nécessaire | Ce qui existe déjà à réutiliser |
|---|---|
| Règles pures testables | `src/Core/Rules/EliteAffixTable.cs` → copier en `GraftTable.cs` (+ tests xUnit) |
| Ennemi → donnée | `EnemySpawnData.AiType` / système sprite data-driven → ajouter un `GraftId` par type |
| Modif de stats + comportement + rendu | `EnemyBase.ApplyElite` (patron exact) + `PlayerStats` |
| Écran de choix | UI des cartes de level-up |
| VFX de transformation | `FusionFlash` (flash désaturation) déjà en AutoLoad |
| Silhouette modifiée | `tools/pseudo3d_lib.py` (couches d'ombrage cohérentes) |

Le vrai coût neuf : la jauge d'assimilation, l'écran de greffe, et **l'équilibrage** (le plus long).

## 9. Plan par phases

**Phase A — Prouver le fun (MVP jouable).**
- `src/Core/Rules/GraftTable.cs` (5 greffes de base, effets chiffrés) + tests xUnit.
- Jauge d'assimilation par type (`AssimilationTracker`, compteur de kills par `AiType`).
- Écran/carte de choix de greffe, 3 slots, remplacement au-delà.
- Effets : stats (`PlayerStats`) + 1-2 comportements simples (dash Drone, carapace Colosse).
- Rendu minimal : icônes de greffe au HUD + halo/teinte perso. Pas encore de refonte de silhouette.
- `data/grafts.json` (tuning sans recompiler).
- **Objectif : sentir que "je deviens une chimère" en une run.** Test game-tester.

**Phase B — Identité chimère.**
- Silhouette du perso modifiée par les greffes (couches pseudo-3D).
- Synergies / fusions de greffes (réutilise `FusionFlash`).
- Greffes par biome (variantes data-driven sur les 28 ennemis).

**Phase C — Méta & profondeur.**
- Slots via Hub, Codex des greffes, jauge accélérée.
- Équilibrage fin, itération marketing (le pitch « deviens la chimère » devient l'accroche du store).

## 10. Effet marketing

Nouveau pitch de store, une phrase, qui n'existe chez aucun concurrent frontal :

> **« Ne tue pas les monstres. Deviens-les. »**
> Chaque run, assimile les créatures de la Rouille pour te muter en une chimère unique.

C'est **lisible en un GIF** (silhouette du perso qui change en jouant), ce que « on a des fusions
d'armes » n'est pas.

---

# PARTIE II — DESIGN CHIFFRÉ (Phase A — prêt à coder)

> Rédigé par `game-designer` le 2026-07-06. Toutes les valeurs sont calibrées dans le même monde
> que l'existant : dégâts/PV/cooldown des armes (GDD §8), affixes d'élite (GDD §22,
> `EliteAffixTable.cs`), ennemis (`data/enemies.json`), courbes XP (GDD §6), économie Échos
> (GDD §9.5). Cible de calibrage baseline : Cyborg/Chimera (~90-100 PV, 200-210 px/s, Damage 10,
> `DamageMultiplier` 1,0).
>
> **★ Révision équilibrage 2026-07-06 (post-test game-tester).** Deux ajustements suite au rapport
> de test (le fun jugé « partiel ») :
> 1. **Punch de la Nuée** (§14.1) : ~30 → **~71 DPS** + lifesteal 4 → 6 %, essaims 3 → **4**. La 1re
>    greffe (toujours la Nuée) se sentait négligeable devant un build ; elle devient une présence
>    réelle sans écraser l'endgame (400+ DPS). *Ancien : count 3, dmg 5, rehit 0,5 s, lifesteal 0,04,
>    rayon 44, 150°/s.*
> 2. **Cadence des jauges tardives** (§12.1/§12.2) : `colossus` 7 → **4**, `stalker` 3 → **5**. Le
>    « choix cornélien de remplacement » était quasi-inatteignable (jauges pleines ~11-13 min pour un
>    boss à 13 min). Nouveau : `stalker` (alimentée par les élites tout au long) se remplit ~5-6 min
>    et devient le **déclencheur fiable du 1er remplacement mid-run** ; `colossus` ~10,5 min = 2e
>    remplacement avant le boss. Le remplacement est désormais vécu dans une run moyenne.
>
> **FLAG-D02** (reprise instantanée d'une jauge après remplacement, §12.3) : **conservé tel quel en
> Phase A** — re-proposer une greffe jetée sert de filet pour corriger une erreur, et le garde-fou
> ×1,5 couvre déjà le cas du rejet. À surveiller au re-test ; si l'effet « insistant » gêne, ajouter
> une reprise à seuil ×0,5 après remplacement.

## 11. Principe de mappage : jauge par ARCHÉTYPE (`AiType`), pas par `id` d'ennemi

Il y a **28 ennemis basiques** (4 archétypes × 5 biomes + originaux) mais seulement **4 `AiType`**.
Pour le MVP, **une jauge par archétype d'IA** (pas par `id`) : tous les fourrages de tous les biomes
alimentent la **même** jauge « Nuée ». Cela colle au cadrage « 5 greffes de base, une par
archétype », rend l'équilibrage traçable, et prépare la Phase B (les variantes de biome donneront
des variantes de greffe sur la même jauge). Une **5ᵉ jauge « champion »** agrège les kills de
mini-boss / boss / élites (la « biomasse riche »).

| # | Jauge (clé) | `AiType` source | Rôle | Greffe débloquée | Rareté |
|---|---|---|---|---|---|
| 1 | `swarm` | `straight_chase` | fourrage | **Nuée Symbiotique** | Commun |
| 2 | `drone` | `erratic_chase` | harceleur | **Servos Erratiques** (dash) | Commun |
| 3 | `sentinel` | `ranged_kiter` | pression distance | **Œil de Visée** (tourelle) | Rare |
| 4 | `colossus` | `slow_hunter` | bruiser | **Carapace Greffée** (armure) | Rare |
| 5 | `stalker` | mini-boss / boss / élites | champion | **Onde du Rôdeur** (onde de choc) | Épique |

> Implémentation : `AssimilationTracker` tient un `Dictionary<string,float>` de points par clé de
> jauge. À chaque `EnemyKilled`, il route le kill vers la clé via `AiType` (mini-boss/boss/élite →
> `stalker`). Aucune dépendance Godot dans la table de règles `GraftTable` (mêmes conventions que
> `EliteAffixTable`) : elle ne décide QUE des seuils, points et effets chiffrés.

## 12. La jauge d'assimilation — cadence de remplissage

### 12.1 Seuils (en « points d'assimilation »)

1 kill d'ennemi **basique** = **1 point** vers la jauge de son archétype. Bonus « biomasse
riche » :
- kill d'**élite** (affixe, GDD §22) = **2 points** vers sa jauge d'archétype **et** **+1 point**
  vers la jauge `stalker` ;
- kill de **mini-boss** (`maxSimultaneous > 0`, non-boss) = **2 points** `stalker` ;
- kill du **boss de fin** (`rusted_core`) = **3 points** `stalker`.

| Jauge | Seuil (points) | 1er remplissage visé | Slot attendu |
|---|---|---|---|
| `swarm` | **30** | ~1,5-2 min | 1 |
| `drone` | **24** | ~4 min | 2 |
| `sentinel` | **14** | ~7 min | 3 |
| `colossus` | **4** *(rév. 7)* | ~10,5 min | 5ᵉ → **remplacement** |
| `stalker` | **5** *(rév. 3)* | ~5-6 min (cumul d'élites) | 4ᵉ → **remplacement** |

### 12.2 Justification de la cadence (comparée aux level-ups §6)

Rappel level-ups : L1→2 en 5 XP (quasi immédiat), **L10 ~3 min**, **L20 ~12 min**. Débit de kills
observé (§9.2 : 520 kills / 780 s) ≈ **40 kills/min** en moyenne, dominés tôt par le fourrage
(seul archétype spawné avant 2:00), puis dilués par l'arrivée successive des rôles (drone 2:00,
sentinelle 5:00, colosse 9:00).

- **0-2 min** : ~100 % des kills sont du fourrage (~15-20/min en montée) → jauge `swarm` (30)
  pleine vers **1,5-2 min**. Première greffe **avant le 3ᵉ level-up** : le joueur sent le système
  très tôt sans qu'il éclipse le premier choix d'arme.
- **2-4 min** : fourrage ~60 % + harceleur ~40 % d'un débit ~30/min → ~24 harceleurs cumulés vers
  **4 min** → `drone` plein (slot 2).
- **5-6 min** : les élites (fréquence `clamp(0,03+0,02·t)`, ~3-4/min à ce stade, +1 point `stalker`
  chacune) remplissent `stalker` (**5**) vers **~5-6 min** → la greffe épique **Onde du Rôdeur**
  arrive comme slot 3 ou 4. *(rév. post-test : c'est cette jauge, alimentée en continu par les
  élites, qui garantit que le remplacement est vécu tôt.)*
- **~7 min** : la sentinelle (~19 % du pool, tankier/kiteuse) → ~14 kills → `sentinel` plein. Si les
  3 slots sont déjà occupés (swarm/drone/stalker), **c'est le 1er écran de REMPLACEMENT** (~7 min) —
  le « choix cornélien » est désormais atteint bien avant le boss, pas dans la fenêtre étroite
  d'avant-boss de la version initiale.
- **~10-11 min** : le colosse (200 PV, poids faible) tombe à ~2-3/min → `colossus` (**4**) plein vers
  **~10,5 min** → **2e remplacement** avant l'arrivée du boss (13:00).

Résultat : greffe 1 vers 1,5-2 min, 3 slots pleins vers ~5-7 min, puis **au moins deux décisions de
remplacement (~7 min et ~10,5 min) dans une run moyenne**, avant même l'overtime. Ni trivial ni
frustrant (le fourrage abonde). Le débit d'assimilation reste **volontairement plus lent que les
level-ups** : c'est un axe identitaire, pas un distributeur.

### 12.3 Règle anti-spam : jauge d'une greffe possédée = en pause

Tant qu'une greffe est **équipée dans un slot**, sa jauge **cesse d'accumuler** (pas de re-proposition
inutile de la même greffe). Si le joueur **remplace** cette greffe plus tard, sa jauge **reprend là
où elle en était** (mémorisée, pas remise à zéro) et pourra la re-proposer. Une jauge qui se remplit
alors que **les 3 slots sont pleins et que la greffe n'est pas déjà équipée** déclenche l'écran de
remplacement (§13.3). Après un **refus** de remplacement, la jauge concernée est remise à 0 et son
seuil est **×1,5** pour ce cycle (évite le harcèlement de la même proposition en boucle).

## 13. La greffe : règles de l'écran « ASSIMILATION »

### 13.1 Déclenchement & pause

`AssimilationScreen` = `CanvasLayer` dédié, calqué sur `LevelUpScreen` : `ProcessMode = Always`,
`GetTree().Paused = true` à l'ouverture, `= false` à la fermeture si aucun autre écran modal en
file. Déclenché par le signal `AssimilationSystem.GaugeFilled(string gaugeKey)`. Identité visuelle
distincte du level-up (bandeau « ASSIMILATION », teinte **magenta/rouille** biologique vs le
cyan des cartes d'arme) — c'est un **moment signature**, pas une carte de plus.

### 13.2 Articulation avec le level-up (file d'attente partagée)

Les deux écrans mettent le jeu en pause et **ne doivent jamais s'afficher en même temps**. Règle :

- Le **level-up est prioritaire** (flux existant, jamais régressé). Si une jauge se remplit pendant
  qu'un `LevelUpScreen` est actif, l'`AssimilationScreen` **s'enfile** et s'ouvre à la fermeture du
  dernier écran de level-up (il s'abonne à un événement `ModalClosed`, ou partage la file de
  `LevelUpScreen`).
- Inversement, si un level-up survient pendant un écran d'assimilation ouvert, il **s'enfile
  derrière** (l'assimilation en cours n'est pas interrompue).
- **Un seul `GetTree().Paused = false`** : c'est le **dernier écran modal de la file** qui rend la
  main au jeu. Piège à documenter dans `PITFALLS.md` (deux systèmes qui togglent `Paused`
  indépendamment se marchent dessus — cf. le même risque que le `LevelUpScreen` sur la physique).

### 13.3 Contenu de l'écran & remplacement

L'écran ne propose **pas 3 greffes aléatoires** : une jauge donne **une** greffe déterministe (celle
de son archétype). Deux cas :

- **Slot libre (< 3 greffes équipées, greffe non possédée)** : l'écran présente la nouvelle greffe
  (nom, icône, effet chiffré, tag d'archétype source) avec deux boutons : **« ASSIMILER »** (équipe
  la greffe dans le prochain slot libre, applique l'effet immédiatement + `FusionFlash`) et
  **« REJETER »** (ferme sans équiper ; la jauge repart à 0). Le rejet existe pour laisser un joueur
  refuser une greffe à malus (ex. la Carapace ralentit).
- **3 slots pleins (greffe non possédée)** : l'écran affiche la **nouvelle greffe en tête** ET les
  **3 greffes actuelles** (icône + effet résumé de chacune). Le joueur **clique la greffe à
  remplacer** (choix cornélien, il voit précisément ce qu'il perd), ou **« CONSERVER »** (garde ses
  3 greffes ; jauge remise à 0, seuil ×1,5 pour le prochain cycle, §12.3). Le remplacement retire
  proprement l'effet de l'ancienne greffe (stats + comportement + rendu) avant d'appliquer la
  nouvelle.

Navigation clavier/manette : mêmes contraintes que les cartes de level-up (focus chain,
`EnsureControlVisible`), cf. `PITFALLS.md` « nav clavier/manette ».

### 13.4 Rendu & feedback (Phase A minimale)

- **Icône de greffe au HUD** : une rangée de 3 (ou 5 via méta) emplacements sous la barre XP ; un
  emplacement vide = silhouette grisée, un slot rempli = icône teintée à l'archétype source.
- **Halo/teinte perso** : à chaque greffe équipée, teinte additive légère cumulée sur le
  `SelfModulate` du joueur (ne pas écraser le `Modulate` — même piège que la teinte d'élite/gel,
  GDD §24). Pas encore de refonte de silhouette (Phase B).
- **Flash** : réutiliser `FusionFlash` (désaturation 0,35 s déjà en AutoLoad) à l'assimilation.

## 14. Les 5 greffes chiffrées

> Toutes les valeurs de dégâts des greffes **scalent avec `DamageMultiplier`** du joueur (comme les
> armes) sauf mention contraire. Aucune greffe ne dépasse un hardcap seule ; les cumuls avec méta +
> passifs sont plafonnés par `StatCaps` (rappel explicite par greffe concernée).

### 14.1 `swarm_symbiote` — Nuée Symbiotique (Commun) — source `straight_chase`

*« Tu es devenu la nuée : ce qui te touche est dévoré. »* Le fourrage se rue et colle — ta greffe
te rend collant à ton tour.

- **4 mini-essaims alliés** *(rév. 3)* orbitant le joueur : rayon **48 px** *(rév. 44)*, vitesse
  angulaire **165°/s** *(rév. 150)* (sprite `rust_swarm` réduit ~16×16, teinté à l'identité du
  perso). Réutilise le pattern d'orbite de l'Essaim de Drones (arme) mais en défensif.
- Dégâts de contact par mini-essaim : **8** *(rév. 5)* (× `DamageMultiplier`), ré-touche du même
  ennemi toutes les **0,45 s** *(rév. 0,5)* → ~17,8 DPS/essaim, **~71 DPS total** en mêlée collée
  (nettoie franchement le fourrage qui hug).
- **Lifesteal** : le joueur récupère **6 %** *(rév. 4 %)* des dégâts infligés par les mini-essaims en
  PV (≈ 3-4 HP/s en combat dense) — le « lifesteal léger au contact » de la Partie I §4.
- **Aucune modification de stat** → aucun hardcap concerné.

> Calibrage *(rév. post-test)* : ~71 DPS reste mineur devant un build maxé (§20 : 400+ DPS à niv.5)
> mais c'est enfin une **présence sentie** dès la 1re greffe (~1,5-2 min), gratuite, défensive et
> thématique — l'ancien ~30 DPS passait inaperçu. Le lifesteal 6 % est un vrai attrait de survie
> early contre les nuées (cf. i-frames 0,45 s).

### 14.2 `erratic_servos` — Servos Erratiques (Commun) — source `erratic_chase`

*« Le drone esquive par saccades. Toi aussi, désormais. »*

- **Dash actif** sur une nouvelle action d'entrée **`dash`** (défaut **Maj gauche** clavier /
  **RB / R1** manette — à câbler dans `InputRemap`, cohérent avec le remap ZQSD §25). Propulse le
  joueur de **180 px** dans la direction de déplacement courante (ou dernier `AimDirection` si à
  l'arrêt) sur **0,18 s** (translation en burst, pas un changement de stat `Speed` → **le plafond
  `MaxSpeed` 380 px/s ne s'applique pas**, à noter pour `developpeur`).
- **Cooldown 3,5 s**, réduit par le `CooldownReduction` du joueur (synergie Capaciteur/méta),
  **plancher 1,5 s** (garde-fou spécifique à la greffe, distinct du `MinCooldown` 0,15 s des armes).
- **I-frames dash : 0,25 s** (invulnérabilité pendant la ruade + court report), **indépendantes** et
  **non cumulables** avec les i-frames de dégât 0,45 s (on prend le max de la fenêtre active).
- **Aucune modification de stat permanente** → aucun hardcap concerné.

> Première greffe orientée **skill** (comme la Lance Vectorielle §23.2) : une esquive active dans un
> jeu majoritairement auto. Le plancher 1,5 s évite le dash-spam invulnérable.

### 14.3 `aiming_eye` — Œil de Visée (Rare) — source `ranged_kiter`

*« La sentinelle t'observait. Maintenant, un de ses yeux est le tien. »*

- **Tir automatique** indépendant de tes armes : 1 projectile toutes les **1,4 s** vers l'ennemi le
  plus proche à **≤ 420 px** (réutilise `Bullet`, aucune nouvelle scène de projectile).
- Dégâts **18** (× `DamageMultiplier`), **perfore 1 ennemi**, vitesse projectile **320 px/s** (plus
  rapide que les 180 px/s de la Sentinelle → lecture « c'est TON tir »). DPS ~13.
- Cooldown réduit par `CooldownReduction` (plancher **0,15 s** = `MinCooldown`, cohérent armes).
- **Aucune modification de stat** → aucun hardcap concerné.

> Rare car c'est une **source de dégâts à distance gratuite et permanente**, utile pour tout build
> et pour finir les kiteurs/fuyards. ~13 DPS est calibré sous une arme de niveau 1 (Canon ~40 DPS)
> pour rester un complément, pas une arme.

### 14.4 `grafted_carapace` — Carapace Greffée (Rare) — source `slow_hunter`

*« Le Colosse avance sans jamais fléchir. Ta peau devient la sienne. »* Tank pur, avec un vrai
malus.

- **+0,15 `DamageReduction`** (additif). **Rappel hardcap** : `DamageReduction` global est plafonné
  à **0,40** par `StatCaps.CapDamageReduction`. Cumul possible : méta (−0,23 max) + Plaque Renforcée
  in-run (−0,20) + cette greffe (−0,15) = −0,58 → **écrêté à −0,40** (chevauchement volontaire, même
  logique que les tiers 2 méta §9.5). Seule, la greffe reste sous le cap.
- **+25 PV max** (`MaxHp`, pas de heal, pas de hardcap — le scaling ennemi absorbe la marge, §17).
- **Représailles de contact (thorns)** : tout ennemi à **≤ 40 px** subit **18** dégâts
  (× `DamageMultiplier`) toutes les **0,6 s** (~30 DPS aux colleurs) — les « dégâts de contact au
  corps » de la Partie I §4.
- **Malus −18 % `Speed`** (multiplicatif, appliqué **après** le recalcul servo_motors ; ne touche
  jamais `MaxSpeed`, il baisse la vitesse). Ex. 210 → ~172 px/s. Vrai coût : tu deviens lent comme
  le Colosse. C'est ce malus qui rend la greffe **rejetable** (§13.3) selon le build.

> Rare et à malus : c'est la greffe la plus « transformante » du MVP (armure + thorns + lenteur).
> Elle arrive tard (~11-12 min) donc la décision de sacrifier de la mobilité est mûre.

### 14.5 `stalker_wave` — Onde du Rôdeur (Épique) — source `champion`

*« Le chasseur territorial émet une onde qui balaie tout. Elle t'appartient. »*

- **Onde de choc périodique** toutes les **4,0 s** (réduit par `CooldownReduction`) : anneau qui
  s'étend sur rayon **160 px**, **60** dégâts (× `DamageMultiplier`) + **knockback 60 px** à tout
  ennemi dans le rayon. Réutilise le VFX d'onde du Colosse / Champ de Surcharge + `ShockwaveRing`
  (GDD §12 polish).
- DPS AoE ~15 mais **fort burst + contrôle de foule** (repousse la nuée, crée de l'air) — comparable
  au Champ de Surcharge niv.5 (30 dmg/1,5 s, 200 px) en DPS moindre mais avec knockback plus net.
- **Aucune modification de stat** → aucun hardcap concerné.

> Épique et **débloquée tard** (jauge `stalker` : mini-boss/boss/élites). Récompense défensive de
> fin de run, cohérente avec l'arrivée du 1er mini-boss (~12 min) et l'escalade overtime.

## 15. Synergies / fusions de greffes — **PHASE B (hors-scope Phase A)**

Reporté explicitement (cadrage utilisateur). Rappel des combos ciblés (Partie I §5), à chiffrer en
Phase B : **Carapace + Servos → « Charge Blindée »** (le dash devient une charge qui encaisse et
projette) ; **Œil + Nuée → « Ruche de Tourelles »** (les mini-essaims deviennent des tourelles
fixes). Réutiliseront `FusionFlash`. **Ne rien coder de cela en Phase A.**

## 16. Spécification de `data/grafts.json` (à créer par `developpeur`, PAS ici)

Mêmes conventions que `weapons.json` / `enemies.json` / `meta_upgrades.json` (chargé au runtime,
modifiable sans recompiler ; commentaires en clés `_comment`/`_designNote`). Structure cible :

```jsonc
{
  "_comment": "grafts.json — Chimera Protocol. Système d'Assimilation (greffes). Chargé par GraftTable/AssimilationSystem. Voir docs/DESIGN_ASSIMILATION.md Partie II.",

  "slots": {
    "baseCount": 3,
    "_comment_slots": "Extensible via l'upgrade méta 'graft_slots' (GraftSlotBonus, lu comme reroll/skip, PAS un champ PlayerStats). Max 5.",
    "maxCount": 5,
    "replacementWhenFull": true
  },

  "gauges": {
    "_comment": "Une jauge par clé (archétype d'IA + 'stalker' pour les champions). Seuil en 'points d'assimilation'.",
    "thresholds": { "swarm": 30, "drone": 24, "sentinel": 14, "colossus": 4, "stalker": 5 },
    "aiTypeToGauge": {
      "straight_chase": "swarm",
      "erratic_chase":  "drone",
      "ranged_kiter":   "sentinel",
      "slow_hunter":    "colossus"
    },
    "pointsBasicKill": 1,
    "eliteKillArchetypePoints": 2,
    "eliteKillStalkerPoints": 1,
    "miniBossStalkerPoints": 2,
    "bossStalkerPoints": 3,
    "ownedGraftPausesGauge": true,
    "resumePausedGaugeFromSavedValue": true,
    "declineThresholdMultiplier": 1.5,
    "gaugeSpeedMetaBonusStat": "GraftGaugeSpeedBonus",
    "_comment_speed_meta": "Réduction de seuil (%) apportée par l'upgrade méta 'graft_metabolism' (max -30%). Seuil effectif = round(threshold * (1 - GraftGaugeSpeedBonus))."
  },

  "grafts": [
    {
      "id": "swarm_symbiote",
      "name": "Nuée Symbiotique",
      "gauge": "swarm",
      "sourceAiType": "straight_chase",
      "rarity": "common",
      "description": "4 mini-essaims orbitent et mordent ce qui t'approche. Vole des PV au contact.",
      "hudIcon": "res://assets/sprites/grafts/swarm_symbiote_icon.png",
      "tint": [1.3, 0.55, 0.4],
      "effects": {
        "orbitingAllies": {
          "count": 4, "orbitRadiusPx": 48, "angularSpeedDegPerSec": 165,
          "contactDamage": 8, "rehitIntervalSec": 0.45, "scalesWithDamageMultiplier": true,
          "lifestealFraction": 0.06
        }
      },
      "statMods": {}
    },
    {
      "id": "erratic_servos",
      "name": "Servos Erratiques",
      "gauge": "drone",
      "sourceAiType": "erratic_chase",
      "rarity": "common",
      "description": "Ruade d'esquive brève et invulnérable, à recharge.",
      "hudIcon": "res://assets/sprites/grafts/erratic_servos_icon.png",
      "tint": [0.6, 0.85, 1.3],
      "effects": {
        "dash": {
          "distancePx": 180, "durationSec": 0.18, "cooldownSec": 3.5,
          "cooldownFloorSec": 1.5, "affectedByCooldownReduction": true,
          "iframesSec": 0.25, "inputAction": "dash"
        }
      },
      "statMods": {}
    },
    {
      "id": "aiming_eye",
      "name": "Œil de Visée",
      "gauge": "sentinel",
      "sourceAiType": "ranged_kiter",
      "rarity": "rare",
      "description": "Un œil greffé tire seul sur l'ennemi le plus proche.",
      "hudIcon": "res://assets/sprites/grafts/aiming_eye_icon.png",
      "tint": [1.3, 0.75, 0.4],
      "effects": {
        "autoTurret": {
          "cooldownSec": 1.4, "affectedByCooldownReduction": true, "cooldownFloorSec": 0.15,
          "damage": 18, "scalesWithDamageMultiplier": true, "pierceCount": 1,
          "projectileSpeed": 320, "targetRangePx": 420, "reuseBullet": true
        }
      },
      "statMods": {}
    },
    {
      "id": "grafted_carapace",
      "name": "Carapace Greffée",
      "gauge": "colossus",
      "sourceAiType": "slow_hunter",
      "rarity": "rare",
      "description": "Armure lourde et représailles de contact. Mais tu deviens lent.",
      "hudIcon": "res://assets/sprites/grafts/grafted_carapace_icon.png",
      "tint": [0.7, 0.5, 0.45],
      "effects": {
        "thorns": { "damage": 18, "radiusPx": 40, "rehitIntervalSec": 0.6, "scalesWithDamageMultiplier": true }
      },
      "statMods": {
        "damageReductionAdd": 0.15,
        "maxHpAdd": 25,
        "speedMult": 0.82,
        "_comment_caps": "damageReductionAdd passe par StatCaps.CapDamageReduction (hardcap 0.40). speedMult appliqué APRÈS recalcul servo_motors, ne touche pas MaxSpeed."
      }
    },
    {
      "id": "stalker_wave",
      "name": "Onde du Rôdeur",
      "gauge": "stalker",
      "sourceAiType": "champion",
      "rarity": "epic",
      "description": "Onde de choc périodique qui balaie et repousse la nuée.",
      "hudIcon": "res://assets/sprites/grafts/stalker_wave_icon.png",
      "tint": [1.2, 0.4, 0.9],
      "effects": {
        "shockwave": {
          "cooldownSec": 4.0, "affectedByCooldownReduction": true,
          "radiusPx": 160, "damage": 60, "scalesWithDamageMultiplier": true,
          "knockbackPx": 60, "reuseShockwaveRingVfx": true
        }
      },
      "statMods": {}
    }
  ]
}
```

> Notes pour `developpeur` : `GraftTable.cs` (Rules pur, testable xUnit — patron `EliteAffixTable`)
> expose les seuils, le routage `AiType → gauge`, l'attribution de points (basique/élite/mini-boss/
> boss) et les structs d'effets/`statMods`. `AssimilationSystem` (AutoLoad, comme `LevelUpSystem`)
> tient les jauges et émet `GaugeFilled`. `AssimilationScreen` calque `LevelUpScreen`. Les effets de
> comportement (orbite, dash, tourelle, thorns, onde) vivent côté nœuds (SRP), la table ne décide
> que des chiffres. Ajouter les greffes à un **Codex** (comme l'arsenal/bestiaire) et à
> `GameSettings` pour la persistance des découvertes.

## 17. Intégration méta (Hub) — 2 upgrades chiffrés

Ajouts à `data/meta_upgrades.json` (arbre à 17 items, 19 080 Échos, GDD §9.5). Coûts calibrés dans
la même fourchette (90 → 1 150 / niveau). Les deux ciblent des **valeurs consommables lues
directement** par `AssimilationSystem` — **PAS** des champs `PlayerStats` (même pattern que
`reroll`/`skip`/`extra_life`).

| id | Nom | statTarget (consommable) | Niveaux | Coûts/niveau | Bonus/niveau |
|---|---|---|---|---|---|
| `graft_slots` | Matrice d'Assimilation | `GraftSlotBonus` | 2 | **500 / 950** | +1 slot de greffe (3 → 4 → 5) |
| `graft_metabolism` | Métabolisme Vorace | `GraftGaugeSpeedBonus` | 3 | **180 / 320 / 520** | −10 % de seuil de jauge (max −30 %) |

- **`graft_slots`** (cher, comme `extra_life` 450/800 et `overtime_stabilizer`) : change
  fondamentalement le pouvoir (une chimère à 5 greffes est un autre jeu) → objectif long terme.
  `AssimilationSystem` lit `MetaProgressionSystem.GetUpgradeLevel("graft_slots")` pour fixer le
  nombre de slots au début de run. Max 2 → **5 slots**.
- **`graft_metabolism`** (bon marché, comme `reroll` 160/320/520) : à −30 %, seuils effectifs
  `swarm` 30→21, `drone` 24→17, `sentinel` 14→10, `colossus` 4→3, `stalker` 5→4 (`round`). Première
  greffe ~1,2 min, 3 slots pleins ~5-6 min — accélère sans trivialiser (30 % n'inverse pas la
  courbe). Seuil effectif = `round(threshold × (1 − GraftGaugeSpeedBonus))` — arrondi au plus proche
  (et non `ceil`) pour que l'upgrade réduise réellement la jauge `stalker` (`ceil(2,1)=3` = no-op).

**Sous-total ajouté : 500+950 + 180+320+520 = 2 470 Échos** → arbre complet **19 items, 21 550
Échos**. Cohérent avec l'objectif « durée de vie plusieurs dizaines de runs » (§9.5) : ces deux
items ne rendent le jeu ni trivial (le scaling ennemi compense) ni obligatoire (les 3 slots de base
suffisent à jouer).

> Codex des greffes : gratuit (pas un upgrade payant), aligné sur l'arsenal/bestiaire à découverte
> (§9.5) — chaque greffe assimilée une fois se documente dans le Codex, persistée dans
> `user://settings.cfg` (même mécanique que les armes découvertes).

## 18. Récapitulatif de calibrage vs hardcaps (`StatCaps`)

| Greffe | Stat touchée | Valeur | Hardcap concerné | Marge |
|---|---|---|---|---|
| Nuée Symbiotique | — | — | aucun | — |
| Servos Erratiques | — (dash = burst, pas `Speed`) | 180 px / 0,18 s | `MaxSpeed` **non applicable** | — |
| Œil de Visée | — (cooldown propre) | 1,4 s, plancher 0,15 s | `MinCooldown` respecté | — |
| Carapace Greffée | `DamageReduction` | +0,15 | **0,40** (`CapDamageReduction`) | OK seule ; écrêtée en cumul méta+Plaque (volontaire) |
| Carapace Greffée | `MaxHp` | +25 | aucun | — |
| Carapace Greffée | `Speed` | ×0,82 | baisse la vitesse (jamais `MaxSpeed`) | — |
| Onde du Rôdeur | — (cooldown propre) | 4,0 s | `MinCooldown` respecté | — |

Aucune greffe ne franchit un plafond **seule**. Seul le cumul `DamageReduction` (méta + Plaque
Renforcée + Carapace) atteint le cap 0,40 — chevauchement assumé, identique au comportement des
tiers 2 méta (§9.5).

---

# PARTIE III — INTRO : spec du plan d'assimilation (2026-07-06, `story-teller`)

> L'intro actuelle (`src/UI/IntroScreen.cs`, 5 plans `INTRO_BEAT_1..5`) raconte la Convergence, la
> corruption d'un drone, le déferlement de la Rouille, le Noyau du Sanctuaire, puis la descente de
> l'Arpenteur — mais ne mentionne jamais l'Assimilation, alors que c'est le différenciateur du jeu
> (« Ne tue pas les monstres. Deviens-les. », `INTRO_TAGLINE`). Cette partie spécifie l'ajout d'un
> **6e plan** (`INTRO_BEAT_6`) pour le `developpeur` — textes déjà écrits dans `localization/ui.csv`,
> uniquement la chorégraphie visuelle reste à implémenter.
>
> **Choix : ajouter un plan plutôt que réécrire le plan 5.** Le plan 5 (`ShotArpenteurDescent`,
> texte « Quelqu'un doit y descendre. Ce sera toi. ») reste le meilleur point de bascule
> narration → joueur (2e personne, appel à l'aventure classique) : le transformer perdrait ce
> beat de mise en jeu du joueur. L'Assimilation mérite son propre instant, juste après, pour ne pas
> diluer les deux idées (« tu vas devoir y aller » / « voici comment tu vas survivre ») dans un seul
> plan surchargé.

## 20. Plan 6 — `ShotAssimilation` (nouveau)

> **✅ Implémenté 2026-07-06 (`developpeur`)** dans `src/UI/IntroScreen.cs` : méthode `ShotAssimilation()`
> + câblage `AddShot("INTRO_BEAT_6", ShotAssimilation, 4.0)` juste avant `RevealTitle`. Ennemi source =
> `SwarmFrames` (reco de la spec). Chorégraphie conforme (3 temps). Vérifié en capture (plan 6 + reveal
> du titre avec la nouvelle tagline « Ne tue pas les monstres. Deviens-les. »). Build 0/0, 112 tests verts.

**Texte (déjà dans `ui.csv`, clé `INTRO_BEAT_6`)** :
> « Tu ne te contenteras pas de tuer ce qui te traque.
> Tu lui arracheras un fragment — et le laisseras devenir toi. »

**Position dans la séquence** : entre `INTRO_BEAT_5` (`ShotArpenteurDescent`) et l'appel à
`RevealTitle()`. Modifier `BuildSequence()` :

```csharp
AddShot("INTRO_BEAT_5", ShotArpenteurDescent, 3.8);
AddShot("INTRO_BEAT_6", ShotAssimilation,     4.0);   // NOUVEAU — juste avant le reveal
_seq.TweenCallback(Callable.From(RevealTitle));
```

**Durée conseillée** : 4,0 s (légèrement plus long que les autres — c'est le beat qui « vend » le
système, il doit se lire sans être précipité).

### Chorégraphie (3 temps : mise à mort → arrachement → mutation)

Réutilise exclusivement des assets déjà chargés ailleurs dans `IntroScreen.cs` (aucun nouvel asset
à produire par `graphiste`) : `PlayerFrames`, `SwarmFrames` (ou `DroneFrames` — voir note ci-dessous
sur le choix), `FusionAura`, `NoyauParticle`.

1. **Mise à mort (0,0 → 1,2 s)** — un ennemi (`AnimatedSprite2D` sur `SwarmFrames`, teinte `Rust`,
   même pattern que `ShotRustSwarm`) approche du joueur (`PlayerFrames`, anim `run_down` ou `idle`,
   position centrale légèrement basse comme en fin de plan 5). L'ennemi s'arrête net à courte
   distance du joueur (`Tween` sur `position`, `TransitionType.Sine`, arrêt brusque — pas de
   ralenti progressif, pour lire un coup net) puis se fige — un flash bref blanc/rust sur son
   `modulate:a` (via un second `Tween` court, 0,15 s) marque l'impact du coup fatal, inspiré du
   `HitFlash` en jeu (cf. `docs/PITFALLS.md`, section VFX).
2. **Arrachement (1,2 → 2,4 s)** — l'ennemi se **désagrège** : `modulate:a` de son sprite descend à
   0 sur ~0,6 s pendant qu'un petit groupe de particules (`AddParticles(NoyauParticle, ...)`, teinte
   `Rust` puis virant progressivement vers `Cyan`/`Violet` — interpoler la couleur des particules
   via deux appels `AddParticles` décalés, ou un `Tween` sur une particule unique si le budget GPU
   le permet) part de la position de l'ennemi **vers le joueur** (`direction` pointant du mort vers
   le joueur, pas l'inverse des autres plans qui font des particules ambiantes). C'est le geste
   « j'arrache un fragment » — la lecture doit être sans ambiguïté : quelque chose *quitte* l'ennemi
   *pour rejoindre* le joueur.
3. **Mutation (2,4 → 4,0 s)** — au moment où les particules atteignent le joueur, réutiliser
   `FusionAura` (comme en fin de plan 5) : scale-up rapide (`Vector2.One * 4f` en ~0,3 s,
   `TransitionType.Back` pour un petit "pop") + fade-in de `modulate:a` jusqu'à ~0,8, PUIS fade-out
   sur le reste du plan. Simultanément, le sprite du joueur reçoit une teinte cumulative légère
   (`modulate` du joueur qui vire d'un blanc neutre vers un mélange `Cyan`/`Rust`, ex.
   `new Color(0.75f, 0.85f, 0.85f)` — **teinte subtile, pas un remplacement total du sprite** :
   c'est exactement le même principe que le halo/teinte HUD prévu en jeu pour les greffes
   équipées, `docs/DESIGN_ASSIMILATION.md` §13.4 — la cinématique doit annoncer visuellement ce
   que le joueur verra en jeu, pas inventer un effet différent). `SlowZoom(1.0f, 1.1f, 4.0)` léger
   sur l'ensemble du plan, cohérent avec les autres plans.

### Choix de l'ennemi source (`SwarmFrames` vs `DroneFrames`)

Recommandation : **`SwarmFrames`** (le Rust Swarm), pas `DroneFrames`. Raisons : (1) c'est le tout
premier ennemi rencontré en jeu et la toute première greffe obtenue en run
(`swarm_symbiote`/Nuée Symbiotique, §14.1 de ce document) — cohérence pédagogique entre l'intro et
la première run ; (2) `ShotRustSwarm` (plan 3) a déjà établi ce sprite visuellement dans la mémoire
du joueur, ce qui rend la relecture immédiate en plan 6 (« ah, c'est le même type de monstre ») ;
(3) sa silhouette petite/agile se prête mieux à l'idée d'« arracher un fragment » qu'un Drone plus
anguleux. Le `developpeur` peut substituer `DroneFrames` si `SwarmFrames` pose un souci technique
(SpriteFrames sans animation `idle`/`move` compatible) — l'important est la chorégraphie, pas
l'identité exacte de l'ennemi.

### Notes d'implémentation pour `developpeur`

- Nouvelle méthode privée `ShotAssimilation()` dans `IntroScreen.cs`, même patron que les 5
  méthodes `Shot*` existantes (voir `ShotArpenteurDescent` comme référence la plus proche
  structurellement — position joueur similaire).
- Pas de nouvelle ressource à charger : tous les chemins (`PlayerFrames`, `SwarmFrames`,
  `FusionAura`, `NoyauParticle`) sont déjà des constantes de la classe.
- Le texte `INTRO_BEAT_6` est déjà présent dans `localization/ui.csv` (EN/FR/ES) — aucune clé à
  ajouter côté texte, seulement le câblage `AddShot("INTRO_BEAT_6", ShotAssimilation, 4.0)`.
- `INTRO_TAGLINE` a été mise à jour dans `ui.csv` pour aligner le reveal de titre sur le pitch
  marketing (« Ne tue pas les monstres. Deviens-les. ») — aucun changement de code requis, `Loc.T`
  lit déjà la clé dans `RevealTitle`/`_Ready`.
- **Piège à surveiller** (cf. `docs/PITFALLS.md`) : régénérer les `.translation` après modification
  de `ui.csv` (`godot --headless --import`) — sinon `INTRO_BEAT_6` et le nouveau `INTRO_TAGLINE`
  n'apparaîtront pas en jeu tant que l'import n'a pas tourné.

