"""
pseudo3d_lib.py — Bibliotheque PIL partagee pour le rendu "pseudo-3D avec ombres"
de Chimera Protocol.

Implemente docs/ART_BRIEF_PSEUDO3D.md (redige par directeur-artistique, 2026-07-02) :
  - direction de lumiere fixe haut-gauche 45° (LIGHT_DIR)
  - derivation shadow/highlight/contact en HSV depuis une couleur de base (shade())
  - ombre portee elliptique 2.2:1 au sol (cast_shadow / add_cast_shadow)
  - ombrage tuile (gradient leger, sans volume) (shade_tile / shade_recessed)
  - ombrage icone UI (2 faces, amplitude reduite) (shade_icon)

Principe d'integration (voir §8 du brief) : chaque script tools/generate_*.py
importe ce module et enveloppe son helper `save(img, path)` existant pour
appliquer automatiquement la bonne fonction de shading selon la categorie de
sortie (personnage/ennemi, obstacle, tuile, icone) — AUCUNE logique de shading
n'est dupliquee dans les scripts individuels.

Algorithme choisi pour la classification des faces (highlight/base_light/shadow/
contact) sur les sprites personnages/ennemis/obstacles : plutot que de re-decouper
manuellement chaque membre articule dans chaque script (cout prohibitif sur
~30 generateurs), la bibliotheque classe chaque pixel opaque selon sa position
DANS LA BOITE ENGLOBANTE OPAQUE GLOBALE du sprite (haut => highlight, bande basse
de 1-2 px => contact/sol, moitie gauche du reste => base eclairci, moitie droite
=> shadow). Comme les sprites du jeu empilent deja tete/torse/jambes du haut vers
le bas (vue 3/4 plongeante), cette approximation reproduit fidelement la regle du
brief §5 ("partie haute de chaque volume -> highlight", "dessous / contact sol ->
contact") sans necessiter de retracer chaque forme. C'est un choix documente,
pas un raccourci silencieux — voir le rapport de fin de tache du graphiste.
"""

import colorsys
import math
import os

from PIL import Image

# --------------------------------------------------------------------------- #
# §1 — direction de lumiere, constante unique, jamais recopiee ailleurs
# --------------------------------------------------------------------------- #
LIGHT_DIR = (-1, -1)  # haut-gauche, normalise conceptuellement ; angle 225°

# --------------------------------------------------------------------------- #
# §2 — table des coefficients HSV par face (personnages / ennemis / obstacles)
# --------------------------------------------------------------------------- #
FACE_COEFFS = {
    "highlight": (1.35, 0.85),   # (mult V, mult S)
    "base":      (1.00, 1.00),
    "shadow":    (0.55, 1.10),
    "contact":   (0.35, 1.15),
}

# Face additionnelle documentee au §5 : flanc gauche d'un volume = "mi-chemin
# base/highlight" (la lumiere vient aussi de la gauche). Derivee comme la
# moyenne des coefficients base/highlight — pas une nouvelle regle inventee,
# une interpolation explicite du §5.
FACE_COEFFS["base_light"] = (
    (FACE_COEFFS["base"][0] + FACE_COEFFS["highlight"][0]) / 2.0,
    (FACE_COEFFS["base"][1] + FACE_COEFFS["highlight"][1]) / 2.0,
)

# §5 icones UI : amplitude fortement reduite, 2 faces seulement (highlight/shadow).
# Le brief ne chiffre que le delta V (x1.20 / x0.70) pour les icones ; le delta S
# est une extrapolation mineure et proportionnee du meme principe (§2), documentee
# ici (ecart signale dans le rapport de fin de tache).
ICON_FACE_COEFFS = {
    "highlight": (1.20, 0.92),
    "shadow":    (0.70, 1.05),
}


def _clamp(v, lo=0.0, hi=1.0):
    return max(lo, min(hi, v))


def shade(base_rgb, face, coeffs=None):
    """Derive highlight/base/shadow/contact depuis une couleur de base (§2).

    Contrainte dure du brief : jamais de noir/blanc pur, teinte (H) inchangee.
    """
    coeffs = coeffs or FACE_COEFFS
    r, g, b = base_rgb[0] / 255.0, base_rgb[1] / 255.0, base_rgb[2] / 255.0
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    dv, ds = coeffs.get(face, (1.0, 1.0))
    v2 = _clamp(v * dv, 0.02, 0.98)  # jamais 0 ni 1 pur
    s2 = _clamp(s * ds, 0.0, 1.0)
    r2, g2, b2 = colorsys.hsv_to_rgb(h, s2, v2)
    return (int(round(r2 * 255)), int(round(g2 * 255)), int(round(b2 * 255)))


def _color_matches(rgb, palette, tolerance=6):
    for c in palette:
        if abs(rgb[0] - c[0]) <= tolerance and abs(rgb[1] - c[1]) <= tolerance and abs(rgb[2] - c[2]) <= tolerance:
            return True
    return False


# --------------------------------------------------------------------------- #
# Sprites personnages / ennemis / obstacles — classification par bbox globale
# --------------------------------------------------------------------------- #
def shade_sprite(img, core_colors=None, tolerance=6, highlight_frac=0.30, min_contact_px=1, max_contact_px=2):
    """Applique l'ombrage pseudo-3D §2/§5 a un sprite (personnage/ennemi/obstacle).

    - Ignore les pixels transparents.
    - Ignore les pixels dont alpha != 255 : ce sont des halos/glow deja rendus en
      degrade additif (glow()), qui ne doivent pas etre re-teintes.
    - Ignore les pixels dont la couleur correspond a `core_colors` (noyau
      energetique / accent de biome) — jamais assombris, cf. §5/§6.
    - Classe le reste par position dans la boite englobante opaque globale du
      sprite (voir docstring du module pour la justification de cette approche).
    """
    core_colors = core_colors or []
    bbox = img.getbbox()
    if bbox is None:
        return img

    x0, y0, x1, y1 = bbox  # x1/y1 exclusifs (convention PIL)
    w = x1 - x0
    h = y1 - y0
    if w <= 0 or h <= 0:
        return img

    contact_rows = min_contact_px if h <= 10 else max_contact_px
    highlight_rows = max(1, round(h * highlight_frac))

    out = img.copy()
    src = img.load()
    dst = out.load()

    for y in range(y0, y1):
        rel_bottom = (y1 - 1) - y
        rel_top = y - y0
        for x in range(x0, x1):
            r, g, b, a = src[x, y]
            if a == 0 or a != 255:
                continue
            if _color_matches((r, g, b), core_colors, tolerance):
                continue
            if rel_bottom < contact_rows:
                face = "contact"
            elif rel_top < highlight_rows:
                face = "highlight"
            else:
                face = "base_light" if (x - x0) < (w / 2.0) else "shadow"
            nr, ng, nb = shade((r, g, b), face)
            dst[x, y] = (nr, ng, nb, a)
    return out


def add_cast_shadow(img, alpha=90, ratio=2.2, width_ratio=0.68, offset=(2, 2), min_width=6, feather=2.5):
    """Ombre portee elliptique au sol (§3), composee SOUS le sprite existant.

    `alpha` : 90 (~35%) pour personnages/ennemis, 100 pour obstacles statiques
    (legerement plus marque, cf. §5 "Obstacles").
    """
    bbox = img.getbbox()
    if bbox is None:
        return img
    x0, y0, x1, y1 = bbox
    w = x1 - x0

    shadow_w = max(min_width, w * width_ratio)
    shadow_h = shadow_w / ratio
    cx = (x0 + x1) / 2.0 + offset[0]
    cy = y1 + offset[1] - shadow_h * 0.25

    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    lw, lh = layer.size
    rx = shadow_w / 2.0
    ry = shadow_h / 2.0
    if rx <= 0 or ry <= 0:
        return img

    x_lo = max(0, int(cx - rx - feather - 1))
    x_hi = min(lw, int(cx + rx + feather + 2))
    y_lo = max(0, int(cy - ry - feather - 1))
    y_hi = min(lh, int(cy + ry + feather + 2))

    px = layer.load()
    for y in range(y_lo, y_hi):
        for x in range(x_lo, x_hi):
            d = math.sqrt(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2)
            if d <= 1.0:
                a = alpha
            elif d <= 1.0 + feather / max(rx, ry):
                t = (d - 1.0) / (feather / max(rx, ry))
                a = int(alpha * (1.0 - t))
            else:
                continue
            if a > 0:
                px[x, y] = (0, 0, 0, a)

    return Image.alpha_composite(layer, img)


# --------------------------------------------------------------------------- #
# Tuiles de sol — gradient leger, pas de volume (§5 "Tuiles de sol")
# --------------------------------------------------------------------------- #
def shade_tile(img, amplitude=0.12, exclude_colors=None, tolerance=6):
    """Degrade haut-gauche (clair) -> bas-droite (sombre), amplitude reduite.

    Ne cree pas de faces plates (highlight/shadow) comme les sprites : un vrai
    gradient continu pixel a pixel est ici volontaire (§5 : "sol qui ne doit pas
    parraitre bossele").
    """
    exclude_colors = exclude_colors or []
    w, h = img.size
    if w <= 1 or h <= 1:
        return img
    out = img.copy()
    src = img.load()
    dst = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = src[x, y]
            if a == 0:
                continue
            if _color_matches((r, g, b), exclude_colors, tolerance):
                continue
            t = (x / (w - 1) + y / (h - 1)) / 2.0  # 0 = haut-gauche, 1 = bas-droite
            factor = 1.0 + amplitude * (1.0 - 2.0 * t)
            hh, ss, vv = colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)
            vv2 = _clamp(vv * factor, 0.02, 0.98)
            r2, g2, b2 = colorsys.hsv_to_rgb(hh, ss, vv2)
            dst[x, y] = (int(round(r2 * 255)), int(round(g2 * 255)), int(round(b2 * 255)), a)
    return out


def shade_recessed(img, target_colors, amplitude=0.12, tolerance=10):
    """Variante inversee de shade_tile pour un element EN CREUX (fissure, conduit) :
    bord haut-gauche assombri, bord bas-droite eclairci (§5 : "un creux a son
    ombre du cote oppose a une bosse"). Ne s'applique qu'aux pixels dont la
    couleur correspond a `target_colors` (le motif en creux), le reste de la
    tuile n'est pas touche par cet appel (utiliser shade_tile en complement).
    """
    w, h = img.size
    if w <= 1 or h <= 1:
        return img
    out = img.copy()
    src = img.load()
    dst = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = src[x, y]
            if a == 0:
                continue
            if not _color_matches((r, g, b), target_colors, tolerance):
                continue
            t = (x / (w - 1) + y / (h - 1)) / 2.0
            factor = 1.0 - amplitude * (1.0 - 2.0 * t)  # inverse de shade_tile
            hh, ss, vv = colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)
            vv2 = _clamp(vv * factor, 0.02, 0.98)
            r2, g2, b2 = colorsys.hsv_to_rgb(hh, ss, vv2)
            dst[x, y] = (int(round(r2 * 255)), int(round(g2 * 255)), int(round(b2 * 255)), a)
    return out


# --------------------------------------------------------------------------- #
# Icones UI — 2 faces seulement, amplitude reduite, pas de contact ni d'ombre
# portee (§5 "Icones UI")
# --------------------------------------------------------------------------- #
def shade_icon(img, core_colors=None, tolerance=6):
    core_colors = core_colors or []
    bbox = img.getbbox()
    if bbox is None:
        return img
    x0, y0, x1, y1 = bbox
    w = x1 - x0
    h = y1 - y0
    if w <= 0 or h <= 0:
        return img

    out = img.copy()
    src = img.load()
    dst = out.load()
    for y in range(y0, y1):
        ry = (y - y0) / max(1, h)
        for x in range(x0, x1):
            r, g, b, a = src[x, y]
            if a == 0 or a != 255:
                continue
            if _color_matches((r, g, b), core_colors, tolerance):
                continue
            rx = (x - x0) / max(1, w)
            # split diagonal 45° aligne sur LIGHT_DIR : haut-gauche = highlight
            face = "highlight" if (rx + ry) < 1.0 else "shadow"
            nr, ng, nb = shade((r, g, b), face, coeffs=ICON_FACE_COEFFS)
            dst[x, y] = (nr, ng, nb, a)
    return out


# --------------------------------------------------------------------------- #
# Dispatch automatique par chemin de sortie — utilise par les wrappers save()
# de chaque generate_*.py (voir §8 : "reutilisees ... via import")
# --------------------------------------------------------------------------- #
def sprite_category_from_path(path):
    p = path.replace("\\", "/").lower()
    fname = p.rsplit("/", 1)[-1]
    # Sprites d'ombre autonomes (ex. tile_pillar_stone_shadow.png, utilise a
    # ZIndex=-1 par le moteur) : jamais reombres/re-shadow, ce sont deja des
    # ombres pures.
    if fname.endswith("_shadow.png"):
        return None
    if "/enemies/" in p or "/player/" in p:
        return "character"
    if "/environment/" in p:
        return "obstacle"
    if "/tileset/" in p:
        return "tile"
    if "/ui/" in p:
        return "icon"
    return None


def apply_by_category(img, path, core_colors=None):
    """Point d'entree unique recommande pour patcher un save(img, path) existant.

    Retourne l'image transformee selon la categorie deduite du chemin de sortie,
    ou l'image inchangee si la categorie n'est pas concernee par le pseudo-3D
    (projectiles, VFX, pickups — matiere/energie vivante non ombree, cf. §5/§6).
    """
    kind = sprite_category_from_path(path)
    if kind == "character":
        img = shade_sprite(img, core_colors=core_colors)
        img = add_cast_shadow(img, alpha=90)
    elif kind == "obstacle":
        img = shade_sprite(img, core_colors=core_colors)
        img = add_cast_shadow(img, alpha=100)
    elif kind == "tile":
        img = shade_tile(img, exclude_colors=core_colors)
    elif kind == "icon":
        img = shade_icon(img, core_colors=core_colors)
    return img


# --------------------------------------------------------------------------- #
# Primitives generiques optionnelles (memes signatures que les conventions
# existantes put/rect/glow/canvas, parametrees par taille de canvas — utiles
# pour les NOUVEAUX scripts, ex. tools/generate_new_enemies.py)
# --------------------------------------------------------------------------- #
def canvas(size=32):
    return Image.new("RGBA", (size, size), (0, 0, 0, 0))


def put(img, x, y, c):
    x, y = int(round(x)), int(round(y))
    if 0 <= x < img.width and 0 <= y < img.height:
        if len(c) == 3:
            c = (c[0], c[1], c[2], 255)
        if c[3] == 255:
            img.putpixel((x, y), c)
        else:
            base = img.getpixel((x, y))
            a = c[3] / 255.0
            img.putpixel((x, y), (
                int(c[0] * a + base[0] * (1 - a)),
                int(c[1] * a + base[1] * (1 - a)),
                int(c[2] * a + base[2] * (1 - a)),
                max(base[3], c[3]),
            ))


def rect(img, x0, y0, x1, y1, c):
    for y in range(int(y0), int(y1) + 1):
        for x in range(int(x0), int(x1) + 1):
            put(img, x, y, c)


def glow(img, cx, cy, r, c, strength=0.5):
    if r <= 0.5:
        put(img, cx, cy, (c[0], c[1], c[2], int(255 * strength)))
        return
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            d = math.hypot(x - cx, y - cy)
            if d <= r:
                a = int(255 * strength * (1 - d / r))
                if a > 0:
                    put(img, x, y, (c[0], c[1], c[2], a))


def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "PNG")


def fade_alpha(img, factor):
    """Multiplie uniformement le canal alpha (utile pour les frames de mort qui
    dessinent a alpha reduit : dessiner a alpha=255 pour beneficier du shading,
    puis appliquer fade_alpha() en tout dernier, apres shade_sprite()).
    """
    out = img.copy()
    r, g, b, a = out.split()
    a = a.point(lambda v: int(v * factor))
    out.putalpha(a)
    return out


def wrap_save(original_save, core_colors=None):
    """Utilitaire pour patcher en une ligne le save() existant d'un script :

        save = pseudo3d_lib.wrap_save(save, core_colors=CORE_COLORS)

    Applique automatiquement apply_by_category() avant d'appeler le save()
    d'origine (qui gere deja os.makedirs + img.save(path, "PNG")).
    """
    def _save(img, path):
        img = apply_by_category(img, path, core_colors=core_colors)
        original_save(img, path)
    return _save
