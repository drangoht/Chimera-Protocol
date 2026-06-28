"""
generate_hud_assets.py
Chimera Protocol — generateur d'assets HUD procéduraux (Pillow)
Produit : assets/sprites/ui/ui_lv_hex.png
          assets/sprites/ui/ui_panel_frame_nobg.png  (cadre tech from scratch)
"""

import math
import os
from PIL import Image, ImageDraw

# Chemins absolus depuis la racine du projet
SCRIPT_DIR  = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.dirname(SCRIPT_DIR)
OUT_DIR     = os.path.join(PROJECT_DIR, "assets", "sprites", "ui")

os.makedirs(OUT_DIR, exist_ok=True)

# Palette
CYAN        = (0x44, 0xFF, 0xEE, 255)   # #44FFEE
CYAN_DIM    = (0x44, 0xFF, 0xEE, 160)   # details discrets
CYAN_FAINT  = (0x44, 0xFF, 0xEE,  55)   # trait quasi-invisible (scan-line)
TRANSPARENT = (0, 0, 0, 0)

# ---------------------------------------------------------------------------
# ui_lv_hex.png — hexagone flat-top 44x26 px (paysage), contour 2px, fond transparent
# Conçu pour tenir dans XpRow custom_minimum_size Vector2(44,24)
# ---------------------------------------------------------------------------
def generate_lv_hex(path: str, w: int = 44, h: int = 26) -> None:
    img  = Image.new("RGBA", (w, h), TRANSPARENT)
    draw = ImageDraw.Draw(img)

    cx, cy = w / 2, h / 2
    # Flat-top hexagon : r_x (horizontal) > r_y (vertical)
    rx = w / 2 - 2.5
    ry = h / 2 - 2.5

    # Flat-top : angles 0°, 60°, 120°, 180°, 240°, 300°
    def hex_points(scale_x: float = 1.0, scale_y: float = 1.0):
        pts = []
        for i in range(6):
            angle_rad = math.radians(60 * i)  # flat-top
            pts.append((cx + rx * scale_x * math.cos(angle_rad),
                        cy + ry * scale_y * math.sin(angle_rad)))
        return pts

    pts_outer = hex_points()
    draw.polygon(pts_outer, outline=CYAN, fill=None)
    pts_inner = hex_points(scale_x=(rx-1)/rx, scale_y=(ry-1)/ry)
    draw.polygon(pts_inner, outline=CYAN, fill=None)

    # Petits éclats aux 6 sommets
    for (px, py) in pts_outer:
        for dx, dy in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
            nx, ny = int(round(px)) + dx, int(round(py)) + dy
            if 0 <= nx < w and 0 <= ny < h:
                img.putpixel((nx, ny), CYAN_DIM)

    img.save(path)
    print(f"  [OK] {path}  ({w}x{h} px)")


# ---------------------------------------------------------------------------
# ui_panel_frame_nobg.png — cadre tech 304x126 px from scratch
# Inspiré du concept : coins L-bracket épais, tirets de graduation sur les bords,
# losange de mi-bord, micro-hachures aux coins, tout sur fond transparent.
# Conçu pour s'afficher en overlay (z_index=2) sur StatsPanelBg (300x122).
# ---------------------------------------------------------------------------
def generate_panel_frame(path: str, w: int = 304, h: int = 126) -> None:
    img  = Image.new("RGBA", (w, h), TRANSPARENT)
    draw = ImageDraw.Draw(img)

    # -- Bordure fine 1px (couleur faint, juste un contour de présence) ----------
    draw.rectangle([0, 0, w-1, h-1], outline=CYAN_FAINT, width=1)

    # -- Coins L-bracket 18x14 px bright (reproduit le style du concept) ---------
    bw, bh = 18, 14   # largeur/hauteur de chaque branche du L
    thick   = 2       # épaisseur du trait bracket

    def bracket(x0, y0, flip_x, flip_y):
        """Dessine un coin L. flip_x/y invertit la direction."""
        sx = 1 if not flip_x else -1
        sy = 1 if not flip_y else -1
        # Branche horizontale
        x1h = x0 + sx * bw
        draw.line([(x0, y0), (x1h, y0)], fill=CYAN, width=thick)
        # Branche verticale
        y1v = y0 + sy * bh
        draw.line([(x0, y0), (x0, y1v)], fill=CYAN, width=thick)
        # Micro-pixel de coin (renforce l'angle)
        img.putpixel((x0, y0), CYAN)

    bracket(0,   0,   False, False)   # haut-gauche
    bracket(w-1, 0,   True,  False)   # haut-droit
    bracket(0,   h-1, False, True )   # bas-gauche
    bracket(w-1, h-1, True,  True )   # bas-droit

    # -- Tirets de graduation sur les bords H (haut + bas) ----------------------
    # 4 tirets de 4px espacés régulièrement entre les coins, alpha moyen
    for y_edge in (0, h-1):
        step = (w - 2*bw) // 5
        for i in range(1, 5):
            x = bw + i * step
            for dx in range(4):
                if 0 <= x+dx < w:
                    img.putpixel((x+dx, y_edge), CYAN_DIM)

    # -- Tirets de graduation sur les bords V (gauche + droite) ------------------
    for x_edge in (0, w-1):
        step = (h - 2*bh) // 3
        for i in range(1, 3):
            y = bh + i * step
            for dy in range(3):
                if 0 <= y+dy < h:
                    img.putpixel((x_edge, y+dy), CYAN_DIM)

    # -- Losange de mi-bord haut (ornement signature du concept) -----------------
    mid_x = w // 2
    for dx, dy, alpha in [(0,0,255),(1,0,200),(-1,0,200),(0,1,200),(0,-1,200),
                           (2,0,100),(-2,0,100),(0,2,100),(0,-2,100)]:
        px, py = mid_x + dx, dy
        if 0 <= px < w and 0 <= py < h:
            img.putpixel((px, py), (0x44, 0xFF, 0xEE, alpha))

    # -- Losange de mi-bord bas --------------------------------------------------
    for dx, dy_off, alpha in [(0,0,255),(1,0,200),(-1,0,200),(0,-1,200),(0,1,200),
                               (2,0,100),(-2,0,100),(0,-2,100),(0,2,100)]:
        px, py = mid_x + dx, h-1 + dy_off
        if 0 <= px < w and 0 <= py < h:
            img.putpixel((px, py), (0x44, 0xFF, 0xEE, alpha))

    img.save(path)
    from PIL import Image as _I
    import numpy as _np
    arr = _np.array(_I.open(path))
    opaque = int((arr[:,:,3] > 0).sum())
    print(f"  [OK] {path}  ({w}x{h} px)  opaque={opaque}/{w*h} ({100*opaque//(w*h)}%)")


# ---------------------------------------------------------------------------
# Point d'entree
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    print("=== Chimera Protocol — generate_hud_assets.py ===")

    out_hex = os.path.join(OUT_DIR, "ui_lv_hex.png")
    generate_lv_hex(out_hex, w=44, h=26)

    out_frame = os.path.join(OUT_DIR, "ui_panel_frame_nobg.png")
    generate_panel_frame(out_frame, w=304, h=126)

    print("=== Termine ===")
