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

> Spécifié le 2026-06-20 par l'agent `game-designer`. Valeurs de tuning dans `data/meta_upgrades.json`.

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

### 9.2 Formule de calcul des Échos en fin de run

```
Échos = floor(tempsSurvécuSecondes / 20)
      + floor(ennemisKills / 10)
      + (noyauxRamassés × 5)
      + 10   ← bonus de base (toujours gagné)
```

**Calibration :**

| Scénario | Temps (s) | Kills | Noyaux | floor(T/20) | floor(K/10) | N×5 | +10 | **Total** |
|---|---|---|---|---|---|---|---|---|
| Mort en 30 s | 30 | 0 | 0 | 1 | 0 | 0 | 10 | **11** |
| Run 3 min sans mourir | 180 | 120 | 4 | 9 | 12 | 20 | 10 | **51** |
| Run 5 min (moyenne) | 300 | 250 | 8 | 15 | 25 | 40 | 10 | **90** |
| Run 15 min complète | 900 | 600 | 25 | 45 | 60 | 125 | 10 | **240** |

*(Valeurs kills/noyaux estimées selon le mix d'ennemis et les spawns périodiques de Noyaux.)*

Objectifs atteints :
- Première run 3-5 min : **51-90 Échos** (cible 50-80, la run 5 min donne un peu plus car elle
  est plus difficile à réaliser — acceptable).
- Run complète 15 min : **~240 Échos** (cible 150-250). ✓
- Mort en 30 s : **11 Échos** (min 10 garanti). ✓
- Lisibilité : les 3 composantes sont affichées séparément sur l'écran de fin (§9.4).

### 9.3 Timer de run et conditions de fin

- **Durée** : 900 s (15 min).
- **Affichage** : compte à **rebours** (15:00 → 0:00) — crée la tension et donne un objectif clair.
- **À 0:00** : extraction forcée, label "EXTRACTION REUSSIE" — fin de run victorieuse. Mêmes
  Échos calculés qu'une mort. Le joueur est récompensé différemment (carte de fin distincte) mais
  pas en Échos supplémentaires (pas de déséquilibre farming timer).
- **Mort** : label "MORT EN SERVICE".

### 9.4 Écran de fin de run

L'écran affiche dans l'ordre :
1. **Cause de fin** : "EXTRACTION REUSSIE" (victoire) ou "MORT EN SERVICE" (défaite), avec
   palette visuelle distincte (cyan pour victoire, rouge-rouille pour mort).
2. **Décompte animé** (style `sequential_countup`, ~0,8 s par composante) :
   - "Temps survécu" → +X Échos
   - "Ennemis éliminés" → +X Échos
   - "Noyaux récupérés" → +X Échos
   - "Bonus de run" → +10 Échos
   - → **TOTAL : X ÉCHOS**
3. **Deux boutons** : "Retour au Hub" / "Rejouer".

Objectif : rendre la fin de run satisfaisante même après une mort — le joueur voit sa progression
meta et comprend immédiatement pourquoi rejouer.

### 9.5 Améliorations permanentes du Hub (MVP : 7 améliorations)

Dépensées au Hub. Structure complète dans `data/meta_upgrades.json`.

| id | Nom | Stat modifiée | Niveaux | Coût niv. 1 | Bonus par niveau |
|---|---|---|---|---|---|
| `hp_boost` | Corps Renforcé | `MaxHp` | 4 | 80 Échos | +20 HP |
| `damage_boost` | Calibration Offensive | `DamageMultiplier` | 5 | 100 Échos | +10% dégâts |
| `speed_boost` | Servos Améliorés | `Speed` | 3 | 90 Échos | +15 px/s |
| `cooldown_reduction` | Synchronisation Aether | `CooldownReduction` | 3 | 120 Échos | -5% cooldown |
| `damage_reduction` | Blindage Composite | `DamageReduction` | 3 | 150 Échos | -5% dégâts reçus |
| `starting_xp` | Mémoire Résiduelle | `StartingXp`* | 1 | 200 Échos | Départ niveau 2 (27 XP) |
| `starting_weapon_alt` | Prototype de Terrain | `UnlockStartingWeapon`* | 1 | 300 Échos | Débloque Essaim de Drones comme arme de départ |

\* Champs non présents dans `PlayerStats` — gérés par `MetaProgressionSystem` (cf. notes
d'implémentation dans `data/meta_upgrades.json`).

**Principes de design :**
- Niveau 1 de toute amélioration atteignable en **1-2 runs moyennes** (80-300 Échos, run moyenne
  ~100-120 Échos).
- Aucune amélioration ne rend le jeu trivial seule : le scaling des ennemis compense sur les runs
  longues.
- `starting_xp` est l'"accélérateur de fun" : sauter la phase d'amorçage ennuyeuse des premières
  secondes, sans skiper l'apprentissage du jeu.
- `starting_weapon_alt` est l'objectif de moyen terme (2-3 runs) qui renforce la rejouabilité
  par la variété de build dès la sélection.

**Hardcaps meta cumulés (meta + passifs en run) :**
- `DamageReduction` : meta max -15% + Plaque Renforcée -20% = -35% (hardcap global 40% respecté).
- `CooldownReduction` : meta max -15% + Capaciteur -38% = -53% → hardcap 0,15 s/arme s'applique.
- `Speed` : meta max +45 px/s (245 px/s départ) + Servo-Moteurs +100 = 345 px/s (sous le plafond
  soft 380 px/s). ✓

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
cyborg-survivor/
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
  - `MetaProgressionSystem` AutoLoad — charge `data/meta_upgrades.json`, expose `AddEchoes`, `TryPurchase`, `ApplyMetaBonusesToStats`, `GetUnlockedStartingWeapons`, `GetStartingXp`
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

> Ajouté le 2026-06-20 par l'agent `game-designer`. Fichier runtime : `data/meta_upgrades.json`.

### 18.1 Flux des Échos en chiffres

```
Échos = floor(T / 20) + floor(K / 10) + (N × 5) + 10
  T = secondes survécues
  K = ennemis tués dans la run
  N = Noyaux d'Aether ramassés dans la run
```

Vitesse d'accumulation :
- ~6 Échos/min en début de run (peu d'ennemis, pas de Noyaux encore).
- ~12-15 Échos/min en milieu de run (essaims denses, Colosses lâchant des Noyaux).
- ~16-18 Échos/min en fin de run (spawn maximal, Noyaux périodiques).

Déblocage de l'arbre :
- Après run 1 (première mort) : ~20-40 Échos → peut acheter `hp_boost` niv. 1 (80 Échos) en 2 runs.
- Après 5 runs courtes (~300 Échos cumulés) : `hp_boost` max + `speed_boost` niv. 1.
- Après 10-15 runs variées (~1 200-1 800 Échos) : arbre complètement débloqué.

### 18.2 Courbe de coût de l'arbre complet

| Amélioration | Coût total (tous niveaux) |
|---|---|
| Corps Renforcé (×4) | 80 + 150 + 250 + 400 = **880 Échos** |
| Calibration Offensive (×5) | 100 + 180 + 300 + 450 + 650 = **1 680 Échos** |
| Servos Améliorés (×3) | 90 + 200 + 350 = **640 Échos** |
| Synchronisation Aether (×3) | 120 + 250 + 400 = **770 Échos** |
| Blindage Composite (×3) | 150 + 300 + 500 = **950 Échos** |
| Mémoire Résiduelle (×1) | **200 Échos** |
| Prototype de Terrain (×1) | **300 Échos** |
| **TOTAL ARBRE COMPLET** | **5 420 Échos** |

Durée estimée pour compléter l'arbre : 25-35 runs longues (ou 40-60 runs courtes). Correct pour
un roguelite MVP — pas trop court (valeur de rejouabilité) pas trop long (frustrant).

### 18.3 Noyaux d'Aether — spawn détaillé

- **Spawn périodique** : 1 Noyau toutes les 45 s → 20 Noyaux max sur 15 min si tous ramassés.
  En pratique : 12-18 ramassés (combat, déplacements).
- **Drop Colosse** : les Colosses spawnent dès 9:00. Sur les 6 dernières minutes à ~1 Colosse/3 min
  → ~2-4 drops supplémentaires.
- **Noyaux totaux accessibles en run de 15 min** : ~22-24. Ramassés réalistes : ~18-22.
- **Gain Échos uniquement via Noyaux** (15 min, 20 ramassés) : 20 × 5 = **100 Échos**.

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
      "starting_xp": 0,
      "starting_weapon_alt": 0
    }
  }
}
```

Chemin de sauvegarde : `user://save.json` (cf. §15 — `FileAccess` Godot).

`MetaProgressionSystem` (AutoLoad) est le seul composant qui lit et écrit cette structure. Il
expose :
- `int CurrentEchoes` (propriété, lecture seule depuis l'extérieur)
- `void AddEchoes(int amount)` — appelé par `RunEndScreen` à la fin de run
- `bool TryPurchase(string upgradeId)` — appelé par `HubScreen`
- `float GetStat(string statTarget)` — lu par `GameManager` au démarrage de chaque run pour
  initialiser `PlayerStats` avec les bonus permanents

### 18.5 Application des bonus meta à PlayerStats

À chaque début de run, `GameManager` appelle `MetaProgressionSystem` pour construire les
`PlayerStats` initiales :

```
PlayerStats.MaxHp            += meta.hp_boost_level × 20
PlayerStats.DamageMultiplier += meta.damage_boost_level × 0.10
PlayerStats.Speed            += meta.speed_boost_level × 15
PlayerStats.CooldownReduction+= meta.cooldown_reduction_level × 0.05
PlayerStats.DamageReduction  += meta.damage_reduction_level × 0.05
```

Si `starting_xp_level == 1` : `XpSystem.AddXp(27)` immédiatement au `_Ready()` du joueur
(avant tout `_PhysicsProcess`). [27 XP = seuil niveau 1→2 depuis la formule rééquilibrée 2026-06-24]

Si `starting_weapon_alt_level == 1` : `HubScreen` affiche un sélecteur avec `["impulse_cannon",
"drone_swarm"]`. L'arme choisie est passée à `InventorySystem` via `GameManager.StartingWeaponId`.

### 18.6 UX du Hub

L'écran Hub (post-MVP : boutique visuelle) pour le MVP est une liste verticale simple :
- Chaque ligne : nom de l'amélioration | niveau actuel / niveau max | coût niveau suivant | bouton
  "Acheter" (grisé si Échos insuffisants ou niveau max atteint).
- En haut : compteur d'Échos disponibles, mis à jour en temps réel après chaque achat.
- Pas d'arbre visuel au MVP — liste suffisante pour valider la mécanique.

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
