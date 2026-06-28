"""
generate_miniboss_sprites.py - Sprites pixel art pour les mini-boss Chimera Protocol.

RustStalker  : chasseur rapide rouille/or, 32x32, corps de raptor metallique
MasterSentinel : version elite de la Sentinelle, 32x32, blindage violet/cyan

Usage :
  python tools/generate_miniboss_sprites.py
"""

import os
import math
from PIL import Image, ImageDraw

# Palette partagee avec generate_sprites.py
C_TRANSPARENT  = (0x00, 0x00, 0x00, 0)
C_BLACK        = (0x00, 0x00, 0x00, 255)
C_WHITE        = (0xFF, 0xFF, 0xFF, 255)

# Rouille / Stalker
C_RUST_LIGHT   = (0x7A, 0x4A, 0x2A, 255)
C_RUST_DARK    = (0x4A, 0x2A, 0x15, 255)
C_RUST_ORANGE  = (0xCC, 0x55, 0x11, 255)
C_STALKER_BODY = (0x5A, 0x32, 0x1A, 255)
C_STALKER_HI   = (0x9A, 0x62, 0x33, 255)
C_STALKER_OR   = (0xFF, 0xAA, 0x22, 255)
C_STALKER_GLOW = (0xFF, 0xCC, 0x44, 255)
C_METAL_GREY   = (0x4A, 0x4A, 0x52, 255)
C_METAL_DARK   = (0x2A, 0x2A, 0x32, 255)

# Master Sentinel
C_SENTINEL_BODY  = (0x3A, 0x3A, 0x55, 255)
C_SENTINEL_PLATE = (0x5A, 0x5A, 0x7A, 255)
C_SENTINEL_HI    = (0x7A, 0x7A, 0x9A, 255)
C_AETHER_PRI     = (0x00, 0xE5, 0xFF, 255)
C_NOYAU_VIOL     = (0xAA, 0x44, 0xFF, 255)
C_NOYAU_GLOW     = (0xCC, 0x88, 0xFF, 255)
C_PLASMA_HOT     = (0xFF, 0x88, 0x00, 255)


def new_canvas(w=32, h=32):
    return Image.new("RGBA", (w, h), C_TRANSPARENT)


def px(img, x, y, color):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), color)


def rect(img, x0, y0, x1, y1, color):
    draw = ImageDraw.Draw(img)
    draw.rectangle([x0, y0, x1, y1], fill=color)


def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "PNG")


# =============================================================================
#  RUST STALKER — raptor mecanique rouille, yeux or incandescents
#  Corps allonge, griffes avant, 5 frames move (decalage lateral), 4 death
# =============================================================================

def draw_rust_stalker_base(img, phase=0, dying=False):
    """
    Silhouette Rust Stalker 32x32 :
    - Corps central ellipse 14x10 px a y=14
    - Tete triangulaire 8x6 px a y=8
    - 4 pattes (2 avant + 2 arriere) 4 px chacune
    - Yeux or brillants (2 px)
    - Implants orange incandescents sur le dos
    phase 0-4 pour animation marche (oscillation pattes)
    """
    # Corps
    cx, cy = 16, 16
    body_color = C_STALKER_BODY if not dying else (0x3A, 0x1A, 0x0A, 255)
    hi_color   = C_STALKER_HI   if not dying else (0x5A, 0x2A, 0x1A, 255)

    # Corps principal
    rect(img, cx-7, cy-4, cx+7, cy+5, body_color)
    rect(img, cx-6, cy-5, cx+6, cy+6, body_color)
    # Highlight dos
    rect(img, cx-5, cy-4, cx+4, cy-2, hi_color)

    # Tete (avant = gauche du sprite vue dessus)
    head_x = cx - 10
    head_color = C_STALKER_BODY if not dying else (0x30, 0x18, 0x08, 255)
    rect(img, head_x, cy-3, head_x+6, cy+2, head_color)
    # Machoires
    rect(img, head_x-2, cy-1, head_x, cy+1, body_color)

    # Yeux (or incandescents)
    eye_color = C_STALKER_GLOW if not dying else (0x99, 0x55, 0x11, 255)
    px(img, head_x+1, cy-2, eye_color)
    px(img, head_x+1, cy+1, eye_color)
    # Aura yeux
    glow = C_STALKER_OR if not dying else (0x66, 0x33, 0x00, 255)
    px(img, head_x,   cy-2, glow)
    px(img, head_x,   cy+1, glow)

    # Implants dos (3 bosses orange)
    implant = C_RUST_ORANGE if not dying else (0x44, 0x22, 0x08, 255)
    for i in range(3):
        bx = cx - 4 + i * 4
        px(img, bx, cy-5, implant)
        px(img, bx, cy-4, implant)

    # Queue (arriere = droite)
    tail_x = cx + 8
    rect(img, tail_x, cy-1, tail_x+4, cy+1, C_RUST_DARK)

    # Pattes (oscillation selon phase)
    offsets_front = [(0, 3), (0, 2), (0, 1), (0, 2), (0, 3)]
    offsets_rear  = [(0, 1), (0, 2), (0, 3), (0, 2), (0, 1)]
    ph = phase % 5

    # Pattes avant (2 paires)
    fp_y_top = cy - 4 + offsets_front[ph][0]
    fp_y_bot = cy + 4 + offsets_front[ph][1]
    rect(img, cx-4, fp_y_top, cx-2, fp_y_top+1, C_RUST_LIGHT)
    rect(img, cx-4, fp_y_bot, cx-2, fp_y_bot+1, C_RUST_LIGHT)

    # Pattes arriere
    rp_y_top = cy - 4 + offsets_rear[ph][0]
    rp_y_bot = cy + 4 + offsets_rear[ph][1]
    rect(img, cx+2, rp_y_top, cx+5, rp_y_top+1, C_RUST_LIGHT)
    rect(img, cx+2, rp_y_bot, cx+5, rp_y_bot+1, C_RUST_LIGHT)

    # Contour noir
    draw = ImageDraw.Draw(img)
    # Quelques pixels de contour sur le corps
    for bx in [cx-7, cx+7]:
        for by in range(cy-4, cy+6):
            if 0 <= bx < 32 and 0 <= by < 32:
                r, g, b, a = img.getpixel((bx, by))
                if a > 0:
                    px(img, bx, by, C_BLACK)


def gen_rust_stalker(out_dir):
    out = os.path.join(out_dir, "rust_stalker")
    os.makedirs(out, exist_ok=True)

    # idle : 3 frames legere oscillation
    for i in range(3):
        img = new_canvas()
        draw_rust_stalker_base(img, phase=i % 2)
        save(img, f"{out}/enemy_rustsalker_idle_{i+1:02d}.png")

    # move : 5 frames
    for i in range(5):
        img = new_canvas()
        draw_rust_stalker_base(img, phase=i)
        save(img, f"{out}/enemy_rustsalker_move_{i+1:02d}.png")

    # death : 4 frames (fade + dispersion rouille)
    for i in range(4):
        img = new_canvas()
        draw_rust_stalker_base(img, phase=0, dying=True)
        # Chaque frame retire des pixels
        pixels_to_remove = i * 6
        data = list(img.getdata())
        removed = 0
        new_data = []
        for j, pix in enumerate(data):
            if pix[3] > 0 and removed < pixels_to_remove and (j % (4 - i + 1) == 0):
                new_data.append(C_TRANSPARENT)
                removed += 1
            else:
                new_data.append(pix)
        img.putdata(new_data)
        # Teinte rouille plus sombre
        if i >= 2:
            for y_p in range(32):
                for x_p in range(32):
                    r, g, b, a = img.getpixel((x_p, y_p))
                    if a > 0:
                        img.putpixel((x_p, y_p), (max(0, r-20), max(0, g-30), max(0, b-40), a))
        save(img, f"{out}/enemy_rustsalker_death_{i+1:02d}.png")

    print(f"[RustStalker] {3+5+4} sprites dans {out}")


# =============================================================================
#  MASTER SENTINEL — version elite blindee, double canon violet+cyan
#  Corps hexagonal, 4 frames idle, 6 move, 4 attack, 6 death
# =============================================================================

def draw_master_sentinel_base(img, phase=0, attacking=False, dying=False):
    """
    Master Sentinel 32x32 :
    - Corps central hexagone 12x14 px
    - Blindage superieur (epaulettes)
    - 2 canons (avant) violet et cyan
    - Implants Aether lumineux
    """
    cx, cy = 16, 17

    body   = C_SENTINEL_BODY  if not dying else (0x22, 0x22, 0x33, 255)
    plate  = C_SENTINEL_PLATE if not dying else (0x33, 0x33, 0x44, 255)
    hilit  = C_SENTINEL_HI    if not dying else (0x44, 0x44, 0x55, 255)

    # Corps principal
    rect(img, cx-6, cy-6, cx+6, cy+6, body)
    # Coins biseautes (pseudo hexagone)
    px(img, cx-6, cy-6, C_TRANSPARENT)
    px(img, cx+6, cy-6, C_TRANSPARENT)
    px(img, cx-6, cy+6, C_TRANSPARENT)
    px(img, cx+6, cy+6, C_TRANSPARENT)

    # Blindage torse
    rect(img, cx-5, cy-6, cx+5, cy-3, plate)
    rect(img, cx-4, cy-7, cx+4, cy-6, plate)

    # Epaulettes
    rect(img, cx-8, cy-5, cx-6, cy-3, plate)
    rect(img, cx+6, cy-5, cx+8, cy-3, plate)

    # Highlight
    rect(img, cx-4, cy-5, cx+3, cy-3, hilit)

    # Implants Aether (lignes cyan sur le corps)
    if not dying:
        aether_color = C_AETHER_PRI
        # Ligne verticale centrale
        for ay in range(cy-3, cy+5):
            px(img, cx, ay, aether_color)
        # Lignes laterales
        px(img, cx-3, cy-1, aether_color)
        px(img, cx+3, cy-1, aether_color)
        px(img, cx-3, cy+1, aether_color)
        px(img, cx+3, cy+1, aether_color)

    # Implant violet central (noyau)
    if not dying:
        noyau = C_NOYAU_VIOL
        for nx, ny in [(cx-1, cy+2), (cx, cy+2), (cx+1, cy+2),
                       (cx-1, cy+3), (cx, cy+3), (cx+1, cy+3)]:
            px(img, nx, ny, noyau)

    # Jambes (2 paires, oscillation)
    leg_off = [0, 1, 0, -1][phase % 4] if not dying else 0
    leg_color = C_METAL_GREY if not dying else (0x33, 0x33, 0x3A, 255)
    rect(img, cx-7, cy+3+leg_off, cx-5, cy+7+leg_off, leg_color)
    rect(img, cx+5, cy+3-leg_off, cx+7, cy+7-leg_off, leg_color)

    # Canons (avant = haut du sprite)
    if not dying:
        # Canon gauche (violet)
        canon_col_l = C_NOYAU_VIOL if not attacking else C_NOYAU_GLOW
        rect(img, cx-5, cy-10, cx-3, cy-6, canon_col_l)
        # Canon droit (cyan)
        canon_col_r = C_AETHER_PRI if not attacking else C_WHITE
        rect(img, cx+3, cy-10, cx+5, cy-6, canon_col_r)
        # Flamme si attaque
        if attacking:
            px(img, cx-4, cy-11, C_NOYAU_GLOW)
            px(img, cx+4, cy-11, C_AETHER_PRI)
            px(img, cx-4, cy-12, C_WHITE)
            px(img, cx+4, cy-12, C_WHITE)
    else:
        # Canons detruits
        rect(img, cx-5, cy-9, cx-3, cy-6, C_METAL_DARK)
        rect(img, cx+3, cy-9, cx+5, cy-6, C_METAL_DARK)


def gen_master_sentinel(out_dir):
    out = os.path.join(out_dir, "master_sentinel")
    os.makedirs(out, exist_ok=True)

    # idle : 4 frames
    for i in range(4):
        img = new_canvas()
        draw_master_sentinel_base(img, phase=i)
        save(img, f"{out}/enemy_mastersentinel_idle_{i+1:02d}.png")

    # move : 4 frames (meme que idle avec oscillation accentuee)
    for i in range(4):
        img = new_canvas()
        draw_master_sentinel_base(img, phase=i)
        save(img, f"{out}/enemy_mastersentinel_move_{i+1:02d}.png")

    # attack : 4 frames
    for i in range(4):
        img = new_canvas()
        draw_master_sentinel_base(img, phase=0, attacking=(i >= 1))
        save(img, f"{out}/enemy_mastersentinel_attack_{i+1:02d}.png")

    # death : 5 frames
    for i in range(5):
        img = new_canvas()
        draw_master_sentinel_base(img, phase=0, dying=True)
        # Dispersion progressive
        pixels_to_remove = i * 8
        data = list(img.getdata())
        removed = 0
        new_data = []
        for j, pix in enumerate(data):
            if pix[3] > 0 and removed < pixels_to_remove and (j % (5 - i + 1) == 0):
                new_data.append(C_TRANSPARENT)
                removed += 1
            else:
                new_data.append(pix)
        img.putdata(new_data)
        save(img, f"{out}/enemy_mastersentinel_death_{i+1:02d}.png")

    print(f"[MasterSentinel] {4+4+4+5} sprites dans {out}")


# =============================================================================
#  Main
# =============================================================================

if __name__ == "__main__":
    import sys
    out_dir = sys.argv[1] if len(sys.argv) > 1 else "assets/sprites/enemies"
    gen_rust_stalker(out_dir)
    gen_master_sentinel(out_dir)
    print("Sprites mini-boss generes.")
