# BRIEF DA — ARENE "LE SANCTUAIRE EN RUINES" — PHASE 4
## Auteur : directeur-artistique — 2026-06-21

> Ce brief est la source de vérité pour les agents `developpeur` et `graphiste` sur les
> quatre chantiers d'amélioration de l'arène identifiés après les premiers tests joueur.
> Toute implémentation doit être validée par `directeur-artistique` avant merge.
> Références permanentes : `docs/GDD.md` §10 §12, `docs/STYLE_GUIDE.md`, `src/Systems/GroundRenderer.cs`.

---

## État actuel (baseline Phase 3b)

- Arène intérieure jouable : ~1216×656 px (1280 − 2×32 murs)
- `GroundRenderer` produit : 40×23 tiles de sol (5 variantes), murs H×42 / V×25, 4 débris_01
  + 3 débris_metal + 3 flaques rouille (ZIndex=-9) + 1 pilier tech + 2 colonnes collidables
- 2 `AetherGeyser` positionnés à (-300,-150) et (280,180)
- Bloom Godot actif (threshold 0.6, intensity 0.8, Additive)

**Problèmes remontés par le joueur :**
1. Background trop fouillé — lisibilité en combat dense insuffisante
2. Absence d'obstacles solides significatifs
3. Arène trop petite pour le gameplay survivor (essaims 200 ennemis)
4. Manque de "juiciness" — effets de lumière et particules

---

## Section 1 — Simplification du background

### Diagnostic

Le `GroundRenderer` actuel génère 5 variantes de tiles de sol distribuées selon :
- 60% `tile_floor_01` (ardoise sombre `#1A1A22`)
- 20% `tile_floor_02` (gris-pierre `#2D2A33`)
- 8% `tile_floor_crack` (fissure + halo Aether)
- 7% `tile_floor_rust` (tache rouille)
- 5% `tile_floor_debris` (fragments métalliques)

Avec 40×23 = 920 tiles, cela génère ~74 tiles fissures Aether et ~64 tiles rouille éparpillées
uniformément. Ce bruit visuel uniforme rivalise avec les sprites dynamiques (joueur, ennemis,
projectiles) en absence de zones de repos visuel.

### Décisions DA

**Règle de densité maximale (actée) :** dans toute zone de 256×256 px (8×8 tiles), les tiles
"chargées" (crack + rust + debris) ne doivent pas dépasser 3 au total. Le sol doit être
majoritairement monotone pour que les entités dynamiques ressortent.

**Nouvelle distribution des tiles de sol :**

| Tile | Pourcentage actuel | Pourcentage cible | Ratio |
|---|---|---|---|
| `tile_floor_01` (base sombre) | 60% | 72% | Augmente — zone de repos visuel |
| `tile_floor_02` (variation) | 20% | 18% | Légère baisse |
| `tile_floor_crack` (fissure Aether) | 8% | 5% | Réduit — éviter le bruit cyan |
| `tile_floor_rust` (tache rouille) | 7% | 4% | Réduit |
| `tile_floor_debris` (fragments) | 5% | 1% | Quasi-supprimé — trop de texture |

Cette distribution réduit les tiles "bruyantes" de 20% à 10%.

**Décors superposés (ZIndex=-8 à -9) — limites strictes :**

| Élément | Quantité actuelle | Quantité cible | Justification |
|---|---|---|---|
| Débris de pierre (`tile_debris_01`) | 4 | 2 | Trop de fragments, noient le sol |
| Débris metal (`tile_debris_metal`) | 3 | 2 | Idem |
| Flaques Rouille (`tile_rust_pool`) | 3 | 2 | 3 flaques 32×32 = bruit vert-bile diffus |
| Pilier tech (`tile_tech_pillar`) | 1 | 1 | Conserver — élément narratif fort |
| Colonnes collidables | 2 | → remplacées par les obstacles §2 | Visuellement insuffisantes |

**Règle de placement des décors superposés :** aucun décor ZIndex > -10 ne doit être placé
dans un rayon de 64 px du centre de l'arène (zone de spawn joueur). Zone de sécurité visuelle
absolue au démarrage d'une run.

**Critère de validation lisibilité (test obligatoire) :** le joueur, les ennemis et les
projectiles doivent être distincts du fond en moins de 150 ms sur n'importe quelle tile.
Méthode de test : screenshot en combat dense (≥50 ennemis) → flou gaussien 2 px appliqué
en post → les silhouettes des entités dynamiques doivent rester identifiables. Si un ennemi
se confond avec une tile, la tile est trop chargée.

**Instruction `developpeur` :** modifier `GroundRenderer.BuildFloor()` pour ajuster les
seuils `if (r < 0.XX)` selon la nouvelle distribution. Modifier `BuildDecor()` pour réduire
les boucles `for` selon le tableau ci-dessus.

---

## Section 2 — Obstacles solides (P0)

### Principes DA et GD

Le GDD §10 acte explicitement "Tilemap avec collisions (murs de ruines, débris infranchissables)".
L'absence d'obstacles intérieurs vide l'arène de toute structure tactique — les ennemis convergent
en ligne droite sans que le joueur puisse s'interposer, créer des goulots ou choisir une position
défensive. Ce point est P0 car il impacte directement la lisibilité du positionnement joueur et
la profondeur tactique.

**Règle de navigation absolue :** tout cluster d'obstacles doit laisser un couloir libre d'au
moins 80 px (2.5 tiles) sur tous les axes de passage. Un joueur à vitesse max (380 px/s) doit
pouvoir circuler sans se bloquer.

**Règle de lisibilité silhouette :** un obstacle doit lire "infranchissable" en moins de 300 ms
sans que le joueur ait besoin de le tester. Cela implique : hauteur visuelle supérieure au joueur
(sprite > 32 px), ombre portée ou effet de profondeur, et couleur clairement dans la famille
"matière morte" §1.1.

### Cinq types d'obstacles définis

---

#### Obstacle A — Pilier de Sanctuaire (P0)

**Lore :** colonne architecturale pré-Convergence, pierre taillée qui a absorbé de l'Aether
au fil des siècles. Les fissures révèlent une lueur intérieure.

**Brief graphiste :**
- Taille sprite : 32×64 px (2 tiles de haut — dépasse clairement la silhouette joueur 32 px)
- Vue du dessus : le haut du sprite est la partie "toit" de la colonne (circulaire ou octogonale,
  12×12 px), vu légèrement de dessus. Le bas du sprite est l'ombre portée ovale.
- Palette : pierre `#252028` base, détail `#3A2E35` joints de pierre, fissures `#00A0BB`
  (Aether abyssal légèrement haussé pour trigger le bloom — autorisation actée STYLE_GUIDE §7.3)
- Une fissure verticale de 1-2 px sur la face visible, dont l'intérieur est `#00A0BB`
- Ombre portée : ovale 28×8 px `#0A0A0F` à 50% alpha, ZIndex=-1 (sous la colonne, sur les ennemis)
- Nommage fichier : `tile_pillar_stone.png` (sprite principal 32×64) + `tile_pillar_stone_shadow.png`
  (ombre 32×8)
- **CollisionShape2D** : `CapsuleShape2D`, rayon 12 px, hauteur 0 px (cercle effectif) — centré
  sur la moitié basse du sprite (offset Y = +16 px depuis le centre du StaticBody2D)
- Placement recommandé : clusters de 2-3 colonnes en triangle isoèle, espacées de 80-128 px

---

#### Obstacle B — Épave de Machine (P0)

**Lore :** carcasse d'une machine de la pré-Convergence, mi-organique mi-mécanique, figée dans
la Rouille Vivante. Masse horizontale basse, infranchissable.

**Brief graphiste :**
- Taille sprite : 64×32 px (2 tiles de large × 1 tile de haut)
- Vue du dessus : forme irrégulière de ferraille aplatie. 3-4 volumes distincts qui se chevauchent
  (carter moteur 24×20 px + bras mécanique 20×8 px qui dépasse sur le côté + panneau tombé 16×24 px)
- Palette : métal `#4A4A52` base, rouille `#7A4A2A` sur les bords, corps organique noir-vert
  `#3A3A28` sur les zones infestées, noir `#0A0A0F` pour les zones les plus sombres
- Détail narratif : un câble `#4A4A52` qui sort d'un côté, se termine par un connecteur rouillé
- Nommage : `tile_wreck_machine.png` (64×32)
- **CollisionShape2D** : `RectangleShape2D(56, 24)` — légèrement plus petit que le sprite pour
  éviter les accrochages invisibles sur les pixels de rouille des bords
- Placement recommandé : isolé ou en paire (2 épaves à 90° formant un angle L), jamais 3 en ligne
  droite (créerait un mur infranchissable)

---

#### Obstacle C — Caisse Technologique Empilée (P1)

**Lore :** conteneurs de transport militaire pré-Convergence, empilés en hâte lors de l'évacuation.
Peuvent contenir des pickups si on les détruit (décor destructible, hors scope MVP — ajouter le
trigger post-MVP).

**Brief graphiste :**
- Taille sprite : 32×40 px (légèrement plus haut que 32 px pour la perspective)
- Vue du dessus légèrement inclinée : la caisse du dessus (24×20 px) + rebord de profondeur
  8 px en bas (fausse perspective)
- Palette caisse : métal peint effrité `#4A5566` (même famille que l'armure joueur — cohérence
  lore : matériel militaire d'origine commune). Rouille `#7A4A2A` sur les coins. Logo effacé
  (rectangle 8×4 px `#3A3A42`)
- Couleur distincte de l'armure joueur : ajouter une bande de signalisation `#4A2A15` (brun
  rouille) sur le côté — le joueur est `#4A5566` uniforme, la caisse est rayée
- Nommage : `tile_crate_tech.png` (32×40)
- **CollisionShape2D** : `RectangleShape2D(28, 28)` — la partie collidable est la moitié basse
  du sprite (offset Y = +6 px)
- Placement recommandé : clusters de 2-4 en L ou en U, jamais seule (trop petite pour être
  lisible comme obstacle tactique)
- MVP : indestructible. Post-MVP : 3 hits → drop pickup + QueueFree

---

#### Obstacle D — Arche Effondrée (P1)

**Lore :** reste d'une arcade architecturale du Sanctuaire. L'arche est tombée et bloque
partiellement le passage, créant un choix tactique : passer en dessous (si le sprite le suggère)
ou contourner.

**Brief graphiste :**
- Taille sprite : 96×32 px (3 tiles de large)
- Vue du dessus : deux piliers de pierre (24×32 px chacun aux extrémités) reliés par un linteau
  effondré (48×20 px, affaissé au centre, ZIndex > linteau = devant)
- Zone centrale (entre les piliers) : sol visible sous l'arche — ne pas le remplir, laisser
  voir le tile de sol en dessous. La zone centrale n'est PAS collidable (passage possible à
  travers l'arche mais sous le sprite du linteau)
- Palette : identique aux piliers de sanctuaire (`#252028`, `#3A2E35`) + mousses
  `#3A3A28` sur les joints
- Nommage : `tile_arch_fallen.png` (96×32)
- **CollisionShape2D** : deux `RectangleShape2D(20, 28)` pour les piliers uniquement — pas de
  collision centrale (passage libre sous l'arche)
- Placement recommandé : orientée horizontalement ou verticalement (le `developpeur` gère la
  rotation à 90° via `RotationDegrees`), jamais dans un couloir < 160 px de large

---

#### Obstacle E — Terminal Corrompu (P2)

**Lore :** borne d'accès à l'infrastructure pre-Convergence, désactivée et partiellement
digérée par la Rouille Vivante. Son écran mort émet parfois un flash parasite.

**Brief graphiste :**
- Taille sprite : 32×48 px
- Vue du dessus : socle carré (28×16 px) + écran incliné vers le joueur (20×24 px),
  écran éteint `#1A1A22` avec pixel mort `#0A0A0F`, encadrement `#4A4A52`
- Effet parasite (animation 2 frames, 0.8 s/frame) : pixel `#00A0BB` 2×2 au centre de l'écran
  qui apparaît/disparaît (bloom trigger)
- Corruption Rouille sur le socle : filaments `#3A3A28` qui remontent sur les bords
- Nommage : `tile_terminal_corrupt_01.png`, `tile_terminal_corrupt_02.png`
- **CollisionShape2D** : `RectangleShape2D(24, 16)` sur le socle uniquement
- Placement : isolé, jamais en cluster — c'est un point focal narratif, pas un obstacle mural

---

### Placement dans l'arène — règles générales

**Zones interdites aux obstacles :**
- Rayon 150 px autour du centre (0,0) — spawn joueur et zone de dégagement initiale
- Rayon 48 px autour de chaque `AetherGeyser` (positions: (-300,-150) et (280,180))
- Bande 80 px le long des murs intérieurs (les murs ont déjà leur propre densité visuelle)

**Distribution cible pour l'arène à taille Phase 4 (cf. §3) :**
- 4-6 Piliers de Sanctuaire (type A) — priorité P0
- 2-3 Épaves de Machine (type B) — priorité P0
- 4-6 Caisses Technologiques (type C) — priorité P1
- 1-2 Arches Effondrées (type D) — priorité P1
- 0-2 Terminaux Corrompus (type E) — priorité P2

**Instruction `developpeur` :** créer une méthode `BuildObstacles(RandomNumberGenerator rng)` dans
`GroundRenderer.cs`, appelée depuis `_Ready()` après `BuildDecor()`. Chaque obstacle est un
`StaticBody2D` avec `CollisionLayer=1`, `CollisionMask=1`, ZIndex=1. Les colonnes existantes
peuvent être migrées vers le type A. La méthode `PlaceColumn()` existante sert de modèle.

---

## Section 3 — Agrandissement de l'arène (P0)

### Diagnostic de la taille actuelle

L'arène intérieure actuelle fait 1216×656 px (1280−64 murs × 720−64 murs). À 200 ennemis actifs
(scaling t=10 min selon GDD §17), cela représente environ 1 ennemi par 4 px² — une densité
qui empêche tout mouvement tactique et noie visuellement les projectiles.

Les survivors de référence (Vampire Survivors) utilisent une arène infinie avec scroll.
L'approche "arène fermée" de Chimera Protocol est un différenciateur qui exige une surface
minimale de respiration, estimée à 3-4× la surface actuelle pour un gameplay fluide à 200 ennemis.

### Nouvelle taille d'arène

**Décision actée :** 60×38 tiles de 32 px = **1920×1216 px de zone intérieure jouable.**
Taille totale avec murs : (1920+64) × (1216+64) = **1984×1280 px.**

Justification du ratio 60×38 (vs 60×40 évoqué dans la demande) :
- 60×40 = 1920×1280 intérieur → taille totale 1984×1344 → hors des proportions 16:10 de l'écran
- 60×38 = 1920×1216 → ratio intérieur ≈ 1.58 (proche 16:10) → la camera centrée sur le joueur
  voit environ 1280×720 de jeu, cohérent avec la résolution cible du projet
- Densité à 200 ennemis : 1920×1216 / 200 ≈ 11.7 px² par ennemi — surface 3× supérieure
  à l'actuel, viable pour la lisibilité

### Impact sur `GroundRenderer`

Modifier `Constants.cs` :
```
ArenaWidth  = 1920   (était 1280)
ArenaHeight = 1216   (était 720)
```

Modifier `GroundRenderer.cs` :
```
GridCols = 60  (était 40 — 1920/32)
GridRows = 38  (était 23 — 1216/32)
```

Les murs H passent de 42 à 63 tiles (`(1920 + 64) / 32 = 62` → arrondir à 63).
Les murs V passent de 25 à 41 tiles (`(1216 + 64) / 32 = 40` → arrondir à 41).

Repositionner les `AetherGeyser` pour qu'ils restent hors de la zone centrale :
- Geyser 1 : de (-300,-150) à (-500,-250)
- Geyser 2 : de (280,180) à (480,300)

### Impact sur `Camera2D`

**Instruction `developpeur` :** la `Camera2D` attachée au joueur doit avoir des limites
(`limit_left`, `limit_right`, `limit_top`, `limit_bottom`) calées sur les bords intérieurs
de l'arène. Sans ces limites, le joueur pourra voir les murs s'effondrer visuellement hors
champ au scroll.

Valeurs cibles (centre arène = (0,0)) :
```
limit_left   = -960  (ArenaWidth / 2)
limit_right  = +960
limit_top    = -608  (ArenaHeight / 2)
limit_bottom = +608
```

Zoom `Camera2D` : conserver à 1.0. La résolution native 1280×720 du projet donne un viewport
qui voit 1280×720 px de monde — le joueur ne voit jamais toute l'arène d'un coup, ce qui
est l'objectif (exploration, tension des bords hors-écran).

### Impact sur `EnemySpawner`

L'`EnemySpawner` spawn actuellement aux bords intérieurs de l'arène. Avec la nouvelle taille,
les constantes de spawn margin doivent être mises à jour : lire `Constants.ArenaWidth/2` et
`Constants.ArenaHeight/2` plutôt que des valeurs hardcodées.

La **densité perçue** par le joueur ne change pas (il ne voit que 1280×720 à la fois) mais le
pool d'ennemis se répartit sur une surface plus grande, réduisant la sensation d'asphyxie.

**Vigilance `game-tester` :** vérifier que les ennemis de type `ranged_kiter` (Sentinelle)
et `slow_hunter` (Colosse) restent dans le champ visuel du joueur assez longtemps pour être
lisibles. Si la surface agrandie les rend trop rares, augmenter légèrement leur `spawnWeight`
dans `data/enemies.json`.

### Impact sur `GroundRenderer` — calcul tiles

```
Nombre de tiles sol : 60 × 38 = 2 280 tiles  (était 920)
Tiles H murs (haut + bas) : 63 × 2 = 126 tiles
Tiles V murs (gauche + droite) : 41 × 2 = 82 tiles
Total sprites instanciés : ~2 488 Sprite2D
```

**Performance :** 2 488 Sprite2D statiques instanciés en `_Ready()` — tous ZIndex négatifs, pas
de logique en `_Process()`. Le moteur les batch-draw efficacement (même texture = même draw call).
Avec 5 textures de sol et 3 de mur, on reste à ~8 draw calls pour la totalité du sol.
**Aucun risque de perf identifié** — les 200 ennemis dynamiques restent le goulet d'étranglement.

---

## Section 4 — Effets de lumière et particules ("juiciness")

### Principe directeur (rappel §12 GDD)

> "Priorité visuelle absolue : lisibilité en combat dense > esthétique."

Chaque effet de cette section est évalué sur ce critère. Les effets qui risquent de noyer
la lecture des projectiles, ennemis ou du joueur sont classés P2 ou refusés.

**Règle d'or des particules pour ce projet :** `amount` ≤ 15 par `GPUParticles2D` à l'écran
simultanément. Avec 200 ennemis pouvant mourir quasi-simultanément, un budget de 15 particules
par mort × 200 = 3 000 particules simultanées est inacceptable. Les particules de mort sont
donc limitées à 8 et durée de vie 0.4 s (elles sortent rapidement du pipeline).

---

### Effet 1 — Lueur Aether sur les geysers actifs (P0)

**Quoi :** `PointLight2D` Godot, émis depuis le centre du geyser.

**Pourquoi P0 :** les geysers infligent 5 HP/s — le joueur doit les voir de loin. Sans
indicateur lumineux, ils sont visuellement indistincts du sol pendant la phase inactive.

**Nœud Godot :** `PointLight2D`, enfant de `AetherGeyser.tscn`, ZIndex=0.

**Paramètres état inactif :**
- `enabled = true`
- `color = Color(0.0, 0.9, 1.0, 1.0)` (cyan Aether `#00E5FF` approx.)
- `energy = 0.4` (lueur résiduelle faible — toujours visible mais ne noie pas)
- `texture_scale = 1.2`
- Texture : utiliser `GradientTexture2D` circulaire radiale blanc → transparent, rayon 48 px
  (ou `PointLight2D` avec texture built-in Godot)

**Paramètres état actif (cycle 2 s) :**
- `energy` animé de 0.4 → 1.8 en 0.3 s via `Tween` dans `AetherGeyser.cs` à l'entrée
  de la phase active, puis retour à 0.4 en 0.5 s à la sortie
- `color` identique (pas de changement de teinte)

**Instruction `developpeur` :** ajouter le `PointLight2D` dans `AetherGeyser.tscn` + propriété
`_light` dans `AetherGeyser.cs`. Animer `energy` via `Tween` dans `SetActive(bool)`.

---

### Effet 2 — Particules ambiantes Aether dans l'arène (P1)

**Quoi :** `GPUParticles2D` stationnaire, émission continue, effet ambiant.

**Pourquoi P1 et non P0 :** purement esthétique. Le bloom déjà actif sur les tiles
`tile_wall_crack_aether.png` assure la présence Aether visible sans particules. Les particules
ajoutent du "vivant" mais ne sont pas nécessaires à la lisibilité.

**Contrainte absolue :** ces particules ne doivent PAS être présentes dans la zone centrale
de l'arène (rayon 300 px du centre) — elles noieraient la lecture des entités. Elles
flottent en bordure d'arène, près des murs.

**Nœuds Godot :** 4 `GPUParticles2D` placés dans `Game.tscn`, un par bord de l'arène,
à mi-longueur. Géré statiquement (pas de spawn via code).

**Paramètres :**
- `amount = 8` par node (32 particules total — budget acceptable)
- `lifetime = 4.0`
- `emission_shape = Box`, taille 800×200 px pour les bords horizontaux, 200×800 pour les verticaux
- Texture : `vfx_particle_aether_ambient.png` — point flou 3×3 px `#00A0BB`
  (Aether abyssal légèrement haussé — autorisé §7.3 pour le bloom)
- `direction = Vector2(0, -1)` (flottement vers le haut)
- `initial_velocity = 8.0`, `velocity_spread = 60°`
- `scale = 1.0`, `scale_random = 0.5`
- `color_ramp` : de `Color(0,0.63,0.73,0.6)` à `Color(0,0.63,0.73,0)` (fade out)
- `gravity = Vector2(0, -2)` (légère tendance ascendante)

**Brief graphiste :** produire `vfx_particle_aether_ambient.png` — carré flou 3×3 px `#00A0BB`.

---

### Effet 3 — Impact de projectile (P1)

**Quoi :** `GPUParticles2D` déclenché à la position d'impact, émission one-shot.

**Pourquoi P1 :** améliore le "feel" du tir mais le gameplay est lisible sans lui (les ennemis
ont le hit-flash de §5.1 déjà acté).

**Nœuds Godot :** `GPUParticles2D` enfant de `Bullet.tscn` et `EnemyBullet.tscn`. Déclenché
dans `OnBodyEntered()` ou `OnAreaEntered()` juste avant `QueueFree()`.

**Paramètres (projectile joueur — Canon à Impulsions) :**
- `amount = 6`
- `lifetime = 0.25`
- `one_shot = true`, `emitting = false` (déclenché via code)
- Texture : `vfx_particle_impact_plasma.png` — carré 2×2 px `#FFD700` (jaune solaire)
- `initial_velocity = 120.0`, `velocity_spread = 180°` (burst radial)
- `color_ramp` : jaune → orange `#FF8800` → transparent en 0.25 s
- `gravity = Vector2(0, 0)`

**Paramètres (projectile Sentinelle) :**
- Identiques mais texture `vfx_particle_impact_sentinel.png` — carré 2×2 px `#FF6644`
- `initial_velocity = 80.0` (moins violent)

**Instruction `developpeur` :** dans `Bullet.cs`, stocker référence `GPUParticles2D _impactParticles`.
Dans `OnBodyEntered`, appeler `_impactParticles.Emitting = true` puis déplacer le node
hors de la hiérarchie (`Reparent(GetTree().Root)`) avant `QueueFree()` pour laisser les
particules terminer leur lifetime. Alternative plus simple : créer un `PackedScene`
`ImpactBurst.tscn` instancié à la position d'impact, auto-détruit via `Timer`.

**Brief graphiste :** produire `vfx_particle_impact_plasma.png` (2×2 px `#FFD700`) et
`vfx_particle_impact_sentinel.png` (2×2 px `#FF6644`).

---

### Effet 4 — Mort d'ennemi : burst de particules + flash (P0)

**Statut actuel :** les textures de particules sont déjà actées dans `STYLE_GUIDE.md` §5.2
(`vfx_particle_rustswarm.png`, `vfx_particle_drone.png`, etc.). Ce point concerne
l'**implémentation** dans `EnemyBase.Die()`, pas la création des sprites.

**Flash blanc à la mort (P0) :** déjà prévu en §5.1 via `modulate`. S'assurer que le hit-flash
joue une dernière fois lors du `Die()` — durée légèrement plus longue (0.1 s vs 0.05 s pour
un hit normal) pour marquer la mort de manière plus satisfaisante.

**GPUParticles2D à la mort (P0) :**
- `amount = 8`
- `lifetime = 0.4`
- `one_shot = true`
- `initial_velocity = 80.0`, `velocity_spread = 360°`
- Instancier via `PackedScene` pour éviter d'avoir un `GPUParticles2D` permanent sur chaque
  ennemi (×200 = overhead mémoire inacceptable). Créer `vfx_enemy_death_burst.tscn` avec
  paramètres modifiables (`texture` et `color` settables via code selon le type d'ennemi).

**Instruction `developpeur` :** dans `EnemyBase.Die()`, après le flash blanc, instancier
`vfx_enemy_death_burst.tscn` via `CallDeferred`, set la texture selon le type d'ennemi,
`emitting = true`. Le node se détruit automatiquement via un `Timer = lifetime + 0.1`.

---

### Effet 5 — Collecte XP : trail vers le joueur (P1)

**Quoi :** quand une orbe XP entre dans la zone d'aspiration (80 px) et se dirige vers
le joueur, un trail de particules suit son déplacement.

**Nœud Godot :** `GPUParticles2D` enfant de `XpOrb.tscn`, actif uniquement pendant la
phase d'aspiration.

**Paramètres :**
- `amount = 4`
- `lifetime = 0.15`
- `direction = Vector2(0, 0)`, `initial_velocity = 0` (les particules restent sur place)
- Texture : réutiliser `vfx_particle_xp.png` déjà acté §5.5
- `color_ramp` : `#AAFF44` → transparent en 0.15 s
- Activer `emitting = true` dans `XpOrb.cs` quand `_isMagneted = true`, désactiver sinon

**Note :** ce trail ne doit pas être visible quand les orbes ne bougent pas — activer/désactiver
dynamiquement.

---

### Effet 6 — XP Orb pulse (P1)

**Quoi :** l'orbe XP pulse visuellement pour attirer l'œil du joueur.

**Décision DA :** implémenter via `AnimationPlayer` sur `modulate.a` plutôt que shader ou
particules supplémentaires — coût CPU quasi nul, déjà supporté par Godot.

**Paramètres :**
- `modulate.a` oscille entre 0.7 et 1.0 sur un cycle de 0.6 s, boucle infinie
- Courbe : `EaseInOut` (pulse naturel, pas mécanique)
- Activer uniquement quand l'orbe est à portée > rayon d'aspiration (hors zone magnétique)
  — une fois magnétée, la pulse cesse et le trail prend le relais

**Instruction `developpeur` :** ajouter un `AnimationPlayer` dans `XpOrb.tscn` avec l'animation
"pulse" décrite ci-dessus. Le déclencher dans `_Ready()`, le stopper dans la condition
`_isMagneted = true`.

---

### Tableau de priorité consolidé

| # | Effet | Nœud Godot | Priorité | Impact lisibilité | Responsable |
|---|---|---|---|---|---|
| 1 | Lueur geyser actif (`PointLight2D`) | `AetherGeyser.tscn` | **P0** | Positif (danger visible) | `developpeur` |
| 4 | Mort ennemi burst + flash | `vfx_enemy_death_burst.tscn` | **P0** | Neutre (momentané) | `developpeur` |
| 3 | Impact projectile burst | `vfx_impact_burst.tscn` | P1 | Neutre (0.25 s) | `developpeur` |
| 5 | XP trail pendant aspiration | `XpOrb.tscn` | P1 | Neutre | `developpeur` |
| 6 | XP orb pulse (`AnimationPlayer`) | `XpOrb.tscn` | P1 | Légèrement positif | `developpeur` |
| 2 | Particules ambiantes Aether | `Game.tscn` (4 nodes) | P1 | Attention : garder hors zone centrale | `developpeur` |
| — | Chromatic aberration hits violents | Shader `CanvasItemMaterial` | **P2** | Risque négatif acté §12 | Post-MVP |
| — | Vignette bords d'écran | Shader ou `ColorRect` | P2 | Neutre acté §12 | Post-MVP |

---

## Récapitulatif des livrables par agent

### `graphiste` — Nouveaux sprites à produire (Phase 4)

| Fichier | Taille | Couleur | Priorité |
|---|---|---|---|
| `tile_pillar_stone.png` | 32×64 | `#252028` + fissures `#00A0BB` | P0 |
| `tile_pillar_stone_shadow.png` | 32×8 | `#0A0A0F` 50% alpha | P0 |
| `tile_wreck_machine.png` | 64×32 | `#4A4A52` + `#7A4A2A` + `#3A3A28` | P0 |
| `tile_crate_tech.png` | 32×40 | `#4A5566` + bande `#4A2A15` | P1 |
| `tile_arch_fallen.png` | 96×32 | `#252028` + `#3A3A28` | P1 |
| `tile_terminal_corrupt_01.png` | 32×48 | `#4A4A52` + écran `#1A1A22` | P2 |
| `tile_terminal_corrupt_02.png` | 32×48 | Idem + pixel `#00A0BB` | P2 |
| `vfx_particle_aether_ambient.png` | 3×3 | `#00A0BB` | P1 |
| `vfx_particle_impact_plasma.png` | 2×2 | `#FFD700` | P1 |
| `vfx_particle_impact_sentinel.png` | 2×2 | `#FF6644` | P1 |

Les sprites `vfx_particle_rustswarm.png`, `vfx_particle_drone.png`, `vfx_particle_sentinel.png`,
`vfx_particle_colossus.png` et `vfx_particle_xp.png` sont déjà actés dans `STYLE_GUIDE.md` §5
et doivent être livrés en même temps que les nouvelles textures d'impact.

### `developpeur` — Tâches d'implémentation

**P0 :**
1. `Constants.cs` : `ArenaWidth = 1920`, `ArenaHeight = 1216`
2. `GroundRenderer.cs` : `GridCols = 60`, `GridRows = 38`, distribution tiles §1, décors §1,
   méthode `BuildObstacles()` avec types A (pilier) et B (épave)
3. `AetherGeyser.cs` + `.tscn` : ajouter `PointLight2D`, animer `energy` via Tween
4. `EnemyBase.Die()` : instancier `vfx_enemy_death_burst.tscn` via CallDeferred, flash mort
5. `Camera2D` dans `Game.tscn` : ajouter limites `limit_*` selon nouvelles dimensions
6. Repositionner les geysers : (-500,-250) et (480,300)

**P1 :**
7. `GroundRenderer.cs` : méthode `BuildObstacles()` avec types C (caisse) et D (arche)
8. `Bullet.cs` + `EnemyBullet.cs` : instancier burst d'impact
9. `XpOrb.tscn/cs` : `GPUParticles2D` trail + `AnimationPlayer` pulse
10. `Game.tscn` : 4 `GPUParticles2D` ambiance Aether bords d'arène

**P2 :**
11. `GroundRenderer.cs` : type E (terminal)
12. Shaders post-processing (chromatic aberration, vignette) — après tests joueurs

---

## Notes de cohérence DA — Familles visuelles

Les obstacles introduits en Phase 4 appartiennent tous à la famille "matière morte" (§0 STYLE_GUIDE).
Aucun ne doit utiliser les couleurs Aether (§1.2) sauf exception explicitement justifiée
(fissures du Pilier de Sanctuaire — autorisée et documentée ci-dessus).

La présence de `tile_crate_tech.png` dans la palette `#4A5566` (même famille que l'armure joueur)
est volontaire et lore-cohérente (matériel militaire partagé). La distinction joueur/obstacle
est assurée par la bande `#4A2A15` et l'absence d'implants lumineux cyan.

Les terminaux corrompus (type E) avec leur pixel `#00A0BB` sont l'unique point de contact
entre "matière morte" et "énergie vivante" parmi les obstacles — ils symbolisent des machines
à demi-ressuscitées par l'Aether ambiant du Sanctuaire. Ce choix est validé par le lore
(GDD §3 : "énergie Aether qui s'infiltre dans les structures technologiques").
