"""
retouch_hud_assets.py
Chimera Protocol — Retouche automatique des assets HUD par masquage couleur.

Technique : conserver uniquement les pixels dont la teinte correspond aux éléments
UI (cyan #44FFEE et violet #AA44FF) avec saturation et luminosité suffisantes.
Rend le reste transparent. Pas de Photoshop nécessaire.
"""

import os
import sys
import numpy as np
from PIL import Image

SCRIPT_DIR  = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.dirname(SCRIPT_DIR)
SRC_IMG     = os.path.join(PROJECT_DIR, "idea", "idee_hud_chimera_core.png")
OUT_DIR     = os.path.join(PROJECT_DIR, "assets", "sprites", "ui")

os.makedirs(OUT_DIR, exist_ok=True)


# ---------------------------------------------------------------------------
# Utilitaires numpy
# ---------------------------------------------------------------------------

def rgb_to_hsv_np(arr_float):
    """arr_float shape (H,W,3) valeurs 0-1 → retourne h(0-360), s(0-1), v(0-1)."""
    r, g, b = arr_float[:,:,0], arr_float[:,:,1], arr_float[:,:,2]
    v = np.maximum(np.maximum(r, g), b)
    mn = np.minimum(np.minimum(r, g), b)
    diff = v - mn

    s = np.where(v > 1e-6, diff / v, 0.0)

    h = np.zeros_like(v)
    nz = diff > 1e-6
    # Hue quand rouge domine
    mr = nz & (v == r)
    h[mr] = (60.0 * ((g[mr] - b[mr]) / diff[mr])) % 360.0
    # Hue quand vert domine
    mg = nz & (v == g)
    h[mg] = 60.0 * ((b[mg] - r[mg]) / diff[mg]) + 120.0
    # Hue quand bleu domine
    mb = nz & (v == b)
    h[mb] = 60.0 * ((r[mb] - g[mb]) / diff[mb]) + 240.0

    return h, s, v


def keep_hues(img_rgba: Image.Image,
              hue_ranges: list,
              sat_min: float = 0.25,
              val_min: float = 0.15,
              dilate_px: int = 1) -> Image.Image:
    """
    Conserve les pixels dont la teinte est dans l'un des intervalles (h_min, h_max),
    avec saturation >= sat_min et luminosité >= val_min.
    Les autres pixels deviennent transparents.
    dilate_px : dilate le masque (comble les trous de 1-2px liés à l'anti-aliasing).
    """
    arr = np.array(img_rgba, dtype=np.float32)
    rgb = arr[:,:,:3] / 255.0
    h, s, v = rgb_to_hsv_np(rgb)

    keep = np.zeros(arr.shape[:2], dtype=bool)
    for h_min, h_max in hue_ranges:
        if h_min <= h_max:
            band = (h >= h_min) & (h <= h_max)
        else:                              # intervalle qui croise 0°/360°
            band = (h >= h_min) | (h <= h_max)
        keep |= band & (s >= sat_min) & (v >= val_min)

    # Dilatation simple : un pixel est conservé si lui ou un voisin est dans le masque
    if dilate_px > 0:
        from numpy.lib.stride_tricks import sliding_window_view
        padded = np.pad(keep, dilate_px, mode='constant', constant_values=False)
        win = sliding_window_view(padded, (2*dilate_px+1, 2*dilate_px+1))
        keep = win.any(axis=(-2, -1))

    result = arr.copy()
    result[~keep, 3] = 0
    return Image.fromarray(result.astype(np.uint8), "RGBA")


def brighten_colored(img_rgba: Image.Image, factor: float = 2.0,
                     remove_dark_bg: bool = True, dark_thresh: int = 40) -> Image.Image:
    """
    Éclaircit les pixels colorés (saturation > 0.2) et retire le fond sombre.
    Utilisé pour l'icône Chimera Core.
    """
    arr = np.array(img_rgba, dtype=np.float32)
    rgb = arr[:,:,:3] / 255.0
    h, s, v = rgb_to_hsv_np(rgb)

    # Retire fond sombre
    if remove_dark_bg:
        r8 = arr[:,:,0].astype(int)
        g8 = arr[:,:,1].astype(int)
        b8 = arr[:,:,2].astype(int)
        dark = (r8 < dark_thresh) & (g8 < dark_thresh) & (b8 < dark_thresh + 20)
        arr[dark, 3] = 0

    # Éclaircit les pixels colorés (pas les noirs/gris)
    colored = s > 0.20
    arr[colored, :3] = np.clip(arr[colored, :3] * factor, 0, 255)

    return Image.fromarray(arr.astype(np.uint8), "RGBA")


def crop_retouch_save(img: Image.Image, box: tuple, out_name: str,
                      mode: str, target_size: tuple = None, **kwargs) -> None:
    """
    Crop → retouche (mode='hue'|'brighten') → redimensionne → sauvegarde.
    """
    region = img.crop(box)
    w_src, h_src = region.size
    region = region.convert("RGBA")

    if mode == 'hue':
        region = keep_hues(region, **kwargs)
    elif mode == 'brighten':
        region = brighten_colored(region, **kwargs)

    if target_size:
        w_max, h_max = target_size
        ratio = min(w_max / region.width, h_max / region.height)
        nw = max(1, int(region.width * ratio))
        nh = max(1, int(region.height * ratio))
        region = region.resize((nw, nh), Image.LANCZOS)

    out_path = os.path.join(OUT_DIR, out_name)
    region.save(out_path)
    fw, fh = region.size
    opaque = int(np.array(region)[:,:,3].astype(bool).sum())
    total  = fw * fh
    print(f"  [OK] {out_name}  {w_src}x{h_src} -> {fw}x{fh}  "
          f"opaque={opaque}/{total} ({100*opaque//total}%)")


# ---------------------------------------------------------------------------
# Teintes UI à conserver
# Cyan #44FFEE  → HSV ≈ 177°,  garder 155–200°
# Violet #AA44FF → HSV ≈ 270°, garder 255–290°
# Teal-vert concept #00BBCC → HSV ≈ 186°, inclus dans 155-200
# Blanc/gris lumineux des reflets → traité par val_min élevée dans les appels
# ---------------------------------------------------------------------------
CYAN_RANGE   = (155, 205)   # cyan + teal
VIOLET_RANGE = (250, 295)   # violet + bleu-violet

# Etendu pour capter les variations de couleur du concept (anti-aliasing, ombres)
FRAME_RANGES = [CYAN_RANGE, (130, 160), (205, 230)]   # cyan + nuances adjacentes


# ---------------------------------------------------------------------------
# Recette par asset
# ---------------------------------------------------------------------------

def retouch_all(img: Image.Image) -> None:
    w, h = img.size
    print(f"\n=== RETOUCHE  (image {w}×{h}) ===\n")

    # ------------------------------------------------------------------
    # 1. ui_panel_frame_nobg.png — cadre panneau stats gauche
    #    Garder : contours cyan du cadre, coins, séparateurs
    #    Rejeter : fond teal foncé, texte blanc baked, HP/XP baked
    # ------------------------------------------------------------------
    crop_retouch_save(
        img, (4, 21, 1092, 810),
        "ui_panel_frame_nobg.png",
        mode='hue',
        target_size=(300, 240),
        hue_ranges=FRAME_RANGES,
        sat_min=0.30,
        val_min=0.35,
        dilate_px=1,
    )

    # ------------------------------------------------------------------
    # 2. ui_timer_frame_nobg.png — cadre timer LCD
    #    Garder : crochets latéraux cyan, bordure fine
    #    Rejeter : "14:57" baked, fond gris
    # ------------------------------------------------------------------
    crop_retouch_save(
        img, (1310, 20, 1720, 215),
        "ui_timer_frame_nobg.png",
        mode='hue',
        target_size=(160, 72),
        hue_ranges=FRAME_RANGES,
        sat_min=0.28,
        val_min=0.30,
        dilate_px=1,
    )

    # ------------------------------------------------------------------
    # 3. ui_chimera_core_icon.png — icône hexagonale violette
    #    Garder : pixels violet + cyan saturés
    #    Retirer : fond noir, texte baked, zones grises
    #    + éclaircissement x1.8
    # ------------------------------------------------------------------
    # Passe 1 : masque par teinte violet + cyan
    region = img.crop((2370, 55, 2740, 365)).convert("RGBA")
    region = keep_hues(region,
                       hue_ranges=[CYAN_RANGE, VIOLET_RANGE, (290, 340)],
                       sat_min=0.25, val_min=0.15, dilate_px=2)
    # Passe 2 : éclaircissement des pixels colorés restants
    arr = np.array(region, dtype=np.float32)
    rgb_f = arr[:,:,:3] / 255.0
    _, s, _ = rgb_to_hsv_np(rgb_f)
    colored = (s > 0.15) & (arr[:,:,3] > 0)
    arr[colored, :3] = np.clip(arr[colored, :3] * 1.8, 0, 255)
    region = Image.fromarray(arr.astype(np.uint8), "RGBA")
    # Redimensionne
    region = region.resize((56, 56), Image.LANCZOS)
    out_path = os.path.join(OUT_DIR, "ui_chimera_core_icon.png")
    region.save(out_path)
    opaque = int(np.array(region)[:,:,3].astype(bool).sum())
    print(f"  [OK] ui_chimera_core_icon.png  56×56  opaque={opaque}/3136 ({100*opaque//3136}%)")

    # ------------------------------------------------------------------
    # Diagnostic : affiche un rapport de densité pour chaque asset
    # ------------------------------------------------------------------
    print()
    for name in ["ui_panel_frame_nobg.png", "ui_timer_frame_nobg.png",
                 "ui_chimera_core_icon.png"]:
        path = os.path.join(OUT_DIR, name)
        a = np.array(Image.open(path).convert("RGBA"))
        opaque  = int((a[:,:,3] > 128).sum())
        total   = a.shape[0] * a.shape[1]
        print(f"  densité {name:40s}  {opaque}/{total}  ({100*opaque//total}%)")


# ---------------------------------------------------------------------------
# Point d'entrée
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    if not os.path.exists(SRC_IMG):
        print(f"ERREUR : image source introuvable : {SRC_IMG}")
        sys.exit(1)

    img = Image.open(SRC_IMG).convert("RGBA")
    print(f"Source : {img.size[0]}×{img.size[1]} px")
    retouch_all(img)
    print(f"\n=== Terminé — assets dans {OUT_DIR} ===")
