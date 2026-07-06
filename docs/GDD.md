# GAME DESIGN DOCUMENT — "CHIMERA PROTOCOL" (titre de travail)

> Document vivant. Toute décision prise pendant une session Claude Code (choix techniques,
> ajustements de design, renommages) doit être reportée ici par l'agent concerné, afin que ce
> fichier reste la source de vérité du projet. Il est chargé en contexte par `CLAUDE.md` à la
> racine du dépôt.

## 1. Pitch

Un *survivor roguelite* en vue du dessus, à la croisée de **Vampire Survivors** (boucle de
survie/auto-combat, montée en puissance exponentielle, vagues d'ennemis massives) et
**Everything is Crab** (choix d'évolutions qui transforment visuellement et mécaniquement le
personnage, forte variété de builds). L'univers est **fantaisie-science-fiction** : magie
ancienne (l'Aether) et technologie en décomposition fusionnées après une catastrophe, peuplé
d'**humains**, de **cyborgs** et de **robots**.

## 2. Inspirations et différenciateurs

| Référence | Ce qu'on en prend |
|---|---|
| Vampire Survivors | Boucle run-based chronométrée, auto-attaque, ramassage de XP, montée de niveau avec choix d'amélioration, essaims d'ennemis, échelle de puissance qui explose en fin de run |
| Everything is Crab | Les améliorations ne sont pas que des stats : elles **fusionnent** en formes évoluées qui changent l'apparence et le comportement de l'arme/du personnage ; forte importance de la variété de build et du "feel" de la transformation |

**Différenciateur du jeu** : l'évolution n'est pas juste statistique, elle raconte la fusion du
personnage avec la technologie/magie environnante — chaque évolution doit être lisible
visuellement (silhouette, VFX, palette) et cohérente avec le lore (cf. §3 et l'agent
`story-teller`).

## 3. Univers (résumé — la bible complète est à produire par l'agent `story-teller`)

Il y a deux siècles, **la Convergence** a fait fusionner l'Aether (énergie magique ancienne) et
les réseaux technologiques mondiaux lors d'une catastrophe expérimentale. Le monde de surface est
aujourd'hui infesté par la **Rouille Vivante** : une corruption mi-organique, mi-mécanique qui
anime débris technologiques et créatures mutées. Les survivants — humains, cyborgs greffés et
automates réactivés — explorent les ruines (les *Sanctuaires*) à la recherche de **Noyaux
d'Aether**, seule ressource capable de repousser la Rouille et de maintenir les dernières enclaves
en vie.

Le joueur incarne un **Arpenteur** envoyé en mission d'extraction dans un Sanctuaire : survivre le
plus longtemps possible, absorber de l'énergie pour évoluer sur place, puis extraire un maximum de
Noyaux avant la mort ou la fin du temps imparti.

### 3.1 Le Protocole Chimère — comment l'Arpenteur assimile sans être corrompu

> **LORE CANONIQUE — validé par l'utilisateur le 2026-07-06.** Répond à la question de cohérence
> soulevée par `game-tester`/`game-designer` sur le système d'Assimilation (greffes,
> `docs/DESIGN_ASSIMILATION.md`) : comment un Arpenteur peut-il greffer de la Rouille Vivante sur son
> corps sans être converti par elle ? Détail développé dans `docs/lore-bible.md` §7.

La Rouille Vivante n'est pas un poison qui tue — c'est une corruption qui **intègre** (cf. la
cinématique d'ouverture : *« Elle ne détruit pas : elle intègre. Elle transforme. »*). Un humain, un
cyborg ou un automate en contact prolongé avec elle ne meurt pas : il devient un ennemi de plus. C'est
précisément ce risque qui rend l'Assimilation crédible comme **technique maîtrisée** plutôt que comme
simple pillage de pièces détachées : les Arpenteurs sont les seuls formés à la pratiquer sans se faire
retourner, parce qu'ils portent en eux la seule chose qui puisse discipliner la Rouille — l'Aether.

Chaque Arpenteur part en mission avec un **noyau de greffe** : un éclat d'Aether personnel, greffé au
même titre que l'augmentation des cyborgs (« Greffés », §4 — l'Assimilation n'invente pas la
greffe, elle en est le prolongement extrême). Au moment où l'Arpenteur porte le coup de grâce à une
créature de Rouille, ce noyau **purge l'intention corruptrice du tissu prélevé** dans l'instant même
de la greffe : il n'assimile pas la Rouille telle quelle, il assimile ce qu'elle a fait à la biomasse
— le muscle-servo du Drone, l'œil de la Sentinelle, la carapace du Colosse — vidé de sa volonté
d'intégrer à son tour son porteur. Le Protocole Chimère est donc un acte de **domestication**, pas
de guérison : la Rouille grafée reste en l'Arpenteur, neutralisée mais présente, ce qui explique la
teinte rouille/organique qui s'accumule visuellement sur lui à mesure qu'il assimile (halo/teinte du
HUD, cf. `docs/DESIGN_ASSIMILATION.md` §13.4) — ce n'est pas un simple effet cosmétique, c'est la
marque diégétique de la Rouille qu'il porte, tenue en respect.

Cette domestication a un **prix assumé**, qui justifie les règles de jeu déjà écrites plutôt que de
les contredire :
- **Le nombre de slots est limité** (3, extensible via le Hub) parce que le noyau de greffe d'un
  Arpenteur ne peut stabiliser qu'une quantité finie de Rouille contenue à la fois — au-delà, il
  faut choisir laquelle on relâche pour en contenir une autre (le « choix cornélien » de
  remplacement, §13.3 du design, devient un choix littéral : quelle part de Rouille dormante
  garder en soi).
- **Le remplacement n'est pas gratuit dans la fiction** : relâcher une greffe libère un instant la
  Rouille qu'elle contenait avant que le noyau ne la neutralise à nouveau ailleurs — cohérent avec
  l'idée qu'une greffe assimilée reste « vivante » tant qu'elle est portée.
- **Un Arpenteur qui pousserait le Protocole trop loin** (au-delà de ce que son noyau peut tenir)
  risquerait de perdre le contrôle de la domestication — piste narrative pour une fin d'exploitation
  future (échos, complétions, ou un antagoniste/mini-boss "Arpenteur corrompu" qui aurait échoué le
  Protocole), sans rien contredire de l'existant.

Ce cadrage explique aussi pourquoi le boss de fin, **Le Noyau Rouillé**, porte ce nom : il n'est pas
une simple grosse créature, mais un ancien noyau de stabilisation — de Sanctuaire ou d'Arpenteur —
qui a cédé, où l'équilibre entre Aether et Rouille s'est inversé. Le joueur, en l'affrontant, voit
ce qui arrive quand le Protocole échoue ; en l'assimilant à travers ses greffes tout au long de la
run, il en démontre l'inverse.

## 4. Personnages jouables

Trois archétypes prévus à terme (un seul requis pour le MVP, cf. §13) :

- **Humain (Survivant)** — pas d'augmentation lourde, joue sur l'agilité et les reliques
  magiques ; profil "burst/évasif".
- **Cyborg (Greffé)** — fusion homme-machine, profil équilibré, mécanique de surchauffe
  (puissance accrue en prenant un risque de surchauffe). **Recommandé comme personnage du MVP** :
  il incarne le thème central du jeu (fusion homme/machine/magie) et permet de démontrer aussi
  bien des power-ups "tech" que "Aether" sans contrainte de lore.
- **Robot (Automate)** — tanky, pas de régénération de vie classique mais gestion d'une jauge
  d'énergie, profil "zone control".

> Décision à valider en Phase 0 avec l'agent `game-designer` : confirmer que le Cyborg est bien le
> personnage du MVP, ou trancher pour un autre archétype.

## 5. Boucle de gameplay (run)

1. **Sélection** (menu principal → Hub) : choix du personnage (un seul au MVP) et de son arme de
   départ.
2. **Run chronométrée** (cible 15–20 min, ajustable) dans une arène (cf. §10), vagues d'ennemis
   qui montent en difficulté avec le temps.
3. **Ramassage de XP** (orbes d'Aether lâchées par les ennemis) → montée de niveau → écran de
   choix (3 cartes parmi armes/passifs/évolutions disponibles).
4. **Fusions/évolutions** : quand une arme atteint son niveau max *et* qu'un passif prérequis est
   possédé, elle propose une fusion en forme évoluée (cf. §7 et §8, mécanique inspirée
   d'Everything is Crab).
5. **Fin de run** : mort du joueur, ou survie jusqu'au bout du timer (extraction). Dans les deux
   cas, les Noyaux d'Aether collectés pendant la run sont convertis en **monnaie meta**
   (cf. §9), même en cas de mort (principe roguelite : on progresse même en échouant).
6. **Retour au Hub** : dépense de la monnaie meta en améliorations permanentes valables sur
   toutes les runs futures.

## 6. Système de progression in-run

### XP et seuils de niveau

- **Valeur des orbes XP** (par ennemi) — système 4 tiers (cf. §19) :

  | Ennemi | XP | Tier orbe | Couleur |
  |---|---|---|---|
  | Essaim de Rouille | 3 | T1 | 🟢 Vert #44FF66 |
  | Drone Corrompu | 7 | T2 | 🔵 Cyan #44AAFF |
  | Sentinelle Corrompue | 20 | T3 | 🟣 Violet #AA44FF |
  | Colosse Greffé | 60 | T4 | 🟡 Or #FFD700 |
  | Rôdeur de Rouille *(mini-boss)* | 80 | T4 | 🟡 Or #FFD700 |
  | Sentinelle Maîtresse *(mini-boss)* | 120 | T4 | 🟡 Or #FFD700 |

- **Formule des seuils** (inspirée de Vampire Survivors, dataminée 2026-06-24) :
  - L1–L19 : `xpToNext(L) = 5 + (L-1) × 10` — identique VS, linéaire +10/niveau
  - L20 : **390 XP** — mur volontaire ×2 (signal mi-run ; VS fait ×4 pour des runs 30+ min)
  - L21+ : `xpToNext(L) = 208 + (L-21) × 13` — phase endgame +13/niveau (VS phase 2)
  - Niveau 1→2 : **5 XP** (2 kills Essaim de Rouille — quasi-immédiat)
  - Niveau 5→6 : 45 XP
  - Niveau 10→11 : 95 XP
  - Objectif : level 10 en ~3 min, level 20 en ~12 min sur une run de 15 min
- **Heal au level-up** : chaque montée de niveau restaure **25% des HP max** (flash vert joueur)
- **Niveau max par run** : 30
- **Rayon d'aspiration orbes XP** : 80 px automatique (300 px/s), au-delà contact physique requis

### Drops HP en run

- **Orbe HP** (losange rouge 10×10 px) : droppé aléatoirement à la mort de chaque ennemi
  - Chance drop : **8%** ennemis normaux / **25%** mini-boss
  - Effet au contact joueur : **+15% HP max**
  - Non magnétisé : le joueur doit marcher sur l'orbe (prise de risque intentionnelle)
  - PointLight2D rouge pulsant pour la lisibilité en combat dense

### Item Aimant (aspiration d'XP) — ajouté 2026-06-28

- **Aimant** (fer à cheval gris + pointes rouges, halo cyan pulsant) : **item à apparition programmée**, pas un drop d'ennemi.
  - Apparition via `MagnetSpawner` (Node dans `Game.tscn`) : **au maximum 3 fois par run**, à des positions aléatoires dans l'arène (≥ 150 px des murs).
  - Fenêtres temporelles : ~2-5 min, ~6-10 min, et **une « proche de la fin »** (~12-13 min, autour de l'arrivée du boss final). Aucune apparition après la fin de run.
  - Effet au contact du joueur : attire **toutes les orbes d'XP présentes dans l'arène** vers lui (`XpOrb.ForceMagnet` → attraction à toute distance, ~2,5× la vitesse de magnétisation normale). Aspiration globale façon Vampire Survivors.
  - Non magnétisé : le joueur doit marcher dessus (comme l'orbe HP).

### Choix de montée de niveau

3 propositions aléatoires pondérées par rareté :

| Rareté | Poids | Couleur |
|---|---|---|
| Commun | 60 | Gris (#AAAAAA) |
| Rare | 30 | Bleu (#44AAFF) |
| Épique | 10 | Violet (#CC44FF) |

**Règles de présentation des 3 cartes** :
1. Jamais 2 cartes du même id dans les 3 propositions.
2. Une fusion est forcée si conditions remplies depuis ≥ 1 niveau sans avoir été proposée.
3. Si moins de 3 cartes uniques disponibles, compléter avec un bonus de 50 XP à la place.
4. Transition écran level-up : fond instantané + scale-in des 3 cartes sur 0,08 s, durée perçue < 0,2 s.

- **Fusions/évolutions** : transforment une arme + un passif en une forme unique (nouveau
  sprite/VFX, comportement modifié, saut de puissance net).

> Détail complet des raretés par carte et règles de tirage dans `data/levelup_config.json`.

## 7. Ennemis (4 archétypes + 2 mini-boss)

> Valeurs de base (sans scaling temporel). Formule scaling : `stat = stat_base × (1 + t_minutes × coefficient)`.
> Détail complet dans `data/enemies.json`. Design mini-boss en §19.

| Ennemi | HP | Vitesse (px/s) | Dégâts | XP | Spawn (min) | Max simultané |
|---|---|---|---|---|---|---|
| Essaim de Rouille | 20 | 120 | 5/s contact | 3 | 0:00 | illimité |
| Drone Corrompu | 15 | 220 | 8/s contact | 7 | 2:00 | illimité |
| Sentinelle Corrompue | 45 | 70 | 12/projectile | 20 | 5:00 | illimité |
| Colosse Greffé | 200 | 55 | 20/s contact | 60 | 9:00 | illimité |
| **Rôdeur de Rouille** | 300 | 85 | 15/s contact | 80 | 12:00 | **1** |
| **Sentinelle Maîtresse** | 450 | 50 | 18/projectile ×2 | 120 | 16:00 | **1** |

### Comportements IA

**Essaim de Rouille** (`straight_chase`, poids spawn 10)
- Fonce en ligne droite vers le joueur, pas d'esquive.
- Dégâts de contact si distance < 24 px, toutes les secondes.
- Scaling HP : +8%/min (36 HP à t=10 min).

**Drone Corrompu** (`erratic_chase`, poids spawn 7, dès 2:00)
- Se dirige vers le joueur mais dévie aléatoirement de ±45° toutes les 0,4–0,8 s.
- Très rapide (220 px/s), très fragile (15 HP) — meurt en 1–2 balles Canon.
- Force le joueur à rester mobile et à anticiper les trajectoires.

**Sentinelle Corrompue** (`ranged_kiter`, poids spawn 4, dès 5:00)
- Se maintient à 200–350 px du joueur.
- Si joueur < 200 px : recule en diagonale. Si joueur > 350 px : avance lentement.
- Tire un projectile (180 px/s, durée 3 s) toutes les 2,5 s.
- Scaling HP : +10%/min.

**Colosse Greffé** (`slow_hunter`, poids spawn 2, dès 9:00)
- Avance inexorablement, non dévié par les autres ennemis.
- Frappe toutes les 1,5 s à portée de 36 px : 20 dégâts.
- Scaling HP : +12%/min (560 HP à t=15 min).

**Mini-boss implémentés** (cf. §19 pour le design complet) : Rôdeur de Rouille (dès 12 min) et Sentinelle Maîtresse (dès 16 min) — chacun drop un écran de choix d'arme à sa mort.

Post-MVP : boss de fin de run ("Le Noyau Rouillé") qui conditionne l'extraction.

## 8. Power-ups / Armes (MVP = 8 cartes + 2 fusions)

> Valeurs complètes par niveau dans `data/weapons.json`.

### Armes actives (4) — niveau max 5

**Canon à Impulsions** — Commun
| Niv | Dégâts | Cooldown | Projectiles | Perforation |
|---|---|---|---|---|
| 1 | 10 | 0,80 s | 1 | Non |
| 2 | 14 | 0,75 s | 1 | Non |
| 3 | 18 | 0,70 s | 1 | Oui |
| 4 | 22 | 0,65 s | 2 cibles | Oui |
| 5 | 28 | 0,55 s | 2 cibles | Oui |
Fusion : + Capaciteur → **Rail Surchargé**

**Lame Plasma** — Commun
| Niv | Dégâts | Cooldown | Arc | Rayon |
|---|---|---|---|---|
| 1 | 18 | 1,20 s | 180° | 80 px |
| 2 | 24 | 1,10 s | 200° | 90 px |
| 3 | 30 | 1,00 s | 220° | 100 px |
| 4 | 38 | 0,90 s | 260° | 110 px |
| 5 | 48 | 0,80 s | 320° | 120 px |
Fusion : + Noyau Thermique → **Lame à Fusion**

**Essaim de Drones** — Rare — orbite fixe 70 px
| Niv | Dégâts | Drones | Vitesse orbite |
|---|---|---|---|
| 1 | 12 | 2 | 120°/s |
| 3 | 18 | 3 | 160°/s |
| 5 | 28 | 4 | 200°/s |

**Champ de Surcharge** — Rare
| Niv | Dégâts | Cooldown | Rayon | Knockback |
|---|---|---|---|---|
| 1 | 8  | 2,5 s | 100 px | 40 px |
| 3 | 16 | 2,0 s | 145 px | 50 px |
| 5 | 30 | 1,5 s | 200 px | 60 px |

### Passifs (4) — niveau max 3

**Noyau Thermique** — Commun : +15% dégâts/niveau (max +45%). Prérequis Lame à Fusion.

**Plaque Renforcée** — Commun : +25 HP max/niveau + réduction dégâts reçus (0 / -10% / -20%, hardcap 40%).

**Servo-Moteurs** — Commun : +30/+30/+40 px/s (max +100 px/s, plafond soft 380 px/s).

**Capaciteur** — Rare : -12% / -24% / -38% cooldown. Cooldown min 0,15 s (hardcap). Prérequis Rail Surchargé.

### Fusions MVP (2)

**Lame à Fusion** — Épique
- Condition : Lame Plasma niveau 5 + Noyau Thermique possédé.
- Anneau continu 360°, 55 dps, rayon 130 px, pulse toutes les 0,15 s. Plus de cooldown.
- VFX : anneau GPUParticles2D cyan-magenta + animation métamorphose 8 frames 32×32.

**Rail Surchargé** — Épique
- Condition : Canon à Impulsions niveau 5 + Capaciteur possédé.
- Rafale de 3 projectiles (22 dégâts chacun, intervalle 0,12 s), cooldown entre rafales 0,6 s. Perforation infinie. Vitesse 600 px/s. DPS effectif ~69 dps.
- VFX : sillage cyan + animation métamorphose 10 frames 32×32.

Toute fusion supplémentaire est hors-scope MVP (cf. §14).

## 9. Monnaie meta : les **Échos d'Aether**

> Spécifié le 2026-06-20 par l'agent `game-designer`. **Rééquilibré le 2026-07-02** suite au
> passage au modèle « survie sans fin » (`docs/LEVEL_PROGRESSION_PLAN.md`, verrouillé
> 2026-06-30) : au-delà de `runDurationSeconds` (arrivée du boss de fin de niveau, 780 s / 13 min),
> la run entre en **overtime** et ne se termine plus qu'à la mort du joueur — temps survécu, kills
> et Noyaux ne sont plus bornés par un timer. L'ancienne formule linéaire sans plafond permettait à
> une excellente run overtime de rapporter plusieurs milliers d'Échos en un seul run (vs ~240 pour
> une run complète avant ce changement), vidant l'intégralité du Hub en 1 à 3 runs. Le §9.2
> ci-dessous documente la nouvelle formule à plafond souple qui corrige ce problème. Valeurs de
> tuning dans `data/meta_upgrades.json`.

### 9.1 Noyaux d'Aether (pickups en run)

Les Noyaux d'Aether sont des collectables **distincts des orbes XP** :

- **Apparence placeholder** : Polygon2D violet `#AA44FF` (bien distinct du jaune-vert `#AAFF44`
  des orbes XP).
- **Deux sources de spawn** :
  1. **Périodique** : 1 Noyau spawn toutes les **45 s** à une position aléatoire à l'intérieur
     de l'arène (min 150 px des murs), indépendamment des ennemis.
  2. **Drop ennemi** : chaque **Colosse Greffé** lâche 1 Noyau à sa mort (100% de chance).
     Logique : l'ennemi le plus difficile → récompense tangible et mémorable.
- **Ramassage MANUEL** uniquement — pas d'aspiration automatique. Rayon de collecte : **20 px**
  (contact quasi-direct). Objectif de design : forcer le joueur à se déplacer et prendre des
  risques pour récupérer les Noyaux, créant une tension supplémentaire.
- **Valeur en Échos** : voir formule §9.2 — chaque Noyau ramassé contribue **5 Échos** au total.

### 9.2 Formule de calcul des Échos en fin de run — v2 (2026-07-02, à plafond souple)

La run n'est plus bornée par un timer (cf. §9.3) : temps survécu, kills et Noyaux peuvent croître
indéfiniment en overtime. La formule sépare donc une **partie standard** (identique à l'esprit de
la v1, plafonnée à ce qu'une run « cible » de 13 min rapporte) et un **bonus de surcharge**
(overtime) fortement amorti et lui-même plafonné, pour qu'une run exceptionnellement longue ne
rapporte jamais plus qu'une fraction bornée au-dessus d'une run standard complète.

```
tStd = min(tempsSurvécuSecondes, capTimeSecs)      // capTimeSecs = 780 (= runDurationSeconds)
kStd = min(kills, capKills)                        // capKills = 520
nStd = min(noyauxRamassés, capCores)                // capCores = 22

Échos_standard = floor(tStd / 20) + floor(kStd / 10) + (nStd × 5) + 10

tOver = max(0, tempsSurvécuSecondes - capTimeSecs)
kOver = max(0, kills - capKills)
nOver = max(0, noyauxRamassés - capCores)

Bonus_surcharge_brut = floor(tOver/20 × 0,15) + floor(kOver/10 × 0,15) + floor(nOver×5 × 0,15)
Bonus_surcharge = min(Bonus_surcharge_brut, 100)     // overtimeBonusCap

Échos = Échos_standard + Bonus_surcharge
```

Paramètres `data/meta_upgrades.json` → `echoesFormula` : `timeDiv=20`, `killDiv=10`, `coreMult=5`,
`baseBonus=10` (inchangés depuis la v1), plus 5 nouveaux : `capTimeSecs=780`, `capKills=520`,
`capCores=22`, `overtimeDampening=0.15`, `overtimeBonusCap=100`.

**Calibration :**

| Scénario | Temps | Kills | Noyaux | Échos standard | Bonus surcharge | **Total** |
|---|---|---|---|---|---|---|
| Mort en 30 s | 30 s | 0 | 0 | 11 | 0 | **11** |
| Run 3 min sans mourir | 180 s | 120 | 4 | 51 | 0 | **51** |
| Run 5 min (moyenne) | 300 s | 250 | 8 | 90 | 0 | **90** |
| Run complète, boss vaincu, sans overtime | 780 s (13 min) | 520 | 22 | 211 | 0 | **211** |
| Run + overtime modeste (+5 min après boss) | 1080 s (18 min) | 920 | 29 | 211 | 13 | **224** |
| Run + overtime excellente | 2400 s (40 min) | 3000 | 60 | 211 | 77 | **288** |
| Run + overtime extrême (plafond atteint) | 3600 s (60 min) | 8000 | 100 | 211 | 100 | **311** |

Comparaison directe avec l'ancienne formule (v1, sans plafond) sur le scénario « extrême » :
`floor(3600/20) + floor(8000/10) + 100×5 + 10 = 180 + 800 + 500 + 10 = 1490 Échos` — soit près du
coût total de l'ancien arbre (6960 Échos, §18.2) en **une seule run**. La v2 plafonne cet écart à
**+100 Échos maximum** au-delà d'une run standard complète, quelle que soit la durée de survie
ensuite.

Objectifs atteints :
- Runs courtes (30 s à 5 min) : valeurs **identiques à l'ancienne calibration** (51-90 Échos) —
  aucune régression pour les joueurs qui découvrent le jeu.
- Run standard complète (13 min, boss vaincu, sans pousser l'overtime) : **211 Échos** — dans la
  même fourchette que l'ancien plafond de fait (240 pour 15 min), très légèrement réduit car la
  cible standard est plus courte (13 min vs 15 min).
- Overtime : **rendements fortement décroissants** au-delà de la run standard, plafonnés à **+100
  Échos** cumulés quelle que soit la durée de survie ensuite. Une run de bon niveau (quelques
  minutes d'overtime) profite encore d'un vrai bonus (+13 à +30 environ) ; au-delà d'environ 5-10
  minutes d'overtime, le bonus sature.
- Lisibilité : 5 composantes affichées séparément sur l'écran de fin (§9.4), dont un nouveau
  « Bonus de Surcharge » qui rend le plafond transparent pour le joueur plutôt que de le cacher.

### 9.3 Timer de run et conditions de fin — modèle « survie sans fin » (2026-06-30)

> Voir `docs/LEVEL_PROGRESSION_PLAN.md` pour le détail complet des étapes d'implémentation.

- **Durée standard** : `runDurationSeconds` = 780 s (13 min) — c'est le délai avant l'arrivée du
  **boss de fin de niveau** (Le Noyau Rouillé, §20), pas la fin de la run.
- **Affichage** : compte à **rebours** (13:00 → 0:00) jusqu'à l'arrivée du boss, puis bascule en
  affichage du **temps survécu total** (compte croissant) une fois en overtime.
- **À 0:00 (`RunStatsTracker.Overtime`)** : pas de fin de run. Bannière **« OVERTIME »** ;
  escalade brutale gérée par `EnemySpawner`/`SpawnCurve` — vagues massives, mini-boss et boss en
  boucle à cadence croissante (garde-fou perf : cap 300 ennemis simultanés maintenu).
- **Boss de fin de niveau vaincu** (`RustedCore` → `RunStatsTracker.OnLevelBossDefeated()`) :
  marque le **niveau TERMINÉ** (bannière + déblocage du biome suivant + persistance de la
  complétion), mais **ne termine pas la run** — l'overtime continue.
- **Mort du joueur** : seule condition de fin de run (`EndRun("death")`). Enregistre le **high
  score** (temps survécu, cf. `docs/LEVEL_PROGRESSION_PLAN.md` étape 4) et calcule les Échos
  (§9.2). Label "MORT EN SERVICE".
- **Conséquence directe sur l'économie meta** : puisque temps/kills/Noyaux ne sont plus bornés par
  un timer de fin, la formule d'Échos (§9.2) doit elle-même porter le plafond — c'est le rôle du
  `capTimeSecs`/`capKills`/`capCores`/`overtimeBonusCap`.

### 9.4 Écran de fin de run

L'écran affiche dans l'ordre :
1. **Cause de fin** : "NIVEAU TERMINÉ" (si le boss a été vaincu pendant la run, en plus de la
   cause finale) puis "MORT EN SERVICE" — palette visuelle distincte (or pour la complétion,
   rouge-rouille pour la mort). Il n'y a plus de "EXTRACTION REUSSIE" isolée : battre le boss ne
   termine plus la run (§9.3).
2. **Décompte animé** (style `sequential_countup`, ~0,8 s par composante) :
   - "Temps survécu" → +X Échos (plafonné à `capTimeSecs`)
   - "Ennemis éliminés" → +X Échos (plafonné à `capKills`)
   - "Noyaux récupérés" → +X Échos (plafonné à `capCores`)
   - **"Bonus de Surcharge"** → +X Échos *(nouveau 2026-07-02 — n'apparaît/n'anime que si > 0,
     c'est-à-dire si la run a dépassé un des plafonds standard ci-dessus, typiquement en overtime)*
   - "Bonus de run" → +10 Échos
   - → **TOTAL : X ÉCHOS**
3. **Nouveau record ?** (temps survécu, high score par niveau/difficulté) affiché si battu.
4. **Deux boutons** : "Retour au Hub" / "Rejouer".

Objectif : rendre la fin de run satisfaisante même après une mort — le joueur voit sa progression
meta et comprend immédiatement pourquoi rejouer, ET comprend visuellement pourquoi une run
overtime très longue ne rapporte pas des dizaines de fois plus qu'une run standard (le "Bonus de
Surcharge" rend le plafond explicite plutôt que de le cacher dans un calcul opaque).

### 9.5 Améliorations permanentes du Hub — v2 (17 améliorations, rééquilibré 2026-07-02 ; `starting_weapon_alt` retiré 2026-07-04)

Dépensées au Hub. Structure complète dans `data/meta_upgrades.json`. **Rééquilibrage complet**
suite au §9.2/§9.3 : les items MVP+post-MVP existants voient leurs prix augmenter modérément
(+8% à +29% selon leur puissance relative), et **10 nouveaux items** sont ajoutés pour étaler la
progression sur beaucoup plus de runs maintenant que l'économie n'explose plus en overtime.

> **Retrait de `starting_weapon_alt` « Prototype de Terrain » (2026-07-04)** : cette amélioration
> débloquait l'Essaim de Drones comme arme de départ *sélectionnable au Hub*, mais **aucun sélecteur
> d'arme de départ n'a jamais été câblé** (l'arme de départ est déterminée par le personnage choisi,
> cf. §4). L'achat était donc sans effet — retiré de `data/meta_upgrades.json`, des textes et des
> clés de localisation, avec la méthode morte `MetaProgressionSystem.GetUnlockedStartingWeapons`.
> L'Essaim de Drones reste jouable comme arme de départ du personnage qui le porte.

**Les 7 améliorations existantes (prix rééquilibrés, effets inchangés) :**

| id | Nom | Stat modifiée | Niveaux | Coûts par niveau | Bonus par niveau |
|---|---|---|---|---|---|
| `hp_boost` | Corps Renforcé | `MaxHp` | 4 | 90/180/300/480 | +20 HP |
| `damage_boost` | Calibration Offensive | `DamageMultiplier` | 5 | 130/230/380/580/850 | +10% dégâts |
| `speed_boost` | Servos Améliorés | `Speed` | 3 | 100/220/380 | +15 px/s |
| `cooldown_reduction` | Synchronisation Aether | `CooldownReduction` | 3 | 140/290/470 | -5% cooldown |
| `damage_reduction` | Blindage Composite | `DamageReduction` | 3 | 170/340/560 | -5% dégâts reçus |
| `reroll` | Recalibrage Tactique | consommable* | 3 | 160/320/520 | +1 reroll/run |
| `skip` | Esquive de Sélection | consommable* | 3 | 130/270/450 | +1 skip/run |

Sous-total : **7 740 Échos**.

**Les 10 nouvelles améliorations (2026-07-02) :**

| id | Nom | Stat modifiée | Niveaux | Coûts par niveau | Bonus par niveau |
|---|---|---|---|---|---|
| `hp_boost_2` | Plaque Blindée | `MaxHp` (tier 2) | 2 | 300/450 | +35 HP |
| `damage_boost_2` | Calibration Avancée | `DamageMultiplier` (tier 2) | 3 | 350/500/700 | +8% dégâts |
| `cooldown_reduction_2` | Synchronisation Aether II | `CooldownReduction` (tier 2) | 2 | 320/480 | -4% cooldown |
| `damage_reduction_2` | Blindage Composite II | `DamageReduction` (tier 2) | 2 | 350/550 | -4% dégâts reçus |
| `extra_life` | Noyau de Secours | `ExtraLifeCharges`* | 2 | 450/800 | +1 revive à 30% HP/run |
| `damage_absorb` | Plaque Adaptative | `DamageAbsorbCharges`* | 3 | 200/350/550 | +1 coup absorbé (0 dégât)/run |
| `hp_regen` | Auto-Réparation | `HpRegenPerSecond`** | 3 | 220/380/600 | +0,4 HP/s (max 1,2 HP/s) |
| `core_magnetism` | Résonance de Noyau | `CoreCollectionRadiusBonus`* | 3 | 160/280/450 | +15/+15/+20 px (20→70 px) |
| `overtime_stabilizer` | Stabilisateur de Surcharge | `OvertimeRampReduction`* | 3 | 450/750/1150 | -5% pente overtime (max -15%) |
| `bonus_magnet` | Aimant Auxiliaire | `BonusMagnetCharges`* | 2 | 200/350 | +1 apparition Aimant/run (3→5) |

Sous-total : **11 340 Échos**. **TOTAL ARBRE COMPLET (17 items) : 19 080 Échos.**

\* Consommables par run ou valeurs lues directement par le système consommateur — **PAS** des
champs `PlayerStats` (même pattern que `reroll`/`skip` déjà existants). \*\* `HpRegenPerSecond`
est le seul **nouveau champ `PlayerStats`** requis par ce rééquilibrage. Détail complet du câblage
de chaque id dans `data/meta_upgrades.json` → `_implementationNotes` et champs `_designNote` par
item.

**Principes de design (v2) :**
- Les 8 items historiques restent le socle accessible tôt (niveau 1 de chacun toujours atteignable
  en 1-2 runs standards, ~90-210 Échos).
- Les 10 nouveaux items sont des objectifs de **moyen à long terme** (jusqu'à 2 350 Échos pour
  `overtime_stabilizer` au maximum) — variété volontaire au-delà des simples +stats : un
  mécanisme de rachat de mort (`extra_life`), un amortisseur de chip damage (`damage_absorb`),
  une régénération passive (`hp_regen`), une réponse directe au point de douleur "overtime trop
  brutal" (`overtime_stabilizer`), et deux boosts d'économie in-run (`core_magnetism`,
  `bonus_magnet`).
- Aucune amélioration ne rend le jeu trivial seule : le scaling des ennemis compense sur les runs
  longues, et les items les plus puissants (`overtime_stabilizer`, `damage_boost_2`) sont aussi
  les plus chers.
- Objectif de durée de vie de l'arbre complet : **plusieurs dizaines de runs** (là où l'ancienne
  économie avant le passage en overtime sans plafond permettait de tout débloquer en 1-3 runs
  exceptionnelles — cf. §9 intro).
- `bonus_magnet` est un objectif bon marché côté économie XP ; `core_magnetism` en est le pendant
  côté ramassage de Noyaux.

**Hardcaps meta cumulés (meta + passifs en run), mis à jour avec les tiers 2 :**
- `DamageReduction` : meta max -15% (`damage_reduction`) -8% (`damage_reduction_2`) = -23% + Plaque
  Renforcée -20% (in-run) = -43% → **plafonné au hardcap global 40%** (dernier point du tier 2
  partiellement "gâché" si le passif in-run est aussi maxé — volontaire, chasse aux marges pour
  joueurs perfectionnistes).
- `CooldownReduction` : meta max -15% (`cooldown_reduction`) -8% (`cooldown_reduction_2`) = -23% +
  Capaciteur -38% (in-run) = -61% → hardcap 0,15 s/arme s'applique (inchangé).
- `Speed` : meta max +45 px/s (245 px/s départ, aucun tier 2 ajouté volontairement — item déjà
  bien dimensionné) + Servo-Moteurs +100 (in-run) = 345 px/s (sous le plafond soft 380 px/s). ✓
- `MaxHp` : meta max +80 (`hp_boost`) +70 (`hp_boost_2`) = +150 HP permanents, aucun hardcap
  (le scaling ennemi absorbe la marge sur les runs longues, cf. §17).

## 10. Niveau / Arène (MVP = 1 arène complète et soignée)

**"Le Sanctuaire en Ruines"** : architecture magique en décomposition envahie par des structures
technologiques rouillées. Éléments attendus :
- Tilemap avec collisions (murs de ruines, débris infranchissables)
- Décor destructible mineur (caisses qui lâchent des pickups)
- Au moins un hasard environnemental (ex. geyser d'Aether qui inflige des dégâts de zone
  périodiques) pour casser la lisibilité plate d'une arène vide
- Lisibilité prioritaire sur la densité : le joueur doit toujours distinguer son personnage,
  les ennemis et les projectiles dans le chaos visuel d'une run avancée

**État Phase 4 (2026-06-21) :** arène intérieure jouable **1920×1216 px** (60×38 tiles 32 px).
Taille totale avec murs 1984×1280 px. `GroundRenderer` procédural (seed 42) — distribution
72/18/5/4/1% (base/variation/fissure/rouille/débris). Obstacles collidables : 5 Piliers de
Sanctuaire (type A, `CapsuleShape2D` r=12, sprite 32×64) + 2 Épaves de Machine (type B,
`RectangleShape2D(56,24)`, sprite 64×32). Types C (caisse) et D (arche) : sprites produits,
intégration code reportée en P2. Geysers repositionnés à (-500,-250) et (480,300), éclairés
par `PointLight2D` energy 0.4 (inactif) ↔ 1.8 (actif). `Camera2D` limits ±960/±608.
Brief complet : `docs/ARENA_DA_BRIEF.md`.

## 11. UI / UX

- **Splash screen** : image clé (key art) du personnage du MVP dans le Sanctuaire en Ruines,
  affichée à l'ouverture avant/au menu principal
- **Menu principal** : Jouer / Hub-Boutique (améliorations meta) / Options / Quitter
- **HUD en run** : panel sombre semi-transparent (gauche), barre HP 18px avec drain animé
  et pulsation basse vie, barre XP 12px avec flash au level-up, label `LV N`, sous-label XP
  `cur / max XP`, timer encadré (centre haut), compteur Noyaux ⬡ (droite haut)
- **Écran de montée de niveau** : 3 cartes de choix, pause du jeu pendant le choix
- **Menu pause**

## 12. Direction artistique (résumé — détaillée par `directeur-artistique` et `graphiste`)

Palette suggérée : tons sombres/désaturés de ruines (gris, brun rouille) percés de couleurs
saturées d'Aether (cyan, magenta, vert toxique) pour tout ce qui est magique/actif — XP, VFX de
sorts, énergie des cyborgs/robots. Cette opposition "matière morte / énergie vivante" doit guider
toutes les décisions visuelles (cf. brief détaillé dans l'agent `directeur-artistique`).

> **Guide de style complet produit le 2026-06-20 : `docs/STYLE_GUIDE.md`** — palette 32 couleurs,
> briefs personnage/ennemis/armes/VFX/UI, spécifications techniques de production (frames,
> nommage, priorité de livraison). C'est la source de vérité opérationnelle pour `graphiste`.
> Le §12 ci-dessous reste le résumé décisionnel ; `docs/STYLE_GUIDE.md` contient les hex codes,
> dimensions et nombres de frames exacts.
>
> **Refonte "pseudo-3D avec ombres" (2026-07-02) : `docs/ART_BRIEF_PSEUDO3D.md`** — passage de
> TOUS les sprites (personnages, ennemis, obstacles, tuiles de sol, icônes) à un ombrage
> volumétrique cohérent (lumière fixe haut-gauche 45°, dérivation HSV highlight/shadow/contact,
> ombre portée elliptique au sol), via une bibliothèque PIL partagée `tools/pseudo3d_lib.py`.
> Ne change ni la résolution (32×32, 48×48 Colosse), ni la palette, ni le nombre de frames :
> c'est une couche de rendu, pas une nouvelle politique de production.

**Décisions actées par `directeur-artistique` (2026-06-19) :**

- **Résolution des tiles** : 32×32 px. Compromis lisibilité des silhouettes en combat dense
  (16×16 trop serré) / volume de production acceptable au MVP (64×64 trop long). Aucune exception
  sans justification explicite.
- **texture_filter global** : `Nearest` (pixel art). Posé au niveau projet Godot — aucun import
  de sprite ne doit le contredire par défaut.
- **Post-processing MVP (in-scope)** :
  - Bloom/glow Aether (`WorldEnvironment.glow`) : indispensable MVP. Sans lui, l'opposition
    "matière morte / énergie vivante" s'effondre (orbes XP, VFX sorts, yeux ennemis corrompus
    doivent briller). Signature visuelle du jeu, pas un ornement.
  - Flash de désaturation totale (0,3 s) lors d'une fusion/évolution : frame en niveaux de gris
    qui "reset" l'œil avant l'éclatement de la nouvelle palette. Sert directement le différenciateur
    narratif du §2. **Implémenté 2026-06-21** — `FusionFlash` AutoLoad (`CanvasLayer` layer=99),
    `ColorRect` blanc `Color(1,1,1,*)` animé sur `color:a` (montée 0.1s + descente 0.25s = 0.35s
    total), `ProcessMode=Always` + `TweenPauseMode.Process` pour survivre à la pause LevelUpScreen.
- **Post-processing post-MVP** :
  - Chromatic aberration sur les hits violents (risque de lisibilité en combat dense, nécessite
    tests joueurs avant intégration).
  - Vignette bords d'écran (confort visuel, aucun impact lisibilité combat).
- **VFX d'évolution/fusion** : combinaison obligatoire — animation frame-by-frame (8–12 frames,
  32×32) pour la métamorphose de silhouette + `GPUParticles2D` pour l'aura Aether pendant et
  après la transition. Les particules seules produisent un "pop" sans lecture narrative.

**Décisions actées par `directeur-artistique` (2026-06-24) — Phase "Polish next-level" :**
Brief complet : `docs/VISUAL_POLISH_BRIEF.md`.

- **Sol holographique animé (shader P0)** : `ShaderMaterial` sur le `Polygon2D` dark-overlay
  existant (ZIndex=-7). Grille de circuits holographiques `#00A0BB` à opacité 0.06, animée
  via `TIME`. Renforce l'identité futuriste sans concurrencer les entités. Aucun nouveau sprite
  requis — shader GLSL pur.
- **Vignette écran (shader P0, sorti de post-MVP)** : promue en scope polish. `ColorRect`
  plein écran ZIndex=90 (sous FadeOverlay 100) avec `ShaderMaterial` vignette radiale
  `smoothstep`. Opacité max 0.72, couleur `#000005`. Concentre l'attention sur le centre
  de l'action en combat dense — améliore la lisibilité, ne la nuit pas.
- **Screen shake (P0)** : `Camera2D` offset animé via `Tween` sans modifier `GlobalPosition`
  du joueur. Amplitudes : mort ennemi faible 3 px / 0.12 s, mort Colosse 12 px / 0.35 s,
  mort joueur 20 px / 0.5 s, niveau up 6 px / 0.2 s. Décroissance `EaseOut`.
- **Hit stop (P1)** : freeze `Engine.TimeScale = 0.05` pendant 2–3 frames (0.033–0.050 s)
  sur mort Colosse et fusion uniquement. Restauration immédiate via `Tween` sur `TimeScale`.
  Crée la "satisfaction" de l'impact lourd sans interrompre le gameplay.
- **Shockwave ring (P1)** : `ShaderMaterial` sur `Sprite2D` 256×256 px blanc instancié à la
  mort du Colosse. Shader GLSL anneau `smoothstep` qui s'expand de rayon 0 → 1.0 en 0.4 s
  avec `BlendMode Add`. Budget : 1 instance max simultanée.
- **Chromatic aberration sur fusion (P1)** : shader screen-space sur `CanvasLayer` ZIndex=89,
  déclenché pendant `FusionFlash`. Décalage RGB ±4 px, durée 0.3 s. Contexte spécifique
  (fusion = pause, pas de combat dense) — risque lisibilité nul, sorti de post-MVP.
- **Trail joueur (P1)** : `GPUParticles2D` enfant de `Player.tscn`, amount=6, lifetime=0.1 s,
  couleur `#44FFEE` → transparent. Actif uniquement en mouvement (`Velocity.Length() > 10`).
- **Outline glow ennemis (P2)** : shader `canvas_item` sur chaque `AnimatedSprite2D` ennemi.
  Couleur outline `#FF2200` épaisseur 1 px, pulsation `sin(TIME)` opacité 0.3→0.7. Améliore
  la lisibilité silhouette en fond sombre. Coût GPU : surveiller sur 200 ennemis.
- **Bloom renforcé (P0)** : `glow_intensity` 0.8 → 1.4, `glow_strength` 1.2 → 1.8,
  `glow_hdr_threshold` 0.6 inchangé (les matières mortes ne doivent pas bloomer).
  Les `PointLight2D` existants (joueur, balles, geysers) bénéficient directement de ce renfort.
- **Vignette dynamique — VignetteFollow.cs (bugfix 2026-06-24)** : la vignette centrée fixe
  sur `vec2(0.5)` créait un bug en bord de carte — quand la caméra est bloquée par ses limites,
  le joueur se décale vers le bord de l'écran et entre dans la zone sombre. Fix :
  `VignetteFollow.cs` (Node dans `Game.tscn`) met à jour le uniform `center` du shader chaque
  frame via `Camera2D.GetScreenCenterPosition()` (tient compte du clamp de caméra) +
  offset joueur / taille viewport normalisé 0-1. `GetFinalTransform()` Godot 4 ne retourne
  pas le transform caméra 2D — `GetScreenCenterPosition()` est la seule API fiable.
- **HUD layer=95** : au-dessus de la vignette (layer=90) — HP bar, XP bar et timer ne sont
  plus assombris par la vignette.
- **Règle de lisibilité absolue (non négociable)** : tout effet P1 ou P2 qui, en test
  combat dense (≥100 ennemis), réduit la distinction joueur/ennemi/projectile en dessous
  de 150 ms de lecture est automatiquement désactivé ou rétrogradé en P3.

## 13. Périmètre du MVP — Definition of Done

Le MVP doit être **un jeu complet et jouable de bout en bout**, contenant :

- [x] 1 personnage jouable (Cyborg recommandé, cf. §4) — livré Phase 1-3
- [x] 4 types d'ennemis minimum (cf. §7) — livré Phase 2 (RustSwarm, Drone, Sentinelle, Colosse)
- [x] 6 à 8 power-ups minimum, actifs + passifs (cf. §8) — livré Phase 2 (4 armes + 4 passifs + 2 fusions)
- [x] Mécanique de montée de niveau en run avec écran de choix (cf. §6) — livré Phase 2
- [x] Monnaie meta fonctionnelle (gain en run, dépense au Hub, persistance entre les runs)
  (cf. §9) — livré Phase 2
- [x] 1 arène complète avec gestion des collisions (cf. §10) — livré Phase 4 (1920×1216, obstacles A–D)
- [x] Éléments graphiques : sprites (personnage, ennemis, projectiles), effets graphiques
  (VFX de hit, de mort, d'évolution) — livré Phase 3-4 + hit flash Phase 5
- [x] Musique et sons (au moins : thème de menu, thème de run, SFX de tir/hit/mort/level-up)
  — livré Phase 3 (WAV synthétiques CC0) puis **remplacés par assets définitifs 2026-06-22** (31/31 CC0 : 24 SFX + 2 stingers Kenney.nl, 5 musiques longues Juhani Junkala)
- [x] Menu principal avec splash screen — livré Phase 3b
- [x] Sauvegarde locale persistante de la monnaie meta et des améliorations achetées
  — livré Phase 2
- [x] Build Windows exécutable (.exe) packagée et lançable hors environnement de dev
  — livré Phase 4, rebuild 2026-06-24 (`build/ChimeraProtocol.exe` 168 MB + `data_ChimeraProtocol_windows_x86_64/`)

## 14. Hors-scope MVP (backlog post-MVP, à ne pas commencer avant validation du MVP)

- Personnages Humain et Robot jouables
- Biomes/arènes additionnelles
- Boss de fin de run (Le Noyau Rouillé — extracteur conditionnel)
- Plus de fusions/armes
- Support manette
- Succès Steam / intégration plateforme
- Coopération multijoueur
- Cosmétiques
- Fusions supplémentaires au-delà des 2 actées en §8 (Lame à Fusion + Rail Surchargé)

## 15. Pile technique

**Décision actée en Phase 0 (2026-06-19)** par l'agent `developpeur`, validée par
`game-designer` et `directeur-artistique`.

### Choix retenu : Godot 4 avec C# (.NET 8 / GodotSharp)

**Score comparatif (5 critères, note /5) :**

| Critère | Godot 4 C# | MonoGame | Unity |
|---|---|---|---|
| Vitesse itération MVP solo | 5 | 2 | 3 |
| Pipeline sprite/animation | 5 | 2 | 4 |
| Intégration audio | 5 | 2 | 4 |
| Build/packaging Windows | 5 | 3 | 3 |
| Réutilisation expertise C# | 4 | 5 | 4 |
| **Total** | **24/25** | **14/25** | **18/25** |

### Justification

- **Itération MVP** : sprite animé visible en moins de 10 minutes sans une ligne de code ;
  MonoGame exige plusieurs centaines de lignes avant la première image.
- **Pipeline sprite/animation** : `SpriteFrames` + `AnimatedSprite2D` + `TileMap` +
  `GPUParticles2D` éditables visuellement dans l'IDE — supprime toute une classe de bugs.
- **Essaim d'ennemis** : accès direct au `RenderingServer` pour bypass du scene tree, patterns
  de pooling via scènes instanciées. Cible MVP : 200–300 entités simultanées (pic < 250 actives).
- **Export Windows** : `godot4 --headless --export-release "Windows Desktop" ./build/ChimeraProtocol.exe`
  — produit un `.exe` + `.pck` packageable en ZIP, reproductible sans licence commerciale.
- **Licence MIT** : zéro contrainte pour un projet portfolio ou commercial futur.
- **C# .NET 8 natif** : LINQ, génériques, records, pattern matching — le porteur de projet
  retrouve ses habitudes. Seule courbe d'apprentissage : l'architecture Node/Scene, pas le langage.
- **VFX et shaders** : `GPUParticles2D` + `CanvasItemShader` + `WorldEnvironment.glow` —
  critique pour la DA "énergie vivante vs matière morte" (cf. §12).

### Contraintes de production validées

**Par `game-designer` (2026-06-19) :**
- Convention sprites : PNG transparent, grille uniforme 32×32 px, nommage `nom_action_NN.png`
- Fusions MVP : 2 uniquement (Lame à Fusion + Rail Surchargé) — les suivantes en §14
- Transition écran level-up : fond instantané + scale-in des 3 cartes sur 0,08 s via
  `AnimationPlayer`, durée totale perçue < 0,2 s. Pas de fade sur le fond de jeu.
- Performance essaims : cible 200–300 entités simultanées, pic < 250 actives

**Par `directeur-artistique` (2026-06-19) — voir détail en §12.**

### Versions et outils

- **Godot** : **4.7 .NET** (variante .NET obligatoire — la version standard ne supporte pas C#)
- **SDK** : .NET 8 SDK
- **IDE** : Rider ou VS 2022 avec plugin Godot Tools
- **Format assets** : PNG sprites 32×32, OGG musique, WAV ou OGG SFX, JSON tuning
- **Sauvegarde** : JSON local via `FileAccess` Godot (`user://save.json`)
- **Godot.NET.Sdk** : régénérer le `.csproj` via *Project → Tools → C# → Create C# Solution*
  après tout changement de version Godot (version SDK doit correspondre exactement à l'éditeur)
- **CRITIQUE export .NET** : le fichier `ChimeraProtocol.sln` DOIT être présent à la racine
  avant l'export. Sans lui, Godot exporte un `.exe` sans assemblée C# (crash `STATUS_ACCESS_VIOLATION`
  à l'adresse 0x0, `CORE API HASH: 0` dans les logs). Créer avec :
  `dotnet new sln --name ChimeraProtocol --format sln && dotnet sln ChimeraProtocol.sln add ChimeraProtocol.csproj`
  (noter le `--format sln` — .NET 10 crée `.slnx` par défaut, Godot cherche `.sln`).
  L'export produit `data_ChimeraProtocol_windows_x86_64/` (runtime .NET 8, GodotSharp, assemblées)
  indispensable pour distribuer. Ce dossier est ignoré par git, régénéré à chaque export.

### Structure de dossiers du projet

```
chimera-protocol/
├── docs/GDD.md
├── CLAUDE.md
├── project.godot
├── export_presets.cfg
├── src/
│   ├── Core/          (GameManager.cs, SaveManager.cs, Constants.cs)
│   ├── Entities/
│   │   ├── Player/    (Player.cs, PlayerStats.cs)
│   │   └── Enemies/   (EnemyBase.cs, RustSwarm.cs, …)
│   ├── Weapons/       (WeaponBase.cs, …)
│   ├── Systems/       (EnemySpawner.cs, ObjectPool.cs, XpSystem.cs,
│   │                   LevelUpSystem.cs, MetaProgressionSystem.cs, AudioSystem.cs)
│   └── UI/            (HUD.cs, LevelUpScreen.cs, MainMenu.cs, HubScreen.cs)
├── scenes/
│   ├── Main.tscn
│   ├── Game.tscn
│   ├── MainMenu.tscn
│   ├── Hub.tscn
│   ├── entities/
│   ├── weapons/
│   └── ui/            (HUD.tscn, LevelUpCard.tscn)
├── assets/
│   ├── sprites/       (player/, enemies/, weapons/, vfx/, ui/, tileset/)
│   ├── audio/         (music/, sfx/)
│   └── fonts/
├── data/              (weapons.json, enemies.json, levelup_cards.json,
│                       meta_upgrades.json)
└── build/             (gitignore)
```

## 16. Implémentation — état d'avancement

> Section mise à jour à chaque session. Cocher au fil des implémentations.

### Phase 1 — Prototype jouable (2026-06-19) ✓

Objectif : quelque chose de jouable au clavier sans art ni audio.

**Implémenté :**
- [x] Structure de dossiers complète (`src/`, `scenes/`, `assets/`, `data/`)
- [x] `project.godot` configuré (Godot 4.7, texture Nearest, résolution 1280×720, AutoLoad GameManager)
- [x] `GameManager` singleton AutoLoad (`src/Core/GameManager.cs`)
- [x] Joueur Cyborg — déplacement 8 directions, `CharacterBody2D`, `MoveAndSlide()`
- [x] `PlayerStats` resource (HP 100, Speed 200, Damage 10)
- [x] Arène de test (`scenes/Game.tscn`) — sol Polygon2D + 4 murs `StaticBody2D`
- [x] `Camera2D` attachée au joueur
- [x] Ennemi — Essaim de Rouille (`RustSwarm`) : fonce vers le joueur, dégâts de contact 5/s
- [x] `EnemySpawner` — spawn toutes les 2 s sur les 4 bords, limite 20 ennemis simultanés
- [x] Arme — Canon à Impulsions (`ImpulseCannon`) : auto-ciblage ennemi le plus proche, cooldown 0,8 s
- [x] Projectile `Bullet` (`Area2D`) — vitesse 400, auto-destroy 3 s, hit = TakeDamage
- [x] HUD minimal — label HP en polling
- [x] Placeholders visuels : `Polygon2D` (cyan joueur, rouge-rouille ennemi, jaune balle)
- [x] `icon.svg` placeholder (carré cyan sur fond sombre) — remplacer par le key art MVP en §12

**Décisions techniques actées en Phase 1 :**
- `CollisionMask = 0` sur les ennemis — ils traversent les murs (cohérent avec le genre
  survivor ; seul le joueur est contenu). Dégâts de contact gérés par distance dans le code.
- Spawn à l'intérieur des murs (±48 px des bords) et non à l'extérieur.
- `GD.Load<PackedScene>()` dans `_Ready()` pour les références de scènes — les `[Export]`
  restent optionnels, pas d'assignation manuelle dans l'éditeur nécessaire.

### Phase 2 — Terminée (2026-06-20)

**Implémenté le 2026-06-20 :**
- [x] `XpSystem` AutoLoad — signal `LevelUp`, formule `20*n²+40*n`, niveau max 30
- [x] `XpOrb` (Area2D) — magnet 80 px / 300 px/s, drop à la mort des ennemis
- [x] `PlayerStats` étendu — `DamageMultiplier`, `DamageReduction`, `CooldownReduction`, `BaseSpeed`, hardcaps
- [x] `EnemyBase` — drop XP, `ApplyScaling()`, dégâts réduits par `DamageReduction`, `_isDead` guard
- [x] 3 ennemis supplémentaires : `CorruptedDrone` (erratic_chase), `CorruptedSentinel` (ranged_kiter + `EnemyBullet`), `GraftedColossus` (slow_hunter, melee 1.5 s)
- [x] `EnemySpawner` refondu — lit `data/enemies.json`, scaling temporel, pool pondéré par `spawnWeight`, filtre `spawnStartMinute`
- [x] `InventorySystem` AutoLoad — armes/passifs/fusions, application des stats depuis JSON
- [x] 3 nouvelles armes : `PlasmaBlade` (arc mêlée), `DroneSwarm` (orbite N drones), `OverloadField` (pulse zone + knockback)
- [x] 2 fusions MVP : `FusionBlade` (anneau continu 55 dps), `RailOvercharged` (rafale 3 projectiles)
- [x] `ImpulseCannon` upgradé — `ProjectileCount`, `IsPiercing`, multi-cibles
- [x] `Bullet` upgradé — perforation optionnelle (`IsPiercing`)
- [x] `LevelUpSystem` AutoLoad — pool pondéré, fusion forcée, règle XP bonus si < 3 cartes
- [x] `LevelUpScreen` — pause jeu, 3 cartes colorées par rareté, scale-in 0.08 s via Tween
- [x] `HUD` — barre XP (ColorRect proportionnelle), label niveau, connecté aux signaux `XpSystem`
- [x] `project.godot` — nouveaux AutoLoad : `XpSystem`, `InventorySystem`, `LevelUpSystem`
- [x] `Game.tscn` — `LevelUpScreen` ajouté, `EnemySpawner` sans référence manuelle de scène

**Décisions techniques actées en Phase 2 :**
- `file sealed class` interdit dans les signatures de membres d'une classe non-`file` (CS9051) → utiliser `internal sealed class` pour les DTOs internes (ex. `EnemySpawnData`).
- `FileAccess` ambigu entre `Godot.FileAccess` et `System.IO.FileAccess` dès qu'on importe `System.Text.Json` → toujours qualifier `Godot.FileAccess.Open()` et `Godot.FileAccess.ModeFlags`.
- `AddChild` dans un callback physique lève une erreur Godot → utiliser `parent.CallDeferred(Node.MethodName.AddChild, node)` + `node.SetDeferred("global_position", pos)`. Appliqué dans `EnemyBase.SpawnXpOrb()`.
- Pression cumulée des ennemis (`CollisionMask = 0`) pousse le joueur hors des murs → clamp `GlobalPosition` à `±(ArenaWidth/2 - WallThickness)` après `MoveAndSlide()` dans `Player._PhysicsProcess()`.

- [x] `.gitignore` — exclut `.godot/`, `build/`, `bin/`, `obj/`, `*.exe`, `*.pck`, `.vs/`, `.idea/`

**Implémenté le 2026-06-20 (suite) :**
- [x] Monnaie meta (Échos d'Aether) + Hub + sauvegarde (`SaveManager`)
  - `SaveManager` AutoLoad — sérialise/désérialise `user://save.json` via `System.Text.Json`
  - `MetaProgressionSystem` AutoLoad — charge `data/meta_upgrades.json`, expose `AddEchoes`, `TryPurchase`, `ApplyMetaBonusesToStats`, `GetStartingXp`
  - `RunStatsTracker` (Node dans Game.tscn) — tracke temps/kills/noyaux, calcule Échos, appelle `EndRun`
  - `AetherCore` pickup (Area2D violet #AA44FF, rayon 20 px, ramassage manuel)
  - `AetherCoreSpawner` (Node dans Game.tscn) — spawn périodique toutes les 45 s
  - `GraftedColossus` override `Die()` — drop `AetherCore` via `CallDeferred`
  - `RunEndScreen` — animation countup séquentielle 4 composantes (Tween), boutons Hub/Rejouer
  - `HubScreen` — liste dynamique des 7 upgrades, sélecteur arme de départ conditionnel
  - `MainMenu` minimal — Jouer / Hub / Quitter
  - `HUD` — timer MM:SS compte à rebours + compteur "Noyaux : N"
  - `GameManager` — signal `EnemyKilled`, `StartingWeaponId`, application bonus meta au `RegisterPlayer`
  - `Player.HandleDeath()` — appelle `RunStatsTracker.EndRun("death")`

**Bugfixes Phase 2 (2026-06-20) :**
- [x] `RunEndScreen` : suppression `GetTree().Paused`, `_Ready()` force `Paused = false`, ordre
  de fermeture `ChangeSceneToFile` → `RemoveChild` → `QueueFree`
- [x] `CorruptedSentinel.Shoot()` : `AddChild` → `CallDeferred` (crash physics flush)
- [x] `DroneSwarm` : drones ajoutés comme enfants du nœud lui-même (pas `GetTree().Root`)
- [x] `Player._isDead` : flag évitant les doubles appels à `HandleDeath()` et le mouvement post-mort
- [x] `WeaponBase._Ready()` : `_timer = Cooldown` (plus de tir instantané au frame 0)
- [x] `LevelUpSystem.Reset()` : efface `_pendingFusionId` et `_lastFusionAvailableLevel` entre runs
- [x] `GameManager.RegisterPlayer()` : reset `XpSystem` + `InventorySystem` + `LevelUpSystem` + gestion arme de départ alternative
- [x] `InventorySystem.Reset()` : guard `IsInstanceValid` avant `QueueFree()` sur les nœuds d'armes
- [x] `project.godot` : `main_scene` → `MainMenu.tscn`
- [x] `Bash(*)` dans `.claude/settings.local.json` (permissions game-tester)

**Bugfixes post-test (2026-06-20 — suite au rapport `docs/TEST_REPORT.md`) :**
- [x] `base._Ready()` ajouté en dernière ligne du `_Ready()` de chaque sous-classe d'arme
  (`ImpulseCannon`, `PlasmaBlade`, `OverloadField`, `FusionBlade`, `RailOvercharged`, `DroneSwarm`).
  Godot C# n'appelle pas la méthode parente automatiquement → sans cet appel `_timer` valait `0f`
  et l'arme tirait immédiatement au frame 0 malgré le fix de `WeaponBase._Ready()`.
- [x] `InventorySystem.RefreshWeaponDamages()` implémenté (miroir de `RefreshWeaponCooldowns()`),
  appelé depuis `ApplyPassiveDelta()` après le bonus `thermal_core`. Corrige le cas où une arme
  au niveau max ne recevait jamais le bonus dégâts du Noyau Thermique.

**Phase 3 — Implémenté le 2026-06-20 :**

*Direction artistique (`directeur-artistique`) :*
- [x] `docs/STYLE_GUIDE.md` — palette 32 couleurs, specs animations par entité, ordre de production
- [x] GDD §12 finalisé — post-processing MVP acté (bloom obligatoire, flash désaturation fusion)
- [x] Décision : Colosse Greffé en 48×48 px (unique exception à la grille 32×32, justifiée lisibilité)

*Narratif (`story-teller`) :*
- [x] `data/texts.json` — noms/descriptions de toutes les armes, passifs, ennemis, upgrades meta, textes UI
- [x] `docs/NARRATIVE.md` — bible narrative condensée, backstory Arpenteur, glossaire 15 termes
- [x] Tagline retenue : *"Survive. Évolue. Ne reviens jamais le même."*

*Audio (`musicien`) :*
- [x] `src/Systems/AudioSystem.cs` — AutoLoad singleton, pool 8 SFX, 2 canaux musique fondu enchaîné
- [x] `project.godot` — AutoLoad `AudioSystem` ajouté (après `MetaProgressionSystem`)
- [x] Branchements dans 16 fichiers — armes, ennemis, XP, level-up, menus, fin de run
- [x] `docs/AUDIO_GUIDE.md` — 26 SFX + 7 musiques, sources CC0 (Kenney.nl), transitions run 5:00/10:00
- [x] 31 placeholders WAV silencieux (7 musiques + 24 SFX) — `tools/generate_audio_placeholders.py`
- [x] `AudioSystem.LoadMusic()` : tente `.ogg` puis `.wav` (fallback dev sans crash)

*Graphisme (`graphiste`) :*
- [x] 165 PNG générés via `tools/generate_sprites.py` (Python 3.13 + Pillow) — palette STYLE_GUIDE exacte
- [x] Joueur Cyborg : idle 4f / run_right 6f / run_down 6f / death 8f — 32×32 px
- [x] Essaim de Rouille : idle 3f / move 4f / death 5f
- [x] Drone Corrompu : idle 3f / move 4f / death 4f
- [x] Sentinelle Corrompue : idle 4f / move 6f / attack 4f / death 6f
- [x] Colosse Greffé : idle 4f / move 6f / attack 5f / death 10f — 48×48 px, implants violets `#AA44FF`
- [x] Pickups (orbe XP 3f, Noyau Aether 3f), projectiles, VFX, tiles sol/mur/décor, icônes UI
- [x] 5 SpriteFrames `.tres` (player, rustswarm, drone, sentinel, colossus)
- [x] `Player.tscn`, `RustSwarm.tscn`, `Bullet.tscn` : Polygon2D → AnimatedSprite2D
- [x] `Player.cs` + `RustSwarm.cs` : logique animations idle/run/death + flip_h
- [x] `docs/ASSET_STATUS.md`

*Animations ennemis restants + bloom (`developpeur`) :*
- [x] `CorruptedDrone.tscn/cs` : AnimatedSprite2D, idle/move selon déviation angulaire, flip_h
- [x] `CorruptedSentinel.tscn/cs` : AnimatedSprite2D, attack dans `Shoot()`, retour idle via `AnimationFinished`
- [x] `GraftedColossus.tscn/cs` : AnimatedSprite2D 48×48, `Die()` sans `base.Die()` (animation complète), spawn AetherCore sur `AnimationFinished`
- [x] `Game.tscn` : `WorldEnvironment` + bloom Aether (threshold 0.6, intensity 0.8, blend Additive)

**Décisions techniques Phase 3 :**
- Colosse : `Die()` inline sans `base.Die()` — `EnemyBase.Die()` appelle `QueueFree()` immédiatement, ce qui détruirait le sprite avant la fin des 10 frames. Spawn AetherCore déclenché par `AnimationFinished` à la frame 7 (flash violet).
- Sentinelle : `flip_h` sur direction vers joueur (pas vélocité) — peut reculer à droite en visant à gauche.
- Drone : `idle` si déviation angulaire > 30° (effet saccade visuelle), `move` sinon.
- Audio placeholders : WAV silencieux 44 100 Hz 16 bits mono, générés via `wave` stdlib Python (aucune dépendance externe). `LoadMusic()` tente `.ogg` puis `.wav`.
- Python sur cette machine : `C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe` (`python` n'est pas dans le PATH).

**Phase 3 — Reste à faire :**
- [x] Audio synthétique CC0 — `tools/generate_music_synth.py` (7 musiques + 24 SFX, stdlib Python, CC0) — livré 2026-06-21
- [x] Arène "Le Sanctuaire en Ruines" — `GroundRenderer.cs` + `AetherGeyser` — livré 2026-06-21
- [x] Menu principal + splash screen — `splash_art.png` + `MainMenu.tscn` stylisé — livré 2026-06-21
- [x] Assets audio définitifs — 31/31 CC0 définitifs (2026-06-22) : 24 SFX + 2 stingers Kenney.nl, 5 musiques Juhani Junkala (OpenGameArt.org). Conversion via ffmpeg 8.1.1. Détail : `assets/audio/CREDITS.md`.
- [x] Build Windows exportable (.exe) — livré Phase 4 (`build/ChimeraProtocol.exe`, export templates installés 2026-06-21)

**Phase 3b — Implémenté le 2026-06-21 :**

*Arène graphique (`graphiste` + `developpeur`) :*
- [x] `tools/generate_arena_extras.py` — 8 tiles : `tile_aether_geyser_01/02/03`, `tile_rust_pool_01/02`, `tile_tech_pillar`
- [x] `src/Systems/GroundRenderer.cs` — Node2D procédural, seed 42 : sol 40×23 (5 variantes), murs tuilés H×42/V×25, 4 débris_01 + 3 débris_metal + 3 flaques Rouille (ZIndex=-9) + 1 pilier tech + 2 colonnes collidables
- [x] `src/Entities/Environment/AetherGeyser.cs` + `scenes/entities/AetherGeyser.tscn` — zone dégâts 5 HP/s, cycle 3s inactif / 2s actif, Area2D rayon 24px
- [x] `assets/sprites/environment/geyser_frames.tres` — SpriteFrames idle 1f / active 2f à 4fps
- [x] `scenes/Game.tscn` — Polygon2D sol/murs/décors supprimés, GroundRenderer + 2 geysers aux positions (-300,-150) et (280,180)
- [x] BUG-003 corrigé : `vTiles = 25` (était 24 tronqué — couverture 768 px vs 784 px requis)

*Audio synthétique (`musicien`) :*
- [x] `tools/generate_music_synth.py` — 7 musiques (menu, run intro/mid/intense, hub, stingers) + 24 SFX synthétiques
- [x] `assets/audio/CREDITS.md` — documentation sources CC0, limitations, roadmap Phase 4+

*Splash screen + Menu (`developpeur`) :*
- [x] `tools/generate_splash.py` — `assets/sprites/ui/splash_art.png` (1280×720, texte intégré dans l'image)
- [x] `scenes/MainMenu.tscn` — `SplashArt` (TextureRect plein écran) + 3 boutons stylisés

*Bugfixes menu et mort (`developpeur`) :*
- [x] Titre/sous-titre doublés : suppression `TitleLabel`/`SubtitleLabel` dans `MainMenu.tscn` (texte déjà dans `splash_art.png`)
- [x] Jeu qui continue après la mort : `RunEndScreen.ProcessMode = Always`, `RunStatsTracker.PauseTree()` différé, `GetTree().Paused = false` avant chaque `ChangeSceneToFile`

**Décisions techniques Phase 3b :**
- **`GroundRenderer` — pas de TileMap Godot natif** : le TileMap de Godot 4 requiert l'éditeur graphique pour placer les tiles. Approche retenue : `Node2D` + `_Ready()` C# qui instancie des `Sprite2D` procéduralement (seed 42). Plus maintenable sans éditeur, reproductible.
- **`vTiles = 25` hardcodé** : `(784 / 32)` en division entière tronque à 24 (768 px). 25 tiles couvrent 800 px et chevauchent les coins H. Impact visuel nul car les tiles H masquent les gaps.
- **Flaques Rouille en ZIndex=-9** (non -8 comme les débris) : les flaques sont sur le sol, pas par-dessus. Placement recommandé sur `tile_floor_01` uniquement (contraste différentiel ~30 niveaux, risque lisibilité sur tiles chargés).
- **Audio synthétique — format WAV, pas OGG** : Kenney.nl distribue en OGG ; ffmpeg absent sur la machine → conversion impossible. `generate_music_synth.py` produit des WAV directement (synthèse additive, stdlib `wave`). CC0 / domaine public.
- **`splash_art.png` — texte intégré dans l'image** : le script Python bake le titre et la tagline dans le PNG (Pillow). Conséquence : ne pas ajouter de `Label` Godot avec le même texte dans `MainMenu.tscn` — doublon garanti. Si le texte doit être indépendant de l'image, régénérer le splash sans texte et rajouter des Labels.
- **Pause à la mort** : `RunStatsTracker.OpenEndScreen()` appelle `CallDeferred(PauseTree)` après `CallDeferred(AddChild, screen)`. L'ordre des CallDeferred garantit que `RunEndScreen._Ready()` s'exécute en premier (pose `ProcessMode = Always`), puis le tree est pausé. `Tween.TweenPauseMode.Process` dans `ShowEndScreen()` assure que l'animation countup Échos joue même en pause.

**Phase 3c — UI Polish (2026-06-21) :**

*Commits : `a191edd` HUD / `85552f1` LevelUpScreen / `1e906bd` RunEndScreen / `0b3337d` HubScreen / `2256f87` FusionFlash*

- [x] `MainMenu.tscn/cs` — vignette shader (smoothstep 0.35→0.75, opacité 0.65), 3 boutons
  StyleBoxFlat 3 états (cyan border=2 normal, violet border=3 hover/pressed), FadeOverlay z_index=100
  process_mode=Always, hover scale 1.04f/0.12s via Tween, fade-in entrée + fade-out transition 0.4s
- [x] `HUD.tscn/cs` — barres HP/XP en `ColorRect` imbriqués (max 200px, resize via `Size.X`),
  HP couleur dynamique 3 états (>50% cyan / 25-50% orange / <25% rouge), timer couleur dynamique
  (>120s blanc cassé / 60-120s orange / <60s rouge), `CoresLabel` ⬡ violet ancré top-right
- [x] `LevelUpScreen.tscn/cs` — divider 400×2px cyan 50%, HintLabel "Clic pour choisir" 14px,
  hover scale 1.03f/0.1s sur les 3 cartes, `PivotOffset` recalculé dans MouseEntered (layout résolu)
- [x] `RunEndScreen.tscn/cs` — fond `Color(0.05,0.04,0.1,0.92)`, frame décoratif `Color(0.267,1,0.933,0.12)`
  700×520 centré, 2 boutons StyleBoxFlat 3 états, FadeOverlay (`Color(0,0,0,1)` forcé en début de
  `ShowEndScreen()` puis fade vers 0 en 0.5s), chemins GetNode sans préfixe "Background/"
- [x] `HubScreen.tscn/cs` — vignette identique à MainMenu (opacité 0.55), lignes upgrade
  encapsulées dans `PanelContainer` stylé (`Color(0.08,0.08,0.18,0.7)`, border cyan 20% opacité),
  FadeOverlay, fade-in 0.6s + transition 0.3s, boutons "Retour"/"Jouer" StyleBoxFlat 3 états
- [x] `FusionFlash.tscn/cs` + AutoLoad — flash blanc aveuglant GDD §12 (voir §12 ci-dessus)

**Patterns UI établis (appliquer à toutes les futures UI) :**
- Palette : fond `Color(0.102,0.102,0.18)`, cyan `Color(0.267,1,0.933)`, violet `Color(0.667,0.267,1)`,
  or `Color(1,0.8,0.267)`, blanc cassé `Color(0.85,0.85,0.95)`
- `StyleBoxFlat` 3 états : `bg=Color(0.05,0.05,0.12,0.85)` + `border=2` + `borderColor=cyan` (normal) ;
  `bg=Color(0.1,0.1,0.25,0.95)` + `border=3` + `borderColor=violet` (hover/pressed)
- `FadeOverlay` : `ColorRect` z_index=100, process_mode=3 (Always), mouse_filter=2 (Ignore) — obligatoire
  sur chaque écran pour les transitions entre scènes
- Hover scale : `btn.PivotOffset = btn.Size / 2f` dans MouseEntered (pas dans `_Ready()` où Size = 0),
  scale 1.04f en 0.12s `EaseOut Quad`, retour 1.0f en 0.1s `EaseOut Quad`

### Phase 4 — P0 + P1 (2026-06-21)

**P0 — Arène (commits 21f9a60 + bugfix) :**
- [x] `Constants.cs` : `ArenaWidth = 1920`, `ArenaHeight = 1216`
- [x] `GroundRenderer.cs` : `GridCols = 60`, `GridRows = 38`, murs H=63/V=41, distribution tiles §10, décors 2/2/2, `BuildObstacles()` (5 piliers A + 2 épaves B), fallback Polygon2D si sprite absent
- [x] `AetherGeyser.tscn/.cs` : `PointLight2D "Light"`, energy 0.4↔1.8 via `CreateTween()` dans `SetActive(bool)`
- [x] `EnemyBase.Die()` : `SpawnDeathBurst()` via `CallDeferred`, `_deathBurstScene` cache statique. `vfx_enemy_death_burst.tscn` (amount=8, lifetime=0.4, spread=180°, velocity 60–100, draw_pass_1=BurstMesh 6×6) + `EnemyDeathBurst.cs` (`Godot.Timer` qualifié)
- [x] `Player.tscn` `Camera2D` : `limit_left=-960`, `limit_right=960`, `limit_top=-608`, `limit_bottom=608`
- [x] `Game.tscn` : murs recalculés (WallTop/Bottom 1984×32 à Y=±624, WallLeft/Right 32×1280 à X=±976), geysers (-500,-250) et (480,300)
- [x] Sprites P0 : `tile_pillar_stone.png` (32×64, fissure `#00A0BB`), `tile_pillar_stone_shadow.png` (32×8), `tile_wreck_machine.png` (64×32)

**P1 — VFX (commit b3b4db5 + fix 523c9af) :**
- [x] `GraftedColossus.Die()` : death burst inline, `_deathBurstScene ??=` cache statique, `_cachedDeathPos` capturé avant `QueueFree`
- [x] `XpOrb.tscn/.cs` : `GPUParticles2D "Trail"` (amount=4, lifetime=0.15, actif sur `_isMagneted`) + `AnimationPlayer "Anim"` pulse `modulate.a` 0.7→1.0 0.6s EaseInOut boucle
- [x] `vfx_impact_burst.tscn` + `ImpactBurst.cs` : burst 6 particules 0.25s, instancié depuis `Bullet.cs` (plasma `#FFD700`) et `EnemyBullet.cs` (sentinel `#FF6644`), caches statiques `??=`
- [x] `Game.tscn` : 4 `GPUParticles2D` ambiants Aether bords (amount=8 chacun, ZIndex=-1, lifetime=4)
- [x] Sprites P1 : `tile_crate_tech.png` (32×40), `tile_arch_fallen.png` (96×32), `vfx_particle_aether_ambient.png` (3×3), `vfx_particle_impact_plasma.png` (2×2), `vfx_particle_impact_sentinel.png` (2×2)
- [x] BUG-006 : `draw_pass_1` assigné dans `vfx_enemy_death_burst.tscn` et `vfx_impact_burst.tscn`

**Décisions techniques Phase 4 :**
- `SpawnDeathBurst()` : `CallDeferred(AddChild)` puis `SetDeferred("global_position", ...)` — l'ordre est impératif (même pattern que `SpawnXpOrb()`). Assigner `GlobalPosition` directement avant `AddChild` place le burst à (0,0).
- `GraftedColossus` : ne peut pas appeler `base.Die()` (cf. Phase 3) → `_deathBurstScene` dupliqué en champ statique propre à la classe (`_deathBurstSceneColossus` renommé `_deathBurstScene` dans le fichier).
- `PointLight2D` sans texture dans le `.tscn` : Godot utilise un cercle par défaut jusqu'à ce que `AetherGeyser._Ready()` assigne la `GradientTexture2D` radiale via code. Inoffensif — l'assignation se fait dans la même frame que le premier `_Ready()`.
- `draw_pass_1` sur `GPUParticles2D` : la sub_resource `QuadMesh` doit être explicitement assignée au champ `draw_pass_1` du nœud pour que la taille des particules soit maîtrisée. Sans cet assignat, Godot utilise un quad interne de taille par défaut.
- Obstacles types C/D : sprites `tile_crate_tech.png` et `tile_arch_fallen.png` produits (Phase 4 P1) mais `BuildObstacles()` ne les instancie pas encore — intégration reportée en P2 pour ne pas retarder le test joueur.

**Phase 4 P2 — Fixes + finitions (commit 63fc20d + 30ef21d, 2026-06-21) :**
- [x] `GroundRenderer.BuildObstacles()` : types C (4 caisses, `RectangleShape2D(28,28)` offset Y=+6, anti-alignement) et D (2 arches, 2 CollisionShape2D latéraux uniquement, rotation 50% aléatoire)
- [x] BUG-001 : `SpawnDeathBurst()` `private` → `protected` dans `EnemyBase`. Appelé dans `Die()` de `RustSwarm`, `CorruptedDrone`, `CorruptedSentinel` — les 3 types manquaient de feedback visuel de mort
- [x] BUG-002 : 4 `ParticleProcessMaterial` distincts dans `Game.tscn` (Top/Bottom/Left/Right), directions orientées vers l'intérieur de l'arène
- [x] BUG-003 : sub_resource `LightTexture` orpheline supprimée de `AetherGeyser.tscn` (`load_steps` 6→4)

**Phase 4 — Build Windows (2026-06-21) :**
- [x] `ChimeraProtocol.sln` créé et commité (`dotnet new sln --format sln` + `dotnet sln add`)
- [x] Export `build/ChimeraProtocol.exe` (114 MB, PCK embarqué) + `data_ChimeraProtocol_windows_x86_64/` — testé fonctionnel (~400 MB RAM)
- **Décision** : le `.sln` est versionné dans git. Sans lui Godot exporte sans assemblée C# → crash immédiat.

**Phase 4 — Reste à faire :**
- [x] Validation visuelle interactive — réalisée via PyAutoGUI + analyse screenshots game-tester (2026-06-22) : transitions sans crash, HUD fonctionnel, ennemis spawnent, arène correcte
- [ ] Obstacle E : terminal corrompu 32×48 + sprites `tile_terminal_corrupt_01/02.png` (post-MVP)
- [ ] Chromatic aberration / vignette (shaders post-MVP, nécessite tests joueurs)

### Phase 5 — Navigation clavier/manette (2026-06-22)

**Commits : `f3b287a` (navigation) + `7916b0f` (StyleBox focus) + `603a162` (BUG-101/BUG-102 fixes)**

- [x] `MainMenu.cs` : `GrabFocus()` sur PlayButton en `_Ready()`, `FocusEntered`/`FocusExited` → scale sans SFX, `_UnhandledInput` ui_cancel → Quit
- [x] `HubScreen.cs` : `GrabFocus()` sur BackButton en TweenCallback post-fade, `SetupFocusChain()` (FocusNeighborTop/Bottom explicites sur tous les BuyButton via `GetPathTo`), ui_cancel → OnBackPressed
- [x] `LevelUpScreen.cs` : `GrabFocus()` sur Card0 via `.Chain()` après tween `SetParallel(true)`, `FocusEntered`/`FocusExited` sur les 3 cartes, pas de ui_cancel
- [x] `RunEndScreen.cs` : `GrabFocus()` sur ReplayButton en TweenCallback final, `FocusNeighborLeft`/`Right` Hub↔Replay, ui_cancel → OnHubPressed, `fadeTween.SetPauseMode(Process)`
- [x] `AddThemeStyleboxOverride("focus", ...)` — StyleBox violet `#AA44FF` border=3px dans `ConnectHoverEffects`/`ConnectCardHover` de tous les écrans
- [x] `tools/test_ui_keyboard.py` — test automatisé PyAutoGUI, 35 screenshots, **9 PASS / 0 FAIL** (2026-06-22)
- [x] Validation visuelle screenshots : bordure violette distincte sur bouton focusé, LevelUpScreen et RunEndScreen détectés sans faux positif
- [x] **BUG-101** : `RunEndScreen.tscn` avait `theme_override_styles/focus = SubResource("StyleNormal_Hub")` sur HubButton et ReplayButton → écrasait le `AddThemeStyleboxOverride` violet posé dans `_Ready()`. Supprimé.
- [x] **BUG-102** : `_label_color_at_top()` : step=4 trop grossier (pixels anti-aliasés g≈133 < seuil g>180), 2 colonnes hors du label. Fix : step=2, 9 colonnes x=[0.35..0.65], seuil `g>120`.

**Décisions techniques Phase 5 :**
- `FocusEntered` → scale uniquement, `MouseEntered` → scale + SFX (deux handlers distincts — évite le son parasite au GrabFocus programmatique)
- `AddThemeStyleboxOverride("focus")` posé à la construction (pas dans FocusEntered) — Godot gère l'état automatiquement
- `SetupFocusChain()` obligatoire quand des boutons sont dans des `PanelContainer` : l'algo de focus spatial Godot ne traverse pas les containers non-Control de façon fiable
- `FocusNeighborLeft/Right` obligatoire quand les boutons sont enfants directs d'un `CanvasLayer` sans container commun
- `GrabFocus()` toujours dans un `TweenCallback` post-animation (jamais dans `_Ready()` direct) — évite le focus pendant les transitions opaques
- **Piège `.tscn` vs `_Ready()`** : `theme_override_styles/focus` dans le `.tscn` prend la priorité sur `AddThemeStyleboxOverride("focus")` dans `_Ready()` — ne jamais poser `focus` dans le `.tscn` si le script le surcharge
- **Pixels anti-aliasés dans la détection PyAutoGUI** : les glyphes de font ont des pixels de bord à g≈50% de la valeur centrale ; step=4 les rate systématiquement — utiliser step=2 et abaisser les seuils

### Playtest équilibrage MVP (2026-06-23)

**Verdict : MVP prêt à distribuer** — 0 crash sur 3 runs (~25 min de jeu), flux d'écrans correct, calcul Échos validé.

**BUG-202 corrigé :** `maxEnemies` initial réduit de 20 à 8 (commit `7b76272`). Joueur mourait < 60s avant le 1er level-up. La valeur 20 est désormais atteinte à t=1 min.

**BUG-201 clos (fausse alerte) :** "Colosse à t=90s" = mauvaise identification visuelle dans les screenshots. `EnemySpawner` est un nœud de `Game.tscn` (non AutoLoad) → `_elapsed` reset garanti à chaque run. `spawnStartMinute=9` respecté.

**Observations restantes (non bloquantes) :**
- Navigation RunEndScreen → Hub légèrement fragile si bouton activé avant fin du countup (BUG-204)
- Audio non vérifié par le test automatisé (PyAutoGUI ne capture pas le son)

### Polish visuel — lisibilité arène + effets lumière + VFX armes (2026-06-23)

**Commit : `9334f9e`**

Trois axes d'amélioration répondant au retour "arène trop chargée, sprites illisibles" :

#### 1. Clarté arène (GroundRenderer)

- **Tiles sol** : `Modulate = Color(0.42, 0.42, 0.48)` — assombris à 42%, légèrement teintés bleu (froid, neutre)
- **Tiles murs** : `Modulate = Color(0.55, 0.55, 0.62)` — assombris à 55%, teinte cohérente avec le sol
- **Dark overlay** : `Polygon2D` couvrant l'arène entière, couleur `Color(0, 0.01, 0.06, 0.38)` (bleu-noir semi-transparent), ZIndex=-7 (au-dessus des décors, en-dessous des entités)
- **Résultat** : fond clairement séparé des sprites joueur/ennemis sans masquer les obstacles

#### 2. Effets de lumière dynamiques (Player, Bullet, EnemyBullet)

- **Joueur** : `PointLight2D` cyan Aether (`#44FFEE`, énergie 0.55, TextureScale 4.0 → rayon ~256px). Suit le joueur et illumine la zone de combat.
- **Balles joueur** : `PointLight2D` plasma bleu (`Color(0.5, 0.85, 1)`, énergie 1.2, TextureScale 1.8). Chaque projectile laisse un halo lumineux.
- **Balles ennemies** : `PointLight2D` rouge-orange (`Color(1, 0.35, 0.1)`, énergie 1.0, TextureScale 1.6). Distingue visuellement les projectiles hostiles.
- **Texture lumière** : `GradientTexture2D` radiale blanc→transparent, factory `Player.MakeRadialLightTexture(int size)`, textures cachées statiquement (une seule allocation par classe).
- **BlendMode = Add** sur tous les PointLight2D (additive — cumul avec le glow WorldEnvironment existant).

#### 3. Notifications d'armes équipées (HUD, InventorySystem)

- **HUD.WeaponNotifLabel** : `Label` centré en bas d'écran (anchor bottom), font_size 22, fondu alpha 0→1→0 sur chaque notification.
- **3 types de notification distincts** :
  - Nouvelle arme (`ShowWeaponEquipped`) : couleur or `#FFCC44` + flash joueur or-chaud `Color(2.8, 1.8, 0.3)`
  - Upgrade arme (`ShowWeaponUpgraded`) : couleur cyan + flash joueur cyan-teal `Color(0.3, 2.5, 2.2)`
  - Passif acquis (`ShowPassiveAcquired`) : couleur violet `#AA44FF` + flash joueur violet `Color(1.5, 0.5, 2.8)`
- **Noms localisés** : `GetWeaponDisplayName()` et `GetPassiveDisplayName()` en français (Canon Impulseur, Lame Plasma, etc.)
- **`HUD.Instance`** singleton, nettoyé dans `_ExitTree()`.
- **Flash distinct de l'existant** : damage flash = blanc sur-exposé (`Color(5,5,5)`) / hit flash = pas de flash / fusion flash = `FusionFlash` blanc plein écran → les 3 nouveaux flashs utilisent des teintes colorées saturées (énergie 2.5–2.8) pour ne pas se confondre.

### HUD — refonte "juicy" sci-fi (2026-06-25)

**Commit : `52e9d6a`**

Refonte complète de `scenes/ui/HUD.tscn` et `src/UI/HUD.cs` pour un rendu plus immersif et cohérent avec la direction artistique (palette cyan/violet Aether, dark theme).

**Layout :**
- **Panel stats** (gauche) : `Color(0.04, 0.04, 0.10, 0.90)` — bordure cyan 5px sur la gauche (AccentBar)
- **HP row** : icône ♥ rouge + label `current / max` + barre 18px — couleur dynamique (cyan >50% / orange 25–50% / rouge <25%)
- **Séparateur** : ligne cyan à 18% alpha entre HP et XP
- **XP row** : label `LV N` cyan + barre 12px + sous-label discret `cur / max XP`
- **Timer** : panel centré en haut encadré de deux barres cyan latérales
- **Noyaux** : panel top-right avec accent violet gauche, icône ⬡ violette

**Animations :**
- **Drain HP smooth** : gain instantané, perte en lerp (`MoveToward` 2.5 ratio/s ≈ drain total ~0.4s). Donne du "weight" aux dégâts.
- **Glow derrière HP bar** : `ColorRect` légèrement plus large (+8px) à 22% alpha, couleur synchronisée — profite du bloom WorldEnvironment.
- **Pulsation basse vie** (<25%) : barre HP pulse alpha 0.45→1.0→0.45 toutes les 0.64s via tween `SetLoops()`.
- **Flash XP au level-up** : barre surexposée `Color(3,3,3,1)` → retour blanc en 0.5s (overexposure visible grâce au bloom).
- **Glow XP ambiant** : `ColorRect` plein-largeur à 10% alpha toujours visible (ambiance sci-fi permanente).

**Chemins de nœuds mis à jour (vs Phase 3c) :**
`HpHeader/HpLabel` / `HpBarBg/HpBar` / `HpBarBg/HpBarGlow` / `XpRow/LevelLabel` / `XpRow/XpBarBg` / `XpRow/XpBarBg/XpBar` / `XpRow/XpBarBg/XpBarGlow` / `XpSubLabel` / `TimerLabel` / `CoresContainer/CoresLabel` / `WeaponNotifLabel`

---

### HUD — intégration assets concept cyberpunk (2026-06-25)

Intégration d'éléments visuels depuis l'image concept `idea/idee_hud_chimera_core.png` (3060×1408 px, style terminal hacker cyberpunk).

**Éléments ajoutés :**

- **Titres de panneau** : `StatsPanelTitle` "CHIMERA PROTOCOL" (9px cyan 55% alpha) + séparateur fin — convention : chaque panneau a un titre fonctionnel en petites majuscules. `CoresPanelTitle` "NOYAUX AETHER" (violet 55% alpha).
- **Barre XP segmentée 20 blocs** : `BuildXpSegments()` génère 20 `ColorRect` dynamiquement via `CallDeferred`. Espacement 1px entre blocs. Flash level-up sur tous les segments simultanément (`Color(3,3,3,1)` → tween 0.5s). `_segmentsReady` guard. `_xpBar` conservé à taille 0 pour compatibilité `OnLevelUp()`.
- **Sous-label XP reformaté** : `"LV {n}  |  {xp} / {xpToNext} XP"` — lecture secondaire complète d'un coup d'œil.
- **Sous-label timer** : `TimerSubLabel` "RUNTIME ENCRYPTED" (9px, blanc cassé 45% alpha) sous le grand timer.
- **Coins L-bracket** : 8 `ColorRect` 1–2px aux 4 angles de chaque panneau (stats cyan / noyaux violet / timer cyan). Bordures fines 1px supplémentaires.
- **Indicateur de niveau hexagonal** (`LvHexBg`) : `TextureRect` 44×26 px flat-top, `ui_lv_hex.png` généré procéduralement (contour double 2px cyan, fond transparent). `LevelLabel` centré en enfant. `size_flags_vertical = 4` (ShrinkCenter) pour alignement vertical avec la barre XP.
- **Icône Chimera Core** (`CoreIconTex`) : `TextureRect` 30×30 dans `CoresContainer`, `ui_chimera_core_icon.png` extrait du concept par masquage HSV (teinte violet 250–295° + cyan 155–205°, fond transparent, couleurs ×1.8). Remplace l'emoji ⬡ dans l'affichage numérique.
- **Cadre panneau stats** (`StatsPanelFrame`) : `TextureRect` z_index=2, `ui_panel_frame_nobg.png` généré from scratch (304×126 px, 2% opaque — coins L-bracket épais, tirets de graduation sur les bords H/V, losanges de mi-bord haut/bas, fond 100% transparent).
- **Cadre timer** (`TimerFrame`) : `TextureRect` z_index=3, `ui_timer_frame_nobg.png` extrait par masquage HSV (4% opaque — crochets latéraux uniquement, chiffres baked éliminés). `TimerLabel` z_index=4 pour rester au-dessus.

**Scripts de génération/retouche (reproductibles) :**
- `tools/generate_hud_assets.py` : génère `ui_lv_hex.png` (44×26 flat-top) et `ui_panel_frame_nobg.png` (304×126, tech ornaments)
- `tools/extract_hud_assets.py` : découpe 9 éléments depuis l'image concept
- `tools/retouch_hud_assets.py` : masquage HSV numpy — conserve uniquement les pixels cyan/violet (sat≥0.28, val≥0.30), dilation 1px anti-aliasing

**Leçon technique** : le masquage HSV ne peut pas séparer "cadre" de "contenu" quand l'image source dessine tout dans les mêmes teintes — recréation from scratch obligatoire pour les éléments monochrome. Applicable aux sprites extraits avec couleurs distinctes (icône violet, crochets timer cyan).

**Chemins de nœuds supplémentaires :**
`XpRow/LvHexBg` / `XpRow/LvHexBg/LevelLabel` / `CoresContainer/CoreIconTex`

### Rééquilibrage Échos & Hub post-overtime (2026-07-02)

**Problème signalé par le joueur** : le Hub se vide en une seule run depuis le passage au modèle
« survie sans fin » (2026-06-30, `docs/LEVEL_PROGRESSION_PLAN.md`) — l'ancienne formule d'Échos
linéaire sans plafond récompensait une run overtime exceptionnelle de plusieurs milliers d'Échos
(vs ~240 pour l'ancien plafond de fait à 15 min), soit quasiment le coût total de l'ancien arbre
(6 960 Échos) en une seule run.

**Décisions actées par `game-designer` :**
- [x] **Formule d'Échos v2** (`data/meta_upgrades.json` → `echoesFormula`) : partie standard
  plafonnée (`capTimeSecs=780`, `capKills=520`, `capCores=22`, identique en valeur à l'ancien
  plafond) + bonus de surcharge overtime fortement amorti (`overtimeDampening=0.15`) et lui-même
  plafonné (`overtimeBonusCap=100`). Détail complet et calibration : §9.2.
- [x] **Rééquilibrage des 8 améliorations existantes** : prix +8% à +29% selon leur puissance
  (total 6 960 → 8 090 Échos), effets inchangés. Détail : §9.5, §18.2.
- [x] **10 nouvelles améliorations** (total 11 340 Échos) : 4 extensions tier-2 des stats
  existantes (`hp_boost_2`, `damage_boost_2`, `cooldown_reduction_2`, `damage_reduction_2`,
  réutilisant les champs `PlayerStats` déjà câblés) + 6 mécaniques variées (`extra_life` — revive
  à 30% HP, `damage_absorb` — coups absorbés à 0 dégât, `hp_regen` — régénération passive,
  `core_magnetism` — rayon de collecte Noyaux, `overtime_stabilizer` — réponse directe au point de
  douleur overtime (dampening de la pente d'escalade, max -15%), `bonus_magnet` — Aimants
  supplémentaires en overtime). Détail : §9.5, §18.2.
- [x] **Écran de fin de run** : nouvelle 5e composante "Bonus de Surcharge" (n'anime que si > 0)
  rendant le plafond overtime transparent pour le joueur plutôt que de le cacher dans un calcul
  opaque. Détail : §9.4.
- [ ] **Câblage développeur** (non fait par `game-designer`, cf. notes `_implementationNotes` dans
  `data/meta_upgrades.json`) : nouvelle signature `EchoFormula.Calculate()` (5 nouveaux
  paramètres), nouveau champ `PlayerStats.HpRegenPerSecond`, lectures directes de
  `MetaProgressionSystem.GetUpgradeLevel()` dans `Player.cs`/`MagnetSpawner.cs`/`AetherCore.cs`/
  `EnemySpawner.cs` pour les statTargets consommables/non-PlayerStats, 5e composante sur
  `RunEndScreen`, `ScrollContainer` sur `HubScreen.tscn` (18 items débordent du `VBoxContainer`
  simple actuel).
- [ ] Nouvelles clés de localisation EN/FR/ES pour les 10 nouveaux items (`UPGRADE_<ID>_NAME`/
  `_DESC`) — texte source FR fourni par `game-designer`, traduction/intégration CSV à charge de
  `developpeur`.

Total de l'arbre complet post-rééquilibrage : **19 430 Échos** (18 items) contre 6 960 avant
(8 items) — cf. §18.2 pour le détail runs/durée estimée.

## 17. Tuning Phase 2 — Valeurs de référence

> Ajouté le 2026-06-20 par l'agent `game-designer`. Les fichiers `data/enemies.json`,
> `data/weapons.json` et `data/levelup_config.json` contiennent les valeurs runtime.

### Courbe XP — rééquilibrage 2026-06-24

Formule : `xpToNext(L) = 20 × L + 7` (modifiée le 2026-06-24 — était `15n² + 25n` puis `20n² + 40n`)
Cumul : `xpCumulative(L) = (L-1) × (10L + 7)`

Objectif : ~2,3× plus de niveaux par minute qu'avant. Cible 15-20 niveaux sur 15 min.

| Niveau | XP pour ce niveau | XP cumulatif | Temps estimé (mix ennemis) |
|---|---|---|---|
| 2 | 27 | 27 | ~0:30 |
| 5 | 107 | 228 | ~2:00 |
| 8 | 167 | 609 | ~3:30 |
| 10 | 207 | 963 | ~5:00 |
| 15 | 307 | 2 198 | ~9:00 |
| 20 | 407 | 3 933 | ~13:30 |
| 25 | 507 | 6 168 | run exceptionnelle |

**XP ennemis (inchangés depuis 2026-06-23)** — différenciation claire des tiers de danger.

| Ennemi | XP | Ratio vs Essaim |
|---|---|---|
| Essaim de Rouille | 3 | ×1 |
| Drone Corrompu | 7 | ×2,3 |
| Sentinelle Corrompue | 20 | ×6,7 |
| Colosse Greffé | 60 | ×20 |

XP/min estimé par phase (mix ennemis) : ~120 (0-2 min) → ~250 (2-5 min) → ~500 (5-9 min) → ~700 (9-15 min).
Fun zone niveaux 5-8 atteinte vers ~2-3 min. Niveau 15 vers 9 min. **À valider en playtest.**

### EnemySpawner — formules de scaling

```
maxEnemies    = min(200, 8 + (int)(t_minutes * 12))   // corrigé 2026-06-23 (était 20, trop brutal)
spawnInterval = max(0.7f, 2.0f - t_minutes * 0.087f)
```

| Paramètre | t=0 | t=1 min | t=5 min | t=10 min | t=15 min |
|---|---|---|---|---|---|
| Max ennemis actifs | 8 | 20 | 68 | 128 | 188 |
| Intervalle spawn | 2,0 s | 1,9 s | 1,4 s | 1,0 s | 0,7 s |

**Décision 2026-06-23 (playtest BUG-202)** : départ à 8 ennemis au lieu de 20. Le joueur sans upgrade mourait systématiquement en < 60s avant d'atteindre son premier level-up. Avec 8 Essaims à t=0 (vs 20), la première minute est accessible. La courbe rejoint l'ancienne à t=1 min (20 ennemis), puis dépasse légèrement (68 à t=5 min vs 80 anciennement) — acceptable.

Mix ennemis à t=15 min (poids relatifs actifs) : Essaim 43% / Drone 30% / Sentinelle 17% / Colosse 9%.

### Cohérence DPS joueur vs HP ennemis (t=0, sans passifs)

| Arme | DPS niv. 1 | TTK Essaim (20 HP) | TTK Colosse t=9 (200 HP) |
|---|---|---|---|
| Canon à Impulsions | 12,5 | 1,6 s | 16 s |
| Lame Plasma | ~15 | 1,2 s | 13 s |
| Essaim de Drones (2) | ~24 | 0,8 s | 8 s |
| Champ de Surcharge | ~3,2 | 6 s | 62 s (soutien) |

### Scaling ennemis — valeurs clés

| Ennemi | HP t=0 | HP t=5 | HP t=10 | HP t=15 |
|---|---|---|---|---|
| Essaim de Rouille | 20 | 28 | 36 | 44 |
| Drone Corrompu | 15 | 20 | 25,5 | 31 |
| Sentinelle Corrompue | 45 | 67 | 90 | 112 |
| Colosse Greffé | 200 | — | 320 | 560 |

### Plafonds techniques

- Max ennemis simultanés (pic) : 200 (objectif : 60 fps constant)
- Vitesse joueur max : 380 px/s (soft cap Servo-Moteurs)
- Réduction dégâts reçus max : 40% (hardcap Plaque Renforcée)
- Cooldown minimum par arme : 0,15 s (hardcap Capaciteur)

## 18. Tuning Meta — Référence complète

> Ajouté le 2026-06-20 par l'agent `game-designer`. **Rééquilibré le 2026-07-02** (cf. §9 pour le
> rationnel complet du passage à la formule d'Échos v2 et aux 18 améliorations). Fichier runtime :
> `data/meta_upgrades.json`.

### 18.1 Flux des Échos en chiffres (v2)

```
Échos_standard = floor(min(T,780)/20) + floor(min(K,520)/10) + (min(N,22) × 5) + 10
Bonus_surcharge = min( floor(max(0,T-780)/20×0,15) + floor(max(0,K-520)/10×0,15)
                       + floor(max(0,N-22)×5×0,15) , 100 )
Échos = Échos_standard + Bonus_surcharge
  T = secondes survécues · K = ennemis tués · N = Noyaux d'Aether ramassés
```

Vitesse d'accumulation (phase standard, 0-13 min, identique à la v1) :
- ~6 Échos/min en début de run (peu d'ennemis, pas de Noyaux encore).
- ~12-15 Échos/min en milieu de run (essaims denses, Colosses lâchant des Noyaux).
- ~16-18 Échos/min en fin de phase standard (spawn maximal, Noyaux périodiques).

Au-delà de 13 min (overtime) : rendement qui s'effondre à ~15% du taux standard, puis à **zéro**
une fois le bonus de surcharge plafonné à +100 Échos (atteint après environ 5-10 minutes
d'overtime selon le skill du joueur, cf. calibration §9.2).

Déblocage de l'arbre (18 items, 19 430 Échos au total) :
- Après run 1 (première mort précoce) : ~11-40 Échos → peut acheter `hp_boost` niv. 1 (90 Échos)
  en 2-3 runs.
- Après 5 runs courtes (~350-450 Échos cumulés) : `hp_boost` max + `speed_boost` niv. 1.
- Après 15-20 runs variées (~2 500-3 500 Échos) : les 8 items historiques débloqués (8 090 Échos).
- Après plusieurs dizaines de runs supplémentaires, incluant des runs overtime réussies
  (~11 340 Échos de plus) : les 10 nouveaux items débloqués → arbre complet.

### 18.2 Courbe de coût de l'arbre complet (v2, 18 items)

| Amélioration | Coût total (tous niveaux) |
|---|---|
| Corps Renforcé (×4) | 90+180+300+480 = **1 050 Échos** |
| Calibration Offensive (×5) | 130+230+380+580+850 = **2 170 Échos** |
| Servos Améliorés (×3) | 100+220+380 = **700 Échos** |
| Synchronisation Aether (×3) | 140+290+470 = **900 Échos** |
| Blindage Composite (×3) | 170+340+560 = **1 070 Échos** |
| Prototype de Terrain (×1) | **350 Échos** |
| Recalibrage Tactique (×3, consommable) | 160+320+520 = **1 000 Échos** |
| Esquive de Sélection (×3, consommable) | 130+270+450 = **850 Échos** |
| *Sous-total 8 items historiques* | **8 090 Échos** |
| Plaque Blindée (×2) | 300+450 = **750 Échos** |
| Calibration Avancée (×3) | 350+500+700 = **1 550 Échos** |
| Synchronisation Aether II (×2) | 320+480 = **800 Échos** |
| Blindage Composite II (×2) | 350+550 = **900 Échos** |
| Noyau de Secours (×2) | 450+800 = **1 250 Échos** |
| Plaque Adaptative (×3) | 200+350+550 = **1 100 Échos** |
| Auto-Réparation (×3) | 220+380+600 = **1 200 Échos** |
| Résonance de Noyau (×3) | 160+280+450 = **890 Échos** |
| Stabilisateur de Surcharge (×3) | 450+750+1150 = **2 350 Échos** |
| Aimant Auxiliaire (×2) | 200+350 = **550 Échos** |
| *Sous-total 10 nouveaux items* | **11 340 Échos** |
| **TOTAL ARBRE COMPLET (18 items)** | **19 430 Échos** |

Durée estimée pour compléter l'arbre : plusieurs dizaines de runs, la majorité des Échos venant de
runs standards répétées (211 Échos max sans overtime) complétées ponctuellement par des runs
overtime réussies (+jusqu'à 100 Échos de bonus). Volontairement plus long que l'ancien arbre
(5 420-6 960 Échos, 25-35 runs) car une progression méta plus longue est désormais nécessaire pour
éviter qu'une poignée de runs exceptionnelles ne vident le Hub (cf. §9 intro — c'est le problème
que ce rééquilibrage corrige).

### 18.3 Noyaux d'Aether — spawn détaillé

- **Spawn périodique** : 1 Noyau toutes les 45 s → ~17 Noyaux possibles sur les 780 s de la phase
  standard, et indéfiniment au-delà en overtime (extensible par `bonus_magnet` côté Aimant, et par
  `core_magnetism` côté facilité de ramassage — cf. §9.5).
- **Drop Colosse** : les Colosses spawnent dès 9:00. Sur les ~4 dernières minutes de la phase
  standard (9:00-13:00) à ~1 Colosse/3 min → ~1-2 drops supplémentaires avant le boss.
  En overtime, les Colosses (et l'escalade de mini-boss/boss) continuent d'en fournir.
- **Noyaux accessibles en phase standard (0-13 min)** : ~18-20 réalistes ramassés — cohérent avec
  le `capCores=22` de la formule §9.2 (qui plafonne leur contribution de toute façon).
- **Gain Échos via Noyaux en phase standard** (22 ramassés, au plafond) : 22 × 5 = **110 Échos**.
  Au-delà (overtime), chaque Noyau supplémentaire ne rapporte plus que 5×0,15=0,75 Échos avant
  d'atteindre le plafond global du bonus de surcharge (+100, partagé avec temps et kills).

### 18.4 Intégration SaveManager — structure de sauvegarde attendue

```json
{
  "meta": {
    "currentEchoes": 0,
    "totalEchoesEarned": 0,
    "totalEchoesSpent": 0,
    "upgrades": {
      "hp_boost": 0,
      "damage_boost": 0,
      "speed_boost": 0,
      "cooldown_reduction": 0,
      "damage_reduction": 0,
      "reroll": 0,
      "skip": 0,
      "hp_boost_2": 0,
      "damage_boost_2": 0,
      "cooldown_reduction_2": 0,
      "damage_reduction_2": 0,
      "extra_life": 0,
      "damage_absorb": 0,
      "hp_regen": 0,
      "core_magnetism": 0,
      "overtime_stabilizer": 0,
      "bonus_magnet": 0
    }
  }
}
```

Chemin de sauvegarde : `user://save.json` (cf. §15 — `FileAccess` Godot). Compatibilité ascendante
garantie : les 10 nouveaux ids sont simplement absents des saves existantes (`GetUpgradeLevel`
retourne 0 par défaut) — **aucune migration nécessaire**.

`MetaProgressionSystem` (AutoLoad) est le seul composant qui lit et écrit cette structure. Il
expose :
- `int CurrentEchoes` (propriété, lecture seule depuis l'extérieur)
- `void AddEchoes(int amount)` — appelé par `RunStatsTracker` à la fin de run
- `bool TryPurchase(string upgradeId)` — appelé par `HubScreen`
- `int GetUpgradeLevel(string upgradeId)` — lu directement par les systèmes consommateurs
  (`Player`, `MagnetSpawner`, `AetherCore`, `EnemySpawner`, `LevelUpSystem`) pour les statTargets
  qui ne sont pas des champs `PlayerStats` (consommables par run ou valeurs lues au point d'usage,
  cf. §9.5 et `_implementationNotes` de `data/meta_upgrades.json`).

### 18.5 Application des bonus meta à PlayerStats (v2)

À chaque début de run, `GameManager`/`MetaProgressionSystem.ApplyMetaBonusesToStats()` construit
les `PlayerStats` initiales :

```
PlayerStats.MaxHp             += meta.hp_boost_level × 20 + meta.hp_boost_2_level × 35
PlayerStats.DamageMultiplier  += meta.damage_boost_level × 0.10 + meta.damage_boost_2_level × 0.08
PlayerStats.Speed             += meta.speed_boost_level × 15
PlayerStats.CooldownReduction += meta.cooldown_reduction_level × 0.05 + meta.cooldown_reduction_2_level × 0.04
PlayerStats.DamageReduction   += meta.damage_reduction_level × 0.05 + meta.damage_reduction_2_level × 0.04   (clampé au hardcap 0.40)
PlayerStats.HpRegenPerSecond   = meta.hp_regen_level × 0.4     // NOUVEAU champ PlayerStats
```

L'arme de départ est déterminée par le **personnage** choisi (`CharacterDef.StartingWeaponId` →
`GameManager.StartingWeaponId` → `InventorySystem`), cf. §4. Le sélecteur d'arme de départ au Hub
imaginé ici (piloté par l'ex-upgrade `starting_weapon_alt`) n'a jamais été câblé — l'upgrade a été
retiré le 2026-07-04.

Les statTargets **consommables par run ou lus au point d'usage** (pattern déjà établi par
`reroll`/`skip` dans `LevelUpSystem`) ne passent PAS par `ApplyMetaBonusesToStats()` — chaque
système lit `MetaProgressionSystem.Instance.GetUpgradeLevel(id)` directement :
- `Player.cs` : `extra_life` (revive à 30% HP), `damage_absorb` (N coups à 0 dégât/run).
- `MagnetSpawner.cs` : `bonus_magnet` (fenêtres de spawn Aimant supplémentaires en overtime).
- `AetherCore.cs` : `core_magnetism` (rayon de collecte, base 20 px + bonus cumulé).
- `EnemySpawner.cs` : `overtime_stabilizer` (dampening de la pente d'escalade overtime, max -15%).

### 18.6 UX du Hub

L'écran Hub liste désormais **17 améliorations** (au lieu de 8) :
- Chaque ligne : nom de l'amélioration | niveau actuel / niveau max | coût niveau suivant | bouton
  "Acheter" (grisé si Échos insuffisants ou niveau max atteint).
- En haut : compteur d'Échos disponibles, mis à jour en temps réel après chaque achat.
- **Scroll requis** (post-MVP, à charge de `developpeur`) : la liste de 17 items déborde de l'écran
  dans le `VBoxContainer` simple actuel — nécessite un `ScrollContainer` (hors scope de cette
  passe de design, cf. brief `developpeur`).
- Pas d'arbre visuel au MVP — liste suffisante pour valider la mécanique ; un regroupement visuel
  "8 historiques / 10 nouveaux" ou par catégorie (offensif / défensif / économie / overtime) est
  une piste d'amélioration UX post-MVP à évaluer avec `directeur-artistique`.

## 19. Tiers ennemis, orbes XP différenciés et mini-boss

> Ajouté le 2026-06-24 par l'agent `game-designer`. Valeurs runtime dans `data/enemies.json`.
> Les mini-boss sont sortis de la liste §14 (hors-scope) et promus en scope post-MVP immédiat —
> ils enrichissent la boucle de run sans modifier l'architecture existante.

### 19.1 Tiers ennemis et orbes XP différenciés

Chaque tier dispose d'une orbe XP de taille et de couleur distinctes, renforçant la lecture
visuelle de la valeur d'un kill sans UI supplémentaire.

| Tier | Ennemi | Orbe (px) | Couleur hex | XP drop | Rôle |
|------|---------|-----------|-------------|---------|------|
| T1 | Essaim de Rouille (`rust_swarm`) | 8×8 | `#44FF66` vert | 3 | Fourrage |
| T2 | Drone Corrompu (`corrupted_drone`) | 10×10 | `#44AAFF` cyan | 7 | Harceleur |
| T3 | Sentinelle Corrompue (`corrupted_sentinel`) | 12×12 | `#AA44FF` violet | 20 | Pression distance |
| T4 | Colosse Greffé (`grafted_colossus`) | 14×14 | `#FFD700` or | 60 | Bruiser |
| MB | Mini-boss | 14×14 | `#FFD700` or brillant (bloom fort) | 80–120 | Elite |

**Règles d'implémentation des orbes différenciées :**
- La taille de l'orbe est contrôlée par `Scale` du nœud `XpOrb` instancié à la mort.
- La couleur hex est appliquée en `Modulate` sur le `Sprite2D` de l'orbe.
- La valeur XP est passée en paramètre à `SpawnXpOrb(int xpAmount)` depuis `EnemyBase.Die()`.
- Les mini-boss utilisent un `PointLight2D` d'énergie doublée sur leur orbe (effect bloom gold).
- `EnemyBase` expose un champ `[Export] int XpValue` lu depuis `data/enemies.json` par `EnemySpawner`.

**Cohérence avec §6 et §17 :** les valeurs XP de ce tableau remplacent définitivement celles du
tableau §7 (anciennes valeurs : Essaim=2, Drone=3, Sentinelle=8, Colosse=25 — données obsolètes
d'avant l'équilibrage 2026-06-23). Les valeurs de référence sont dans `data/enemies.json`.

### 19.2 Mini-boss — Rôdeur de Rouille (`rust_stalker`)

**Concept** : araignée mécanique corrodée, boss de mi-run. Première confrontation à un ennemi
qui nécessite un build orienté plutôt qu'un spam de DPS de zone.

| Paramètre | Valeur |
|-----------|--------|
| Sprite | 64×64 px (exception justifiée : silhouette boss distincte) |
| HP | 300 |
| Vitesse | 85 px/s |
| Dégâts contact | 15/s (contactRadius = 32 px) |
| XP drop | 80 (orbe or brillant) |
| Spawn dès | 12 min |
| Poids spawn | 1 |
| Max simultanés | 1 |
| Délai respawn min | 180 s (3 min après mort) |
| IA | `straight_chase` (fonce sans esquive — prévisible mais tanky) |
| Scaling HP | +10%/min |
| Scaling dégâts | +7%/min |

**Animations (SpriteFrames à produire) :**
- `idle` : 4 frames, boucle
- `move` : 6 frames, boucle
- `attack` : 5 frames (impact au frame 3), one-shot → retour `idle`
- `death` : 12 frames (dissolution mécanique), one-shot → `QueueFree`

**Récompense à la mort :**
1. Orbe XP or brillant (80 XP).
2. Déclenche un écran de choix d'arme (3 cartes armes uniquement, pas de passifs) — même
   mécanique que `LevelUpScreen` mais pool restreint aux armes non possédées ou upgradables.

**Règles mini-boss :**
- Ne compte PAS dans le cap `maxEnemies` de `EnemySpawner` (spawn garanti indépendamment de la
  pression normale).
- Un seul `rust_stalker` peut exister simultanément. Si un est vivant, `EnemySpawner` ignore les
  tirages qui l'auraient sélectionné.
- Après sa mort : respawn possible après 3 min minimum (champ `minRespawnDelaySec = 180`).
- Barre de vie dédiée affichée en haut d'écran (HUD) quand il est présent dans l'arène.

### 19.3 Mini-boss — Sentinelle Maîtresse (`master_sentinel`)

**Concept** : version élite de la Sentinelle Corrompue, doubles canons, tir en éventail.
Confrontation à un boss à distance nécessitant de la mobilité et une couverture derrière les
obstacles.

| Paramètre | Valeur |
|-----------|--------|
| Sprite | 64×64 px |
| HP | 450 |
| Vitesse | 50 px/s |
| Dégâts projectile | 18 par projectile |
| Cadence de tir | 1 projectile toutes les 1,5 s, éventail ±12° (2 projectiles simultanés) |
| XP drop | 120 (orbe or brillant) |
| Spawn dès | 16 min |
| Poids spawn | 1 |
| Max simultanés | 1 |
| Délai respawn min | 180 s |
| IA | `ranged_kiter` (même comportement que `corrupted_sentinel`, zone de maintien 250–400 px) |
| Scaling HP | +12%/min |
| Scaling dégâts | +8%/min |

**Animations (SpriteFrames à produire) :**
- `idle` : 4 frames, boucle
- `move` : 6 frames, boucle
- `attack` : 6 frames (flash double-canon aux frames 2 et 4), one-shot → retour `idle`
- `death` : 14 frames (explosion systémique), one-shot → `QueueFree`

**Récompense à la mort :**
1. Orbe XP or brillant (120 XP).
2. Écran choix d'arme (3 cartes armes), même mécanique que `rust_stalker`.

**Règles mini-boss :** identiques à §19.2. Les deux mini-boss coexistent potentiellement (un
`rust_stalker` et une `master_sentinel` peuvent être présents en même temps si les timers de
respawn s'alignent après 16 min).

### 19.4 Architecture EnemySpawner pour les mini-boss

Champs JSON spécifiques aux mini-boss (présents dans `data/enemies.json`) :

```json
"maxSimultaneous": 1,
"minRespawnDelaySec": 180,
"isMinisBoss": true
```

Comportement attendu de `EnemySpawner` :

1. **Pool séparé** : les mini-boss ne participent pas au tirage pondéré du pool normal. Ils ont
   leur propre minuterie par `id` (`Dictionary<string, float> _miniBossRespawnTimers`).
2. **Spawn conditionnel** : à chaque frame `_Process`, vérifier si `_elapsed >= spawnStartMinute * 60`
   ET `_miniBossRespawnTimers[id] <= 0` ET count actuel de cet id dans l'arène == 0.
3. **Position de spawn** : même règle que les ennemis normaux — bords de l'arène, hors zone de
   sécurité 200 px autour du joueur.
4. **Mort** : déclencher `_miniBossRespawnTimers[id] = minRespawnDelaySec` depuis le signal
   `EnemyKilled` de `GameManager` en filtrant sur `enemyId`.
5. **Barre de vie HUD** : `HUD` s'abonne au signal `MiniBossSpawned(string id, EnemyBase enemy)`
   et `MiniBossDied(string id)` émis par `EnemySpawner`. Affiche `BossHpBar` en haut d'écran
   (couleur `#FF4422` rouge-rouille) pendant la présence du mini-boss.

### 19.5 Cohérence narrative (à valider avec `story-teller`)

- **Rôdeur de Rouille** : créature de Rouille Vivante, araignée mécanique mutée. Lore possible :
  ancien drone de surveillance du Sanctuaire, corrompu par la Convergence, devenu chasseur
  territorial. Nom provisoire — validation par `story-teller` avant production sprite.
- **Sentinelle Maîtresse** : unité d'élite d'avant la Convergence, coque intacte mais
  IA corrompue. Concept articulé autour du thème "technologie qui s'est retournée contre
  ses créateurs". Validation `story-teller` requise.

### 19.6 Cohérence visuelle (à valider avec `directeur-artistique`)

- Les deux mini-boss utilisent des sprites 64×64 px (même exception que le Colosse Greffé en
  48×48 px — justifiée par la nécessité d'une silhouette boss distincte en combat dense).
- Palette mini-boss : teintes **rouille intense** (`#CC3300` à `#881100`) pour le Rôdeur ;
  teintes **gris métallique froid** (`#AABBCC` à `#445566`) pour la Sentinelle Maîtresse.
  Implants Aether corrompus `#AA22FF` (rouge-violet, distinct du violet pur `#AA44FF` des
  ennemis normaux) pour les deux.
- Barre de vie HUD mini-boss : validation DA requise pour l'intégration dans le HUD existant
  sans surcharger l'écran (placement suggéré : centré en haut, sous le timer).
- Validation `directeur-artistique` requise avant production des SpriteFrames.

## 20. Boss final — Le Noyau Rouillé (`rusted_core`) — équilibrage TTK

> Spécifié le 2026-06-28 par l'agent `game-designer`. Valeurs runtime : `data/enemies.json`
> (`rusted_core`). Le Noyau Rouillé est désormais **la condition de victoire** de la run (le vaincre
> déclenche « EXTRACTION REUSSIE », cf. CLAUDE.md / `RustedCore.FinishDeath`). Il apparaît à 13 min.

### 20.1 Problème constaté (2026-06-28)

Le boss mourait en **3,5 à 8,6 s** = anticlimax. Cause : `maxHp` de base = 1600, mais l'`EnemySpawner`
applique `PV = maxHp × (1 + t_min × hpScalingPerMinute) × EnemyHpMult`. À 13 min avec l'ancien
scaling 0,12 : `1600 × (1 + 13×0,12) = 1600 × 2,56 = 4096` PV en Normal — dérisoire face à un build
réaliste de 5 armes (~474 DPS niv.5, ~711 niv.10, ~1185 niv.20, hors `thermal_core` ×1,45).

### 20.2 Cible de TTK décidée

**Cible : ~25-30 s pour un build moyen (armes niveau ~10) en Normal**, soit **~20 000 PV effectifs**.

Justification design :
- La victoire ne se joue plus au timer : on peut kiter le boss indéfiniment, donc **la survie est le
  vrai gate**, pas le DPS brut. Le rôle des PV du boss est de transformer le kill en **événement
  climactique** : assez long pour ressentir des phases de fuite et la pression des adds (~180 ennemis
  au cap à 13 min), assez court pour rester lisible dans le chaos.
- < 10 s = anticlimax ; > ~45 s en combat dense = épuisant et le boss se « perd » visuellement. La
  fenêtre 20-30 s est le pic d'intensité idéal pour un survivor.
- On calibre sur le build **sans** `thermal_core` : un build min-maxé (avec ×1,45) tue ~30% plus vite,
  ce qui est une récompense légitime de l'optimisation, pas un déséquilibre.

### 20.3 Valeurs retenues (`data/enemies.json`)

| Champ | Avant | Après | Raison |
|---|---|---|---|
| `maxHp` | 1600 | **12000** | Atteint ~20 000 PV effectifs en Normal à 13 min |
| `hpScalingPerMinute` | 0,12 | **0,05** | Boss à **spawn fixe** (13 min) → le scaling n'est qu'un multiplicateur constant ; on l'abaisse pour rendre les PV prévisibles et robustes même si le joueur retarde le spawn (kite) |
| `damagePerProjectile` | 28 | **26** | Léger trim : le combat dure ~5× plus longtemps → l'exposition aux salves radiales est multipliée. On baisse un peu pour que le combat ne devienne pas une pure course aux PV, sans le rendre inoffensif (~46 dmg/projectile reste ~30% d'une barre saine) |
| `damageScalingPerMinute` | 0,08 | **0,06** | Idem, compense l'exposition prolongée |

PV effectifs = `12000 × (1 + 13×0,05) × EnemyHpMult` = `12000 × 1,65 × mult` :
- **Facile** (×0,8) : **15 840 PV** · **Normal** (×1,0) : **19 800 PV** · **Difficile** (×1,3) : **25 740 PV**

Dégât projectile effectif = `26 × (1 + 13×0,06) × EnemyDamageMult` = `26 × 1,78 × mult` = 46,3 base :
- Facile (×0,6) : **27,8** · Normal (×1,0) : **46,3** · Difficile (×1,35) : **62,5**

### 20.4 Vérification TTK (PV effectifs ÷ DPS build, hors `thermal_core`)

| Build | DPS | Facile (15 840) | **Normal (19 800)** | Difficile (25 740) |
|---|---|---|---|---|
| niv.5 | 474 | 33 s | 42 s | 54 s |
| **niv.10** | 711 | 22 s | **28 s** | 36 s |
| niv.20 | 1185 | 13 s | 17 s | 22 s |

Avec `thermal_core` (×1,45) en Normal : niv.5 → 29 s, **niv.10 → 19 s**, niv.20 → 12 s.

Lecture : la cellule de référence (**niv.10, Normal**) tombe à **28 s** (19 s avec `thermal_core`),
pile dans la fenêtre 20-30 s visée. Les extrêmes sont cohérents : un build sur-optimisé niv.20 garde
un vrai combat de 12-17 s en Normal ; un joueur sous-levelé (niv.5) sur Difficile met ~54 s — mais à
ce stade il mourra probablement aux salves + adds avant de le tuer (le gate de survie fait son office,
résultat « tu n'étais pas prêt » intentionnel).

### 20.5 Évolution possible — mécanique de PHASES (brief pour `developpeur`, NON implémenté)

Si le playtest juge qu'un long sac à PV manque de relief, on pourra ajouter des phases SANS toucher
aux PV globaux. À traiter séparément par `developpeur` (hors périmètre data) :
- **2 seuils de PV** (66% et 33%) déclenchant un changement de pattern : ex. phase 1 salves radiales
  (actuel), phase 2 salves radiales **+ ondes de choc plus rapprochées**, phase 3 **invocation d'un
  pack d'adds** + cadence de tir accrue.
- **Brève invulnérabilité telegraphiée** (~1 s, flash + recharge du noyau) à chaque passage de phase,
  pour marquer la bascule et créer une fenêtre de fuite/repositionnement.
- Garder les PV totaux à 12000 base : les phases redistribuent l'intensité, elles n'allongent pas la
  TTK. À valider lisibilité avec `directeur-artistique` (la bascule de phase doit être lisible dans le
  chaos) et cohérence narrative avec `story-teller` (le Noyau « se surcharge » en mourant).

## 21. Faune par biome — 20 nouveaux ennemis basiques (conçu 2026-07-02)

> Conçu par l'agent `game-designer`, en réponse à `docs/EXPANSION_PLAN.md` §B.2 (« nouveaux ennemis
> thématisés par biome »), avec une contrainte resserrée : **réutiliser exclusivement les 4
> archétypes d'IA existants** (`straight_chase`, `erratic_chase`, `ranged_kiter`, `slow_hunter`),
> sans nouveau type d'IA ni nouvelle mécanique de gimmick (téléportation, flaque au sol, slow au
> contact, laser téléguidé...) — ces idées plus ambitieuses de l'EXPANSION_PLAN restent en
> **hors-scope MVP** (§14) tant qu'un nouvel archétype d'IA n'est pas explicitement priorisé et
> câblé côté `developpeur`. Livrable brut (JSON prêt à relire/fusionner) :
> **`data/enemies_biome_expansion.json`** — volontairement **séparé** de `data/enemies.json`, la
> fusion (et la création des scènes/sprites dédiés) étant gérée par une tâche `developpeur` en
> parallèle.

### 21.1 Principe : matrice 5 biomes × 4 archétypes

Chaque biome (sanctuaire, aether, fournaise, givre, néon) reçoit exactement **une variante par
archétype**, pour préserver dans chaque biome la même composition de vagues à 4 rôles que
l'existant (fourrage / harceleur / pression à distance / bruiser), tout en changeant l'identité de
faune :

| Biome | Fourrage (`straight_chase`) | Harceleur (`erratic_chase`) | Pression distance (`ranged_kiter`) | Bruiser (`slow_hunter`) |
|---|---|---|---|---|
| Sanctuaire (rouillé/mécanique) | Marcheur Marqué | Drone Éclaireur | Tourelle Ambulante | Golem de Maintenance |
| Aether (spectral/énergétique) | Éclat d'Aether | Spectre Dérivant | Vigile Spectral | Golem d'Aether |
| Fournaise (igné/fondu) | Rampant de Braise | Étincelle Volatile | Cracheur de Lave | Colosse de Magma |
| Givre (gelé/cristallin) | Rampant de Givre | Éclat de Glace Errant | Tireur Cryogénique | Titan de Glace |
| Néon (synthétique/holographique) | Drone de Sécurité | Glitch Holographique | Tourelle Laser | Golem Synthétique |

Chaque variante s'écarte d'au plus ~20% des stats de son archétype ancre (`rust_swarm` HP20/vit120/
dps5, `corrupted_drone` HP15/vit220/dps8, `corrupted_sentinel` HP45/vit70/dpp12,
`grafted_colossus` HP200/vit55/dps20), avec des écarts **dirigés par le thème du biome** plutôt
qu'aléatoires :

- **Sanctuaire** : le plus proche des valeurs ancre (biome neutre de référence).
- **Aether** : plus rapide/fragile et plus erratique (angle de déviation 60°, le plus haut du jeu)
  — incarne l'énergie pure, volatile.
- **Fournaise** : dégâts et scaling de dégâts relevés sur les 4 rôles, cooldowns de frappe/tir les
  plus courts — cohérent avec le malus de biome existant (ennemis +18% vitesse).
- **Givre** : PV et scaling PV relevés, vitesses et cadences les plus lentes (le Titan de Glace est
  le bruiser le plus tanky du jeu) — cohérent avec le bonus de biome existant (ennemis -18%
  vitesse) : le biome le plus « défensif » à jouer, mais avec des ennemis qui encaissent plus.
- **Néon** : profil « glass cannon » (PV parmi les plus bas, dégâts/vitesse de projectile parmi les
  plus hauts — la Tourelle Laser tire le projectile le plus rapide et le plus mordant du jeu) —
  cohérent avec le double bonus/malus de biome existant (ennemis +10% vitesse, +15% XP,
  risque contre récompense).

### 21.2 Câblage côté `developpeur` — FAIT (2026-07-02)

Les 20 entrées suivent exactement le format de `data/enemies.json` (mêmes champs, mêmes
conventions `ai.type`). Câblage réalisé :
- **Mécanisme sprite data-driven par id** (même principe que `Player.SetCharacterFrames` /
  `CharacterDef.FramesPath`) : `EnemySpawnData.FramesPath` (optionnel, JSON `framesPath`) est
  chargé au runtime par `EnemyBase.SetSpriteFrames(path)` et appliqué à l'`AnimatedSprite2D` de la
  scène instanciée, juste après `AddChild` dans `EnemySpawner.SpawnEnemy`. Aucune nouvelle scène
  `.tscn` ni sous-classe C# créée : les 20 ids réutilisent les 4 scènes archétype existantes
  (`RustSwarm.tscn`/`CorruptedDrone.tscn`/`CorruptedSentinel.tscn`/`GraftedColossus.tscn`),
  résolues via `EnemySpawnData.AiType` (JSON `ai.type`) et `EnemySpawner.ArchetypeScenePaths` quand
  l'id n'a pas d'entrée dédiée dans `ScenePaths`. Les 8 ennemis existants ne sont pas affectés
  (`FramesPath` vide → SpriteFrames posé dans le `.tscn` inchangé).
- Fusionné dans `data/enemies.json` (28 entrées au total ; `data/enemies_biome_expansion.json`
  reste en place comme trace du livrable design d'origine, non lu par le jeu).
- Ajouté au bestiaire (`Codex.Enemies`), avec accents Fournaise/Givre/Néon dédiés (`Ember`/`IceB`/
  `Magenta` dans `src/UI/Codex.cs`) et clés `ENEMY_*` EN/FR/ES dans `localization/ui.csv`.
- Chemins de sprite référencés (`res://assets/sprites/enemies/<id>/<id>_frames.tres` et
  `..._idle_01.png`) mais **pas encore générés** — à produire par `graphiste` en tâche séparée.
- **À valider par `game-tester`** : la dilution de `spawnWeight` documentée dans
  `enemies.json.faunaExpansionNote` (le pool actif double dans chaque biome ; ajuster les poids si
  la densité par type baisse trop après premier playtest).

### 21.3 Cohérence narrative et visuelle (à valider)

- Noms provisoires, cohérents avec le lore existant (Rouille Vivante pour Sanctuaire, Aether
  corrompu pour Aether/Néon) mais **non validés par `story-teller`** — à faire avant production
  sprite définitive, notamment pour les noms Aether/Néon qui touchent au lore de la Convergence.
- Palette suggérée par `colorPlaceholder` dans le JSON (ex. `rust_amber`, `violet_glow`,
  `ember_orange`, `frost_blue`, `neon_magenta`...) — à valider par `directeur-artistique` avant
  toute production de sprite, en cohérence avec `docs/STYLE_GUIDE.md`.

## 22. Affixes d'élite (implémenté 2026-07-04)

> Inspiration : les **élites/affixes** de Risk of Rain 2 et Diablo. N'importe quel ennemi *basique*
> peut être promu « élite » et recevoir **un** affixe qui change radicalement sa menace, à coût de
> production quasi nul (aucune nouvelle scène ni sprite — seulement des multiplicateurs de stats +
> un rendu teinté/agrandi + un halo). Répond à la **limite « silhouettes recolorées »** de §21 : la
> variété de menace vient désormais du *comportement*, pas de la forme. Logique pure et testée dans
> `src/Core/Rules/EliteAffixTable.cs` (12 tests xUnit).

### 22.1 Fréquence et éligibilité

- **Éligibles** : uniquement les ennemis basiques (`maxSimultaneous == 0`). Les mini-boss, boss et
  le boss de fin de niveau ne deviennent **jamais** élite (ils sont déjà des menaces uniques).
- **Fréquence** (`EliteAffixTable.EliteChance`) : `clamp(0.03 + 0.02 × t_minutes, 0, 0.28)` — ~3 % au
  début, montée de +2 %/min, **plafond dur à 28 %** (jamais une horde d'élites, garde-fou lisibilité
  + perf). Un ennemi éligible tire son affixe au hasard, réparti également entre les 5.
- **Flag debug** `--force-elites` : force tous les ennemis basiques en élite (validation game-tester
  + tuning), combinable avec `--biome=<id>`. Aucun effet en build normal (`DebugHooks.ForceElites`).

### 22.2 Les 5 affixes

| Affixe | Teinte | Effet de jeu | Contre-jeu |
|---|---|---|---|
| **Blindé** (`Armored`) | bleu acier | dégâts reçus ×0.45, PV ×1.7 | burst/perce-armure, ou l'ignorer |
| **Régénérant** (`Regenerating`) | vert | régén 6 %/s du MaxHp si non frappé depuis 1.5 s, PV ×1.5 | DPS soutenu (ne pas lâcher la cible) |
| **Explosif** (`Explosive`) | orange | explose à la mort (AoE rayon 84 px, dégâts = contact ×2.2, anneau rouge + screenshake), PV ×1.1 | tuer à distance / ne pas être collé |
| **Frénétique** (`Frenzied`) | rouge | vitesse ×1.7, dégâts ×1.3, mais PV ×0.7 (glass cannon) | prioriser, kiter |
| **Vampirique** (`Vampiric`) | magenta | se soigne de 50 % des dégâts qu'il inflige au joueur, PV ×1.4 | ne pas le laisser toucher / burst |

- **Récompense** : tout élite donne **×2.5 à ×3 d'XP** (orbe de tier supérieur automatiquement via
  `GetOrbTier`) et une **proba de drop de PV relevée** (0.20–0.30 vs 0.08 normal) — le risque paie.
- **Rendu** : teinte multiplicative sur le `SelfModulate` de l'`AnimatedSprite2D` (n'écrase pas le
  `Modulate` du corps → le HitFlash reste net), agrandissement ×1.35, et **halo pulsant coloré**
  (`EliteAura`, Node2D en `_Draw`) derrière l'ennemi. Pas de nameplate : la menace se lit à la
  couleur + la taille (approche RoR2).

### 22.3 Câblage technique

- `EliteAffixTable` (Rules pur) : `EliteChance` / `ShouldBeElite(t, roll)` / `Pick(roll)` /
  `Modifiers(affix)` → `EliteModifiers` (struct de multiplicateurs + teinte r/g/b, sans dépendance
  Godot). Tuning centralisé ici.
- `EnemyBase.ApplyElite(affix)` : applique les multiplicateurs **après** `ApplyScaling` (donc sur les
  stats déjà scalées), câble les comportements (blindage via `_damageTakenMult` dans `TakeDamage`,
  régén via `UpdateEliteRegen`, vampirisme via `ApplyLifesteal`, explosion via
  `TriggerEliteExplosion` dans `Die`), et le rendu.
- `EnemySpawner.SpawnEnemy` : tire l'élite pour les ids éligibles juste après `ApplyScaling`.
- **Piège** : `GraftedColossus` (scène des `slow_hunter`, `Die()` surchargé qui n'appelle pas
  `base.Die()`) appelle explicitement `TriggerEliteExplosion()` + `ApplyLifesteal()` pour que les
  affixes Explosif/Vampirique restent universels. Les ennemis *ranged* (kiters) ne déclenchent pas le
  vampirisme (ils ne touchent pas au contact) — c'est voulu.

### 22.4 Extensions possibles (hors-scope actuel)

- **Invocateur** (spawn de fourrage périodique) et **Aura de ralentissement** (slow du joueur à
  proximité) : listés au brainstorm mais nécessitent un accès au spawner / une mécanique de slow
  côté joueur — reportés (surface de code + risque plus élevés).
- **Double affixe en overtime**, **affixes sur mini-boss**, ligne de bestiaire dédiée aux élites.

## 23. Retours testeur — arme dirigée + recadrage difficulté (2026-07-04)

Suite au retour d'un testeur (« difficile au début, mais on devient OP trop facilement si on
survit ; les flammes occultent le perso ; tout est auto-visé, peu de place au skill »), trois
ajustements :

### 23.1 Lisibilité — le joueur passe au-dessus des VFX d'armes
`Player.ZIndex = 5` (posé dans `_Ready`) : le joueur reste visible « dans le feu de l'action »,
au-dessus des flammes du Jet de Pyre (ZIndex 2), muzzle flashs (4), cônes/champs. Reste sous les
flashs d'impact ponctuels (foudre ZIndex 6) et l'UI (CanvasLayer). Corrige l'occultation du perso
par ses propres flammes sans retoucher chaque VFX.

### 23.2 Arme DIRIGÉE — Lance Vectorielle (`vector_lance`, Rare)
Première arme qui laisse place au **skill de visée** : au lieu d'auto-viser l'ennemi le plus proche,
elle tire un trait perforant dans la **direction de visée du joueur** (`Player.AimDirection`). Visée
**souris** en clavier/souris (direction joueur→curseur), **stick droit** en manette (`JoyAxis.RightX/Y`,
deadzone 0.35) — bascule automatique selon le dernier périphérique actionné (mise à jour 2026-07-04,
remplace l'ancienne visée par direction de déplacement). Un **réticule** (petit triangle `Polygon2D`
teinté à l'identité du perso) tourne autour du joueur (`AimIndicatorRadius=28`) pour montrer la visée ;
affiché uniquement si une arme dirigée est équipée (`vector_lance`/`vector_beam`). Perforante dès le niveau 1 (récompense l'alignement) ; niv. 4-5 ajoutent des
traits en éventail serré (14° puis 20°). Réutilise `Bullet` (aucun nouveau projectile). Le reste de
l'arsenal garde l'auto-visée. Stats → `data/weapons.json`.

**Fusion — Rayon Vecteur (`vector_beam`, Épique)** : `vector_lance` niv.5 + **Servo-Moteurs**. Le trait
dirigé devient un **rayon perforant CONTINU** (`type: continuous_beam`, plus de cooldown) orienté par
`Player.AimDirection`, lissé (Slerp) pour pivoter sans à-coups. Inflige `damagePerTick=11` toutes les
`tickInterval=0.13`s à tout ennemi à ≤ `hitRadius=34` px du segment `[joueur → joueur + 520px·aim]`
(perforation totale). Amplifie le skill de visée (on « peint » l'écran). Stats en dur dans
`VectorBeam.cs` (les fusions n'ont pas de niveaux JSON, cf. `SolarColumn`/`IonicStorm`). VFX : double
`Line2D` (halo doré + cœur blanc) + `PointLight2D` à la source. Sépare Servo-Moteurs sur 3 fusions
(orbital_swarm, hornet_swarm, vector_beam) — thème mobilité cohérent avec une arme liée au déplacement.

### 23.3 Courbe de difficulté non-linéaire
`EnemyScaling.CurvedFactor` remplace le facteur linéaire `1 + t×perMinute` pour le scaling runtime
(HP/dégâts, via `ScaledCurved` dans `EnemySpawner`) :
- **Early grace** (t < 1,5 min) : ennemis affaiblis jusqu'à −15% à t=0 → départ moins punitif.
- **Linéaire** entre 1,5 et 4 min (identique à avant).
- **Accélération quadratique** au-delà de 4 min (`LateCoeff=0.08 × perMinute × (t−4)²`) : le mid/late
  rattrape le power-creep du build (fusions + multiplicateurs) → on ne devient plus « OP » passif.
  Ex. à t=12 min, un ennemi à `perMinute=0.14` est ~+27% plus coriace qu'en linéaire ; à t=15, ~+44%.

**La courbe cible les ennemis BASIQUES uniquement.** Les mini-boss (`maxSimultaneous > 0`) et le boss
de fin (`rusted_core`) gardent le **scaling linéaire** `Scaled` : ce sont des gates de survie calibrés
séparément (TTK, cf. §17/§18/§20), la courbe fausserait leur fenêtre de victoire (+22% de PV boss à
13 min). Le tri se fait dans `EnemySpawner.SpawnEnemy` via `isChampion`. `grafted_colossus`
(`maxSimultaneous=0`, spawn ambiant + éligible élite) reste un ennemi basique costaud → courbe.

`EnemyScaling.Scaled` (linéaire) est donc conservé à la fois pour les champions, la compat et les
tests de référence. Couvert par 4 tests `CurvedFactor`/`ScaledCurved` (87 tests au total).

## 24. Fusion — Voile de Givre (`frost_veil`, Épique) (2026-07-04)

Nouvelle fusion complétant la matrice arme/passif et rééquilibrant l'usage de **Plaque Renforcée**
(qui n'alimentait qu'une seule fusion, `overload_aegis`).

**`cryo_lance` niv.5 + Plaque Renforcée → Voile de Givre.** Le rayon glacé perçant devient une **aura
de givre CONTINUE** centrée sur le joueur (`type: continuous_frost_aura`, plus de cooldown discret,
tick 0.2 s) : chaque tick inflige `damagePerTick=8.4` (42 DPS) ET un ralentissement fort
(`slowMult=0.55`, plafonné à −40 % par `CrowdControlCaps` via `EnemyBase.ApplySlow`, `slowDuration=0.5`
réappliqué en continu) à tout ennemi dans `radius=150` px. Résultat : la nuée reste engluée au ralenti
à portée — traduction gameplay du fantasme défensif du blindage (« ta meilleure armure, c'est le froid »).

Implémentée façon `FusionBlade` (aura radiale, stats en dur dans `FrostVeil.cs`, les fusions n'ayant pas
de niveaux JSON). **VFX « vraie brume de froid »** (refonte 2026-07-05, sans shader) : deux nappes de
brume douce (sprites radiaux translucides bleutés) qui dérivent et pulsent en sens opposés → volume
tourbillonnant, particules de givre flottantes, liseré glacé discret (portée), lueur froide additive
légère — tout en `ZIndex=-1` (sous le sprite joueur, au-dessus des ennemis).

**Ennemis visiblement gelés** : tout ennemi ralenti (Voile de Givre OU Lance Cryo) vire au **bleu
glacé** — teinte `FrostTint` multipliée sur `SelfModulate` dans `EnemyBase.UpdateStatusEffects`, basculée
au seul changement d'état (perf 200-300 ennemis). Se combine proprement avec la teinte d'élite (multipliée
sur `_baseSelfModulate`) et n'interfère pas avec le HitFlash (qui agit sur `Modulate` du corps).

Répartition des passifs sur les fusions après ajout : thermal_core ×2, capacitor ×2, servo_motors ×3
(orbital/hornet/vector_beam), reinforced_plating ×2 (overload_aegis/frost_veil). Armes encore sans
fusion : scatter_volley, glaive, singularity.

## 25. Contrôles de déplacement — ZQSD + remap clavier (2026-07-05)

Le déplacement passe des actions natives `ui_*` (flèches uniquement) à des actions dédiées
**`move_up/down/left/right`** (`InputRemap`), séparées de la navigation menu (qui garde `ui_*`) pour
qu'un remap ne casse jamais le focus clavier. Bindings par défaut : **ZQSD** (touches par label →
AZERTY natif) + flèches directionnelles + manette (D-pad & stick gauche). Le `Player` lit
`Input.GetVector(move_left, move_right, move_up, move_down)`.

**Remap** : l'écran Options ajoute une section « Contrôles » avec un bouton par direction. Clic → « appuyez
sur une touche » → la prochaine touche pressée devient la touche principale de la direction (Échap
annule). Les flèches et la manette restent toujours actives en secondaire. Un bouton « Touches par défaut
(ZQSD) » restaure. Persistance : section `input` de `user://settings.cfg` (keycode par action), rechargée
et appliquée au démarrage via `GameSettings.Apply → InputRemap.ApplyAll`.

## 26. Personnage — Vecteur (cyborg de précision) (2026-07-05)

4e personnage jouable, premier bâti autour d'une **arme dirigée**. `Characters.All` (id `vecteur`) :
PV 90 / vitesse 210 (profil médian-fragile, entre Chimera et Vagabond), teinte violette `(0.72, 0.5, 1)`,
sprite dédié `assets/sprites/player/vecteur/` (généré par `tools/generate_character_sprites.py` — châssis
violet élancé, bandeau-scanner + noyau énergie). **Arme de signature : `vector_lance`** (Lance Vectorielle),
donc le réticule de visée (souris/stick droit, cf. §23.2) est actif dès le départ et teinté à son identité.
Ajouté à `GameSettings.SignatureWeapons` (toujours « découverte » à l'arsenal). Aucune nouvelle mécanique
moteur : réutilise entièrement le pipeline perso existant (`GameManager` pose stats/frames/teinte +
`InventorySystem.AddOrUpgradeWeapon("vector_lance")`). Clés loc `CHAR_VECTEUR_NAME/TAG/DESC` (EN/FR/ES).
Fantasme : récompenser la visée maîtrisée plutôt que l'auto-tir — le seul perso dont l'arme de base « vise
à la main ».
