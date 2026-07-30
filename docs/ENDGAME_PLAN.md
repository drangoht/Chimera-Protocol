# ENDGAME_PLAN — Ascension, challenge de fin de partie et rejouabilité

> Plan validé le **2026-07-30** (après la 1.24.0). Trois arbitrages tranchés par l'auteur du jeu :
> **(a)** l'axe est l'**ascension cumulative** ; **(b)** elle est **choisie et récompensée**, jamais
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
seul type de question. L'ascension est le cadre qui permet d'en poser d'autres, un cran à la fois.

## 2. L'Ascension — principes de conception

**Un cran = une règle nommée, lisible avant de jouer, et qui retire une certitude.** Pas un
multiplicateur invisible. Le joueur doit pouvoir dire *pourquoi* il est mort et *ce qu'il changera*.

Trois règles que le plan s'impose :

1. **Viser en priorité les axes où le joueur est sans plafond** — soins reçus, PV max, régénération,
   temps de construction du build. Un cran qui ajoute des PV aux ennemis est le moins intéressant des
   crans : il alimente l'échange de statistiques que le joueur gagne toujours.
2. **Cumulatif et ordonné** : l'ascension N applique les crans 1…N. On ne panache pas (ce serait des
   mutateurs — voir §6, hors périmètre ici).
3. **Jamais un mur sur le boss.** `LevelThreat.ChampionHpSoftening = 0,55` existe précisément parce que
   battre le boss conditionne le déblocage du niveau suivant, et le boss est calibré sur un **TTK
   joué** (`rusted_core.maxHp = 5000`, GDD §20.6). L'ascension doit réutiliser cet amortissement, sinon
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

### Ce que l'ascension ne doit PAS faire

- **Toucher aux i-frames du joueur** (0,45 s, marqué CRITIQUE pour les nuées dans `CLAUDE.md`) : les
  raccourcir ne crée pas de la difficulté, il crée de la mort inexpliquée en nuée.
- **Réduire la lisibilité** : pas de télégraphe supprimé, pas de VFX retiré. Le cran X supprime un
  filet, il ne cache pas l'information.
- **Se cumuler avec `DifficultyTuning`** sans réflexion : trois axes multiplicatifs existent déjà
  (réglage joueur × palier de niveau × overtime). L'ascension est un **quatrième** — décider si elle
  remplace « Difficile » ou s'y ajoute (voir question ouverte §7).

## 3. Économie d'Échos

Modèle éprouvé du dépôt : `LevelThreat.EchoMult` (1,00 → 1,45 sur 5 paliers) branché dans
`EchoFormula`, avec la justification « sans lui, farmer le 1er niveau resterait optimal ». L'ascension
suit le même patron, avec une pente plus forte parce que le coût en compétence l'est aussi :

**+20 % d'Échos par cran, cumulatif** → ascension X ≈ **×3** (recommandé, à ajuster).

Garde-fou anti-farm à vérifier au moment de l'implémentation : le gain horaire d'une ascension haute
doit rester supérieur à celui d'une ascension basse **rejouée vite**. Une run d'ascension X qui dure
deux fois plus longtemps pour ×3 d'Échos est rentable ; à ×1,5 elle ne le serait pas, et le joueur
optimal redescendrait — exactement le travers que `EchoMult` corrige déjà entre biomes.

## 4. Rejouabilité — les cadres, pas le contenu

Arbitrage (c) : exploiter ce qui existe déjà (5 biomes, 28 ennemis, 9 fusions, 13 défis, 3 mid-boss).

1. **Records par ascension.** `GameSettings` indexe **déjà** complétions et meilleurs temps par
   difficulté (`CompletionKey(biomeId, difficulty)`, `_bestDiff`). Étendre la clé à l'ascension donne
   une grille 5 biomes × N crans à remplir — de la rejouabilité pour le coût d'une clé de dictionnaire.
2. **Graine du jour.** `--seed` existe et est **journalisé** depuis la 1.24.0 : une graine dérivée de
   la date donne à tous les joueurs la même run, comparable. Le socle technique est déjà là, il ne
   manque que l'entrée de menu et l'affichage du score.
3. **Défis conditionnels.** `ChallengeSystem` porte déjà 13 défis évalués en fin de run. Ajouter des
   conditions d'ascension (« battre le Néon en ascension 5 », « gagner sans greffe ») réutilise le
   système entier, y compris les titres cosmétiques.
4. **Titres d'ascension** — la vitrine, déjà supportée par le flair du menu principal.

## 5. Comment on validera — et c'est nouveau

L'ascension est **le premier système du projet dont chaque cran est validable au banc**, grâce à
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

**Lot 1 — le cadre (viser 1.25.0).** `AscensionTable` en logique pure (`src/Core/Rules/`) sur le patron
de `LevelThreat` : tables indexées, `EchoMult`, amortissement des champions. Sélecteur d'ascension à
l'écran de niveau (avec la liste des crans actifs, lisible **avant** de lancer), déblocage par victoire,
persistance dans `settings.cfg`, affichage du multiplicateur d'Échos à l'écran de fin. Crans **I à V**
(leviers existants). Tests unitaires sur la table et le déblocage. Validation au banc, cran par cran.

**Lot 2 — les crans qualitatifs.** **VI** (dégâts en % des PV max), **VII** (corrosion), **VIII**
(brèche). C'est le cœur du problème mesuré, et le lot le plus risqué : chacun demande du code de
gameplay neuf et un passage de `game-tester`. À découper si nécessaire.

**Lot 3 — la rejouabilité.** Records et complétions par ascension, graine du jour, défis conditionnels,
titres. Beaucoup de valeur par heure, aucune mécanique neuve.

**Lot 4 — crans de finition.** **IX** (deux champions) et **X** (sans filet), qui ne valent que si les
lots 1-2 ont tenu.

## 7. Questions ouvertes (à trancher au début du lot 1)

1. **L'ascension remplace-t-elle « Difficile » ou s'y ajoute-t-elle ?** Quatre axes multiplicatifs
   simultanés rendent tout diagnostic difficile (le chantier §31 l'a montré). Piste recommandée :
   l'ascension **absorbe** `DifficultyTuning`, « Difficile » devenant l'ascension 1 — un seul axe
   lisible plutôt que deux qui se cumulent en silence.
2. **Combien de crans au lancement ?** Cinq crans réellement testés valent mieux que dix annoncés.
3. **Le déblocage est-il par biome ou global ?** Par biome multiplie la grille (donc la rejouabilité)
   mais peut se transformer en corvée sur cinq niveaux.

## 8. Hors périmètre (décidé)

- **Mutateurs panachés** (run composée à la carte) : écartés au profit de l'ascension ordonnée. À
  reconsidérer seulement si l'ascension s'avère trop rigide à l'usage.
- **Nouveau contenu** (armes, greffes, biome, boss) : explicitement reporté. Une arme de plus vaut
  davantage quand il existe une ascension élevée où la tester.
- **Export web** (`docs/WEB_EXPORT_ANALYSIS.md`) : sans rapport, toujours bloqué par le .NET.
