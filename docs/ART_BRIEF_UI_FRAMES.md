# ART BRIEF — Cadres d'UI ("plaque blindée octogonale")

> Rédigé par `directeur-artistique` le 2026-07-26. Destinataire : `graphiste` (production des
> textures) et `developpeur` (câblage Godot). Complète `docs/STYLE_GUIDE.md` §6 et
> `docs/ART_BRIEF_PSEUDO3D.md` sans les contredire — même palette (GDD §12 / `CLAUDE.md`), même
> grille pixel 32×32, même `texture_filter = Nearest`. Ce brief ajoute la couche "cadres d'UI"
> (boutons, panneaux, popups, cartes) qui n'était pas couverte par le passage pseudo-3D (lequel
> ne concernait que les sprites de jeu, cf. ART_BRIEF_PSEUDO3D §6 : "le pseudo-3D s'applique aux
> SPRITES DE JEU, pas aux éléments d'interface eux-mêmes").

---

## 1. Diagnostic — pourquoi l'UI actuelle fait "générique"

Constat factuel, sourcé sur le code actuel (`src/UI/*.cs`) et les captures `docs/*.png`
(`nr_menu_es_2026.png`, `hub_reset.png`, `pause_quit.png`, `test_arsenal.png`).

1. **Un seul motif, recopié partout avec juste la couleur qui change.** `MainMenu.BtnStyle()`
   (l.336-343), `HubScreen` boutons Acheter (l.115-134) et chips (l.267-286), `CodexScreenBase`
   (l.215-221), `LevelUpScreen.MakeActionButton()` (l.246-263) et cartes (l.168-173),
   `AssimilationScreen` (l.80-81, l.336-341) : **tous** instancient un `StyleBoxFlat` avec
   `BgColor` quasi-noir translucide + `SetBorderWidthAll(n)` + `BorderColor` plat +
   `SetCornerRadiusAll(n)`. Boutons de menu, popup de pause, carte de level-up et chip de perk
   partagent l'exacte même recette — seule la teinte de bordure (cyan/violet/or/orange) et le
   rayon (3, 4, 6, 8 ou 10 px selon l'écran, sans règle) changent. C'est la signature d'un
   composant de bibliothèque générique (type "bordered card" Bootstrap/Material), pas d'un
   objet dessiné pour cet univers.
2. **`SetBorderWidthAll` / `SetCornerRadiusAll` : aucune bordure n'est jamais asymétrique.**
   Les 4 côtés et les 4 coins sont toujours identiques. Rien ne signale "ceci est assemblé,
   soudé, boulonné" — un cadre unique et uniforme est l'antithèse visuelle d'une plaque
   d'armure ou d'un boîtier de machine.
3. **Le rayon de coin arrondi + l'anti-aliasing natif de `StyleBoxFlat` produisent un bord
   flou.** Godot lisse `corner_radius` par défaut (`anti_aliasing = true`), ce qui donne des
   coins ronds à dégradé de pixels — en clash direct avec les sprites en `texture_filter =
   Nearest` (bords nets, sans dégradé). Le contraste "sprites nets / cadres flous" est visible
   à l'œil sur `test_arsenal.png` (zoom sur les coins des panneaux de liste) et
   `pause_quit.png`.
4. **Le fond des panneaux est presque de la même couleur que le fond d'écran.**
   `CodexScreenBase.PanelBg = (0.10, 0.10, 0.18, 0.92)` (→ `#1A1A2E` à 92 %) est quasi
   identique au fond de scène `#1A1A2E` (`CLAUDE.md`). Résultat : un panneau ne se distingue du
   décor QUE par son liseré fin — il n'a aucune épaisseur, aucune "matière" propre. C'est le
   symptôme classique d'un `<div>` bordé posé sur un fond presque égal : rien ne dit "objet
   physique posé devant" plutôt que "rectangle dessiné par-dessus".
5. **L'état focus n'est qu'un changement d'épaisseur (2→3 px) et de couleur.**
   `ConnectHoverEffects` (`MainMenu.cs` l.235-240), `ApplyChipStyle` (`HubScreen.cs` l.283-285) :
   le focus passe juste `BorderColor` à l'or/violet et `border_width` de 2 à 3 px. Aucune
   différence de forme, aucun élément additionnel — exactement le "simple changement de teinte
   subtil" que la contrainte d'accessibilité de ce brief interdit.
6. **Les séparateurs sont des `HSeparator`/`VSeparator` Godot par défaut** (`PauseScreen.cs`
   l.92, l.397-404 ; `OptionsScreen.cs` l.61-77) : trait fin gris neutre du thème natif,
   aucune identité, aucun lien avec la palette ni l'univers.
7. **Aucune texture pixel n'entre jamais en jeu pour les cadres** — tout est vectoriel
   `StyleBoxFlat`, alors que 100 % des sprites de jeu sont en PNG pixel art nommé/rangé
   précisément (`docs/STYLE_GUIDE.md` §8). L'UI est donc le seul pan visuel du jeu qui n'a
   jamais reçu de traitement pixel art dédié — d'où l'impression de "maquette" plaquée sur un
   jeu pixel art fini.

En résumé chiffré : **7 écrans différents, 1 seule recette de cadre** (fond quasi-invisible +
liseré plat uniforme + coin rond flou + focus = couleur qui change), zéro asymétrie, zéro
texture. C'est ce qui lit comme "généré par défaut".

---

## 2. Le parti pris — "plaque blindée octogonale"

Chaque cadre d'UI devient une plaque de métal aux **quatre coins chanfreinés** (jamais arrondis)
avec un bevel intérieur qui reprend **exactement** la direction de lumière déjà actée pour tous
les sprites de jeu (`LIGHT_DIR` haut-gauche 45°, `tools/pseudo3d_lib.py`) — l'interface et le
monde partagent la même physique de lumière, ce qui n'a jamais été le cas jusqu'ici. Deux rivets
d'angle ancrent le cadre comme une pièce assemblée/boulonnée plutôt que dessinée, en écho direct
au lore cyborg (chair + métal greffés) plutôt qu'à un style purement décoratif. L'état **focus**
est le seul à recevoir un liseré vivant pulsé — un filet fin de couleur Aether qui semble
s'infiltrer le long d'un bord, comme la Rouille Vivante contaminant une plaque morte — rendant
cet état reconnaissable par la forme ET le mouvement ET la couleur, jamais par la couleur seule.

---

## 3. Spécifications exactes

### 3.0 Palette dérivée (nouvelle — dérivée des 4 teintes imposées, aucune teinte franche ajoutée)

| Rôle | Hex | Dérivation |
|---|---|---|
| Acier plaque (fill de base des cadres) | `#242440` | Légèrement plus clair que le fond `#1A1A2E` — **volontaire** : le cadre doit se détacher du fond, pas s'y fondre (cf. diagnostic §1.4) |
| Acier highlight (bevel haut/gauche) | `#3A3A5C` | `shade(#242440, "highlight")` — même formule HSV que `pseudo3d_lib.shade()` (×1.35 V, ×0.85 S) |
| Acier shadow (bevel bas/droite) | `#121223` | `shade(#242440, "shadow")` (×0.55 V, ×1.10 S) |
| Acier contact (ligne de pose au sol / trait externe) | `#0B0B16` | `shade(#242440, "contact")` (×0.35 V, ×1.15 S) |
| Accent nav (cyan) | `#44FFEE` | Existant, inchangé |
| Accent primaire/Aether/focus (violet) | `#AA44FF` | Existant, inchangé |
| Accent récompense/économie (or) | `#FFCC44` | Existant, inchangé |
| Accent danger/destructif (ambre sombre) | `#997A1E` | `shade(#FFCC44, "shadow")` — même hue que l'or (44°), assombri : différencie "Acheter" (or vif) de "Réinitialiser/Quitter" (ambre sourd) sans introduire de rouge/orange hors charte |
| Rarité commun / rare / épique | `#AAAAAA` / `#44AAFF` / `#CC44FF` | Déjà actées `docs/STYLE_GUIDE.md` §1.4 — inchangées, hors scope de ce brief |
| Texte | `#D9D9F2` | Existant, inchangé — jamais utilisé comme couleur de cadre |

### 3.1 Anatomie commune du cadre "plaque" (boutons, cartes, popups)

Bande de cadre = **16 px** de large (de l'arête externe à la zone de contenu), en 4 couches
empilées de l'extérieur vers l'intérieur :

| Couche | Épaisseur | Couleur | Règle |
|---|---|---|---|
| Contact (ligne de pose) | 1 px | `#0B0B16` @ 70 % alpha | Sur les 4 côtés, uniforme — ancre visuellement la plaque sur le fond sombre |
| Bevel acier | 3 px | `#3A3A5C` (côtés haut + gauche) / `#121223` (côtés bas + droite) | Reprend `LIGHT_DIR` — c'est cette bande qui donne la lecture "métal biseauté" |
| Plaque (fill) | 8 px | `#242440` @ alpha selon état (voir §3.2) | Le "corps" de la plaque, quasi tout l'espace visible |
| Liseré accent | 3 px | Couleur de catégorie (voir §3.0), opacité selon état | Bande la plus proche du contenu — **jamais** toute la bordure comme aujourd'hui, un simple filet |
| Séparateur interne | 1 px | `#0B0B16` @ 40 % | Coupe le liseré accent du texte/icône pour que le glow ne bave pas sur le contenu |

**Coins chanfreinés (les 4)** : coupe à 10 px depuis chaque sommet de coin (le long des deux
axes), teinte de la coupe = bevel du coin correspondant (coin haut-gauche = highlight `#3A3A5C`
le plus lumineux du cadre ; coin bas-droit = shadow `#121223` le plus sombre ; coins haut-droit
et bas-gauche = `#242440` neutre). **Jamais de `corner_radius` Godot** sur ces familles — angle
droit ou chanfrein uniquement, aucun arrondi.

**Rivets** : 2 rivets de 3×3 px (steel highlight/shadow, mini-bevel identique à la table
ci-dessus) placés sur les coins **haut-gauche et bas-droit** (les deux coins éclairés/ombrés,
les plus "structurels" visuellement), inset de 4 px depuis l'intersection du chanfrein. Absents
sur les éléments < 64 px de large (ex. boutons drapeaux 44×30 px) — trop petit, deviendrait du
bruit (cf. règle de lisibilité `ART_BRIEF_PSEUDO3D.md` §5 icônes UI, même logique de repli).

**Bord "soudé" épais (asymétrie)** :
- **Boutons / chips / cartes** → bord **bas** renforcé à **22 px** au lieu de 16 (ajout de 6 px
  de plaque pleine `#242440`, pas de détail supplémentaire) — lit comme "monté depuis le bas".
- **Popups / modales** → bord **haut** renforcé à **22 px** — lit comme "hublot/panneau
  suspendu depuis le haut", cohérent avec un titre de popup toujours en haut.
- **Panneaux de fond d'écran** → pas de bord soudé (famille non chanfreinée, voir §3.3).

### 3.2 Bouton — états normal / hover / pressed / focus / disabled

| État | Fill plaque (alpha) | Bevel | Liseré accent | Effet additionnel |
|---|---|---|---|---|
| **Normal** | `#242440` @ 85 % | tel quel | catégorie @ 55 % alpha | — |
| **Hover** | `#242440` @ 95 %, teinté ×1.15 V (léger éclat) | tel quel | catégorie @ 85 % alpha | `PivotOffset`/scale ×1.04 déjà en place (`OnButtonMouseEntered`) — **conservé** |
| **Pressed** | `#242440` @ 100 %, teinté ×0.8 V (enfoncé) | bevel **inversé** (highlight↔shadow échangés — lit comme "plaque pressée vers l'intérieur") | catégorie @ 100 % alpha | pas de scale-up (déjà géré ailleurs) |
| **Focus** | identique à Hover | identique à Hover | catégorie @ 100 %, **+ liseré vivant pulsé** : alpha oscille 60→100 %, cycle 0,6 s (même pattern que le clignotement des implants HP critique, `STYLE_GUIDE.md` §2.3) | `expand_margin_*` = +3 px sur les 4 côtés (le cadre focus déborde légèrement du bouton — signal de forme, pas seulement de couleur), scale ×1.04 conservé |
| **Disabled** | `#242440` @ 40 %, désaturé (bevel remplacé par un gris neutre unique `#33334A`, plus de highlight/shadow différencié) | plat, pas de bevel | liseré accent désaturé à 50 % de sa saturation d'origine, alpha 30 % | pas de rivets visibles (masqués sous l'alpha réduit) |

Le focus reste ainsi identifiable par **3 signaux indépendants et cumulés** (débordement de
forme, pulsation, opacité maximale du liseré) — jamais par la seule teinte, conformément à la
contrainte d'accessibilité.

### 3.3 Panneau de fond d'écran

Cette famille (grands conteneurs passifs : liste d'améliorations du Hub, colonne du Codex,
panneau d'Options) **reste 100 % `StyleBoxFlat`, sans texture, sans chanfrein** — c'est
volontairement la famille la plus calme de la hiérarchie (contenant vs. contenu actionnable) :

- `corner_radius = 0` (angle droit strict — fin de l'arrondi flou, cf. diagnostic §1.3)
- `bg_color = #1A1A2E @ 88 %` — **distinct** du fill "plaque" des boutons/cartes (`#242440`),
  légèrement plus sombre que le fond d'écran (pas identique comme aujourd'hui) pour lire comme
  un renfoncement, pas une simple superposition
- Bordure **par côté indépendant** (Godot `border_width_left/top/right/bottom`, pas
  `SetBorderWidthAll`) : haut + gauche = 1 px `#3A3A5C` (highlight), bas + droite = 1 px
  `#121223` (shadow) — le même bevel HSV que les plaques, juste sans chanfrein ni liseré accent
  saturé (garde ce niveau de hiérarchie visuellement en retrait)
- `content_margin` : 16 px sur les 4 côtés

Ce traitement ne nécessite **aucun nouvel asset** — c'est un changement de code pur (remplacer
`SetCornerRadiusAll`/`SetBorderWidthAll` par les valeurs par côté ci-dessus) livrable en
quelques minutes et qui, à lui seul, supprime déjà l'essentiel du symptôme "panneau qui se fond
dans le fond" (diagnostic §1.4).

### 3.4 Popup / modale (LevelUpScreen, AssimilationScreen, PauseScreen)

Base = anatomie §3.1 (plaque chanfreinée complète, texture 9-slice), avec :

- Bande de cadre élargie à **20 px** (au lieu de 16) — une popup est vue de plus loin/plus
  grande à l'écran, une bande trop fine y perdrait sa lisibilité de "cadre lourd"
- Chanfrein élargi à **14 px**
- Bord soudé épais **en haut** (22 → **28 px** sur cette famille, cf. §3.1)
- Rivets sur les coins **haut-gauche et haut-droit** (pas bas-droit ici — cohérent avec le bord
  soudé en haut, les deux coins "porteurs" sont en haut)
- **Ombre portée dure** (pas de flou gaussien, cf. contrainte "rendu net") : un second
  `Panel`/`ColorRect` identique en silhouette, rempli `#000000 @ 45 %`, décalé de **+6 px x /
  +6 px y**, dessiné **avant** (sous) le panneau principal — un décalage "hard shadow" à la
  Game Boy/rétro, cohérent avec l'ombre portée elliptique déjà actée pour les sprites
  (`ART_BRIEF_PSEUDO3D.md` §3), juste rectangulaire ici (pas d'ellipse pour un cadre UI)
- Fill plaque à `#242440 @ 96 %` (plus opaque que les boutons — une popup doit occulter
  franchement ce qu'il y a derrière, elle bloque l'action)

### 3.5 Carte sélectionnable (cartes de level-up, chips perk/titre, entrées de codex)

Base = anatomie §3.1 standard (bande 16 px, chanfrein 10 px, bord soudé bas 22 px), avec :

- Le **liseré accent = couleur de rareté** pour les cartes de level-up (`#AAAAAA` /
  `#44AAFF` / `#CC44FF`, inchangées) ; **couleur de catégorie** (violet perk / or titre) pour
  les chips du Hub — reprend exactement la sémantique déjà en place, seule la forme change
- État **sélectionné** (équivalent focus pour un chip déjà choisi, hors navigation clavier) :
  liseré accent figé à 100 % (pas de pulsation — la pulsation est réservée à la navigation
  active, sinon toutes les cartes équipées clignoteraient en permanence, ce qui serait un bruit
  visuel constant et contredirait la règle de lisibilité). Utiliser à la place un **triangle
  plein 6×6 px** en coin haut-droit dans l'accent, coupé par le chanfrein — un badge de coche
  discret et permanent
- Rareté **épique uniquement** : un détail optionnel (P3, non bloquant) — une micro-fissure de
  2 px partant du coin bas-gauche vers le centre, teintée liseré violet à faible alpha (20 %) :
  clin d'œil à la Rouille Vivante/Aether qui "infiltre" un objet rare, cohérent avec le thème de
  l'Assimilation. À ne produire qu'après validation du reste — pur bonus narratif, jamais au
  détriment de la lisibilité

### 3.6 Séparateurs et titres de section

Aucune texture requise — 100 % `ColorRect` :

- **Séparateur horizontal** : `ColorRect` 2 px de haut, couleur = accent de contexte de l'écran
  (cyan par défaut) à 60 % alpha, largeur = largeur du conteneur parent moins 2× 12 px de marge.
  Deux "ticks" de coin : `ColorRect` 4×4 px pleine couleur accent à 100 % alpha, positionnés
  aux deux extrémités du séparateur, 2 px sous la ligne — remplace le `HSeparator` neutre
  (diagnostic §1.6) sans nécessiter de nouvel asset
- **Titre de section** (ex. "MISSION", "JOUEUR" dans PauseScreen, ou tout header de sous-bloc) :
  Label existant inchangé (police, taille, couleur) **+** le séparateur ci-dessus systématique
  juste en dessous — actuellement ces sections utilisent parfois un `HSeparator` nu, parfois
  rien du tout ; la règle devient : tout titre de section est TOUJOURS suivi de ce séparateur
- **Titre d'écran (H1)** (ex. "HUB — Améliorations Permanentes") : sous le texte existant,
  double-trait au lieu du simple `HSeparator` actuel : `ColorRect` 2 px accent @ 90 % + `ColorRect`
  1 px `#121223` (steel shadow) 2 px en dessous — lit comme un soulignement gravé, pas une
  ligne HTML

---

## 4. StyleBoxFlat pur vs. texture 9-slice — répartition honnête

| Famille | Chanfrein / rivets nécessaires ? | Verdict |
|---|---|---|
| **Bouton** (menu, Acheter, actions) | Oui (chanfrein + rivets + bevel inversé au pressed) | **9-slice PNG obligatoire.** `StyleBoxFlat` ne sait ni couper un coin en diagonale, ni poser un rivet discret — seul un `corner_radius` (arrondi) existe côté vectoriel |
| **Carte sélectionnable** | Oui (même anatomie que bouton) | **9-slice PNG obligatoire**, mêmes textures que les boutons réutilisées (même bande 16 px, chanfrein 10 px) — seule la couleur de liseré change, donc génération paramétrée (§5), pas de nouveau dessin par carte |
| **Popup / modale** | Oui (chanfrein élargi 14 px + rivets haut + ombre dure décalée) | **9-slice PNG obligatoire**, gabarit dédié plus grand (bande 20 px vs 16) |
| **Panneau de fond d'écran** | Non (angle droit strict, décision §3.3) | **100 % `StyleBoxFlat`** — `corner_radius=0` + bordures par côté indépendantes (`border_width_left/top/right/bottom`), zéro nouvel asset |
| **Séparateurs / titres** | Non | **100 % `ColorRect`** — zéro nouvel asset |

Conclusion : sur les 5 familles demandées, **3 nécessitent une texture 9-slice** (bouton, carte,
popup) et **2 sont livrables en pur code** (panneau, séparateurs) — celles-ci peuvent démarrer
**immédiatement**, sans attendre le graphiste (voir priorités §6).

---

## 5. Génération des textures 9-slice — script à produire

Un seul script `tools/generate_ui_frames.py` (nouveau, à créer par `graphiste`), qui **importe
`tools/pseudo3d_lib.py`** pour la fonction `shade()` (dérivation HSV highlight/shadow/contact
identique à celle des sprites — ne pas dupliquer la logique de teinte) et génère toute la
matrice par paramétrage, plutôt que des fichiers peints à la main un par un.

**Paramètres de la fonction génératrice** : `family` (`button` | `card` | `popup`), `accent_hex`
(couleur du liseré), `band_px` (16 ou 20 selon famille), `chamfer_px` (10 ou 14), `weld_side`
(`bottom` | `top` | `none`), `weld_extra_px` (6 pour boutons/cartes, 12 pour popups), `state`
(`normal` | `focus` | `disabled`).

**Canvas et marges 9-slice** (à régler dans Godot via `StyleBoxTexture.texture_margin_*`) :

| Famille | Canvas PNG | Marge 9-slice (4 côtés) | Zone centrale répétable |
|---|---|---|---|
| Bouton / Carte | 48×48 px | 16 px | 16×16 px (fill plein, s'étire) |
| Popup / modale | 56×56 px | 20 px | 16×16 px |

**Astuce d'implémentation (zéro art supplémentaire pour le bord soudé)** : ne PAS générer de
texture séparée pour l'état "bord épais" — dans Godot, régler `texture_margin_bottom = 22`
(bouton/carte) ou `texture_margin_top = 28` (popup) au lieu de la valeur symétrique par défaut.
Comme la zone au-delà du chanfrein (au-delà des premiers 10-14 px) est déjà un fill plat dans le
PNG source, étirer la marge dans cette zone donne visuellement un bord plus épais **sans pixel
supplémentaire à dessiner** — un seul PNG par (famille, accent, état) suffit pour les variantes
symétrique et asymétrique.

**Matrice de fichiers à produire** (nommage suivant la convention `docs/STYLE_GUIDE.md` §8.3,
préfixe `ui_frame_`) :

```
assets/sprites/ui/frames/
├── ui_frame_button_cyan.png          # normal/hover/pressed via Modulate runtime (voir §3.2)
├── ui_frame_button_cyan_focus.png
├── ui_frame_button_violet.png
├── ui_frame_button_violet_focus.png
├── ui_frame_button_or.png
├── ui_frame_button_or_focus.png
├── ui_frame_button_danger.png        # accent ambre sombre #997A1E
├── ui_frame_button_danger_focus.png
├── ui_frame_button_disabled.png      # 1 seul fichier, partagé par tous les accents
├── ui_frame_card_common.png / _rare.png / _epic.png (+ _focus pour chacun)
├── ui_frame_popup_violet.png         # PauseScreen, AssimilationScreen (défaut violet Aether)
├── ui_frame_popup_cyan.png           # LevelUpScreen ou tout popup non-Aether
└── ui_frame_popup_disabled.png       # non utilisé en pratique, généré pour cohérence de script
```

Total : **9 textures bouton + 8 textures carte + 3 textures popup = 20 fichiers PNG**, tous
générés par le même script paramétré — pas 20 sessions de dessin manuel.

**Focus = normal + pulsation runtime**, jamais entièrement "baked" (une image statique ne peut
pas pulser) : le fichier `_focus.png` fixe la géométrie maximale (liseré large, expand_margin) ;
l'oscillation d'alpha 60→100 % sur 0,6 s est un `Tween`/`AnimationPlayer` sur `self_modulate:a`
côté `developpeur`, déclenché sur `FocusEntered` et stoppé sur `FocusExited` — exactement le
mécanisme déjà en place pour le clignotement des implants HP critique
(`docs/STYLE_GUIDE.md` §2.3), à répliquer ici plutôt qu'à réinventer.

**Lot de test avant génération complète** (même discipline que `ART_BRIEF_PSEUDO3D.md` §7) :
produire d'abord `ui_frame_button_violet.png` + `_focus.png` + `ui_frame_popup_violet.png`,
les intégrer sur UN bouton du menu principal et sur `PauseScreen`, valider à l'écran (pas
seulement zoomé dans l'éditeur) que :
1. Le chanfrein est net (pas de flou d'anti-aliasing) à la résolution de jeu 1280×720.
2. Le focus reste identifiable en < 150 ms sans avoir à comparer côte à côte avec l'état normal.
3. Le bevel du bouton et celui d'un sprite de jeu proche (ex. icône d'arme) donnent la même
   impression de direction de lumière (haut-gauche clair).
Si un de ces 3 tests échoue, corriger le script avant de générer les 20 fichiers.

---

## 6. Ordre d'implémentation priorisé

1. **(Jour 1, zéro art, code seul — ~40 % de l'effet)** Panneaux de fond d'écran §3.3
   (`corner_radius=0` + bordures par côté) partout où `PanelBg`/panels génériques existent
   (`CodexScreenBase`, `HubScreen` liste, `OptionsScreen`) + séparateurs/titres §3.6
   (`ColorRect` à la place des `HSeparator`). Change déjà la lecture générale de "template" à
   "interface qui a une matière", sans attendre de nouvel asset.
2. **(Lot de test, §5)** Bouton violet + focus + popup violet — valider les 3 checks avant
   d'aller plus loin.
3. **(80 % de l'effet atteint ici)** Génération complète des 9 textures bouton (§5) +
   câblage sur `MainMenu`, `HubScreen` (Acheter, chips, reset), `CodexScreenBase`,
   `OptionsScreen` — c'est l'élément le plus répété à l'écran, donc celui qui porte le plus
   l'identité visuelle une fois réglé.
4. Textures popup (§3.4) sur `PauseScreen`, `AssimilationScreen`, tout écran modal bloquant.
5. Textures carte (§3.5) sur `LevelUpScreen` (cartes de choix) et chips perk/titre du Hub —
   dernier car ce sont des éléments déjà bien différenciés par couleur de rareté, le gain
   marginal est plus faible que §1-4.
6. (P3, optionnel, non bloquant) Détail fissure épique §3.5, timing narratif à valider avec
   `story-teller` avant production.

---

*Ce brief est actif à partir de sa validation par `directeur-artistique`. Toute divergence
constatée en production (ex. une famille où le chanfrein casse la lisibilité à petite taille)
doit être remontée avant intégration définitive — même règle d'arbitrage que
`ART_BRIEF_PSEUDO3D.md` §8.4.*
