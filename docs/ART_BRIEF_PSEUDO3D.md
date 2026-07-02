# BRIEF DIRECTION ARTISTIQUE — Passage "Pseudo-3D avec ombres"

> Rédigé par `directeur-artistique` le 2026-07-02. Destinataire : `graphiste`.
> Complète `docs/STYLE_GUIDE.md` (palette, frames, nommage — inchangé) sans le remplacer.
> Objectif : coder `tools/pseudo3d_lib.py`, une bibliothèque PIL partagée, puis régénérer
> TOUS les sprites (personnages, ennemis existants + ~20 à venir, obstacles, tuiles de sol,
> icônes armes/UI) pour leur donner une illusion de volume/profondeur en pixel art pré-rendu,
> sans moteur 3D ni shader temps réel.

Ne contredit aucune décision actée en GDD §12 : résolution 32×32 (48×48 Colosse), palette
32 couleurs, opposition matière morte/énergie vivante, `texture_filter = Nearest`. Ce brief
ajoute une **couche de rendu** (comment peindre chaque pixel), pas de nouvelles règles de
production (dimensions, nombre de frames restent ceux de `docs/STYLE_GUIDE.md`).

## 0. Pourquoi maintenant

Les sprites actuels (cf. `tools/generate_character_sprites.py`) utilisent des blocs de
couleur plate D/M/L (dark/mid/light) posés à la main, sans règle de lumière cohérente d'un
sprite à l'autre : le "D" du Titan n'a pas la même signification géométrique que le "D" du
Vagabond. Résultat : le jeu a l'air plat malgré les efforts. La bibliothèque partagée doit
remplacer ces choix ad hoc par UNE fonction de dérivation appliquée partout.

## 1. Direction de lumière unique et fixe

**Haut-gauche à 45°**, vecteur lumière `L = normalize(-1, -1)` (soit un angle de 225° si on
compte 0°=droite, sens trigo standard, ou simplement "de haut-gauche vers bas-droite").

Pourquoi ce choix et pas un autre :
- **Convention historique du RPG isométrique / pixel art top-down** (Diablo, Zelda ALTTP,
  la plupart des survivors récents type Vampire Survivors qui ont migré vers du pseudo-volume) :
  le joueur reconnaît instantanément "haut = éclairé, bas = ombre" sans effort cognitif.
  Casser cette convention forcerait une réinterprétation à chaque sprite.
- **Cohérence avec l'ombre portée** (§3) : une lumière haut-gauche projette une ombre
  bas-droite, qui NE RECOUVRE PAS le sprite lui-même en vue du dessus stricte — condition
  nécessaire pour que l'ombre reste lisible comme un élément distinct au sol plutôt que de
  se confondre avec le shading du corps.
- **Uniformité obligatoire** : TOUTE entité (joueur, ennemi, obstacle, tuile) utilise le
  même `L`. Aucune exception, même pour les futurs ~20 ennemis. Une lumière qui changerait
  de direction selon l'entité casserait immédiatement l'illusion d'un monde cohérent.

Cette direction doit être une **constante nommée** dans `pseudo3d_lib.py` (ex. `LIGHT_DIR =
(-1, -1)`), jamais recopiée en dur ailleurs.

## 2. Règle de dérivation shadow/highlight (formule HSV)

Pour toute couleur de base `base_rgb`, la bibliothèque doit exposer une fonction du type :

```
def shade(base_rgb, face) -> rgb:
    h, s, v = rgb_to_hsv(base_rgb)
    if face == "highlight":   # face tournée vers la lumière (haut/gauche du volume)
        v' = clamp(v * 1.35, 0, 1.0)       # +35% luminosité (V)
        s' = clamp(s * 0.85, 0, 1.0)       # -15% saturation (évite le "blanc sale")
    elif face == "base":       # face perpendiculaire, teinte de référence
        v' = v
        s' = s
    elif face == "shadow":     # face tournée à l'opposé de la lumière (bas/droite, contact sol)
        v' = clamp(v * 0.55, 0, 1.0)       # -45% luminosité (V)
        s' = clamp(s * 1.10, 0, 1.0)       # +10% saturation (l'ombre ne doit pas devenir grise/terne)
    elif face == "contact":    # ligne de contact au sol (pied, base de l'obstacle) — la plus sombre
        v' = clamp(v * 0.35, 0, 1.0)       # -65% luminosité
        s' = clamp(s * 1.15, 0, 1.0)
    return hsv_to_rgb(h, s', v')
```

Règles chiffrées à retenir (applicables telles quelles en code) :

| Face | ΔV (luminosité) | ΔS (saturation) | Usage |
|---|---|---|---|
| `highlight` | ×1.35 | ×0.85 | arête haute/gauche d'un volume (épaule, sommet de casque, angle supérieur d'un pilier) |
| `base` | ×1.00 | ×1.00 | couleur de référence telle que définie dans la palette existante — ne change pas |
| `shadow` | ×0.55 | ×1.10 | face basse/droite d'un volume (dessous du bras, flanc droit d'un torse) |
| `contact` | ×0.35 | ×1.15 | pixel(s) de contact direct avec le sol (semelle, base d'un pilier) — jamais plus de 1-2 px de haut |

Contrainte dure : **jamais de noir/blanc pur** (`v=0` ou `v=1`) — on reste dans la teinte
d'origine (`h` inchangé) pour que shadow/highlight lisent comme "la même matière sous un
angle de lumière différent", pas comme un halo ou un contour ajouté après coup.

Un sprite typique n'utilise que 3 à 4 faces (highlight, base, shadow, contact) — pas de
dégradé continu pixel-par-pixel façon rendu 3D réel : on reste dans l'esprit "cel-shading"
du pixel art, avec des zones plates de teinte dérivée, pas un gradient lissé.

## 3. Ombre portée au sol

- **Forme** : ellipse aplatie, ratio largeur:hauteur = **2.2:1** (ex. sprite 32×32 →
  ellipse ~22×10 px). Jamais un cercle (lirait comme un halo magique, pas une ombre).
- **Couleur** : noir pur désaturé, `rgba(0, 0, 0, alpha)` — PAS de teinte colorée (l'ombre
  au sol n'est pas de la matière, ne doit jamais rivaliser avec la palette Aether).
- **Opacité** : `alpha = 90` (35%) au centre de l'ellipse, dégradé radial vers `alpha = 0`
  au bord (2-3 px de fondu) pour éviter un bord dur qui accroche l'œil en combat dense.
- **Décalage par rapport au sprite** : l'ombre est centrée horizontalement sur l'ancre au
  sol du sprite (les pieds/la base), **décalée de +2 px vers le bas-droite** (direction
  opposée à `LIGHT_DIR`, cf. §1) — cohérence géométrique lumière/ombre non négociable.
- **Taille relative** : largeur d'ellipse = ~65-70% de la largeur du sprite au niveau des
  pieds/base (pas de la largeur totale si le sprite a des bras/armes qui dépassent).
- **Z-order** : toujours dessinée AVANT le sprite (sous), jamais en `Add`/additif — c'est
  une occlusion de lumière, pas une émission.
- Pour les tuiles de sol (§5) : pas d'ombre portée (elles sont la surface elle-même), sauf
  éventuel relief ponctuel (fissure, conduit en saillie) qui suit la même règle à échelle
  réduite (ellipse 1-2 px).

## 4. Contour / outline — cohabitation avec l'ombrage

**Garder le contour accent vif existant sur les obstacles** (`Outline()` dans
`src/Systems/BiomeObstacles.cs`, couleur = accent du biome, épaisseur 2.5 px, + halo
additif `AddHalo`) — c'est un signal de gameplay ("ceci est infranchissable"), pas un choix
esthétique pur, donc il **ne doit pas être supprimé ni affaibli** par l'ombrage.

Règle de cohabitation :
- L'ombrage pseudo-3D (highlight/base/shadow/contact, §2) s'applique **à l'intérieur** du
  contour, sur le remplissage du sprite.
- Le contour lui-même **reste plat, à la couleur accent pure** (pas de dégradé sur le
  contour) — il doit rester le repère le plus contrasté et constant de la silhouette, quel
  que soit l'éclairage interne.
- Le halo additif reste identique (glow autour du contour, indépendant du volume interne).
- Sur les personnages et ennemis (pas de contour accent aujourd'hui, silhouette nue) :
  ne pas ajouter de contour — l'ombrage seul doit suffire à la lisibilité, cf. §5. Si un
  test de lisibilité (§7) échoue en combat dense, la solution est un contour sombre fin
  (1 px, `shadow`-teinte, pas noir pur) sur les ennemis uniquement, jamais sur le joueur.

## 5. Application différenciée par catégorie

### Personnages (joueur + ennemis avec squelette articulé)
- Volumes principaux à traiter séparément : tête/casque, torse, bras/armes, jambes.
- Règle simple applicable à un sprite "vue de dessus/3-4" légèrement plongeante" (le jeu
  n'est pas une vraie vue du dessus stricte, les sprites actuels montrent le visage/visière) :
  - Partie **haute** de chaque volume (dessus des épaules, dessus du casque, dessus des
    cuisses) → `highlight`.
  - Flanc **gauche** de chaque volume → `base` légèrement éclairci (mi-chemin base/highlight,
    car la lumière vient aussi de la gauche).
  - Flanc **droit** de chaque volume → `shadow`.
  - Dessous (bas des jambes au contact du sol, dessous des bras) → `contact`.
- Le noyau/implant énergétique (Aether, cyan, orange Titan, etc.) reste **hors système
  d'ombrage** : il émet sa propre lumière (`glow()` existant), ne doit jamais être assombri
  en `shadow`/`contact` — c'est la source d'énergie vivante, elle est visible même dans
  l'ombre du corps (cohérence avec l'opposition matière morte/énergie vivante du GDD §12).
- Ombre portée : ancrée sous les pieds (voir §3), suit le personnage à chaque frame de marche.

### Obstacles (formes statiques, un seul rendu, pas d'animation)
- Traiter comme un unique volume simple (colonne, cristal, monolithe, pylône) : face
  haut-gauche = `highlight`, face bas-droite = `shadow`, base au sol = `contact` + ombre
  portée elliptique.
- Le contour accent (§4) encercle l'ensemble, indépendant du volume interne.
- Comme l'obstacle ne bouge pas, l'ombre portée peut être légèrement plus marquée
  (`alpha = 100`, +10 vs personnages) pour ancrer visuellement l'objet au sol dans un
  décor qui, lui, va aussi recevoir du relief (§5 tuiles).

### Tuiles de sol (surface plane, par biome)
- Pas de volume à proprement parler : la profondeur vient d'un **léger relief de surface**,
  pas d'un ombrage de silhouette.
- Règle : sur les variantes de tuile existantes (base/variation/fissure/rouille/débris,
  cf. GDD §10 — `GroundRenderer` seed 42, distribution 72/18/5/4/1%), appliquer un dégradé
  très subtil **haut-gauche `highlight` → bas-droite `shadow`** sur toute la tuile, à
  **amplitude réduite** (±10-15% de luminosité seulement, pas ×1.35/×0.55 comme les
  personnages — une tuile n'est pas un objet, un gradient fort donnerait un effet "carrelage
  en relief" qui casse la continuité entre tuiles adjacentes).
- Les éléments en creux (fissures, conduits, flaques de Rouille Vivante) reçoivent l'inverse
  (bord haut-gauche en `shadow`, bord bas-droite en `highlight`) pour lire comme un creux et
  non une bosse — règle simple et vérifiable : "la lumière arrive d'en haut-gauche, donc un
  creux a son ombre du côté opposé à une bosse".
- Aucune ombre portée elliptique sur les tuiles (elles seraient la surface elle-même).

### Icônes UI (armes, passifs, items — 32×32 px, vues à petite taille dans les cartes/HUD)
- Ombrage **fortement réduit** par rapport aux sprites en jeu : n'utiliser que 2 faces
  (`highlight` + `shadow`, PAS `contact`, PAS d'ombre portée elliptique) — à cette échelle
  d'affichage (souvent scalée en dessous de 32 px dans le HUD), un relief à 4 niveaux se
  brouille en bruit visuel.
- Amplitude encore réduite vs tuiles : `highlight` ×1.20 / `shadow` ×0.70 (au lieu de
  ×1.35/×0.55) — priorité absolue à la reconnaissance immédiate de la silhouette/icône sur
  la lisibilité du volume.
- Contour : les icônes gardent un contour 1 px sombre existant si présent (cohérence avec
  le pattern StyleBox de l'UI) — ne pas le dégrader.
- Limite dure : si un test à taille d'affichage réelle (voir §7) montre une icône plus
  difficile à identifier qu'avant, RETIRER l'ombrage sur cette icône plutôt que de forcer —
  la lisibilité UI prime toujours sur la cohérence de style pour les éléments les plus petits.

## 6. Cohérence avec la palette UI et les accents de biome

- Le système d'ombrage (§2) **dérive** toujours de la couleur de base existante — il ne
  remplace jamais une couleur de palette par une teinte hors-charte. Ex. : le cyan `#44FFEE`
  reste un cyan en highlight/shadow, jamais un cyan qui vire au bleu-gris neutre.
- Les couleurs de fond UI (`#1A1A2E`) et accents (`#44FFEE` cyan, `#AA44FF` violet, `#FFCC44`
  or) ne sont PAS concernées par l'ombrage volumétrique — elles restent des couleurs UI
  plates (panels, texte, bordures d'écran). Le pseudo-3D s'applique aux SPRITES DE JEU
  (personnages/ennemis/obstacles/tuiles/icônes), pas aux éléments d'interface eux-mêmes
  (barres HP/XP, fonds de panel, texte).
- Les accents de biome (`GroundRenderer.BiomeDef.Accent` — sanctuaire `#4CD9F2` cyan-gris,
  aether `#9E66FF` violet, fournaise `#FF8030` orange, givre `#9EE0F2` bleu-glace, néon
  `#F24CD9` magenta) servent de **couleur de base pour highlight/glow** des obstacles et du
  contour (§4), inchangé. L'ombrage volumétrique (§2) s'applique à la couleur de matière de
  l'obstacle (pierre, cristal, basalte, glace, métal), PAS à l'accent lui-même qui reste la
  teinte de contour/halo pure — même logique que le noyau énergétique des personnages (§5) :
  la matière s'ombre, l'énergie/l'accent rayonne.

## 7. Checklist de validation visuelle (avant régénération en masse)

Le graphiste doit valider ces 3 critères sur un petit lot de test (2-3 sprites représentatifs
d'une catégorie chacun : 1 personnage, 1 obstacle, 1 jeu de tuiles, 1 icône) **avant** de
lancer la régénération complète de tous les sprites du jeu :

1. **Test de cohérence de lumière** : poser côte à côte un personnage, un obstacle et une
   tuile régénérés — la direction de l'ombre portée et l'emplacement du highlight doivent
   être identiques (haut-gauche clair, bas-droite sombre) sur les trois, sans avoir besoin
   de lire une légende pour s'en convaincre.
2. **Test de silhouette à distance / en mouvement** (zoom-out ou aperçu à taille réelle en
   jeu, pas en éditeur zoomé) : le sprite doit rester identifiable en < 150 ms au milieu
   d'un fond de tuiles ombrées — reprend la règle de lisibilité absolue déjà actée en GDD
   §12 ("tout effet qui réduit la distinction joueur/ennemi/projectile en dessous de 150 ms
   de lecture en combat dense est désactivé ou rétrogradé").
3. **Test à taille d'affichage réelle pour les icônes UI** : afficher l'icône dans son
   contexte réel (carte de level-up, slot HUD) et vérifier qu'elle reste reconnaissable
   sans zoomer — si l'ombrage la rend plus confuse qu'une version plate, appliquer la
   clause de repli du §5 (retirer l'ombrage sur cette icône).

Si un de ces trois tests échoue, corriger la bibliothèque (`pseudo3d_lib.py`) et refaire le
lot de test — ne jamais lancer la régénération en masse (personnages + ~20 nouveaux ennemis
+ obstacles + tuiles + icônes) sur un système qui n'a pas passé les 3 checks.

## 8. Livrables attendus du `graphiste`

1. `tools/pseudo3d_lib.py` — fonctions `shade(base_rgb, face)`, `cast_shadow(img, anchor,
   width_ratio=0.65..0.70)`, constantes `LIGHT_DIR`, tables de coefficients du §2, réutilisées
   par tous les `tools/generate_*.py` existants (personnages, ennemis, boss, obstacles,
   tuiles, icônes) via import — pas de duplication de logique de shading dans chaque script.
2. Lot de test (§7) soumis à validation `directeur-artistique` avant régénération complète.
3. Régénération de tous les scripts `tools/generate_*.py` pour utiliser la bibliothèque,
   `.import` régénérés et commités (piège connu BUG-301, cf. `CLAUDE.md`).
4. Tout écart par rapport à ce brief (ex. une catégorie qui ne peut visiblement pas
   supporter l'ombrage sans perdre en lisibilité) doit être remonté à `directeur-artistique`
   pour arbitrage avant d'être appliqué en masse — ne pas trancher unilatéralement une
   exception à la direction de lumière unique (§1) ou à la clause de repli lisibilité (§5/§7).
