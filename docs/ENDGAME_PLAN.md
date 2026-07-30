# ENDGAME_PLAN — Saturation, challenge de fin de partie et rejouabilité

> Plan validé le **2026-07-30** (après la 1.24.0). Trois arbitrages tranchés par l'auteur du jeu :
> **(a)** l'axe est la **saturation cumulative** ; **(b)** elle est **choisie et récompensée**, jamais
> imposée ; **(c)** la rejouabilité vient de **nouvelles raisons de rejouer**, pas de nouveau contenu.
> Design de référence : `docs/GDD.md` §28 (paliers de menace), §31 (overtime), §33 (cartes de
> surcharge). Mesures citées : `docs/TEST_REPORT.md` (2026-07-29 et 2026-07-30).

## 1. Le problème, mesuré

Le constat « avec toutes les évolutions le jeu devient facile » est structurel, et les relevés en
donnent le mécanisme exact :

| ce qui croît **sans plafond** | ce qui croît selon une **courbe fixe** |
|---|---|
| `OverloadCards.Plating` **+45 PV/prise, illimité** — 270 PV/min mesurés | `OvertimeEscalation.StatAcceleration = 2,25` |
| `OverloadCards.Damage` **+5 % dégâts/prise, illimité** | densité : **saturée** dès la 8ᵉ minute (cap 300) |
| méta permanente (Échos, greffes, perks) : chaque run part plus forte | `DifficultyTuning` plafonne à **×1,35** dégâts |

Trois faits mesurés qui interdisent la réponse paresseuse (« remonter les multiplicateurs ») :

1. **Le joueur est en surplus de PV 60,7 % du temps d'overtime** et meurt d'un **pic**, pas d'usure.
   Une menace quantitative plus forte déplace ce chiffre à peine — le §31 l'a tenté **trois fois**
   (1,5 puis 3 puis 2,25) pour finir par constater que la pente de la défense est un *choix du joueur*.
2. **Le canal de soin dominant est le soin ponctuel** : 86,4 PV/s contre 8,2 de régénération. Tout
   réglage qui l'ignore agit sur 10 % du problème.
3. **Le plafond de difficulté est dérisoire** devant la croissance du joueur : ×1,35 sur les dégâts
   ennemis, face à un DPS joueur qui fait **×700** sur une run.

**Conclusion de design.** Il ne manque pas de puissance à la menace, il lui manque des **dimensions**.
Le joueur n'a qu'un seul type de réponse (plus de PV, plus de DPS) parce que la menace ne pose qu'un
seul type de question. La saturation est le cadre qui permet d'en poser d'autres, un cran à la fois.

## 2. La Saturation — principes de conception

**Un cran = une règle nommée, lisible avant de jouer, et qui retire une certitude.** Pas un
multiplicateur invisible. Le joueur doit pouvoir dire *pourquoi* il est mort et *ce qu'il changera*.

Trois règles que le plan s'impose :

1. **Viser en priorité les axes où le joueur est sans plafond** — soins reçus, PV max, régénération,
   temps de construction du build. Un cran qui ajoute des PV aux ennemis est le moins intéressant des
   crans : il alimente l'échange de statistiques que le joueur gagne toujours.
2. **Cumulatif et ordonné** : la saturation N applique les crans 1…N. On ne panache pas (ce serait des
   mutateurs — voir §6, hors périmètre ici).
3. **Jamais un mur sur le boss.** `LevelThreat.ChampionHpSoftening = 0,55` existe précisément parce que
   battre le boss conditionne le déblocage du niveau suivant, et le boss est calibré sur un **TTK
   joué** (`rusted_core.maxHp = 5000`, GDD §20.6). La saturation doit réutiliser cet amortissement, sinon
   chaque cran rallonge un TTK déjà mesuré comme long.

### Les crans proposés

Les cinq premiers réutilisent des leviers **déjà en place** (donc peu de code) ; les suivants ajoutent
la dimension qualitative qui manque. Valeurs à confirmer au banc, cran par cran (§5).

| # | Nom | Règle | Levier |
|---|---|---|---|
| I | Meute | Ennemis +30 % PV | `LevelThreat.HpMults` (existant) |
| II | Hémorragie | **Soins reçus −40 %** (orbes, lifesteal, Blindage) | attaque le canal dominant mesuré |
| III | Compte à rebours | Overtime dès la **10ᵉ** minute | seuil d'overtime (existant) |
| IV | Surcharge | Le boss gagne une **4ᵉ phase** | `BossPhases` (existant) |
| V | Élite ordinaire | Fréquence d'élite **×3** | `EliteAffixTable` (existant) |
| VI | Purificateur | Certains ennemis frappent pour un **% des PV max** | **neuf** — rend l'empilement de PV inopérant |
| VII | Corrosion | Régénération **−50 %** et la réserve ne se remplit **qu'en mouvement** | **neuf** — touche la 1.24.0 |
| VIII | Brèche | L'arène **rétrécit** en overtime | **neuf** — casse le kiting long |
| IX | Régence | **Deux champions** simultanés en overtime | spawn (existant, à orchestrer) |
| X | Sans filet | Plus de vies supplémentaires ni de Plaque Adaptative | méta (existant, à inverser) |

Le cran **VI** est le plus important du lot : c'est le seul qui répond directement au « surplus de PV
60 % du temps ». À l'inverse, **I** est le moins utile — il est là parce qu'un premier cran doit être
rassurant, pas parce qu'il apporte quelque chose.

### Ce que la saturation ne doit PAS faire

- **Toucher aux i-frames du joueur** (0,45 s, marqué CRITIQUE pour les nuées dans `CLAUDE.md`) : les
  raccourcir ne crée pas de la difficulté, il crée de la mort inexpliquée en nuée.
- **Réduire la lisibilité** : pas de télégraphe supprimé, pas de VFX retiré. Le cran X supprime un
  filet, il ne cache pas l'information.
- **Se cumuler avec `DifficultyTuning`** sans réflexion : trois axes multiplicatifs existent déjà
  (réglage joueur × palier de niveau × overtime). La saturation est un **quatrième** — décider si elle
  remplace « Difficile » ou s'y ajoute (voir question ouverte §7).

## 3. Économie d'Échos

Modèle éprouvé du dépôt : `LevelThreat.EchoMult` (1,00 → 1,45 sur 5 paliers) branché dans
`EchoFormula`, avec la justification « sans lui, farmer le 1er niveau resterait optimal ». La saturation
suit le même patron, avec une pente plus forte parce que le coût en compétence l'est aussi :

**+20 % d'Échos par cran, cumulatif** → saturation 5 ≈ **×3** (recommandé, à ajuster).

Garde-fou anti-farm à vérifier au moment de l'implémentation : le gain horaire d'une saturation haute
doit rester supérieur à celui d'une saturation basse **rejouée vite**. Une run de saturation 5 qui dure
deux fois plus longtemps pour ×3 d'Échos est rentable ; à ×1,5 elle ne le serait pas, et le joueur
optimal redescendrait — exactement le travers que `EchoMult` corrige déjà entre biomes.

## 4. Rejouabilité — les cadres, pas le contenu

Arbitrage (c) : exploiter ce qui existe déjà (5 biomes, 28 ennemis, 9 fusions, 13 défis, 3 mid-boss).

1. **Records par niveau × difficulté × saturation.** `GameSettings` indexe **déjà** complétions et
   meilleurs temps par difficulté (`CompletionKey(biomeId, difficulty)`, `_bestDiff`, `_bestTimes`). La
   clé doit devenir un **triplet** : `biome:difficulté:saturation`.
   - **Un record par combinaison, pas un record écrasé.** Aujourd'hui `_bestTimes` garde un seul temps
     par biome et se contente de mémoriser à côté la difficulté du record (`_bestDiff`) : un temps
     réalisé au cran 4 est donc **écrasé** par un temps plus long fait au cran 0, et l'exploit
     disparaît. C'est exactement ce que la grille doit empêcher.
   - **La difficulté d'assistance reste une dimension à part entière** : un temps fait en « Facile » ne
     concourt pas contre un temps fait en « Normal ».
   - Grille résultante : 5 biomes × 2 difficultés × (MaxRank+1) crans — de la rejouabilité pour le coût
     d'une clé de dictionnaire, et l'écran de sélection peut afficher « meilleur au cran courant »
     plutôt qu'un record sans rapport avec ce que le joueur s'apprête à jouer.
   - **Affichage** : montrer le record de la combinaison **sélectionnée**, et non le meilleur absolu —
     sinon monter d'un cran donne l'impression d'avoir régressé.
2. **Graine du jour.** `--seed` existe et est **journalisé** depuis la 1.24.0 : une graine dérivée de
   la date donne à tous les joueurs la même run, comparable. Le socle technique est déjà là, il ne
   manque que l'entrée de menu et l'affichage du score.
3. **Défis conditionnels.** `ChallengeSystem` porte déjà 13 défis évalués en fin de run. Ajouter des
   conditions de saturation (« battre le Néon en saturation 5 », « gagner sans greffe ») réutilise le
   système entier, y compris les titres cosmétiques.
4. **Titres de saturation** — la vitrine, déjà supportée par le flair du menu principal.

## 5. Comment on validera — et c'est nouveau

La saturation est **le premier système du projet dont chaque cran est validable au banc**, grâce à
l'outillage de la 1.24.0 :

```
py tools/power_curve_multi.py --overtime --minutes 20 --runs 1 --seed-base 1000   # ×4 graines
py tools/power_curve_multi.py --report-only --runs 4 --compare docs/bench/ref_overtime_225.json
```

**Critère par cran** : faire baisser le **temps soutenable** de plus de **6 %** (le plus petit écart
que la campagne sache détecter). Un cran dont l'effet est sous ce seuil ne mérite pas d'exister — il
coûte un palier au joueur sans rien changer.

⚠ **Ce que le banc ne peut pas dire** (rappels acquis cette semaine) : ne **jamais** lire la survie du
bot (censurée par `--run-limit`, et 2,6× celle d'un joueur) ; le bot tire ses cartes **au hasard**,
donc il ne juge aucun **arbitrage** ; et l'appariement est fragile sur les métriques de *build* dès que
le changement modifie la longueur de la run. Les crans II, VII et X touchent des choix de cartes :
leur ressenti se juge **en jouant**, le banc n'en mesure que la pression.

## 6. Lots livrables

Chaque lot est publiable seul et laisse le jeu cohérent.

**Lot 1 — le cadre. ✅ LIVRÉ le 2026-07-30** (commit `30ec10d`, non publié). `SaturationTable` en logique
pure sur le patron de `LevelThreat`, sélecteur à l'écran de niveau (liste des règles actives, lisible
avant de lancer), déblocage global par victoire, persistance + **migration** des anciens `settings.cfg`,
Échos via une source unique. Crans **I à V**, 21 tests, flag **`--saturation=<n>`** pour le banc.

**Validé au banc** (`docs/TEST_REPORT.md`, 4 graines appariées) : temps soutenable **60,7 % → 39,9 %**
(0/4, net) et survie théorique **÷2** — le critère des 6 % est dépassé d'un facteur 5, et 2 runs sur 4
finissent désormais par une **mort réelle** là où les quatre atteignaient le plafond du banc.

⚠ Deux écarts au plan, assumés et documentés dans le code :
- le cran IV prévu (« une phase de boss en plus ») est **reporté au lot 2** et remplacé par « Sans
  filet » (l'ancien cran X). Motif : `BossPhases.Count` est une constante à tables fixes et le refactor
  toucherait le HUD, la télémétrie, douze appels de `RustedCore` et des tests aux seuils codés en dur —
  une règle **publiée** à re-tester sur cinq incarnations, qui n'a pas sa place dans le lot qui valide
  le cadre lui-même ;
- le cran V ne laisse pas le facteur ×3 traverser le plafond d'élites (3 × 0,28 = 84 %, soit la
  « horde » que le code interdit par commentaire, avec le coût des affixes sur 200-300 entités) : le
  plafond est relevé **explicitement** à 0,55.

⚠ **Reste à mesurer** : seul le **cumul** des cinq crans est validé, pas chaque cran isolément
(≈2 h 20 de banc). La progressivité de la courbe est donc inconnue, et un cran pourrait être sous le
seuil de détection. À instruire avant le lot 2.

⚠ **Reste au lot 3** : les complétions et records restent indexés par *difficulté* et non par
saturation. Conséquence temporaire : le badge « vaincu » ne distingue plus les crans (`RecordCompletion`
reçoit désormais toujours `Normal`).

**Lot 2 — les crans qualitatifs.** **VI** (dégâts en % des PV max), **VII** (corrosion), **VIII**
(brèche). C'est le cœur du problème mesuré, et le lot le plus risqué : chacun demande du code de
gameplay neuf et un passage de `game-tester`. À découper si nécessaire.

**Lot 3 — la rejouabilité.** Records et complétions par saturation, graine du jour, défis conditionnels,
titres. Beaucoup de valeur par heure, aucune mécanique neuve.

**Lot 4 — crans de finition.** **IX** (deux champions) et **X** (sans filet), qui ne valent que si les
lots 1-2 ont tenu.

## 7. Décisions (tranchées le 2026-07-30, avant le lot 1)

1. **La saturation absorbe la difficulté.** Quatre axes multiplicatifs simultanés rendraient tout
   diagnostic impossible — le chantier §31 a mis trois sessions à isoler une cause pour cette raison
   exacte. Donc :
   - **la saturation 1 EST l'ancien « Difficile »**, aux mêmes valeurs (PV ×1,30, dégâts ×1,35, spawn
     ×1,25). Ce n'est pas une coïncidence exploitée après coup : c'est ce qui rend les **records et
     complétions existants valides sans migration destructrice** ;
   - **« Facile » survit comme mode d'ASSISTANCE**, pas comme une saturation négative. C'est de
     l'accessibilité (dégâts ×0,60) et cela ne se mélange pas avec une échelle de challenge ;
   - `GameDifficulty.Difficile` reste dans l'énumération pour **relire les anciens `settings.cfg`**,
     mais n'est plus proposé : au chargement, il est converti en *Normal + saturation 1*.
2. **Cinq crans au lancement** (I à V), tous validés au banc. Les crans VI-X viennent aux lots 2 et 4.
   Cinq crans réellement testés valent mieux que dix annoncés.
3. **Déblocage global, records par biome × saturation.** Le cran maximum atteint est global (battre
   la saturation N sur *n'importe quel* biome débloque N+1) : par biome, cinq niveaux × dix crans
   deviendrait une corvée. En revanche les **records** restent indexés par biome **et** par saturation —
   la grille à remplir existe pour qui la veut, sans être un péage.

## 8. Migration des sauvegardes des joueurs déjà en place

La 1.24.0 est **publiée** : des joueurs ont des `settings.cfg` et des `save.json` antérieurs à
la saturation. Rien ne doit être perdu ni réinterprété.

### Fait au lot 1

| cas | conversion | pourquoi |
|---|---|---|
| `difficulty=2` (Difficile) | → *Normal + saturation 1* | le cran 1 a **les mêmes multiplicateurs** : le record reste exact |
| `difficulty=0` (Facile) | → assistance, hors échelle | l'accessibilité n'est pas une saturation négative |
| a déjà terminé un niveau en Difficile | `saturation_beaten = 1` | il jouait effectivement au cran 1 : le lui créditer, plutôt que de le renvoyer au bas de l'échelle |
| absence de la clé `gameplay/saturation` | déclenche la migration **une seule fois** | l'écriture des deux clés au premier `Save()` fait foi ensuite |

Ces quatre cas sont **testés** (`SaturationTableTests`), et la migration s'exécute *après* le chargement
des complétions — sans elles, le crédit du cran serait perdu.

### Reste à faire (lot 3) — et à ne pas oublier

1. **Clés de complétion et de records → triplet `biome:difficulté:saturation`.** Elles sont
   aujourd'hui indexées par *difficulté* seule (`"biome:2"` = Difficile). Conversion :
   `"biome:2"` → `biome:1:1` (Normal, cran 1 — il jouait bien au-dessus de Normal),
   `"biome:1"` → `biome:1:0`, `"biome:0"` → `biome:0:0` (assistance).
   **Régression temporaire acceptée depuis le lot 1** : `RecordCompletion` reçoit toujours `Normal`,
   donc le badge ne distingue plus les crans.
2. **Meilleurs temps** (`_bestTimes` / `_bestDiff`) : même conversion, en **conservant le temps**. Point
   de vigilance : le schéma actuel garde *un* temps par biome, donc la migration ne peut reconstituer
   qu'**une** case de la nouvelle grille — celle de la difficulté mémorisée dans `_bestDiff`. Les autres
   cases démarrent vides, ce qui est correct : elles n'ont jamais été jouées *en tant que telles*.
3. **Écrire une migration versionnée**, pas une détection par clé absente. La parade actuelle (« pas de
   clé `saturation` ⇒ ancien fichier ») ne fonctionne qu'**une fois** ; le lot 3 en ajoutera d'autres. Un
   entier `save_version` dans `settings.cfg` rend les migrations suivantes déterministes et ordonnées.
4. **Ne jamais faire monter un joueur dans l'échelle sans victoire.** Leçon du 2026-07-30 : les runs de
   banc sous `--saturation=5` avaient persisté `saturation_beaten=5` dans une sauvegarde réelle, ouvrant
   tous les crans. Protéger la valeur *choisie* ne suffisait pas — le **déblocage** est une seconde voie
   d'écriture. Tout nouveau champ de progression doit être audité de la même façon.
5. **Vérifier sur une sauvegarde réelle avant publication** (copie du `settings.cfg` d'un joueur de la
   1.24.0), pas seulement sur un fichier neuf : les défauts de migration ne se voient que sur des
   données accumulées.

## 9. Hors périmètre (décidé)

- **Mutateurs panachés** (run composée à la carte) : écartés au profit de la saturation ordonnée. À
  reconsidérer seulement si la saturation s'avère trop rigide à l'usage.
- **Nouveau contenu** (armes, greffes, biome, boss) : explicitement reporté. Une arme de plus vaut
  davantage quand il existe une saturation élevée où la tester.
- **Export web** (`docs/WEB_EXPORT_ANALYSIS.md`) : sans rapport, toujours bloqué par le .NET.
