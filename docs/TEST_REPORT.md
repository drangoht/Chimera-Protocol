# TEST_REPORT — Chimera Protocol

Rapport de sessions de test. Chaque section correspond à une session de test distincte.

---

## Saturation — le cadre fonctionne : temps soutenable 60,7 % → 39,9 % — 2026-07-30

**Objet :** vérifier que le lot 1 de `docs/ENDGAME_PLAN.md` produit une difficulté réelle, sur le
critère posé par le plan lui-même (§5) : un cran doit faire baisser le **temps soutenable** de plus de
**6 %**, sinon il coûte un palier au joueur sans rien changer.

**Protocole :** 4 runs sur les **mêmes graines** (1000-1003) que `docs/bench/ref_overtime_225.json`,
`--overtime --minutes 20 --saturation 5`, Fournaise. Comparaison **appariée**, test des signes.

| métrique | avant (asc. 0) | après (asc. 5) | delta médian | signes | verdict |
|---|---|---|---|---|---|
| **temps soutenable** | 60,7 % | **39,9 %** | **−23,2** | **0/4** | **net** |
| **survie théo. hors soins** | 24,4 s | **12,3 s** | **−11,8** | **0/4** | **net** |
| dégâts subis/s | 94,9 | 130,7 | +35,8 | 4/4 | net |
| soins ponctuels/s | 86,4 | 117,6 | +31,1 | 4/4 | net |
| PV max | 2 725 | 2 082 | −665 | 0/4 | net |
| niveau final | 128,5 | 93,0 | −34,5 | 0/4 | net |

**Le critère est dépassé d'un facteur 5** : −34 % de temps soutenable là où 6 % suffisaient, et les
quatre paires poussent dans le même sens. La survie théorique est **divisée par deux**.

Signe le plus parlant : **2 runs sur 4 finissent par une mort réelle** (`seed 1002` à 1:30 d'overtime,
`seed 1003` à 1:53), alors que les quatre atteignaient le plafond du banc sans saturation. Le bot —
celui qui tenait 22 minutes d'overtime et « survivait bien mieux qu'un humain » — ne passe plus la
deuxième minute sur la moitié des graines.

Lecture des baisses de **PV max** et de **niveau** : ce ne sont pas des effets directs des crans mais
leurs conséquences (moins de temps de jeu, donc moins de cartes). La réserve de lecture du 2026-07-30
s'applique — l'appariement est fiable sur les métriques de pression, fragile sur celles de build dès
que le changement modifie la longueur de la run.

### Le cran 1 seul : mesurable, mais au ras du seuil — et le joueur ne le sent pas

Mesuré ensuite sur les mêmes graines, `--saturation 1` (le seul cran accessible à un joueur qui n'a rien
encore battu) :

| métrique | asc. 0 | asc. 1 | delta | signes |
|---|---|---|---|---|
| temps soutenable | 60,7 % | 55,4 % | **−4,3** (−7 % relatif) | 0/3 |
| survie théo. hors soins | 24,4 s | 16,9 s | −7,6 | 0/4 net |
| dégâts subis/s | 94,9 | 119,1 | +26,3 | 4/4 net |

**Le câblage fonctionne** — les dégâts subis montent de 28 %, unanimement. Mais l'effet sur le temps
soutenable (−7 % relatif) est **à peine au-dessus du seuil de détection de la campagne** (6 %), et le
testeur a joué une session sans **percevoir aucune différence**.

C'est un défaut de conception, pas une illusion : un écart que quatre runs de banc distinguent à peine
du bruit ne peut pas se sentir en une partie. Le plan l'avait à demi anticipé (« I est le moins utile des
dix : il est là parce qu'un premier cran doit être rassurant »), mais la conséquence est plus grave que
prévu — **c'est la porte d'entrée de l'échelle**, et un premier pas invisible fait conclure que le
système entier ne fait rien. Exactement le travers de l'Auto-réparation (§33.5).

⚠ Tension à trancher : le cran 1 est délibérément l'**équivalent exact de l'ancien « Difficile »**, ce
qui est ce qui rend les records déjà gagnés valides. Le rendre plus perceptible casse cette équivalence.

### Après échange : le cran I « Hémorragie » est 2,5× plus perceptible — et il revalorise la réserve

Les crans I et II ont été échangés (Hémorragie devient la porte d'entrée). Nouvelle campagne appariée,
mêmes graines, `--saturation 1` :

| métrique | cran 0 | cran 1 (Hémorragie) | delta | signes |
|---|---|---|---|---|
| **temps soutenable** | 60,7 % | **53,6 %** | **−10,7** (−17,6 % relatif) | **0/4 net** |
| soins ponctuels/s | 86,4 | **66,3** | −19,8 | 0/4 net |
| régénération rendue/s | 8,2 | **18,7** | +10,6 | 4/4 net |
| dégâts subis/s | 94,9 | 84,3 | −7,5 | 2/4 bruit |
| survie théo. hors soins | 24,4 s | 27,1 s | +1,9 | 3/4 net |

**−17,6 % contre −7 % pour l'ancien cran I** : la porte d'entrée est maintenant nettement au-dessus du
seuil de détection, et elle agit par le canal visé (les soins ponctuels chutent, unanimement). Les
dégâts subis, eux, ne bougent pas — c'est correct, Hémorragie ne touche pas la menace.

**Effet de système non anticipé, et heureux : Hémorragie revalorise la régénération.** Les PV rendus par
la régénération **plus que doublent** (8,2 → 18,7 PV/s, 4/4). La raison est mécanique : en coupant les
soins ponctuels, le joueur cesse de vivre à PV pleins, donc la régénération trouve enfin des PV
manquants à rendre — au lieu d'être gaspillée à 58 % (§ du 2026-07-30). Le cran rend donc la **réserve de
régénération** de la 1.24.0 pertinente, alors qu'elle était noyée par un canal dix fois plus gros. Les
deux systèmes se répondent au lieu de se neutraliser.

⚠ **Piège de lecture** : la « survie théorique hors soins » **monte** (+1,9 s) alors que le cran est plus
dur. Ce n'est pas une contradiction — cette métrique exclut délibérément les soins ponctuels et bénéficie
donc de la hausse de régénération. C'est « temps soutenable » qui tranche ici, parce qu'il compte **tous**
les PV rendus. Confirmation que c'est la métrique de référence.

### Bug trouvé : le banc débloquait l'échelle dans la sauvegarde réelle

`saturation_beaten=5` a été trouvé dans le `settings.cfg` du testeur **sans aucune victoire à ce cran** :
les campagnes lancées sous `--saturation=5` appelaient `RecordSaturationBeaten` à la mort du boss, ce qui
persistait le déblocage. Protéger la valeur *choisie* (`_persistedSaturation`, sur le modèle de `--lang`)
ne suffisait pas — le **déblocage est une seconde voie d'écriture**. Corrigé : sous `--saturation`, le
déblocage n'est jamais enregistré. Sauvegarde du testeur réparée (`saturation_beaten=0`, ses complétions
datant d'avant la saturation).

### Ce qui n'est pas encore mesuré

Les crans **2, 3 et 4** ne sont pas mesurés isolément — cela demanderait trois campagnes appariées
supplémentaires (≈1 h 30 de banc). Deux conséquences :

1. il reste possible qu'un cran particulier soit **sous le seuil de détection** et ne mérite pas sa
   place dans l'échelle ;
2. la **progressivité** de la courbe (le saut entre crans consécutifs) n'est pas connue : rien ne
   garantit encore que le cran 2 soit un pas plus petit que le cran 5.

À instruire cran par cran avant d'ajouter les crans VI-X du lot 2.

## Réserve de régénération — validée au banc apparié : +94 % de régénération rendue, difficulté inchangée — 2026-07-30

**Objet :** valider `RegenReserve` (GDD §33.6) contre la référence figée le même jour, sur le critère
posé d'avance : **rendre la carte utile sans déplacer la difficulté**.

**Protocole :** 4 runs sur les **mêmes graines** (1000-1003) que
`docs/bench/ref_overtime_225.json`, `--overtime --minutes 20`, Fournaise. Comparaison **appariée**,
lecture par **test des signes**.

| métrique | avant | après | delta médian | signes | verdict |
|---|---|---|---|---|---|
| **régénération rendue/s** | 8,2 | **15,9** | **+7,3** | **4/4** | **net** |
| **temps soutenable** | 60,7 % | 60,7 % | −1,8 | 2/4 | **bruit** |
| survie théo. hors soins | 24,4 s | 23,6 s | −0,9 | 2/4 | bruit |
| soins ponctuels/s | 86,4 | 77,3 | −3,5 | 0/4 | net |
| DPS en overtime | 27 179 | 28 001 | +902 | 3/4 | net |

**Les deux objectifs sont tenus :**

1. **La régénération rendue double** (+94 %), et le test des signes est **unanime** — les 4 paires
   poussent dans le même sens. Le débit nominal n'a pas changé d'un PV : c'est bien le gaspillage de
   58 % qui est récupéré, exactement le mécanisme visé.
2. **La difficulté n'a pas bougé** : « temps soutenable » reste à 60,7 % (delta classé *bruit*), donc
   dans la bande de validation ±6 %. La carte devient utile sans allonger l'overtime, ce qui était la
   condition pour ne pas rouvrir `StatAcceleration = 2,25`.

### Réserve de lecture : l'appariement diverge dès que le nombre de tirages change

`PV max` (0/4, −125) et `puissance` (3/4, +650) sont classés « net » et **ne doivent pourtant pas être
lus comme des effets du changement**. La réserve fait gagner ~2 niveaux de plus (le porteur survit
mieux, tue davantage), donc **le nombre de cartes tirées change** — et la séquence RNG, bien que fixée
par la graine, ne fait plus correspondre les mêmes tirages. Sous `--auto-play` les cartes sont tirées
**au hasard**, si bien qu'un Blindage de moins suffit à faire bouger les PV max de 125.

Règle d'usage qui en découle : **l'appariement est fiable pour les métriques de pression** (régénération
rendue, dégâts subis, temps soutenable — des états moyens), et **fragile pour les métriques de build**
(PV max, puissance, niveau) dès que le changement testé modifie la longueur de la run.

### Ce que le banc ne peut pas dire

La question de design d'origine — *un joueur humain choisira-t-il enfin cette carte ?* — reste
**ouverte** : le bot tire ses cartes au hasard et n'arbitre pas. Le banc établit que la carte délivre
désormais ce qu'elle promet ; seule une session jouée dira si l'arbitrage change (44 Blindage contre 1
Auto-réparation, GDD §33.5). À observer avec le liseré cyan sous la barre de vie.

## Banc multi-run — première campagne réelle : le canal de soin dominant n'était pas celui qu'on réglait — 2026-07-30

**Objet :** produire la mesure de référence du banc multi-run (`tools/power_curve_multi.py`, livré la
veille) sur le réglage publié `OvertimeEscalation.StatAcceleration = 2,25`, afin de disposer d'une
base `avant/après` appariée pour les prochains réglages d'overtime.

**Protocole :** `--overtime --runs 6 --biome fournaise` (= `--start-at 13 --saturate-arsenal`,
`--timescale 3`, graines 1000…1005). Campagne **interrompue après 2 runs complètes** ; le relevé
ci-dessous ajoute la run `seed 42` (run complète depuis 0, même build) → **3 runs exploitables**.

### La campagne coûte 20× son estimation, et `--timescale` n'y est pour rien

| | attendu | mesuré |
|---|---|---|
| durée réelle d'une run d'overtime | ~4 min (12 min de jeu ÷ 3) | **~11,8 min** (708 s) |
| accélération effective | ×3 | **×1,0** |

En nuée d'overtime (300 entités) le headless est **limité par le CPU** : `Engine.TimeScale` demande 3×
mais le moteur ne fournit que le temps réel. Conséquence de planification : une campagne de 6 runs
coûte **~70 min**, pas 25. `--timescale` reste utile en début de run, où la densité est faible.

### Défaut de méthode nº 1 : la survie du bot est censurée, et le rapport n'en disait rien

Les deux runs de campagne se sont terminées sur la **limite de temps** (`bench_limit`), pas sur une
mort — survie identique à la seconde (1500 s), overtime 11:45 dans les deux cas. Le script en tirait
une médiane, un p90 et un « plus petit écart détectable » de **13 %** : la variance était écrasée par
le plafond, donc la campagne s'annonçait **précise là où elle n'avait rien mesuré**.

Corrigé : une run `bench_limit` est marquée censurée, sa survie s'affiche « ≥ », et dès que la moitié
des runs sont dans ce cas les métriques de durée affichent `non mesurable (censuré)` au lieu d'un
chiffre. Une run interrompue sans `# fin de run` (le cas de `seed 1002`, coupée par l'arrêt) est
désormais **écartée** : elle entrait auparavant dans les stats comme une mort à l'instant de la coupe.

### Défaut de méthode nº 2 : le bot survit bien mieux qu'un humain

| | joueur (5ᵉ session, 2026-07-29) | bot de banc |
|---|---|---|
| survie en overtime | 8 min 36 s (mort subie) | **22 min 42 s** (`seed 42`, mort subie) |

Arsenal saturé, le bot tient **2,6× plus longtemps** que le joueur sur le même réglage. Lever la
censure demanderait ~40 min de jeu par run, soit des **heures** de campagne — et mesurerait surtout
l'habileté du bot. La survie du bot est donc **abandonnée comme critère de réglage**.

Remplacée par la **survie théorique** = PV max ÷ (dégâts subis − régénération rendue), lue sur chaque
échantillon d'overtime : non censurée, disponible dans le budget de 12 min, et elle décrit la
**pression produite par le réglage** plutôt que la performance de l'agent qui l'encaisse.

### Le résultat qui compte : les soins ponctuels écrasent tout le reste

Médianes sur 3 runs (les deux dernières colonnes sont nouvelles) :

| métrique | médiane | bruit | plus petit écart détectable |
|---|---|---|---|
| dégâts subis/s en overtime | 140,5 | 13 % | ±12,6 (9 %) |
| **régénération rendue/s** | **13,6** | 23 % | ±2,1 (16 %) |
| **soins ponctuels/s** | **129,7** | 15 % | ±12,8 (10 %) |
| PV max | 2 860 | 21 % | ±479 (17 %) |
| survie théo. hors soins | 17,5 s | 18 % | ±2,6 (15 %) |
| **temps soutenable** | **60,4 %** | 5 % | ±2,1 (3 %) |

Trois lectures, toutes issues du même relevé :

1. **Le canal de soin dominant est le soin ponctuel, pas la régénération** — 129,7 contre 13,6 PV/s,
   un facteur **9,5**. La régénération ne représente que **9,7 %** des dégâts encaissés. Le retour du
   testeur (« l'effet de l'Auto-réparation ne se voit pas ») est donc exact *deux fois* : faible
   devant les dégâts, et surtout **noyée dans un canal dix fois plus gros** (orbes, lifesteal, et la
   carte Blindage qui soigne de son propre gain). Toute retouche de la régénération est invisible par
   construction tant que ce rapport tient.
2. **Le joueur est en surplus de PV 60 % du temps d'overtime.** La mort ne peut alors venir que d'un
   **pic** — ce qui est cohérent avec les deux morts « subies » observées et avec le fait que la
   défense moyenne ne prédise pas l'heure de la mort. Régler la survie moyenne par les PV max ou la
   régénération revient à agir sur les 40 % restants.
3. **Le levier de survie en overtime est le soin ponctuel**, et il est piloté par les tirages de
   cartes — donc par le **choix du joueur**, ce qui confirme et précise la requalification du
   2026-07-29 (`MeasuredHpGainPerOvertimeMinute` = profil de jeu, pas constante).

**Ce que la campagne ne dit pas :** n = 3, dont 2 censurées sur la durée, sur un seul biome. Les
seuils de détection ci-dessus valent pour cette taille de campagne ; « temps soutenable » est de loin
la métrique la plus stable (bruit 5 %) et devient le candidat naturel pour trancher un réglage.

### Défaut de méthode nº 3 : une run sans overtime pollue une campagne d'overtime

En agrégeant les graines relevées, la run de calibration du bot (`seed 777`, 7:18, **jamais entrée en
overtime**) apportait des **zéros** sur toutes les colonnes « en OT » : médianes écrasées et p10 à 0.
Corrigé — ces runs sont exclues des métriques « en OT » seulement, et restent comptées pour le niveau,
la puissance et les PV max. Corollaire visible dans le rapport : mélanger deux protocoles (run depuis
0 vs `--overtime`) fait bondir le bruit sur les PV max de **21 % à 64 %**. Une campagne doit être
homogène ; l'outil le signale mais ne peut pas le réparer.

### Référence figée — 4 runs appariées, et le bruit s'effondre

Deux tentatives de campagne en tâche de fond ont été **tuées par l'environnement** (~12 min réelles
par run). Contournement, qui découle directement de l'abandon de la survie comme critère : puisque les
métriques retenues se lisent **échantillon par échantillon**, une fenêtre d'overtime de **7 min**
suffit — soit `--minutes 20`, ~6,7 min réelles par run, lançable au premier plan. Les 4 graines ont
donc été **rejouées dans ce protocole unique** plutôt que mélangées au précédent.

Campagne `--overtime --minutes 20`, graines 1000-1003, Fournaise → `docs/bench/ref_overtime_225.json` :

| métrique | médiane | bruit | plus petit écart détectable |
|---|---|---|---|
| niveau final | 128,5 | 5 % | ±3,9 (3 %) |
| PV max | 2 725 | 5 % | ±87 (3 %) |
| DPS en overtime | 27 179 | **4 %** | ±659 (2 %) |
| dégâts subis/s | 94,9 | 13 % | ±7,4 (8 %) |
| régénération rendue/s | 8,2 | 25 % | ±1,3 (16 %) |
| soins ponctuels/s | 86,4 | 10 % | ±5,4 (6 %) |
| survie théo. hors soins | 24,4 s | 11 % | ±1,7 (7 %) |
| **temps soutenable** | **60,7 %** | 9 % | ±3,3 (6 %) |

**C'est le résultat qui valide le chantier.** Deux sessions humaines différaient d'un facteur **2,4**
(240 %) à l'entrée en overtime ; le protocole standardisé ramène la dispersion à **4-13 %** selon la
métrique. Un réglage dont l'effet dépasse ~6 % sur le temps soutenable est désormais **décidable en
28 minutes de banc**, sans session jouée.

Deux confirmations au passage :

1. **« Temps soutenable » est invariant à la fenêtre d'observation** — 60,4 % sur 11:45 d'overtime,
   **60,7 %** sur 6:45. La « survie théorique » ne l'est pas (17,5 s → 24,4 s : la pression croît avec
   le temps, donc la médiane dépend de la fenêtre). C'est un argument décisif pour en faire **la**
   métrique de réglage : elle est comparable entre campagnes de durées différentes.
2. **Le rapport soins/régénération se confirme sur 4 runs** : 86,4 contre 8,2 PV/s, soit **×10,5**
   (×9,5 sur le relevé précédent).

**Mode d'emploi pour le prochain réglage d'overtime :**

```
…modifier la valeur…
py tools/power_curve_multi.py --overtime --minutes 20 --runs 1 --seed-base 1000   # puis 1001…1003
py tools/power_curve_multi.py --report-only --runs 4 --compare docs/bench/ref_overtime_225.json
```

Lire le **test des signes** (« hausses/bougé »), sur **temps soutenable** en premier — jamais la
survie du bot.

### Le chemin `--compare` validé, et un contresens de verdict corrigé

Le code de comparaison appariée n'avait jamais été exécuté. Test à blanc (la référence comparée à
elle-même) : **4/4 paires appariées, deltas nuls au chiffre près** — ce qui valide au passage le
**déterminisme de `--seed`**, les graines rejouées redonnant exactement les mêmes relevés.

Ce test a révélé un contresens : le verdict affichait **« ← net » sur un delta de +0,0**. Le test des
signes comptait les **ex æquo** comme « pas en hausse » (0/4 → interprété comme un effet unanime à la
baisse). Or un ex æquo ne porte aucune direction : ils sont désormais **écartés du décompte** (colonne
« hausses/bougé »), et une campagne rejouée à l'identique affiche **« ← identique »**. Sans ce
correctif, le premier réglage sans effet aurait été présenté comme un effet net.

---

## Survie en overtime — session jouée de validation — 2026-07-29

**Objet :** valider en jeu le correctif d'overtime (`081b6f6`, design → `docs/GDD.md` §31), la survie
n'étant pas mesurable en banc (le bot `--auto-play` ne se déplace pas).

**Protocole :** `tools/power_curve_session.ps1 -Biome fournaise` (palier de menace 3, difficulté
Normal), session jouée jusqu'à la mort. Journal `user://power_curve.log`, échantillon / 15 s.
Correctif vérifié actif : aucune source `.cs` postérieure à la DLL utilisée.

**Résultat brut :** overtime à 13:00, mort réelle (`# fin de run : death`) à **14:14** →
**74 secondes de survie**, contre 5-10 minutes visées (GDD §9.2).

### Le correctif tient sa promesse — et ne change presque rien

| | avant `081b6f6` | après |
|---|---|---|
| dégâts entrants en fin d'overtime | 92,5/s en 54 s | **61,8/s en 74 s** |
| survie en overtime | ~60 s | **74 s** |

−33 % de dégâts entrants pour **+14 s**. Le goulot était donc ailleurs.

### Le vrai goulot : la défense sature deux minutes avant l'overtime

| | 4 min | 8 min | 11 min | 12 min | 14,2 min |
|---|---|---|---|---|---|
| PV max | 304 | 412 | **451** | **451** | **451** |
| réduction de dégâts | 0,40 | 0,40 | 0,40 | 0,40 | 0,40 |
| DPS joueur | 1 312 | 3 420 | 7 930 | 12 535 | 17 464 |
| survie sans soin | 152 s | 31 s | 13 s | 10,5 s | 7,3 s |

Deux constats, tous deux issus du relevé :

1. **Effet de bord du §30.** `PassiveScaling` amortissait aussi les +25 PV/niveau de
   `reinforced_plating` : 500 → 251 PV à L20, d'où le plafond de 451 atteint dès la 11ᵉ minute.
   → corrigé, les PV max sont exemptés de l'amortissement (`InventorySystem.ApplyPassiveDelta`).
   Estimation : survie ~1 min → ~2 min. **238 tests** au vert.
2. **16 niveaux pour rien.** Le joueur passe du **niveau 124 au niveau 140** pendant les 74 s
   d'overtime, sans aucun gain : armes L20, passifs saturés et retirés du pool par le §30. Aucune
   progression ne reste ouverte alors que la menace ne sature jamais. → non tranché, cf. GDD §31.6.

**Conclusion :** la cible des 5-10 min n'est pas atteignable par réglage de pentes seul. Publication
de la 1.23.0 à arbitrer — le correctif PV est un gain net, le trou de progression est un chantier.

### Suite — cartes de surcharge (`OverloadCards`, GDD §33)

Chantier ouvert dans la foulée sur le point 2 ci-dessus. Validation **en banc**, la chaîne complète
étant fonctionnelle et non un équilibrage (le calibrage, lui, demandera une session jouée).

| contrôle | résultat |
|---|---|
| tests unitaires | **243** au vert (5 nouveaux : `OverloadCardsTests`) |
| compilation | 0 avertissement, 0 erreur |
| `--saturate-arsenal` draine le pool | **156 cartes** consommées → 5 fusions L20 + passifs saturés, pool vide |
| carte proposée pool vide → appliquée | OK — `[InventorySystem] Surcharge overload_plating ×1`, puis `overload_regen ×1` au run suivant (tirage aléatoire) |
| PV max effectifs, `reinforced_plating` L20 | **675** (contre 451 avant `c15f6db`) — le correctif PV est vérifié en jeu |
| non-régression, run complète Givre 17 min sans le flag | voir ci-dessous |

**Trois obstacles rencontrés, tous instructifs pour la suite** (consignés dans `docs/PITFALLS.md`) :

1. **Le banc n'atteint jamais la saturation.** Bot immobile → niveau **73** en 17 min de jeu (armes
   L12-16), là où la session jouée est au niveau **124** dès la 13ᵉ minute. Le chemin de code était
   structurellement intestable sans flag dédié.
2. **Monter armes et passifs au plafond ne vide pas le pool** : il se remplit alors des **fusions**,
   justement rendues disponibles par ces niveaux max, puis de leur montée de L1 à L20. D'où
   `LevelUpSystem.DebugDrainPool`, qui boucle jusqu'à point fixe.
3. **Un bot à l'arsenal saturé ne monte plus jamais de niveau** : il tue tout à distance et ne
   ramasse plus un seul orbe d'XP (`N=0` sur 300 s). Le flag doit donc octroyer explicitement le
   niveau qu'il sert à rendre observable.

### Session jouée de calibrage — 2026-07-29 (2e session) — **borne inférieure, cible non mesurée**

Même protocole (Fournaise, palier 3, Normal), avec les trois correctifs. Fin à **18:18**, overtime à
13:00 → **5 min 18 s d'overtime**.

> ⚠ **Le testeur s'est laissé mourir volontairement** : il estime que la run « aurait pu durer
> beaucoup plus longtemps ». 5:18 est donc un **plancher**, pas la survie réelle, et la cible du GDD
> §9.2 (5-10 min) n'est **ni validée ni infirmée** — elle pourrait être dépassée. Le journal ne
> distingue pas une mort subie d'une mort consentie (`# fin de run : death` dans les deux cas) : ne
> jamais conclure un calibrage de survie sans cette confirmation orale.

| | 1ʳᵉ session (081b6f6 seul) | 2ᵉ session (+ c15f6db + 8b36e32) |
|---|---|---|
| survie en overtime | 74 s | **318 s** (×4,3) |
| dégâts entrants encaissés à la mort | 61,8/s | **254,8/s** |
| PV max à la mort | 451 (plat depuis la 11ᵉ min) | **2680** |

La courbe de PV n'est plus plate — c'était tout l'objet du chantier :

| | 11 min (saturation) | 13 min (OT) | 15 min | 18 min (mort) |
|---|---|---|---|---|
| PV max | 700 | 1060 | 1780 | **2680** |
| multiplicateur de dégâts | 2,08 | 2,73 | 3,21 | 3,81 |
| dégâts subis | 26/s | 29/s | 48/s | 114/s |

**Défaut relevé : l'Auto-réparation est un choix mort.** Prises déduites des stats entre la
saturation (11ᵉ min) et la mort : Blindage `(2680−700)/45` = **44**, Surtension `(3,81−2,08)/0,05` =
**~35**, pour **80 niveaux** gagnés sur la période. 44 + 35 = 79 : l'Auto-réparation n'a donc été
choisie **qu'une fois sur 80**. À +0,6 PV/s face à 100+ dégâts/s, elle est invisible, quand +45 PV
max est immédiat et se cumule avec son propre soin. Sur trois cartes, le joueur n'en arbitre que
deux. À instruire — la cible étant atteinte, toucher à ce paramètre invaliderait ce relevé.

**Ce que la puissance devient** : indice ×1,48 et population ×1,80 entre l'entrée en overtime et la
mort — à comparer au ×6,42 corrigé en 1.22.0 (§30). La Surtension n'a donc pas rouvert le
power-creep sur cette run.

**Conclusion et suite** : la mort volontaire a révélé que le pendule était allé trop loin. En
comparant les deux croissances sur ce cas, la menace passait **sous** la défense à 5 min d'overtime
(×2,37 contre ×2,44) — les cartes font gagner ~306 PV/min au joueur, qui remportait donc la course.
→ `StatAcceleration` **1,5 → 3** (GDD §31.7). Le critère de test passe d'un seuil absolu à une
comparaison des deux pentes.

### 3ᵉ session jouée — `StatAcceleration = 3` : trop dur

Mort **subie** à 14:31, overtime à 13:00 → **1 min 31 s**. Très en deçà des 5-10 min.

Mais le relevé interdit d'imputer l'écart au seul réglage. **À l'entrée en overtime, où
`StatAcceleration` n'a encore aucun effet** (zéro minute écoulée), les deux sessions divergeaient
déjà d'un facteur 2,4 :

| à t = 13 min | session à 1,5 | session à 3 |
|---|---|---|
| PV max | 1060 | **745** |
| dégâts subis | 28,9/s | **48,9/s** |
| survie sans soin | 36,7 s | **15,2 s** |
| PV à la mort | 2680 | 1195 |

Les PV sont restés bloqués à 700 jusqu'à 12:48 (contre 835 dès 12:00) : l'arsenal s'est saturé plus
tard, donc bien moins de cartes de surcharge accumulées avant la bascule. **Une session par réglage
mesure surtout le bruit.**

→ valeur retenue **2,25**, au milieu des deux essais. Le seuil du 2ᵉ critère de test est assoupli de
1,3 à 1,25 : fixé à 1,3 il excluait `StatAcceleration = 2`, pourtant défendable — le test doit suivre
le design, pas le contraindre. **244 tests.** À revérifier sur **plusieurs** runs.

### Ergonomie — deux angles morts signalés par le testeur

Sans rapport avec l'équilibrage, relevés dans la même passe.

1. **Le dash n'annonçait sa touche nulle part** — ni HUD, ni description de greffe, ni écran
   d'assimilation. Une run entière jouée sans savoir qu'une touche existait. → ligne « Shift —
   esquive » sous la rangée de greffes + « ▸ Appuie sur Shift pour l'utiliser » à l'acquisition,
   libellé lu de l'`InputMap` (touche remappable).
2. **L'Auto-réparation était crue *active*** — « son effet ne se voit pas et ce n'est pas clair
   comment la déclencher ». → indicateur `♥ +X/s` au HUD + description explicite (« automatiquement
   et en permanence. Aucune touche à presser »). **Valeur inchangée à dessein**, cf. GDD §33.5.

Validé en capture (`--saturate-arsenal --force-graft=erratic_servos`, fenêtré d3d12). Le premier
essai plaçait la touche *sur* le slot de greffe : illisible, le slot étant en `ClipContents` et
rognant « Shift » aux deux bords — corrigé en le sortant du slot, vérifié à la capture suivante.

### Deux correctifs supplémentaires signalés par le testeur

**1. Les modales étaient assombries par la vignette.** `PostFX` (vignette plein écran) est un
`CanvasLayer` à **layer 90**, et trois écrans passaient dessous : level-up (10), fin de run (20),
assimilation (60). Leurs bords — donc les cartes latérales — étaient assombris, alors qu'une modale
doit se lire à luminosité constante. Le menu pause (100) n'avait pas le défaut, ce qui rendait le
symptôme partiel et déroutant. → remontés à **97 / 98 / 97**.

Vérifié par la mesure sur capture (et non à l'œil) : luminance du bord droit contre le centre,
**0,97 / 255** d'écart. ⚠ Piège de méthode rencontré : la première mesure donnait 10,6 d'écart, mais
c'était la carte **focalisée** qui était *plus claire* (liseré cyan) — une vignette aurait assombri
les *deux* bords. Toujours comparer des zones de même état de focus.

**2. Les tirs ennemis écrasaient le reste du mixage.** Mesure RMS de toute la banque SFX :

| SFX | RMS |
|---|---|
| `sfx_weapon_sentinel_shoot` | **−7,5 dB** — le plus fort de la banque |
| `sfx_enemy_sentinel_projectile` | −13,6 dB |
| `sfx_weapon_rail_shoot` (joueur) | −16,5 dB |
| `sfx_weapon_impulse_shoot` (joueur) | −16,9 dB |
| `sfx_player_hit` | −19,5 dB |

Le tir ennemi était **+9,4 dB au-dessus du tir du joueur** — près de trois fois plus fort en
amplitude — et joué par chaque sentinelle à l'écran. Les SFX venant de banques Kenney distinctes
n'avaient jamais été nivelés, et `PlaySfx` leur appliquait à tous le même gain. → nouvelle table
`AudioSystem.MixGainDb`. Corrigé en code plutôt qu'en réencodant le WAV : l'asset CC0 reste intact et
le réglage reste lisible.

Premier essai à **−9 dB**, qui alignait exactement le tir ennemi sur celui du joueur : jugé « encore
un peu fort » à l'écoute en jeu. La mesure ne suffisait pas — elle compare des fichiers, pas des
situations : une dizaine de sentinelles tirent *ensemble* contre une seule arme du joueur. → porté à
**−12 dB** (et −6 dB pour `sfx_enemy_sentinel_projectile`), ce qui place le tir ennemi ~2,6 dB
*sous* celui du joueur. **À revalider à l'oreille.**

### 4ᵉ session jouée — non exploitable

Mort réelle à **4:36**, soit avant l'overtime : la phase à mesurer n'a jamais été atteinte. Build
tout en offensive — 36 niveaux pour un seul point en `reinforced_plating`, donc **0 % de réduction
de dégâts** (le niveau 1 n'en accorde aucune) et 200 PV, contre 304 PV / DR 0,40 au même instant sur
la session de référence. Aucun rapport avec les correctifs (`StatAcceleration` n'agit qu'en overtime,
l'exemption des PV ne joue qu'au-delà du niveau 3 de la Plaque). `StatAcceleration = 2,25` **reste
donc à valider**.

### 5ᵉ session jouée — `StatAcceleration = 2,25` : **cible atteinte**

Fournaise, palier 3, Normal. Entrée en overtime à **13:00**, `# fin de run : death` à **21:36** →
**8 min 36 s d'overtime, mort subie**, dans la fenêtre de 5-10 min du GDD §9.2. C'est la première
run à mesurer cette valeur ; les deux essais précédents l'encadraient sans la toucher (1,5 → ≥5:18
interrompue volontairement ; 3 → 1:31 subie).

| | entrée OT (13:00) | fin (21:36) | facteur |
|---|---|---|---|
| Défense (`MaxHp`) | 1015 | 2140 | **×2,11** |
| Menace (dégâts subis/s) | 28,5 | ~259 | **×9,1** |
| Niveau | 126 | 207 | +81 |
| Indice de puissance | 3029 | 5617 | ×1,85 |

La menace distance donc franchement la défense — ce que le §31.4.3 exige — et il faut malgré tout
8 min 36 s pour tuer. Le dernier échantillon est partiel (2 s) à **573 dégâts/s** : submersion, pas
usure.

**Ce que montre la courbe des PV, plus instructif que le chiffre de survie.** La pente de la défense
n'est pas constante — elle s'effondre en cours d'overtime :

- 13:00 → 16:00 : 1015 → 1825 PV, soit **270 PV/min** ;
- 16:00 → 21:36 : 1825 → 2140 PV, soit **56 PV/min**, dont un **plateau strictement plat de 16:00 à
  17:48** (16 niveaux gagnés, **zéro PV**).

Sur ce même plateau le DPS monte de 27 % (26 948 → 34 135) : le joueur prenait **Surtension** plutôt
que **Blindage**. Ce n'est pas un défaut des cartes de surcharge, c'est **l'arbitrage offense/défense
qu'elles étaient censées créer** — et c'est ce choix, pas la constante d'escalade, qui a décidé de
l'heure de la mort. Corollaire pour tout réglage futur : `StatAcceleration` et
`OverloadCards.Plating.Delta` sont couplés, mais **le choix du joueur pèse davantage que les deux**.

**Compte des prises de cartes de surcharge**, déduit des colonnes du journal (81 niveaux d'overtime) :

| carte | prises | déduction |
|---|---|---|
| **Surtension** | ~46 | `mult_degats` 2,73 → 5,06, +0,05/prise |
| **Blindage** | 25 | `pv_max` 1015 → 2140, +45/prise |
| reste, dont **Auto-réparation** | ≤10 | non instrumenté — le journal ne relève pas la régénération |

Le ratio Surtension/Blindage vaut **1,84** ici, contre **0,80** sur la session du même jour (35 contre
44). Même joueur, deux runs, arbitrage inversé : le repère
`OverloadCards.MeasuredHpGainPerOvertimeMinute = 306` décrit **un profil de jeu, pas une constante du
système** — la note du champ a été corrigée en ce sens.

**Lacune d'instrumentation relevée :** `PowerTelemetry` ne journalise pas la régénération, si bien que
la question ouverte « l'Auto-réparation est-elle prise maintenant que son effet est visible ? » ne
peut pas être tranchée sur ce relevé — seulement bornée à ≤10 prises sur 81. Une colonne `regen_ps`
la rendrait mesurable au lieu de reposer sur le souvenir du testeur.

État du build en fin de run : armes toutes L20 (5), `reinforced_plating` L20, `thermal_core` L20,
`capacitor` **L7** et `servo_motors` **L5** (les deux s'arrêtent d'eux-mêmes — rendements décroissants
du §30, comportement attendu), DR au cap 0,40, vitesse au cap 380.

→ **`StatAcceleration = 2,25` est retenu.** Réserve maintenue : une run unique ne mesure pas la
variance (facteur 2,4 constaté entre deux entrées en overtime), mais la valeur tombe au milieu de la
fenêtre visée, avec une mort subie — les deux bornes fausses sont écartées.

## Écran de pause — les boutons sortaient de l'écran en fin de run — 2026-07-29

**Signalé par le testeur :** « lorsqu'il y a beaucoup d'informations affichées sur l'écran de pause on
ne voit plus le bouton pour quitter ».

**Cause, lue dans `PauseScreen.BuildLayout`.** Titre, corps à deux colonnes **et** les trois boutons
étaient empilés dans un seul `VBoxContainer`, dans un `CenterContainer`, **sans `ScrollContainer` ni
plafond de hauteur**. Le panneau grandissait donc avec la run : 5 armes L20 et leur ligne de détails,
4 passifs, 5 greffes à description multiligne. Passé la hauteur de la fenêtre, et le panneau étant
*centré*, le débordement se répartit en haut **et** en bas — emportant les boutons, situés tout en
bas. Conséquence concrète : **plus aucun moyen d'abandonner la partie** au moment précis où le joueur
en a le plus besoin (fin de run chargée).

**Correctif.** Seul le corps entre dans un `ScrollContainer` ; titre et boutons restent hors du scroll
et donc toujours visibles. Le corps demande sa hauteur naturelle
(`cols.GetCombinedMinimumSize().Y` — un `ScrollContainer` ignore la taille minimale de son contenu
dans l'axe de défilement et s'écraserait à zéro sans cela), puis on retranche l'excédent mesuré sur le
panneau assemblé.

**Un premier essai a échoué et vaut d'être noté** : réserver une constante de 300 px pour le chrome
(titre + séparateurs + 3 boutons). Vérifié en capture — « Quitter la partie » restait **coupé au bord
de l'écran** : le chrome réel vaut ~430 px, les cadres « plaque blindée » d'`UiStyle` rendant les
boutons bien plus hauts que leur `CustomMinimumSize` de 44. Remplacé par une mesure —
`overflow = panel.GetCombinedMinimumSize().Y − budget` — qui ne dépend d'aucune constante devinée et
suit la police, la langue et la résolution.

**Vérifié en jeu** (`--saturate-arsenal --force-graft=all`, soit le contenu maximal) : les trois
boutons tiennent entièrement dans la fenêtre avec de la marge, et Page↓ fait défiler le corps jusqu'à
la cinquième greffe. Le défilement clavier/manette passe par `ui_page_up`/`ui_page_down` **seuls** :
`ui_up`/`ui_down` servent déjà à naviguer entre les trois boutons, les capturer aurait cassé la
chaîne de focus.

## Mid-boss par biome — validation en jeu — 2026-07-29

**Chantier :** `docs/EXPANSION_PLAN.md` B.3, dernier point non livré (design → `docs/GDD.md` §32).

### Constat de départ (lecture de `data/enemies.json`)

`master_sentinel` était déclarée à **16:00** pour une run de **13 minutes** : elle n'apparaissait
**jamais** en run normale. `rust_stalker` à 12:00 arrivait juste avant le boss. Trois niveaux sur
cinq (Sanctuaire, Givre, Fournaise) n'avaient aucun champion de mi-run.

### Protocole

Nouveau flag **`--debug-enemy=<id>`** : spawn isolé du champion visé, avec le scaling de **sa** fenêtre
de spawn (8 min) et non celle du boss — sans quoi un mid-boss serait mesuré avec ~50 % de PV en trop.
Captures en jeu via `tools/capture_midboss.py` (d3d12, fenêtré, rafale).

| contrôle | résultat |
|---|---|
| spawn des 3 champions (headless, 3 biomes) | OK — aucune exception, **aucune** erreur `no animation` |
| tests unitaires | **237** au vert |
| run de fumée complète (Givre, `--auto-play --invuln`) | 0 erreur |
| Gardien Néon en jeu | bouclier magenta + nœuds cyan lisibles, tirs absorbés côté couvert |
| Sentinelle Cryo en jeu | 3 plaques de givre déposées **alignées dans l'axe** du tir |
| Colosse en Fusion en jeu | flaque de magma déposée sous ses pas pendant la charge |

### Deux défauts trouvés et corrigés par la capture

1. **Le bouclier sortait gris.** Pixels mesurés à **(142,142,145)** au lieu du magenta attendu.
   `EnemyBase.HitFlash` anime `Modulate` depuis `(5,5,5,1)` et `Modulate` se propage au `_Draw` du
   nœud : ×5, toutes les composantes saturent. Le joueur tirant en continu, l'état flashé *était*
   l'état normal — la couleur disparaissait exactement quand elle sert à viser la brèche. Corrigé par
   `ChampionOverlay`, parenté à la racine (même parti pris que `BossHazard`). Vérifié après correctif :
   bouclier magenta franc.
2. **Deux champions se fondaient dans leur propre biome.** Colosse brun sur le sol brun de la
   Fournaise, Sentinelle bleue sur le sol bleu du Givre — repérables à leur seule aura. Châssis
   assombris (accents d'énergie seuls en couleur vive).

### Piège de protocole (noté dans `docs/PITFALLS.md`)

Première série : **huit captures d'une arène vide**, interprétées à tort comme un spawn défaillant.
En réalité un champion apparaît **hors champ** (~800 px) et rejoint le joueur à sa propre vitesse —
58 px/s pour la Sentinelle, qui garde en plus ses distances à 250 px. Il faut ~15 s d'approche avant
de déclencher la rafale (`WARMUP` dans `capture_midboss.py`).

### Défaut trouvé au premier test joué — les champions étaient trop petits (2026-07-29)

Retour du testeur après une run complète : « le mid-boss est trop petit ». Vérifié dans les données —
c'était exact, et l'erreur était de raisonnement, pas de goût.

| entité | sprite natif | `Scale` au rendu | **taille à l'écran** |
|---|---|---|---|
| faune | 32 | — | 32 |
| **mid-boss de biome** | **48** | — | **48** |
| mini-boss globaux (`rust_stalker`, `aether_revenant`, `master_sentinel`) | 64 | — | **64** |
| boss de fin `rusted_core` | 64 | **2,4** | **154** |

`generate_midboss_sprites.py` justifiait le 48 par « plus imposant que la faune (32) sans égaler le
boss de fin (64) ». La prémisse est fausse : le boss de fin n'est pas à 64 à l'écran mais à **154**,
et le vrai voisinage d'un mid-boss, ce sont ses **pairs de rôle** `mini_boss`, tous à 64. Les trois
champions de biome étaient donc les **plus petits de tous les champions**, pas seulement plus petits
que le boss.

**Le défaut n'était pas qu'esthétique : la hitbox débordait du sprite.** Leurs statistiques avaient
été écrites pour un champion de la taille de ses pairs — le Colosse a un `contactRadius` de 36, soit
un **diamètre dangereux de 72 px**, pour un corps qui n'en occupait que 48 à l'écran. Le joueur
encaissait à une distance où il ne voyait rien.

**Correctif : 72 px** (`MidBossVisuals.SpriteScale = 1,5`), cible calée sur le `contactRadius` du
Colosse. Le facteur 1,5 garde des pixels réguliers ; l'échelle est appliquée **au rendu** et non par
régénération des PNG, parce que le générateur dessine en coordonnées entières dans un espace logique
de 48 (`range(int(y0), int(y1)+1)`) — y injecter un facteur laisserait des rangées vides. Même parti
pris que le boss de fin.

**Vérifié en jeu** (`capture_midboss.py`, les trois champions, 8 captures chacun) : le Colosse passe
d'environ 1,2× à **1,8× la hauteur du joueur** ; la Sentinelle et le Gardien dominent nettement la
silhouette du joueur. **Aucune régression sur les overlays** — le bouclier orbital du Gardien reste à
l'extérieur du corps (`ShieldRadius = 34` inchangé, `MidBossVisuals.ApplyTo` ne touche qu'au sprite),
le cône de gel reste calé sur sa portée réelle. Deux conflits potentiels écartés par lecture du code :
le retournement passe par `FlipH` (jamais par un `Scale.X` négatif), et le grossissement d'élite
(`_sprite.Scale *= EliteAffixTable.VisualScale`) ne s'applique qu'aux ennemis de
`MaxSimultaneous == 0` — donc jamais à un mid-boss.

### Reste à faire

**Équilibrage en session jouée.** Le bot ne se déplace pas : il ne peut ni contourner un bouclier, ni
sortir d'un cône, ni éviter un sillage — c'est-à-dire aucune des trois mécaniques. Les PV (700/620/800)
et les périodes d'attaque n'ont donc **pas encore été confrontés à un joueur**. À vérifier en
particulier : le Gardien Néon n'est-il pas trop long à abattre pour un build de 8 min qui n'aurait pas
compris la règle du bouclier ? La 5ᵉ session jouée (Fournaise, 21 min) a croisé le Colosse sans que
son ressenti de combat soit relevé — seule sa **taille** l'a été.

---

## Survie en overtime — l'escalade de densité se déversait sur les stats — 2026-07-28

**Point traité :** celui laissé « à surveiller » à la publication de la 1.22.0 — le testeur meurt
**1 minute après l'entrée en overtime**, alors que l'économie d'Échos est dimensionnée sur des runs
d'overtime de **5 à 10 minutes** (GDD §9.2).

### Mesure — session JOUÉE (Fournaise, palier 3, Normal)

Relevé `PowerTelemetry`, colonne `degats_subis_ps` (la seule qui ait un sens ici : un banc `--invuln`
la met à zéro, et le bot `--auto-play` ne se déplace pas). Le joueur remplit la condition de victoire
— Noyau Rouillé vaincu, TTK 18,7 s — puis continue en overtime :

| moment | PV max | réduc. dégâts | vitesse | dégâts subis/s | indice de puissance |
|---|---|---|---|---|---|
| 10,8 min | 451 | 0,40 | 380 | 39 | 1057 |
| 13,0 min (entrée en overtime) | **451** | **0,40** | **380** | 30 | 2436 |
| +54 s d'overtime (**mort**) | **451** | **0,40** | **380** | **92,5** | 3112 |

Deux courbes divergentes : l'offense croît encore, la **survie est figée depuis la 10ᵉ minute** —
`reinforced_plating` à son **niveau maximum (20)**, réduction de dégâts et vitesse aux **caps durs**
`StatCaps`. Aucun levier de survie ne reste disponible pendant que les dégâts entrants triplent.

### Cause

`EnemySpawner` dérivait ses deux temps de référence l'un de l'autre : `tStat = tDensity + offset`.
L'accélérateur d'overtime **×4**, documenté comme servant la densité, se déversait donc *en entier*
sur les PV et les dégâts — à travers le terme **quadratique** de `EnemyScaling.CurvedFactor`, qui
recevait un temps déjà multiplié par 4 et l'élevait au carré. Or à l'entrée en overtime **tous les
leviers de densité sont saturés depuis plusieurs minutes** (cap de 300 dès la 8ᵉ minute — déjà
constaté dans la session précédente de ce rapport —, intervalle de spawn au plancher dès la 11ᵉ,
taille de lot clampée dès la 4ᵉ) : ce ×4 ne densifiait plus rien.

### Correction (GDD §31)

Règle pure **`OvertimeEscalation`** : `DensityMinutes` garde le ×4, `StatMinutes` passe à **×1,5**,
et `tStat` ne dérive plus de `tDensity`. Dégâts entrants rapportés à l'entrée en overtime :

| overtime écoulé | avant | après |
|---|---|---|
| +2 min | ×1,9 | ×1,3 |
| +5 min | ×4,5 | ×2,1 |
| +10 min | **×10,9** | **×4,5** |

**237 tests** (6 ajoutés). La fenêtre visée est encodée en test (`OvertimeEscalationTests` : ni
au-delà de ×6 en dix minutes, ni en deçà de ×3 — l'overtime doit continuer de tuer).

### Vérification au banc (Fournaise, `--auto-play --invuln --timescale=3 --run-limit=1500`)

Le banc **ne peut pas valider la survie** (bot immobile + `--invuln`), mais il valide ce que le
découplage risquait de casser :

| contrôle | attendu | mesuré |
|---|---|---|
| densité préservée | population au cap | **295-301 ennemis** en continu de la 13ᵉ à la 25ᵉ minute |
| boss d'overtime tuable | TTK dans/près de 20-30 s | **23,1 à 42,7 s** sur 15 combats jusqu'à 25 min |
| courbe de puissance | ×2,5 max (cible §30.6) | ×2,22 (indice 1829 → 4066 en 12 min d'overtime) |
| stabilité | run complète | 25 min, aucune erreur, `bench_limit` atteint |

Le boss d'overtime atteint ~11 300 PV **cinq minutes après** l'entrée en overtime, contre deux
minutes avant la correction : le scaling monte toujours, deux fois et demie moins vite.

### Reste à faire

**Validation par une session jouée** — `tools/power_curve_session.ps1 -Biome fournaise`, en poussant
l'overtime jusqu'à la mort. C'est la durée de survie après la bannière OVERTIME qui tranche, et elle
n'est mesurable qu'en jouant. Cible : 5 à 10 minutes. Si la survie dépasse largement 10 minutes,
c'est `StatAcceleration` qu'il faut relever (1,5 → 2) ; si elle reste sous 3 minutes, la piste
suivante est le terme quadratique de `CurvedFactor` (GDD §31.5).

---

## Courbe de puissance du joueur — instrumentation, diagnostic et correction — 2026-07-28

**Point ouvert traité :** « la puissance du joueur explose en overtime, les boss d'overtime
deviennent triviaux » (laissé en suspens après la 1.21.0). Aucun outil ne mesurait autre chose qu'un
instantané par combat de boss : ajout de `PowerTelemetry` (flag `--power-curve`), qui échantillonne
toute la run — DPS infligé, dégâts subis, population, **indice de puissance du loadout**
(`InventorySystem.PowerIndex()` = somme dégâts/recharge) et build complet.

### Mesure initiale (banc Fournaise, 25 min, bot immobile et invulnérable)

| moment | indice de puissance | mult. dégâts | réduc. recharge | ennemis |
|---|---|---|---|---|
| 4 min | 189 | 1,65 | 0,05 | 127 |
| 12,8 min (fin du temps imparti) | 679 | 1,95 | 0,29 | 300 |
| +12 min d'overtime | **4361** | 2,70 | **0,85** | 300 |

**×6,42 de puissance en 12 minutes d'overtime**, contre ×2,8 de PV pour le boss. Second constat :
la **population sature le cap de 300 dès la 8ᵉ minute**, cinq minutes avant l'overtime — l'escalade
ne peut donc plus jouer que sur les PV unitaires.

### Cause

Les 4 passifs ne définissent que **3 niveaux** pour un plafond de **20** ; au-delà, le delta du
dernier niveau défini était réappliqué **en additif non borné**. Le cas pathologique est le
**Capaciteur** : +0,14 par niveau → **100 % de réduction de recharge dès le niveau 8**, ce qui fait
tomber **toutes** les armes au plancher `MinCooldown` (0,15 s). Une arme lourde à 1,2 s tirait alors
exactement aussi vite qu'un canon léger à 0,4 s. C'est aussi l'explication de la dispersion notée
plus bas dans ce rapport : TTK du même boss de **14,8 s à 42 s selon une seule carte prise**.

### Correction (GDD §30)

`PassiveScaling` (rendements décroissants au-delà des niveaux définis, `delta / (1 + 0,20 n)`) +
`StatCaps.MaxCooldownReduction = 0,75` + retrait du pool des passifs dont l'unique stat est au
plafond (`IsPassiveSaturated`).

### Contre-mesure (même protocole)

| | fin du temps imparti | +12 min d'overtime | ratio |
|---|---|---|---|
| avant | 679 · recharge 0,29 | 4361 · 0,85 | **×6,42** |
| après | 942 · recharge 0,55 | 2568 · 0,75 (plafond) | **×2,73** |

Les deux runs n'ont pas le même build (choix de cartes aléatoires en `--auto-play`) : c'est le
**ratio** qui vaut, pas les valeurs absolues. Effet exact à build égal, calculé sur celui du testeur
humain (`thermal_core` L16, `capacitor` L15) : multiplicateur de dégâts **3,40 → 2,36**, cadence
cumulée **−28 %** sur les 12 armes → **DPS ×0,50**. Les armes rapides (Canon 0,55 s, Flux 0,40 s)
ne perdent **rien** — elles étaient déjà au plancher ; les armes lourdes retrouvent leur identité
(Singularité : 0,15 s → 1,00 s).

### Session JOUÉE de validation (Fournaise, 2026-07-28)

Relevé par le testeur, run complète jusqu'à la mort en overtime :

| | valeur |
|---|---|
| niveau au boss de fin | 124 |
| build | 3 armes L20 + `fusion_blade` L20 · `thermal_core` L20, **`capacitor` L7**, `reinforced_plating` L20 |
| DPS effectif contre le boss | **488** |
| TTK à 4000 PV de base (9102 effectifs) | **18,7 s** |
| puissance à l'entrée de l'overtime → mort (1,1 min) | 2436 → 3112, soit **×1,33** |

Trois enseignements :

1. **Le Capaciteur s'arrête de lui-même à L7** — la carte quitte le pool dès le plafond atteint,
   exactement le comportement visé. Le testeur montait à L15-L20 auparavant, sans aucun gain.
2. **La correction tient sur la durée** : ×1,33 sur la première minute d'overtime, contre ×3,8 en
   deux minutes avant.
3. **La projection analytique sous-estimait le DPS de 40 %** (310 estimés contre 488 mesurés) : à
   4000 PV le TTK tombait à 18,7 s, sous la fenêtre. **`maxHp` porté à 5000** → ~23 s pour ce build,
   ~26 s pour un build de banc à 425 DPS. *Ne jamais calibrer ce boss autrement que sur un TTK joué*
   (GDD §20.6) — c'est la deuxième fois qu'un calcul théorique se trompe d'un facteur voisin.

À surveiller sur les prochaines runs : le testeur est **mort une minute après l'entrée en
overtime** (92,5 dégâts subis/s pour 451 PV max). C'est conforme au design — l'overtime est une
escalade brutale et la run se termine à la mort — mais `reinforced_plating` a lui aussi été amorti
(+251 PV au lieu de +500 à L20). Si les runs suivantes se terminent toutes aussi vite, c'est ce
passif-là qu'il faudra détendre, pas la difficulté.

### Bugs relevés dans le journal de cette session

Deux erreurs répétées dans `user://logs/godot.log`, **sans rapport avec le rééquilibrage** :

- **`ArgumentOutOfRangeException` dans `LevelUpSystem.BuildWeaponCards`** (9 occurrences) :
  `(int)GD.Randi() % (i + 1)` donne un index **négatif une fois sur deux** (le cast d'un `uint`
  précède le modulo). Conséquence de gameplay : la **récompense de mini-boss était perdue une fois
  sur deux**, sans aucun signe à l'écran — l'exception est avalée par le callback Godot. Corrigé en
  faisant le modulo en `uint`.
- **`There is no animation with name 'attack'`** (144 occurrences) : les scènes archétype sont
  partagées par la faune data-driven, et les 5 golems `slow_hunter` n'exposent pas d'animation
  `attack`. Corrigé par `EnemyBase.PlayAnim`, qui ne joue que si l'animation existe et **renvoie si
  elle a démarré** — indispensable pour `death`, dont dépend le `QueueFree` (un sprite sans `death`
  aurait laissé l'ennemi mort à l'écran indéfiniment).

### Banc de non-régression après correction (Fournaise, 16 min de jeu)

`--auto-play --invuln --power-curve --timescale=3 --run-limit=960`. **Zéro erreur** dans le journal
(contre 153 sur la session jouée précédente : 144 animations + 9 index négatifs). Les mini-boss
d'élite reviennent en boucle toutes les 14 s en overtime, le chemin du drop de carte a donc été
largement exercé sans lever une seule fois.

Quatre combats de boss enchaînés dans la même run — c'est la démonstration que l'escalade tient :

| combat | PV effectifs | DPS du joueur | TTK |
|---|---|---|---|
| boss de fin | 11 371 | 327 | 34,8 s |
| overtime +1 | 12 867 | 455 | 28,3 s |
| overtime +2 | 14 306 | 509 | 28,1 s |
| overtime +3 | 15 698 | 604 | **26,0 s** |

Le DPS du joueur double sur la période et les PV du boss suivent : le TTK reste dans 26-35 s au lieu
de s'effondrer à 14,8 s comme avant correction. Le Capaciteur s'est arrêté à L7, comme dans la
session jouée.

*Note de banc* : le message `resources still in use at exit` en fin de processus vient de la sortie
forcée (`--run-limit` → `GetTree().Quit()`), qui court-circuite la purge des VFX parentés à la
racine. Sans effet en jeu — une fin de run normale passe par l'écran de fin.

### Recalibrage du boss en cascade

`rusted_core.maxHp` **8000 → 5000** (4000 dans un premier temps, corrigé sur la session jouée
ci-dessus). Projection initiale de la fenêtre de TTK à la Fournaise (PV effectifs 9 100) : **~21 s** pour un build qui n'a pas monté le Capaciteur (425 DPS mesurés au banc) et
**~29 s** pour un build qui l'a saturé (617 → ~310 DPS estimés). La fenêtre **se resserre** au lieu
de se déplacer, là où l'écart allait de 14,8 s à 43 s.

**Vérifié par la session jouée ci-dessus**, qui a corrigé la valeur : le « ×0,50 » projeté était
trop pessimiste (488 DPS réels contre 310 estimés), d'où 5000 et non 4000. Le loadout figé de
`--debug-boss` donne 9-11 s sur les trois biomes testés, mais il a toujours produit des TTK 2 à 3
fois plus courts que le jeu réel (13,8 s contre 29 s joué avant correction) — il ne vaut que comme
test de non-régression.

---

## Fusions d'armes — la vraie cause du « boss trop long » — 2026-07-28

**Point de départ :** le banc de TTK (section suivante) donnait un boss 2,6 à 3,6× trop long, et un
relevé humain à **275 DPS** là où le calibrage du GDD §20.4 supposait ~711 (théoriques). Retour du
testeur : *« tout dépend du niveau du joueur arrivé au boss, et des améliorations débloquées »* —
exact, et le protocole ne mesurait ni l'un ni l'autre : le loadout `--debug-boss` était figé au
niveau 1 avec 5 armes L10 et un seul passif.

**Outillage ajouté pour lever le doute** : `--auto-play` (les écrans de level-up et d'assimilation se
résolvent seuls — sans quoi une run en banc se fige au premier niveau, ce qui explique que personne
n'ait jamais mesuré autre chose qu'un loadout figé) et `--timescale=<x>`, permettant de jouer les
13 minutes précédant le boss en ~4 minutes réelles.

**Ce qu'une vraie run révèle** (4 runs, choix aléatoires) : niveau **66-84** au boss, passifs jusqu'à
L20, armes bien au-delà de leurs 5 niveaux définis. Et un DPS qui varie **de 103 à 410** à niveau et
palier identiques. Le facteur n'était ni le niveau ni le palier, mais le **nombre d'armes fusionnées**.

| Build (même niveau ~80, même méta) | DPS avant | DPS après correctif |
|---|---|---|
| Sanctuaire — tout fusionné | 105 | **368** |
| Néon — tout fusionné | 103 | **490 / 539** |
| Avec une arme de base montée conservée | 347-410 | **858** |

**Cause** (trois défauts cumulés, cf. `GDD.md` §8) : dégâts des fusions codés en dur et jamais
multipliés (`ApplyWeaponStats` ne parcourait que la section `weapons`), retour au niveau 1 à la
fusion, et absence totale du pool de cartes — l'arme de base en étant retirée, le slot était mort
pour le reste de la run.

**Effet sur le boss, PV inchangés (21 360 au Sanctuaire) :** avec un loadout de test recalé sur les
builds réellement observés (armes L7, 4 passifs, 4 fusions), le TTK au banc tombe à **21,4 s
(Sanctuaire)** et **25,7 s (Néon)** — pile dans la fenêtre 20-30 s du GDD §20.2. **Le boss n'avait
donc pas besoin d'être rééquilibré** : le tuning envisagé (PV −63 %) a été annulé, il aurait rendu le
combat trivial une fois les fusions réparées.

**Limite connue :** la dispersion des builds reste large (368 à 2335 DPS selon les choix de cartes),
donc le TTK varie de ~10 à ~60 s à PV constants. Aucun réglage de PV ne satisfait les deux extrêmes ;
c'est le rôle du boss d'être un test de build, mais l'amplitude mérite un arbitrage de design.

**Mesuré depuis (run complète jouée, Fournaise, 2026-07-28)** — trois combats dans la même run, le
boss de fin puis ses deux réapparitions d'overtime :

| # | Moment | Niveau | DPS | PV | TTK |
|---|---|---|---|---|---|
| 1 | boss de fin (~13 min) | 126 | 617 | 27 304 | **44,2 s** |
| 2 | overtime +45 s | 135 | 1381 | 30 872 | 22,4 s |
| 3 | overtime +105 s | 144 | 2322 | 34 321 | 14,8 s |

- **Le facteur humain n'existe pas** : 617 DPS joué, dans la fourchette du banc immobile (368-858).
  Le correctif des fusions tient — les trois fusions du build ont atteint le niveau max (L20).
- **Le joueur monte bien plus haut que le banc** : niveau 126 contre 66-84 en `--auto-play` (un
  humain se déplace pour ramasser l'XP).
- **La puissance explose en overtime** : DPS ×3,8 en deux minutes, tous les passifs au plafond L20.
  Le boss d'overtime tombe en 14,8 s malgré +26 % de PV.

**Décision :** `rusted_core.maxHp` **12000 → 8000**, calibré sur ce relevé (≈ 23 s Sanctuaire /
29 s Fournaise / 32 s Néon à 617 DPS). Les dégâts du boss ne bougent pas : le joueur a survécu au
combat sans difficulté avec `reinforced_plating` L20.

**Ouvert :** la courbe de puissance dépasse celle du contenu une fois l'arbre de cartes épuisé (tous
les passifs L20 dès l'overtime) — sujet de design distinct du TTK.

---

## Banc de TTK du boss de fin — 5 biomes — 2026-07-28 (MESURE AUTOMATISÉE)

**Outil :** `BossTelemetry` + `tools/boss_ttk_session.ps1`. **Protocole :** `--headless --debug-boss
--invuln --biome=<id>`, loadout de référence (5 armes L10 + `thermal_core` L3, + `swarm_symbiote` du
perk de départ équipé sur la save du poste), difficulté Normal, boss isolé (aucun spawn ambiant).
**Version :** `1.20.0-60b82be`.

**Nature de la mesure : BORNE BASSE.** Le joueur reste immobile et invulnérable : toutes les armes,
y compris la Lame Plasma (mêlée), restent à portée en permanence. Le DPS obtenu est le maximum
atteignable par ce loadout — le TTK d'un joueur qui esquive ne peut qu'être **supérieur**.

**Dispersion :** deux passages par biome, campagne séquentielle (une seule instance à la fois). Le
DPS varie de 494 à 690 d'un passage à l'autre (±17 % autour de ~610) alors que les PV sont fixes :
selon le tirage de l'arène, le boss est plus ou moins souvent tenu à distance par un obstacle, ce qui
prive le loadout de ses armes de courte portée. Le TTK d'un même biome varie donc de ~25 % — raison
de plus pour ne jamais conclure sur un relevé isolé.

| Biome | Palier | PV effectifs | **TTK (2 passages)** | DPS moyen |
|---|---|---|---|---|
| Sanctuaire | 0 | 21 360 | **32,2 / 38,6 s** | 664 / 553 |
| Aether | 1 | 22 991 | **37,7 / 37,7 s** | 609 / 609 |
| Givre | 2 | 24 913 | **40,7 / 50,4 s** | 612 / 494 |
| Fournaise | 3 | 27 017 | **39,1 / 41,7 s** | 690 / 648 |
| Néon | 4 | 29 437 | **42,7 / 47,5 s** | 690 / 620 |

**Constat — le boss est trop long sur les cinq niveaux.** Cible GDD §20.2 : **20-30 s**, avec « > 45 s
en combat dense = épuisant ». Même en borne basse, le meilleur relevé du premier niveau (32,2 s) sort
déjà de la fenêtre, et les hauts paliers atteignent le seuil d'épuisement. Les deux causes sont
additives :

1. **Le DPS réel vaut ~62 % du DPS théorique** qui a servi au calibrage (§20.4 : 711 hors
   `thermal_core`, soit ~1031 avec ×1,45 ; mesuré : 609-665). L'écart, ce sont les projectiles qui
   ratent, le temps de vol et les temps morts — invisibles dans une somme de DPS de fiche.
2. **Le palier de menace gonfle le boss deux fois** : par `ChampionHpMult` (+27,5 % au Néon) *et* par
   le décalage `TimeOffsetMinutes` qui s'ajoute au temps de scaling (+8 % au Néon). Cumulé : +37,8 %
   de PV entre Sanctuaire et Néon, pour un joueur dont le DPS ne monte pas d'autant.

**Reste à faire avant d'ajuster les valeurs :** un relevé joué **par un humain** (mêmes flags, sans
`--invuln`) sur au moins Sanctuaire et Néon, pour mesurer l'écart entre le DPS du banc et celui d'un
joueur qui esquive. C'est cet écart qui décide de l'ampleur de la baisse de PV.

---

## Revue Fix rendu obstacles (occultation joueur, commit `e8fbd00`) — 2026-07-05 (SESSION CIBLÉE)

**Testeur :** game-tester (agent). **Moteur :** Godot 4.7.stable.mono, D3D12. **Build :** vert (0 err/0 warn). **Version :** `1.11.2-f5ecd4b`. **Fix testé :** `e8fbd00` — corps des obstacles `ZIndex 1→6` (enfants relatifs → 7-10) dans `BiomeObstacles.Build`, ombre au sol ré-ancrée en `ZAsRelative=false`/`ZIndex=1` absolu, sprite de colonne `GroundRenderer.PlaceColumn` `1→6`. Joueur à `ZIndex=5`, ennemis à `ZIndex=0` (défaut), HUD/PostFX sur `CanvasLayer`.

**Instrumentation temporaire (ENTIÈREMENT REVERTÉE, arbre propre) :** flag `--no-spawn` (arène vide, pas de level-up parasite) + 2-3 obstacles garantis autour du spawn joueur (`GroundRenderer.BuildObstacles`) pour forcer le chevauchement joueur↔obstacle de façon déterministe. Captures PyAutoGUI, 5 biomes forcés via `--biome=<id>`, dossier de captures temporaire supprimé.

**VERDICT GLOBAL : PASS.** Le fix restaure l'illusion « obstacle solide infranchissable ». Le compromis (occultation quand le joueur est devant) est **acceptable — pas de Y-sort nécessaire à ce stade**.

### 1. Occultation corps > joueur : **PASS** (les 5 biomes)
Joueur pressé DERRIÈRE un obstacle (au-dessus à l'écran, contre son pied) → le corps passe bien DEVANT le joueur :
- **Sanctuaire (pilier)** et **Aether (cristal)** : silhouettes hautes → joueur **entièrement masqué** derrière le corps (aucun pixel joueur ne perce le corps opaque). Réapparaît net dès qu'il s'écarte latéralement.
- **Givre (monolithe)** / **Néon (pylône)** : joueur masqué, seul le sommet (antenne du robot) affleure au-dessus des silhouettes moins hautes — correct.
- **Fournaise (basalte)** : silhouette basse et large → occulte **uniquement la moitié basse** du joueur (jambes derrière le rocher), tête visible au-dessus. C'est le comportement Z attendu (le corps couvre exactement la portion qui le chevauche). Avant le fix, le joueur (Z5) se dessinait toujours par-dessus le corps (Z≤4) → survol cassant l'illusion ; désormais l'ordre est correct.

### 2. Ombre au sol : **PASS**
L'ombre portée (Polygon2D ré-ancré `ZAsRelative=false`, Z absolu = 1) reste **au sol au pied de chaque obstacle** dans tous les biomes, **sous le joueur** (Z5) — le joueur marche par-dessus les ombres sans être masqué par elles. L'ombre ne flotte pas par-dessus le joueur. Détail vérifié analytiquement : Z ombre inchangé avant/après le fix (avant = corps 1 + enfant relatif 0 = 1 ; après = 1 absolu) → **aucune régression** introduite par le ré-ancrage.

### 3. Blocage physique intact : **PASS**
Collision au pied inchangée (le fix ne touche que le rendu) : joueur bloqué net au pied de chaque obstacle, ne le traverse pas, dans les 5 biomes. `CollisionLayer/Mask` inchangés.

### 4. Compromis assumé (joueur DEVANT l'obstacle) : **ACCEPTABLE / négligeable**
Sans Y-sort, l'obstacle (Z 6-10) est toujours au-dessus du joueur (Z5), donc occulte aussi le joueur quand il est « devant » (en dessous à l'écran). **En pratique peu visible** : la collision est ancrée au PIED de l'obstacle, donc un joueur venant du dessous est stoppé SOUS la base — le chevauchement « fautif » se limite aux quelques pixels du sommet de sa tête effleurant la base de l'obstacle. Combiné au mouvement constant du joueur (survivor), c'est négligeable. **Verdict : ne justifie pas un Y-sort** (qui casserait la relation `ZIndex` joueur↔VFX d'armes, cf. `Player.cs` L52-56). À rouvrir seulement si un futur obstacle très large/bas (type basalte étendu) rend le défaut gênant.

### 5. Non-régression de rendu ailleurs : **PASS**
- **UI/HUD :** `HUD.tscn` et `PostFX` sont des `CanvasLayer` → dessinés au-dessus de tout `ZIndex` du monde (max obstacle = 10 << CanvasLayer). Obstacles ne passent jamais devant le HUD (confirmé sur une frame de level-up captée : cartes/HUD nets au-dessus de la scène).
- **Joueur/VFX :** joueur (Z5) toujours au-dessus de ses VFX d'armes proches (Z2-4). Point d'attention documenté : l'éclair d'impact (`ZIndex=6`, cf. `Player.cs`) est désormais au même niveau que le corps d'obstacle — un éclair passant derrière un obstacle peut être occulté, ce qui est cohérent (obstacle solide). Aucun impact sur la lisibilité observé.

**Remontées :** aucune. Fix validé tel quel. Note pour `developpeur` (info, pas action) : si un Y-sort est un jour envisagé, il faudra préserver la relation joueur(Z5)↔VFX d'armes — piste : Y-sort limité au couple joueur↔obstacles via un `YSortEnabled` sur un conteneur dédié, en gardant les VFX hors de ce tri.

---

## Revue Fix rendu gelé v3 (shader `* COLOR`) : bleu + HitFlash + élite — 2026-07-05 (SESSION CIBLÉE)

**Testeur :** game-tester (agent). **Moteur :** Godot 4.7.stable.mono, D3D12. **Build :** vert (0 err, 87 tests).
**Working tree (non commité) :** correctif `assets/shaders/enemy_frost.gdshader` (ligne clé `COLOR = vec4(mix(tex.rgb, frosted, frost), tex.a) * COLOR;`) + `EnemyBase.cs` (`EnsureFrostMaterial`/`UpdateStatusEffects`) + `FrostVeil.cs`. **Scaffolding TEMPORAIRE entièrement REVERTÉ** (grant `frost_veil` + retrait armes offensives dans `GameManager.RegisterPlayer` ; coupe/rétablissement `TakeDamage` du voile ; maintien `Modulate=(4,4,4)` dans `HitFlash`) — arbre propre hors ce fichier. Captures PyAutoGUI joueur immobile, biome Fournaise (ennemis orange) + `--force-elites`, zooms + échantillonnage pixel objectif.

**VERDICT GLOBAL : PASS** (les 3 points visés + non-régression). Une nuance de saturation notée au point 1 (observation art/tuning, pas un blocage).

### 1. Ennemi ORANGE → BLEU dans le voile : **PASS** (avec nuance)
Un cinder/rust orange qui entre dans le voile est **clairement dé-orangé et refroidi** : échantillon corps mesuré B−R **−98 → −44** (le frost s'engage bien ; un « multiply pur » n'aurait pas pu bleuir — la réserve du FAIL historique est levée). Relief pseudo-3D préservé, **retour orange net en sortant** (ennemis hors zone vifs orange). **Nuance :** en biome **Fournaise** (ambiance/éclairage chaud), le rendu lit plutôt un **gris froid désaturé** qu'un **bleu glacé saturé** — le corps reste légèrement chaud-neutre (B<R) au lieu de virer franchement bleu (le calcul shader isolé prédit ~(64,100,175) bleu, l'éclairage chaud du biome le ramène vers le gris). Lisible comme état « gelé », mais pas le bleu vif de la spec dans les biomes chauds. → **remontée `game-designer`/`directeur-artistique`** : envisager de renforcer `frost` ou `frost_color`, ou de rendre l'ennemi gelé insensible à l'éclairage chaud (unshaded), pour un bleu saturé quel que soit le biome. Non bloquant.

### 2. NON-RÉGRESSION HitFlash sur ennemi GELÉ : **PASS** (corrige l'ancien FAIL critique)
Test déterministe : `HitFlash` scaffolé pour maintenir `Modulate=(4,4,4)` + dégâts du voile actifs (frappe chaque tick 0,2 s). Les ennemis **gelés ET frappés** dans le voile deviennent **BLANC ÉCLATANT** (pixel mesuré **(254,254,254)**), tandis que les ennemis hors voile restent orange. Le `* COLOR` du shader préserve donc bien le `Modulate` du nœud : le HitFlash compose par-dessus le gel. L'ancien FAIL (shader qui écrasait `COLOR` → HitFlash perdu) est **corrigé**.

### 3. TEINTE D'ÉLITE sur ennemi GELÉ : **PASS** (corrige l'ancien FAIL)
Lancé `--force-elites`.
- **Élite gelé ≠ bleu uniforme :** un Régénérant (self_modulate vert) gelé dans le voile lit **vert/teal** (mélange teinte+frost) ; un Frénétique (rouge) gelé lit **marron/rouge maté** (R−B +103 → +30, reste rougeâtre, PAS bleu). La teinte d'affixe traverse le gel.
- **Hors gel, teinte intacte :** élites hors voile montrent leur affixe pur (vert-olive, rouge vif, magenta). Le `* COLOR` préserve le `SelfModulate` d'élite. Ancien FAIL corrigé.

### 4. Non-régression technique : **PASS**
~8 lancements sans crash. Console : **aucune erreur de compilation shader, aucun `SCRIPT ERROR`/NullRef** (seuls messages de fermeture abrupte quand le process est tué au timeout — `Unreferenced static string`, `RID leaked at exit`, bruit normal). Perf visuellement fluide (200+ entités + voile). Bonus observé : mort joueur affiche toujours « MORT EN SERVICE » (non-régression fin de run).

---

## Revue Fix 1 v2 (rendu gelé par SHADER) : bleu + non-régression HitFlash/élite — 2026-07-05 (3)

**Testeur :** game-tester (agent Claude). **Moteur :** Godot 4.7.stable.mono, D3D12. **Build :** OK (0 err).
**Working tree (non commité) :** nouveau `assets/shaders/enemy_frost.gdshader` + `src/Entities/Enemies/EnemyBase.cs` (gel via `ShaderMaterial`, plus SelfModulate) + `src/Weapons/FrostVeil.cs`. Scaffolding temporaire (grant `frost_veil`, DPS voile abaissé, HitFlash allongé/maintenu, `--force-elites`) REVERTÉ — arbre propre hors ce fichier. Captures PyAutoGUI joueur immobile, biomes Fournaise/Sanctuaire, zooms.

### 1. Objectif de la réserve — ennemi chaud → BLEU : **PASS**
Un ennemi à dominante chaude (rust swarm / cinder orange) qui entre dans le Voile de Givre lit maintenant **clairement BLEU glacé**, relief pseudo-3D préservé (le `mix(tex, frost_color*luminance, frost)` conserve ombres/highlights). Nette réussite vs le multiply précédent. Le shader charge sans erreur console. La réserve du FAIL précédent est levée sur ce point.

### 2. NON-RÉGRESSION HitFlash — **FAIL (critique)**
Un ennemi **gelé qui est frappé ne flashe PLUS en blanc**. Test déterministe : HitFlash scaffoldé pour MAINTENIR `modulate = (4,4,4)` en continu ; tous les ennemis dans le voile (frappés chaque tick 0.2 s) devraient être blancs éclatants si le modulate compositait. **Ils restent bleus.** → le `modulate` du nœud n'est PAS appliqué par-dessus le shader.
**Cause :** le fragment écrit `COLOR = vec4(mix(...), tex.a)` en **écrasant** le `COLOR` entrant sans le remultiplier ni référencer `MODULATE`. En Godot 4, `modulate` n'est PAS auto-appliqué à un fragment canvas_item qui réécrit `COLOR` (batching 2D → passé via le built-in `MODULATE`). La supposition « le moteur multiplie ensuite automatiquement par le modulate » (commentaire du shader L8-10) est **fausse**.
**Portée aggravée :** `EnsureFrostMaterial()` pose le `ShaderMaterial` au 1er gel et ne le retire jamais (bascule seulement `frost` 0/1). Donc **tout ennemi ayant été gelé une fois ne flashe plus JAMAIS à l'impact**, même après dégel (à `frost=0`, `COLOR=tex.rgb` ignore toujours le modulate). Régression de feedback de combat majeure en build Cryo/Givre où la plupart des ennemis passent par le gel.

### 3. NON-RÉGRESSION teinte d'élite (self_modulate) — **FAIL**
Hors du voile, les élites gardent bien leur teinte d'affixe (sprite orange/vert/rouge via `SelfModulate`) — OK tant que le matériau shader n'est pas encore posé. **Une fois GELÉS, les sprites d'élite deviennent bleu uniforme** : la teinte d'affixe du sprite est perdue (le shader lit `texture()` brut, sans `self_modulate`). L'**aura** (nœud `EliteAura` séparé) conserve sa couleur → les élites restent distinguables par le halo, mais le « mélange lisible élite+gel » visé est perdu. Même défaut permanent : après un 1er gel la teinte d'affixe du sprite ne revient plus (matériau jamais retiré).

### 4. Robustesse — **OK**
Aucune erreur de compilation shader / `gdshader` / NullRef / script error C# en console (hors bruit de fermeture moteur). Perf non dégradée à l'œil (nuée + voile + shader partagé, matériau lazy → batching préservé hors gel).

### Correctif suggéré (→ developpeur)
Le shader doit **réappliquer** le modulate ET le self_modulate au lieu de les écraser. Piste (à valider sur la sémantique Godot 4.7 : `self_modulate` est porté par le `COLOR` de vertex, `modulate` par le built-in `MODULATE`) :
```glsl
void fragment() {
    vec4 tex = texture(TEXTURE, UV);
    float lum = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
    vec3 frosted = frost_color.rgb * (0.35 + 0.75 * lum);
    vec3 rgb = mix(tex.rgb, frosted, frost);
    COLOR = vec4(rgb, tex.a) * COLOR * MODULATE;  // restaure self_modulate (élite) + modulate (HitFlash)
}
```
Fichier : `assets/shaders/enemy_frost.gdshader` L14-20.

### Verdict global : **Objectif bleu PASS / non-régression HitFlash FAIL (critique) + teinte élite FAIL.**
Ne pas expédier en l'état : le rendu bleu est excellent mais le shader casse le feedback d'impact (HitFlash) de façon permanente sur tout ennemi gelé, et écrase la teinte d'affixe des élites gelés. À renvoyer au developpeur pour remultiplier `COLOR * MODULATE` dans le fragment (puis re-test HitFlash + élite gelés).

---

## Revue ciblée VFX Givre : teinte gelée (Fix 1) + densité de brume (Fix 2) — 2026-07-05 (2)

**Testeur :** game-tester (agent Claude). **Moteur :** Godot 4.7.stable.mono, D3D12. **Build :** OK (0 warn / 0 err, 87 tests verts en amont).
**Working tree :** `src/Entities/Enemies/EnemyBase.cs` + `src/Weapons/FrostVeil.cs` (non commité). Scaffolding temporaire (grant `frost_veil` dans `GameManager.RegisterPlayer`, DPS voile abaissé ponctuellement) REVERTÉ — arbre propre hors ce fichier. Captures PyAutoGUI joueur immobile, biomes Fournaise (ennemis orange) + Sanctuaire (fond neutre), zooms comparatifs.

### Fix 2 — Brume plus dense/volumétrique en statique : **PASS**
Sur fond neutre (Sanctuaire), joueur immobile : la zone lit clairement comme une **brume texturée/nuageuse**, densité interne variable (amas de puffs + motes de givre densifiées), PAS un cercle/halo sec ni un blob blanc saturé. Le liseré marque toujours la portée, le joueur reste visible, glow non clampé au blanc. Nette amélioration vs le halo concentrique précédent. Sur fond Fournaise (orange vif) la brume bleue est plus lavée mais reste lisible. RAS.

### Fix 1 — Teinte gelée « bleu franc » sur ennemis chauds : **FAIL**
Objectif non atteint. Un ennemi à dominante chaude (cinder/rust swarm, orange) qui entre dans le Voile de Givre **reste orange/terne — il ne lit PAS bleu**. Vérifié empiriquement (zooms Fournaise ET Sanctuaire : ennemis orange figés dans le voile restent orange) et confirmé analytiquement.

**Cause (déterministe) :** `_sprite.SelfModulate` est un **multiply par canal** texture×couleur. Il ne peut PAS *ajouter* du bleu absent de la texture — au mieux il assombrit. `FrozenColor(White) ≈ (0.505, 0.735, 1.317)` ; pour un pixel orange `(1.0, 0.45, 0.15)` → `(0.505, 0.33, 0.20)` : R reste dominant, B plafonné par la source (≈0.2) → toujours orange/terne. Pire : le coefficient B passe de `1.4` (FrostTint seul, avant) à `1.317` (après Lerp vers FrostTarget) → le nouvel helper rend l'orange **marginalement MOINS bleu** qu'avant, à l'opposé de l'intention. Le Lerp agit sur les coefficients du multiply, jamais sur le pixel final. La méthode ne peut fonctionner que sur des sprites déjà clairs/froids (blanc/gris/bleu) ; elle est structurellement incapable de bleuir un sprite chaud.

**Remède (→ developpeur) :** un multiply ne conviendra jamais. Options : (a) shader `canvas_item` qui lerpe la couleur du pixel *final* vers un bleu cible (`mix(tex.rgb, frostBlue, k)`) ; (b) overlay d'un sprite/Modulate additif bleu par-dessus ; (c) `CanvasItemMaterial` en mode teinte. `src/Entities/Enemies/EnemyBase.cs:41-42` (`FrozenColor`) et son application L111 / L240.

### Non-régression : **OK**
Aucun script error / NullRef C# en console (seul bruit de fermeture moteur sur kill forcé). HUD (barre XP, timer, HP, compteur noyaux), montée de niveau (écran de cartes, clic applique), spawn/combat OK. Perf non dégradée à l'œil (nuée + voile + biome).

**Verdict global : Fix 2 PASS / Fix 1 FAIL.** Ne pas expédier Fix 1 en l'état : sur un sprite chaud il n'atteint pas le bleu franc visé (et régresse très légèrement la composante bleue). À renvoyer au developpeur pour une approche non-multiply.

---

## Refonte VFX Voile de Givre + ennemis gelés + réticule contouré — 2026-07-05

**Testeur :** game-tester (agent Claude). **Moteur :** Godot 4.7.stable.mono, D3D12. **Build :** OK (0 warn / 0 err).
**Verdict : PASS.** Working tree non commité (3 changements) validé visuellement, biome Givre Cryogénique.

**Méthode :** scaffolding temporaire dans `GameManager.RegisterPlayer` (grant `frost_veil` + `vector_lance` au démarrage), REVERTÉ — arbre git propre hors ce fichier. Captures PyAutoGUI (joueur stationnaire, souris fixée à un offset pour la visée), zooms comparatifs ennemi intérieur/extérieur du voile.

**1. VFX brume de froid (frost_veil) — OK.** Rendu = zone de froid lisible : liseré glacé (portée ~150), nappes de brume bleutée translucide + lueur froide douce au centre, particules de givre. Le joueur reste bien visible, pas de blob saturé (glow doux, non clampé au blanc). Lit comme un nuage/zone de froid, pas un simple cercle sec — le liseré reste l'élément le plus net (intention assumée « marquer la portée »), la brume volumétrique est présente mais subtile en frame statique. Pas de crash.

**2. Ennemis gelés — OK (empirique).** Comparaison même type (crawler orange) : vif orange HORS voile, nettement assombri/refroidi + reflet givré bleu-blanc DANS le voile ; revient normal en sortant. NB : `FrostTint (0.55,0.8,1.3)` est un *multiply* → sur sprite orange le rendu « refroidi/terni » plutôt que cyan saturé (orange × bleu = terne, attendu). L'effet reste clairement perceptible et communique « affecté ». Revue code : `_baseSelfModulate` porte la teinte élite (ou blanc), `FrostTint` se multiplie par-dessus sur `SelfModulate` ; HitFlash agit sur `Modulate` du corps → pas d'écrasement, composition correcte. Bascule au seul changement d'état.

**3. Réticule contouré (vector_lance) — OK.** Triangle blanc + contour sombre (triangle noir légèrement plus grand dessous) nettement contrasté et lisible sur sol clair bleuté. Corrige la réserve cosmétique notée le 2026-07-04.

**Non-régression — OK.** Pas d'erreur C# / NullRef / exception console (uniquement le bruit de teardown Godot au forçage de sortie : RID leaks, unreferenced static strings). Slow partagé `ApplySlow` (donc rendu gelé de la Lance Cryo de base) validé indirectement via frost_veil qui emprunte le même chemin. Level-up, XP, timer, HUD OK.

**Réserve mineure (non bloquante) :** teinte gelée peu « bleue » sur ennemis à dominante chaude (limite du multiply) — à arbitrer avec le DA si un bleu plus franc est souhaité (piste : lerp vers FrostTint plutôt que multiply pur). Brume volumétrique un peu discrète en statique (correcte en mouvement, nappes dérivantes).

---

## Refonte VISÉE Lance Vectorielle (commit `2adec5d`) — 2026-07-04

**Testeur :** game-tester (agent Claude). **Moteur :** Godot 4.7.stable.mono, D3D12. **Build :** OK (0 warn / 0 err).
**Verdict : PASS.**

**Méthode :** scaffolding temporaire dans `GameManager.RegisterPlayer` (grant `vector_lance` L5 au démarrage), REVERTÉ — arbre git propre (hors ce fichier). Captures empiriques via script PyAutoGUI (souris fixée à un offset du joueur centré caméra, joueur stationnaire ou en déplacement opposé).

**1. Visée souris (empirique) — OK.** Souris à droite → réticule (petit triangle) à droite du joueur, pointant à droite ; souris en haut → réticule au-dessus, pointant en haut. Le réticule suit le curseur, pas l'ennemi le plus proche ni la direction de déplacement.

**2. Découplage déplacement/visée (empirique, critique) — OK.** Maintien flèche GAUCHE (joueur se déplace à gauche) + souris à DROITE → muzzle flash doré + impacts de la Lance sur les ennemis à DROITE, réticule à droite. Le tir suit la SOURIS, pas le déplacement. Confirmé sur capture `d2_move_left_aim_right`.

**3. Réticule (présence/tinte) — OK.** Affiché uniquement quand `vector_lance`/`vector_beam` équipé (`UpdateAimIndicator` teste `InventorySystem.WeaponLevels`). Absence sans arme dirigée vérifiée par revue de code (condition `directed` sans ambiguïté ; le réticule n'apparaît en jeu qu'après le grant de la Lance par le scaffold). Tinte : `Color = _characterTint` posé par `ApplyCharacterVisual` via `RegisterPlayer` (ligne 65) AVANT `BuildAimIndicator` (ligne 86) → réticule bien teinté à l'identité du perso (rendu blanchâtre = tinte claire du perso par défaut, normal).

**4. Manette / stick droit — OK (revue de code, pas de manette dispo).** `UpdateAim` lit `Input.GetJoyAxis(pads[0], JoyAxis.RightX/RightY)` du 1er joypad connecté, applique la deadzone 0.35 (`AimStickDeadzone`), passe `_gamepadAim=true` quand le stick dépasse la zone morte et repasse en mode souris dès que la souris bouge (>1 px). Logique de bascule correcte ; conserve la dernière visée quand aucun périphérique n'est actionné.

**5. Rayon Vecteur (fusion `vector_beam`) — couvert.** `VectorBeam` consomme le même `Player.AimDirection` ; le réticule s'affiche aussi pour `vector_beam` (condition `UpdateAimIndicator`). Non re-testé empiriquement séparément (même source de vérité).

**6. Non-régression — OK.** Pas de crash ni erreur console sur ~3 lancements. Armes auto-visées (impulse_cannon vu tirer sur l'ennemi le plus proche pendant que la Lance visait la souris) non affectées. Build 0 err ; 87 tests déjà validés (logique de visée sans dépendance testable, non couverte par xUnit).

**Réserve mineure (non bloquante) :** aucune. Suggestion cosmétique optionnelle : le réticule blanchâtre par défaut est peu contrasté sur le sol clair — à surveiller côté DA si feedback joueur.

---

## Fusion « Voile de Givre » (frost_veil) — 2026-07-04

**Testeur :** game-tester (agent Claude). **Moteur :** Godot 4.7.stable.mono, D3D12.
**Verdict : PASS.**

**1. Câblage (vérif statique) — OK, 100 %.**
- `weapons.json` : `cryo_lance` porte `fusionRequires.passive=reinforced_plating` + `fusionResult=frost_veil` ; entrée `fusions[].frost_veil` complète (weapon cryo_lance / weaponLevel 5 / passive reinforced_plating, `replaces=cryo_lance`, `damagePerTick 8.4`, `tickInterval 0.2`, `radius 150`, `slowMult 0.55`).
- `LevelUpSystem.AllFusionIds` ✓ · `InventorySystem.WeaponScenePaths → FrostVeil.tscn` ✓ · `Codex` (TAG_FUSION + `IconById → ui_icon_frost_veil.png`) ✓ · loc `WPN_FROST_VEIL_NAME/_DESC` EN/FR/ES présentes et cohérentes ✓ · `FrostVeil.tscn` + icône `.import` commités ✓.
- `CanFuse`/`ApplyFusion` sont génériques et lisent correctement `requires` : la carte se propose bien à cryo_lance L5 + reinforced_plating ≥1 ; `ApplyFusion` retire cryo_lance (QueueFree + WeaponLevels.Remove) et instancie frost_veil au slot. Cohérence `FrostVeil.cs` ↔ JSON : `Damage = Dps(42)×TickInterval(0.2) = 8.4`, radius 150, slow 0.55 → OK.

**2. Comportement en jeu (scaffolding temporaire dans `GameManager`, REVERTÉ).**
Forcé cryo_lance L5 + reinforced_plating + `ApplyFusion("frost_veil")` au démarrage. Observé (capture 35 s, LV 5) :
- Aura **continue et centrée sur le joueur** : anneau Line2D glacé (~150 px) + halo PointLight2D bleu/violet + particules de givre.
- **Dégâts de zone** actifs : flashs de hit sur le cluster d'ennemis au bord/dans l'aura, ennemis engluées au contour (slow réappliqué à chaque tick, code `ApplySlow(0.55, 0.5)` toutes les 0.2 s conforme).
- **Joueur reste visible** au centre (VFX en ZIndex -1, confirmé visuellement).
- Aucun crash ni erreur console ; run stable jusqu'à LV 5 / timer 12:21.

**3. Non-régression.** Entrée JSON `cryo_lance` (5 niveaux, slow -40 % max) intacte ; FrostVeil est un script séparé ne modifiant pas la Lance Cryo de base. Build C# OK (0 warn / 0 err) avant et après revert.

**Scaffolding reverté — `git status` propre hormis ce fichier.**

---

## Fusion « Rayon Vecteur » (vector_beam) — 2026-07-04

**Testeur :** game-tester (agent Claude)
**Branche :** main. Working changes non commités liés à la fusion : `data/weapons.json`,
`src/Systems/InventorySystem.cs`, `src/Systems/LevelUpSystem.cs`, `src/UI/Codex.cs`,
`localization/ui.csv`, `tools/generate_weapon_icons.py`, `docs/*`, `.claude/skills/carte-projet` +
non suivis `src/Weapons/VectorBeam.cs(.uid)`, `scenes/weapons/VectorBeam.tscn`,
`assets/sprites/ui/ui_icon_vector_beam.png(.import)`.
**Moteur :** Godot 4.7.stable.mono, D3D12, AMD Radeon RX 9070.
**Build :** `dotnet build` OK — 0 erreur / 0 warning. Tests unitaires : 87/87 PASS.
**Méthode :** revue de câblage (data + C# + loc + icône) ; captures scriptées PyAutoGUI avec
scaffolding temporaire `--debug-vbeam` (monte vector_lance L5 + servo_motors + `ApplyFusion`),
**revert intégral après test** (`git checkout GameManager.cs`, `git status` propre) ; boot base
sans flag pour non-régression.

**VERDICT : PASS.**

### 1. Câblage de la fusion — PASS
- `data/weapons.json` : `vector_lance` porte `fusionRequires.passive=servo_motors` +
  `fusionResult=vector_beam` ; entrée `fusions[].vector_beam` complète (requires weapon
  vector_lance / weaponLevel 5 / passive servo_motors, stats continuous_beam, `replaces=vector_lance`,
  rareté epic).
- `LevelUpSystem.AllFusionIds` contient `vector_beam`. L'offre de carte est data-driven
  (`GetFusionCards` → `CanFuse`) et la règle de fusion forcée (`UpdatePendingFusion` +
  `PickCards`, forcée si `currentLevel > lastFusionAvailableLevel + 1`) est générique — donc
  identique aux 7 fusions déjà validées. `CanFuse("vector_beam")` a renvoyé **True** en jeu.
- `InventorySystem.WeaponScenePaths` mappe `vector_beam → res://scenes/weapons/VectorBeam.tscn`.
  `ApplyFusion` retire `vector_lance` (QueueFree + retrait de WeaponLevels) et instancie la fusion
  dans le slot — confirmé par log `[InventorySystem] Fusion appliquée : vector_beam` et par le HUD
  qui n'affiche plus qu'un slot arme mis à jour vers l'icône beam (le HUD itère `WeaponLevels` via
  `Codex.LoadIcon`, donc le slot bascule automatiquement).
- `Codex` : fiche `vector_beam` (TAG_FUSION, icône `ui_icon_vector_beam.png`, accent Gold) +
  `IconById`. Icône présente, distincte (barre dorée à cœur clair), `.import` commité.
- Loc `WPN_VECTOR_BEAM_NAME/_DESC` présentes EN/FR/ES.

### 2. Comportement du rayon en jeu — PASS
Observé sur captures (biomes Givre puis Fournaise) :
- **Continuité** : rayon toujours affiché, jamais intermittent, part bien du joueur.
- **Orientation** : suit la direction de déplacement (bas au repos = dernière dir par défaut Down ;
  droite / haut / gauche selon les touches). Pivot **lisse** (Slerp 18×delta) capté en transitoire.
  À l'arrêt, garde la dernière direction (vérifié au repos initial).
- **Perforation / dégâts** : tue plusieurs ennemis alignés d'un coup (débris + orbes XP le long du
  segment) ; montée LV1→LV3 en ~15 s en fauchant les nuées → dégâts continus confirmés
  (11/0.13 s ≈ 85 DPS single-target, mais appliqués à TOUTE la ligne, 520 px / rayon 34 px).
- **VFX** : halo doré (Line2D large) + cœur blanc (Line2D fin) + PointLight2D au muzzle (battement
  léger). Joueur reste visible (robot + light cyan à l'origine). Pas de blob lumineux occultant.
- **Stabilité** : aucun crash, log Godot propre (aucun SCRIPT ERROR / null / exception). Beam suit
  le joueur (enfant de l'arme, pivoté chaque frame).

### 3. Non-régression — PASS
- `dotnet build` 0/0 ; 87/87 tests.
- Boot base **sans** `--debug-vbeam` : jeu démarre normalement, arme de départ impulse_cannon,
  28 ennemis chargés, aucun résidu de scaffolding, aucune erreur.
- Impulse_cannon (arme de départ) présent et actif en parallèle du beam pendant les tests.

### 4. Réserves / points d'attention (non bloquants)
- **ZIndex du beam** : `VectorBeam.cs` ne pose aucun ZIndex sur ses Line2D/PointLight2D (défaut 0,
  relatif → même plan que le joueur ZIndex=5, dessiné après le sprite joueur car enfant). En
  pratique le joueur reste lisible (rayon fin de 6 px partant du centre) et la lumière est additive,
  donc pas d'occultation gênante observée. À surveiller si le rendu du joueur doit rester
  strictement au-dessus (les FusionBlade posent des ZIndex explicites 400+). **Cosmétique.**
- Test de l'**apparition réelle de la carte de fusion** au level-up : validé par câblage +
  `CanFuse=True`, pas par une run naturelle jusqu'à vector_lance L5 + servo_motors (coûteux à
  atteindre sans scaffolding). Mécanisme strictement identique aux fusions déjà en prod.

**Scaffolding temporaire `--debug-vbeam` : REVERTÉ. `git status` ne liste plus `GameManager.cs`.**

---

## Affixes d'élite — 2026-07-04

**Testeur :** game-tester (agent Claude)
**Branche :** main (working changes non commités : `EliteAffixTable.cs`, `EliteAura.cs`, +
`EnemyBase`/`EnemySpawner`/`GraftedColossus`/`DebugHooks`/`RulesTests`).
**Moteur :** Godot 4.7.stable.mono, D3D12, AMD Radeon RX 9070.
**Build :** `dotnet build` OK — 0 erreur / 0 warning. Tests unitaires filtre `Elite` : 12/12 PASS.
**Méthode :** captures scriptées (PyAutoGUI, bot kite en cercle) sur 5 sessions :
`--force-elites --biome=neon`, rafale d'explosions neon, `--force-elites --biome=givre`,
run normale `--biome=neon` (~95 s), + revue de code des chemins d'application.

### 1. Stabilité — PASS
Aucun crash ni exception sur les 5 sessions. Logs Godot propres (aucun `SCRIPT ERROR` / null / stack).
Spawn massif d'élites (`--force-elites`, écran entier), morts en chaîne via singularity, explosions
répétées près du joueur : le jeu tourne et reste fluide. Joueur survit > 55 s en mode full-élite
(LV12), preuve que ni le spawn ni la mort des élites ne déstabilise la boucle.

### 2. Lisibilité / différenciation — PASS
Les élites sont nettement distinguables : agrandissement ×1.35 + teinte `SelfModulate` + halo
`EliteAura` pulsant (disque translucide 0.28 + liseré vif) dessiné en `ZIndex=-1` derrière le sprite.
Les 5 teintes sont différenciables sur **les deux fonds testés** :
- Blindé = bleu acier, Régénérant = vert (rendu olive/jaune-vert au blend), Explosif = orange,
  Frénétique = rouge, Vampirique = magenta/violet.
- Sur **neon** (fond magenta sombre) : le halo **Vampirique magenta** est le moins contrasté
  (proche de la couleur d'ambiance) mais reste lu grâce au liseré et à la taille.
- Sur **givre** (fond bleu clair glacé) : c'est le halo **Blindé bleu acier** qui contraste le moins ;
  toujours lisible via le liseré. Le Vampirique, lui, ressort très bien sur ce fond.
- Aucun affixe n'est masqué de façon critique sur un biome donné (le pire cas de chaque teinte a le
  meilleur cas sur l'autre fond).

### 3. Explosion (affixe Explosif) — PASS (mécanique confirmée)
Empirique : morts d'élites orange près du joueur produisent des gerbes orange denses + `HitFlash`
cyan sur le joueur (dégâts AoE encaissés). Revue de code cohérente : `TriggerEliteExplosion`
instancie `vfx_shockwave_ring.tscn` (modulate incandescent 1.6/0.5/0.3), `ScreenShake.Shake(8,0.25)`,
et `player.TakeDamage` si distance < 84 px (respecte les i-frames via `Player.TakeDamage`).
`GraftedColossus.Die()` appelle bien `TriggerEliteExplosion()` — l'affixe reste universel malgré
son `Die()` custom. Note : en nuée dense, l'anneau rouge unique se noie visuellement dans la masse
des death-bursts orange (cosmétique, pas un défaut).

### 4. Ressenti / équilibrage — PASS
Jouable même en mode full-élite : ce n'est pas un mur infranchissable. Blindés (`DamageTakenMult`
0.45 + HP ×1.7) visiblement plus longs à tuer ; Frénétiques (rouge, vitesse ×1.7) foncent nettement
plus vite et se massent au contact ; Régénérants forcent le burst. Récompense XP forte (×2.5–3) :
en `--force-elites` la montée de niveau est très rapide (LV12 en ~55 s) — attendu pour le flag debug,
sans incidence sur le mode normal.

### 5. Fréquence en mode normal — PASS
Run `--biome=neon` sans flag : à ~45 s (LV6) → 2 élites à l'écran parmi ~15 ennemis ; à ~95 s
(LV12, ~1,5 min) → 1 seul élite (Blindé) parmi une quinzaine d'ennemis normaux. Les élites
apparaissent occasionnellement, rares au début, sans jamais dominer la nuée. Conforme à la courbe
`EliteAffixTable` (Base 3 % + 2 %/min, plafond 28 %). Éligibilité correcte : gate
`data.MaxSimultaneous == 0 && !BossIds.Contains` → mini-boss et boss jamais promus.

### 6. Observations mineures (non bloquantes, pour game-designer)
- **[Cosmétique]** Halo Vampirique (magenta) sur neon et halo Blindé (bleu acier) sur givre : contraste
  le plus faible de leur session respective. Lisibles mais envisager un liseré légèrement plus opaque
  ou une teinte de halo distincte de la couleur d'ambiance du biome si l'on veut une lecture instantanée.
- **[Cosmétique]** En nuée très dense, les halos translucides voisins se fondent en une nappe lumineuse
  diffuse ; la lecture individuelle des affixes baisse (sans gêner la jouabilité).

**Verdict global : PASS.** Feature stable, lisible et jouable sur les deux biomes testés. Aucun bug
bloquant/majeur. Deux notes cosmétiques de lisibilité transmises au game-designer. Captures
temporaires supprimées après analyse — aucun artefact laissé dans le dépôt.

---

## Test ciblé — 3 nouvelles fusions d'armes (ionic_storm / solar_column / hornet_swarm) — 2026-07-03

**Testeur :** game-tester (agent Claude)
**Branche :** main (câblage 3 fusions ; build C# 0 erreur / 0 warning ; 62 tests OK annoncés)
**Moteur :** Godot 4.7.stable.mono, D3D12, AMD Radeon RX 9070.
**Méthode :** hook temporaire `--debug-fusion=<id>` (accorde les prérequis puis applique la fusion,
nuée ambiante conservée), 1 lancement fenêtré par fusion, biome varié. Kite pour amasser une nuée,
5 captures échelonnées + log console complet par run. Validation des points A→E demandés.
Logs : `docs/fusion_<id>_console.log`. Captures : `docs/fusion_<id>_0..4.png`.

### Verdict global : **3/3 PASS**. Aucun crash, aucune exception console sur les 3 runs.

| Fusion | A. Appliquée | B. Tire & tue | C. VFX lisible | D. Console propre | E. Icône |
|---|---|---|---|---|---|
| `ionic_storm`  | PASS | PASS | PASS | PASS | PASS |
| `solar_column` | PASS | PASS | PASS (réserve mineure) | PASS | PASS |
| `hornet_swarm` | PASS | PASS | PASS | PASS | PASS |

#### 1. `ionic_storm` — Tempête Ionique (biome Aether) — **PASS**
- **A.** Log : `[InventorySystem] Fusion appliquée : ionic_storm` + `[GameManager] --debug-fusion=ionic_storm appliquée.` L'arme de base tesla_coil (montée niv.5) est remplacée.
- **B/C.** `fusion_ionic_storm_2.png` : long arc d'éclair cyan partant du joueur et **chaînant** sur un chapelet d'ennemis vers la droite (nœuds visibles aux coudes), avec éclats dorés de mort sur les cibles touchées → chaînage continu opérationnel, dégâts appliqués. Aura cyan intense autour du joueur, LightningBolt bien lisible. **Le joueur (robot bleu) reste parfaitement discernable au centre — aucun blob masquant.**
- **D.** Log strictement propre (voir `docs/fusion_ionic_storm_console.log`).
- **E.** Icône présente dans l'arsenal (slot dédié, remplace tesla_coil).

#### 2. `solar_column` — Colonne Solaire (biome Fournaise) — **PASS (réserve de lisibilité mineure)**
- **A.** Log : `[InventorySystem] Fusion appliquée : solar_column` + confirmation GameManager. Remplace pyre_stream niv.5.
- **B.** `fusion_solar_column_3.png` : particules de brûlure sur ennemis proches + pluie de gemmes XP → dégâts radiaux + DoT confirmés (kills).
- **C.** `fusion_solar_column_4.png` : la **couronne de ~6 lobes de flammes (PyreFlame)** est nettement visible en anneau autour du joueur, **joueur clairement discernable au centre** (le cœur de l'anneau est sombre). Aura orange. VFX conforme au design.
  - **Réserve mineure :** au tout premier instant du pic de pulse (`fusion_solar_column_2.png`), le cœur sature en un blob jaune/blanc intense qui noie brièvement le joueur, aggravé par le biome Fournaise déjà très clair + glow WorldEnvironment. Transitoire (cooldown 0.7 s, le joueur redevient visible dès la phase « couronne »), **nettement moins sévère que l'ancien BUG-701 de la Lame à Fusion**. Non bloquant. Si l'on veut être strict sur la charte « jamais de blob masquant », piste : plafonner l'énergie de flash / réduire un poil `TextureScale 5.0` du `PointLight2D` de `SolarColumn._Ready()` (cf. correctif appliqué à FusionBlade sur BUG-701). À arbitrer par le game-designer/DA — pas d'action dev impérative.
- **D.** Log strictement propre.
- **E.** Icône étoile orange présente dans l'arsenal.

#### 3. `hornet_swarm` — Nuée de Frelons (biome Néon) — **PASS**
- **A.** Log : `[InventorySystem] Fusion appliquée : hornet_swarm` + confirmation GameManager. Remplace seeker_swarm niv.5.
- **B/C.** `fusion_hornet_swarm_2.png` et `_4.png` : salve dense de missiles chercheurs (SeekerMissile), ~7+ traînées fléchées se déployant en éventail et **chassant** vers les cibles sur tout l'écran ; convergence sur les amas d'ennemis avec éclats d'impact rouges + nombreuses gemmes XP → re-ciblage et kills confirmés. Missiles petits et très lisibles, **joueur toujours parfaitement visible, aucun blob.**
- **D.** Log strictement propre.
- **E.** Icône présente dans l'arsenal.

### Conclusion
Les 3 fusions sont fonctionnelles, câblées correctement (application, remplacement de l'arme de base,
icône, VFX attendu) et sans erreur console. Aucun bug bloquant/majeur. Seule remarque : la légère
saturation au pic de `solar_column` sur biome clair (réserve cosmétique mineure, non bloquante,
même famille que BUG-701 déjà résolu). Le hook `--debug-fusion` est temporaire et à retirer après
validation (cf. `src/Core/DebugHooks.cs` / `GameManager.ApplyFusionDebugHook`).

---

## Test ciblé — VFX Lame à Fusion v2 : distorsion de chaleur — 2026-07-03

**Testeur :** game-tester (agent Claude)
**Branche :** main (+ enrichissement VFX FusionBlade non commité : heat haze BackBufferCopy + braises)
**Moteur :** Godot 4.7.stable.mono, D3D12, AMD Radeon RX 9070.
**Objet :** valider la distorsion de chaleur en espace écran (`BackBufferCopy` CopyMode=Viewport
z=400 + `Sprite2D` shadé z=401 rééchantillonnant `hint_screen_texture` avec bruit Perlin animé,
masqué au disque), les braises (`CpuParticles2D` z=402), l'anneau à bord incandescent (z=403).

### Méthode

Lancé via le hook `--debug-fusion` (fusion appliquée d'emblée, nuée ambiante conservée). Rebuild
`dotnet build` : 0 avertissement / 0 erreur. Rafales de frames rapprochées (~0,33 s) joueur à
l'arrêt sur le motif de grille du biome Aether — la grille régulière est le révélateur idéal de
la distorsion, et les rafales détectent un éventuel clignotement du BackBufferCopy.
Captures : `docs/heat_still_0..4.png`, `docs/heat_still2_0..3.png`. Log : `docs/fusion_heat_console.log`.

### Résultat : **PASS avec 1 réserve de lisibilité** (points A/B/C/E PASS, D réserve)

**A. Distorsion visible + ordre BackBufferCopy correct — PASS (point critique levé).**
Le motif de grille du sol ET les obstacles (cristaux Aether) situés DANS le rayon sont nettement
gondolés / ondulés vs la grille droite et nette à l'extérieur du disque (flagrant sur
`heat_still_0.png` et `heat_still_3.png`). Aucun clignotement : le disque est rempli et distordu
de façon identique sur les 5 frames de la 1re rafale et les 4 de la 2e → l'ordonnancement du
`BackBufferCopy` (z absolu 400 avant le quad 401) capture bien l'écran rendu chaque frame, pas de
frame vide/noire. **Le risque d'ordre de rendu ne s'est PAS matérialisé.**

**B. Braises montantes — PASS (réserve mineure).** Des particules chaudes montantes sont présentes,
mais elles se confondent visuellement avec les étincelles d'impact des autres armes équipées
(pyre_stream/seeker) et le fill incandescent du disque. Lisibles en tant que « chaleur » mais peu
distinctes en tant que « braises » spécifiques. Cosmétique, non bloquant.

**C. Lecture « matière en fusion / chaleur » — PASS.** L'ensemble teinte chaude du disque + grille
ondulée + liseré incandescent de l'anneau + braises lit clairement comme une zone de chaleur/fusion,
bien au-delà d'un simple cercle. Objectif atteint.

**D. Lisibilité en nuée / joueur discernable — RÉSERVE (Majeur cosmétique).**
Au repos et hors pic de flash, le joueur reste discernable au centre (`heat_still_0.png`). En
revanche, en combat soutenu (ennemis en permanence dans le rayon → `FlashPulse` quasi continu),
le centre du disque devient un **blob blanc/doré très intense qui masque totalement le sprite du
joueur** (`heat_still2_0.png`, `heat_still2_1.png`, deux frames à 0,33 s → persistant, pas un pic
isolé). Hypothèse : cumul additif aura `PointLight2D` (énergie de flash 0.9) + overlay `molten_color`
du shader + **glow/bloom du WorldEnvironment**, aggravé sur le biome Aether au sol déjà clair (et
possiblement le pulse d'`overload_field` aussi équipé sur ce build de test). Voir BUG-701.

**E. Aucune erreur console (FusionBlade / shader / BackBufferCopy) — PASS.** Log parfaitement propre :
seules traces `[InventorySystem] Fusion appliquée : fusion_blade` et le print `--debug-fusion`.
Aucune erreur de compilation shader, aucun warning `hint_screen_texture`, aucune exception.

### [BUG-701] Hotspot de bloom masque le joueur pendant le flash de la Lame à Fusion — RÉSOLU (2026-07-03)
- **Sévérité :** Majeur (cosmétique / lisibilité)
- **Statut :** ✅ RÉSOLU. Correctif developpeur : aura `PointLight2D` énergie base 0.35→0.22 et TextureScale 6.0→4.5, pic de flash 0.9→0.4, contribution additive du liseré `molten_color` 0.30→0.20. Re-test game-tester `--debug-fusion --biome=aether`, combat soutenu joueur immobile dans la nuée (`docs/heatfix_still_2.png`, `docs/heatfix_still_4.png`, log `docs/fusion_heat2_console.log` propre) : le sprite joueur reste **clairement discernable** au centre du disque même flash actif, le fill est redevenu un halo laiteux doux (fini le blob blanc saturé). Non-régression OK : distorsion (grille ondulée dans le disque), anneau incandescent et braises montantes toujours bien visibles. **PASS.**
- **Contexte :** run avec `fusion_blade` active, combat soutenu (ennemis en continu dans le rayon), biome clair (Aether).
- **Reproduction :** `--debug-fusion`, amasser une nuée, rester immobile ennemis dans le rayon ~2 s.
- **Observé :** le centre du disque blanchit en un blob lumineux qui recouvre le sprite du joueur ; on perd la position exacte du joueur.
- **Attendu :** le joueur doit rester lisible même au pic de flash (juice VFX ≠ perte de lisibilité — cf. `juice-and-difficulty`).
- **Hypothèse :** additif aura flash (energy 0.9) + `molten_color` edge + bloom WorldEnvironment se cumulent au-dessus du sprite joueur. Pistes : plafonner l'énergie de flash (~0.6), réduire la contribution additive du liseré `molten_color`, ou dessiner le sprite joueur au-dessus du stack VFX (z du joueur > 403) pour qu'il ne soit jamais noyé.
- **Assigné à :** developpeur (arbitrage tuning avec game-designer sur l'intensité du flash).

---

## Test ciblé — VFX Lame à Fusion (`fusion_blade`) — 2026-07-03

**Testeur :** game-tester (agent Claude)
**Branche :** main (hash de tête `53947d5`, + correctif VFX FusionBlade non commité)
**Objet :** valider le nouveau visuel de la Lame à Fusion (anneau `Line2D` doré tournant,
aura `PointLight2D`, flash par pulse) après un bug où l'arme était invisible.
**Moteur :** Godot 4.7.stable.mono, D3D12, AMD Radeon RX 9070.

### Méthode

`fusion_blade` est une fusion (`plasma_blade` niveau 5 + passif `thermal_core`, remplace
`plasma_blade` — cf. `data/weapons.json` §fusions). Le hook existant `--debug-boss` fournit les
prérequis mais isole le boss sans nuée et n'applique pas la fusion → inadapté à un test de VFX
en jeu. J'ai donc ajouté un hook de debug **temporaire** `--debug-fusion` (miroir de
`--debug-boss`) :

- `src/Core/DebugHooks.cs` : propriété `FusionDebug` (flag `--debug-fusion`).
- `src/Core/GameManager.cs` : `ApplyFusionDebugHook()` — monte `plasma_blade` à L5, ajoute
  `thermal_core`, appelle `InventorySystem.ApplyFusion("fusion_blade")`, **sans** toucher au
  spawner (nuée ambiante conservée pour tester la lisibilité).

Build `dotnet build` : 0 avertissement / 0 erreur. Lancement
`Godot ... res://scenes/Game.tscn --debug-fusion`, kite en cercle, captures à t=8/20/35/50 s
(`docs/fusion_t08.png`, `fusion_t20.png`, `fusion_t35.png`, `fusion_t50.png`).

### Résultat : **PASS** (4/4 critères)

1. **Anneau visible et centré** — PASS. Anneau doré (rayon 130 px) net et parfaitement
   centré sur le joueur dans toutes les frames ; aura rosée de remplissage à l'intérieur
   (PointLight2D or en blend Add sur biome Fournaise). Le joueur reste au centre exact.
2. **Rotation + dégradé qui balaie + flash à l'impact** — PASS. L'arc brillant du dégradé
   occupe une position différente à chaque frame (bas-gauche en t35, haut-droite en t20, haut
   en t50) → rotation confirmée. Flash à l'impact bien visible en t50 (burst blanc/doré + width
   de l'anneau qui gonfle) quand des ennemis entrent dans le rayon.
3. **Lisibilité en nuée** — PASS. L'anneau délimite clairement la zone de frappe sans masquer
   les ennemis extérieurs ; au repos (t35) le joueur est parfaitement discernable.
   *Réserve mineure (cosmétique)* : lors d'un flash d'impact avec plusieurs ennemis empilés sur
   le joueur (t50), le burst d'aura est assez lumineux et brille brièvement par-dessus le sprite
   joueur. Reste discernable, mais si le VFX venait à être renforcé, surveiller l'énergie de
   flash (0.9) pour ne pas noyer le joueur. Non bloquant.
4. **Aucune erreur console liée à FusionBlade** — PASS. Log propre, seules traces attendues :
   `[InventorySystem] Fusion appliquée : fusion_blade` puis
   `[GameManager] --debug-fusion : fusion_blade appliquée`. Aucune exception, aucun SCRIPT ERROR.

### Note pour le développeur

Le hook `--debug-fusion` est **temporaire** et marqué comme tel dans le code (gardé derrière le
flag, aucun effet en jeu normal, parallèle exact de `--debug-boss`). Recommandation : le
**conserver** comme outil de test des fusions (utile pour valider `rail_overcharged`,
`orbital_swarm`, `overload_aegis` de la même façon) — sinon le retirer de `DebugHooks.cs` et
`GameManager.cs`. Idéalement le généraliser en `--debug-fusion=<id>` pour cibler n'importe
quelle fusion.

---

## Test Phase 3 — 2026-06-20

**Testeur :** game-tester (agent Claude)
**Hash git :** ea50a22 (docs: update CLAUDE.md and GDD.md with Phase 3 decisions)
**Commits Phase 3 testés :**
- `77c0e1d` feat: animations ennemis restants + bloom WorldEnvironment
- `4056545` feat: placeholders audio silencieux + AudioSystem WAV fallback musique
- `d8c0c52` feat: Phase 3 — sprites, audio, narratif, guide de style

---

### Résumé

La Phase 3 livre 165 sprites PNG, 5 SpriteFrames .tres, les AnimatedSprite2D intégrés dans
Player/RustSwarm/CorruptedDrone/CorruptedSentinel/GraftedColossus, l'AudioSystem avec 31 WAV
placeholders silencieux, et le WorldEnvironment glow. L'objectif du test est de valider la
présence et la correction des sprites, les transitions d'animations, l'absence d'erreurs audio,
et l'absence de régressions Phase 2.

**Résultat global :** 2 bugs identifiés (1 Majeur, 1 Mineur), aucun bug bloquant, pas de
régression Phase 2 détectable depuis le code source.

---

### Smoke test

**Compilation C# :** PASS — `--build-solutions --quit` se termine sans erreur. Les 7 AutoLoad
se chargent dans l'ordre correct (SaveManager avant MetaProgressionSystem, AudioSystem en
dernier). Aucune erreur CS dans la console.

**Démarrage sans crash :** PASS. Logs de démarrage propres :
```
[MetaProgressionSystem] 7 upgrades chargés.
[SaveManager] Sauvegarde chargée.
[MetaProgressionSystem] Prêt. Échos disponibles : 56
[InventorySystem] Arme existante enregistrée : impulse_cannon niveau 1
[EnemySpawner] 4 types d'ennemis chargés.
```

**Erreurs [AudioSystem] introuvable :** ABSENTES. Les 31 WAV placeholders sont présents et
correctement importés (fichiers .import générés). Le fallback WAV de `LoadMusic()` fonctionne.

---

### Sprites et animations — analyse statique et runtime

#### Joueur Cyborg

**Présence sprite :** PASS. `Player.tscn` référence `player_frames.tres` via
`AnimatedSprite2D`. Les 24 PNG (4 idle + 6 run_right + 6 run_down + 8 death) sont présents dans
`assets/sprites/player/`. Le `Polygon2D` cyan a bien été supprimé de la scène.

**Inspection visuelle `player_idle_01.png` :** sprite 32x32 lisible — silhouette cyborg avec
implants cyan sur fond transparent. Cohérent avec la direction artistique STYLE_GUIDE.

**Animation idle :** définie à 6 fps dans `player_frames.tres`, boucle. Autoplay "idle" dans
`Player.tscn`. L'appel `_sprite?.Play("idle")` dans `Player._Ready()` est redondant (déjà
autoplay) mais inoffensif.

**Animation run :** logique dans `Player.UpdateAnimation()` : horizontal dominant → `run_right`
avec `FlipH` pour la gauche, vertical bas → `run_down`, vertical haut → `run_down` sans flip
vertical (approximation MVP documentée dans le code — acceptable).

**flip_h direction droite/gauche :** PASS en théorie — `_sprite.FlipH = direction.X < 0f`
est correct.

**Animation death :** `HandleDeath()` appelle `_sprite.Play("death")`. L'animation est en
`loop: false` à 10 fps sur 8 frames = 0,8 s. PASS structurel.

**Clignotement < 25% HP :** logique `UpdateHpBlink()` correcte — 0,4 s ON / 0,2 s OFF,
modulate 0,6/0,6/0,6 quand OFF. Le reset à `Colors.White` quand HP > 25% est présent.

#### Ennemis

**Essaim de Rouille :** PASS. `RustSwarm.tscn` référence `rustswarm_frames.tres`. Sprites
présents (3 idle + 4 move + 5 death). Autoplay "move". FlipH sur direction vers joueur.

**Drone Corrompu :** PASS. `CorruptedDrone.tscn` référence `drone_frames.tres`. Sprites présents
(3 idle + 4 move + 4 death). Logique animation : `idle` si déviation > 30°, `move` sinon.
FlipH sur `_currentDir.X`.

**Sentinelle Corrompue :** PASS. `CorruptedSentinel.tscn` référence `sentinel_frames.tres`.
Sprites présents (4 idle + 6 move + 4 attack + 6 death). L'animation `attack` est déclenchée
dans `Shoot()`. Le signal `AnimationFinished` → retour en `idle` est connecté dans `_Ready()`.
Le `flip_h` suit la direction vers le joueur (pas la vélocité) — conforme à la décision Phase 3.

**Inspection visuelle `enemy_sentinel_idle_01.png` :** sprite 32x32 — sentinelle avec forme
angulaire caractéristique et yeux rouges. Lisible.

**Colosse Greffé :** PASS structurel. `GraftedColossus.tscn` référence `colossus_frames.tres`.
Sprites 48x48 présents (4 idle + 6 move + 5 attack + 10 death). Échelle du sprite : 48x48
laissée native (pas de scale dans le tscn — `scale = Vector2(1, 1)`). Collision shape reste
`RectangleShape2D(24, 32)` — collision plus petite que le sprite visuel : acceptable MVP.

**Inspection visuelle `enemy_colossus_idle_01.png` :** sprite 48x48 — colosse avec implants
violets `#AA44FF`. Les implants sont présents et distincts.

**Inspection `enemy_colossus_death_07.png` (frame flash libération Noyau, index 6) :** la
frame contient une tache violette sur fond clair — le flash de libération du Noyau est visible
visuellement. Cohérent avec la décision de design.

#### Projectiles

**Bullet (Canon à Impulsions) :** PASS. `Bullet.tscn` référence `weapon_bullet_impulse.png`
via `Sprite2D` (pas AnimatedSprite2D — correct, projectile statique). Le PNG est un trait orange
horizontal — distinct du Polygon2D jaune de Phase 1.

#### WorldEnvironment / Bloom

**Présence :** PASS. `Game.tscn` contient un nœud `WorldEnvironment` en premier enfant (avant
`Ground`). L'`Environment` sub-resource configure :
- `glow_enabled = true`
- `glow_levels/1 = true`, `glow_levels/2 = true`
- `glow_hdr_threshold = 0.6` (seules les couleurs très saturées brillent)
- `glow_intensity = 0.8`, `glow_strength = 1.2`
- `glow_blend_mode = 0` (valeur 0 = `Additive` dans l'enum Godot 4 — conforme CLAUDE.md)

**Eligibilité des couleurs au glow :** XpOrb `Color(0.4, 1.0, 0.2)` → composante G=1.0 > 0.6,
éligible. AetherCore `Color(0.667, 0.267, 1.0)` → composante B=1.0 > 0.6, éligible. Sol
`Color(0.102, 0.22, 0.102)` → toutes composantes < 0.6, ne brillera pas. Conforme au design.

**Note :** la validation visuelle du bloom (effet à l'écran) n'est pas vérifiable en headless.
L'inspection statique du tscn confirme la configuration correcte.

---

### Bugs trouvés

#### BUG-001 — RustSwarm : animation death coupée par QueueFree immédiat

- **Sévérité :** Majeur
- **Contexte :** `src/Entities/Enemies/RustSwarm.cs`, méthode `Die()`, ligne 43-47.
- **Reproduction :** Tuer n'importe quel Essaim de Rouille. L'animation death est lancée au
  frame 0, puis le nœud est immédiatement détruit par `base.Die()` → `QueueFree()` dans le même
  frame (ou le frame suivant).
- **Comportement observé :** Le sprite death flash pendant 1 frame maximum avant disparition.
  L'animation 5 frames à 10 fps (= 0,5 s) n'a pas le temps de se jouer.
- **Comportement attendu :** L'animation death se joue en intégralité (0,5 s) avant que le nœud
  soit détruit, comme c'est le cas pour GraftedColossus.
- **Analyse technique :** `RustSwarm.Die()` appelle `_sprite?.Play("death")` puis `base.Die()`
  immédiatement. `EnemyBase.Die()` appelle `QueueFree()` de façon synchrone. Contrairement à
  `GraftedColossus.Die()` qui réimplémente la logique inline et attend `AnimationFinished`,
  `RustSwarm.Die()` fait confiance à `base.Die()` en espérant un délai — qui n'existe pas.
  Le commentaire dans le code (`// Le QueueFree sera decale pour laisser l'animation se jouer`)
  est erroné : `QueueFree()` est immédiat.
- **Hypothèse de correction :** Même pattern que `GraftedColossus` : ne pas appeler `base.Die()`
  depuis `RustSwarm.Die()`, mais connecter `AnimationFinished` pour appeler `base.Die()` après
  l'animation. Alternativement, ajouter un `AnimationFinished` handler dans `EnemyBase` et un
  flag `_playingDeathAnim`. Le problème existe probablement aussi pour `CorruptedDrone.Die()` et
  `CorruptedSentinel.Die()` (même pattern — voir BUG-002).
- **Assigné à :** developpeur

#### BUG-002 — CorruptedDrone / CorruptedSentinel : même bug animation death que RustSwarm

- **Sévérité :** Mineur (l'animation death des drones/sentinelles est plus courte et moins
  remarquable visuellement ; à 10 fps sur 4-6 frames, la coupure à 1 frame est moins perceptible
  qu'une animation de 5 frames d'essaim)
- **Contexte :** `src/Entities/Enemies/CorruptedDrone.cs` (Die() ligne 69-76) et
  `src/Entities/Enemies/CorruptedSentinel.cs` (Die() ligne 120-128).
- **Reproduction :** Tuer un Drone Corrompu ou une Sentinelle.
- **Comportement observé :** Même que BUG-001 — `_sprite.Play("death")` suivi de `base.Die()` →
  `QueueFree()` immédiat. L'animation death (4 frames drone, 6 frames sentinelle) est coupée.
- **Note pour la Sentinelle :** La mort de la Sentinelle appelle `_sprite.Play("death")` puis
  `base.Die()`. Or `EnemyBase.Die()` appelle `QueueFree()` immédiatement — le signal
  `AnimationFinished` (qui est connecté dans `_Ready()`) ne sera jamais déclenché puisque le
  nœud sera détruit.
- **Comportement attendu :** Animations death jouées en intégralité.
- **Assigné à :** developpeur

---

### Régressions Phase 2

**XP / Level-up :** Aucune régression détectée — les systèmes `XpSystem`, `LevelUpSystem`,
`InventorySystem` sont inchangés. Les modifications Phase 3 sont circonscrites aux fichiers
d'entités et audio.

**Meta / RunEndScreen / Hub :** Aucune modification dans ces fichiers. Pas de régression.

**Armes (tir instantané frame 0) :** Le fix `base._Ready()` en dernière ligne des sous-classes
d'armes reste présent dans tous les fichiers d'armes (vérifié lors des sessions précédentes).

**AudioSystem :** Le système est nouveau en Phase 3. Le fallback WAV est implémenté correctement
dans `LoadMusic()`. Les SFX sont chargés en WAV en priorité. Aucune erreur de chargement en
runtime (confirmé par les logs de démarrage).

**Reset inter-run :** `GameManager.RegisterPlayer()` appelle bien les 3 Reset() avant
l'enregistrement de l'arme. Aucune modification dans ce fichier depuis les bugfixes Phase 2.

---

### Points positifs

1. **Compilation propre :** Zéro erreur C# sur 165 PNG + 5 SpriteFrames .tres ajoutés +
   AudioSystem complet. Les qualifications `Godot.FileAccess` sont respectées.

2. **AudioSystem robuste :** La gestion null-safe sur les fichiers manquants et le double
   fallback OGG → WAV sont bien implémentés. Plus aucune erreur `[AudioSystem] introuvable` en
   démarrage.

3. **GraftedColossus.Die() correctement implémenté :** Le pattern de mort différée via
   `AnimationFinished` est le seul ennemi correctement implémenté pour les animations death.
   Le spawn d'AetherCore sur `AnimationFinished` (après 10 frames à 10 fps = 1 s) est élégant.

4. **Bloom WorldEnvironment bien configuré :** La configuration statique dans `Game.tscn` est
   correcte — threshold 0.6 filtrera les zones sombres, les couleurs Aether (G=1.0 ou B=1.0)
   seront éligibles.

5. **Identité visuelle cohérente :** Le Cyborg en 32x32 avec implants cyan, les ennemis avec
   leurs palettes distinctives (rouge-rouille essaim, orange-brun drone, rouge sentinelle,
   gris-rouge colosse avec implants violets), et le projectile orange du Canon — l'opposition
   "matière morte / énergie vivante" du STYLE_GUIDE est lisible sur les sprites.

6. **Sprite Bullet remplace le Polygon2D jaune :** `Bullet.tscn` utilise `Sprite2D` avec
   `weapon_bullet_impulse.png` (trait orange). La transition Phase 1 → Phase 3 sur les
   projectiles est complète.

---

### Briefing developpeur — BUG-001 et BUG-002

**Problème :** `RustSwarm.Die()`, `CorruptedDrone.Die()`, `CorruptedSentinel.Die()` appellent
`_sprite.Play("death")` puis immédiatement `base.Die()` qui appelle `QueueFree()`. L'animation
death est donc coupée au frame 0 (ou frame suivant).

**Référence :** `GraftedColossus.Die()` est le seul ennemi qui gère correctement ce cas — il
n'appelle pas `base.Die()` et attend le signal `AnimationFinished`.

**Fichiers à modifier :**
- `src/Entities/Enemies/RustSwarm.cs`
- `src/Entities/Enemies/CorruptedDrone.cs`
- `src/Entities/Enemies/CorruptedSentinel.cs`
- Potentiellement `src/Entities/Enemies/EnemyBase.cs` si on veut centraliser la logique

**Solution recommandée (option A — per-class, minimal diff) :**
Dans chaque classe, connecter `_sprite.AnimationFinished` dans `_Ready()` et y appeler
`base.Die()` si l'animation est "death". Ne pas appeler `base.Die()` directement dans `Die()`.

**Solution recommandée (option B — EnemyBase, plus propre) :**
Ajouter dans `EnemyBase` :
- un champ `protected bool _playingDeathAnim = false`
- dans `Die()` : si l'`AnimatedSprite2D` existe et a une animation "death", lancer l'anim,
  setter le flag, et différer `QueueFree()` + signaux au signal `AnimationFinished`
- Les sous-classes n'ont plus à surcharger `Die()` pour l'animation

**Attention `CorruptedSentinel` :** Elle connecte déjà `AnimationFinished` pour le retour idle
après "attack". Le handler `OnAnimationFinished` devra gérer les deux cas ("attack" → idle,
"death" → base.Die()).

---

### Vérifications non réalisables sans interaction humaine

Les points suivants nécessitent un test en session interactive (IHM, fenêtre de jeu ouverte) :

- Validation visuelle du bloom à l'écran (orbes XP et Noyaux brillent-ils ?)
- Fluidité des animations en combat dense (200 ennemis)
- Animation death Sentinelle coupée — visibilité réelle au joueur (1 frame vs 6 frames à 10 fps)
- Clignotement HP < 25% — lisibilité en combat
- Sens du flip_h joueur en diagonales à 45° (direction ambigüe entre run_right et run_down)

---

## Test Arène Phase 3b — 2026-06-21 (analyse statique préliminaire)

**Testeur :** game-tester (agent Claude)
**Hash git :** dabcfed (fix: animation death ennemis coupée au frame 0 — BUG-001/002)
**Périmètre :** Arène "Sanctuaire en Ruines" — remplacement Polygon2D par GroundRenderer procédural, AetherGeyser zone de danger, 17 nouveaux tiles PNG, scène AetherGeyser.tscn.

---

### Résumé

Nouveaux systèmes intégrés : `GroundRenderer.cs` (sol 40×23 tiles + murs tuiles + décors),
`AetherGeyser.cs` (zone de danger cyclique), 17 tiles PNG, `geyser_frames.tres`, scène
`AetherGeyser.tscn`. `Game.tscn` perd ses anciens `Ground`/`WallVisual`/`Decor` Polygon2D.

**Résultat global :** 1 bug Cosmétique (BUG-003, murs V — sous-couverture masquée par overlap
H), 1 point d'attention visuelle (rotation tile mur latéral). Aucun bug bloquant ou majeur.
BUG-001/002 confirmés fixés. Aucune régression Phase 3 détectée.

---

### 1. Smoke test — PASS

**Compilation C# :** PASS. Build headless `--build-solutions --quit` terminé sans erreur.
`GroundRenderer.cs` et `AetherGeyser.cs` compilent proprement. Aucun avertissement CS.

**Structure `Game.tscn` :** PASS.
- `GroundRenderer` présent (Node2D + script `res://src/Systems/GroundRenderer.cs`).
- `Geyser01` à `(-300, -150)` et `Geyser02` à `(280, 180)` présents (instances de
  `scenes/entities/AetherGeyser.tscn`).
- Aucun nœud `Ground` (Polygon2D), `WallVisual`, ni `Decor` subsistant — nettoyage confirmé.
- `load_steps = 18` (était 16 avant Phase 3b) — cohérent avec l'ajout de 2 `ext_resource`
  (`GroundRenderer.cs` + `AetherGeyser.tscn`).

---

### 2. GroundRenderer — analyse statique

#### Grille sol

- **Dimensions :** 40×23 tiles × 32 px = 1280×736 px. CONFORME : largeur exactement égale à
  `ArenaW = 1280`. Hauteur 736 px dépasse de 8 px de chaque côté l'arène intérieure (720 px) —
  le sol déborde légèrement sous les murs, masquant tout joint entre sol et mur. Correct.
- **Point de départ :** `startX = -ArenaW/2 = -640` (bord gauche exact), `startY =
  -(GridRows*TileSize/2) = -368` (centré verticalement). La grille couvre intégralement la
  surface jouable.
- **Distribution tiles :** `r<0.60` → floor_01 (60%), `r<0.80` → floor_02 (20%), `r<0.88` →
  crack (8%), `r<0.95` → rust (7%), sinon debris (5%). Total 100%. CONFORME.

#### Murs tuiles

- **Murs horizontaux :** `hTiles = (1280 + 64) / 32 = 42`. Couverture 42×32 = 1344 px =
  `ArenaW + 2×WallThick`. CONFORME. Positions `yTop = -376`, `yBot = +376`. Aligné avec les
  `StaticBody2D` WallTop/WallBottom de la scène.
- **Murs verticaux :** `vTiles = (720 + 64) / 32 = 24`. Couverture 24×32 = 768 px vs 784 px
  requis. Manque 16 px (8 px en haut + 8 px en bas). **Voir BUG-003 ci-dessous.** Impact
  visuel nul : les tiles H chevauchent les coins et masquent intégralement le gap.
- **Distribution murs :** `r<0.78` → wall_01 (78%), `r<0.91` → wall_rust (13%), sinon
  wall_crack_aether (9%). Pas de spécification GDD pour ce ratio — acceptable MVP.

#### Décors

- **Debris :** 4× `debris_01` (ZIndex=-8), 3× `debris_metal` (ZIndex=-8). CONFORME.
- **Colonnes :** 2× `StaticBody2D` avec `CollisionLayer=1`, `CollisionMask=1`,
  `RectangleShape2D(28, 28)`, offset `(0, +16)` Y. CONFORME à la décision CLAUDE.md.
- **Sprite colonne :** `Sprite2D` avec `ZIndex=1` relatif au parent StaticBody2D. CONFORME
  (colonnes=+1).
- **Zone safe centrale :** rayon 100 px autour de (0,0), margin murs 80 px, 30 tentatives max.
  CONFORME.
- **ZIndex résumé :** sol=-10, murs=-9, décors=-8, colonnes ZIndex=+1 relatif. CONFORME.

#### Budget de nœuds

920 Sprite2D sol + 84 Sprite2D murs H + 48 Sprite2D murs V + 7 Sprite2D décors = **1059
Sprite2D statiques** + 2 StaticBody2D colonnes (3 nœuds enfants chacun) = **~1065 nœuds
total**. Tous statiques, aucune physique ou logique par frame. Risque performance faible —
les Sprite2D statiques sont batchés par Godot. Création en une seule frame dans `_Ready()` :
à surveiller si lag constaté à l'entrée de scène. Acceptable MVP.

---

### 3. AetherGeyser — analyse statique — PASS

- **Cycle :** démarre inactif (`_isActive=false`, `_cycleTimer=InactiveDuration=3f`). CONFORME.
  Bascule sur `_cycleTimer <= 0` → réaffecte durée (`2f` si actif, `3f` si inactif). CONFORME.
- **Dégâts :** `DamagePersecond * delta * (1f - stats.DamageReduction)`. Formule correcte.
  `DamageReduction` accédé sur `_playerInZone.Stats`. Pas de valeur hardcodée. CONFORME.
- **Guard `IsInstanceValid` :** présent ligne 51 —
  `if (_isActive && _playerInZone != null && IsInstanceValid(_playerInZone))`. CONFORME.
- **Détection joueur :** `BodyEntered` / `BodyExited` sur `Area2D`. `collision_mask = 1` cible
  le layer joueur. `collision_layer = 0` correct pour un récepteur Area2D passif. CONFORME.
- **`geyser_frames.tres` :** animation `idle` : 1 frame (tile_geyser_01), loop, 4 fps.
  Animation `active` : 2 frames (tile_geyser_02 + tile_geyser_03), loop, 4 fps. CONFORME à
  la spec CLAUDE.md (idle=frame1, active=frames2+3 à 4 fps).
- **Autoplay `idle` dans `AetherGeyser.tscn` :** `animation = &"idle"`, `autoplay = "idle"`.
  L'appel `_sprite?.Play("idle")` dans `_Ready()` est redondant mais inoffensif.

---

### 4. Cohérence visuelle — présence des tiles

Tous les tiles référencés dans `GroundRenderer.cs` et `geyser_frames.tres` sont présents :

| Fichier | Présent |
|---|---|
| `tile_floor_01.png` | PASS |
| `tile_floor_02.png` | PASS |
| `tile_floor_crack.png` | PASS |
| `tile_floor_rust.png` | PASS |
| `tile_floor_debris.png` | PASS |
| `tile_wall_01.png` | PASS |
| `tile_wall_rust.png` | PASS |
| `tile_wall_crack_aether.png` | PASS |
| `tile_debris_01.png` | PASS |
| `tile_debris_metal.png` | PASS |
| `tile_column.png` | PASS |
| `tile_aether_geyser_01.png` | PASS |
| `tile_aether_geyser_02.png` | PASS |
| `tile_aether_geyser_03.png` | PASS |
| `tile_rust_pool_01.png` | Présent — non référencé dans GroundRenderer |
| `tile_rust_pool_02.png` | Présent — non référencé dans GroundRenderer |
| `tile_tech_pillar.png` | Présent — non référencé dans GroundRenderer |

`tile_rust_pool_01/02.png` et `tile_tech_pillar.png` sont des tiles orphelins — présents sur
disque mais pas intégrés dans `GroundRenderer.cs`. Aucune conséquence runtime. Voir briefing
game-designer ci-dessous.

---

### 5. Régressions Phase 3 — confirmation — PASS

**BUG-001/002 animation death :** le commit `dabcfed` fixe les trois ennemis concernés.
Vérification du code source :

- `RustSwarm.Die()` : `_sprite?.Play("death")` sans appel à `base.Die()`. Signal
  `AnimationFinished` → `OnAnimationFinished()` → `QueueFree()` si animation == "death".
  **FIXE.**
- `CorruptedDrone.Die()` : même pattern. `AnimationFinished` → `QueueFree()`. **FIXE.**
- `CorruptedSentinel.Die()` : `OnAnimationFinished()` gère les deux cas — "attack" → idle,
  "death" → `QueueFree()`. **FIXE.**

Les trois ennemis utilisent le même pattern que `GraftedColossus` : pas d'appel à `base.Die()`
dans `Die()`, destruction différée à `AnimationFinished`. PASS.

**Sprites joueur et ennemis :** `Player.tscn`, `RustSwarm.tscn`, `CorruptedDrone.tscn`,
`CorruptedSentinel.tscn`, `GraftedColossus.tscn` inchangés. Références SpriteFrames intactes.
PASS.

**WorldEnvironment glow :** toujours présent en premier enfant de `Game.tscn`. PASS.

**Systèmes meta/XP/Level-up/Weapons :** aucune modification. PASS.

---

### 6. Bugs trouvés

#### BUG-003 — Murs V : sous-couverture de 16 px aux extrémités latérales

- **Sévérité :** Cosmétique
- **Contexte :** `src/Systems/GroundRenderer.cs`, méthode `BuildWalls()`, ligne 87.
- **Reproduction :** Calcul statique — `vTiles = (720 + 64) / 32 = 784 / 32 = 24` (division
  entière tronquée). 24 tiles couvrent 768 px ; la zone physique totale requiert 784 px.
- **Observé :** Gap de 8 px non couverts en haut et 8 px en bas de chaque mur latéral.
- **Attendu :** Couverture complète 784 px (25 tiles, ou `Math.Ceiling` du calcul).
- **Impact réel :** Nul à l'écran. Les tiles H (`yTop=-376`, `yBot=+376`) chevauchent les
  coins et masquent intégralement les 8 px de gap de chaque côté. Non visible en gameplay.
- **Hypothèse de correction :**
  `int vTiles = (int)Math.Ceiling((double)(ArenaH + 2 * WallThick) / TileSize);` // = 25
  avec `int vStartY = -(vTiles * TileSize / 2);` // = -400.
  Alternative : hardcoder `vTiles = 25` — cohérent avec le débordement voulu des tiles H.
- **Assigné à :** developpeur (priorité basse)

---

### 7. Points d'attention visuelle (non-bugs)

**Rotation 90° tiles murs latéraux :** `WallSprite()` applique `RotationDegrees = 90f` pour
les murs V. Si `tile_wall_01.png` (et variantes rust/crack) n'est pas symétrique
horizontalement, la rotation produira un rendu avec ombre portée ou motif inversé. A valider
visuellement en runtime. Si jugé incorrect : produire des variantes `tile_wall_vertical_*` ou
utiliser `FlipH` combiné à la rotation.

**Tiles orphelins :** `tile_rust_pool_01/02.png` et `tile_tech_pillar.png` préparés mais non
intégrés dans `GroundRenderer.cs`. A clarifier (voir briefing game-designer ci-dessous).

**Budget nœuds — instantiation en `_Ready()` :** ~1065 nœuds créés en une frame. Acceptable
sur le hardware de dev. A surveiller si lag constaté à l'entrée de `Game.tscn` — envisager
étalement via `CallDeferred` ou coroutine si nécessaire en Phase 4.

**Colonnes vs clamp joueur :** les colonnes (`CollisionLayer=1`, `CollisionMask=1`) créent de
nouveaux obstacles physiques. Vérifier en runtime qu'un joueur ne peut pas être coincé entre
une colonne et un mur (la résolution de collision Godot devrait gérer, mais le point mérite
validation interactive).

**Positionnement des geysers :** `(-300, -150)` et `(280, 180)` — zones médianes de l'arène.
A valider que ces positions sont suffisamment visibles sans être injustes (notamment Geyser02
à `(280, 180)` potentiellement dans une zone de spawn dense en fin de run).

---

### Briefing developpeur — BUG-003

**Fichier :** `src/Systems/GroundRenderer.cs`, méthode `BuildWalls()`, ligne 87.

Le calcul `int vTiles = (ArenaH + 2 * WallThick) / TileSize` effectue une division entière
qui tronque. `784 / 32 = 24`, alors que la couverture exacte requiert 24,5 tiles. Gap
résultant : 8 px en haut + 8 px en bas des murs V. Impact réel nul (masqué par overlap H).
Correction optionnelle, priorité basse :

```csharp
int vTiles = (int)Math.Ceiling((double)(ArenaH + 2 * WallThick) / TileSize); // 25
int vStartY = -(vTiles * TileSize / 2); // -400
```

---

### Briefing game-designer — tiles orphelins

`tile_rust_pool_01.png`, `tile_rust_pool_02.png`, `tile_tech_pillar.png` sont livrés dans
`assets/sprites/tileset/` mais pas référencés dans `GroundRenderer.cs`. Confirmer l'intention :

- **Option A** : Assets pour `BuildDecor()` Phase 3c — pools de rouille aléatoires au sol
  et piliers technologiques supplémentaires. Briefer le `developpeur` pour les intégrer.
- **Option B** : Assets réservés pour une TileMap Godot native (Phase 4). Documenter dans
  `CLAUDE.md` pour éviter toute confusion lors des prochaines sessions.

Note : les `tile_rust_pool` ne sont pas liés aux geysers Aether (absents de
`geyser_frames.tres`) — ce sont des décors de sol distincts.

---

### Vérifications non réalisables sans session interactive

- Rendu visuel de la rotation 90° des tiles murs latéraux (aspect à l'écran).
- Cohérence des tiles sol avec le glow WorldEnvironment (teintes sombres ne doivent pas briller).
- Dégâts AetherGeyser perçus en jeu : transition idle→active et dégâts effectivement appliqués
  quand le joueur stationne sur le geyser actif.
- Comportement du clamp joueur `ClampToArena()` face aux nouvelles colonnes collidables.
- Positionnement effectif des 2 geysers — accessibilité et lisibilité en gameplay réel.

## Test Arène Phase 3b — 2026-06-21

**Testeur :** game-tester (agent Claude)
**Hash git :** dabcfed (fix: animation death ennemis coupée au frame 0 — BUG-001/002)
**Périmètre :** `GroundRenderer.cs`, `AetherGeyser.cs`, `AetherGeyser.tscn`, `geyser_frames.tres`, 17 tiles PNG, `Game.tscn` modifié.

---

### Résumé

La Phase 3b livre l'arène graphique "Sanctuaire en Ruines" : sol tuilé procédural (40×23), murs
visuels tuilés, décors statiques (débris, colonnes collidables), flaques de Rouille Vivante,
piliers tech et geysers Aether (zone de dégâts 5 HP/s, cycle 2s actif / 3s inactif). Résultat
global : 1 bug cosmétique (BUG-003) corrigé en session, aucun bug bloquant, aucune régression.

---

### Smoke test — PASS

Compilation `--build-solutions --quit` sans erreur C#. Structure `Game.tscn` conforme : `Ground`
supprimé, 4 `WallVisual` supprimés, `GroundRenderer` + `Geyser01`/`Geyser02` présents.
`load_steps = 18` (était 16).

---

### GroundRenderer — PASS

- Sol 40×23 tiles, `startX=-640`, `startY=-368` — couverture exacte 1280×736 px.
- Distribution tiles : 60% floor_01 / 20% floor_02 / 8% crack / 7% rust / 5% debris. Conforme.
- Murs horizontaux : 42 tiles × 1344 px. Conforme.
- Murs verticaux : **25 tiles** (corrigé BUG-003 en session), couverture 800 px.
- Décors : 4 debris_01, 3 debris_metal, 3 flaques Rouille (zIndex=-9), 1 pilier tech, 2 colonnes
  (CollisionLayer=1, CollisionMask=1, hitbox 28×28 offset +16Y).
- Budget nœuds : ~1065 Sprite2D statiques. Risque performance faible (aucune logique par frame).

### AetherGeyser — PASS

- Cycle inactif 3s / actif 2s. Dégâts : `5f × delta × (1 - DamageReduction)`. Conforme.
- Guard `IsInstanceValid(_playerInZone)` présent. `Area2D.collision_mask=1`. Conforme.
- `geyser_frames.tres` : `idle` 1 frame (geyser_01) / `active` 2 frames (geyser_02+03), 4 fps.

### Présence tiles — PASS intégral

17 tiles vérifiés présents. Tiles orphelins (`tile_rust_pool_01/02`, `tile_tech_pillar`) intégrés
dans `GroundRenderer.BuildDecor()` en session (corrigé avant commit).

### Régressions Phase 3 — PASS

BUG-001/002 (animation death) confirmés fixés. Sprites ennemis/joueur inchangés.
Systèmes XP/Level-up/Weapons/Meta inchangés.

---

### Bugs trouvés

#### BUG-003 — Murs verticaux : sous-couverture de 16 px aux extrémités

- **Sévérité :** Cosmétique (impact visuel nul — tiles H couvrent les coins)
- **Contexte :** `src/Systems/GroundRenderer.cs`, `BuildWalls()`.
- **Cause :** `(784 / 32)` division entière tronquée = 24 tiles (768 px) au lieu de 25 (800 px).
- **Correction appliquée :** `vTiles = 25` hardcodé. Résolu en session.

---

### Points d'attention (non-bugs)

- **Rotation 90° murs V :** rendu à valider visuellement — si `tile_wall_01` n'est pas symétrique
  horizontalement, la rotation peut produire un effet inattendu. Envisager variantes verticales
  dédiées si jugé inacceptable à l'écran.
- **Colonnes vs clamp joueur :** vérifier en session interactive qu'un joueur ne peut pas être
  coincé entre une colonne et un mur latéral.
- **Positionnement geysers :** `(-300,-150)` et `(280,180)` — à valider que la pression n'est
  pas injuste en fin de run (zone haute densité ennemis + geyser actif).
- **Instantiation ~1065 nœuds en une frame :** surveiller un lag à l'entrée de `Game.tscn` sur
  hardware modeste. Envisager `CallDeferred` si nécessaire en Phase 4.

---

## Test Phase 4 P0 — 2026-06-21

**Testeur :** game-tester (agent Claude)
**Hash git :** b48c0a7 (docs: GDD §12/§16 + CLAUDE.md — polish UI Phase 3c et FusionFlash) — Phase 4 P0 non encore commitée, working tree modifié.
**Commits Phase 4 testés :** working tree (fichiers non commités)
**Périmètre :** Arène agrandie 1920×1216, obstacles collidables (5 piliers + 2 épaves), lueur PointLight2D geysers, death burst GPUParticles2D ennemis, limites Camera2D, geysers repositionnés.
**Device GPU :** AMD Radeon RX 9070 (D3D12 Forward+)

---

### Résumé exécutif

Deux sessions de lancement successives ont été effectuées.

**Session 1 (pré-import)** : 3 erreurs "No loader found" pour les textures obstacles (`tile_pillar_stone.png`, `tile_pillar_stone_shadow.png`, `tile_wreck_machine.png`) — voir BUG-004. Le fallback Polygon2D coloured s'est activé correctement. Jeu non bloqué.

**Session 2 (post-import headless)** : Zéro erreur texture. Démarrage propre. Une run complète observée (T=46s, K=17, Échos=13), persistance sauvegarde confirmée. Architecture Phase 4 P0 fonctionnelle.

**Verdict P0 :** VALIDÉ AVEC RÉSERVE. La mécanique centrale est opérationnelle. BUG-004 (`.import` manquants) est bloquant pour tout développeur/testeur qui clone le repo sans passer par l'éditeur Godot au préalable — il doit être corrigé avant tout merge ou partage du build.

---

### 1. Smoke test — PASS (après import)

**Logs de démarrage session 2 (référence) :**
```
Godot Engine v4.7.stable.mono.official.5b4e0cb0f
D3D12 12_0 - Forward+ - Using Device #0: AMD - AMD Radeon RX 9070
[MetaProgressionSystem] 7 upgrades chargés.
[SaveManager] Sauvegarde chargée.
[MetaProgressionSystem] Prêt. Échos disponibles : 61
[InventorySystem] Arme existante enregistrée : impulse_cannon niveau 1
[EnemySpawner] 4 types d'ennemis chargés.
```

Aucune erreur de texture, aucun crash. Les 7 AutoLoad initialisés dans le bon ordre. PASS.

**Run observée :**
```
Player died.
[SaveManager] Sauvegarde écrite (182 octets).
[MetaProgressionSystem] +13 Échos. Total disponible : 74
[RunStatsTracker] Fin de run — outcome=death, T=46s, K=17, N=0, Échos=13
```

Calcul Échos vérifié : `floor(46/20) + floor(17/10) + (0×5) + 10 = 2 + 1 + 0 + 10 = 13`. CONFORME à la formule GDD §9.2.

---

### Scénario 1 — Démarrage et lisibilité arène

**Arène agrandie 1920×1216 :** PASS (analyse statique).
- `Constants.ArenaWidth = 1920`, `Constants.ArenaHeight = 1216`. CONFORME.
- `GroundRenderer` : `GridCols = 60`, `GridRows = 38`. Couverture sol 1920×1216 px exacte. CONFORME.
- Murs `Game.tscn` : WallTop/Bottom = `Vector2(1984, 32)`, WallLeft/Right = `Vector2(32, 1280)`. CONFORME — 1984 = 1920 + 2×32 (débordement 32 px de chaque côté).
- `EnemySpawner.RandomSpawnPosition()` utilise déjà `Constants.ArenaWidth/Height` — mise à l'échelle automatique sans modification. CONFORME.
- `Player.ClampToArena()` utilise déjà `Constants.ArenaWidth/Height` — CONFORME.
- Distribution sol Phase 4 : 72% / 18% / 5% / 4% / 1%. CONFORME à `CLAUDE.md`.

**Camera2D limites :** PASS (analyse statique + `Player.tscn` vérifié).
- `limit_left=-960`, `limit_right=960`, `limit_top=-608`, `limit_bottom=608`.
- Calcul : ±(1920/2) = ±960 en X, ±(1216/2) = ±608 en Y. Mathématiquement correct.
- Viewport 1280×720 centré en (0,0) : le joueur au centre voit X∈[-640,+640] ⊂ [-960,+960]. Les limites ne seront atteintes que lors du déplacement vers les bords. CONFORME.
- Le joueur ne peut donc pas voir hors de l'arène (pas de contenu hors-arène visible). PASS.

**Note sur le mur supérieur Phase 4 :** `WallLeft/Right = Vector2(32, 1280)` — hauteur 1280 px pour couvrir l'arène 1216 + 2×32 = 1280 px. CONFORME.

---

### Scénario 2 — Obstacles collidables

**Présence obstacles dans le code :** PASS (analyse statique `GroundRenderer.BuildObstacles()`).
- 5 piliers `BuildPillar()` : `StaticBody2D` + `CapsuleShape2D(radius=12, height=0)` offset Y=+16, `CollisionLayer=1`, `CollisionMask=1`. CONFORME à la spec CLAUDE.md.
- 2 épaves `BuildWreck()` : `StaticBody2D` + `RectangleShape2D(56, 24)`, `CollisionLayer=1`, `CollisionMask=1`. CONFORME.
- Fallback Polygon2D en cas de sprite absent — bien implémenté avec `TryLoadTexture()`.

**Zone interdite spawn obstacles :** PASS (analyse statique `SafeRandPosObstacle()`).
- Centre : rayon 150 px. CONFORME à la spec.
- Geysers : rayon 48 px autour de `(-500,-250)` et `(480,300)`. CONFORME.
- Murs : bande 80 px (`maxHalfX = ArenaW/2 - 80 = 880`, `maxHalfY = ArenaH/2 - 80 = 528`). CONFORME.

**Ennemis traversent obstacles :** PASS structurel. `EnemyBase._Ready()` force `CollisionMask = 0`. Obstacles `CollisionLayer=1`/`CollisionMask=1`. Les ennemis en `CollisionMask=0` ne détectent pas les StaticBody2D des obstacles. CONFORME à la décision Phase 1.

**Risque de placement obstacle invalide :** SIGNALÉ (non-bug, point d'attention).
`SafeRandPosObstacle()` utilise `while (attempts < 50)` avec un `break` interne, mais ne dispose pas d'un `while(!valid && attempts<50)` propre — la structure actuelle effectue 50 itérations puis retourne la dernière position générée quelle qu'elle soit. Si les 50 positions échouent (très peu probable sur une arène 1920×1216), la dernière position invalide est utilisée. Probabilité : négligeable sur la surface disponible, acceptable MVP.

**Sprites obstacles — session 1 :** FAIL (3 erreurs "No loader found", voir BUG-004).
**Sprites obstacles — session 2 :** PASS (après import headless, textures chargées sans erreur).

---

### Scénario 3 — Geysers : lueur PointLight2D

**Présence `Light` dans `AetherGeyser.tscn` :** PASS.
```gdscript
[node name="Light" type="PointLight2D" parent="."]
enabled = true
color = Color(0, 0.9, 1, 1)
energy = 0.4
texture_scale = 1.2
```

**Initialisation texture en `_Ready()` :** PASS (analyse statique).
Le code C# crée une `GradientTexture2D` radiale blanc→transparent en `_Ready()` et l'assigne à `_light.Texture`. La sub-resource `GradientTexture2D` définie dans le `.tscn` avec `gradient = null` est écrasée — la sub-resource du `.tscn` est inutile mais inoffensive.

**Animation tween énergie :** PASS (analyse statique).
- Actif → `CreateTween().TweenProperty(_light, "energy", 1.8, 0.3)`. Conforme à la spec (0.4→1.8 en 0.3s).
- Inactif → `CreateTween().TweenProperty(_light, "energy", 0.4, 0.5)`. Conforme (1.8→0.4 en 0.5s).
- `SetActive(bool)` appelé dans `_Process()` sur chaque transition de cycle. CONFORME.

**Geysers repositionnés :** PASS (`Game.tscn` vérifié).
- `Geyser01` : `position = Vector2(-500, -250)`. CONFORME à la spec Phase 4.
- `Geyser02` : `position = Vector2(480, 300)`. CONFORME.

**Dégâts geyser :** PASS structurel (confirmé Phase 3b, inchangé en Phase 4).

---

### Scénario 4 — Death burst ennemis

**Scène `vfx_enemy_death_burst.tscn` :** PASS (fichier vérifié).
```
[node name="EnemyDeathBurst" type="Node2D"] → script EnemyDeathBurst.cs
[node name="Particles" type="GPUParticles2D"] → amount=8, lifetime=0.4, one_shot=true, explosiveness=1.0
[node name="Timer"] → wait_time=0.6, one_shot=true
```

**Matériau particules :** PASS.
- `spread=180.0` (émission radiale 360° effective). CONFORME à la spec.
- `initial_velocity_min=60.0` / `max=100.0`. CONFORME.
- `scale_min=1.0` / `max=2.0` — variation taille. CONFORME.
- Texture : `vfx_particle_rustswarm.png`. Présent (`git status` confirme fichier modifié = régénéré).

**Intégration `EnemyBase.Die()` :** PASS (analyse statique).
- `_deathBurstScene ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_enemy_death_burst.tscn")` — chargement lazy correctement mutualisé (une seule ressource partagée entre toutes les instances `EnemyBase`).
- `GetTree().Root.CallDeferred(Node.MethodName.AddChild, instance)` — ajout différé correct, évite le crash "Can't change state while flushing queries".
- `instance.GlobalPosition = GlobalPosition` — position avant l'add, mais il faudrait `SetDeferred("global_position", ...)` pour être cohérent avec le pattern existant. POINT D'ATTENTION — voir BUG-005.

**`GraftedColossus` sans death burst :** confirmé (gap documenté, non traité en P0). Le Colosse ne passe pas par `EnemyBase.Die()` — `SpawnDeathBurst()` n'est jamais appelé pour lui. NON-BUG (documenté).

**Qualificateur `Godot.Timer` dans `EnemyDeathBurst.cs` :** PASS. `GetNode<Godot.Timer>("Timer")` — la qualification est présente. CONFORME à la décision Phase 4 P0 documentée dans CLAUDE.md.

---

### Scénario 5 — Stabilité globale

**Pas de crash :** PASS. Deux sessions complètes, exit code 0 dans les deux cas.

**RunEndScreen flow :** PASS (logs confirmés — `outcome=death`, sauvegarde écrite, Échos calculés).

**Sauvegarde persistante :** PASS.
```json
{
  "meta": {
    "currentEchoes": 74,
    "totalEchoesEarned": 254,
    "totalEchoesSpent": 180,
    "upgrades": { "hp_boost": 1, "damage_boost": 1 }
  }
}
```
Données cohérentes (61 + 13 Échos = 74 disponibles après la session). Structure conforme GDD §18.4. PASS.

**EnemySpawner scaling :** PASS structurel. `RandomSpawnPosition()` utilise correctement `Constants.ArenaWidth/Height` mis à jour — les ennemis spawnent bien sur les bords de la grande arène.

---

### Scénario 6 — Performance

**Budget nœuds Phase 4 :**
- Sol Phase 4 : 60×38 = 2280 Sprite2D (contre 920 en Phase 3b).
- Murs H : 63×2 = 126 Sprite2D.
- Murs V : 41×2 = 82 Sprite2D.
- Décors : ~9 Sprite2D + 1 pilier tech.
- Obstacles : 5 StaticBody2D piliers (3 enfants chacun = 15 nœuds) + 2 StaticBody2D épaves (2 enfants = 4 nœuds).
- **Total nœuds statiques GroundRenderer : ~2520 nœuds** (contre ~1065 en Phase 3b — +137%).

Risque de lag à l'entrée de `Game.tscn` significativement augmenté. Aucune donnée de framerate disponible en mode non-interactif. À surveiller impérativement en session interactive — voir POINT D'ATTENTION ci-dessous.

---

### Bugs trouvés

#### BUG-004 — Sprites obstacles Phase 4 sans fichiers `.import` (bloquant workflow)

- **Sévérité :** Majeur
- **Contexte :** Fichiers `tile_pillar_stone.png`, `tile_pillar_stone_shadow.png`, `tile_wreck_machine.png` dans `assets/sprites/environment/` — aucun fichier `.import` correspondant au moment du test.
- **Reproduction :** Cloner le repo et lancer le jeu directement sans passer par l'éditeur Godot. Les 3 textures ne peuvent pas être chargées (Godot requiert un `.import` et un `.ctex` dans `.godot/imported/` pour tout PNG).
- **Observé (session 1, logs) :**
  ```
  ERROR: No loader found for resource: res://assets/sprites/environment/tile_pillar_stone.png
  ERROR: No loader found for resource: res://assets/sprites/environment/tile_pillar_stone_shadow.png
  ERROR: No loader found for resource: res://assets/sprites/environment/tile_wreck_machine.png
  [GroundRenderer] Texture absente (placeholder utilisé) : ...
  ```
  Le fallback Polygon2D s'est activé pour les 7 obstacles (5 piliers + 2 épaves). Le jeu n'a pas crashé.
- **Attendu :** Textures chargées sans erreur, sprites visuels corrects.
- **Résolution en session :** Lancement de `godot --headless --import` — les `.import` et `.ctex` ont été générés. Session 2 : zéro erreur.
- **Cause racine :** Les `.import` ne sont pas commités dans le repo (`.gitignore` n'exclut pas les `.import` dans `assets/`, mais les fichiers n'ont pas été ajoutés/commités). Les `.ctex` dans `.godot/` sont exclus par `.gitignore` — normal. Les `.import` dans `assets/` doivent être commités.
- **Correction recommandée :** Exécuter `godot --headless --import` sur le repo (ou ouvrir l'éditeur une fois), puis `git add assets/sprites/environment/*.import` et commiter. Idéalement intégrer `godot --headless --import` en CI ou en post-checkout hook.
- **Assigné à :** developpeur

#### BUG-005 — `SpawnDeathBurst()` : `GlobalPosition` assignée avant `AddChild` (race condition potentielle)

- **Sévérité :** Mineur
- **Contexte :** `src/Entities/Enemies/EnemyBase.cs`, méthode `SpawnDeathBurst()`, lignes 110-113.
- **Code incriminé :**
  ```csharp
  var instance = _deathBurstScene.Instantiate<Node2D>();
  instance.GlobalPosition = GlobalPosition;
  GetTree().Root.CallDeferred(Node.MethodName.AddChild, instance);
  ```
- **Problème :** `GlobalPosition` est une propriété qui prend effet seulement quand le nœud est dans l'arbre. L'assigner avant `AddChild` (qui est lui-même différé) n'a aucun effet — quand `AddChild` s'exécute, la `GlobalPosition` sera (0,0) car le nœud n'était pas dans l'arbre au moment de l'assignation.
- **Attendu :** Le burst de particules se positionne là où l'ennemi est mort.
- **Observé probable :** Tous les death bursts apparaissent à la position (0,0) de la scène (origine du monde) plutôt qu'à la position de l'ennemi mort. Non confirmable en mode non-interactif, mais la logique du code le garantit.
- **Correction recommandée :** Utiliser `SetDeferred("global_position", GlobalPosition)` après le `CallDeferred(AddChild)`, comme fait dans `EnemyBase.SpawnXpOrb()` (ligne 136) :
  ```csharp
  var instance = _deathBurstScene.Instantiate<Node2D>();
  GetTree().Root.CallDeferred(Node.MethodName.AddChild, instance);
  instance.SetDeferred("global_position", GlobalPosition);
  ```
  Ou alternativement, passer par la position locale du parent (si le burst est enfant d'un nœud déjà dans l'arbre).
- **Hypothèse de sévérité :** Le burst à (0,0) est visuellement incorrect mais non bloquant. L'effet reste présent, il est juste mal positionné. Si (0,0) est toujours dans le viewport (cas fréquent au début de run), le bug peut passer inaperçu.
- **Assigné à :** developpeur

---

### Points d'attention Phase 4 (non-bugs)

**Budget nœuds GroundRenderer ×2.4 vs Phase 3b :** ~2520 nœuds statiques contre ~1065. La création en une frame dans `_Ready()` expose à un freeze visible au chargement de `Game.tscn`. Recommandation : mesurer le temps de chargement en session interactive et envisager un étalement via coroutine si > 100ms perceptible.

**Inutilité de la sub-resource `GradientTexture2D` dans `AetherGeyser.tscn` :** La sub-resource `LightTexture` définie dans le `.tscn` (avec `gradient = null`) est écrasée en `_Ready()` par le code C#. Elle encombre le `.tscn` sans utilité. Nettoyage cosmétique : supprimer la sub-resource et initialiser `_light.Texture` directement en `_Ready()`.

**Obstacle placement — dernière position en cas d'échec des 50 tentatives :** `SafeRandPosObstacle()` retourne la dernière position générée si toutes les 50 tentatives sont invalides. Risque négligeable sur l'espace disponible (arène 1920×1216, zone interdite ~28k px²). Acceptable MVP.

**PointLight2D sur geysers — impact sur la performance :** Deux `PointLight2D` actifs avec bloom WorldEnvironment. Sur GPU AMD RX 9070 (hardware développeur), pas de problème attendu. À surveiller sur configurations plus modestes.

**`tile_crate_tech.png`, `tile_arch_fallen.png`, `tile_terminal_corrupt_01/02.png` présents mais non utilisés :** 4 sprites dans `assets/sprites/environment/` générés mais sans référence dans le code. Assets en attente Phase 4 P1 (décors destructibles ou éléments narratifs). À documenter pour éviter confusion.

---

### Validation des scénarios Phase 4 P0

| Scénario | Résultat | Mode |
|---|---|---|
| S1 — Démarrage et lisibilité arène | PASS | Analyse statique + logs |
| S2 — Obstacles collidables (présence + collision joueur) | PASS structurel / FAIL sprite (BUG-004 résolu) | Analyse statique |
| S3 — Geysers lueur PointLight2D | PASS | Analyse statique |
| S4 — Death burst ennemis | PASS structurel / WARN position (BUG-005) | Analyse statique |
| S5 — Stabilité globale | PASS | Runtime logs |
| S6 — Performance (framerate dense) | NON TESTÉ | Requiert session interactive |

---

### Vérifications requises en session interactive

Les points suivants ne peuvent pas être validés sans pilotage humain du jeu :

1. **Confirmation visuelle arène agrandie** : le joueur ne voit pas les murs en démarrant (viewport 1280×720 dans une arène 1920×1216 — vérifié en théorie).
2. **Obstacle pilier : bloque physiquement le joueur** : collision shape `CapsuleShape2D(r=12)` offset Y=+16 — vérification que le joueur est bien stoppé.
3. **Obstacle épave : bloque physiquement le joueur** : `RectangleShape2D(56, 24)` — idem.
4. **Lueur geyser visible à l'écran** : variation energy 0.4→1.8 avec WorldEnvironment bloom.
5. **Death burst position correcte** (dépend de la correction BUG-005).
6. **Death burst lisibilité en combat dense** (20+ ennemis tués rapidement).
7. **Camera2D ne montre pas hors-arène** lors d'un déplacement vers les bords extremes.
8. **Framerate en combat dense (50+ ennemis)** — critique avec budget nœuds doublé.

---

### Briefing developpeur — Phase 4 P0

#### BUG-004 : Fichiers `.import` manquants pour les sprites obstacles

**Fichiers concernés :**
- `assets/sprites/environment/tile_pillar_stone.png`
- `assets/sprites/environment/tile_pillar_stone_shadow.png`
- `assets/sprites/environment/tile_wreck_machine.png`

**Action immédiate :** `git add assets/sprites/environment/tile_pillar_stone.png.import assets/sprites/environment/tile_pillar_stone_shadow.png.import assets/sprites/environment/tile_wreck_machine.png.import` — les `.import` ont été générés par `godot --headless --import` pendant la session de test et sont maintenant présents sur le disque.

Vérifier également les `.import` des autres PNG Phase 4 non encore dans le repo : `tile_crate_tech.png`, `tile_arch_fallen.png`, `tile_terminal_corrupt_01/02.png`, `vfx_particle_aether_ambient.png`, `vfx_particle_impact_plasma.png`, `vfx_particle_impact_sentinel.png`.

#### BUG-005 : `SpawnDeathBurst()` — `GlobalPosition` ineffective avant `AddChild`

**Fichier :** `src/Entities/Enemies/EnemyBase.cs`, méthode `SpawnDeathBurst()`.

**Correction :**
```csharp
private void SpawnDeathBurst()
{
    _deathBurstScene ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_enemy_death_burst.tscn");
    if (_deathBurstScene == null) return;

    var instance = _deathBurstScene.Instantiate<Node2D>();
    GetTree().Root.CallDeferred(Node.MethodName.AddChild, instance);
    instance.SetDeferred("global_position", GlobalPosition); // SetDeferred, pas assignation directe
}
```

Pattern identique à `SpawnXpOrb()` (ligne ~136 du même fichier). La cohérence du pattern est vérifiable par lecture du code existant.

---

## Test Polish Visuel — 2026-06-23

**Testeur :** game-tester (agent Claude)
**Hash git :** 9334f9e (feat(vfx): clarté arène + lumières + notifications armes équipées)
**Commits testés :** 9334f9e (feat), d7be020 (docs)
**Méthode :** Analyse statique du diff + logs de démarrage runtime. Lancement du binaire `--quit-after 3s` pour vérification smoke test.
**Device GPU :** AMD Radeon RX 9070 (D3D12 Forward+)
**Date :** 2026-06-23

---

### Résumé exécutif

Trois features visuelles implémentées dans le commit 9334f9e :

1. **Clarté arène** : tiles sol assombris (Modulate 42%), murs assombris (55%), overlay Polygon2D bleu-noir alpha 38% ZIndex=-7.
2. **Effets de lumière** : PointLight2D cyan joueur (energy 0.55, rayon 256px), bleu plasma projectiles joueur (energy 1.2), rouge-orange projectiles ennemis (energy 1.0). Texture radiale factoryisée via `Player.MakeRadialLightTexture()`.
3. **Notifications armes** : Label `WeaponNotifLabel` bas d'écran centré, trois variantes couleur (or/cyan/violet), flash modulate joueur synchrone.

**Résultat global : VALIDE AVEC RESERVES.**
- Feature 1 (clarté arène) : PASS structurel complet. Implémentation correcte, aucun conflit ZIndex.
- Feature 2 (lumières) : PASS structurel avec un point d'attention sur le timing des lumières projectiles pendant LevelUp.
- Feature 3 (notifications) : 1 bug Mineur (BUG-203) identifié — flashes joueur et notifications créés pendant la pause tree sont différés, comportement acceptable mais potentiellement contre-intuitif. 1 incohérence de design (BUG-204) sur la notification de fusion.

---

### 1. Smoke test — PASS

**Compilation C# :** PASS. `dotnet build --nologo -v quiet` — 0 avertissements, 0 erreurs. Les 7 fichiers modifiés compilent sans ambiguïté.

**Démarrage runtime (--quit-after 3s) :**
```
Godot Engine v4.7.stable.mono.official.5b4e0cb0f
D3D12 12_0 - Forward+ - Using Device #0: AMD - AMD Radeon RX 9070
[MetaProgressionSystem] 7 upgrades chargés.
[SaveManager] Sauvegarde chargée.
[MetaProgressionSystem] Prêt. Échos disponibles : 7
```
Aucun `GD.PrintErr`. Exit code 0. PASS.

---

### 2. Feature 1 — Clarté arène (GroundRenderer)

#### Tiles sol — Modulate 42%

**Code :** `Modulate = new Color(0.42f, 0.42f, 0.48f, 1f)` sur chaque `Sprite2D` de sol.

**Analyse ZIndex :** Sol ZIndex=-10. Overlay ZIndex=-7. L'overlay s'applique au-dessus du sol mais sous les entités (joueur ZIndex=0 par défaut, ennemis ZIndex=0). La chaîne de rendu est donc : sol × 0.42 → overlay alpha 0.38 → entités. Correct.

**Teinte de l'overlay :** `Color(0f, 0.01f, 0.06f, 0.38f)` — bleu-nuit quasi pur, légèrement désaturé. Compatible avec la palette UI (fond `Color(0.102, 0.102, 0.18)`). Cohérence maintenue.

**Couverture de l'overlay :** `hw = ArenaW/2f = 960`, `hh = ArenaH/2f = 608`. Le Polygon2D couvre exactement `[-960, +960] × [-608, +608]` — identique à l'arène jouable. Bords alignés sur les murs StaticBody2D. CONFORME.

**ZIndex -7 de l'overlay par rapport aux décors :**
- Sol : -10 (sous overlay)
- Flaques rouille et murs visuels : -9 (sous overlay — assombris deux fois : Modulate 55% + overlay)
- Débris : -8 (sous overlay)
- Overlay : -7
- Particules ambiantes Aether (Game.tscn) : -1 (au-dessus de l'overlay — seront légèrement voilées par l'overlay qui est leur enfant de leur point de vue de rendu CanvasItem)
- Obstacles, joueur, ennemis : 0 ou 1 (au-dessus de l'overlay)

Point d'attention : les 4 `GPUParticles2D` ambiants Aether (ZIndex=-1 dans `Game.tscn`) sont enfants du nœud racine `Game`, pas du `GroundRenderer`. L'overlay est enfant de `GroundRenderer` à ZIndex=-7. Dans Godot 4, le ZIndex des enfants s'additionne au ZIndex du parent. `GroundRenderer` est un `Node2D` sans ZIndex explicite (ZIndex=0 par défaut). Donc le Polygon2D overlay a un ZIndex global de 0 + (-7) = -7. Les particules ambiantes ont ZIndex global = 0 + (-1) = -1. Les particules ambiantes (-1) sont donc AU-DESSUS de l'overlay (-7). Comportement correct — les particules ne sont pas masquées par l'overlay.

**Effet net attendu à l'écran :** fond uniformément sombre, teinte bleu-nuit, contrastant avec les sprites joueur/ennemis (ZIndex 0, non affectés par l'overlay).

**Verdict feature 1 :** PASS structurel complet. Aucun bug identifié.

#### Tiles murs — Modulate 55%

**Code :** `Modulate = new Color(0.55f, 0.55f, 0.62f, 1f)` dans `WallSprite()`.

**Effet cumulatif :** Les murs visuels ont ZIndex=-9, soit sous l'overlay (-7). Ils subissent le Modulate 55% + l'overlay alpha 38%. Résultat visuel : murs encore plus sombres que le sol (sol 42% vs murs 55%, mais les murs sont sous l'overlay). Les murs seront légèrement moins sombres que le sol dans la zone de jeu centrale — ce qui est cohérent (murs au bord, moins de pression visuelle).

**Note :** la teinte 62% sur le canal bleu (`0.62f`) donne une légère nuance bleue-grise aux murs, différenciant subtilement la texture de mur de la texture de sol assombrie. Intention artistique lisible.

**Verdict :** PASS.

---

### 3. Feature 2 — Effets de lumière (PointLight2D)

#### Lumière joueur

**Code :** `AddPlayerLight()` crée un `PointLight2D` en `_Ready()` avec :
- `Color = Color(0.267, 1, 0.933)` (cyan Aether — cohérent avec la palette UI)
- `Energy = 0.55f`
- `TextureScale = 4.0f` (rayon = 128px × 4.0 = 512px diamètre, soit ~256px de rayon effectif)
- `BlendMode = Add`

**Texture radiale :** `MakeRadialLightTexture(128)` crée une `GradientTexture2D` radiale 128×128 px, blanc au centre → transparent au bord. Gradient radial avec `FillFrom=(0.5, 0.5)` et `FillTo=(1.0, 0.5)` — falloff correct pour un halo circulaire. CONFORME.

**Cache statique `_playerLightTex` :** `private static Texture2D? _playerLightTex` partagé entre les instances. Dans le jeu, une seule instance `Player` existe — pas de conflit. Économie mémoire correcte.

**Coexistence avec `_hitTween` (Modulate root) :** `_hitTween` opère sur `Player.Modulate` (root node). La `PointLight2D` `PlayerLight` est enfant du joueur — son rendu est indépendant du `Modulate` root (les PointLight2D ne sont pas des CanvasItem affichant un sprite, ils émettent de la lumière). Pas d'interférence entre hit flash et lumière joueur.

**Coexistence avec `UpdateHpBlink()` :** `_sprite.Modulate` (sur le `AnimatedSprite2D` enfant). La PointLight2D est un autre enfant du joueur. Indépendants. PASS.

**Coexistence avec les flashes VFX (InventorySystem) :** Les tweens de flash or/cyan/violet opèrent sur `Player.Modulate` root. Modulate root est multiplicatif sur tous les enfants sprites, mais pas sur les PointLight2D (qui sont des nœuds de lumière, pas des ColorRect ou Sprite2D). Aucune interférence. PASS.

**Activation/désactivation à la mort :** La PointLight2D reste active après la mort du joueur (`_isDead = true`). `_PhysicsProcess` est court-circuité par `_isDead` mais la lumière continue de s'afficher. L'animation death joue, la lumière cyan persiste. Comportement légèrement incohérent — la lumière devrait idéalement s'éteindre à la mort. Non-bloquant.

#### Lumières projectiles joueur (Bullet.cs)

**Code :** `PointLight2D` créé dans `Bullet._Ready()` :
- `Color = Color(0.5, 0.85, 1)` (bleu plasma)
- `Energy = 1.2f` — valeur élevée, potentiellement éligible au glow WorldEnvironment (threshold 0.6)
- `TextureScale = 1.8f` (rayon = 32px × 1.8 = 57.6px diamètre)

**Cache statique `_bulletLightTex` :** partagé entre toutes les instances `Bullet`. Économie correcte. La texture 32×32 est plus petite que la texture joueur 128×128 — halo plus resserré sur les projectiles. Cohérent visuellement.

**Bloom interaction :** `Energy = 1.2` combiné au `BlendMode.Add` va produire des pixels lumineux pouvant dépasser le threshold 0.6 du WorldEnvironment glow. Un halo de glow bleu sur les projectiles est attendu et cohérent avec l'univers SF.

**Performance :** Les projectiles du Canon à Impulsions ont un cooldown de 0.8s au niveau 1. En phase de run dense, 5-10 projectiles peuvent coexister (vitesse × lifecycle). Chaque Bullet crée une PointLight2D. Le renderer 2D Godot gère bien les PointLight2D multiples — pas de risque à ce volume. À surveiller si `ProjectileCount` monte à 5+ (niveau 5 Canon).

#### Lumières projectiles ennemis (EnemyBullet.cs)

**Code :** `PointLight2D` créé dans `EnemyBullet._Ready()` :
- `Color = Color(1, 0.35, 0.1)` (rouge-orange)
- `Energy = 1.0f`
- `TextureScale = 1.6f` (rayon = 32px × 1.6 = 51.2px diamètre)

**Distinction visuelle :** Rouge-orange (balles ennemies) vs bleu-plasma (balles joueur) — différenciation nette. Cohérent avec la palette ennemis (rouge-rouille, orange-brun).

**Seule la Sentinelle Corrompue tire des projectiles :** à raison d'une balle toutes les 2-3s. Volume faible. Pas de risque performance.

**Verdict feature 2 :** PASS structurel. Aucun bug bloquant. Deux points d'attention non-bloquants (lumière joueur post-mort, volume projectiles niveau haut Canon).

---

### 4. Feature 3 — Notifications armes équipées

#### Architecture HUD

**Nœud `WeaponNotifLabel` :** présent dans `HUD.tscn` (lignes 89-103). Position : anchors bas d'écran, centré horizontalement (`anchor_top=1, anchor_bottom=1, offset_top=-130, offset_bottom=-80`). Largeur 440px (offset_left=-220 à offset_right=220). `modulate = Color(1,1,1,0)` par défaut (invisible). CONFORME.

**`HUD.Instance` singleton :** pattern correct — `Instance = this` dans `_Ready()`, `if (Instance == this) Instance = null` dans `_ExitTree()`. Protection contre les instances zombies. CONFORME.

**`GetNode<Label>("WeaponNotifLabel")` :** chemin direct depuis la racine HUD (CanvasLayer). Le nœud est bien un enfant direct de HUD dans le `.tscn`. PASS.

#### Méthodes ShowWeaponEquipped / ShowWeaponUpgraded / ShowPassiveAcquired

**Pattern :** Texte → Kill tween existant → reset Modulate → nouveau tween (interval + fade out). Correct — `_notifTween?.Kill()` évite les tweens accumulés si des level-ups rapides se succèdent. CONFORME.

**Durées :** Equipement = 1.6s + 0.5s fade = 2.1s total. Upgrade = 1.3s + 0.4s = 1.7s. Passif = 1.6s + 0.5s = 2.1s. Durées raisonnables — non vérifiable sans session interactive mais cohérentes avec un jeu de survivor (run rapide).

**Couleurs :** Or `Color(1, 0.8, 0.267)` / Cyan `Color(0.267, 1, 0.933)` / Violet `Color(0.667, 0.267, 1)` — identiques à la palette UI documentée dans CLAUDE.md. CONFORME.

**Glyphes unicode :** `⚡` (nouvel équipement), `↑` (upgrade), `✦` (passif). Ces glyphes peuvent ne pas s'afficher correctement si la police par défaut Godot ne les contient pas. Point à valider visuellement. Non-bloquant (dégradation gracieuse : carré vide).

#### Flashes joueur (tweens InventorySystem)

**Code :** `player.CreateTween().TweenProperty(player, "modulate", Colors.White, duration).From(...)`.

**Valeurs `From` :** Or = `Color(2.8, 1.8, 0.3, 1)` / Cyan = `Color(0.3, 2.5, 2.2, 1)` / Violet = `Color(1.5, 0.5, 2.8, 1)` — valeurs HDR > 1.0 pour un effet de flash lumineux fort. Compatible avec le WorldEnvironment bloom (threshold 0.6).

**Conflit avec `_hitTween` :** Deux tweens distincts sur `Player.Modulate`. Godot permet plusieurs tweens simultanés sur une même propriété — ils se superposent (le dernier créé a priorité si sur la même sous-propriété). Si un hit flash (0.1s) démarre pendant un flash or (0.45s), les deux tweens vont se battre sur `modulate`. Le hit flash `_hitTween?.Kill()` tue le tween précédent mais UNIQUEMENT `_hitTween` (le tween stocké dans la variable). Le tween de flash or (créé dans `InventorySystem`, non stocké dans `_hitTween`) n'est pas tué. Résultat : le hit flash court-circuite visuellement le flash or s'il arrive pendant les 0.45s du flash. Comportement acceptable — le feedback de dégâts prime sur le feedback d'équipement. Non-bloquant.

---

### 5. Bugs identifiés

#### BUG-203 — Flash joueur et notification HUD invisibles si level-up suivi d'un second level-up immédiat

- **Sévérité :** Mineur
- **Contexte :** `src/Systems/InventorySystem.cs` — `TriggerWeaponEquipVfx()`, `TriggerWeaponUpgradeVfx()`, `TriggerPassiveVfx()`. `src/UI/HUD.cs` — `ShowWeaponEquipped()` etc.
- **Reproduction :** Obtenir deux level-ups consécutifs très rapprochés (ex. absorber plusieurs orbes XP en rafale qui déclenchent deux paliers). Le second level-up affiche le LevelUpScreen pendant que le flash/notification du premier est encore en cours (tween de 0.35-0.45s non terminé). Le `_notifTween?.Kill()` tue le tween précédent avant qu'il soit visible.
- **Observé :** La notification du premier level-up est interrompue par `Kill()` avant d'être lue si le second level-up survient en moins de 1.3-1.6s (durée de l'intervalle avant fade-out). La notification du second level-up remplace immédiatement la première.
- **Attendu :** Chaque notification est visible au minimum ~0.5s avant d'être remplacée, ou les notifications sont mises en file.
- **Hypothèse :** Comportement documenté et probablement connu — le `Kill()` est intentionnel pour éviter l'empilement. Dans la pratique, les level-ups rapprochés (< 2s) sont fréquents à mi-run. Le Kill() sur le `_notifTween` provoque un "flash" de remplacement non fluide.
- **Impact réel :** Faible. La dernière notification est toujours correctement affichée. Seule la notification précédente est perdue. Acceptable pour un MVP.
- **Assigné à :** game-designer (décision UX : file ou remplacement immédiat ?)

#### BUG-204 — Notification fusion utilise l'icone "nouvel équipement" (or) au lieu d'un signal visuel distinct

- **Sévérité :** Cosmétique
- **Contexte :** `src/Systems/InventorySystem.cs`, méthode `InstantiateWeapon()` appelée depuis `ApplyFusion()`. `src/UI/HUD.cs`, méthode `ShowWeaponEquipped()`.
- **Reproduction :** Choisir une carte de fusion au LevelUpScreen (ex. "Rail Surchargé").
- **Observé :** `ApplyFusion()` → `InstantiateWeapon()` → `TriggerWeaponEquipVfx()` → flash or + `⚡ Rail Surchargé`. Le FusionFlash (blanc intense CanvasLayer) se déclenche également depuis `LevelUpScreen.OnCardChosen()`. Résultat : deux effets visuels superposés, mais la notification affiche `⚡` (icone nouvelle arme) au lieu d'un indicateur fusion.
- **Attendu :** La fusion est un événement rare et distinct — elle mérite un signal visuel différencié (ex. `⚡⚡ FUSION : Rail Surchargé` en couleur distincte, ou violet comme les épiques).
- **Impact :** Lisibilité du feedback réduite pour un événement rarissime. Le joueur ne distingue pas visuellement "j'ai équipé une nouvelle arme" de "j'ai déclenché une fusion évolutive".
- **Assigné à :** game-designer

---

### 6. Points d'attention (non-bugs)

**Lumière joueur post-mort :** La `PointLight2D` "PlayerLight" reste active après la mort du joueur. `Player._isDead` stoppe `_PhysicsProcess` mais n'éteint pas la lumière. Comportement légèrement incohérent : le joueur mort continue d'émettre une lueur cyan pendant l'animation death. Non-bloquant — l'animation death dure 0.8s et le RunEndScreen apparaît rapidement.

**Double allocation texture si deux instances Player existent :** `_playerLightTex` est `static` — une seule allocation partagée. Si une future feature implique deux `Player` (coop, fantôme), la texture reste correcte (lecture seule). Aucun risque actuel.

**Emoji dans les textes de notification :** `⚡`, `↑`, `✦` nécessitent un support unicode dans la police Godot par défaut. Si absents, ils s'affichent comme des rectangles vides. À valider visuellement. Correction simple : remplacer par des caractères ASCII (`[!]`, `[+]`, `[*]`) si la police ne les supporte pas.

**Accumulation de PointLight2D en combat dense :** À `ProjectileCount = 5` (Canon niveau 5) avec cooldown réduit, jusqu'à 10-15 `Bullet` avec chacun un `PointLight2D` peuvent coexister. Sur RX 9070 = sans impact. Sur GPU intégré (Intel HD) = à surveiller. Acceptable pour la cible PC.

**`TriggerWeaponEquipVfx` appelé aussi via `RegisterExistingWeapon` au démarrage :** Non — `RegisterExistingWeapon()` ne passe pas par `InstantiateWeapon()`, il ne déclenche pas de VFX. PASS.

---

### 7. Validation scénarios par feature

| Feature | Implémentation | Comportement attendu | Risques identifiés |
|---|---|---|---|
| Clarté arène (tiles + overlay) | PASS — ZIndex correct, Modulate appliqué | Sol plus sombre, sprites se détachent | Aucun |
| PointLight2D joueur | PASS — texture radiale, Energy 0.55, cache statique | Halo cyan visible autour du joueur | Lumière post-mort (non-bloquant) |
| PointLight2D balles joueur | PASS — bleu plasma, Energy 1.2 (éligible glow) | Traînée lumineuse plasma sur projectiles | Volume élevé niveau 5 Canon |
| PointLight2D balles ennemies | PASS — rouge-orange, Energy 1.0 | Distinction claire balles joueur/ennemi | Aucun |
| Notification texte HUD | PASS — Label présent en tscn, Modulate 0 initial | Texte apparaît bas d'écran, fade-out 2s | Glyphes unicode (à valider) |
| Flash joueur (modulate) | PASS avec réserve — tweens Inherit suspendus pendant pause, jouent au retour | Flash visible dès la reprise du jeu | Kill() agressif si level-up rapide (BUG-203) |
| Notification fusion | PASS fonctionnel / WARN design | Notification affichée mais signal visuel générique | BUG-204 (cosmétique) |

---

### 8. Vérifications requises en session interactive

Les points suivants ne peuvent pas être confirmés sans pilotage humain :

1. **Visibilité du halo cyan joueur** : Energy 0.55 est-il suffisant pour un rendu lisible sans être écrasant ? Le seuil perceptuel dépend du calibrage de l'écran.
2. **Glow bloom sur les projectiles** : Energy 1.2 sur les balles joueur déclenche-t-il le WorldEnvironment glow ? Si oui, l'effet est-il esthétiquement réussi ou trop intense ?
3. **Contraste sol assombri vs sprites** : Modulate 42% + overlay 38% produit-il un contraste suffisant pour que les sprites (joueur 32×32, ennemis 32×32) soient immédiatement lisibles ?
4. **Rendu glyphes** `⚡`, `↑`, `✦` dans les notifications.
5. **Lisibilité notification en combat dense** : le label à `font_size = 22` bas d'écran est-il visible malgré les particules, les ennemis et le HUD ?
6. **Flash joueur or au premier level-up** : l'effet `Color(2.8, 1.8, 0.3)` → blanc est-il perceptible malgré le LevelUpScreen qui vient de se fermer ?

---

### 9. Briefing developpeur — aucun bug bloquant

Aucun bug bloquant ou majeur identifié dans les 7 fichiers modifiés. Compilation propre, démarrage sans erreur.

**Point à corriger (faible priorité) :** Si la police Godot par défaut ne supporte pas `⚡`/`↑`/`✦`, remplacer dans `HUD.cs` lignes 156, 167, 178 par des alternatives ASCII.

**Vérification à faire en runtime :** confirmer que la `PointLight2D` "PlayerLight" ne cause pas de régression visuelle avec le hit flash existant (deux tweens sur `player.Modulate` simultanés, cf. analyse conflit `_hitTween` section 4).

---

### 10. Briefing game-designer — BUG-203 et BUG-204

**BUG-203 — Notification level-up rapide :** Confirmer la décision UX : remplacement immédiat (comportement actuel — simple, pas de file) ou file FIFO (chaque notification visible ~1s avant la suivante). La file est plus informative mais plus complexe à implémenter.

**BUG-204 — Signal visuel fusion :** La fusion est l'événement le plus rare du run. Proposer une notification distincte : `⚡⚡ FUSION : Rail Surchargé` en couleur or+violet, ou ajouter un handler `ShowFusionAcquired()` dans HUD dédié à ce cas. La notification actuelle (`⚡ Rail Surchargé` en or) ne se distingue pas d'un équipement ordinaire — sous-exploite l'impact émotionnel de la fusion.

**Référence GDD :** §12 (fusions) : "événement rare et important" — le SFX `sfx_fusion_evolve` + `FusionFlash` blanc existent déjà. Une notification textuelle dédiée renforcerait la cohérence de feedback.

---

## Test HUD Refonte — 2026-06-25

**Testeur :** game-tester (agent Claude)
**Hash git :** 52e38fd (docs: GDD §16 HUD juicy + README phase courante 2026-06-25)
**Commit HUD testé :** 52e9d6a (feat(ui): HUD juicy redesign - barres sci-fi avec glow et animations)
**Méthode :** Analyse statique complète (HUD.tscn + HUD.cs) + smoke test runtime `--quit-after 5` + smoke test `--quit-after 10` sur `Game.tscn` + lancement interactif.
**Device GPU :** AMD Radeon RX 9070 (D3D12 Forward+)

---

### Résumé exécutif

Le commit `52e9d6a` livre une refonte complète du HUD : panneau stats haut-gauche avec fond sombre et bordure cyan, barre HP 18px animée (drain smooth + glow), barre XP segmentée 20 blocs générée dynamiquement en C#, sous-label XP format "LV x | y / z XP", timer encadré d'accents cyan, panneau Cores violet haut-droite.

**Compilation C# :** PASS — 0 erreur, 0 warning. Build en 0.70s.

**Démarrage runtime (--quit-after 10, Game.tscn) :**
```
Godot Engine v4.7.stable.mono.official.5b4e0cb0f
D3D12 12_0 - Forward+ - Using Device #0: AMD - AMD Radeon RX 9070
[MetaProgressionSystem] 7 upgrades chargés.
[SaveManager] Sauvegarde chargée.
[MetaProgressionSystem] Prêt. Échos disponibles : 13
[InventorySystem] Arme existante enregistrée : impulse_cannon niveau 1
[EnemySpawner] 6 types d'ennemis chargés.
```
Aucune erreur de nœud manquant, aucune erreur de texture, aucun crash. PASS.

**Verdict global : PASS avec 2 discordances texte (Cosmétique) et 1 point d'attention layout.**

---

### 1. Zones HUD — présence et structure

#### Zone haut-gauche — panneau stats

**StatsPanelBg :** `ColorRect` fond `Color(0.04, 0.04, 0.10, 0.90)`, `offset_left=8 / offset_right=308 / offset_top=8 / offset_bottom=130`. Hauteur effective : 122px (130-8). PRESENT.

**AccentBar :** barre verticale cyan `Color(0.267, 1.0, 0.933)` 5px de large, s'étend sur toute la hauteur du panneau (offset_bottom=130). Cohérente avec l'AccentBar du commit. PRESENT.

**HpHeader (HpIcon + HpLabel) :** icon coeur rouge "♥ " + label texte "100 / 100" à font_size=14. HpLabel a `size_flags_horizontal=3` (fill+expand), `horizontal_alignment=2` (right-align). PRESENT.

**HpBarBg + HpBar + HpBarGlow :** fond sombre `Color(0.06, 0.06, 0.12)`, barre fill cyan, glow alpha=0.14. Hauteur 18px (épaisseur doublée vs 10px précédents). PRESENT.

**HpXpSeparator :** ligne de séparation cyan alpha=0.18 entre HP et XP. PRESENT.

**XpRow (LevelLabel + XpBarBg) :** LevelLabel "LV 1" à font_size=14, `custom_minimum_size=Vector2(44,0)`, `autowrap_mode=0` — fix du commit 6f3cb80 préservé. PRESENT.

**XpBarBg + segments dynamiques :** `ColorRect` fond sombre, enfants : `XpBarGlow` (ambient glow plein fond), `XpBar` (maintenu à width=0, utilisé uniquement pour le flash level-up), 20 segments `ColorRect` créés via `CallDeferred(BuildXpSegments)` en code. PRESENT — segments injectés en runtime, pas dans le .tscn.

**XpSubLabel :** `Label` `offset_top=103 / offset_bottom=128` dans le panneau (bottom=130). font_size=11, color cyan teal atténué. Formaté par code : `"LV {level}  |  {xp} / {xpToNext} XP"`. PRESENT.

#### Zone haut-centre — timer

**TimerBg :** `ColorRect` centré (`anchor_left=0.5`), largeur 144px (`offset_left=-72 / offset_right=72`), hauteur 62px. PRESENT.

**TimerAccentL + TimerAccentR :** barres cyan alpha=0.6 de 5px de large, encadrant le timer. PRESENT.

**TimerLabel :** "15:00" à font_size=32. Mis à jour en code par `UpdateTimer()` — couleur dynamique par `TimerColor()` (blanc>120s, orange 60-120s, rouge <60s). PRESENT.

**TimerSubLabel :** "RUNTIME ACTIVE" (voir BUG-HUD-01 ci-dessous). font_size=9, color blanc atténué 45%. Statique — jamais mis à jour par le code. PRESENT mais texte incorrect.

#### Zone haut-droite — panneau Cores

**CoresBg :** `ColorRect` fond sombre ancré à droite, `offset_left=-192 / offset_right=-8`, hauteur 54px. PRESENT.

**CoresBgAccent :** barre verticale violet `Color(0.667, 0.267, 1.0)` 5px de large. PRESENT.

**CoresPanelTitle :** "CHIMERA CORES" à font_size=9 (voir BUG-HUD-02 ci-dessous). PRESENT mais texte incorrect.

**CoresPanelTitleSep :** ligne de séparation violet alpha=0.12. PRESENT.

**CoresContainer/CoresLabel :** "⬡ 0" à font_size=20, mis à jour par `UpdateCores()`. PRESENT.

#### Zone bas-centre — notifications armes

**WeaponNotifLabel :** `Label` centré, anchors bas, `modulate=Color(1,1,1,0)` par défaut (invisible). font_size=22. Méthodes `ShowWeaponEquipped`, `ShowWeaponUpgraded`, `ShowPassiveAcquired` présentes et inchangées depuis le commit précédent. PRESENT.

---

### 2. Vérification des GetNode

Tous les 11 chemins `GetNode` de `HUD.cs` sont validés contre le .tscn :

| Chemin GetNode | Présent dans .tscn |
|---|---|
| `HpHeader/HpLabel` | PASS |
| `HpBarBg/HpBar` | PASS |
| `HpBarBg/HpBarGlow` | PASS |
| `XpRow/LevelLabel` | PASS |
| `XpRow/XpBarBg` | PASS |
| `XpRow/XpBarBg/XpBar` | PASS |
| `XpRow/XpBarBg/XpBarGlow` | PASS |
| `XpSubLabel` | PASS |
| `TimerLabel` | PASS |
| `CoresContainer/CoresLabel` | PASS |
| `WeaponNotifLabel` | PASS |

Aucun `GetNode` ne provoquera de `NullReferenceException` au démarrage. PASS.

---

### 3. Barre XP segmentée — analyse détaillée

**Création dynamique :** `BuildXpSegments()` est appelée via `CallDeferred` depuis `_Ready()`, donc après que le layout Godot a résolu `XpBarBg.Size`. Le fallback `bgW = 230f` s'active si `XpBarBg.Size.X <= 4f` (premier frame avant layout) — cette situation ne survient pas car `CallDeferred` s'exécute en fin de frame, après le layout.

**Calcul des segments :**
- `XpBarBg` largeur estimée : 238px (XpRow 288px - LevelLabel 44px - séparation 6px).
- `segW = (238 - 19) / 20 = 10.95px`, flooré à 10px.
- Position segment `i` : `x = i * (10.95 + 1) = i * 11.95px`.
- Dernier segment (i=19) : `x = 227.05px, right = 237.05px` — 0.95px de marge à droite. Correct.
- Gap effectif entre segments : `11.95 - 10 = 1.95px` (~2px visuels). Commentaire code dit "1 px gap" — légère imprécision du commentaire, pas un bug fonctionnel.

**Remplissage :** `filled = RoundToInt(ratio * 20)` — interpolation correcte. Les segments remplis passent en `Color(0.267, 1.0, 0.933)` (cyan), les vides en `Color(0.06, 0.06, 0.12)` (fond sombre).

**Flash level-up :** `OnLevelUp()` surexpose `_xpBarGlow` (modulate 2x) + `_xpBar` (modulate 3x) + tous les segments (modulate 3x) → tween fade-back 0.5-0.6s. `_segmentsReady` guard présent. CONFORME.

**Guard `_segmentsReady` :** présent dans `UpdateXpBar()` et `OnLevelUp()`. Si `LevelUp` se déclenche avant `BuildXpSegments()`, aucun crash — le flash des segments est simplement sauté. PASS.

**Verdict barre XP segmentée :** PASS. Fonctionnellement correct. Le commentaire "1 px gap" est imprécis (gap réel ~2px) mais sans incidence sur le rendu.

---

### 4. Sous-label XP

**Format attendu (brief) :** "LV 1  |  37 / 95 XP"

**Format implémenté (HUD.cs ligne 224) :**
```csharp
_xpSubLabel.Text = $"LV {XpSystem.Instance.CurrentLevel}  |  {XpSystem.Instance.CurrentXp} / {XpSystem.Instance.XpToNextLevel} XP";
```
Format identique au brief. CONFORME.

**Position :** `offset_top=103, offset_bottom=128` — dans le StatsPanelBg (bottom=130). Marge inférieure de 2px. font_size=11 ≈ 14px de hauteur de ligne. Serré mais le texte ne dépasse pas du panneau. PASS.

---

### 5. HP bar — régressions

**Drain smooth :** `_displayHpRatio` est mis à jour dans `UpdateHp()` : gain immédiat (`if >= target`), drain via `MoveToward(delta * 2.5f)`. CONFORME.

**Couleur dynamique :** `HpColor(ratio)` retourne cyan >50%, orange 25-50%, rouge <25%. Appliqué à `_hpBar.Color` et `_hpLabel.AddThemeColorOverride`. CONFORME.

**Pulsation basse vie :** tween loop sur `_hpBar.modulate:a` (0.45→1.0→0.45, 0.32s each) déclenché à `ratio < 0.25f`. Kill propre sur `_hpPulseTween` avant le reset. CONFORME.

**Glow HP :** `_hpBarGlow.Size = new Vector2(barW + 8, 20f)`, position `(-4, -1)` relative au HpBarBg. Débordement calculé : à HP=100%, le glow sort de 2px à droite du StatsPanelBg (right=308, glow_right=310). Sévérité cosmétique (alpha=0.22, 2px). Non bloquant.

---

### 6. Timer — sous-label et couleur

**Couleur dynamique :** `TimerColor(remaining)` correctement implémenté dans `UpdateTimer()`. CONFORME.

**TimerSubLabel :** nœud présent dans le .tscn avec `text = "RUNTIME ACTIVE"`. `HUD.cs` ne met jamais ce nœud à jour — il reste statiquement à "RUNTIME ACTIVE". Voir **BUG-HUD-01**.

---

### 7. Notifications armes — régressions

Les trois méthodes `ShowWeaponEquipped`, `ShowWeaponUpgraded`, `ShowPassiveAcquired` sont présentes et inchangées depuis le commit `9334f9e`. `_notifTween?.Kill()` avant chaque nouvelle notification. `WeaponNotifLabel` correctement initialisé dans `_Ready()`. PASS — aucune régression.

---

### 8. Bugs et discordances identifiés

#### BUG-HUD-01 — TimerSubLabel affiche "RUNTIME ACTIVE" au lieu de "RUNTIME ENCRYPTED"

- **Sévérité :** Cosmétique
- **Contexte :** `scenes/ui/HUD.tscn`, nœud `TimerSubLabel`, ligne 180.
- **Reproduction :** Lancer une run. Observer le sous-label sous le timer.
- **Observé :** Texte statique "RUNTIME ACTIVE" dans le .tscn. `HUD.cs` ne référence jamais ce nœud — il ne peut pas le mettre à jour.
- **Attendu (brief) :** "RUNTIME ENCRYPTED"
- **Hypothèse :** Le brief décrit le texte cible "RUNTIME ENCRYPTED" mais le .tscn a été écrit avec "RUNTIME ACTIVE" (peut-être une formulation intermédiaire). Aucun code ne met à jour ce label — il devait être figé comme décoration textuelle, mais avec le mauvais texte.
- **Correction :** Dans `scenes/ui/HUD.tscn`, nœud `TimerSubLabel`, changer `text = "RUNTIME ACTIVE"` en `text = "RUNTIME ENCRYPTED"`.
- **Assigné à :** developpeur (modification triviale du .tscn)

#### BUG-HUD-02 — CoresPanelTitle affiche "CHIMERA CORES" au lieu de "NOYAUX AETHER"

- **Sévérité :** Cosmétique
- **Contexte :** `scenes/ui/HUD.tscn`, nœud `CoresPanelTitle`, ligne 215.
- **Reproduction :** Lancer une run. Observer le titre du panneau cores haut-droite.
- **Observé :** Texte statique "CHIMERA CORES". `HUD.cs` ne référence jamais ce nœud.
- **Attendu (brief) :** "NOYAUX AETHER"
- **Hypothèse :** Même cause que BUG-HUD-01 — le titre en anglais technique "CHIMERA CORES" a été utilisé pendant le développement et n'a pas été aligné sur la terminologie française du GDD.
- **Correction :** Dans `scenes/ui/HUD.tscn`, nœud `CoresPanelTitle`, changer `text = "CHIMERA CORES"` en `text = "NOYAUX AETHER"`.
- **Assigné à :** developpeur (modification triviale du .tscn)

---

### 9. Points d'attention (non-bugs)

**Gap segments XP ~2px au lieu de 1px :** Le calcul `x = i * (segW + 1)` avec `segW = 10.95` (float) produit un gap effectif de ~1.95px entre chaque bloc. Visuellement identique à 2px. Le commentaire code dit "1 px gap between each pair" — imprécision mineure. Impact nul sur la lisibilité.

**HpBarGlow débord 2px à HP=100% :** `_hpBarGlow.Size = new Vector2(barW + 8, 20)` + `Position(-4, -1)` → à HP=100%, le glow dépasse de 2px à droite du StatsPanelBg. Alpha=0.22, 2px de liseré cyan imperceptible sur fond de jeu. Cosmétique.

**XpSubLabel marge inférieure 2px :** `offset_bottom=128` pour une hauteur de texte font_size=11 ≈ 14px. Si le texte est centré dans la zone (103-128 = 25px), la marge est confortable. Si la hauteur de police dépasse l'estimation, le texte pourrait clipper sur le bord inférieur du panneau. À surveiller en session interactive — non confirmable sans rendu à l'écran.

**TimerSubLabel non dynamique :** le nœud n'a aucun crochet en code. Si le design évolue vers un label qui change de texte selon l'état de la run (ex. "RUNTIME ENCRYPTED" → "EXTRACTION PRÊTE" à t<0), il faudra ajouter une référence dans `HUD.cs`. Pour l'instant, purement décoratif.

---

### 10. Validation des points du brief

| Point du brief | Statut | Note |
|---|---|---|
| 4 zones HUD visibles | PASS | Tous les nœuds présents et cohérents |
| Panneau stats haut-gauche (130px) | PASS | Offset bottom=130, hauteur effective 122px |
| Titre "CHIMERA PROTOCOL" | PASS | font_size=9, cyan 55% alpha |
| Barre XP segmentée 20 blocs | PASS | Générée dynamiquement en C# |
| Sous-label "LV x | y / z XP" | PASS | Format exact en HUD.cs |
| Sous-label timer "RUNTIME ENCRYPTED" | FAIL | Affiche "RUNTIME ACTIVE" — BUG-HUD-01 |
| Titre "NOYAUX AETHER" | FAIL | Affiche "CHIMERA CORES" — BUG-HUD-02 |
| HP bar — drain animé, pulsation | PASS | MoveToward 2.5/s, pulsation <25% |
| Timer couleur dynamique | PASS | 3 seuils implémentés |
| Notifications armes | PASS | Aucune régression |
| Crashes ou erreurs console | PASS | 0 erreur, 0 crash |

---

### 11. Régressions

**Fix LevelLabel autowrap (commit 6f3cb80) :** `autowrap_mode=0` et `custom_minimum_size=Vector2(44,0)` présents dans le .tscn. Fix préservé. PASS.

**Fix barre HP hauteur (commit 6f3cb80) :** `HpBar` est maintenant `offset_bottom=18` (18px hauteur), `HpBarBg` est `offset_bottom=54→72` (18px). Plus de `Size.Y=0` au premier frame grâce à la structure offset-based. PASS.

**Systèmes gameplay (XpSystem, InventorySystem, LevelUpSystem, RunStatsTracker) :** non modifiés dans les deux commits de la session HUD. Aucune régression possible. PASS.

**FusionFlash, ScreenShake, VignetteFollow :** non modifiés. PASS.

---

### 12. Briefing developpeur — BUG-HUD-01 et BUG-HUD-02

Deux corrections de texte statique dans `scenes/ui/HUD.tscn` :

**BUG-HUD-01 :** nœud `TimerSubLabel`, ligne 180 :
```
text = "RUNTIME ACTIVE"  →  text = "RUNTIME ENCRYPTED"
```

**BUG-HUD-02 :** nœud `CoresPanelTitle`, ligne 215 :
```
text = "CHIMERA CORES"  →  text = "NOYAUX AETHER"
```

Les deux nœuds sont statiques (jamais touchés par `HUD.cs`). Modification triviale du `.tscn` uniquement, aucun impact sur la compilation ni les systèmes runtime.

---

## Test HUD Sprites — 2026-06-25

**Testeur :** game-tester (agent Claude)
**Hash git :** 52e38fd (docs: GDD §16 HUD juicy + README phase courante 2026-06-25)
**Commit testé :** 52e9d6a (feat(ui): HUD juicy redesign - barres sci-fi avec glow et animations)
**Périmètre :** Intégration de 4 sprites PNG extraits de l'image concept dans le HUD en jeu.
**Sprites nouvellement intégrés :**
- `ui_panel_frame_nobg.png` (300×217 px, 58% opaque) — overlay `StatsPanelFrame`
- `ui_timer_frame_nobg.png` (168×80 px, 9% opaque) — overlay `TimerFrame`
- `ui_chimera_core_icon.png` (64×64 px, 57% opaque) — icône `CoreIconTex`
- `ui_lv_hex.png` (46×64 px, 43% opaque) — fond `LvHexBg`
**Résolution viewport jeu :** 1920×1017 (fenêtré DEBUG, barre titre 31 px)
**GPU :** AMD Radeon RX 9070, D3D12

---

### 1. Smoke test — PASS

**Compilation C# :** `dotnet build --nologo -v quiet` — 0 erreur, 0 avertissement.

**Démarrage runtime (logs console) :**
```
Godot Engine v4.7.stable.mono.official.5b4e0cb0f
D3D12 12_0 - Forward+ - Using Device #0: AMD - AMD Radeon RX 9070
[MetaProgressionSystem] 7 upgrades chargés.
[SaveManager] Sauvegarde chargée.
[MetaProgressionSystem] Prêt. Échos disponibles : 13
```
Aucune erreur, aucun crash. PASS.

---

### 2. Résultats par composant HUD

#### 2.1 Cadre panneau stats (`ui_panel_frame_nobg.png`) — FAIL BLOQUANT

**Observé :** Le sprite `StatsPanelFrame` (TextureRect, `stretch_mode=2` FIT, z_index=2) est
entièrement opaque sur 58% de sa surface et masque l'intégralité du contenu du panneau stats.
Le titre "HACKER RIG STATUS" (texte baked dans le sprite concept), l'icône coeur stylisée, la
colonne de verre cyan verticale, et le texte "MEMORY BUFFER" — tous issus du concept art
original — s'affichent par-dessus les éléments HUD réels (HP label, HP bar, XP row).

**Dimensionnement problématique :** Le sprite fait 300×217 px natifs alors que le panneau HUD
qu'il est censé surplomber (`StatsPanelBg`) fait 300×122 px. Le sprite est donc 78% plus haut que
le panneau cible. En `stretch_mode=2` (FIT), Godot redimensionne proportionnellement pour tenir
dans la box 304×126 px du `TextureRect` — le sprite est écrasé verticalement, rendant les
éléments baked encore plus denses et illisibles.

**Conséquence :** Le label "100 / 100" HP, la HP bar cyan, la XP row segmentée, le LV hex, et
le sous-label "LV x | y / z XP" sont invisibles sous l'overlay opaque. La lisibilité des stats
vitales est réduite à zéro.

**Cause :** `ui_panel_frame_nobg.png` n'est pas un overlay transparent de type "cadre". C'est
le concept art complet du panneau, avec fond teal opaque, icônes de UI et texte intégré. Le
suffixe `_nobg` est trompeur — il signifie probablement "sans background de fenêtre Windows"
(i.e. exporté depuis le logiciel de design sans le fond du canvas), pas "avec canal alpha
transparent". Le résultat est un PNG opaque sur la grande majorité de sa surface.

**Attendu :** Un cadre tech avec fond transparent, seuls les bords/coins/décorations visibles
(comparable à `ui_timer_frame_nobg.png` qui lui est majoritairement transparent à 69%).

#### 2.2 Cadre timer (`ui_timer_frame_nobg.png`) — FAIL MAJEUR

**Observé :** Double affichage du timer. `TimerLabel` (z_index=4) affiche la valeur réelle qui
décompte (ex. "14:38"). `TimerFrame` (z_index=3, TextureRect avec `ui_timer_frame_nobg.png`)
contient le chiffre "14:57" baked dans le sprite — ce chiffre est visible en permanence sous la
valeur réelle, créant un empilement de deux horloges.

**Comportement exact :** La valeur baked "14:57" du sprite reste statique tandis que la valeur
réelle diminue. À t=0:20 on voit "14:40 / 14:57" empilés. Les deux valeurs se superposent dans
la même zone visuelle, rendant le timer difficile à lire.

**Cause :** `ui_timer_frame_nobg.png` (168×80 px) contient "14:57" dessiné en blanc dans le
concept art. Ce chiffre représentait la valeur de démonstration dans le mockup graphique. Le
sprite est affiché à z_index=3, le `TimerLabel` réel à z_index=4 — les deux coexistent.

**Attendu :** Le cadre LCD doit être un overlay purement décoratif (bords, coins, effets) sans
aucune valeur numérique intégrée. Les chiffres doivent venir uniquement du `TimerLabel`.

**Note positive :** Le timer réel "14:38" (valeur en blanc) est lisible malgré la superposition,
car il est au premier plan (z_index=4) et a une police taille 32. La collision visuelle est
gênante mais pas totalement bloquante en conditions normales de jeu.

#### 2.3 Icône Chimera Core (`ui_chimera_core_icon.png`) — FAIL MINEUR

**Observé :** L'icône hexagonale n'est pas visible dans la zone du panneau Noyaux. Seul le
chiffre "0" est affiché (en violet, centré dans `CoresLabel`). Le `CoreIconTex` (`TextureRect`
avec `custom_minimum_size=Vector2(30,30)`, `size_flags_vertical=4` shrink, `expand_mode=1`)
semble absent visuellement.

**Analyse :** Le `CoresContainer` est un `HBoxContainer` ancré haut-droit
(`anchor_left=1, offset_left=-184, offset_right=-8, offset_top=24, offset_bottom=62`), soit une
zone de 176×38 px. `CoreIconTex` avec `custom_minimum_size=30×30` et `CoresLabel` avec
`size_flags_horizontal=3` (expand). Dans une boîte de 176 px, l'icône de 30 px devrait être
visible à gauche du chiffre. Il est possible que le fond noir du sprite (4% de pixels
transparents seulement sur ses 64×64 natifs) le rende invisible sur le fond sombre du panneau
(`Color(0.04, 0.04, 0.10)`) — l'icône est rendue mais ses couleurs internes (violet foncé, bleu
foncé) se fondent dans le fond quasi-identique.

**Attendu :** Une icône hexagonale distincte, lisible sur le fond sombre violet-noir.

**Recommandation :** Augmenter la luminosité/contraste du sprite via `Modulate` dans le `.tscn`,
ou s'assurer que le sprite a un contour visible sur fond sombre.

#### 2.4 Hexagone LV (`ui_lv_hex.png`) — FAIL MAJEUR

**Observé :** La zone `LvHexBg` est noire — l'hexagone n'est pas visible. La crop de 48×26 px
sur la zone du LvHexBg retourne un rectangle entièrement noir.

**Analyse :** `LvHexBg` est une `TextureRect` dans `XpRow` avec `custom_minimum_size=Vector2(44,24)`,
`stretch_mode=5` (KeepAspectCentered). Le sprite `ui_lv_hex.png` fait 46×64 px — ratio 0.72 en
portrait. La zone allouée est 44×24 px — ratio 1.83 en paysage. En `KeepAspectCentered`, le
sprite est réduit pour tenir dans la hauteur (24 px → largeur calculée 24×0.72=17 px) et centré.
L'icône hexagone fait donc effectivement 17 px de large au lieu de 44 px attendus.

**Problème secondaire :** Le sprite `ui_lv_hex.png` contient "LV 1" baked dans l'image (texte
blanc intégré dans le mockup). Le `LevelLabel` placé par-dessus (`text = "LV 1"`) créerait un
double affichage identique au problème du timer si le sprite était visible.

**Cause racine :** Le sprite est orienté portrait (46×64) alors que la zone d'accueil est
paysage (44×24). En `KeepAspectCentered`, la hauteur 24 px contraint la largeur à ~17 px, trop
petit pour être visible. De plus, le sprite contient du contenu baked.

**Attendu :** Un hexagone paysage (large > haut, ex. 64×48 px) sans texte intégré, utilisable
comme fond derrière le `LevelLabel` dynamique.

#### 2.5 Coins L-bracket — PASS

**Observé :** Les 8 `ColorRect` L-bracket sur le panneau stats (cyan `Color(0.267,1,0.933,0.85)`)
et les 8 L-bracket sur le panneau timer (même cyan) et les 8 L-bracket sur le panneau noyaux
(violet `Color(0.667,0.267,1,0.85)`) sont tous présents et visibles dans le `.tscn`.

**Note :** Les brackets cyan du panneau stats sont visibles à l'écran malgré l'overlay `StatsPanelFrame`
opaque, car ils sont à z_index=0 (default) et le `StatsPanelFrame` est à z_index=2 — les brackets
sont EN DESSOUS de l'overlay. Ce qui paraît être un "pass" visuel est en réalité les brackets
partiellement cachés. Sur les captures, seul le bracket TL du panneau stats est visible en
haut à cause de la position du sprite concept qui ne les couvre pas entièrement.

**Couleurs :** Cyan stats + violet noyaux — cohérents avec la palette UI. PASS formel.

#### 2.6 Régressions systèmes HUD — PASS

**HP bar :** Fonctionnelle. La barre cyan `HpBar` est présente dans la scène. La logique
`UpdateHp()` en `_Process()` est inchangée. La couleur dynamique (>50% cyan / 25-50% orange /
<25% rouge-rouille) et la pulsation <25% restent opérationnelles en code.

**Note :** La HP bar est invisible à l'écran pendant la session de test car masquée par le
sprite `StatsPanelFrame` opaque (BUG-HUD-03). Le chiffre "HP/MaxHP" du `HpLabel` n'est pas
lisible non plus.

**Timer couleur dynamique :** Le `TimerLabel` change bien de couleur en fonction du temps
restant (blanc cassé normal, orange warning, rouge-rouille danger). La logique `UpdateTimer()`
et `TimerColor()` sont inchangées. PASS fonctionnel, lecture gênée par le double-affichage.

**Barre XP segmentée :** Les 20 segments `ColorRect` générés par `BuildXpSegments()` sont
fonctionnels en code. Non vérifiable visuellement car masqués par l'overlay `StatsPanelFrame`.

**Notifications armes :** `WeaponNotifLabel` à bas d'écran centré visible pendant la session
("Champ de Surcharge" notification or observée). PASS.

**Level-Up screen :** Déclenché correctement dès ~20s de jeu (courbe XP accélérée), jeu en
pause, 3 cartes avec raretés colorées affichées (Commun violet, Rare violet foncé). PASS.

#### 2.7 Crashes ou erreurs console — PASS

Aucun crash, aucune erreur Godot console durant les sessions de test.

---

### 3. Tableau récapitulatif

| Composant | Statut | Détail |
|---|---|---|
| Cadre panneau stats (`ui_panel_frame_nobg.png`) | FAIL BLOQUANT | Sprite opaque 58% masque HP/XP/LV |
| Cadre timer (`ui_timer_frame_nobg.png`) | FAIL MAJEUR | Double affichage — "14:57" baked + valeur réelle |
| Icône Chimera Core (`ui_chimera_core_icon.png`) | FAIL MINEUR | Invisible — contraste insuffisant sur fond sombre |
| Hexagone LV (`ui_lv_hex.png`) | FAIL MAJEUR | Portrait 46×64 dans zone paysage 44×24 → 17px wide non-visible + "LV 1" baked |
| Coins L-bracket | PASS | Présents, couleurs correctes (partiellement cachés sous panel overlay) |
| HP bar dynamique | PASS (fonctionnel) | Logique OK, invisible sous overlay panel frame |
| Timer couleur dynamique | PASS (fonctionnel) | Logique OK, lecture gênée par double-affichage |
| Barre XP segmentée | PASS (fonctionnel) | Logique OK, invisible sous overlay panel frame |
| Notifications armes | PASS | Visibles et fonctionnelles |
| Level-Up screen | PASS | Déclenché correctement, cartes lisibles |
| Crashes console | PASS | 0 erreur, 0 crash |

---

### 4. Verdict global — FAIL

**4 bugs identifiés, dont 1 bloquant et 2 majeurs.** Le HUD est injouable en l'état : les stats
vitales HP et XP sont masquées par l'overlay du panneau stats. Le timer est difficile à lire.
L'hexagone LV est invisible.

---

### 5. Bugs documentés

#### BUG-HUD-03 — StatsPanelFrame masque la totalité du panneau stats

**Sévérité :** Bloquant
**Contexte :** `scenes/ui/HUD.tscn`, nœud `StatsPanelFrame` (TextureRect, z_index=2), texture `assets/sprites/ui/ui_panel_frame_nobg.png`
**Reproduction :** Lancer une run. Regarder le panneau stats haut-gauche.
**Observé :** Le sprite concept art (fond teal opaque 58%, avec "HACKER RIG STATUS", icône coeur, "MEMORY BUFFER", "LV 1 | 0/5 [XP: DATA_S..." intégrés) recouvre HP label, HP bar, XP row, LV hex et XP sub-label. Rien du contenu dynamique n'est lisible.
**Attendu :** Un cadre technique transparent avec uniquement des bords/coins/ornements visibles, le fond du sprite étant transparent (alpha=0).
**Hypothèse :** `ui_panel_frame_nobg.png` est le concept art complet exporté tel quel, pas un overlay cadre. Le sprite doit être recréé avec fond alpha=0 — seules les décorations de bord doivent être opaques.
**Assigné à :** graphiste (régénérer le sprite) + developpeur (valider le rendu en jeu)

#### BUG-HUD-04 — TimerFrame affiche "14:57" statique superposé au timer réel

**Sévérité :** Majeur
**Contexte :** `scenes/ui/HUD.tscn`, nœud `TimerFrame` (TextureRect, z_index=3), texture `assets/sprites/ui/ui_timer_frame_nobg.png`
**Reproduction :** Lancer une run. Observer le panneau timer central.
**Observé :** Deux valeurs empilées dans la même zone — la valeur réelle (blanc, décompte) et "14:57" baked dans le sprite (gris-blanc, statique).
**Attendu :** Seul le `TimerLabel` doit afficher des chiffres. Le cadre LCD ne doit pas contenir de valeur numérique.
**Hypothèse :** Le sprite contient "14:57" comme valeur de démonstration dans le mockup. Il faut régénérer le sprite sans cette valeur, ou l'effacer (la zone du sprite est à 69% transparente — les chiffres "14:57" représentent une partie des 9% opaques restants).
**Assigné à :** graphiste (effacer les chiffres du sprite timer) + developpeur (vérifier z_index et superposition)

#### BUG-HUD-05 — LvHexBg invisible : orientation portrait incompatible avec zone paysage

**Sévérité :** Majeur
**Contexte :** `scenes/ui/HUD.tscn`, nœud `XpRow/LvHexBg` (TextureRect, `custom_minimum_size=Vector2(44,24)`, `stretch_mode=5`), texture `assets/sprites/ui/ui_lv_hex.png` (46×64 px)
**Reproduction :** Lancer une run. Observer la barre XP — le fond hexagone devant "LV 1" est absent.
**Observé :** Zone entièrement noire — sprite non-visible. En `stretch_mode=5` (KeepAspectCentered) sur une zone 44×24 px, le sprite portrait 46×64 est contraint à 17×24 px — trop petit pour être perceptible.
**Problème secondaire :** Le sprite contient "LV 1" baked — si visible, double affichage avec `LevelLabel`.
**Attendu :** Un hexagone paysage (largeur > hauteur, ex. 64×40 px) sans texte intégré, s'étalant sur les ~44 px de large disponibles.
**Assigné à :** graphiste (refaire le sprite en format paysage sans texte baked) + developpeur (ajuster `custom_minimum_size` si nécessaire)

#### BUG-HUD-06 — Icône Chimera Core invisible sur fond sombre

**Sévérité :** Mineur
**Contexte :** `scenes/ui/HUD.tscn`, nœud `CoresContainer/CoreIconTex` (TextureRect, `custom_minimum_size=30×30`), texture `assets/sprites/ui/ui_chimera_core_icon.png` (64×64 px)
**Reproduction :** Lancer une run. Observer le panneau Noyaux haut-droit.
**Observé :** Seul "0" (violet) est visible dans le panneau Noyaux. L'icône hexagonale attendue à gauche du chiffre n'est pas visible.
**Hypothèse A :** Contraste insuffisant — l'icône a des couleurs sombres (bleu-violet foncé) qui se fondent dans le fond `Color(0.04, 0.04, 0.10)`.
**Hypothèse B :** Le nœud `CoreIconTex` est correctement présent dans le `.tscn` mais l'icône est trop petite (30px de min-size) ou le layout la pousse hors de la zone visible.
**Attendu :** Une icône hexagonale lisible, distincte du fond noir.
**Assigné à :** developpeur (vérifier la taille effective rendue, ajouter `Modulate` si couleurs trop sombres) + graphiste (s'assurer que le sprite a suffisamment de contraste sur fond sombre)

---

### 6. Briefing developpeur — BUG-HUD-03 à BUG-HUD-06

**BUG-HUD-03 (Bloquant) :** Dans `HUD.tscn`, le nœud `StatsPanelFrame` doit avoir sa texture
remplacée par une version recréée avec fond alpha=0. En attendant, une solution de contournement
immédiate est de passer `modulate = Color(1,1,1,0)` sur `StatsPanelFrame` pour le rendre
invisible, ou de le supprimer temporairement du `.tscn`. Cela restaure la lisibilité du panneau
stats sans autre modification de code.

**BUG-HUD-04 (Majeur) :** Dans `HUD.tscn`, le nœud `TimerFrame` peut rester en place si le
graphiste produit une version du sprite sans les chiffres "14:57". En attendant, même correction
de contournement : `modulate = Color(1,1,1,0)` sur `TimerFrame`.

**BUG-HUD-05 (Majeur) :** Dans `HUD.tscn`, nœud `XpRow/LvHexBg` :
- Changer `custom_minimum_size` pour `Vector2(48, 24)` et `stretch_mode=6` (KeepAspectExpanded)
  dès que le sprite recréé en format paysage est disponible.
- Ou, en attendant le sprite corrigé, utiliser `stretch_mode=3` (Scale) pour ignorer l'aspect
  ratio et étaler le sprite sur toute la zone 44×24 px.

**BUG-HUD-06 (Mineur) :** Dans `HUD.tscn`, nœud `CoreIconTex` :
- Ajouter `modulate = Color(2.0, 2.0, 2.0, 1.0)` pour surexposer les couleurs de l'icône et
  la rendre visible sur fond sombre.
- Ou augmenter `custom_minimum_size` à `Vector2(36,36)` si la taille 30px est trop petite.

---

## Test HUD Corrections — 2026-06-25

**Testeur :** game-tester (agent Claude)
**Hash git :** 52e38fd (docs: GDD §16 HUD juicy + README phase courante 2026-06-25)
**Périmètre :** Validation des corrections BUG-HUD-03/04/05/06 appliquées par le développeur.
**Méthode :** Analyse statique `HUD.tscn` + analyse pixel sprites Python/Pillow + smoke test runtime `--quit-after 12 --scene Game.tscn`.
**Device GPU :** AMD Radeon RX 9070, D3D12 Forward+

---

### 1. Smoke test — PASS

**Compilation C# :** 0 erreur, 0 warning (build précédent validé, aucune modification C#).

**Démarrage runtime (Game.tscn, --quit-after 12) :**
```
Godot Engine v4.7.stable.mono.official.5b4e0cb0f
D3D12 12_0 - Forward+ - Using Device #0: AMD - AMD Radeon RX 9070
[MetaProgressionSystem] 7 upgrades chargés.
[SaveManager] Sauvegarde chargée.
[MetaProgressionSystem] Prêt. Échos disponibles : 13
[InventorySystem] Arme existante enregistrée : impulse_cannon niveau 1
[EnemySpawner] 6 types d'ennemis chargés.
```
Aucune erreur GetNode, aucun crash. Les 12 secondes de run passent sans sortie d'erreur. PASS.

Les avertissements "ObjectDB instances leaked" et "1 resources still in use" au shutdown de
`--quit-after` sont normaux (tweens/resources coupés sans cleanup propre par le signal d'arrêt
forcé) — non reproductibles en session normale.

---

### 2. Vérification des 4 corrections

#### 2.1 BUG-HUD-03 — StatsPanelFrame (était : masquait tout le panneau stats)

**Correction appliquée :** `modulate = Color(1, 1, 1, 0)` dans `HUD.tscn`, nœud `StatsPanelFrame`.

**Vérification sprite :** Analyse pixel `ui_panel_frame_nobg.png` (300×217 px) — 91% de pixels
opaques (59 543/65 100). Le sprite reste intrinsèquement opaque : il s'agit du concept art complet
avec fond teal, "HACKER RIG STATUS", "MEMORY BUFFER" etc. La correction est un **workaround**
(masquage via alpha=0), pas une correction du sprite lui-même.

**Impact :** Avec alpha=0, le sprite est invisible. Le panneau stats est libéré : HP label, HP bar,
XP segmentée, XpSubLabel, LvHexBg sont à nouveau accessibles visuellement. WORKAROUND VALIDE.

**Résidu :** Le sprite opaque 91% reste dans le projet comme asset inutilisable pour son rôle
initial. À terme, le graphiste devra produire un vrai cadre overlay (fond transparent, seuls les
bords opaques). Non-bloquant pour la jouabilité actuelle.

**Statut correction :** PASS (workaround opérationnel)

#### 2.2 BUG-HUD-04 — TimerFrame (était : "14:57" statique superposé au timer réel)

**Correction appliquée :** `modulate = Color(1, 1, 1, 0)` dans `HUD.tscn`, nœud `TimerFrame`.

**Vérification sprite :** Analyse pixel `ui_timer_frame_nobg.png` (168×80 px) — 23% opaques
(3 134/13 440). Le sprite contient toujours "14:57" baked dans ses pixels opaques. La correction
masque le sprite — double affichage éliminé.

**Impact :** Le `TimerLabel` (z_index=4, font_size=32, "15:00" → décompte dynamique) s'affiche
seul. Plus de collision visuelle. WORKAROUND VALIDE.

**Résidu :** Même situation que BUG-HUD-03 — le sprite doit être nettoyé (suppression des chiffres
baked) par le graphiste pour une intégration propre à terme.

**Statut correction :** PASS (workaround opérationnel)

#### 2.3 BUG-HUD-05 — LvHexBg (était : sprite portrait 46×64 dans zone paysage 44×24, invisible)

**Correction appliquée :** `ui_lv_hex.png` régénéré en **44×26 px** format paysage flat-top sans
texte baked. `custom_minimum_size` ajusté de `Vector2(44, 24)` à `Vector2(44, 26)`.

**Vérification sprite :** Analyse pixel `ui_lv_hex.png` — 44×26 px, 14% opaques (164/1 144).
14% opaques = seuls les bords/contour du hexagone sont tracés, le fond est transparent. Aucun
texte baked détecté. Structure conforme à un overlay cadre hexagonal. CORRECTION RÉELLE.

**Compatibilité stretch_mode=5 (KeepAspectCentered) :** Le sprite 44×26 dans une zone 44×26 px
a un ratio identique (1.69:1). Godot l'affichera à 100% de sa taille native, centré. Pas de
distorsion, pas de réduction. L'hexagone couvrira toute la zone disponible.

**LevelLabel centré dedans :** offsets -22/-9/+22/+9 → zone 44×18 px, centrée dans le hex 44×26.
Texte "LV 1" font_size=12 ≈ 14px hauteur. Tient confortablement. Pas de doublon de texte. PASS.

**Statut correction :** PASS (correction réelle du sprite)

#### 2.4 BUG-HUD-06 — CoreIconTex (était : icône invisible sur fond sombre)

**Correction appliquée :** `modulate = Color(2.0, 2.0, 2.0, 1.0)` dans `HUD.tscn`, nœud
`CoreIconTex`.

**Vérification sprite :** Analyse pixel `ui_chimera_core_icon.png` (64×64 px) — 92% opaques
(3 793/4 096). Distribution couleurs dominantes :
- `RGB(0,0,0)` : 1 372 pixels (36% de la surface opaque)
- `RGB(0,0,64)` + `RGB(0,64,64)` + teintes teal/violet sombres : 50%+ restants

**Effet du modulate ×2 :** Multiplie chaque canal RGB par 2.0. Impact :
- Noirs purs `RGB(0,0,0)` → restent `RGB(0,0,0)` (aucune amélioration)
- `RGB(0,64,64)` → `RGB(0,128,128)` (teal visible)
- `RGB(128,128,128)` → `RGB(255,255,255)` (blanc saturé)

**Lisibilité résiduelle :** 36% de la surface de l'icône reste noire après modulate ×2, se
fondant dans le fond `Color(0.04, 0.04, 0.10)` (`RGB(10,10,26)`). L'icône sera **partiellement
visible** : les bords teal et les pixels gris deviennent lisibles, mais les zones noires internes
se fondent dans le fond. À 30×30 px de rendu final, l'effet sera un hexagone avec des contours
visibles mais un intérieur sombre.

**Jugement :** Amélioration réelle par rapport à l'invisibilité totale précédente. À 30×30 px sur
fond `#0A0A1A`, les contours teal `RGB(0,128,128)` sont suffisamment contrastés pour être
reconnaissables. Acceptable pour un MVP.

**Statut correction :** PASS CONDITIONNEL — visible mais non optimale. Voir BUG-HUD-06b
ci-dessous pour le résidu.

---

### 3. Vérification des corrections texte (BUG-HUD-01/02)

Confirmées dans la session précédente, vérifiées en statique :

| Nœud | Attendu | Présent dans .tscn | Statut |
|---|---|---|---|
| `TimerSubLabel` | "RUNTIME ENCRYPTED" | `text = "RUNTIME ENCRYPTED"` | PASS |
| `CoresPanelTitle` | "NOYAUX AETHER" | `text = "NOYAUX AETHER"` | PASS |

---

### 4. Vérification GetNode — intégrité de navigation

Tous les 11 paths `GetNode` de `HUD.cs` validés contre le `.tscn` courant (post-corrections) :

| Chemin GetNode | Nœud présent | Statut |
|---|---|---|
| `HpHeader/HpLabel` | PASS | PASS |
| `HpBarBg/HpBar` | PASS | PASS |
| `HpBarBg/HpBarGlow` | PASS | PASS |
| `XpRow/LvHexBg/LevelLabel` | PASS | PASS |
| `XpRow/XpBarBg` | PASS | PASS |
| `XpRow/XpBarBg/XpBar` | PASS | PASS |
| `XpRow/XpBarBg/XpBarGlow` | PASS | PASS |
| `XpSubLabel` | PASS | PASS |
| `TimerLabel` | PASS | PASS |
| `CoresContainer/CoresLabel` | PASS | PASS |
| `WeaponNotifLabel` | PASS | PASS |

Aucun `NullReferenceException` possible au démarrage. PASS.

---

### 5. Régressions systèmes

**HP bar drain animé :** `UpdateHp()` + `MoveToward(delta * 2.5f)` inchangé. `HpBar` ColorRect
accessible (chemin `HpBarBg/HpBar` intact, nœud non modifié). PASS.

**Timer couleur dynamique :** `UpdateTimer()` + `TimerColor()` inchangés. `TimerLabel` accessible.
Le `TimerFrame` masqué (alpha=0) ne perturbe pas le `TimerLabel` (z_index=4). PASS.

**Barre XP segmentée :** `BuildXpSegments()` en `CallDeferred`, 20 `ColorRect` créés dans
`XpBarBg`. Nœuds `XpRow/XpBarBg` et `XpRow/LvHexBg` toujours présents. `LvHexBg` n'est plus
invisible (hex 44×26 visible). PASS.

**Notifications armes :** `WeaponNotifLabel` inchangé. Méthodes `ShowWeaponEquipped/Upgraded/
PassiveAcquired` inchangées. Pas de régression possible. PASS.

**Flash level-up joueur :** Logique `OnLevelUp()` inchangée — flash `_xpBarGlow` (modulate 2x) +
segments (modulate 3x) + tween 0.5-0.6s. `StatsPanelFrame` rendu invisible ne masque plus la
barre XP — le flash sera visible. PASS (amélioration vs état précédent).

**ScreenShake, VignetteFollow, FusionFlash :** non modifiés. PASS.

---

### 6. Bug résiduel identifié

#### BUG-HUD-06b — CoreIconTex : zones noires de l'icône fondues dans le fond après modulate ×2

- **Sévérité :** Mineur (amélioration vs invisible total, mais lisibilité partielle)
- **Contexte :** `scenes/ui/HUD.tscn`, nœud `CoresContainer/CoreIconTex`, texture
  `assets/sprites/ui/ui_chimera_core_icon.png`.
- **Observé :** L'icône `ui_chimera_core_icon.png` a 36% de sa surface opaque en noir pur
  `RGB(0,0,0)`. Avec `modulate Color(2,2,2,1)`, ces noirs restent noirs. Sur fond
  `Color(0.04,0.04,0.10)` (`RGB(10,10,26)`), les zones noires de l'icône sont
  indiscernables du fond. Seuls les contours teal et les pixels gris (≈50% de la surface)
  bénéficient réellement du ×2.
- **Attendu :** Une icône intégralement lisible sur fond sombre — soit un sprite avec fond
  transparent et contenu claire (teal, violet, blanc), soit un modulate encore plus fort
  (×3 ou ×4) si le sprite est conservé.
- **Correction recommandée (option A) :** `modulate = Color(4.0, 4.0, 4.0, 1.0)` — multiplie
  les noirs (restent 0), mais les teintes `RGB(0,64,64)` → `RGB(0,255,255)` (cyan pur,
  très visible). Risque de surexposition excessive sur les pixels déjà gris.
- **Correction recommandée (option B) :** Le graphiste régénère `ui_chimera_core_icon.png`
  avec des couleurs claires sur fond transparent (ex. hexagone violet/cyan lumineux `#AA44FF`
  / `#44FFEE`, fond alpha=0). `modulate` revient à `Color(1,1,1,1)`.
- **Assigné à :** graphiste (option B, priorité basse) / developpeur (option A si workaround
  rapide acceptable)

---

### 7. Tableau récapitulatif

| Composant | Bug précédent | Correction appliquée | Statut post-correction |
|---|---|---|---|
| StatsPanelFrame | BLOQUANT — masquait tout | modulate alpha=0 | PASS (workaround) |
| TimerFrame | MAJEUR — "14:57" statique | modulate alpha=0 | PASS (workaround) |
| LvHexBg hex | MAJEUR — portrait 17px wide invisible | Sprite régénéré 44×26 | PASS (correction réelle) |
| CoreIconTex | MINEUR — invisible sur fond sombre | modulate Color(2,2,2,1) | PASS CONDITIONNEL |
| TimerSubLabel texte | COSMETIQUE — "RUNTIME ACTIVE" | text = "RUNTIME ENCRYPTED" | PASS |
| CoresPanelTitle texte | COSMETIQUE — "CHIMERA CORES" | text = "NOYAUX AETHER" | PASS |
| HP bar drain animé | — | non modifié | PASS (pas de régression) |
| Timer couleur dynamique | — | non modifié | PASS (pas de régression) |
| XP barre segmentée | — | non modifié | PASS (pas de régression) |
| Notifications armes | — | non modifié | PASS (pas de régression) |
| Crashes console | — | — | PASS (0 erreur) |

---

### 8. Verdict global

**PASS avec réserve.**

Les 4 corrections BUG-HUD-03/04/05/06 sont opérationnelles :
- Le panneau stats est **libéré** — HP label, HP bar, XP segmentée et LvHexBg sont accessibles.
- Le timer affiche **une seule valeur** — le double affichage est éliminé.
- Le hexagone LV est **visible** avec le sprite 44×26 px correctement dimensionné.
- L'icône Chimera Core est **partiellement visible** — amélioration réelle vs invisibilité totale.

Réserves :
1. **BUG-HUD-03 et BUG-HUD-04 sont des workarounds** (sprites toujours défectueux mais cachés).
   La lisibilité est restaurée mais les sprites `ui_panel_frame_nobg.png` (91% opaque, contenu
   baked) et `ui_timer_frame_nobg.png` (chiffres baked) doivent être recréés proprement par le
   graphiste pour une intégration durable.
2. **BUG-HUD-06b résiduel** (Mineur) : l'icône Chimera Core est partiellement lisible (contours
   teal) mais les zones noires (~36%) se fondent dans le fond. Workaround ×2 insuffisant.

En l'état, le HUD est **jouable et fonctionnel** — toutes les informations vitales (HP, timer, XP,
noyaux, niveau) sont lisibles. Les réserves sont cosmétiques et n'impactent pas le gameplay.

---

### 9. Briefing developpeur

**BUG-HUD-06b :** Si le graphiste n'est pas disponible immédiatement, changer dans `HUD.tscn`
nœud `CoreIconTex` :
```
modulate = Color(2.0, 2.0, 2.0, 1.0)  →  modulate = Color(4.0, 3.0, 3.5, 1.0)
```
Cela saturera les canaux R et B vers le blanc sur les teintes violet-teal existantes. Risque
de surexposition sur les pixels gris (→ blanc), mais l'icône sera intégralement distincte du
fond noir.

---

### 10. Briefing graphiste

**BUG-HUD-03 (priorité haute) :** Refaire `ui_panel_frame_nobg.png` comme un vrai overlay cadre :
fond entièrement transparent (alpha=0), seuls les bords/coins/ornements visibles. Dimensions
idéales : 300×122 px (correspondant à la zone StatsPanelBg), pas 300×217 (le concept art complet).
Supprimer tout texte baked ("HACKER RIG STATUS", "MEMORY BUFFER").

**BUG-HUD-04 (priorité haute) :** Refaire `ui_timer_frame_nobg.png` sans les chiffres "14:57".
Garder uniquement les bords LCD, les accents latéraux, les ornements coins. Dimensions : 160×72 px
(zone TimerBg = 144×62, avec 8px de marge cadre). Fond transparent.

**BUG-HUD-06b (priorité basse) :** Refaire `ui_chimera_core_icon.png` avec des couleurs claires
sur fond transparent. Couleurs suggérées : contour violet `#AA44FF`, remplissage cyan `#44FFEE`,
fond alpha=0. Taille 64×64 px conservée. Le `modulate` dans le .tscn reviendra à
`Color(1,1,1,1)` une fois le sprite correct.

---

## Test HUD Retouche HSV — 2026-06-25

**Testeur :** game-tester (agent Claude)
**Hash git :** 52e38fd (docs: GDD §16 HUD juicy + README phase courante 2026-06-25)
**Périmètre :** Validation des 3 sprites retouchés par masquage HSV (conservation pixels cyan/violet
uniquement) et réactivés dans `HUD.tscn` (modulate alpha=0 retiré sur StatsPanelFrame et TimerFrame,
modulate ×2.0 retiré sur CoreIconTex).
**Méthode :** Analyse pixel Python/Pillow sur les 3 sprites + lecture visuelle PNG + vérification
statique `HUD.tscn` + smoke test runtime (démarrage, 0 erreur console).
**Device GPU :** AMD Radeon RX 9070, D3D12 Forward+

---

### 1. Smoke test — PASS

**Démarrage runtime :**
```
Godot Engine v4.7.stable.mono.official.5b4e0cb0f
D3D12 12_0 - Forward+ - Using Device #0: AMD - AMD Radeon RX 9070
[MetaProgressionSystem] 7 upgrades chargés.
[SaveManager] Sauvegarde chargée.
[MetaProgressionSystem] Prêt. Échos disponibles : 13
```
0 erreur C#, 0 crash, 0 avertissement GetNode. PASS.

**Vérification `HUD.tscn` — état modulate :**
Seul `WeaponNotifLabel` a `modulate = Color(1,1,1,0)` (alpha=0 par design — la notif est invisible
par défaut et s'anime à la demande). `StatsPanelFrame`, `TimerFrame` et `CoreIconTex` n'ont aucun
`modulate` dans le `.tscn` — les sprites retouchés sont actifs à `Color(1,1,1,1)`. CONFIRMÉ.

---

### 2. Analyse sprite par sprite

#### 2.1 StatsPanelFrame — `ui_panel_frame_nobg.png`

**Métriques alpha (300×217 px) :**
- Pixels opaques (alpha > 10) : **26.2%** (17 065 / 65 100)
- Avant retouche (session précédente) : **91%** — réduction confirmée.
- Couleur moyenne des pixels opaques : R=50 G=138 B=147 — teal/cyan dominant.
- Décomposition : ~35% cyan franc (G>160, B>140, R<120) + ~25% sombre (R<80, G<80, B<100) +
  ~40% teintes intermédiaires teal.

**Lecture visuelle du PNG :** Le contenu du concept art est encore PRÉSENT. On distingue
nettement : barre titre "HACKER RIG STATUS", barre HP, zone "MEMORY BUFFER", texte "LV 1",
segments de barre XP baked, icône coeur, robot humanoïde, boutons "PING" et "FIREWALL". Ces
éléments ont survécu au masquage HSV parce qu'ils sont dessinés en teintes cyan/teal —
précisément les teintes conservées par le filtre. Le fond sombre uniforme a bien été rendu
transparent, mais le contenu graphique (qui est lui aussi cyan) a été conservé.

**Impact en jeu :** Le sprite posé en z_index=2 sur le panneau stats (dimensions 6→310 px H,
6→132 px V) affichera une superposition semi-transparente représentant le concept art partiel à
26% d'opacité. À 26%, le fond StatsPanelBg (`Color(0.04, 0.04, 0.10, 0.90)`) reste visible, mais
les éléments cyan du concept art (barre HP baked, texte "HACKER RIG STATUS", etc.) s'affichent
par-dessus les barres dynamiques HP/XP en `Color(0.267,1,0.933)` — même teinte. Résultat : les
éléments baked cyan du sprite se superposent et se confondent avec les éléments UI réels.

**Verdict : FAIL — amélioration insuffisante.** Le sprite à 26% n'est plus bloquant (le panneau
est lisible à travers lui), mais crée une confusion visuelle entre éléments statiques baked et
éléments dynamiques réels. La HP bar baked du concept art à 26% d'opacité se superpose à la HP
bar dynamique — à 100% HP les deux se chevauchent de façon indiscernable ; à 30% HP la HP bar
réelle (réduite, rouge) contraste avec la HP bar baked (pleine largeur, cyan 26%) créant une
lecture ambigüe.

**Statut : FAIL (amélioration partielle, confusion visuelle subsistante)**

#### 2.2 TimerFrame — `ui_timer_frame_nobg.png`

**Métriques alpha (151×72 px — légèrement redimensionné vs original 168×80) :**
- Pixels opaques (alpha > 10) : **5.2%** (568 / 10 872)
- Avant retouche : **23%** — réduction de 23% à 5.2% confirmée.
- Distribution spatiale : pixels opaques exclusivement sur bord gauche (x=0–30, 13.1% de densité)
  et bord droit (x=121–151, 13.2%), centre entièrement transparent (x=30–121 : 0.0%).
- 61.3% des pixels opaques sont cyan (G>130, B>110, R<120).

**Contenu visible :** Deux barres/crochets verticaux, un à gauche et un à droite. Centre vide.
Aucun chiffre "14:57" détecté — les pixels du centre qui portaient les chiffres sont tous passés
à alpha=0. L'absence des chiffres est confirmée par la distribution spatiale (0% d'opacité sur
toute la zone centrale x=30–121).

**Note dimensionnelle :** Le sprite retouché mesure 151×72 px contre 168×80 px original. La
différence de 17×8 px peut provenir du recadrage de la zone de crop lors du masquage. Le nœud
`TimerFrame` dans `HUD.tscn` est positionné avec `offset_left=-80, offset_right=80` (largeur
160px) en `stretch_mode=2` (Scale). Le sprite 151px sera légèrement étiré à 160px — imperceptible.

**Impact en jeu :** Deux crochets/accents verticaux cyan sur les côtés du timer, fond transparent,
aucun chiffre parasite. Les AccentL/AccentR ColorRect déjà présents dans le HUD (`Color(0.267,
1.0, 0.933, 0.6)`, 5px de large) couvrent exactement la même zone que ces crochets. La
superposition des crochets du sprite (cyan 5.2%) sur les AccentL/AccentR ColorRect (cyan 60%)
produira un léger renforcement visuel des accents — imperceptible ou légèrement positif.

**Verdict : PASS.** Les chiffres "14:57" sont absents. Le sprite réduit à 5.2% d'opacité n'apporte
qu'un léger renfort aux accents latéraux déjà présents — pas de confusion visuelle, pas de
double-affichage.

**Statut : PASS**

#### 2.3 CoreIconTex — `ui_chimera_core_icon.png`

**Métriques alpha (56×56 px — redimensionné vs 64×64 px original) :**
- Pixels opaques : **86.1%** (2 700 / 3 136)
- Avant retouche : **92%** (3 793 / 4 096) — légère réduction.
- Couleur moyenne pixels opaques : R=70 G=109 B=140 — bleu-teal moyen.
- Cyan : 557 px, Violet : 122 px, Dark (R<60,G<60,B<60) : 82 px — majorité de la surface est
  en teintes de bleu-teal intermédiaires (700+ pixels non catégorisés dans ces 3 groupes).

**Lecture visuelle du PNG :** Hexagone distinctement visible avec motif de circuit intérieur.
Fond du sprite transparent, contenu visible en violet/teal/cyan. L'aspect général est un badge
hexagonal tech avec détails internes. Les couleurs de fond noir ont bien été retirées (fond
transparent), les couleurs saturées violettes/cyan ont été amplifiées ×1.8.

**Comportement dans HUD.tscn :** Le nœud `CoreIconTex` n'a aucun `modulate` dans le `.tscn`
(modulate ×2.0 retiré). Le sprite de 56×56 px est rendu dans `custom_minimum_size=Vector2(30,30)`
avec `stretch_mode=5` (KeepAspectCentered). Ratio sprite : 1:1 → s'affiche à 30×30 px centré.

**Impact en jeu :** Avec fond transparent et couleurs amplifiées ×1.8, l'icône sera visible sur
fond sombre `Color(0.04, 0.04, 0.10)`. Les teintes bleu-teal moyennes (R=70, G=109, B=140) ont
une luminosité relative de ~42% — suffisant pour être distinguées du fond quasi-noir (luminosité
~4%). La partie violette et les détails internes seront lisibles. L'absence de fond noir opaque
est la correction principale — c'était le défaut qui rendait l'icône un rectangle noir uniforme.

**Résidu :** Le sprite 56×56 (redimensionné de l'original 64×64) avec `stretch_mode=5` dans une
zone 30×30 px donne une icône 30×30 px — la résolution est serrée mais acceptable pour une icône
de statut. Le rendu final à l'écran sera un hexagone tech bleu-violet-cyan sur fond sombre, lisible.

**Verdict : PASS.** L'icône sera visible et reconnaissable. La correction fond transparent est
effective. BUG-HUD-06b (zones noires internes) est atténué car les noirs constituaient principalement
le fond qui est maintenant transparent — le contenu du sprite a des couleurs bleu-teal suffisamment
contrastées.

**Statut : PASS**

#### 2.4 LvHexBg — `ui_lv_hex.png`

Non modifié depuis la session précédente (correction réelle validée — sprite 44×26 paysage,
contour hexagonal cyan uniquement, pas de texte baked). PASS inchangé.

---

### 3. Tableau récapitulatif

| Composant | Statut avant HSV | Correction HSV | Statut actuel | Verdict |
|---|---|---|---|---|
| StatsPanelFrame | FAIL BLOQUANT (91% opaque, contenu baked) | 91% → 26.2%, cyan conservé | FAIL amélioration — contenu cyan baked visible à 26% | FAIL |
| TimerFrame | FAIL MAJEUR (chiffres "14:57" baked, 23%) | 23% → 5.2%, chiffres absents, crochets seuls | Crochets latéraux uniquement, 0 chiffre parasite | PASS |
| CoreIconTex | PASS CONDITIONNEL (modulate ×2, 92% opaque) | fond transparent, couleurs ×1.8, 86% | Icône visible sur fond sombre sans modulate forcé | PASS |
| LvHexBg | PASS (correction sprite 44×26 session précédente) | non touché | inchangé | PASS |

---

### 4. Verdict global — PASS-CONDITIONNEL

**TimerFrame :** PASS complet. Les chiffres baked "14:57" sont absents, le sprite est réduit à
5.2% d'opacité concentré sur les crochets latéraux. Aucune confusion visuelle.

**CoreIconTex :** PASS. Le fond noir a été retiré, les couleurs amplifiées, l'icône sera lisible
sans modulate forcé.

**StatsPanelFrame :** FAIL subsistant. La réduction 91% → 26% est réelle et significative, mais
le contenu baked (barres HP concept, "HACKER RIG STATUS", robot, boutons) a survécu car il est
dessiné en teintes cyan — précisément les teintes conservées par le filtre HSV. À 26% d'opacité,
le panneau stats reste jouable mais avec des fantômes cyan du concept art superposés aux éléments
UI dynamiques.

**Conséquence pratique :** Le panneau stats est lisible (pas de masquage complet comme avant).
La barre HP dynamique est visible. Les informations vitales restent accessibles. Mais la qualité
visuelle est dégradée par la présence fantôme du concept art à 26% d'opacité.

---

### 5. Bug documenté

#### BUG-HUD-03b — StatsPanelFrame : fantôme concept art à 26% après masquage HSV

**Sévérité :** Majeur (dégradation visuelle, pas bloquant pour la jouabilité)
**Contexte :** `scenes/ui/HUD.tscn`, nœud `StatsPanelFrame` (TextureRect, z_index=2), texture
`assets/sprites/ui/ui_panel_frame_nobg.png` (300×217 px)
**Reproduction :** Lancer une run. Observer le panneau stats haut-gauche.
**Observé :** Le concept art du panneau (barre HP baked cyan, texte "HACKER RIG STATUS", barre
XP baked, robot, boutons PING/FIREWALL) subsiste à ~26% d'opacité superposé aux éléments
dynamiques réels. Les éléments baked étant cyan comme les éléments UI réels, la confusion est
particulièrement visible sur la barre HP (baked pleine largeur cyan à 26% + dynamique variable).
**Attendu :** Un cadre avec fond transparent, seuls les bords/ornements visibles.
**Cause racine :** La technique de masquage HSV (conservation pixels cyan) ne peut pas distinguer
les éléments de déco cyan des éléments de contenu cyan du concept art — les deux ont la même
teinte et sont donc tous conservés.
**Solution :** Le sprite doit être recréé de zéro comme un vrai overlay cadre (fond alpha=0,
seuls bords/coins/ornements dessinés) ou la zone de contenu du sprite doit être manuellement
effacée dans un éditeur d'image (sélection zone centrale + suppression alpha), sans passer par
un masquage automatique par teinte.
**Assigné à :** graphiste

---

### 6. Briefing graphiste — StatsPanelFrame

Le masquage HSV a échoué pour `ui_panel_frame_nobg.png` parce que le contenu graphique (barres,
textes, icônes) du concept art est dessiné dans les mêmes teintes cyan que les ornements de cadre
à conserver. Un filtre par teinte ne peut pas distinguer les deux.

**Solution requise :** Refaire le sprite manuellement :
- Dimensionner à 302×124 px (correspondant à la zone StatsPanelBg : 8→308, 8→132, plus 2px débord)
- Fond entièrement transparent (alpha=0)
- Seuls les bords et ornements de cadre en opaque :
  - Bords H et V de 1px cyan `#44FFEE` à 35% alpha
  - Coins L-bracket (16×1 H + 1×14 V) cyan `#44FFEE` à 85% alpha
  - AccentBar gauche (5×122 px) optionnel si visuellement souhaitée dans le sprite
- Aucun texte, aucune icône, aucune barre intégrée dans le PNG
- Le `LevelLabel`, `HpLabel`, `HpBar` etc. restent des nœuds Godot dynamiques — ne pas les
  reproduire dans le sprite

Alternative rapide (si le graphiste n'est pas disponible) : développeur remet `modulate =
Color(1,1,1,0)` sur `StatsPanelFrame` dans `HUD.tscn` — workaround de la session précédente,
rend le sprite invisible et restaure la lisibilité immédiate.

---

## Test HUD Frame Final — 2026-06-25

**Testeur :** game-tester (agent Claude)
**Hash git :** 52e38fd (docs: GDD §16 HUD juicy + README phase courante 2026-06-25)
**Objectif :** Vérifier le correctif du BUG-HUD-Frame-001 (sprite `ui_panel_frame_nobg.png`
régénéré from scratch avec fond 100% transparent, coins L-bracket épais 2px, tirets de
graduation, losanges ornementaux, bordure 1px quasi-invisible alpha=55).

---

### Smoke test

**Démarrage :** PASS. Le jeu démarre sans crash. Fenêtre "Chimera Protocol (DEBUG)" visible.
MainMenu affiché correctement. Aucune erreur console détectée au lancement.

**MD5 cache import :** PASS. Le fichier `.ctex` importé (hash `cb693cfa50854bc139771718f7ca8a8a`)
est synchronisé avec le PNG source — `source_md5=14cb805c16addb897463c233f186a776` correspond.
Pas de désynchronisation cache/source.

---

### 1. StatsPanelFrame — PASS

**Méthode :** captures à 3x et 4x via PIL NearestNeighbor sur la zone du panneau stats
(coords fenêtre vérifiées via `GetWindowRect`). Plusieurs captures en jeu (LV 1-5, HP plein
et endommagé).

**Observations confirmées :**

- **Coins L-bracket** : visibles aux 4 angles du panneau — traits épais 2px cyan formant
  l'angle caractéristique du style tech. Haut-gauche, haut-droit, bas-gauche, bas-droit tous
  présents.
- **Tirets de graduation** bord haut : ligne de tirets fins entre les coins, clairement
  distincts de la bordure fine alpha=55 quasi-invisible.
- **Losange ornemental** bord haut : carré/losange cyan visible au centre du bord supérieur.
- **Losange ornemental** bord bas : visible au centre du bord inférieur (confirmé capture
  `hud_stats_br_corner.png`).
- **Fond transparent** : le `StatsPanelBg` (ColorRect sombre) est visible à travers le PNG.
  Aucun contenu baked (texte, barre, icône) visible à travers le sprite. BUG-HUD-Frame-001
  CLOS.
- **Éléments dynamiques lisibles** : titre "CHIMERA PROTOCOL", HP label, HP bar, LvHexBg,
  XP bar segmentée, sous-label XP — tous parfaitement lisibles sans artefact de superposition.

**Verdict StatsPanelFrame : PASS — BUG-HUD-Frame-001 RÉSOLU**

---

### 2. TimerFrame — PASS

**Observations :** crochets latéraux du `ui_timer_frame_nobg.png` visibles — deux barres
verticales cyan sur les côtés gauche et droit du bloc timer. Fond transparent respecté.
Timer "13:36", "12:39" etc. lisibles. Sous-titre "RUNTIME ENCRYPTED" visible. Aucun double
affichage. Pas de régression.

**Verdict TimerFrame : PASS**

---

### 3. CoreIconTex — PASS

**Méthode :** localisation automatique via détection des pixels violets (R>100, B>100, G<80)
dans la zone y<300 de la capture fullscreen. Zone trouvée à X=1588-1845, Y=34-109.

**Observations :** l'icône hexagonale `ui_chimera_core_icon.png` (hexagone violet/cyan, circuit
intégré, fond transparent) est rendue correctement dans le `CoresContainer`. Opacité 81%
respectée. Titre "NOYAUX AETHER" lisible. Compteur "0" en violet vif. Bordures violettes
avec crochets aux coins du panneau Cores présents.

**Verdict CoreIconTex : PASS**

---

### 4. LvHexBg — PASS

**Observations :** contour hexagonal `ui_lv_hex.png` visible en cyan autour du texte "LV X"
(LV 1 → LV 5 observé pendant le test). Sprite stretch_mode=5 (Keep Aspect Centered) correct.
Pas de débordement, lisibilité maintenue.

**Verdict LvHexBg : PASS**

---

### 5. Éléments dynamiques HUD — PASS

**HP bar :**
- Pleine (165/165) : barre cyan pleine largeur. PASS
- Endommagée (126/165) : barre raccourcie correctement, couleur cyan maintenue (ratio >50%).
  Label mis à jour en temps réel. PASS
- Glow HP synced. PASS

**XP bar segmentée :**
- LV 4, 1/35 XP : 1 segment rempli sur 20, tirets sombres pour le reste. PASS
- LV 5, 0/45 XP : tous les segments vides (bleu foncé). PASS
- Flash level-up non capturé directement mais level-up fonctionnel (LV 1→5 observé). PASS

**Timer :**
- 14:50 → 12:32 décompte validé. Couleur blanc cassé (>120s restants). PASS

**Compteur Noyaux :**
- Affiché "0" (aucun noyau collecté pendant le test). Compteur fonctionnel. PASS
- "9" visible dans une capture antérieure (début de session — la valeur venait d'une run
  précédente encore active). Mise à jour correcte.

**Notifs armes :** non capturées (délai de capture trop court par rapport au fade-out 0.5s).
Fonctionnalité de code non modifiée, pas de régression attendue.

---

### 6. Level-up screen — PASS (observation annexe)

4 level-up screens observés pendant la session (niveaux 2, 3, 4, 5). À chaque fois :
- Jeu correctement pausé (ennemis figés)
- 3 cartes affichées avec rareté "[Commun]" / "[Rare]"
- Clic carte valide → jeu reprend sans freeze
- Heal level-up confirmé : HP 119/140 → 165/165 après acquisition "Plaque Renforcée" (+MaxHP
  +25% MaxHP heal)

Cartes observées : Capaciteur, Essaim de Drones, Champ de Surcharge, Canon à Impulsions Niv.2,
Plaque Renforcée, Noyau Thermique, Servo-Moteurs.

---

### 7. Régressions détectées

Aucune régression détectée sur le HUD juicy suite à la régénération du sprite
`ui_panel_frame_nobg.png`.

---

### Verdict global — PASS

| Element          | Résultat | Note                                         |
|------------------|----------|----------------------------------------------|
| StatsPanelFrame  | PASS     | BUG-HUD-Frame-001 CLOS                       |
| TimerFrame       | PASS     | Crochets présents, pas de doublon             |
| CoreIconTex      | PASS     | Icône hex visible, fond transparent           |
| LvHexBg          | PASS     | Contour hex correct LV 1-5                   |
| HP bar dynamique | PASS     | Pleine + diminuée + label sync               |
| XP segmentée     | PASS     | Segments corrects LV 4-5                     |
| Timer décompte   | PASS     | 14:50 → 12:32 correct                        |
| Noyaux compteur  | PASS     | Affiché, mis à jour                          |
| Level-up screen  | PASS     | 4 activations, aucun freeze                  |
| Crash            | PASS     | Aucun                                        |

**Verdict final : PASS**

---

# Playtest features — session 2026-06-27 (game-tester)

**Build** : `dotnet build` → 0 erreur / 0 warning (4×, dont après chaque switch de perso).
**Runtime** : 9 lancements de `Game.tscn` (chimera/titan/vagabond + Hub + Arsenal + level-up) → 0 erreur console, 0 exception, 0 NullReference.

## Verdict global : PASS

| # | Feature | Statut | Observé |
|---|---------|--------|---------|
| 1 | 3 personnages (sprites dédiés) | PASS | Sprites + stats + auras distincts confirmés en jeu |
| 2 | 2 fusions (Orbital / Égide) | PASS | Présentes dans l'Arsenal avec icônes + descriptions |
| 3 | 4 biomes + effets gameplay | PASS | Label rendu, modificateurs câblés et consommés |
| 4 | Cohérence textes accentués | PASS | Accents OK partout (cartes, Hub, Arsenal) |
| 5 | Fix HUD `XpSubLabel` | PASS | 0 erreur runtime, référence supprimée |

**Personnages (forcés via `SelectedCharacterId`, restauré à `chimera`)** :
- `titan` : HP 190/190 (150 + 40 méta), sprite robot/tank, aura orange, drone_swarm.
- `vagabond` : HP 115/115 (75 + 40 méta), sprite humanoïde vert.
- `chimera` : HP 140/140 (100 + 40 méta), sprite cyborg cyan, impulse_cannon.
- Bonus méta (+40 PV) s'ajoute bien par-dessus la base de chaque perso → `RegisterPlayer` validé.

**Biomes** : label capturé « Givre Cryogénique / Givre : ennemis -18% lents ». `BiomeXpMult` consommé `XpSystem.cs`, `BiomeEnemySpeedMult` au spawn `EnemySpawner.cs`.

## Bugs / observations
- **[BUG-301] (Mineur)** : `.import` des sprites titan/vagabond absents du commit `da32759` → export depuis clone neuf sans passe d'import = sprites magenta. **Corrigé 2026-06-27** (commit des `.import`).
- **[OBS-1] (Design — game-designer)** : `HubScreen.OnPlayPressed` — si le sélecteur d'arme méta est visible, il écrase silencieusement l'arme de signature du perso (titan→drone_swarm, vagabond→plasma_blade). Comportement cohérent mais sans retour visuel. À trancher.

---

# Session 2026-06-28 — Vérification victoire boss (build exporté)

**Version testée** : branche `main`, HEAD `0a50177`. Lancé via Godot 4.7 .NET sur `res://scenes/Game.tscn` (rendering-driver d3d12). Smoke-test `.exe` exporté déjà PASS (contexte établi).

**Objet borné** : confirmer empiriquement le FLUX de victoire par boss final (le DPS n'est pas l'objet — analytiquement le boss ≈4096 PV à 13 min est tuable en ~3-9 s par un build correct).

## Méthode
Le bot auto-kite (`tools/screenshot_swarm.py`) ne concentre pas le feu sur une cible unique et meurt vers ~76 s sans vrai build → incapable de tuer un boss à PV réel. Le DPS n'étant pas l'objet, j'ai **découplé le test du DPS** pour exercer le FLUX :
- Backup `data/enemies.json` → `/tmp/enemies.json.bak`.
- Édition temporaire : `rusted_core` `spawnStartMinute` 13→0.5, `spawnWeight` 1→6, **`maxHp` 1600→30**, `speed` 46→120 (pour qu'il rejoigne vite le joueur). `rust_swarm` `spawnStartMinute` 0→99 (désactivé : boss = seule cible du canon + joueur survit sans pression).
- Lancement `Game.tscn`, kite + clics centre les ~9 premières s (vider la file de level-up de départ « Mémoire Résiduelle »), puis arrêt des clics pour préserver l'écran de fin.
- **Restauration** `data/enemies.json` depuis backup, vérifiée `git diff --quiet` = identique à HEAD. Aucun code touché.

## Résultats (PASS)

| # | Vérification | Statut | Observé |
|---|---|---|---|
| 1 | Boss tué → écran « EXTRACTION REUSSIE » (~1,4 s après) | PASS | Capture `docs/boss_combat.png` : titre cyan « EXTRACTION REUSSIE » + count-up « Temps survécu : +0 Échos », timer ~14:26 (boss spawn 30 s, mort ~33 s, victoire ~34 s). |
| 2 | Pas de `LevelUpScreen` parasite (boss = 0 orbe XP) | PASS | Aucun overlay de level-up sur l'écran de victoire ; HUD figé LV 4. Cohérent avec `RustedCore.Die()` (pas de `SpawnXpOrb`). |
| 3 | Badge « VAINCU » sur le biome joué | PASS | Biome tiré = Friche d'Aether. `settings.cfg [progress] completions=PackedStringArray("fournaise:1","aether:1")`. `LevelSelectScreen` (`docs/levelselect_vaincu.png`) affiche « VAINCU » doré sur Friche d'Aether ET Fournaise ; absent sur Sanctuaire/Givre. |
| 4 | Persistance après redémarrage | PASS | Le badge est rendu par un **processus Godot neuf** (LevelSelectScreen lancé séparément) lisant `settings.cfg` sur disque → persistance cross-restart prouvée par construction. |
| 5 | Non-régression : mort joueur → « MORT EN SERVICE » | PASS (code + artefact) | Chemin `Player.HandleDeath → RunStatsTracker.EndRun("death") → RunEndScreen` (`isVictory=false` → libellé rouge « MORT EN SERVICE »). Artefact `docs/death_test.png` confirme le rendu. Capture live cette session non obtenue : les runs ont tiré des biomes lents/faciles (Givre -18%, Fournaise) où le bot survit >2 min — non bloquant. |

## Confirmations de code (lecture statique)
- `RustedCore.Die()` : `EmitSignal(Died)` + `NotifyEnemyKilled` + SFX, **pas de `SpawnXpOrb`** (commentaire OBS-2), puis `_sprite.Play("death")`.
- `OnAnimationFinished` (anim `death`) → `FinishDeath()` : explosion (3 ondes, flash, 3 Noyaux, shake/hitstop), `QueueFree()`, puis `tree.CreateTimer(1.4f, processAlways:true).Timeout += () => RunStatsTracker.Instance?.EndRun("extraction_success")`.
- `RunStatsTracker.EndRun("extraction_success")` → `AudioSystem.StopMusic` + `GameSettings.RecordCompletion(CurrentBiomeId, Difficulty)` → `RunEndScreen` libellé cyan.
- `RunStatsTracker._Process` : plus d'auto-victoire au timer (confirmé par commentaire).

## Ce qui N'A PAS pu être vérifié par ce moyen
- La tuabilité du boss **à PV réel** par le bot auto-kite (hors de portée du tool ; couvert analytiquement, hors objet ici).
- La capture live d'un écran de mort cette session (biomes lents tirés au sort) — couvert par code + artefact existant.

## Recommandation (developpeur)
Pour une vérif empirique complète du combat de boss **sans bidouiller les données**, ajouter un **hook de debug** activable (ex. flag `--debug-boss` ou touche dev en build DEBUG) qui : (a) accorde un loadout 5 armes L5 + thermal_core, (b) spawn immédiatement `rusted_core` à PV réel. Cela permettrait de mesurer le TTK réel à l'écran et de valider l'explosion/transition sans toucher `enemies.json`. Sinon, conserver la procédure « boss rush » documentée ici (backup/restore impératifs).

## État fichiers
- `data/enemies.json` : **restauré, identique à HEAD** (`git diff --quiet` OK).
- Aucun fichier source modifié.
- `settings.cfg` : contient désormais `aether:1` (complétion légitime issue du test) — données utilisateur, non réversibles, sans impact.
- Artefacts : `docs/boss_combat.png`, `docs/levelselect_vaincu.png`, `docs/death_test.png`.

---

# Session de test — Run complet avec Reroll & Skip (2026-06-28)

**Verdict : PASS.** Mécaniques reroll/skip validées via les vrais handlers de boutons + run fenêtré sain.

Contexte : save de test (build méta maxé + `reroll:3`/`skip:3`), restauré à l'identique après le test.

## Mécaniques (headless, émission du signal `pressed` des vrais boutons — déterministe)
| Vérification | Résultat |
|---|---|
| `RerollsLeft`/`SkipsLeft` initialisés au niveau acheté | **PASS** — 3 / 3 chargés depuis le save par `LevelUpSystem.Reset()` |
| Boutons « Renouveler »/« Passer » visibles sur le LevelUpScreen (amélioration possédée) | **PASS** |
| **Reroll** régénère les cartes + décrémente | **PASS** — carte0 « Volée Multiple » → « Canon à Impulsions », `RerollsLeft 3→2` |
| **Skip** vide la file de level-ups + décrémente + reprend le jeu | **PASS** — `AddXp(20)` = 2 level-ups ; SKIP #1 (`SkipsLeft=2`, écran visible = avance au 2ᵉ), SKIP #2 (`SkipsLeft=1`, écran caché, `pause=False`) |

## Run complet (fenêtré, bot kite + choix de cartes)
- À ~107 s écoulés (timer 13:13) : joueur **vivant niveau 18**, ~18 level-ups enchaînés, plusieurs armes équipées, arène dense gérée, **0 crash**. Run sain en route vers le boss / la mort.

## Notes
- Approche de vérif : émission de `Button.Pressed` (pas de clic pixel, fragile avec le stretch `canvas_items`) → passe par `OnRerollPressed`/`OnSkipPressed` réels, lecture synchrone du texte des cartes.
- Le retrait de l'XP de départ (`starting_xp`) a supprimé la pause parasite du LevelUpScreen au lancement → les tests gameplay headless fonctionnent désormais sans contournement.
- Boutons nommés `RerollButton`/`SkipButton` (chemins `LevelUpScreen/Actions/*`) pour faciliter les futurs tests.

---

# Non-régression post-refacto SOLID + features (2026-06-29)

**Verdict : PASS — aucune régression détectée.** Validation faite après l'extraction de la
logique pure (`src/Core/Rules/`), le découpage des fichiers, et les features de session
(localisation EN/FR/ES, reroll/skip, choix du personnage, quitter la partie, ennemis bloqués
par obstacles, aimant). (game-tester indisponible — limite de session ; validation manuelle.)

| Vérification | Résultat |
|---|---|
| Tests unitaires `dotnet test` (9 règles pures) | **51/51 PASS** |
| Build jeu `dotnet build -c Debug` | **0 erreur** |
| Run gameplay (bot kite ~70 s) : spawns, montées de niveau, armes, HUD | **PASS** — niveau 8 atteint, level-up à 3 cartes valides, 0 crash |
| Formules refactorées en conditions réelles (XP, spawn, rareté via WeightedPicker, extrapolation) | **PASS** (comportement inchangé, validé aussi par les tests) |
| Localisation : EN par défaut + bascule ES (menu Jugar/Hub/Bestiario/…) + persistance | **PASS** (restauré en `en` après test) |
| Cartes de level-up traduites (Codex via Loc) | **PASS** (descriptions EN affichées) |

Notes : la pause headless du LevelUpScreen n'existe plus (`starting_xp` retiré) → tests gameplay
headless OK. Aucun fichier de gameplay modifié ; `settings.cfg` restauré.

---

## Non-régression — passe game-tester 2026-06-29

**Testeur :** game-tester (agent Claude) — **Hash git :** `5e93ba2`
**Verdict : PASS — aucune régression, aucun bug détecté.** Confirme et complète la validation
préliminaire ci-dessus avec une passe indépendante (tests + captures réelles).

| # | Vérification | Méthode | Résultat |
|---|---|---|---|
| 1 | Tests unitaires (9 règles pures `src/Core/Rules/`) | `dotnet test tests/ChimeraProtocol.Tests.csproj` | **51/51 PASS** (19 ms) |
| 2 | Build Debug | `dotnet build -c Debug ChimeraProtocol.csproj` | **0 erreur / 0 warning** |
| 3 | Smoke / autoloads | boot headless `MainMenu.tscn` | **PASS** — MetaProgression (8 upgrades), SaveManager, 5910 Échos chargés, 0 erreur console |
| 4 | Enchaînement écrans EN (Menu→CharacterSelect→LevelSelect) | captures `screenshot_scene` | **PASS** — 3 perso (Chimera/Titan/Vagabond, stats+desc), 4 biomes (effets+badge « DEFEATED » traduit), boutons Choose/Play/Random/Back |
| 5 | Gameplay core (formules refactorées en réel) | bot kite `screenshot_swarm` 75 s puis 130 s | **PASS** — niveau 14 atteint en Difficile, spawns (nuée + orbes XP), armes qui tirent (Impulse/Plasma/Thermal Core…), HUD live (LV/PV/XP/timer décompte/Noyaux), loadout, 0 crash |
| 6 | Montée de niveau / 3 cartes / rareté | level-up figé (NOCLICK) | **PASS** — « Level 2! », 3 cartes (Rare/Common colorées), file de level-up OK |
| 7 | Reroll / Skip | save.json (reroll:1, skip:1 accordés puis **restauré**) | **PASS** — boutons « Reroll [1] » / « Skip [1] » affichés sous les cartes, gated par `GetUpgradeLevel>0` |
| 8 | Localisation ES | `settings.cfg language="es"` puis **restauré** | **PASS** — menu Jugar/Hub/Bestiario/Arsenal/Opciones/Salir |
| 9 | Localisation EN (défaut) | captures EN | **PASS** — Choose your character / Choose the level / DEFEATED |
| 10 | Fin de run — libellé mort | chaîne loc + artefact + chemin code | **PASS (vérifié indirectement)** — `localization/ui.csv` `RUNEND_DEATH` = EN « KILLED IN ACTION » / FR « MORT EN SERVICE » / ES « CAÍDO EN COMBATE » ; artefact `docs/death_test.png` présent ; chemin `Player.HandleDeath → RunStatsTracker.EndRun("death") → RunEndScreen` inchangé |

**Limite (non bloquante) :** la mort joueur n'a pas pu être capturée live cette session — le bot
auto-kite + i-frames (0.45 s) survit jusqu'au niveau 14 même en difficulté Difficile. Fin de run
validée via la chaîne de localisation, l'artefact existant et le chemin de code inchangé (cf. #10).

**Robustesse fichiers :** `settings.cfg` et `save.json` restaurés à l'identique (diff vide,
`git status` = aucun fichier suivi modifié). Seuls des PNG de capture untracked ajoutés dans `docs/`.

---

## Passage pseudo-3D + 20 nouveaux ennemis biome — 2026-07-03

**Testeur :** game-tester (agent Claude) — **Hash git base :** `e88ccb4` (working tree modifié :
redesign pseudo-3D de tous les sprites + `data/enemies.json` 28 entrées + `docs/ART_BRIEF_PSEUDO3D.md`
+ `tools/pseudo3d_lib.py` + `tools/generate_new_enemies.py`, non commité au moment du test).

**Contexte :** validation du redesign visuel "pseudo-3D avec ombres" (tous sprites) et des 20
nouveaux ennemis basiques data-driven (4/biome × 5 biomes, réutilisant les 4 archétypes d'IA
existants via `EnemyBase.SetSpriteFrames`), conformément au brief du game-designer/développeur.

### Méthode
- Lecture statique : `data/enemies.json` (28 entrées), `src/Systems/EnemySpawner.cs`,
  `EnemySpawnData.cs`, `EnemyBase.cs`, `src/UI/Codex.cs`, `localization/ui.csv`,
  `tools/generate_new_enemies.py`, `docs/ART_BRIEF_PSEUDO3D.md`.
- Build + tests : `dotnet build ChimeraProtocol.csproj`, `dotnet test tests/ChimeraProtocol.Tests.csproj`.
- Captures réelles via un harnais de capture de fenêtre par `PrintWindow` (contourne le problème
  de fenêtre occultée par une autre application au premier plan — cf. Notes) : `BestiaryScreen.tscn`
  scrollé intégralement (28 entrées animées), `MainMenu`/`CharacterSelectScreen`/`LevelSelectScreen`/
  `ArsenalScreen`, et un run réel `Game.tscn --biome=fournaise`.
- Test empirique ciblé de l'anim "attack" 1-frame : backup de `data/enemies.json` → `lava_spitter`
  (ranged_kiter, Fournaise) `spawnStartMinute: 6→0` + `spawnWeight: 3.3→50` → run réel 25 s →
  **restauration immédiate du fichier, diff confirmé identique à l'original**.

### Verdict par point demandé

| # | Point | Verdict | Détail |
|---|---|---|---|
| 1 | Sprites des 20 nouveaux ennemis chargés et distincts | **PASS** | Les 28 entrées du Bestiaire s'affichent avec leur `SpriteFrames` propre animé (idle), aucune image cassée/manquante, aucun repli visible vers un sprite générique. Chaque dossier `assets/sprites/enemies/<id>/` a son propre jeu de PNG + `.import` + `.tres` (12-13 fichiers PNG selon archétype). `[EnemySpawner] 28 types d'ennemis chargés.` en console, 0 erreur `PrintErr` (ni « Aucune scène pour l'id », ni « Scène introuvable »). **Observation non bloquante** : les 5 variantes d'un même archétype (ex. les 5 « harceleurs ») partagent une silhouette générique unique, seule la palette change (confirmé dans `tools/generate_new_enemies.py`, fonctions `draw_forager/draw_harasser/draw_turret/draw_bruiser` communes) — cohérent avec la contrainte « aucune nouvelle mécanique », mais à signaler au `game-designer`/`directeur-artistique` si plus de variété de silhouette est souhaitée à l'avenir. |
| 2 | Anim "attack" 1 frame (5 ranged_kiter) ne crashe pas | **PASS** | `.tres` généré avec `"frames": [{...}], "loop": false` pour les 5 ids (`sanctuary_walker_turret`, `aether_spectral_watcher`, `lava_spitter`, `cryo_marksman`, `neon_laser_turret`) — confirmé par lecture directe de `sanctuary_walker_turret_frames.tres`. Test empirique sur `lava_spitter` (spawn forcé dès t=0, poids 50) : run 25 s, 0 erreur/warning en console, aucun crash. `CorruptedSentinel.cs` appelle `sprite.Play("attack")` avant de spawner le projectile — chemin identique à l'archétype de base, donc pas de risque spécifique aux nouveaux ids. |
| 3 | Dilution du spawnWeight par biome (4→8 ennemis) | **PASS avec observation** | Analyse du pool : les ids globaux (`rust_swarm`, `corrupted_drone`, `corrupted_sentinel`, `grafted_colossus`, sans champ `biomes`) restent actifs partout ET s'ajoutent aux 4 variantes du biome — le poids total par rôle (fourrage/harceleur/tireur/bruiser) **augmente** plutôt que de se diluer à somme constante (ex. Fournaise, rôle tireur à 6 min : `corrupted_sentinel` poids 4 + `lava_spitter` poids 3.3 = 7.3 de poids total contre 4 avant l'expansion). Le rythme de spawn **global** par rôle n'est donc pas appauvri ; c'est la fréquence d'apparition d'un *type précis* qui baisse d'environ moitié (effet recherché : plus de variété visuelle par run). Aucun signal de rythme "clairsemé" observé en test réel (Fournaise, poids gonflé artificiellement pour le test attack-anim : nuée dense et continue, aucun ralenti perceptible). |
| 4 | Cohérence pseudo-3D (joueur/ennemis/obstacles/tuiles) | **PASS** | Inspection pixel (zoom ×10, nearest-neighbor) de `player_idle_01.png`, `sanctuary_marked_walker_idle_01.png`, `lava_spitter_idle_01.png`, `tile_pillar_stone.png`, `tileset/biomes/fournaise/floor_01.png` : highlight haut/gauche cohérent, ombre bas/droite cohérente, ombre portée elliptique présente sur joueur/ennemis/obstacle (absente sur la tuile, conforme au brief §3/§5), amplitude de gradient réduite sur la tuile de sol (conforme §5 "±10-15%"). Aucune direction de lumière divergente relevée. `generate_new_enemies.py` importe `pseudo3d_lib` et applique `shade_sprite` + `add_cast_shadow` de façon identique aux autres générateurs. |
| 5 | Lisibilité joueur en combat dense | **PASS** | Capture réelle en jeu (Fournaise, nuée dense de `lava_spitter` + décor lave orange/rouge saturé) : le joueur (Chimera, silhouette cyan lumineuse) reste immédiatement identifiable par contraste chromatique fort face à la palette chaude du biome — cf. `docs/attack_test_05.png`. Pas de confusion observée avec les ennemis/décor à l'écran. |
| 6 | Non-régression standard | **PASS** | `dotnet build` : 0 erreur/0 warning. `dotnet test` : **62/62 PASS**. `MainMenu.tscn`, `CharacterSelectScreen.tscn` (3 persos, portraits ombrés), `LevelSelectScreen.tscn` (5 biomes, aucun badge « VAINCU » — cohérent, aucune victoire enregistrée), `ArsenalScreen.tscn` (icônes armes ombrées 2-faces, conforme §5 icônes UI) s'affichent sans erreur. Run réel `Game.tscn` : HUD complet (LV/XP/PV/timer/compteur Noyaux/nom biome), level-up 3 cartes rareté colorée, pause/reprise OK. |

### Vérifications complémentaires
- **Câblage complet des 20 ids** : `src/UI/Codex.cs` (accent couleur cohérent par biome — Ember/IceB/Magenta ajoutés), `localization/ui.csv` (60/60 clés `ENEMY_*_NAME/_TAG/_DESC` EN/FR/ES présentes, vérifié par script), `data/enemies.json` (`framesPath` + `biomes` sur les 20 entrées) — **PASS**, aucune clé manquante.
- **`.import` commités** : les 20 dossiers `assets/sprites/enemies/<id>/` ont bien un `.import` par `.png` (piège BUG-301 déjà documenté) — **PASS**, comptage systématique effectué (12 ou 13 selon archétype, correspondance exacte png/import).

### Notes techniques (harnais de test)
- Le filtrage de fenêtre par simple substring de titre + `pyautogui.screenshot(region=...)` a capturé
  par erreur le contenu d'une **autre application** (jeu Steam plein écran ouvert en arrière-plan)
  faute de mise au premier plan fiable. Remplacé par une capture directe du HWND via
  `user32.PrintWindow` (`PW_RENDERFULLCONTENT`), fiable même si la fenêtre est occultée — **à
  réutiliser pour les prochaines sessions** (scripts ad hoc, non committés dans `tools/`).
- Édition temporaire de `data/enemies.json` pour le test empirique de l'anim attack : sauvegarde
  avant modification, diff vérifié identique après restauration — aucun impact laissé sur le dépôt.

**Verdict global : PASS.** Aucun bug bloquant/majeur trouvé sur le redesign pseudo-3D ni sur les 20
nouveaux ennemis. Une observation (silhouette générique partagée par archétype) transmise pour
arbitrage design, non bloquante.

## Correctifs : VFX résiduels menu + retrait upgrade arme de départ — 2026-07-04

**Testeur :** game-tester (agent Claude). Godot 4.7.stable.mono, rendu D3D12.

**Contexte :** (1) purge des VFX/projectiles parentés à la racine, figés par la pause à la mort et
persistant sur le menu/Hub → `SceneCleanup.ClearWorldVfx` appelé aux 3 sorties de run
(`RunEndScreen.OnHubPressed`/`OnReplayPressed`, `PauseScreen.QuitToMenu`) ; (2) retrait de l'upgrade
Hub `starting_weapon_alt` (« Prototype de Terrain ») sans effet (aucun sélecteur d'arme de départ
câblé). Méthode : `--debug-boss --force-elites` pour une mort déterministe avec VFX massifs.

**Correctif 1 — VFX résiduels**

| # | Point | Verdict | Détail |
|---|---|---|---|
| 1-2 | Mort en action → « Retour au Hub » | **PASS** | VFX figés visibles derrière le panneau de fin, puis Hub **totalement propre** après transition. |
| 3 | Mort → « Rejouer » | **PASS** | Nouvelle run **sans VFX résiduel** de la run précédente. |
| 4 | Pause → « Quitter » → menu | **PASS** | Burst figé derrière le panneau PAUSE, menu principal **propre** après sortie. |
| 5 | Absence de crash | **PASS** | Aucune erreur/exception console sur ces transitions. |

**Correctif 2 — Retrait de l'upgrade « Prototype de Terrain »**

| # | Point | Verdict | Détail |
|---|---|---|---|
| 1 | Absence dans le Hub | **PASS** | Upgrade absente de la liste. Console : « 17 upgrades chargés ». |
| 2 | 17 upgrades, sans clé brute | **PASS** | 17 items, libellés propres, continuité tier-1→tier-2, se termine sur « Aimant Auxiliaire ». |
| 3 | Achat/navigation sans crash | **PASS** | Achat Corps Renforcé (−180 Échos exact), boutons grisés hors budget. Aucun crash. |
| 4 | Essaim de Drones = arme de départ du Titan | **PASS** | Sélection perso « Titan-Gardien » → Arme : Essaim de Drones. Intact, indépendant de l'upgrade retirée. |

**Verdict global : PASS.** Les deux correctifs validés, aucune régression, aucun crash.

---

## Session 2026-07-05 — Perso Vecteur + ZQSD/remap clavier (commit 784deeb)

Playthrough réel piloté (D3D12, captures + console). `dotnet build` 0/0, `dotnet test` **87/87**.

| Piste | Verdict | Détail |
|---|---|---|
| Perso **Vecteur** — sélection | **PASS** | 4e carte « Vecteur — Cyborg — précision », sprite violet, PV 90 / Vit 210 / Arme Lance Vectorielle, desc FR. |
| Vecteur — run | **PASS** | Lance Vectorielle comme arme de départ, **réticule violet** suivant la souris, tir dirigé perforant. Aucune exception. |
| **ZQSD** en jeu | **PASS** | Z/Q/S/D déplacent le joueur, flèches conservées. |
| **Remap** (Options → Contrôles) | **PASS** | 4 boutons de direction, remap Haut→K appliqué+persisté, Échap annule sans quitter, « Touches par défaut (ZQSD) » restaure. |
| Non-régression nav clavier | **PASS** | Focus MainMenu/CharacterSelect intact (actions `move_*` séparées des `ui_*`). |

**Verdict global : PASS** — publication recommandée.

### BUG-OPT-01 (Mineur) — écran Options tronqué en 720p — **CORRIGÉ (même session)**
La section « Contrôles » faisait déborder le VBox (dans un simple `CenterContainer`) → « Retour » et « Tout réinitialiser » hors écran. **Fix** : `OptionsScreen._Ready` — contenu placé dans un `ScrollContainer` (`FollowFocus = true`, scroll horizontal désactivé) + `HBoxContainer` de centrage. Vérifié visuellement : tout le contenu est de nouveau atteignable, la nav clavier auto-scrolle vers l'élément focalisé.

---

## Session 2026-07-05 — Poussée des ennemis (push, pas de ghosting) (commit 11e2a83)

Build headless `--build-solutions` : **0 erreur**. Vérif analytique (valeurs de séparation par
type d'ennemi) + captures empiriques (run nuée + `--debug-boss`, D3D12).

**Mécanique testée** : `Player.PushEnemiesAside()` (après `MoveAndSlide`) déplace chaque ennemi
chevauchant le corps du joueur sur un anneau `sep = max(PlayerBodyRadius=13, PushRadius−6)`. Le
joueur ne collisionne pas avec les ennemis (mask=2) → jamais bloqué. `PushRadius = ContactRadius`.

| # | Point | Verdict | Détail |
|---|---|---|---|
| 1 | Pas de blocage / pleine vitesse | **PASS** | Joueur atteint le centre et sweep librement à travers les paquets. Impossible d'être freiné : les ennemis ne sont pas dans le mask de collision du joueur ; seul l'ENNEMI est déplacé. |
| 2 | Pas de ghosting | **PASS** | Aucun ennemi ne recouvre le centre du joueur ; ils sont maintenus sur le bord du corps. Boss (`--debug-boss`) jamais superposé au sprite joueur, tenu à distance de l'anneau. |
| 3 | Dégâts de contact actifs | **PASS** | `sep < ContactRadius` pour **tous** les ennemis contondants (marge 6 px partout où `PushRadius>19`). Empirique : le boss tue le joueur en 16 s au corps-à-corps. I-frames 0.45 s intacts. |
| 4 | Feel de la poussée | **PASS (téléport OK, pas besoin d'interpoler)** | Détail ci-dessous. |
| 5 | Gros ennemis / boss | **PASS** | Boss (`ContactRadius 56` → `sep 50`) repoussé plus loin que les basiques ; pas de tremblement ni téléportation visible sur les stills, plow cohérent. |
| 6 | Non-régression | **PASS** | Orbes XP ramassés (level-ups en chaîne), pas de crash, obstacles/tirs inchangés (le commit ne touche que le déplacement des ennemis en groupe). |

### Vérif analytique de la séparation (contact préservé)

| Ennemi | ContactRadius | sep = max(13, R−6) | Marge (R−sep) | Contact |
|---|---|---|---|---|
| CorruptedDrone | 20 | 14 | 6 | oui |
| RustSwarm / défaut | 24 | 18 | 6 | oui |
| AetherRevenant | 34 | 28 | 6 | oui |
| RustStalker | 32 | 26 | 6 | oui |
| GraftedColossus (Colosse) | 36 | 30 | 6 | oui |
| RustedCore (boss) | 56 | 50 | 6 | oui |

`CorruptedSentinel`/`MasterSentinel` : `HandleContactDamage` vide (à distance) → non concernés.
Marge constante de 6 px : le contact reste actif pour chaque archétype, gros ennemis écartés
proportionnellement (plus la silhouette est grosse, plus l'anneau est large). **Aucun ennemi
actuel avec `ContactRadius` entre 13 et 19** (sinon la marge tomberait sous 6 px).

### Feel — verdict

Le hard-set (téléport, sans interpolation) est **acceptable en l'état** : les ennemis sont lents
(~40–90 px/s → <1,5 px/frame à 60 fps), donc à l'entrée dans l'anneau le clamp ne recule l'ennemi
que d'une fraction de pixel — **aucun saut visible**. Maintenu à l'anneau, l'ennemi oscille en
sous-pixel (imperceptible). Le joueur qui fonce « laboure » la foule (bow-wave) de façon
satisfaisante. **Pas besoin de passer à une interpolation.**

### OBS-PUSH-01 (Cosmétique) — fallback `Vector2.Right` sur recouvrement quasi-parfait
`PushEnemiesAside` : si `dist <= 0.01` la direction retombe sur `Vector2.Right`. Un ennemi que le
joueur centre parfaitement est projeté 1 frame à sa DROITE quel que soit le sens de déplacement.
Cas rare (recouvrement quasi pixel-parfait), 1 frame, non observé en jeu. Améliorable en réutilisant
la dernière direction de déplacement du joueur (`Velocity`) plutôt qu'une constante, mais impact
négligeable — non bloquant. Assigné à : developpeur (optionnel, polish).

**Verdict global : PASS.** Le feel est bon, la poussée est fluide et lisible, le contact et les
i-frames sont préservés, aucun blocage, aucun ghosting. Rien à lisser côté séparation.

---

## Session 2026-07-06 — Rééquilibrage boss « Le Noyau Rouillé » (PV 18000→12000) + non-régression empilement

Build : `main` (v1.11.3-a7c6425), `dotnet build` **0 erreur / 0 warning**. Méthode : hook
`--debug-boss` (loadout de référence 5 armes L10 + thermal_core ×1.45, boss isolé, spawn ambiant
coupé), instrumentation temporaire `[BOSS-TTK] spawn/dead ticks` dans `RustedCore` (retirée après
mesure, build revérifié propre). Runner `scratchpad/boss_ttk_run2.py` (Godot d3d12).

### A) Mesure du TTK boss

| Élément | Valeur |
|---|---|
| PV réel du boss à t=13 min (Normal) | 12000 × (1 + 13×0,06) = **21 360 PV** |
| DPS analytique single-target, loadout réf. L10 + thermal ×1,45 | **~550 DPS** (impulse ~76 + scatter ~43 + tesla ~71 + plasma ~90 + drones ~100, pré-thermal ~380, ×1,45) |
| **TTK analytique** | 21 360 / 550 ≈ **~39 s** |
| **TTK empirique mesuré** (spawn tick 962 ms → dead tick 37 038 ms) | **36,1 s** |

**Note méthodo :** la mesure empirique a d'abord échoué (1re passe : joueur MORT à T=16 s, K=0 —
problème de *survie* du kite, pas de killabilité). Pour isoler le TTK indépendamment de la survie,
`rusted_core.damagePerProjectile` a été mis temporairement à 0 (backup + **restauration vérifiée**,
PV inchangés), joueur quasi-immobile → toutes les armes en portée. Le 36,1 s est donc une **borne
basse (best-case, DPS maximal)** ; un vrai combat kité (armes intermittentes) serait un peu plus
long, cohérent avec le `tuningNote` visé (~43-61 s pour un build moyen ~350-500 DPS).

Capture `docs/boss_ttk/v2_death.png` : explosion dorée du boss + 3 Noyaux d'Aether (losanges cyan),
HP joueur 140/140 (invincibilité dégâts=0 OK), biome Givre. Kill réel confirmé (log :
`[BOSS-TTK] dead ticks=37038`, pas de `Player died`).

**Résultat A : PASS.** Le boss est **BATTABLE**, TTK ≈ 36-39 s sur le build de référence (fort),
et ~43-61 s attendu pour un build moyen. Loin du seuil d'échec (>90 s / boss immortel). Dans la
bande cible.

### B) Non-régression empilement (maxSimultaneous en overtime)

Vérification par lecture de code (`src/Systems/EnemySpawner.cs`) — non trivial à reproduire
visuellement (13 min de run + overtime) :

- `SpawnOvertimeBoss` (l.182-190) appelle `SpawnEnemy(data, tEff)` **sans** `ignoreMaxSimultaneous`.
- Le garde de `SpawnEnemy` (l.243-245) : `!ignoreMaxSimultaneous && data.MaxSimultaneous(=1) > 0 &&
  GetNodesInGroup("rusted_core").Count >= 1 → return`.
- `RustedCore._Ready()` fait `AddToGroup("rusted_core")` ; sortie du groupe seulement au `QueueFree()`
  (fin d'anim de mort, `FinishDeath`).

→ Un seul `rusted_core` vivant à la fois ; un nouveau ne re-spawn qu'**après** la mort effective du
précédent (y compris pas de spawn pendant l'anim de mort). **Résultat B : PASS (par analyse).**

### C) Anomalies / observations

- **OBS-BOSS-01 (Doc, non bloquant)** : le script fourni `tools/boss_ttk_test.py` et sa docstring
  (« encadrer l'apparition de l'écran EXTRACTION RÉUSSIE ») sont **périmés**. La logique actuelle
  (`RustedCore.FinishDeath → RunStatsTracker.OnLevelBossDefeated`, cf. GDD §17) ne déclenche **plus**
  d'écran « EXTRACTION RÉUSSIE » à la mort du boss : elle affiche une bannière dorée « NIVEAU
  TERMINÉ » et **la run continue** (survie sans fin). L'écran de fin cyan « EXTRACTION RÉUSSIE »
  n'apparaît qu'au `RunEndScreen` (mort du joueur) avec le flag `PendingLevelCompleted`. Le script
  ne détecte donc jamais de fin ; il faut viser la bannière/explosion. Assigné à : developpeur
  (mettre à jour le script + docstring).

**Verdict session : PASS.** Boss battable (TTK ~36-39 s réf. / ~43-61 s build moyen), empilement
corrigé (un seul boss vivant), aucune régression. Reste OBS-BOSS-01 (outil de test à rafraîchir).
