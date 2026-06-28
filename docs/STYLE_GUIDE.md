# STYLE GUIDE — CHIMERA PROTOCOL
## Direction artistique opérationnelle — Phase 3 (sprites, animations, VFX, UI)

> Document produit par l'agent `directeur-artistique` le 2026-06-20.
> Source de vérité visuelle pour l'agent `graphiste`. Toute décision prise ici est en
> cohérence avec `docs/GDD.md` §3, §4, §7, §8, §10, §12 et `CLAUDE.md` (conventions sprites).
> Ne rien commencer sans avoir lu ce guide en entier.

---

## 0. Principe directeur : "Matiere morte / Energie vivante"

Tout element visuel du jeu appartient a l'une de ces deux familles — aucune exception.

**Matiere morte** : le monde de surface apres la Convergence. Rouille, pierre effritee,
metal oxyde, beton craquale. Couleurs basses en saturation, valeurs moyennes a sombres.
Le desature est la norme. Tout ce qui ne pulse pas appartient a cette famille.

**Energie vivante** : l'Aether et tout ce qui en est contamine ou alimente. Cyborgs,
projectiles, orbes, VFX de sorts. Couleurs saturees, luminosite haute, bloom actif.
Un element Aether doit sembler emettre sa propre lumiere meme sur un ecran sans post-process.

**Regle de contraste operationnelle** : a tout moment sur l'ecran de jeu, les elements
"energie vivante" (joueur, projectiles, orbes XP, Noyaux, VFX) doivent se lire en moins
de 150 ms contre le fond "matiere morte" (sol, murs, debris). Si ce n'est pas le cas,
le livrable est refuse.

---

## 1. Palette chromatique complete (32 couleurs maximum)

### 1.1 Couleurs de base — Matiere morte (fond, arene, debris)

| Role | Nom | Hex | Usage |
|---|---|---|---|
| Sol sombre | Gris-ardoise profond | `#1A1A22` | Fond de tuile sol principal |
| Sol mid | Gris-pierre rouille | `#2D2A33` | Variation sol, joints de tuiles |
| Sol clair | Pierre chaude | `#3D3840` | Eclairage directionnel faible, decor |
| Mur principal | Beton oxide | `#252028` | Mur de ruines, base |
| Mur detail | Brique ancienne | `#3A2E35` | Morceaux de mur, variation |
| Rouille claire | Ocre oxide | `#7A4A2A` | Detail rouille sur metal |
| Rouille sombre | Brun fer | `#4A2A15` | Ombre rouille, salissures |
| Metal gris | Acier mort | `#4A4A52` | Structures metalliques desactivees |
| Metal sombre | Fonte froide | `#2A2A32` | Ombre metal |
| Debris organique | Vert bile | `#3A3A28` | Zones infestees par la Rouille Vivante |

### 1.2 Couleurs Aether — Energie vivante (joueur, XP, VFX)

| Role | Nom | Hex | Usage |
|---|---|---|---|
| Aether primaire | Cyan electrique | `#00E5FF` | Couleur signature Aether. Yeux joueur, orbes XP aura, VFX fusion principale |
| Aether secondaire | Cyan pale | `#80F0FF` | Reflet, highlight sur elements Aether |
| Aether sombre | Cyan abyssal | `#007A99` | Ombre des elements Aether, interieur des orbes |
| Aether chaud | Magenta pulse | `#FF00CC` | Fusion evoluee (Lame a Fusion), paire complementaire au cyan |
| Aether magenta pale | Rose neon | `#FF80E8` | Highlight fusion magenta |
| Energie XP | Jaune-vert vif | `#AAFF44` | Orbes XP (couleur placeholder actee en GDD §9.1) |
| Energie XP glow | Jaune-vert pale | `#CCFF88` | Halo orbe XP, centre lumineux |
| Noyau Aether | Violet profond | `#AA44FF` | Noyau d'Aether (couleur placeholder actee en GDD §9.1) |
| Noyau glow | Violet pale | `#CC88FF` | Halo Noyau, eclat de collecte |
| Plasma chaud | Orange electrique | `#FF8800` | Projectile Canon a Impulsions centre |
| Plasma bord | Jaune solaire | `#FFD700` | Bord projectile Canon, tracer |
| Surcharge | Rouge sang | `#FF2200` | Projectile Sentinelle Corrompue |
| Surcharge pale | Rouge vif | `#FF6644` | Bord projectile ennemi |

### 1.3 Couleurs joueur — Cyborg

| Role | Nom | Hex | Usage |
|---|---|---|---|
| Corps principal | Gris-bleu metal | `#4A5566` | Armure principale du Cyborg |
| Corps highlight | Acier clair | `#7A8899` | Reflets sur armure, bords exposes |
| Corps ombre | Bleu-anthracite | `#2A3344` | Zones d'ombre, interieur armure |
| Peau/Organique | Chair brulee | `#8A6655` | Zone organique residuelle (visage, bras partiellement expose) |
| Implants actifs | Cyan electrique | `#00E5FF` | Yeux, lignes d'implants sous-dermiques |

### 1.4 Couleurs UI

| Role | Nom | Hex | Usage |
|---|---|---|---|
| Fond HUD | Noir translucide | `#0A0A0F` + alpha 80% | Fond barres HP/XP, panneaux |
| Barre HP pleine | Vert Aether | `#22CC66` | HP > 50% |
| Barre HP critique | Rouge alarme | `#FF3322` | HP < 25% |
| Barre HP fond | Gris fonce | `#1A1A22` | Fond de la barre HP |
| Barre XP | Jaune-vert XP | `#AAFF44` | Rempli de la barre XP |
| Barre XP fond | Vert sombre | `#1A3308` | Fond de la barre XP |
| Texte principal | Blanc casse | `#E8E4E0` | Labels, valeurs numeriques |
| Texte secondaire | Gris chaud | `#AA9988` | Descriptions, labels secondaires |
| Rarity Commun | Gris | `#AAAAAA` | Border carte level-up commun (acte GDD §6) |
| Rarity Rare | Bleu | `#44AAFF` | Border carte level-up rare (acte GDD §6) |
| Rarity Epique | Violet | `#CC44FF` | Border carte level-up epique (acte GDD §6) |
| Victoire | Cyan | `#00E5FF` | "EXTRACTION REUSSIE" (acte GDD §9.3) |
| Defaite | Rouge-rouille | `#CC3311` | "MORT EN SERVICE" (acte GDD §9.3) |

### 1.5 Regle d'application "matiere morte / energie vivante"

1. Le fond de l'arene (sol + murs) n'utilise JAMAIS de couleur de la famille Aether (§1.2).
   Exception unique : les fissures dans les murs d'ou filtre de l'Aether ambiant, representees
   par un trait `#007A99` (Aether abyssal, intensite basse). Une ou deux par tuile de mur maximum.

2. Tout element qui appartient au joueur ou qui est benefique (XP, Noyaux, projectiles joueur)
   utilise les familles Aether §1.2 ou Joueur §1.3. Jamais de rouge ou de rouille.

3. Les ennemis sont intentionnellement positionnes dans la famille Matiere morte (§1.1),
   corrompus par la Rouille Vivante. Leurs seuls emprunts a la famille Aether se limitent
   a leurs yeux ou capteurs (corruption Aether qui les anime) : un seul pixel rouge `#FF2200`
   ou orange `#FF8800` par ennemi suffira a indiquer l'activation corrompue.

4. Le bloom Godot (`WorldEnvironment.glow`) est active sur toutes les couleurs de la famille
   §1.2. Le threshold de glow doit laisser les couleurs §1.1 non-affectees. Regler
   `glow_hdr_threshold` de facon a ce que les valeurs < 0.6 de luminosite ne blooment pas.

---

## 2. Personnage jouable — Le Cyborg (Arpenteur)

### 2.1 Silhouette de base

Format : 32×32 px, fond transparent `#00000000`.
Le personnage doit occuper une zone utile de 20×28 px au centre du canvas (laisser 6 px de
marge basse pour l'ombre portee et 6 px de marge haute pour la barre de vie eventuelle).

Proportions en vue du dessus (demi-profil isometrique leger) :
- Tete/casque : 8×8 px, au tiers superieur du canvas
- Torse/armure : 10×10 px, centre du canvas
- Jambes : 8×6 px, tiers inferieur
- Bras droit (arme) : sort legerement a droite, 4 px de large

Le Cyborg doit etre lisible a cette resolution comme "humanoide en armure lourde avec
des implants lumineux" — la silhouette asymetrique (bras arme plus developpe) permet
de distinguer son orientation sans indication directionnelle supplementaire.

### 2.2 Palette dediee Cyborg

Utiliser exclusivement les couleurs §1.3 + noir pur `#000000` pour les contours.

- Contour : `#000000` (1 px, contour externe uniquement — pas de contour interne)
- Corps : `#4A5566` (masse principale)
- Highlight : `#7A8899` (bord superieur eclaire des volumes — lumiere venue du haut)
- Ombre : `#2A3344` (cote inferieur, dessous des volumes)
- Organique : `#8A6655` (visible sur le visage : 4-6 px de peau entre casque et col)
- Implants : `#00E5FF` (yeux, 2×1 px chacun ; lignes d'implants, 1 px de large)

### 2.3 Animations requises

Toutes en spritesheets horizontales, 32×32 px par frame.
Nommage : `player_idle_NN.png`, `player_run_NN.png`, `player_death_NN.png`.

| Animation | Frames | Duree frame | Boucle | Notes |
|---|---|---|---|---|
| `idle` | 4 | 0,15 s | Oui | Respiration subtile (corps monte de 1 px frames 2-3), implants qui pulsent (cyan clignote) |
| `run` | 6 | 0,08 s | Oui | Le jeu utilise 8 directions de deplacement. Produire 2 orientations : face droite et face bas-droite. Godot inverse automatiquement pour les directions miroir via `flip_h`. Pas besoin de 8 orientations. |
| `death` | 8 | 0,1 s | Non | Chute progressive : frame 1-3 recul, frame 4-5 genou a terre, frame 6-8 dissolution de l'implant (cyan qui s'eteint pixel par pixel) |
| `hit_flash` | 2 | 0,05 s | Non | Silhouette enticement blanche frame 1, retour normal frame 2. Implementable via shader plutot que sprite dedie. |

**Indicateur HP critique** : sous 25% de HP, les implants cyan `#00E5FF` clignotent avec
un cycle 0,4 s ON / 0,2 s OFF. Implementer via `AnimationPlayer` sur le `modulate` du
noeud `AnimatedSprite2D` — pas de sprite dedie supplementaire requis.

### 2.4 Orientation en run (vue du dessus)

Le jeu est en vue du dessus stricte (pas isometrique). Le personnage est vu legerement
de dessus, avec une legere inclinaison vers le bas (3/4 vue du dessus). Cette convention
permet de lire la "face" du personnage tout en etant coherent avec les deplacements 8-dir.

Pour le `run`, le graphiste produit 2 orientations :
- `player_run_right_NN.png` : deplacement vers la droite (frame 01 a 06)
- `player_run_down_NN.png` : deplacement vers le bas (frame 01 a 06)

Le code Godot gere les 6 autres directions par `flip_h` (gauche = droite inverse),
rotation de sprite (haut = down inverse vertical, diagonales = interpolation ou choix
de l'orientation la plus proche). Confirmer la convention avec `developpeur` avant
de produire les sprites de run.

---

## 3. Ennemis (4 types — MVP)

Tous les ennemis partagent :
- Format 32×32 px, fond transparent
- Contour `#000000` 1 px externe
- Teintes de base dans la famille §1.1 (matiere morte / rouille)
- Un element "corruption active" unique : 1-2 pixels de la famille §1.2 pour les yeux
  ou capteurs (anime : il clignote en idle)

### 3.1 Essaim de Rouille

**Lore** : debris technologiques minuscules animes par la Rouille Vivante, fondus en une
masse grouillante. Rapide, fragile, nombreux.

**Silhouette** : non-humanoide. Masse arrondie basse (12 px de haut, 18 px de large),
comme un tas de ferraille rampant. Pas de membres distincts — des prolongements
irreguliers sur les cotes evoquent des pattes ou des filaments metalliques. La forme
doit lire "nuisible et nombreux" : petite, agressive, organique-mecanique.

**Palette** :
- Corps : `#4A2A15` (brun fer) + `#7A4A2A` (rouille claire)
- Detail organique : `#3A3A28` (vert bile, plaque infestee)
- Yeux (corruption) : 2 px `#FF2200` (rouge sang, clignotant)
- Highlight metal : `#8A6A4A` (bord superieur, 1 px)

**Animations** :

| Animation | Frames | Duree frame | Notes |
|---|---|---|---|
| `idle` | 3 | 0,2 s | Vibration sur place (masse oscille de 1 px left/right) |
| `move` | 4 | 0,12 s | Deplacement rapide : masse s'etire dans la direction puis se retracte. Les "pattes" se decalent alternativement |
| `death` | 5 | 0,1 s | Eclatement : frame 1 gonflement, frames 2-4 pixels qui se dispersent, frame 5 vide |

Nommage : `enemy_rustswarm_idle_NN.png`, `enemy_rustswarm_move_NN.png`, `enemy_rustswarm_death_NN.png`.

### 3.2 Drone Corrompu

**Lore** : drone de surveillance pre-Convergence, reprogramme par la Rouille Vivante.
Se deplace de maniere erratique. Tres rapide, tres fragile.

**Silhouette** : forme losange horizontal (18 px large, 12 px haut) avec 2 "ailes" laterales
fines (2 px chacune). Un oeil central unique de 4×4 px. La forme rappelle un quadcopter
corrompu. Cet ennemi doit se distinguer de l'Essaim par sa forme geometrique reguliere
vs la masse organique de l'Essaim.

**Palette** :
- Corps : `#2A2A32` (fonte froide) + `#4A4A52` (acier mort)
- Rouille : `#7A4A2A` (plaques oxydees sur les bords)
- Oeil (corruption) : `#FF2200` centre + `#FF6644` halo, 4×4 px, anime
- Ailes detail : `#3A3A42` (metal plus leger)

**Animations** :

| Animation | Frames | Duree frame | Notes |
|---|---|---|---|
| `idle` | 3 | 0,18 s | Rotation lente sur place (oeil tourne de 1 px de position), ailes oscillent de 1 px haut/bas |
| `move` | 4 | 0,09 s | Inclinaison dans la direction du deplacement (1-2 px de decalage du corps), vitesse visuelle elevee |
| `death` | 4 | 0,1 s | Oeil qui s'eteint (rouge → gris → noir sur 2 frames), puis explosion en 8 debris pixelises |

Nommage : `enemy_drone_idle_NN.png`, `enemy_drone_move_NN.png`, `enemy_drone_death_NN.png`.

### 3.3 Sentinelle Corrompue

**Lore** : automate de securite quadrupede transforme par la Rouille Vivante. Se maintient
a distance et tire des projectiles. Plus lente mais plus robuste.

**Silhouette** : corps central en boite (14×14 px) sur 4 pattes rigides (2 px chacune,
5 px de long). Hauteur totale ~22 px, largeur ~22 px. Un canon emerge du cote droit
du corps (6×3 px). La forme quadrupede la distingue clairement de l'Essaim (amorphe)
et du Drone (volant) dans les combats denses. Le canon doit pointer dans la direction
du joueur — Godot gerera la rotation du sprite via `look_at()`.

**Palette** :
- Corps : `#3A2E35` (brique ancienne) + `#252028` (beton oxide)
- Pattes : `#4A4A52` (acier mort)
- Canon : `#2A2A32` (fonte froide), avec `#FF6644` (rouge vif) sur l'embout quand
  l'attaque est chargee (frame 3-4 de l'animation attack)
- Capteurs (2 yeux) : `#FF2200` (2×2 px chacun)
- Rouille d'usure : `#7A4A2A` plaquee sur les pattes et bords du corps

**Animations** :

| Animation | Frames | Duree frame | Notes |
|---|---|---|---|
| `idle` | 4 | 0,2 s | Corps qui se balance legerement (1 px), pattes ajustement de position mineur |
| `move` | 6 | 0,12 s | Marche quadrupede : paires de pattes alternees (paire avant puis paire arriere) |
| `attack` | 4 | 0,08 s | Frame 1-2 : accumulation d'energie sur le canon (pixel rouge qui grossit), frame 3 : tir (flash), frame 4 : recul 1 px |
| `death` | 6 | 0,1 s | Pattes qui s'effondrent une par une (frames 1-4), corps qui chute (frame 5), dissolution (frame 6) |

Nommage : `enemy_sentinel_idle_NN.png`, `enemy_sentinel_move_NN.png`, `enemy_sentinel_attack_NN.png`, `enemy_sentinel_death_NN.png`.

### 3.4 Colosse Greffe

**Lore** : ancien etre humain ou robot massivement augmente par la Rouille Vivante,
devenu une plateforme d'assaut lente et devastatrice. Lache un Noyau d'Aether a sa mort
(GDD §9.1) — le seul ennemi a le faire, ce qui doit etre visible lors de la mort.

**Silhouette** : humanoide desequilibre, fortement asymetrique. Bras droit : bras organique
(4 px de large). Bras gauche : pince mecanique enorme (10 px de large, 14 px de long).
Corps massif (16×20 px). Tete petite et ecrasee (6×6 px, presque enfouie dans les epaules).
Hauteur totale ~28 px — il doit dominer visuellement les autres ennemis dans la meme frame.

La taille superieure a 24 px impose un choix : soit le sprite tient dans 32×32 avec des
marges reduites (recommande), soit on utilise 48×48 px uniquement pour le Colosse
(voir §8 specifications techniques — exception justifiee par la lisibilite).
Decision : **48×48 px pour le Colosse uniquement**. Godot supporte des tailles de sprites
mixtes sans contrainte de pipeline.

**Palette** :
- Corps/chair : `#3A2A20` (chair necrosee, tres sombre)
- Armure grefee : `#4A4A52` (metal) + `#7A4A2A` (rouille)
- Pince mecanique : `#2A2A32` (fonte) + `#4A4A52` (detail) + `#7A4A2A` (usure)
- Implants internes visibles (fissures dans la chair) : `#AA44FF` (violet Noyau)
  — unique ennemi a exhiber la couleur Noyau dans son corps, indice visuel de lore :
  la Rouille Vivante a digere des Noyaux pour l'animer.
- Yeux : `#FF2200` (4×2 px, plus grands que les autres ennemis)
- Ombre de masse : `#1A1010` (contour interieur sur les zones tres profondes)

**Animations** (48×48 px) :

| Animation | Frames | Duree frame | Notes |
|---|---|---|---|
| `idle` | 4 | 0,25 s | Respiration lente (corps monte de 2 px), pince qui s'ouvre/ferme lentement |
| `move` | 6 | 0,15 s | Demarche lourde et irreguliere : fort impact sur les frames 1 et 4 (corps s'abaisse de 2 px) |
| `attack` | 5 | 0,1 s | Frames 1-3 : leve-bras de la pince (monte de 4 px), frame 4 : impact (flash blanc de contour), frame 5 : retrait |
| `death` | 10 | 0,1 s | Chute en 5 frames (genoux, puis corps), puis 4 frames de dissolution avec release de Noyau (flash violet `#AA44FF` sur frame 7, pixel violet qui "sort" du corps et reste au sol) |

Nommage : `enemy_colossus_idle_NN.png`, etc. (prefixe `enemy_colossus_`).

---

## 4. Armes et projectiles

### 4.1 Canon a Impulsions — Projectile

Vitesse : 400 px/s. Format projectile : 8×4 px (horizontal), oriente dans la direction du tir.
Godot gere la rotation via `look_at()` sur la velocity.

Structure visuelle du projectile (horizontal, 8 px de large) :
- Pixel 1 (pointe) : `#FFFFFF` (blanc pur, 1 px de large)
- Pixels 2-4 (noyau) : `#FFD700` (jaune solaire)
- Pixels 5-7 (tracer) : `#FF8800` (orange electrique)
- Pixel 8 (queue) : `#FF440000` a `#FF440000` (fade vers transparent, 1 px orange pale)

Pas d'animation de frames pour le projectile de base — la forme en fusee et le tracer
en degrade suffisent a communiquer la vitesse et la direction. Produire 1 fichier statique :
`weapon_bullet_impulse.png`.

Effet de muzzle flash (optionnel MVP, recommande) : `weapon_bullet_impulse_muzzle_NN.png`,
3 frames, 8×8 px, flash `#FFD700` → `#FF8800` → transparent.

### 4.2 Projectile Sentinelle Corrompue

Vitesse : 180 px/s. Format : 6×6 px, circulaire.

Structure (cercle de 6 px) :
- Centre 2×2 px : `#FFFFFF` (blanc, point chaud)
- Anneau interieur 1 px : `#FF2200` (rouge sang)
- Anneau exterieur 1 px : `#FF6644` (rouge vif, bord)
- Fond : transparent

Animation de pulsation (2 frames, 0,15 s par frame) : le blanc central alterne entre
2×2 px et 1×1 px pour suggerer une pulsation d'energie corrompue.
Fichiers : `enemy_bullet_sentinel_01.png`, `enemy_bullet_sentinel_02.png`.

### 4.3 Lame Plasma — Zone d'arc

La Lame Plasma n'est pas un projectile — c'est un arc de mêlee. En jeu : `Line2D` ou
zone `CollisionShape2D` orbitant autour du joueur. Le VFX visuel est un arc semi-circulaire
qui apparait pendant la duree du swing (cooldown).

Format VFX : animation 32×32 px centree sur le joueur (l'arc s'etend jusqu'au bord du canvas).
Couleurs : `#00E5FF` (cyan electrique) → `#007A99` (cyan abyssal) en fondu de l'interieur
vers l'exterieur. Bord exterieur `#FFFFFF` (1 px blanc).

Fichiers : `weapon_plasmablade_swing_NN.png`, 4 frames, 0,05 s/frame (swing rapide visible).

### 4.4 Essaim de Drones — Drone orbital

Le drone est un satellite qui orbite autour du joueur. Taille : 8×8 px.

Silhouette : mini-version du Drone Corrompu (§3.2) mais avec la palette joueur (pas ennemi).
- Corps : `#4A5566` (acier joueur)
- Detail : `#7A8899` (highlight)
- Oeil : `#00E5FF` (cyan Aether — c'est un drone allie, pas corrompu)

Animation : `weapon_drone_idle_NN.png`, 3 frames, 0,15 s/frame — rotation legere de l'oeil.

### 4.5 Champ de Surcharge — Flash de zone

Pas de sprite dedie — implementer en GPUParticles2D ou via un `Circle2D` shader.
Couleurs : flash `#00E5FF` (cyan) sur 0,1 s, puis fade vers transparent sur 0,3 s.
Le rayon du flash correspond au rayon de la zone (100-200 px selon niveau).
Le graphiste n'a rien a produire pour ce VFX — il est genere proceduralement en code.

### 4.6 Lame a Fusion (fusion) — Anneau continu

L'anneau continu 360° est le VFX le plus complexe du MVP. Deux composantes :

**Animation frame-by-frame de metamorphose** (transition Lame Plasma → Lame a Fusion) :
8 frames, 32×32 px, 0,08 s/frame.
- Frames 1-2 : l'arc plasma se referme (les deux extremites se rejoignent)
- Frames 3-4 : anneau instable, couleur qui pulse de `#00E5FF` a `#FF00CC`
- Frames 5-6 : anneau se stabilise, bord magenta se solidifie
- Frame 7-8 : anneau final, cyan et magenta entrelaces (bande bicolore de 2 px de large)
Fichiers : `weapon_fusionblade_metamorphose_NN.png` (01 a 08).

**Anneau en idle (apres metamorphose)** :
Implementer en `Line2D` circulaire avec gradient shader ou GPUParticles2D.
Couleurs de l'anneau : alternance `#00E5FF` / `#FF00CC` par segments de 30°.
Le graphiste produit une texture de bande 64×4 px pour le gradient de l'anneau :
`weapon_fusionblade_ring_texture.png`.

### 4.7 Rail Surchage (fusion) — Rafale et sillage

Projectile de la rafale : meme forme que le projectile Canon a Impulsions (§4.1) mais :
- Couleur : integralement `#00E5FF` (cyan Aether) avec pointe `#FFFFFF`
- Format : 12×4 px (plus long que l'original, suggere la vitesse 600 px/s)
- Fichier : `weapon_bullet_rail.png`

Animation de metamorphose (Canon → Rail) : 10 frames, 32×32 px, 0,08 s/frame.
- Frames 1-3 : le canon s'allonge (details mecaniques qui se deroulent)
- Frames 4-6 : flash `#00E5FF` qui englobe toute la zone 32×32
- Frames 7-8 : le nouveau canon rail emerge, plus long, avec sillage cyan visible
- Frames 9-10 : stabilisation, sillage `#007A99` en trace derriere le canon
Fichiers : `weapon_rail_metamorphose_NN.png` (01 a 10).

Sillage du projectile en vol : pas de sprite — implementer en `Line2D` (longueur 20 px,
couleur `#00E5FF` → transparent) attache au projectile. Aucun livrable graphiste requis.

---

## 5. VFX

### 5.1 Hit flash (ennemi touche)

Implementer via `CanvasItemMaterial` avec un shader de teinte blanche — aucun sprite separe.
La procedure : a chaque hit, `modulate` du sprite ennemi passe a `#FFFFFFFF` pendant 0,05 s
puis retourne a `#FFFFFFFF` avec `modulate` normal. Godot permet cela via `AnimationPlayer`
ou Tween sur la propriete `modulate` du noeud.
Le graphiste ne produit rien pour ce VFX.

### 5.2 Mort ennemi — Particules de dissolution

Format : GPUParticles2D, pas de sprite d'animation.
Parametres recommandes au `developpeur` :
- 8-12 particules, forme carre 1×1 px
- Couleur initiale : couleur principale de l'ennemi tue (ex. `#7A4A2A` pour Essaim)
- Couleur finale : `#00000000` (transparent)
- Duree de vie particules : 0,4 s
- Velocity initiale : radiale depuis le centre, 60-120 px/s
- Gravity : 0 (pas de chute, vue du dessus)

Le graphiste produit 1 texture particule par type d'ennemi (carres de 2×2 px de
la couleur principale) :
- `vfx_particle_rustswarm.png` : `#7A4A2A`
- `vfx_particle_drone.png` : `#4A4A52`
- `vfx_particle_sentinel.png` : `#3A2E35`
- `vfx_particle_colossus.png` : `#4A4A52` + `#AA44FF` (moitie des particules en violet,
  evoque la liberation d'Aether a sa mort et le Noyau qu'il lache)

### 5.3 Level-up — Flash ecran

Implementer en code : `CanvasLayer` avec un `ColorRect` plein ecran `#FFFFFF` qui
passe de alpha 0,7 a 0 sur 0,3 s via Tween. Le graphiste ne produit rien.

### 5.4 Fusion / Evolution — Sequence complete

Sequence en 3 temps (actee en GDD §12) :

**Temps 1 — Flash de desaturation** (0,3 s) :
`CanvasItemShader` applique a la racine du `SubViewport` ou via `WorldEnvironment` :
conversion en niveaux de gris totale. Implementer en code/shader. Pas de sprite.

**Temps 2 — Animation frame-by-frame de metamorphose** :
Voir §4.6 (Lame a Fusion, 8 frames) et §4.7 (Rail Surchage, 10 frames).
Ces animations se jouent pendant ou juste apres le retour de la couleur (fondu depuis
le gris vers la nouvelle palette).

**Temps 3 — Aura GPUParticles2D** (apres metamorphose, duree 2 s) :
Particules emises depuis le centre du joueur :
- 30 particules, cercle de rayon 40 px
- Couleur : pour Lame a Fusion, `#FF00CC` → transparent ; pour Rail, `#00E5FF` → transparent
- Duree de vie : 1,5 s
- Velocity : radiale vers l'exterieur, 80 px/s

Le graphiste produit uniquement les textures de particules d'aura :
- `vfx_aura_fusionblade.png` : losange 3×3 px `#FF00CC` (magenta)
- `vfx_aura_rail.png` : losange 3×3 px `#00E5FF` (cyan)

### 5.5 Collecte orbe XP

GPUParticles2D simples sur collecte :
- 6 particules, `vfx_particle_xp.png` (carre 2×2 px `#AAFF44`)
- Converge vers le joueur (velocity negative, vers le centre)
- Duree 0,3 s

Le graphiste produit `vfx_particle_xp.png`.

### 5.6 Collecte Noyau Aether

Sequence plus marquee que l'orbe XP (le Noyau est plus rare et precieux) :
- Flash ponctuel `#AA44FF` alpha 0,5, rayon 20 px, duree 0,15 s (implementer en code)
- 12 particules `vfx_particle_noyau.png` (losange 3×3 px `#AA44FF`) qui se dispersent
  puis se contractent vers le joueur
- Son : prevoir un SFX distinct du SFX orbe XP (a specifier avec `musicien`)

Le graphiste produit `vfx_particle_noyau.png`.

---

## 6. UI / HUD

### 6.1 Police de caracteres

Police INTEGREE (2026-06-26) : **VT323** (OFL, Google Fonts `ofl/vt323/`), pixel font
monospace type terminal CRT — colle au theme cyberpunk "RUNTIME ENCRYPTED" et donne des
chiffres monospace stables sur les compteurs (HP, timer, Noyaux). Fichier
`assets/fonts/VT323.ttf`, applique globalement via `assets/themes/ui_theme.tres`
(`project.godot [gui] theme/custom`).

Import en rendu pixel NET (impératif pour éviter le flou "baveux") : `antialiasing=0`,
`hinting=0`, `subpixel_positioning=0`, `oversampling=1.0` dans `VT323.ttf.import`.
Tailles : monter d'un cran vs une police vectorielle (VT323 rend plus petit à taille égale).
Base 18 px ; titres section 16 ; timer 40 ; compteur Noyaux 28.

ATTENTION glyphes : VT323 ne couvre que l'ASCII de base (+ `: / . -`). Pas de ♥ ⚡ ✦ ↑ →
ils retombent sur la police système (flou + incohérence). Remplacer par de l'ASCII.

Reserve titres 8-bit : **Press Start 2P** (`assets/fonts/PressStart2P.ttf`, OFL).
Alternatives pixel non retenues (sources gated) : m5x7 (Daniel Linssen), Pixel Operator.

### 6.2 Barre HP

- Dimensions : 200×10 px
- Position : coin superieur gauche, offset 8 px depuis les bords
- Fond : `ColorRect` `#1A1A22`
- Rempli > 50% HP : `ColorRect` `#22CC66` (vert Aether)
- Rempli 25-50% HP : `ColorRect` `#FFAA00` (orange alerte, transition a implementer en code)
- Rempli < 25% HP : `ColorRect` `#FF3322` (rouge alarme, clignotant 0,5 Hz)
- Bordure : `StyleBoxFlat` 1 px `#3A3A42`
- Label "HP" : m5x7 8 px, `#AA9988`, a gauche de la barre

Icone coeur : optionnelle MVP, 8×8 px, `#FF3322`, fichier `ui_icon_hp.png`.

### 6.3 Barre XP

- Dimensions : 200×6 px, sous la barre HP, gap 2 px
- Fond : `ColorRect` `#1A3308`
- Rempli : `ColorRect` `#AAFF44`
- Bordure : aucune (minimalisme)
- Label niveau : m5x7 16 px, `#E8E4E0`, format "Niv. X", a droite de la barre

### 6.4 Timer et compteur Noyaux (HUD en run)

- Timer : coin superieur centre, m5x7 16 px `#E8E4E0`. Format "MM:SS". Passe en
  `#FF3322` quand < 60 s restantes.
- Compteur Noyaux : coin superieur droit. Icone 8×8 px `#AA44FF` + label m5x7 16 px.
  Fichier icone : `ui_icon_noyau.png` — losange 8×8 px violet.

### 6.5 Cartes de level-up

Format carte : 80×120 px. Fond `#1A1A22`. Bordure 2 px par rarity :
- Commun : `#AAAAAA`
- Rare : `#44AAFF`
- Epique : `#CC44FF`

Layout interne :
- Zone icone : 48×48 px au centre-haut (y : 10px), fond `#252028`
- Nom : m5x7 8 px centree, `#E8E4E0`, y : 64 px
- Description courte : m5x7 8 px, `#AA9988`, 2 lignes max, y : 76 px
- Badge rarity : rond 8×8 px en coin superieur droit, couleur rarity

Le graphiste produit les icones d'armes et passifs au format 32×32 px (elles seront
scalees a 48×48 dans le layout carte via Godot) :
- `ui_icon_impulse_cannon.png`
- `ui_icon_plasmablade.png`
- `ui_icon_droneswarm.png`
- `ui_icon_overloadfield.png`
- `ui_icon_thermal_core.png`
- `ui_icon_reinforced_plate.png`
- `ui_icon_servomotors.png`
- `ui_icon_capacitor.png`
- `ui_icon_fusionblade.png`
- `ui_icon_rail.png`

Ambiance icones : fond sombre `#252028`, element central en couleur saturee de sa famille
(arme joueur : palette Aether §1.2 ; passif : palette joueur §1.3). Style lisible a 32 px.

### 6.6 Ecran de fin de run

Fond : `ColorRect` `#0A0A0F` plein ecran, alpha 95%.
Titre : m5x7 16 px × 3 (tres grand) — "EXTRACTION REUSSIE" en `#00E5FF` ou
"MORT EN SERVICE" en `#CC3311`. (Taille a moduler via Godot scale.)
Corps du decompte : m5x7 16 px, `#E8E4E0`.
Total Echos : m5x7 16 px × 2, `#AA44FF` (violet, couleur Noyau = couleur Echoes).

Boutons "Retour au Hub" et "Rejouer" :
- Format : 120×30 px
- Fond normal : `#252028`
- Fond hover : `#44AAFF` (rare bleu pour l'interactivite)
- Texte : m5x7 8 px, `#E8E4E0`
- Bordure 1 px : `#4A4A52`

Le graphiste ne produit pas de sprites pour l'ecran de fin — tout en `ColorRect` +
`Label`. Uniquement les icones de boutons si souhaite.

### 6.7 Hub — Liste d'ameliorations

Meme style que l'ecran de fin. Chaque ligne d'amelioration :
- Label nom : m5x7 8 px, `#E8E4E0`
- Label niveau : m5x7 8 px, `#AAFF44` ("X/Y")
- Cout prochain niveau : m5x7 8 px, `#AA44FF` + icone `ui_icon_noyau.png`
- Bouton "Acheter" : meme style que boutons fin de run
- Etat grise si Echoes insuffisants : modulate `#606060`

### 6.8 Splash screen (key art)

Format final : 1280×720 px (resolution cible du projet).
Composition recommandee :
- Fond : arene en ruines (silhouette de murs et colonnes effondrées), tons §1.1
- Plan median : le Cyborg de dos ou 3/4, arme levee, implants qui luisent `#00E5FF`
- Plan avant : debris au sol, traces de rouille
- Titre "CHIMERA PROTOCOL" : m5x7 ou police alternative impact, 48 px, blanc `#E8E4E0`,
  ombre portee noire 1 px
- Tagline : "Fusionne. Evolue. Survive." m5x7 16 px, `#00E5FF`
- Bloom Aether : aura cyan autour du joueur et des fissures de murs en arriere-plan

Le splash screen est un livrable distinct de tous les sprites — c'est une illustration.
Peut etre produit en pixel art plus grand puis redimensionne, ou dessine directement
en 1280×720 (grille 4×4 per pixel au niveau logique).

---

## 7. Arene — "Le Sanctuaire en Ruines"

### 7.1 Tileset sol

Produire un tileset 32×32 px compatible Godot `TileMap`. Format : spritesheet
ou fichiers individuels selon la preference du graphiste (Godot supporte les deux).

Tiles de sol requises (toutes 32×32) :

| Tile | Fichier | Description |
|---|---|---|
| Sol base | `tile_floor_01.png` | Gris-ardoise `#1A1A22`, texture subtile (variation de 1-2 px) |
| Sol variation | `tile_floor_02.png` | Version legerement plus claire `#2D2A33`, joint visible |
| Sol fissure | `tile_floor_crack.png` | Sol base avec fissure + halo Aether `#007A99` dans la fissure |
| Sol rouille | `tile_floor_rust.png` | Sol base avec tache rouille `#4A2A15` |
| Sol debris | `tile_floor_debris.png` | Petits fragments metalliques poses sur le sol |

### 7.2 Tileset murs et collisions

Murs solides : 32×32 px, hauteur visuelle 40 px (les 8 px superieurs depassent en fausse
perspective — layer de decor NON collidable positionne 8 px au-dessus du mur collidable).

| Tile | Fichier | Description |
|---|---|---|
| Mur base | `tile_wall_01.png` | Beton oxide `#252028`, texture de pierre |
| Mur rouille | `tile_wall_rust.png` | Mur base + plaque metallique rouilee |
| Mur fissure Aether | `tile_wall_crack_aether.png` | Mur avec fissure laissant passer `#007A99` (1-2 px de brillance) |
| Debris au sol | `tile_debris_01.png` | Bloc de pierre effondre, non collidable, decor |
| Debris metal | `tile_debris_metal.png` | Structure metallique tombee, non collidable |
| Colonne brisee | `tile_column.png` | Colonne de 32×64 px (2 tiles de haut), collidable sur 32×32 bas |

### 7.3 Ambiance lumineuse

Le bloom Aether (`WorldEnvironment.glow`) est active globalement. Elements qui beneficient
du bloom (seuil de luminosite suffisant) :
- Toute couleur de la famille §1.2 emise par les sprites
- Les fissures Aether dans les murs (`tile_wall_crack_aether.png`) — `#007A99` est en limite
  de seuil : le placer en `#00A0BB` pour garantir le bloom (le `directeur-artistique` autorise
  cet ecart de 2 valeurs hex sur cette case unique)
- Les yeux des ennemis (`#FF2200`) — couleur saturee, bloom naturel
- Les implants du joueur (`#00E5FF`)
- Tous les VFX de la famille Aether

Elements intentionnellement PAS en bloom (sous le seuil) :
- Sol de l'arene (famille §1.1)
- Corps des ennemis (rouille, metal mort)
- Fond HUD

### 7.4 Elements de decor recommandes

Pour briser la lisibilite plate de l'arene et enrichir le lore visuel :

1. **Geyser d'Aether** : zone circulaire de 48 px de diametre d'ou sort periodiquement
   un jet de particules `#00E5FF`. Le graphiste produit une animation 3 frames 32×32 :
   `tile_aether_geyser_NN.png` (jet montant, pale, semi-transparent).
   Le geyser inflige des degats de zone periodiques (cf. GDD §10).

2. **Piliers de technologie morte** : structures 32×64 px, metal desactive (`#4A4A52`)
   avec un ecran eteint (`#1A1A22` uniform). Decor non-interactif.
   Fichier : `tile_tech_pillar.png`.

3. **Flaques de Rouille Vivante** : zones au sol 32×32, couleur `#3A3A28` (vert bile),
   avec une animation subtile de "bulles" (2 frames, 0,5 s/frame).
   Fichier : `tile_rust_pool_NN.png`. Purement decoratif MVP.

---

## 8. Specifications techniques de production

### 8.1 Format des fichiers

- Format : PNG, canal alpha transparent `#00000000`
- Profondeur : 32 bits RGBA
- Fond : transparent pur — aucun pixel de fond residuel
- Compression PNG : maximum (lossless) — les fichiers sprite sont petits, pas de raison
  d'economiser sur la compression

### 8.2 Resolution de reference

- **Grille universelle : 32×32 px par frame** (actee en GDD §12)
- Exception unique actee : Colosse Greffe a 48×48 px (cf. §3.4)
- Splash screen a 1280×720 px
- Icones UI a 32×32 px (scalees dans Godot si besoin)
- Textures de particules : 2×2 px ou 3×3 px (taille minimale)

### 8.3 Convention de nommage (confirmee et etendue depuis CLAUDE.md)

Format : `{categorie}_{objet}_{action}_{numero_frame:02d}.png`

Numeros de frames : toujours 2 chiffres, commence a `01`.

| Categorie | Prefixe | Exemple |
|---|---|---|
| Joueur | `player_` | `player_run_right_01.png` |
| Ennemis | `enemy_{nom}_` | `enemy_rustswarm_move_03.png` |
| Armes (VFX en jeu) | `weapon_{nom}_` | `weapon_plasmablade_swing_02.png` |
| Projectiles | `weapon_bullet_` | `weapon_bullet_impulse.png` (statique, pas de num.) |
| VFX | `vfx_` | `vfx_particle_xp.png` |
| Tiles | `tile_` | `tile_floor_crack.png` |
| UI icones | `ui_icon_` | `ui_icon_hp.png` |
| UI elements | `ui_` | `ui_card_background.png` |

Fichiers statiques (1 seule frame) : PAS de suffixe `_01` — le nom s'arrete apres l'action.
Fichiers animes : suffixe `_NN` obligatoire meme pour 2 frames.

### 8.4 Nombre de frames standard par type d'animation

| Type | Frames standard | Duree frame | Total duree | Notes |
|---|---|---|---|---|
| Idle | 4 | 0,15-0,25 s | ~0,8 s | Boucle naturelle |
| Run | 6 | 0,08-0,12 s | ~0,6 s | Fluidite deplacement rapide |
| Attack / Swing | 4 | 0,05-0,1 s | ~0,25 s | Lisibilite de l'impact |
| Death | 6-10 | 0,08-0,1 s | ~0,7-1 s | Satisfaction de la mort ennemi |
| Metamorphose fusion | 8-10 | 0,08 s | ~0,7-0,8 s | Acte GDD §12 |
| Hit flash | 2 | 0,05 s | 0,1 s | Peut etre un shader |

### 8.5 Organisation des fichiers dans le depot

Tous les sprites livres dans `assets/sprites/` selon la structure :
```
assets/sprites/
├── player/
│   ├── player_idle_01.png ... player_idle_04.png
│   ├── player_run_right_01.png ... player_run_right_06.png
│   ├── player_run_down_01.png ... player_run_down_06.png
│   └── player_death_01.png ... player_death_08.png
├── enemies/
│   ├── rustswarm/
│   ├── drone/
│   ├── sentinel/
│   └── colossus/
├── weapons/
│   ├── weapon_bullet_impulse.png
│   ├── weapon_bullet_rail.png
│   ├── weapon_bullet_sentinel_01.png
│   ├── weapon_bullet_sentinel_02.png
│   ├── weapon_plasmablade_swing_01.png ... 04.png
│   ├── weapon_drone_idle_01.png ... 03.png
│   ├── weapon_fusionblade_metamorphose_01.png ... 08.png
│   ├── weapon_fusionblade_ring_texture.png
│   └── weapon_rail_metamorphose_01.png ... 10.png
├── vfx/
│   ├── vfx_particle_rustswarm.png
│   ├── vfx_particle_drone.png
│   ├── vfx_particle_sentinel.png
│   ├── vfx_particle_colossus.png
│   ├── vfx_particle_xp.png
│   ├── vfx_particle_noyau.png
│   ├── vfx_aura_fusionblade.png
│   └── vfx_aura_rail.png
├── tileset/
│   ├── tile_floor_01.png ... tile_floor_rust.png
│   ├── tile_wall_01.png ... tile_column.png
│   ├── tile_aether_geyser_01.png ... 03.png
│   ├── tile_tech_pillar.png
│   └── tile_rust_pool_01.png ... 02.png
└── ui/
    ├── ui_icon_hp.png
    ├── ui_icon_noyau.png
    ├── ui_icon_impulse_cannon.png
    ├── ui_icon_plasmablade.png
    ├── ui_icon_droneswarm.png
    ├── ui_icon_overloadfield.png
    ├── ui_icon_thermal_core.png
    ├── ui_icon_reinforced_plate.png
    ├── ui_icon_servomotors.png
    ├── ui_icon_capacitor.png
    ├── ui_icon_fusionblade.png
    └── ui_icon_rail.png
```

Le splash screen et le key art sont dans `assets/` a la racine (pas dans `sprites/`).

### 8.6 Priorite de production (ordre recommande pour le MVP)

Le graphiste doit livrer dans cet ordre pour permettre l'integration au fur et a mesure :

**Iteration 1 — Jouabilite visible** :
1. `player_idle`, `player_run_right`, `player_run_down` (le joueur doit etre visible immediatement)
2. `enemy_rustswarm_move` (ennemi le plus frequent des le debut de run)
3. `weapon_bullet_impulse` (projectile de base)
4. `tile_floor_01`, `tile_floor_02`, `tile_wall_01` (arene lisible)

**Iteration 2 — Contenu complet** :
5. Tous les ennemis restants (`drone`, `sentinel`, `colossus`)
6. Animations `death` de tous les ennemis + `player_death`
7. `weapon_plasmablade_swing`, `weapon_drone_idle`, `weapon_fusionblade_ring_texture`
8. Tiles de decor restantes

**Iteration 3 — Polish et UI** :
9. Toutes les icones UI
10. VFX particles (petits fichiers, rapides a produire)
11. Animations de metamorphose des fusions
12. Splash screen

---

## 9. Cohérence entre les trois familles visuelles

Le GDD prevoit trois familles jouables a terme (§4) : Humains, Cyborgs, Robots. Le MVP
utilise uniquement le Cyborg. Ce guide anticipe la coherence visuelle pour ne pas creer
de rupture lors des ajouts futurs.

**Regles d'appartenance visuelle** :
- **Humains** : majoritairement couleurs organiques (`#8A6655` chair, `#5A4A3A` cuir, `#C8A870`
  tissus). Tres peu de metal. L'Aether est porte sous forme de reliques (objets distincts,
  pas implantes). Leur "energie vivante" est plus chaude : ambre `#FFAA00` plutot que cyan.
- **Cyborgs** (famille MVP) : metal froid + organique residuel + implants Aether cyan. Le
  curseur homme/machine est variable selon le niveau d'evolution du personnage.
- **Robots** : integralement metal et composants electroniques. Aucun organique. Leur "energie"
  est une batterie : couleur ambre `#FFCC00` pour les Robots allies, `#FF2200` pour les corrompus.

Cette distinction garantit que dans une run avec plusieurs personnages jouables futurs, la
silhouette et la palette suffisent a identifier la famille sans lire le nom du personnage.

Les **ennemis** (Rouille Vivante) sont une categorie distincte : ils ne sont ni la famille
Humains ni Robots ni Cyborgs. Ils empruntent des formes des trois familles mais les corrompent
avec la palette §1.1 (matiere morte). Seul marqueur commun : les yeux `#FF2200`.

---

*Ce guide est actif a partir de la Phase 3 du projet. Toute modification doit passer par le
`directeur-artistique` avant integration. Toute ambiguite non resolue dans ce document est
a remonter avant de commencer la production du livrable concerne.*
