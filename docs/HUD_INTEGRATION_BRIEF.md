# HUD INTEGRATION BRIEF — CHIMERA PROTOCOL
## Direction artistique — 2026-06-25

> Produit par `directeur-artistique` sur la base de l'analyse du concept `idea/idee_hud_chimera_core.png`
> et de la lecture du HUD actuel (`scenes/ui/HUD.tscn` / `src/UI/HUD.cs`).
> Ce brief est destiné à `graphiste` pour modification du HUD en C# + .tscn.
> Toutes les décisions ci-dessous sont cohérentes avec `docs/STYLE_GUIDE.md` et `docs/GDD.md` §12, §16.

---

## 1. Analyse du concept : ce que montre l'image

L'image `idea/idee_hud_chimera_core.png` est un concept haute résolution (style "terminal hacker")
qui présente cinq zones :

- **Haut-gauche** : panneau "HACKER RIG STATUS" — titre en petites majuscules sur fond sombre
  avec cadre tech hachuré, barre HP labellée "140 / 140", section "MEMORY BUFFER" avec une barre
  XP segmentée en blocs rectangulaires colorés + sous-label "LV1 | 0/0 (XP: DATA_STREAM)",
  indicateur de niveau hexagonal "LV1", et deux boutons hexagonaux "PING" / "FIREWALL" en bas.
- **Haut-centre** : double affichage du timer — un petit format "14:57" sur fond tech + un grand
  LCD "14:57" sur panneau séparé, sous-label "RUNTIME ENCRYPTED".
- **Haut-droite** : panneau "LOOT COUNTER / CRYPTO-RESOURCES" — icône hexagonale centrale
  "CHIMERA CORE" entourée de compteurs SCRAP (125), CHIPS (3), SCRAP (0).
- **Milieu-droite** : mini-map radar circulaire avec points de couleur (ennemis).
- **Bas-droite** : panneau "SKILL TREE / UPGRADE PREVIEW" avec arbre de compétences.

Le concept est graphiquement cohérent avec notre univers — il renforce l'identité "terminal
cyberpunk" et l'opposition matière morte / énergie vivante. Le cadre tech hachuré, les
titres de panneau et la barre XP segmentée sont les trois emprunts les plus précieux.

---

## 2. Ce qu'il FAUT intégrer (MVP — obligatoire)

### 2.1 Titre de panneau "CHIMERA PROTOCOL" en petites majuscules

Le concept introduit une convention forte : chaque panneau HUD a un titre fonctionnel
en petites majuscules (`font_size 9`) sur une bande horizontale sombre séparée du contenu.
C'est la décision la plus impactante visuellement pour un coût d'implémentation minimal.

**Adapation du nom** : ne pas utiliser "HACKER RIG STATUS" (trop "hacking/cyber", éloigné
du lore Arpenteur/Sanctuaire). Utiliser **"CHIMERA PROTOCOL"** — c'est le titre du jeu,
c'est le protocole d'exploration que l'Arpenteur exécute. Cohérent avec le lore §3.

**Implémentation** : ajouter un `Label` en haut du `StatsPanelBg`, texte "CHIMERA PROTOCOL",
`font_size 9`, couleur `Color(0.267, 1.0, 0.933, 0.55)` (cyan à 55% alpha — discret, lisible).
Décaler vers le bas tous les nœuds actuels du panneau de +14 px pour faire de la place.

### 2.2 Barre XP segmentée en blocs

La barre XP plate actuelle est fonctionnelle mais manque de caractère. Le concept montre
une barre divisée en segments rectangulaires avec des "cuts" entre chaque bloc — effet
"énergie stockée par unités". C'est réalisable en C# pur avec plusieurs `ColorRect`.

Voir spécifications complètes en §5.3.

### 2.3 Sous-label XP reformaté

Actuellement : `"37 / 95 XP"` — purement numérique.
Cible : `"LV 1 | 37 / 95 XP"` — inclure le niveau dans le sous-label pour que l'info
soit lisible d'un coup d'oeil même si le label `LevelLabel` est masqué.

Note : le `LevelLabel` existant (cyan "LV 1") reste présent — le sous-label sert de
lecture secondaire plus complète.

### 2.4 Titre de panneau "NOYAUX" haut-droite

Même convention que 2.1 : ajouter un `Label` "NOYAUX" ou "CHIMERA CORES" au-dessus
du compteur ⬡ existant. `font_size 9`, couleur violet `Color(0.667, 0.267, 1.0, 0.55)`.

Décaler le panneau `CoresBg` vers le bas de 14 px pour accueillir ce titre.

### 2.5 Sous-label timer "RUNTIME ACTIVE"

Le concept montre "RUNTIME ENCRYPTED" sous le grand timer LCD. Adapter en **"RUNTIME ACTIVE"**
(neutre, toujours vrai — "ENCRYPTED" implique un gameplay de décryptage absent du jeu).
`font_size 9`, blanc cassé `Color(0.85, 0.85, 0.95, 0.45)`. Affiché sous le `TimerLabel`.

---

## 3. Ce qu'il NE FAUT PAS intégrer (hors scope ou nuisible)

### 3.1 Mini-map radar

Hors scope MVP — aucun système de tracking spatial n'existe. La carte est petite (1920×1216 px),
le joueur voit l'essentiel sans radar. A évaluer uniquement si l'arène est agrandie significativement.

### 3.2 Boutons PING / FIREWALL

Les systèmes correspondants n'existent pas. Ajouter des boutons non-fonctionnels dégrade
l'expérience joueur. Ces slots hexagonaux pourraient accueillir à terme les cooldowns d'armes
actives si des armes actives sont ajoutées — conception à faire à ce moment-là.

### 3.3 Panneau "SKILL TREE / UPGRADE PREVIEW"

L'arbre de compétences en run n'est pas dans le scope MVP. Si ce système est ajouté,
il mérite son propre écran dédié (pas une intégration HUD permanente qui consomme
de l'espace écran pendant le combat).

### 3.4 Double affichage du timer (petit + grand)

Le concept montre deux panneaux timer superposés — le petit au-dessus du grand. C'est une
redondance visuelle qui consomme de l'espace central précieux. Conserver un seul timer
centré, grand format. Le concept confirme que notre disposition actuelle est la bonne —
il faut seulement ajouter le sous-label "RUNTIME ACTIVE".

### 3.5 Compteurs SCRAP / CHIPS

Ces ressources n'existent pas dans le système actuel. Le seul compteur de run pertinent
est les Noyaux d'Aether (⬡). Ne pas ajouter de compteurs fantômes.

### 3.6 Cadres tech hachurés complexes

Le concept utilise des cadres avec textures hachurees, coins arrondis, dégradés. En pixel art
1280×720, ces détails disparaissent ou créent du bruit visuel. Notre convention actuelle
(fond uni + barre d'accent couleur latérale) est plus lisible et cohérente avec le style du jeu.

### 3.7 Icône hexagonale "CHIMERA CORE" avec texture interne

L'icône violet-hexagone du concept est belle en haute résolution mais demande un sprite
PNG dédié (32×32 ou 24×24) dont la production est hors scope immédiat. L'emoji ⬡ actuel
est une solution acceptable MVP. Si un sprite est produit ultérieurement, le brief sera émis séparément.

---

## 4. Adaptations de taille et de police

### 4.1 Contexte viewport

Le viewport de jeu est environ 1280×720 pixels. Le concept est probablement dessiné en
1920×1080 ou plus. Appliquer un facteur de 0.67 à tous les éléments si la taille paraît trop grande.

Les tailles actuelles du HUD (panel 300×116 px, font 14px, barres 18/12px) sont correctement
calibrées pour 1280×720. Les additions de ce brief doivent rester dans ces enveloppes.

### 4.2 Tailles de police pour les ajouts

| Élément | Font size | Couleur | Justification |
|---|---|---|---|
| Titre panneau gauche "CHIMERA PROTOCOL" | 9 px | Cyan 55% alpha | Discret, informatif, pas compétitif |
| Titre panneau droite "CHIMERA CORES" | 9 px | Violet 55% alpha | Cohérence avec panneau gauche |
| Sous-label timer "RUNTIME ACTIVE" | 9 px | Blanc cassé 45% alpha | Ambiance, pas critique |
| Sous-label XP reformaté | 11 px | Cyan-vert 55% alpha | Conserve la lisibilité actuelle |

Ne pas dépasser 11 px pour les labels secondaires HUD. La lisibilité en combat dense
(fond vignette sombre, beaucoup d'entités) dépend de la hiérarchie typographique :
les éléments principaux (HP, timer, niveau) doivent dominer visuellement.

### 4.3 Ajustement des hauteurs de panneaux

Le panneau gauche actuel : `offset_top=8, offset_bottom=116` = 108 px de haut.
Avec le titre de panneau (+14 px) : nouveau `offset_bottom=130`.

Le panneau droite actuel : `offset_top=8, offset_bottom=46` = 38 px de haut.
Avec le titre de panneau (+14 px) : nouveau `offset_bottom=60`.

---

## 5. Layout précis — positions et tailles pixel par pixel

### 5.1 Panneau stats gauche (modifications)

**Nœuds à modifier dans `HUD.tscn` :**

```
StatsPanelBg     : offset_bottom = 130.0  (était 116.0)
AccentBar        : offset_bottom = 130.0  (était 116.0)
```

**Nœuds à ajouter (insérer AVANT HpHeader dans l'ordre du .tscn) :**

```
[node name="StatsPanelTitle" type="Label" parent="."]
offset_left   = 18.0
offset_top    = 11.0
offset_right  = 306.0
offset_bottom = 24.0
text          = "CHIMERA PROTOCOL"
theme_override_font_sizes/font_size = 9
theme_override_colors/font_color = Color(0.267, 1.0, 0.933, 0.55)

[node name="StatsPanelTitleSep" type="ColorRect" parent="."]
offset_left   = 18.0
offset_top    = 25.0
offset_right  = 306.0
offset_bottom = 26.0
color         = Color(0.267, 1.0, 0.933, 0.12)
```

**Décalage de tous les nœuds existants du panneau gauche de +18 px vers le bas :**

```
HpHeader     : offset_top = 32.0   (était 14.0),  offset_bottom = 52.0  (était 34.0)
HpBarBg      : offset_top = 54.0   (était 36.0),  offset_bottom = 72.0  (était 54.0)
HpXpSeparator: offset_top = 76.0   (était 58.0),  offset_bottom = 78.0  (était 60.0)
XpRow        : offset_top = 83.0   (était 65.0),  offset_bottom = 100.0 (était 82.0)
XpSubLabel   : offset_top = 103.0  (était 85.0),  offset_bottom = 128.0 (était 112.0)
```

### 5.2 Reformatage du sous-label XP

Dans `HUD.cs`, méthode `UpdateXpBar()` — modifier la ligne qui assigne `_xpSubLabel.Text` :

```csharp
// Avant :
_xpSubLabel.Text = $"{XpSystem.Instance.CurrentXp} / {XpSystem.Instance.XpToNextLevel} XP";

// Après :
_xpSubLabel.Text = $"LV {XpSystem.Instance.CurrentLevel}  |  {XpSystem.Instance.CurrentXp} / {XpSystem.Instance.XpToNextLevel} XP";
```

Attention : le `LevelLabel` reste présent et se met toujours à jour — les deux sont complémentaires.
Si la lisibilité du sous-label est insuffisante en test, augmenter son alpha de 0.55 à 0.70.

### 5.3 Barre XP segmentée — spécification d'implémentation C#

La barre XP actuelle est un seul `ColorRect`. La remplacer par une barre segmentée.

**Principe** : au lieu d'un seul ColorRect, générer N petits ColorRect en C# dans `HUD.cs`,
chacun représentant un segment. Les segments remplis sont cyan, les vides sont sombres.
Les "coupures" entre segments sont des espaces de 1 px.

**Paramètres** :
- Nombre de segments : **20 segments fixes** (indépendant du niveau — représente la progression
  vers le prochain niveau, pas le niveau absolu).
- Largeur totale disponible : identique à `_xpBarBg.Size.X` (résolu au runtime, ~230 px).
- Hauteur de segment : **12 px** (identique à la barre actuelle).
- Espacement entre segments : **1 px**.
- Couleur segment rempli : `Color(0.267, 1.0, 0.933, 1.0)` (cyan — identique à la barre actuelle).
- Couleur segment vide : `Color(0.06, 0.06, 0.12, 1.0)` (fond sombre — identique au fond actuel).
- Glow : **conserver le `XpBarGlow` actuel** (ColorRect pleine largeur à 10% alpha) — il
  reste derrière les segments et donne l'ambiance.

**Implémentation dans `HUD.cs`** :

Ajouter un champ `private ColorRect[] _xpSegments = Array.Empty<ColorRect>();`
et une constante `private const int XpSegmentCount = 20;`.

Dans `_Ready()`, après la résolution du layout (utiliser `CallDeferred` ou un flag d'init),
créer les 20 segments et les ajouter comme enfants de `XpRow/XpBarBg`.

Dans `UpdateXpBar()`, calculer `int filledSegments = Mathf.RoundToInt(ratio * XpSegmentCount)`
et mettre à jour la couleur de chaque segment.

**Attention aux chemins de nœuds** : la barre XP principale `_xpBar` (l'ancien `ColorRect` remplissant)
est référencée dans `OnLevelUp()` pour le flash de surexposition. Si elle est supprimée, transférer
cet effet sur le premier segment ou sur le `XpBarGlow`. Recommandation : **conserver `_xpBar` comme
ColorRect invisible (Size.X = 0) uniquement pour le flash de level-up**, et superposer les segments
par-dessus. Cela évite de réécrire `OnLevelUp()`.

### 5.4 Panneau noyaux haut-droite (modifications)

**Nœuds à modifier :**

```
CoresBg        : offset_top = 8.0,  offset_bottom = 62.0  (était 46.0 — +16 px)
CoresBgAccent  : offset_top = 8.0,  offset_bottom = 62.0  (était 46.0 — +16 px)
CoresContainer : offset_top = 24.0, offset_bottom = 62.0  (était 8.0/46.0 — décalé de +16 px)
```

**Nœuds à ajouter (insérer AVANT CoresContainer dans l'ordre du .tscn) :**

```
[node name="CoresPanelTitle" type="Label" parent="."]
anchor_left   = 1.0
anchor_right  = 1.0
offset_left   = -184.0
offset_top    = 11.0
offset_right  = -8.0
offset_bottom = 23.0
text          = "CHIMERA CORES"
horizontal_alignment = 1
theme_override_font_sizes/font_size = 9
theme_override_colors/font_color = Color(0.667, 0.267, 1.0, 0.55)

[node name="CoresPanelTitleSep" type="ColorRect" parent="."]
anchor_left   = 1.0
anchor_right  = 1.0
offset_left   = -192.0
offset_top    = 24.0
offset_right  = -8.0
offset_bottom = 25.0
color         = Color(0.667, 0.267, 1.0, 0.12)
```

### 5.5 Sous-label timer "RUNTIME ACTIVE"

**Nœuds à modifier :**

```
TimerBg        : offset_bottom = 70.0  (était 58.0 — +12 px)
TimerAccentL   : offset_bottom = 70.0  (était 58.0)
TimerAccentR   : offset_bottom = 70.0  (était 58.0)
```

Le `TimerLabel` existant reste inchangé (offset_top=8, offset_bottom=58 — centré verticalement
dans l'ancienne hauteur, les 12 px ajoutés sont sous lui pour le sous-label).

**Nœud à ajouter (après TimerLabel dans l'ordre du .tscn) :**

```
[node name="TimerSubLabel" type="Label" parent="."]
anchor_left   = 0.5
anchor_right  = 0.5
offset_left   = -72.0
offset_top    = 56.0
offset_right  = 72.0
offset_bottom = 70.0
text          = "RUNTIME ACTIVE"
horizontal_alignment = 1
vertical_alignment   = 1
theme_override_font_sizes/font_size = 9
theme_override_colors/font_color = Color(0.85, 0.85, 0.95, 0.45)
```

---

## 6. Palette — cohérence et dérives à éviter

### 6.1 Couleurs du concept compatibles avec notre palette

| Couleur concept | Correspondance STYLE_GUIDE | Usage dans ce brief |
|---|---|---|
| Cyan électrique titre "HACKER RIG STATUS" | `#44FFEE` (Aether secondaire UI) | Titre panneau gauche à 55% alpha |
| Violet contour panneau droite | `#AA44FF` (Noyau Aether) | Titre panneau droite à 55% alpha |
| Fond sombre panneau | `#0A0A0F` à 90% alpha | Identique à l'actuel — aucun changement |
| Blanc cassé texte numérique | `#D9D9F2` | Identique à l'actuel — aucun changement |
| Blocs XP remplis cyan | `#44FFEE` | Segments XP remplis |

### 6.2 Couleurs du concept à NE PAS adopter

| Couleur concept | Problème | Alternative |
|---|---|---|
| Fond hachuré turquoise-vert des cadres | Trop saturé, concurrence les entités en jeu | Conserver fond `#0A0A0F` uni |
| Vert clair des bords de panneau | S'approche du `#AAFF44` des orbes XP — confusion visuelle | Conserver cyan `#44FFEE` |
| Gris-bleu moyen `~#557799` des cadres tech | Trop lumineux pour un panneau HUD, attire l'oeil hors des infos critiques | Non utilisé |
| Magenta `#FF00CC` Aether chaud | Réservé aux fusions évoluées (Lame à Fusion) — ne doit pas apparaître dans le HUD permanent | Non utilisé dans ce brief |

### 6.3 Règle de lisibilité sur fond vignette

La vignette screen (`CanvasLayer` layer=90, opacité 0.72) assombrit les bords de l'écran.
Le HUD est en layer=95 (au-dessus) — il n'est pas affecté. Mais le contraste interne des
panneaux doit rester lisible sur fond sombre.

**Vérification obligatoire** : après intégration, tester en combat dense (>50 ennemis)
que les titres de panneau à 55% alpha restent lisibles. Si non, monter à 65% alpha.

---

## 7. Éléments graphiques à produire (PNG)

Pour ce brief MVP, **aucun PNG supplémentaire n'est requis**. Toutes les additions sont
réalisables avec `ColorRect` (séparateurs, fonds) et `Label` (textes).

Le cas de l'icône hexagonale "CHIMERA CORE" (élément le plus marquant du concept haut-droite)
est intentionnellement exclu : l'emoji ⬡ est suffisant pour le MVP, et si un sprite dédié
est décidé, un brief séparé sera produit par `directeur-artistique` avec les spécifications
pixel art exactes (24×24 px, palette violet §1.4, 3–4 frames pulse).

---

## 8. Ordre d'implémentation recommandé

Le graphiste doit implémenter dans cet ordre, du plus simple au plus risqué (régression
du HUD existant) :

1. **Sous-label timer** "RUNTIME ACTIVE" — ajout pur, aucune modification de nœud existant.
   Tester que le timer reste lisible après l'agrandissement du `TimerBg`.

2. **Titre panneau droite** "CHIMERA CORES" + décalage de `CoresContainer` — modification
   limitée à 3 nœuds + 2 ajouts.

3. **Titre panneau gauche** "CHIMERA PROTOCOL" + décalage de tous les nœuds du panneau —
   c'est le changement le plus risqué (6 nœuds décalés). Vérifier en jeu que la barre HP
   et la barre XP restent alignées après le décalage.

4. **Reformatage sous-label XP** dans `HUD.cs` — modification d'une seule ligne, très faible
   risque. Vérifier que le texte ne déborde pas (le panneau est élargi à 130 px de haut).

5. **Barre XP segmentée** — c'est le seul changement qui touche la logique C# de manière
   significative. Implémenter en dernier une fois que le layout est stable. Conserver
   `_xpBar` pour le flash level-up comme décrit en §5.3.

---

## 9. Ce que ce brief N'IMPLIQUE PAS pour les autres systèmes

- **`HUD.cs` — `UpdateXpBar()`, `UpdateHp()`, `UpdateTimer()`, `UpdateCores()`** : aucune
  logique de données ne change. Seuls les nœuds référencés et le texte du sous-label XP
  sont modifiés.
- **`WeaponNotifLabel`** : position et comportement inchangés.
- **Barre de vie mini-boss** : mentionnée dans GDD §19.2, elle sera intégrée dans un brief
  séparé quand le système mini-boss sera activé. Elle se positionne sous le timer (centré),
  n'interfère pas avec les modifications décrites ici.
- **`ScreenShake`, `FusionFlash`, `VignetteFollow`** : non affectés.
- **Layer HUD = 95** : inchangé, déjà correct.

---

## 10. Critères de validation DA

Le livrable sera validé si et seulement si :

1. Les cinq panneaux (stats gauche, timer centre, noyaux droite) restent lisibles en
   moins de 1 seconde à froid sur une capture d'écran en combat dense.
2. Aucun titre de panneau (font 9px) ne déborde de son conteneur ni ne recouvre une
   barre ou un label de données.
3. La hiérarchie visuelle est respectée : les valeurs numériques principales (HP, timer,
   niveau) dominent visuellement les titres et sous-labels secondaires.
4. Les couleurs des titres (cyan 55% / violet 55%) ne saturent pas l'écran — les panneaux
   doivent rester "discrets mais identifiés", pas "présents visuellement".
5. La barre XP segmentée donne le même signal de progression que l'ancienne barre continue,
   et le flash de level-up reste visible (surexposition `Color(3,3,3,1)` sur au minimum
   le segment le plus à droite ou le `XpBarGlow`).
6. Build 0 erreur 0 warning avant soumission.
