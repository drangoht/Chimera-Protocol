# CLAUDE.md — Mémoire de projet

Chargé automatiquement au démarrage de chaque session : **rester court et stable**. Le détail vit
dans des fichiers chargés **à la demande** (pointés ci-dessous) pour limiter le contexte par session.

## Le projet

"Chimera Protocol" — survivor roguelite vue du dessus, univers fantaisie-science-fiction (humains,
cyborgs, robots), inspiré de Vampire Survivors et Everything is Crab.

- **Design complet → `docs/GDD.md`** : le consulter avant toute tâche de design/implémentation, et le tenir à jour à chaque décision.
- **Localiser du code** (système, écran, arme, ennemi, données, outil) → invoquer le skill **`/carte-projet`** plutôt que Glob/Grep à froid : il indexe l'arborescence + les checklists de câblage. Le maintenir à jour dans le même commit qu'un changement structurel.
- **Avant de coder** dans un domaine (armes, ennemis, UI/focus, VFX, scènes, assets, tests headless) → lire **`docs/PITFALLS.md`** (pièges non-évidents Godot/C# + checklists de câblage). Y ajouter tout nouveau piège découvert.
- **Comprendre l'architecture du code** (principe logique-pure/moteur, cycle de vie d'une run, contrats `EnemyBase`/`Player`/`WeaponBase`, calques d'UI, persistance, instrumentation, dette connue) → **`docs/ARCHITECTURE.md`**. À MAJ quand une structure change.
- **État d'implémentation détaillé & version courante → `docs/PROJECT_STATE.md`** (évolutif). Résumé de phase ci-dessous.
- **Synthétiser du volume** (relever/résumer/inventorier à partir de plusieurs gros fichiers : `data/*.json`, docs longues, logs, rapports de test) → déléguer au **MCP local** `mcp__local-llm__local_digest` / `local_map` (outils différés : `ToolSearch` d'abord) plutôt que d'enchaîner les `Read` : le serveur lit les fichiers côté LM Studio, seule la synthèse entre en contexte. Ne pas l'utiliser pour du code que l'on s'apprête à éditer — là, le contenu réel est nécessaire.

**Phase actuelle : 1.25.1 PUBLIÉE le 2026-07-31** (Saturation de Rouille, lot 1 + correctif
d'Hémorragie) — build butler **#1846002**, `version.json` à 1.25.1. **Devlogs 1.25.0 et 1.25.1 rédigés
(`docs/DEVLOG.md`), à coller sur itch par l'utilisateur.**
⚠ **Le lot 2 (cran VI) est dans `main` mais NON PUBLIÉ et NON JOUÉ** — le jeu en ligne est la 1.25.1.

**(8) Mesurer ce qui se *sent* — et le lot 2 livré** (2026-08-02, **non publié**). Le lot 1 avait
**passé** son critère (temps soutenable −10,0 %, 4/4, seuil 6 %) et le testeur ne sentait rien : cause
structurelle, **toutes** les colonnes de `PowerTelemetry` sont des **débits moyennés sur 15 s**, donc
aveugles à un **pic** — un plongeon à 10 % des PV suivi d'une remontée ne déplace aucune moyenne, et
c'est pourtant ce qu'un joueur appelle « difficile ». → **`PressureMeter`** (logique pure) observe la
barre **à la frame** et compte des **événements** (`frolements` sous 30 % des PV max, `pv_min_pct`,
`part_danger`). **Ce qu'il révèle du jeu publié** : au cran 0, sur 6:45 d'overtime, la barre du joueur
**ne descend jamais sous 76 %** — zéro frôlement, aucun instant de tension dans une partie entière.
**Cran VI « Purificateur » (lot 2)** : les **champions** infligent au minimum **12 % des PV max**
(plancher **avant** la DR — les i-frames et la réserve de régénération continuent de jouer). Une seule
règle ferme les deux points ouverts : elle vise ce qui **crée** le surplus (PV max, +277/min sans
plafond) au lieu du canal des soins saturé de gaspillage, et rend le boss dangereux **sans toucher son
TTK** (jamais un mur de patience). **Validé au banc** (4 graines appariées, cran 5 → 6) : runs
mortelles **1/4 → 4/4**, `PV bas min %` **25,5 → 2,5** (0/4 net), pour **+6,6 %** de dégâts subis
seulement (indécidable) — *la menace change de nature, pas d'intensité*, là où le cran V ajoutait
+50 % de dégâts sans jamais faire descendre la barre.
⚠ **Piège d'implémentation** : un plancher en % des PV max ne doit **jamais** toucher un dégât
**continu** (PV/s × delta) — appliqué à chaque tick il tue en quelques frames. D'où
`EnemyBase.DealDiscreteDamage`, chemin **unique** des coups discrets (qui absorbe au passage la DR
recopiée par huit appelants) ; `EnemyBullet.FromChampion` se pose **au tir** (un projectile survit à
son tireur).
⚠ **Deux biais de LECTURE, trouvés en route** : ① `--min-samples` est un **biais de survie** — une run
courte parce que le joueur **meurt vite** est le meilleur résultat du réglage ; la run où le bot meurt
en 1 min était exclue du verdict du cran qui l'avait tuée, et faisait paraître l'effet **inversé**.
② un **compte** d'événements rares ne s'arbitre pas (verdict inversé entre 2 et 3 paires) : le critère
porte sur la **profondeur** (`PV bas min %`), et le **taux de runs mortelles** se lit en premier.
⚠ **JAMAIS JOUÉ** : le bot kite mécaniquement et tire au hasard, or ce cran vise un comportement
**humain** (rester au contact d'un champion parce qu'on a les PV pour) ; **0,12 n'est pas calibré**,
seulement prouvé *effectif*. Design → `docs/GDD.md` **§34.6/§34.7** ; mesures → `docs/TEST_REPORT.md`
(2026-08-02) ; checklist « ajouter un cran » → `docs/PITFALLS.md`. **319 tests.**

**(7) L'échelle complète jouée — et le canal des soins est saturé de gaspillage** (2026-08-01, **non
publié**, aucun changement de gameplay). Le testeur a joué **les crans 1 à 5** : « pas de difficulté
particulière ». Mesuré (4 graines appariées, `tools/power_loop.py --paired`) : **le joueur jette 80 %
des soins reçus** — 293,6 PV/s offerts pour **58,8 retenus** au cran 0. **Couper les soins ne peut donc
pas durcir le jeu** : « Hémorragie » divise l'offre par deux (**−46,4 %**, mieux que ses −40 %
annoncés) et le joueur en retient *davantage* qu'avant, parce qu'il est plus souvent blessé. → **le
canal des soins est exclu du lot 2** ; viser ce qui **crée** le surplus (PV max, cartes de surcharge
+45/prise sans plafond). Design → `docs/GDD.md` **§34.4 ter**.
⚠ **Piège de mesure, à connaître avant tout réglage** : `Player.Heal`/`HealFlat` clampent à `MaxHp`, donc
un soin reçu à PV pleins vaut **zéro**. La colonne `soins_ps` compte le **retenu** — une *conversion*,
qui monte mécaniquement avec les dégâts subis — et non la générosité. Lue à l'envers, elle **inverse le
diagnostic** : le cran V semblait rendre **+41 %** de soins et « annuler Hémorragie », il en donne
**−46 %**. Deux découplages de l'affixe d'élite ont été écrits, mesurés et **annulés** sur cette
lecture fausse. Signal manqué : le premier correctif supprimait ~200 orbes/min sans déplacer la
métrique d'un point (85,3 → 85,0) — *quand ça arrive, c'est l'instrument qu'il faut suspecter*.
Nouvelle colonne **`soins_bruts_ps`** (PV offerts) ; pièges → `docs/PITFALLS.md` §Soins.
⚠ **Non expliqué** : le cran 5 fait chuter le temps soutenable de **89,3 % à 67,7 %** et **tue le bot**
— et reste imperceptible. Le critère « >6 % de temps soutenable » **ne prédit pas le ressenti**.
**Piste la plus sérieuse, jamais instruite** : le **boss est tué 13 fois par run** (réapparition toutes
les ~70 s) et son TTK est **insensible aux crans** (7,9-35,8 s au cran 5 sur le biome le plus facile,
contre 9,8-37,4 s au cran 0 sur le plus dur ; il ne gagne que ×1,17 PV).
Outils : `tools/power_loop.py` (comparaison appariée + test des signes), cran de saturation dans
l'en-tête de `power_curve.log`, `tests/AudioAssetReferenceTests.cs`. Mesures → `docs/TEST_REPORT.md`
(section 2026-08-01, **lire le §5 avant le §3 : le §3 est réfuté**). **302 tests.**
⚠ **Première partie jouée au cran I (2026-07-31) : « aucune difficulté rencontrée » — et le testeur
avait raison.** La carte **Blindage** (canal de soin dominant : 44 prises contre 1 d'Auto-réparation)
écrivait `CurrentHp` **en direct**, donc hors du multiplicateur d'Hémorragie *et* hors de
`PowerTelemetry` ; même défaut sur le soin du passif `reinforced_plating`. Le cran I ne rognait que les
orbes et le vol de vie. Corrigé en 1.25.1 (tout passe par `Player.HealFlat`) — **règle générale : un
soin ne s'écrit jamais dans `CurrentHp`, il passe par `Heal`/`HealFlat`**, seul chemin qui applique les
crans et journalise. `BossTelemetry` journalise désormais la saturation (bloc + colonne CSV).
**Campagne refaite le 2026-07-31 (8 runs, 4 graines × 2 crans, même version)** — et elle a trouvé plus
gros que le cran : **toute la base de mesure était biaisée**. Le cran 0 relève **89,3 %** de temps
soutenable contre **60,7 %** dans l'ancienne référence, *sans aucun changement de gameplay* — le soin
du Blindage n'était pas notifié à `PowerTelemetry`, donc le canal **dominant** était invisible pour
l'instrument qui sert à régler le jeu. → `docs/bench/ref_overtime_225.json` **périmée**, remplacée par
**`ref_overtime_1251_sat0.json`** ; la progressivité publiée (60,7 → 39,9 %) est fausse **en valeur
absolue** (le sens tient) ; et le diagnostic du plan est **pire** qu'annoncé — le joueur est en surplus
de PV **89 %** du temps d'overtime, pas 60,7 %, ce qui renforce l'urgence du **lot 2** (cran VI).
Le cran I corrigé vaut **89,3 → 80,4 %** (−10,0 % relatif, 4/4) : il passe le critère des 6 %, mais
le joueur reste soutenable **80 % du temps** — le ressenti « aucune difficulté » est **exact**.
Points ouverts : l'**arbitrage** de l'Auto-réparation (le banc ne peut pas trancher un choix de carte,
cf. (5)) ; les crans **II à V jamais joués** ; le **cran III** n'est mesurable par aucun banc.

**(6) Saturation — challenge de fin de partie** (2026-07-31, **publié en 1.25.0**). Plan complet →
**`docs/ENDGAME_PLAN.md`** (validé : saturation cumulative, **choisie et récompensée**, rejouabilité par
de nouvelles *raisons* de rejouer et non par du contenu neuf). Diagnostic : le jeu devenait facile parce
que la défense du joueur croît **sans plafond** (Blindage +45 PV/prise) face à une menace à **courbe
fixe**, densité déjà saturée et plafond de difficulté à ×1,35 — et surtout parce que la menace ne posait
qu'**une** question (des statistiques), donc le joueur n'avait qu'**une** réponse, qu'il gagne toujours.
**Lot 1 livré** : `SaturationTable` (logique pure) — un cran = **une règle nommée** lisible avant de
lancer, qui retire une certitude ; les statistiques ne montent plus après le cran 1 (un test le
verrouille). I Hémorragie (soins −40 %, le canal **dominant** : 86,4 PV/s contre 8,2) · II Meute
(ex-« Difficile ») · III Compte à rebours (overtime à la 10ᵉ — attaque le temps de *build*) ·
IV Sans filet (**le passage de niveau ne soigne plus**, + filets méta coupés) · V Élite
ordinaire. **Le cran se règle et se débloque PAR NIVEAU** (2026-07-30, renverse le §7.3 du plan) :
sélecteur **sur la carte du biome** (la liste défile — un panneau global aurait laissé régler un niveau
hors écran), et `settings.cfg` passe en **`save_version=2`** (tables `biome:cran`, migration qui diffuse
l'ancien cran global à tous les biomes). La saturation **absorbe** l'ancienne difficulté : le cran 1 vaut « Difficile » aux mêmes
valeurs, donc **les records déjà gagnés restent exacts** ; « Facile » survit comme mode d'**assistance**,
hors échelle (migration des `settings.cfg` testée). Échos +20 %/cran via une **source unique**
(`TotalEchoMult`) — `EchoFormula` et `RunEndScreen` doivent appliquer le même facteur, sinon la somme
animée diverge du total crédité. Nouveau flag **`--saturation=<n>`** : sans lui aucun cran n'est
mesurable (le bot ne traverse pas l'écran de sélection), non persisté via la même parade que `--lang`.
**Validé au banc** : temps soutenable **60,7 % → 39,9 %** (0/4, net), survie théorique **÷2**, et
**2 runs sur 4 meurent** là où les quatre atteignaient le plafond. **Progressivité mesurée cran par
cran** : 0 **60,7 %** → I **53,6 %** → II **50,0 %** → IV **−16 %** relatif (contre le cran III) →
cumul **39,9 %**. Le cran I porte la moitié de la descente, le cran II — le seul purement statistique —
deux fois moins. ⚠ Le **cran III n'est pas mesurable par ce banc** (il déplace le temps : les deux
protocoles biaisent en sens opposés). ⚠ **Un cran ne doit jamais reposer sur un levier optionnel** :
« Sans filet » ne coupait que deux consommables **achetés**, absents de la sauvegarde de référence après
84 runs — il ne retirait donc rien, et un bonus *fini* est de toute façon invisible pour une métrique de
*flux*. Élargi au filet **universel** (soin de passage de niveau).
**Migration vérifiée avant publication** (§8.5 du plan) sur un `settings.cfg` d'avant la saturation
forgé en « Difficile » + complétions : le binaire exporté démarre et joue sans crash, et **rien n'est
perdu** — `save.json` (Échos, greffes, perks, défis) est hors périmètre du lot, `IsUnlocked` et le
badge passent par `HasCompletedAny` (toutes difficultés), donc les anciennes clés `"biome:2"`
débloquent toujours la suite. La migration ne s'écrit qu'au **premier `Save()`** et se rejoue à
l'identique d'ici là (idempotente). ⚠ Conséquence assumée : les crans étant **cumulatifs**, un habitué
du « Difficile » ne retrouve pas son réglage exact — Normal (plus facile) ou cran 2 = Difficile
**+ Hémorragie** (plus dur). ⚠ Piège d'UI corrigé au dernier moment : le sélecteur **disparaissait
sans un mot** en mode Assistance (`BuildCardSaturation` sortait sur `IsAssisted`) — un joueur en
« Facile » ne pouvait pas savoir que l'échelle existait ni où la réactiver → ligne `SAT_ASSISTED`.
Même famille que le dash sans touche annoncée : **invisible se lit inexistant**.
Mesures → `docs/TEST_REPORT.md` ; design → `docs/GDD.md` **§34**. **300 tests.**

**(5) Réserve de régénération — la carte ne manquait pas de valeur, elle en perdait 58 %**
(2026-07-30, **publié en 1.24.0**). Premier réglage instruit **au banc apparié** plutôt qu'à la session jouée.
Le §33.5 posait l'alternative « monter le débit ou l'indexer sur les PV max » : la mesure montre que
**les deux auraient manqué la cause**. La régénération tourne à **19,2 PV/s nominaux pour 8,2 rendus**
(**58 % jeté**) parce que le porteur passe **100 % de l'overtime au-dessus de 90 % de ses PV max** et
meurt d'un **pic**, pas d'usure — monter le débit n'aurait fait que grossir la part perdue. → `RegenReserve`
(logique pure) : le tick **soigne d'abord**, le surplus alimente une **réserve** (20 s de débit, bornée
à 25 % des PV max) qui **absorbe le prochain coup**, après les i-frames. Le plafond dépend du **débit**
et non des seuls PV max, sinon une prise finirait par valoir quarante. Lisibilité (l'angle mort du
§33.5 se rejouerait) : liseré cyan sous la barre de vie, et un coup entièrement absorbé se lit **paré**
— flash cyan, aucun son de blessure. **Validé au banc sur les 4 mêmes graines** : régénération rendue
**8,2 → 15,9 PV/s** (+94 %, **4/4 en hausse**) et **temps soutenable inchangé** (60,7 %, classé bruit)
— la carte devient utile **sans** allonger l'overtime, donc sans rouvrir `StatAcceleration = 2,25`.
⚠ Ce que le banc ne dit pas : le bot tire ses cartes **au hasard**, donc l'*arbitrage* d'un humain
reste à observer. ⚠ L'appariement est fiable sur les métriques de **pression**, fragile sur celles de
**build** (PV max, puissance) dès que le changement modifie la longueur de la run.
Design → `docs/GDD.md` **§33.6** ; mesures → `docs/TEST_REPORT.md`. **272 tests.**

**(4) Banc de mesure — sortir du bruit** (2026-07-29, non publié). Les trois chantiers d'équilibrage
précédents se sont réglés à **une session jouée par valeur**, alors que le relevé (g) a établi que la
variance inter-run atteint un **facteur 2,4** *à l'entrée en overtime, là où le réglage testé n'a
encore aucun effet*. Une run isolée — humaine ou bot — ne tranche donc rien. Livré :
**(a) le bot de banc se déplace** (`AutoPilotPolicy`, logique pure : 16 caps évalués sur un couloir
de 220 px en 3 points, menaces/orbes/murs/inertie ; pont `BenchAutoPilot`). Il kite, ramasse et dashe,
donc **meurt pour de vrai** : la survie et les dégâts subis sont mesurables **sans `--invuln`**, ce
qu'aucun banc ne savait faire — immobile, le bot mourait en 20 s, ou affichait zéro dégât subi.
Survie portée de 3:51 à **7:18** entre les deux versions du scoring (couloir long > zigzag).
**(b) `--start-at=<minutes>`**, car 7:18 ne suffit pas : la fenêtre à instruire commence à la 13ᵉ.
Combiné à `--saturate-arsenal` (raccourci `--overtime`), il démarre la run **à l'entrée en overtime
avec un état standardisé** — et c'est là l'apport réel : la variance qui empêchait de conclure venait
justement de cet état d'entrée. La survie ainsi mesurée sert à **comparer des réglages**, pas à
prédire une durée de vie de joueur.
**(c) `--seed=<n>`** (`GD.Seed` + RNG de `PowerUpSpawner`, graine journalisée) → **comparaison
appariée** : relancer une campagne sur les mêmes graines après un changement annule le bruit de
tirage dans la différence. Quelques paires appariées tranchent ce que trente runs libres laisseraient
indécis. La lecture qui compte est le **test des signes**, pas le delta médian.
**(d) `tools/power_curve_multi.py`** : N runs + médiane/p10/p90 et surtout **le plus petit écart que
la campagne sait détecter** — un réglage dont l'effet attendu est sous ce seuil n'est pas validable,
quel que soit le nombre de sessions.
**(e) régénération instrumentée** (point ouvert de la 1.23.0) : `PowerTelemetry` distingue désormais
le taux **nominal** du **réellement rendu** (nul à PV pleins) et des soins ponctuels. Premier chiffre
obtenu, qui donne raison au testeur (« son effet ne se voit pas ») : en overtime tardif,
l'Auto-réparation rend **~15 PV/s effectifs pour 24 nominaux**, face à **~230 dégâts/s** — soit
**~6 %** de ce qui est encaissé.
**(f) première campagne réelle** (2026-07-30) — elle a **invalidé deux choix de méthode et déplacé le
levier**. ① `--timescale=3` ne rend **×1,0** en nuée (headless limité par le CPU) : une campagne de
6 runs coûte **70 min**, pas 25. ② La **survie du bot ne peut pas servir de critère** : arsenal saturé,
il tient **22:42** d'overtime contre **8:36** pour le joueur, et sous `--minutes 25` toutes les runs
finissent sur la limite de temps — la survie n'est alors pas mesurée mais **minorée**, avec une
variance écrasée qui faisait annoncer une précision de 13 % là où rien n'avait été mesuré (censure
désormais signalée, runs interrompues écartées). → métriques non censurées : **survie théorique**
(PV max ÷ dégâts nets) et **temps soutenable** (part du temps d'overtime où les PV rendus couvrent les
PV perdus — la plus stable, bruit 5 %). ③ **Le résultat de design** : le canal de soin dominant n'est
pas celui qu'on réglait — **soins ponctuels 129,7 PV/s contre 13,6 pour la régénération** (×9,5), et
le joueur est **en surplus de PV 60 % du temps d'overtime**. L'Auto-réparation est donc invisible *par
construction* (9,7 % des dégâts encaissés, noyée dans un canal dix fois plus gros), et la mort ne peut
venir que d'un **pic** : le levier de survie en overtime est le **soin ponctuel** (donc les tirages de
cartes, donc le choix du joueur). ④ **Référence figée** (`docs/bench/ref_overtime_225.json`, 4 graines
appariées, `--overtime --minutes 20` — 7 min d'overtime suffisent puisque les métriques se lisent par
échantillon, soit **28 min de banc** au total) : le bruit tombe de **240 %** (deux sessions humaines)
à **4-13 %**. Un réglage dont l'effet dépasse **~6 %** du temps soutenable est désormais décidable
**sans session jouée**. Et « temps soutenable » se révèle **invariant à la fenêtre d'observation**
(60,4 % sur 11:45 d'overtime, 60,7 % sur 6:45) là où la survie théorique ne l'est pas → c'est **la**
métrique de réglage. Prochain réglage : `--compare docs/bench/ref_overtime_225.json`, lire le test des
signes.
Pièges → `docs/PITFALLS.md` (§Tests headless, §Cartes de surcharge) ; mesures → `docs/TEST_REPORT.md`.
**256 tests.**

**(3) Survie en overtime — mesurée en jeu, cause réelle trouvée** (2026-07-29). La session jouée de
validation du chantier (1) l'a **réfuté** : le découplage tient sa métrique (dégâts entrants
**−33 %**) et n'achète que **14 secondes** (60 s → 74 s de survie, cible 5-10 min). Le relevé montre
que **la défense du joueur sature à la 11ᵉ minute**, deux minutes *avant* l'overtime — PV bloqués à
451, DR au cap depuis la 4ᵉ — pendant que le DPS fait **×700** sur la run. Deux causes, toutes deux
corrigées :
**(a)** effet de bord du §30 — `PassiveScaling` amortissait *aussi* les +25 PV/niveau de
`reinforced_plating` (500 → 251 PV à L20), alors qu'il visait `capacitor`/`thermal_core` ; des PV
plats et additifs croissent linéairement et n'ont jamais causé de power-creep. Les PV max sont
désormais la **seule stat exemptée** → **675 PV** mesurés en banc.
**(b)** les **niveaux vides** — le joueur passait du niveau **124 à 140 en 74 s** pour un gain
**nul** (armes L20, passifs saturés retirés du pool), `LevelUpSystem` complétant avec `XP_BONUS` :
de l'XP pour gagner des niveaux qui ne donnent rien. Livré : **`OverloadCards`**, trois cartes de
fin de partie **sans plafond** (Blindage +45 PV max et soigne d'autant · Auto-réparation +0,6 PV/s ·
Surtension +5 % de dégâts), proposées uniquement quand le pool est vide. Nouveau flag
**`--saturate-arsenal`** (le banc n'atteint jamais cet état seul).
**(c)** 2ᵉ session jouée : PV **700 → 2680** en overtime (la courbe n'est plus plate), survie 74 s →
**5 min 18 s**… mais **mort volontaire**, la run « aurait pu durer beaucoup plus longtemps ». Le
pendule était allé trop loin : la menace passait **sous** la défense à 5 min (×2,37 contre ×2,44),
les cartes rapportant ~306 PV/min. → `OvertimeEscalation.StatAcceleration` **1,5 → 3** (la valeur de
1,5 n'existait que parce que la survie était plafonnée — les cartes ont supprimé ce plafond). Le test
ne compare plus la menace à un **seuil absolu** mais à la **pente de la défense**.
**(d)** 3ᵉ session : à ×3, mort **subie** à **1 min 31 s** — trop dur. Valeur retenue **2,25**.
⚠ **La variance entre runs domine le réglage** : à l'entrée en overtime, *où `StatAcceleration` n'a
aucun effet*, deux sessions différaient d'un facteur **2,4** en survie (1060 PV / 28,9 dég/s contre
745 / 48,9) selon que l'arsenal sature vers la 11ᵉ ou la 13ᵉ min. **Une session par réglage mesure
surtout le bruit.**
**(e)** Deux angles morts d'ergonomie signalés par le testeur, corrigés : le **dash n'annonçait sa
touche nulle part** (ni HUD, ni description, ni écran d'assimilation — une run entière jouée sans
savoir qu'une touche existait) → ligne « Shift — esquive » au HUD + rappel à l'acquisition, libellé
lu de l'`InputMap` ; l'**Auto-réparation était crue *active*** (« son effet ne se voit pas ») →
indicateur `♥ +X/s` au HUD + description « automatiquement et en permanence, aucune touche à
presser ». Valeur **inchangée à dessein** : le retour porte sur la lisibilité.
**(f)** Deux correctifs de finition : les **modales étaient assombries par la vignette** (`PostFX` est
à `layer 90` ; level-up 10, fin de run 20 et assimilation 60 passaient dessous) → remontées à 97/98 ;
et les **tirs ennemis écrasaient le mixage** (`sfx_weapon_sentinel_shoot` à −7,5 dB RMS, le plus fort
de la banque, **+9,4 dB au-dessus du tir du joueur**) → nouvelle table `AudioSystem.MixGainDb`,
**−12 dB** après un premier essai à −9 encore jugé trop fort : mixer selon la **polyphonie réelle**
(N sentinelles contre 1 arme), pas seulement selon le niveau du fichier.
**(g)** **`2,25` mesuré — cible atteinte** : 5ᵉ session, mort **subie** à **8 min 36 s** d'overtime
(fenêtre visée 5-10 min). La menace y distance franchement la défense (dégâts subis **×9,1** contre
PV max **×2,11**) et met malgré tout ce temps à tuer. Le relevé **déplace le levier principal** : la
pente de la défense n'est pas une propriété du réglage mais un **choix du joueur** — 270 PV/min sur
les 3 premières minutes, puis **56**, avec un plateau *strictement plat* de près de 2 min pendant
lequel le DPS montait de 27 % (~46 prises de **Surtension** contre 25 de **Blindage**, ratio 1,84 ;
la session précédente donnait l'inverse, 0,80). Même joueur, deux runs, facteur **2,3** sur la pente
de la défense. Les cartes produisent donc bien l'**arbitrage** pour lequel elles ont été écrites, et
c'est lui — pas l'escalade — qui décide de l'heure de la mort. Le repère
`OverloadCards.MeasuredHpGainPerOvertimeMinute = 306` est requalifié : **profil de jeu, pas
constante**.
Design → `docs/GDD.md` §31.6, **§31.7**, **§31.8**, **§33** ; pièges → `docs/PITFALLS.md`
(§Amortissement des passifs, §Cartes de surcharge, §Capacités déclenchées par une touche,
§Calques d'écran, §Mixage des SFX) ; mesures → `docs/TEST_REPORT.md`. **244 tests.**
**(h)** Testeur : mixage des tirs ennemis (−12 dB) et Auto-réparation **validés à l'usage** ; en
revanche **« le mid-boss est trop petit »** — exact, et l'erreur était de raisonnement. Les 3 sprites
étaient en 48 pour « ne pas égaler le boss de fin (64) », or celui-ci est rendu à `Scale = 2,4`
(**154 px**) et les mini-boss globaux sont à **64** natif : les champions de biome étaient les plus
petits de tous. Et **leur hitbox débordait du corps** — le Colosse touche dans un diamètre de 72 px
pour une silhouette de 48. → **72 px** (`MidBossVisuals.SpriteScale = 1,5`), cible calée sur le
`contactRadius`, échelle appliquée **au rendu** (le générateur dessine en entiers dans un espace de
48 : un facteur y laisserait des rangées vides). Hiérarchie enfin alignée sur le rôle : faune 32 ·
mini-boss globaux 64 · **champions de biome 72** · boss 154. Vérifié en jeu sur les 3, overlays
intacts.
**(i)** **Écran de pause — les boutons sortaient de l'écran en fin de run.** Titre, corps et les trois
boutons vivaient dans un seul `VBoxContainer` sous un `CenterContainer`, **sans scroll ni plafond** :
avec 5 armes L20, 4 passifs et 5 greffes multilignes, le panneau dépassait la fenêtre et, *étant
centré*, débordait des deux côtés — « Quitter la partie » hors cadre, plus moyen d'abandonner la
partie. → seul le **corps** défile (titre et boutons hors du `ScrollContainer`). Le plafond n'est pas
une constante devinée (un 1ᵉʳ essai à 300 px sous-estimait le chrome de ~130, les cadres « plaque
blindée » gonflant les boutons) mais une **mesure** : `panel.GetCombinedMinimumSize().Y − budget`.
Défilement clavier via `ui_page_up/down` **seuls** — `ui_up/down` appartiennent à la chaîne de focus
des boutons. Vérifié avec `--saturate-arsenal --force-graft=all`.
**Reste** : ressenti de **combat** des mid-boss (jamais joués — seule leur taille a été relevée) ;
l'Auto-réparation n'est pas instrumentée (`PowerTelemetry` ne journalise pas la régénération).

**(2) Mid-boss par biome** (2026-07-29) — dernier point non livré de `docs/EXPANSION_PLAN.md` (B.3).
La faune par biome était complète, mais **trois niveaux sur cinq n'avaient aucun champion de mi-run**
et `master_sentinel` (16 min, pour une run de 13) n'apparaissait **jamais** hors overtime. Livré :
**Colosse en Fusion** (Fournaise — charges télégraphiées laissant un sillage de magma), **Sentinelle
Cryo** (Givre — cône de gel dirigé + plaques dans l'axe), **Gardien Néon** (Néon — bouclier orbital
qui n'absorbe que 20 % des dégâts venus du secteur couvert), + tag de biome des existants et
`master_sentinel` ramenée à 11 min. Le boss de fin ayant déjà une signature par biome (§29), chaque
mid-boss demande le réflexe **inverse** de l'incarnation finale de son biome. Nouveau flag
**`--debug-enemy=<id>`** (spawn isolé) + `tools/capture_midboss.py`. Design → `docs/GDD.md` §32 ;
pièges → `docs/PITFALLS.md` §Mid-boss (dont : **ne jamais dessiner un effet dans le `_Draw` du
champion** — `HitFlash` sature les couleurs à blanc via `Modulate` → `ChampionOverlay`).

**(1) Survie en overtime.** Le point
laissé « à surveiller » en 1.22.0 est instruit : le testeur mourait **1 min après l'entrée en
overtime**, quand l'économie d'Échos est dimensionnée sur **5-10 min** d'overtime (GDD §9.2) — ce
levier de méta-progression était donc inatteignable. Cause : `EnemySpawner` dérivait ses deux temps
de référence l'un de l'autre (`tStat = tDensity + offset`), si bien que l'accélérateur d'overtime
**×4 destiné à la densité** se déversait *en entier* sur les PV et les dégâts, via un terme
**quadratique** — alors que **tous les leviers de densité sont saturés** à l'entrée en overtime (cap
de 300 dès la 8ᵉ min). En face, la survie du joueur est **triplement plafonnée** (`reinforced_plating`
L20, DR 0,40, vitesse 380) : aucune fenêtre possible, quel que soit le skill. Corrigé →
`OvertimeEscalation` (densité ×4 conservé, scaling **×1,5**, courbes découplées). Dégâts entrants à
10 min d'overtime : **×10,9 → ×4,5**. Design → `docs/GDD.md` §31 ; pièges → `docs/PITFALLS.md`
§Escalade d'overtime ; mesures → `docs/TEST_REPORT.md`. **237 tests.**
⚠ **Diagnostic réfuté depuis par la session jouée** : correct sur son propre objectif, mais ce
n'était pas la cause de la mort du joueur → voir le chantier **(3)** ci-dessus.
Avant ça : **courbe de puissance assainie**, publiée
**1.22.0** le 2026-07-28. Le point ouvert de la 1.21.0 est
corrigé : la puissance faisait **×6,42 en 12 min d'overtime** (mesure `PowerTelemetry`, nouveau flag
**`--power-curve`** + `tools/power_curve_session.ps1`). Cause : les 4 passifs ne définissent que
**3 niveaux** pour un plafond de **20**, et au-delà le delta était réappliqué **en additif non
borné** — `capacitor` atteignait **100 % de réduction de recharge dès L8**, mettant *toutes* les
armes au plancher de 0,15 s (une arme lourde tirait aussi vite qu'un canon léger). Corrigé →
`PassiveScaling` (rendements décroissants), `StatCaps.MaxCooldownReduction = 0,75`, passifs saturés
retirés du pool de cartes. Ratio ramené à **×2,73**, et **×1,33** sur la session jouée de validation
(Fournaise, niveau 124 : le Capaciteur s'y arrête de lui-même à L7). Le TTK joué a fixé
`rusted_core.maxHp` à **5000** — le 4000 calculé donnait 18,7 s, sous la fenêtre : *ce boss ne se
calibre que sur un TTK joué*. Deux bugs trouvés dans le journal de cette session, sans rapport avec
l'équilibrage : `(int)GD.Randi() % n` **négatif une fois sur deux** (la récompense de mini-boss était
perdue une fois sur deux, silencieusement) et les sprites de faune `slow_hunter` sans animation
`attack` (144 erreurs/session) → `EnemyBase.PlayAnim`. Design → `docs/GDD.md` §30 ; pièges →
`docs/PITFALLS.md` (§Aléatoire, §Animations d'ennemis, §Passifs) ; mesures → `docs/TEST_REPORT.md`.
**231 tests.**
Avant ça : **fusions d'armes réparées + boss recalibré**,
publiée **1.21.0** le 2026-07-28. Les 9 fusions **divisaient le DPS de fin de run par 3 à 6** (dégâts
en dur jamais multipliés, retour au niveau 1, absentes de tout pool de cartes) : la carte la plus
spectaculaire du jeu en était le pire choix. Corrigé → héritage du niveau, multiplicateurs, montée
par cartes (piège → `docs/PITFALLS.md` §Fusions ; règle → `docs/GDD.md` §8). Dans la foulée,
`rusted_core.maxHp` **12000 → 8000** sur la première mesure de TTK *jouée* (GDD §20.6). Outillage :
`BossTelemetry` (journal `user://boss_ttk.log`), `tools/boss_ttk_session.ps1`, flags **`--auto-play`**
+ **`--timescale`** pour jouer une run complète en banc.
Avant ça : **boss de fin — phases & incarnations**, publié **1.20.0**. Le
Noyau Rouillé reste la condition de victoire unique des 5 niveaux, mais combat désormais en **trois
phases** (100→66→33→0 % de PV, cadences resserrées, adds en phase III, 1 s de surcharge télégraphiée
à chaque bascule) et prend une **incarnation par biome** (éventail dirigé / translocation / nova de
givre / flaques de magma / faisceaux rotatifs), avec sprite et nom propres. Nouvelle **barre de boss**
au HUD (crans aux seuils, numéro de phase). Design → `docs/GDD.md` §29 ; logique pure →
`BossPhases` + `BossIncarnations` (222 tests) ; pièges + checklist « ajouter une incarnation » →
`docs/PITFALLS.md`. **Reste à faire** : mesure de TTK par un testeur humain (le bot de test kite mal).
Avant ça : **options enrichies + accès depuis la pause**, publiées **1.19.0**, et les **paliers de
menace** en **1.18.0**. Avant ça : **bande-son metal industriel & musique adaptative**, publiée **1.17.0** le 2026-07-27. Les 14 musiques sont **générées sur Suno** à partir des
prompts de `docs/AUDIO_AI_PROMPTS.md` (source de vérité de la direction sonore) — metal industriel
/ synth-metal : guitares down-tuned et batterie live au premier plan, synthés et chœurs sans
paroles au service du riff, 112 à 176 BPM. **Licence Suno : plan gratuit = usage non commercial**,
acté pour un jeu distribué gratuitement — monétiser imposerait de regénérer sous plan payant
(`assets/audio/CREDITS.md`). Intégration : déposer les fichiers dans `music_ai/` puis
`python tools/import_ai_music.py` (bouclage, loudness, encodage). Pendant une run,
`MusicDirector` alterne **deux versions du même morceau par biome** (`calm` couplet / `combat`
refrain) + un thème de **boss commun**, par fondu croisé selon l'intensité de l'action — jamais
en superposition, ces pistes ne sont pas synchronisées entre elles. La bande-son synthétisée par
le dépôt (`tools/generate_music_v3.py`, ambiance Vangelis, `docs/ART_BRIEF_AUDIO.md`) reste
régénérable et sert de filet de sécurité sans contrainte de licence. Avant ça : refonte des **cadres d'UI
« plaque blindée »** (chanfreins, bevel, rivets, focus pulsé — `docs/ART_BRIEF_UI_FRAMES.md`)
étendue aux modales / level-up / écrans de sélection puis aux curseurs, interrupteurs et menus
déroulants + correctif audio, publié **1.16.0** le 2026-07-26. Précédemment : **Défis & Récompenses** — 4e levier de rétention (après arsenal / Hub /
Assimilation), publié **1.15.0** le 2026-07-08. 13 défis évalués en fin de run → Échos, **perks de
départ** (greffe offerte / arme sup / +1 slot) ou **titres** cosmétiques. Nouvel écran **Défis**
(progression X/N), sélection perk/titre au Hub, flair du titre sur le menu. Menu principal réorganisé
(sous-menu **Codex** regroupant Bestiaire/Arsenal/Chimère/Défis/Perks) + sélecteur de langue à
**drapeaux**. Correctif : cooldown de la Frappe Nova visible au HUD. Détail :
`docs/DESIGN_CHALLENGES.md`. Avant ça : **Assimilation** (Phase A+B, 1.12.0→1.14.0,
`docs/DESIGN_ASSIMILATION.md`). Détail dans `docs/PROJECT_STATE.md`.

## Équipe d'agents

Agents dans `.claude/agents/` : `game-designer`, `directeur-artistique`, `graphiste`, `developpeur`,
`musicien`, `story-teller`, `marketing`, `game-tester`, `release-manager`. Déléguer proactivement à
l'agent compétent (ordre de lancement : `GUIDE-CLAUDE-CODE.md`).

- **`game-tester`** : lance Godot (`--rendering-driver d3d12`), joue le jeu, documente les bugs dans `docs/TEST_REPORT.md`. À invoquer après chaque implémentation majeure.
- **`release-manager`** : publie la release binaire de bout en bout (bump semver, release notes, `tools/release_itch.ps1`, MAJ doc) puis **rédige le devlog** (titre + corps à coller) — l'utilisateur le publie lui-même sur itch (l'agent ne pilote pas le navigateur). Source des notes : `docs/DEVLOG.md`.

## Maintenance de la doc

- `README.md` (racine) — MAJ à chaque changement de phase / ajout majeur (tableau des phases, roadmap, captures).
- `docs/PROJECT_STATE.md` + `docs/GDD.md` + `/carte-projet` + `docs/PITFALLS.md` — MAJ dans le commit qui change ce qu'ils décrivent.

## Conventions

- Plateforme cible : Windows (.exe). Moteur : **Godot 4.7 .NET** (toujours la variante `.NET`, jamais la standard). Langage : C# (.NET 8), GodotSharp.
- Build Windows : `"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe" --headless --export-release "Windows Desktop" "./build/ChimeraProtocol.exe"`
- **CRITIQUE export .NET** : `ChimeraProtocol.sln` DOIT être présent à la racine (sinon le .exe crashe au lancement). Recréer : `dotnet new sln --name ChimeraProtocol --format sln && dotnet sln ChimeraProtocol.sln add ChimeraProtocol.csproj`. L'export produit `build/ChimeraProtocol.exe` + `build/data_ChimeraProtocol_windows_x86_64/` (runtime .NET 8, ignoré par git, régénéré).
- **Publication & MAJ auto (itch.io + Butler)** : incrémenter `config/version` dans `project.godot`, puis skill **`/publier-itch`** (ou `tools/release_itch.ps1 -Version X.Y.Z`), ou déléguer à l'agent **`release-manager`** (pipeline complet + devlog itch). Runbook : `docs/RELEASE.md`. Notes de version cumulées : `docs/DEVLOG.md`. Un push = auto-update pour les joueurs de l'app itch.
- Style de code : PascalCase classes/méthodes, `_camelCase` champs privés, `readonly` par défaut.
- Architecture : `src/` (logique C#) / `scenes/` (.tscn) / `assets/` (raw) / `data/` (JSON tuning modifiable sans recompiler).
- **Logique pure testable** : `src/Core/Rules/` (classes statiques sans dépendance Godot — `XpCurve`, `EnemyScaling`, `EliteAffixTable`…). Les nœuds y délèguent (SRP).
- **Tests unitaires** : xUnit, `dotnet test tests/ChimeraProtocol.Tests.csproj`. **237 tests**.
- **Difficulté** : trois axes multiplicatifs — réglage du joueur (`DifficultyTuning`), **palier de menace du niveau joué** (`LevelThreat`, croît avec l'ordre de déblocage : PV/dégâts/densité/Échos, cf. `docs/GDD.md` §28) et escalade d'overtime.
- Singletons (AutoLoad) : `GameManager`, `XpSystem`, `InventorySystem`, `LevelUpSystem`, `SaveManager`, `MetaProgressionSystem`, `ChallengeSystem` (défis/succès, `docs/DESIGN_CHALLENGES.md`), `AudioSystem`, `MusicDirector` (musique adaptative : calm/combat/boss en fondu croisé), `FusionFlash`, `ScreenShake`, `GameSettings`, `DiscordPresence` (Rich Presence), `VersionStamp` (tampon `v<ver>-<sha>` bas-droite).
- Sauvegarde : `user://save.json` (méta/Échos) + `user://settings.cfg` (préférences, high scores, complétions, armes découvertes).
- Sprites : PNG transparent, grille 32×32 px (Colosse 48×48 — exception), `texture_filter = Nearest` global. Style **pseudo-3D avec ombres** (`docs/ART_BRIEF_PSEUDO3D.md`) via `tools/pseudo3d_lib.py` — toujours dériver shadow/highlight avec `shade()`/`shade_sprite()`/`shade_tile()`/`shade_icon()`, jamais des couleurs plates ad hoc.
- **Audio** : musique **générée sur Suno** (prompts : `docs/AUDIO_AI_PROMPTS.md`) — jamais éditer
  un `.ogg` de `assets/audio/music/` à la main. Pour en remplacer une : regénérer sur Suno, déposer
  dans `music_ai/`, puis `python tools/import_ai_music.py [--only <id>] [--keep-preview]` et
  `godot --headless --import`. Contrôle : `tools/check_music_assets.gd`. Bande-son de secours
  synthétisée (sans contrainte de licence) : `tools/generate_music_v3.py`, `docs/ART_BRIEF_AUDIO.md`.
  SFX = WAV Kenney CC0. Crédits et **licence Suno (plan gratuit, non commercial)** :
  `assets/audio/CREDITS.md`.
- Localisation EN/FR/ES : `localization/ui.csv` → `Loc.T("CLÉ")`.
- Performance cible : 200–300 entités simultanées ; I-frames joueur 0.45 s (CRITIQUE pour les nuées).
- Palette UI : fond `#1A1A2E`, cyan `#44FFEE`, violet `#AA44FF`, or `#FFCC44`, blanc cassé `#D9D9F2`. Police : Share Tech Mono (AA on, `ui_theme.tres`, size 16) ; VT323 en réserve (AA off).
- **Cadres & couleurs d'UI** : toujours via `src/UI/UiPalette.cs` (couleurs) et `src/UI/UiStyle.cs` (fabrique des cadres « plaque blindée » — chanfreins, bevel, rivets, focus pulsé). Jamais de `StyleBoxFlat` ad hoc ni de couleur en dur, ni dans le C# ni dans les `.tscn`. Parti pris → `docs/ART_BRIEF_UI_FRAMES.md` ; assets → `tools/generate_ui_frames.py`.
- Python : `C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe` (pas dans le PATH).

## Pièges critiques → `docs/PITFALLS.md`

Tous les pièges non-évidents (API Godot C# manquante, callbacks/threading, checklists de câblage
armes/ennemis, affixes d'élite, VFX parentés à la racine, navigation clavier/manette, StyleBox/focus,
cycle de vie des scènes, assets `.import`, tests headless) sont dans **`docs/PITFALLS.md`**.
**Le consulter avant de coder dans le domaine concerné.**
