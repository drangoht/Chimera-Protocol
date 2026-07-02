"""
generate_sprites_v2.py — Revamp sprites + orbes XP + mini-boss pour Chimera Protocol.
Produit :
  1. Orbes XP 4 variantes (assets/sprites/vfx/)
  2. Mini-boss RustStalker 64x64 + MasterSentinel 64x64 + fichiers .tres
  3. Revamp 4 ennemis existants (memes noms de fichiers, meme frame count)

Resolution :
  - Ennemis standard : 32x32 px
  - Colosse : 48x48 px
  - Mini-boss : 64x64 px
  - Orbes XP : 8–14 px

Usage :
  C:/Users/drang/AppData/Local/Programs/Python/Python313/python.exe tools/generate_sprites_v2.py
"""

import os
import sys
import math
from PIL import Image, ImageDraw

# ─── Palette ──────────────────────────────────────────────────────────────────

T = (0, 0, 0, 0)          # transparent

# Rouille / organique
RUST_DARK    = (0xCC, 0x44, 0x00, 255)
RUST_MID     = (0xFF, 0x66, 0x22, 255)
RUST_LIGHT   = (0xFF, 0x99, 0x44, 255)
RUST_BROWN   = (0x66, 0x33, 0x00, 255)
FILAMENT     = (0x66, 0x33, 0x00, 255)
FILAMENT_HI  = (0x99, 0x44, 0x11, 255)

# Metal / mecanique
METAL_DARK   = (0x22, 0x22, 0x33, 255)
METAL_MID    = (0x33, 0x44, 0x55, 255)
METAL_GREY   = (0x55, 0x66, 0x77, 255)
METAL_LIGHT  = (0x77, 0x88, 0x99, 255)
BLADE_EDGE   = (0xAA, 0xBB, 0xCC, 255)

# Sentinelle
SENT_BODY    = (0x44, 0x55, 0x66, 255)
SENT_DARK    = (0x33, 0x44, 0x55, 255)
SENT_SHADOW  = (0x22, 0x33, 0x44, 255)
SHIELD_MID   = (0x22, 0x33, 0x44, 255)
CANON_TIP    = (0xFF, 0xAA, 0x00, 255)
VISOR        = (0xFF, 0x88, 0x00, 255)

# Colosse
COL_FLESH    = (0x33, 0x22, 0x22, 255)
COL_PLATE    = (0x55, 0x44, 0x44, 255)
COL_ARM      = (0x77, 0x88, 0x99, 255)
IMPLANT_VIO  = (0xAA, 0x44, 0xFF, 255)
IMPLANT_GLOW = (0xCC, 0x88, 0xFF, 255)

# Araignee
SPIDER_BODY  = (0x88, 0x44, 0x22, 255)
SPIDER_CARA  = (0xCC, 0x55, 0x00, 255)
SPIDER_LEG   = (0xAA, 0x66, 0x33, 255)
SPIDER_CRAK  = (0xBB, 0x44, 0x00, 255)
SPIDER_EYE   = (0xFF, 0x22, 0x22, 255)

# Drone hex
DRONE_BODY   = (0x22, 0x22, 0x33, 255)
DRONE_BLADE  = (0x55, 0x66, 0x77, 255)
DRONE_BLADE2 = (0x44, 0x55, 0x66, 255)
DRONE_EYE    = (0xFF, 0x22, 0x22, 255)
DRONE_EYE_DK = (0x44, 0x00, 0x00, 255)

# Energie / Aether
AETHER       = (0x00, 0xE5, 0xFF, 255)
AETHER_DARK  = (0x00, 0x7A, 0x99, 255)

# XP orbes
XP_T1_MAIN   = (0x44, 0xFF, 0x66, 255)
XP_T1_EDGE   = (0x22, 0x88, 0x44, 255)
XP_T2_MAIN   = (0x44, 0xAA, 0xFF, 255)
XP_T2_EDGE   = (0x22, 0x66, 0xAA, 255)
XP_T3_MAIN   = (0xAA, 0x44, 0xFF, 255)
XP_T3_EDGE   = (0x66, 0x22, 0xAA, 255)
XP_T4_MAIN   = (0xFF, 0xD7, 0x00, 255)
XP_T4_EDGE   = (0xBB, 0x99, 0x00, 255)
WHITE        = (0xFF, 0xFF, 0xFF, 255)
BLACK        = (0x00, 0x00, 0x00, 255)


# ─── Utilitaires ──────────────────────────────────────────────────────────────

def canvas(w, h):
    return Image.new("RGBA", (w, h), T)

def px(img, x, y, c):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), c)

def rect(img, x0, y0, x1, y1, c):
    ImageDraw.Draw(img).rectangle([x0, y0, x1, y1], fill=c)

def outline(img, x0, y0, x1, y1, c):
    ImageDraw.Draw(img).rectangle([x0, y0, x1, y1], outline=c)

def circle(img, cx, cy, r, c):
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx*dx + dy*dy <= r*r:
                px(img, cx+dx, cy+dy, c)

def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "PNG")


# ─── Pseudo-3D (docs/ART_BRIEF_PSEUDO3D.md) : noyau energetique / accents ────
# jamais assombris par l'ombrage (§5/§6) — exclus de shade_sprite() ci-dessous.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pseudo3d_lib as _p3d

_CORE_COLORS = [
    AETHER[:3], AETHER_DARK[:3], IMPLANT_VIO[:3], IMPLANT_GLOW[:3],
    DRONE_EYE[:3], DRONE_EYE_DK[:3], SPIDER_EYE[:3], CANON_TIP[:3], VISOR[:3],
    XP_T1_MAIN[:3], XP_T2_MAIN[:3], XP_T3_MAIN[:3], XP_T4_MAIN[:3],
]
save = _p3d.wrap_save(save, core_colors=_CORE_COLORS)


def hline(img, y, x0, x1, c):
    for x in range(x0, x1 + 1):
        px(img, x, y, c)

def vline(img, x, y0, y1, c):
    for y in range(y0, y1 + 1):
        px(img, x, y, c)


# ─── 1. ORBES XP ──────────────────────────────────────────────────────────────

def draw_diamond(img, cx, cy, size, fill, edge):
    """Dessine un losange pixel art de rayon 'size' en coordonnees Manhattan."""
    for dy in range(-size, size + 1):
        half_w = size - abs(dy)
        for dx in range(-half_w, half_w + 1):
            # Bord = Manhattan distance == size
            if abs(dx) + abs(dy) == size:
                px(img, cx + dx, cy + dy, edge)
            else:
                px(img, cx + dx, cy + dy, fill)


def generate_xp_orbs(out_dir):
    os.makedirs(out_dir, exist_ok=True)

    # T1 — 8x8 vert
    img = canvas(8, 8)
    draw_diamond(img, 3, 3, 3, XP_T1_MAIN, XP_T1_EDGE)
    px(img, 3, 3, WHITE)  # pixel central blanc
    save(img, os.path.join(out_dir, "vfx_xp_orb_t1.png"))

    # T2 — 10x10 cyan
    img = canvas(10, 10)
    draw_diamond(img, 4, 4, 4, XP_T2_MAIN, XP_T2_EDGE)
    px(img, 4, 4, WHITE)
    save(img, os.path.join(out_dir, "vfx_xp_orb_t2.png"))

    # T3 — 12x12 violet
    img = canvas(12, 12)
    draw_diamond(img, 5, 5, 5, XP_T3_MAIN, XP_T3_EDGE)
    px(img, 5, 5, WHITE)
    save(img, os.path.join(out_dir, "vfx_xp_orb_t3.png"))

    # T4 — 14x14 or, etoile 3px au centre
    img = canvas(14, 14)
    draw_diamond(img, 6, 6, 6, XP_T4_MAIN, XP_T4_EDGE)
    # Etoile 3px : centre + 4 branches de 1px
    for sx, sy in [(6, 6), (5, 6), (7, 6), (6, 5), (6, 7)]:
        px(img, sx, sy, WHITE)
    save(img, os.path.join(out_dir, "vfx_xp_orb_t4.png"))

    print(f"  Orbes XP : 4 fichiers generes dans {out_dir}")


# ─── 2. REVAMP RUSTSWARM 32x32 ────────────────────────────────────────────────
# 3 spheres organiques reliees par filaments
# Corps central (rayon 5) + 2 satellites (rayon 3) + filaments

def draw_rustswarm(img, idle_frame=0, move_frame=None, death_frame=None):
    cx, cy = 16, 17

    if death_frame is not None:
        if death_frame == 0:
            # gonflement
            circle(img, cx, cy, 7, RUST_LIGHT)
            circle(img, cx, cy, 5, RUST_MID)
            circle(img, cx, cy, 3, RUST_DARK)
            px(img, cx, cy - 1, WHITE)
        elif death_frame < 4:
            spread = death_frame * 5
            # fragments qui s'arrachent
            for angle_deg in range(0, 360, 40):
                rad = math.radians(angle_deg)
                fx = int(cx + math.cos(rad) * spread)
                fy = int(cy + math.sin(rad) * spread)
                circle(img, fx, fy, max(1, 3 - death_frame), RUST_MID)
            # fragment central residuel
            if death_frame < 3:
                circle(img, cx, cy, 3 - death_frame, RUST_DARK)
        # frame 4 = transparent (mort)
        return

    # Oscillation verticale des satellites (idle)
    osc = [0, 1, 0][idle_frame % 3] if move_frame is None else 0

    # Positions des 3 spheres
    # Corps central
    main_cx, main_cy = cx, cy
    main_r = 5

    # Satellites : top-left et bottom-right
    # En move, les filaments s'etirent vers la direction de deplacement (on simule en decalant)
    move_stretch = 0
    if move_frame is not None:
        stretches = [0, 2, 3, 1]
        move_stretch = stretches[move_frame % 4]

    sat1_cx = cx - 7 - move_stretch
    sat1_cy = cy - 4 + osc
    sat1_r = 3

    sat2_cx = cx + 7 + move_stretch
    sat2_cy = cy + 3 - osc
    sat2_r = 3

    # Filament sat1 → corps central
    # Ligne simple de FILAMENT entre les deux centres
    steps = 8
    for s in range(1, steps):
        t = s / steps
        fx = int(sat1_cx + (main_cx - sat1_cx) * t)
        fy = int(sat1_cy + (main_cy - sat1_cy) * t)
        # eviter d'ecraser les corps
        dist_main = math.sqrt((fx - main_cx)**2 + (fy - main_cy)**2)
        dist_sat = math.sqrt((fx - sat1_cx)**2 + (fy - sat1_cy)**2)
        if dist_main > main_r - 1 and dist_sat > sat1_r - 1:
            px(img, fx, fy, FILAMENT)
            px(img, fx, fy - 1, FILAMENT_HI)

    # Filament sat2 → corps central
    for s in range(1, steps):
        t = s / steps
        fx = int(sat2_cx + (main_cx - sat2_cx) * t)
        fy = int(sat2_cy + (main_cy - sat2_cy) * t)
        dist_main = math.sqrt((fx - main_cx)**2 + (fy - main_cy)**2)
        dist_sat = math.sqrt((fx - sat2_cx)**2 + (fy - sat2_cy)**2)
        if dist_main > main_r - 1 and dist_sat > sat2_r - 1:
            px(img, fx, fy, FILAMENT)

    # Corps central
    circle(img, main_cx, main_cy, main_r, RUST_DARK)
    circle(img, main_cx, main_cy, main_r - 1, RUST_MID)
    # Highlight nord-ouest
    for hx, hy in [(-2, -3), (-1, -3), (0, -3), (-3, -2), (-3, -1)]:
        px(img, main_cx + hx, main_cy + hy, RUST_LIGHT)
    # Contour
    for angle_deg in range(0, 360, 10):
        rad = math.radians(angle_deg)
        bx = int(main_cx + math.cos(rad) * main_r)
        by = int(main_cy + math.sin(rad) * main_r)
        px(img, bx, by, BLACK)

    # Satellites
    for scx, scy, sr in [(sat1_cx, sat1_cy, sat1_r), (sat2_cx, sat2_cy, sat2_r)]:
        circle(img, scx, scy, sr, RUST_DARK)
        circle(img, scx, scy, max(1, sr - 1), RUST_MID)
        # highlight
        px(img, scx - 1, scy - 1, RUST_LIGHT)
        for angle_deg in range(0, 360, 15):
            rad = math.radians(angle_deg)
            bx = int(scx + math.cos(rad) * sr)
            by = int(scy + math.sin(rad) * sr)
            px(img, bx, by, BLACK)


def generate_rustswarm_v2(out_dir):
    # idle 3 frames
    for i in range(3):
        img = canvas(32, 32)
        draw_rustswarm(img, idle_frame=i)
        save(img, os.path.join(out_dir, f"enemy_rustswarm_idle_{i+1:02d}.png"))

    # move 4 frames
    for i in range(4):
        img = canvas(32, 32)
        draw_rustswarm(img, idle_frame=0, move_frame=i)
        save(img, os.path.join(out_dir, f"enemy_rustswarm_move_{i+1:02d}.png"))

    # death 5 frames
    for i in range(5):
        img = canvas(32, 32)
        draw_rustswarm(img, death_frame=i)
        save(img, os.path.join(out_dir, f"enemy_rustswarm_death_{i+1:02d}.png"))

    print(f"  RustSwarm revamp : {3+4+5} frames -> {out_dir}")


# ─── 3. REVAMP DRONE CORROMPU 32x32 ──────────────────────────────────────────
# Disque hexagonal sombre, 3 pales en croix, oeil rouge central

def draw_hexagon(img, cx, cy, r, fill, edge):
    """Hexagone approximatif pixel art (flat-top)."""
    for dy in range(-r, r + 1):
        # Largeur a cette hauteur pour un hex flat-top
        half_w = int(r - abs(dy) * 0.5)
        for dx in range(-half_w, half_w + 1):
            if abs(dx) == half_w or abs(dy) == r:
                px(img, cx + dx, cy + dy, edge)
            else:
                px(img, cx + dx, cy + dy, fill)


def draw_drone_v2(img, phase=0, death_frame=None):
    cx, cy = 16, 16

    if death_frame is not None:
        if death_frame == 0:
            # Oeil qui s'eteint
            draw_hexagon(img, cx, cy, 7, DRONE_BODY, BLACK)
            # pales partiellement arrachees
            for angle in [0, 120, 240]:
                rad = math.radians(angle + phase * 30)
                blade_cx = int(cx + math.cos(rad) * 5)
                blade_cy = int(cy + math.sin(rad) * 5)
                rect(img, blade_cx - 1, blade_cy - 1, blade_cx + 1, blade_cy + 1, DRONE_BLADE)
            # oeil eteint
            px(img, cx, cy, DRONE_EYE_DK)
            px(img, cx + 1, cy, DRONE_EYE_DK)
        elif death_frame == 1:
            # Corps fissure
            draw_hexagon(img, cx, cy, 6, DRONE_BODY, BLACK)
            # fissure diagonale
            for i in range(-4, 5):
                px(img, cx + i, cy + i, (0x66, 0x66, 0x77, 255))
            px(img, cx, cy, DRONE_EYE_DK)
        elif death_frame == 2:
            # Debris disperses
            for angle_deg in range(0, 360, 45):
                rad = math.radians(angle_deg + 10)
                fx = int(cx + math.cos(rad) * 6)
                fy = int(cy + math.sin(rad) * 6)
                rect(img, fx - 1, fy - 1, fx + 1, fy + 1, DRONE_BLADE2)
            # fragment central
            rect(img, cx - 2, cy - 2, cx + 2, cy + 2, DRONE_BODY)
            px(img, cx, cy, (0x33, 0x00, 0x00, 255))
        # frame 3 = transparent
        return

    # Rotation des pales : chaque frame tourne de 30 degres
    blade_rotation = phase * 30  # degres

    # Corps hexagonal
    draw_hexagon(img, cx, cy, 7, DRONE_BODY, BLACK)

    # Detail interne : anneau leger
    for angle_deg in range(0, 360, 20):
        rad = math.radians(angle_deg)
        rx = int(cx + math.cos(rad) * 4)
        ry = int(cy + math.sin(rad) * 4)
        px(img, rx, ry, METAL_MID)

    # 3 pales en croix (120 deg de separation), partent du centre
    for base_angle in [0, 120, 240]:
        angle = base_angle + blade_rotation
        rad = math.radians(angle)
        # La pale est un rectangle allonge 2x6 px dans la direction
        for t in range(2, 8):
            bx = int(cx + math.cos(rad) * t)
            by = int(cy + math.sin(rad) * t)
            # epaisseur 2px perpendiculaire
            perp_rad = rad + math.pi / 2
            px(img, bx, by, DRONE_BLADE)
            px(img, bx + int(math.cos(perp_rad)), by + int(math.sin(perp_rad)), DRONE_BLADE2)
        # Bout de pale : pixel plus claire
        tip_x = int(cx + math.cos(rad) * 8)
        tip_y = int(cy + math.sin(rad) * 8)
        px(img, tip_x, tip_y, BLADE_EDGE)

    # Oeil central rouge 2x2 px luisant
    eye_bright = (phase % 3 != 2)
    eye_col = DRONE_EYE if eye_bright else DRONE_EYE_DK
    px(img, cx, cy, eye_col)
    px(img, cx + 1, cy, eye_col)
    px(img, cx, cy + 1, eye_col)
    # reflet blanc au centre
    if eye_bright:
        px(img, cx, cy, (0xFF, 0x88, 0x88, 255))

    # Contour exterieur renforce
    outline(img, cx - 7, cy - 7, cx + 7, cy + 7, BLACK)


def generate_drone_v2(out_dir):
    # idle 3 frames
    for i in range(3):
        img = canvas(32, 32)
        draw_drone_v2(img, phase=i)
        save(img, os.path.join(out_dir, f"enemy_drone_idle_{i+1:02d}.png"))

    # move 4 frames (pales inclinees = rotation plus prononcee)
    for i in range(4):
        img = canvas(32, 32)
        draw_drone_v2(img, phase=i + 1)  # rotation decalee
        save(img, os.path.join(out_dir, f"enemy_drone_move_{i+1:02d}.png"))

    # death 4 frames
    for i in range(4):
        img = canvas(32, 32)
        draw_drone_v2(img, death_frame=i)
        save(img, os.path.join(out_dir, f"enemy_drone_death_{i+1:02d}.png"))

    print(f"  Drone revamp : {3+4+4} frames -> {out_dir}")


# ─── 4. REVAMP SENTINELLE CORROMPUE 32x32 ─────────────────────────────────────
# Bipede angulaire, torse rectangulaire, canon sur l'epaule droite

def draw_sentinel_v2(img, idle_frame=0, move_frame=None, attack_frame=None, death_frame=None):
    # Origine : centre du torse a (16, 14)
    tx, ty = 8, 4   # top-left du torse (10x16 px)

    if death_frame is not None:
        if death_frame < 4:
            # Effondrement progressif : corps descend et s'incline
            offset_y = death_frame * 2
            # Torse
            rect(img, tx, ty + offset_y, tx + 13, ty + 15 + offset_y, SENT_BODY)
            rect(img, tx + 1, ty + 1 + offset_y, tx + 12, ty + 4 + offset_y, SENT_DARK)
            outline(img, tx, ty + offset_y, tx + 13, ty + 15 + offset_y, BLACK)
            # Tete penchee
            hx = death_frame  # lean droit en tombant
            rect(img, tx + 3 + hx, ty - 6 + offset_y, tx + 10 + hx, ty - 1 + offset_y, SENT_SHADOW)
            # yeux eteints
            px(img, tx + 4 + hx, ty - 4 + offset_y, DRONE_EYE_DK)
            px(img, tx + 8 + hx, ty - 4 + offset_y, DRONE_EYE_DK)
            outline(img, tx + 3 + hx, ty - 6 + offset_y, tx + 10 + hx, ty - 1 + offset_y, BLACK)
            # Jambes partielles
            if death_frame < 2:
                rect(img, tx + 2, ty + 15 + offset_y, tx + 5, ty + 21 + offset_y, SENT_SHADOW)
                rect(img, tx + 8, ty + 15 + offset_y, tx + 11, ty + 21 + offset_y, SENT_SHADOW)
        elif death_frame == 4:
            # Corps effondre au sol (rectangle aplati)
            rect(img, tx - 2, ty + 20, tx + 15, ty + 26, SENT_SHADOW)
            outline(img, tx - 2, ty + 20, tx + 15, ty + 26, BLACK)
        else:
            # Dissolution
            alpha = max(0, 200 - (death_frame - 4) * 60)
            tmp = canvas(32, 32)
            draw_sentinel_v2(tmp, idle_frame=0)
            fade = Image.new("RGBA", (32, 32), (0, 0, 0, alpha))
            merged = Image.alpha_composite(tmp, fade)
            img.paste(merged, (0, 0), merged)
        return

    # Animation de marche
    leg_bob = 0
    if move_frame is not None:
        leg_bobs = [0, 1, 2, 1, 0, -1]
        leg_bob = leg_bobs[move_frame % 6]
    elif idle_frame is not None:
        idle_bobs = [0, 0, 1, 0]
        leg_bob = idle_bobs[idle_frame % 4]

    # Jambes (courtes, mecaniques)
    leg_y_base = ty + 16
    # Jambe gauche
    rect(img, tx + 1, leg_y_base + leg_bob, tx + 5, leg_y_base + 6 + leg_bob, SENT_SHADOW)
    outline(img, tx + 1, leg_y_base + leg_bob, tx + 5, leg_y_base + 6 + leg_bob, BLACK)
    # Pied gauche
    rect(img, tx, leg_y_base + 6 + leg_bob, tx + 6, leg_y_base + 8 + leg_bob, SENT_DARK)
    # Jambe droite (phase opposee)
    rect(img, tx + 8, leg_y_base - leg_bob, tx + 12, leg_y_base + 6 - leg_bob, SENT_SHADOW)
    outline(img, tx + 8, leg_y_base - leg_bob, tx + 12, leg_y_base + 6 - leg_bob, BLACK)
    rect(img, tx + 7, leg_y_base + 6 - leg_bob, tx + 13, leg_y_base + 8 - leg_bob, SENT_DARK)

    # Torse rectangulaire (14x12 px)
    rect(img, tx, ty, tx + 13, ty + 15, SENT_BODY)
    # Plaque d'armure sur le torse
    rect(img, tx + 1, ty + 1, tx + 12, ty + 4, SENT_DARK)
    # Highlight haut du torse
    hline(img, ty, tx + 1, tx + 12, METAL_LIGHT)
    outline(img, tx, ty, tx + 13, ty + 15, BLACK)

    # Tete (8x6 px, sur le torse)
    head_tx = tx + 3
    head_ty = ty - 6
    rect(img, head_tx, head_ty, head_tx + 7, head_ty + 5, SENT_DARK)
    # Visiere orange
    rect(img, head_tx + 1, head_ty + 2, head_tx + 6, head_ty + 4, VISOR)
    outline(img, head_tx, head_ty, head_tx + 7, head_ty + 5, BLACK)
    # Yeux (2x1 chacun dans la visiere)
    px(img, head_tx + 1, head_ty + 3, CANON_TIP)
    px(img, head_tx + 2, head_ty + 3, CANON_TIP)
    px(img, head_tx + 5, head_ty + 3, CANON_TIP)
    px(img, head_tx + 6, head_ty + 3, CANON_TIP)

    # Canon sur l'epaule droite
    canon_y = ty + 2
    canon_flash = attack_frame is not None and attack_frame >= 2

    # Socle du canon sur epaule
    rect(img, tx + 13, canon_y, tx + 15, canon_y + 4, METAL_DARK)
    # Canon (tube 8x2)
    rect(img, tx + 15, canon_y + 1, tx + 22, canon_y + 3, METAL_MID)
    outline(img, tx + 15, canon_y + 1, tx + 22, canon_y + 3, BLACK)
    # Bout lumineux
    if canon_flash:
        # Flash : bout brulant + flash jaune
        circle(img, tx + 23, canon_y + 2, 2, CANON_TIP)
        px(img, tx + 24, canon_y + 2, WHITE)
    else:
        px(img, tx + 22, canon_y + 2, CANON_TIP)

    # Oscillation du canon en idle
    if idle_frame is not None and move_frame is None and attack_frame is None:
        bob_pxs = [0, 0, 1, 0]
        # Deja integre dans leg_bob mais le canon oscille independamment
        # (visuel : leger mouvement vertical du bout du canon)


def generate_sentinel_v2(out_dir):
    # idle 4 frames
    for i in range(4):
        img = canvas(32, 32)
        draw_sentinel_v2(img, idle_frame=i)
        save(img, os.path.join(out_dir, f"enemy_sentinel_idle_{i+1:02d}.png"))

    # move 6 frames
    for i in range(6):
        img = canvas(32, 32)
        draw_sentinel_v2(img, move_frame=i)
        save(img, os.path.join(out_dir, f"enemy_sentinel_move_{i+1:02d}.png"))

    # attack 4 frames
    for i in range(4):
        img = canvas(32, 32)
        draw_sentinel_v2(img, attack_frame=i)
        save(img, os.path.join(out_dir, f"enemy_sentinel_attack_{i+1:02d}.png"))

    # death 6 frames
    for i in range(6):
        img = canvas(32, 32)
        draw_sentinel_v2(img, death_frame=i)
        save(img, os.path.join(out_dir, f"enemy_sentinel_death_{i+1:02d}.png"))

    print(f"  Sentinel revamp : {4+6+4+6} frames -> {out_dir}")


# ─── 5. REVAMP COLOSSE GREFFE 48x48 ──────────────────────────────────────────
# Masse courbee, plaques metalliques, bras mecaniques, implants violets 4x4 px

def draw_colossus_v2(img, breathe_off=0, attack_frame=None, death_frame=None):
    # Centre bas du corps
    cx, cy = 24, 32

    if death_frame is not None:
        if death_frame < 5:
            # Chute + decomposition
            fall = death_frame * 2
            draw_colossus_v2(img, breathe_off=fall)
            # Fondu progressif
            fade_alpha = death_frame * 20
            fade = Image.new("RGBA", (48, 48), (0, 0, 0, fade_alpha))
            img.paste(Image.alpha_composite(img, fade), (0, 0))
        elif death_frame == 5:
            draw_colossus_v2(img, breathe_off=8)
        elif death_frame == 6:
            # Frame 7 (index 6) : flash violet — eclat du Noyau
            draw_colossus_v2(img, breathe_off=8)
            flash = Image.new("RGBA", (48, 48), (0xAA, 0x44, 0xFF, 140))
            img.paste(Image.alpha_composite(img, flash), (0, 0))
            # Particules violettes qui s'arrachent
            for angle_deg in range(0, 360, 60):
                rad = math.radians(angle_deg)
                fx = int(cx + math.cos(rad) * 12)
                fy = int(cy - 8 + math.sin(rad) * 12)
                circle(img, fx, fy, 2, IMPLANT_VIO)
        elif death_frame < 10:
            # Dissolution finale
            fade_alpha = min(255, (death_frame - 6) * 70)
            fade = Image.new("RGBA", (48, 48), (0, 0, 0, fade_alpha))
            img.paste(Image.alpha_composite(img, fade), (0, 0))
        return

    by = breathe_off  # offset vertical de respiration

    # --- Jambes mecaniques (larges) ---
    # Jambe gauche
    rect(img, cx - 9, cy + 2 + by, cx - 4, cy + 14 + by, METAL_GREY)
    rect(img, cx - 10, cy + 12 + by, cx - 3, cy + 16 + by, METAL_DARK)  # pied
    outline(img, cx - 9, cy + 2 + by, cx - 4, cy + 14 + by, BLACK)
    # Jambe droite
    rect(img, cx + 4, cy + 2 + by, cx + 9, cy + 14 + by, METAL_GREY)
    rect(img, cx + 3, cy + 12 + by, cx + 10, cy + 16 + by, METAL_DARK)
    outline(img, cx + 4, cy + 2 + by, cx + 9, cy + 14 + by, BLACK)
    # Detail rouille sur jambes
    px(img, cx - 7, cy + 11 + by, (0x99, 0x55, 0x22, 255))
    px(img, cx + 7, cy + 10 + by, (0x99, 0x55, 0x22, 255))

    # --- Corps / Torse (18x20 px, incurve) ---
    body_x0 = cx - 9
    body_y0 = cy - 18 + by
    body_x1 = cx + 9
    body_y1 = cy + 2 + by

    rect(img, body_x0, body_y0, body_x1, body_y1, COL_FLESH)

    # Plaques metalliques greffees sur le torse
    rect(img, body_x0 + 1, body_y0 + 2, body_x1 - 1, body_y0 + 8, COL_PLATE)
    rect(img, body_x0 + 1, body_y0 + 2, body_x1 - 1, body_y0 + 3, COL_ARM)  # highlight plaque
    # Plaques laterales
    rect(img, body_x0, body_y0 + 9, body_x0 + 3, body_y1 - 2, COL_PLATE)
    rect(img, body_x1 - 3, body_y0 + 9, body_x1, body_y1 - 2, COL_PLATE)

    # Fissures avec implants violets (2px de large)
    for fis_y in [body_y0 + 11, body_y0 + 15]:
        for fis_x in range(body_x0 + 3, body_x1 - 2, 4):
            px(img, fis_x, fis_y, IMPLANT_VIO)
            px(img, fis_x + 1, fis_y, IMPLANT_GLOW)

    outline(img, body_x0, body_y0, body_x1, body_y1, BLACK)

    # --- Implants epaule gauche (4x4 px violets luisants) ---
    impl_gx, impl_gy = body_x0 - 1, body_y0 + 2 + by // 2
    rect(img, impl_gx, impl_gy, impl_gx + 3, impl_gy + 3, IMPLANT_VIO)
    outline(img, impl_gx, impl_gy, impl_gx + 3, impl_gy + 3, BLACK)
    # Pixel brillant centre
    px(img, impl_gx + 1, impl_gy + 1, IMPLANT_GLOW)

    # --- Implant dos (4x4) ---
    impl_dx, impl_dy = cx - 2, body_y0 + 4
    rect(img, impl_dx, impl_dy, impl_dx + 3, impl_dy + 3, IMPLANT_VIO)
    px(img, impl_dx + 1, impl_dy + 1, IMPLANT_GLOW)
    outline(img, impl_dx, impl_dy, impl_dx + 3, impl_dy + 3, BLACK)

    # --- Tete petite et courbee (8x7 px) ---
    head_x = cx - 4
    head_y = body_y0 - 7 + by
    rect(img, head_x, head_y, head_x + 7, head_y + 6, COL_FLESH)
    # Plaques sur la tete
    rect(img, head_x + 1, head_y, head_x + 6, head_y + 2, COL_PLATE)
    # Yeux rouges (2x1)
    eye_bright = death_frame is None
    eye_col = (0xFF, 0x22, 0x22, 255) if eye_bright else (0x22, 0x00, 0x00, 255)
    px(img, head_x + 1, head_y + 3, eye_col)
    px(img, head_x + 2, head_y + 3, eye_col)
    px(img, head_x + 5, head_y + 3, eye_col)
    px(img, head_x + 6, head_y + 3, eye_col)
    outline(img, head_x, head_y, head_x + 7, head_y + 6, BLACK)

    # --- Bras mecanique droit (lourd, 10x18 px) ---
    arm_base_attack = 0
    if attack_frame is not None:
        arm_raises = [0, -3, -6, -3, 0]
        arm_base_attack = arm_raises[min(attack_frame, 4)]

    arm_rx = body_x1 + 1
    arm_ry = body_y0 + 2 + by + arm_base_attack
    rect(img, arm_rx, arm_ry, arm_rx + 9, arm_ry + 18, COL_ARM)
    rect(img, arm_rx + 1, arm_ry + 1, arm_rx + 8, arm_ry + 4, METAL_LIGHT)  # highlight
    # Pince en bas du bras
    pince_open = attack_frame is not None and 1 <= attack_frame <= 3
    if pince_open:
        rect(img, arm_rx, arm_ry + 16, arm_rx + 4, arm_ry + 20, COL_ARM)  # machoire sup
        rect(img, arm_rx + 5, arm_ry + 18, arm_rx + 9, arm_ry + 22, COL_ARM)  # machoire inf
    else:
        rect(img, arm_rx + 1, arm_ry + 16, arm_rx + 8, arm_ry + 20, COL_ARM)
    outline(img, arm_rx, arm_ry, arm_rx + 9, arm_ry + 18, BLACK)

    # --- Bras gauche organique plus petit ---
    arm_lx = body_x0 - 5
    arm_ly = body_y0 + 5 + by
    rect(img, arm_lx, arm_ly, arm_lx + 4, arm_ly + 12, COL_FLESH)
    outline(img, arm_lx, arm_ly, arm_lx + 4, arm_ly + 12, BLACK)


def generate_colossus_v2(out_dir):
    # idle 4 frames : respiration (corps monte/descend 1px)
    breathe_seq = [0, 0, 1, 1]
    for i in range(4):
        img = canvas(48, 48)
        draw_colossus_v2(img, breathe_off=breathe_seq[i])
        save(img, os.path.join(out_dir, f"enemy_colossus_idle_{i+1:02d}.png"))

    # move 6 frames : demarche lourde
    move_breathe = [0, 1, 2, 1, 0, -1]
    for i in range(6):
        img = canvas(48, 48)
        draw_colossus_v2(img, breathe_off=max(0, move_breathe[i]))
        save(img, os.path.join(out_dir, f"enemy_colossus_move_{i+1:02d}.png"))

    # attack 5 frames : bras leves
    for i in range(5):
        img = canvas(48, 48)
        draw_colossus_v2(img, attack_frame=i)
        save(img, os.path.join(out_dir, f"enemy_colossus_attack_{i+1:02d}.png"))

    # death 10 frames
    for i in range(10):
        img = canvas(48, 48)
        draw_colossus_v2(img, death_frame=i)
        save(img, os.path.join(out_dir, f"enemy_colossus_death_{i+1:02d}.png"))

    print(f"  Colosse revamp : {4+6+5+10} frames -> {out_dir}")


# ─── 6. MINI-BOSS : RUST STALKER 64x64 ───────────────────────────────────────
# Araignee mecanique : corps ovale rouille 20x12, 6 pattes 3px, yeux rouges 2px

def draw_rust_stalker(img, move_phase=0, attack_frame=None, death_frame=None):
    cx, cy = 32, 30  # centre du corps

    if death_frame is not None:
        if death_frame < 6:
            # Corps qui s'effondre
            fall = death_frame * 2
            draw_rust_stalker(img, move_phase=0)
            # Pattes qui s'arrachent : masquer progressivement
            # Fondu
            fade_alpha = death_frame * 15
            tmp = canvas(64, 64)
            draw_rust_stalker(tmp, move_phase=0)
            fade = Image.new("RGBA", (64, 64), (0x44, 0x11, 0x00, fade_alpha))
            merged = Image.alpha_composite(tmp, fade)
            img.paste(merged, (0, fall), merged)
        elif death_frame < 9:
            # Explosion de fragments
            spread = (death_frame - 5) * 7
            for angle_deg in range(0, 360, 30):
                rad = math.radians(angle_deg)
                fx = int(cx + math.cos(rad) * spread)
                fy = int(cy + math.sin(rad) * spread)
                size = max(1, 4 - (death_frame - 6))
                circle(img, fx, fy, size, SPIDER_BODY)
                circle(img, fx, fy, max(0, size - 2), SPIDER_CARA)
        # frames 9-11 = transparent (quelques debris)
        elif death_frame < 12:
            for angle_deg in range(0, 360, 60):
                rad = math.radians(angle_deg + death_frame * 15)
                spread = 12 + (death_frame - 9) * 4
                fx = int(cx + math.cos(rad) * spread)
                fy = int(cy + math.sin(rad) * spread)
                circle(img, fx, fy, 1, RUST_DARK)
        return

    # Animation des pattes : move_phase cycle 0-5 pour 6 frames
    # 3 pattes gauche + 3 pattes droite alternees
    leg_offsets_L = [
        (-14, -8), (-16, 0), (-14, 8)   # gauche haut / milieu / bas
    ]
    leg_offsets_R = [
        (14, -8), (16, 0), (14, 8)      # droite haut / milieu / bas
    ]

    # Phase de mouvement : les pattes L et R alternent
    leg_bobs = [0, 1, 0, -1, 0, 1]
    bob = leg_bobs[move_phase % 6]

    # Pattes gauche (3 segments de 3px)
    for li, (lox, loy) in enumerate(leg_offsets_L):
        leg_bob_li = bob if li % 2 == 0 else -bob
        # Point d'attache sur le corps
        attach_x = cx - 8
        attach_y = cy + loy // 3
        # Extremite de la patte (genou a mi-chemin, puis extremite)
        mid_x = cx + lox // 2 - 3
        mid_y = cy + loy // 2 + leg_bob_li
        end_x = cx + lox
        end_y = cy + loy + leg_bob_li

        # Segment 1 : corps -> genou
        draw_line(img, attach_x, attach_y, mid_x, mid_y, SPIDER_LEG)
        # Segment 2 : genou -> extremite
        draw_line(img, mid_x, mid_y, end_x, end_y, SPIDER_LEG)
        # Epaisseur 2px : decale d'un pixel
        draw_line(img, attach_x, attach_y + 1, mid_x, mid_y + 1, (0x88, 0x55, 0x22, 255))
        # Griffe
        px(img, end_x - 1, end_y, BLACK)
        px(img, end_x, end_y + 1, BLACK)

    # Pattes droite
    for li, (rox, roy) in enumerate(leg_offsets_R):
        leg_bob_ri = -bob if li % 2 == 0 else bob
        attach_x = cx + 8
        attach_y = cy + roy // 3
        mid_x = cx + rox // 2 + 3
        mid_y = cy + roy // 2 + leg_bob_ri
        end_x = cx + rox
        end_y = cy + roy + leg_bob_ri

        draw_line(img, attach_x, attach_y, mid_x, mid_y, SPIDER_LEG)
        draw_line(img, mid_x, mid_y, end_x, end_y, SPIDER_LEG)
        draw_line(img, attach_x, attach_y + 1, mid_x, mid_y + 1, (0x88, 0x55, 0x22, 255))
        px(img, end_x + 1, end_y, BLACK)
        px(img, end_x, end_y + 1, BLACK)

    # Corps ovale (20x12 px)
    body_x0 = cx - 10
    body_y0 = cy - 6
    body_x1 = cx + 10
    body_y1 = cy + 6

    # Corps principal rouille
    draw_ellipse_fill(img, cx, cy, 10, 6, SPIDER_BODY)

    # Carapace dorsale plus claire (partie superieure)
    draw_ellipse_fill(img, cx, cy - 1, 8, 4, SPIDER_CARA)

    # Fissures sur la carapace
    for fis_x in range(cx - 4, cx + 5, 3):
        px(img, fis_x, cy - 2, SPIDER_CRAK)
        px(img, fis_x + 1, cy - 1, SPIDER_CRAK)

    # Contour du corps
    draw_ellipse_outline(img, cx, cy, 10, 6, BLACK)

    # Deux yeux rouges luisants (2x2 px chacun)
    attack_glow = attack_frame is not None and attack_frame >= 2
    eye_col = (0xFF, 0x44, 0x44, 255) if attack_glow else SPIDER_EYE
    # Position : avant du corps (gauche du canvas car vue de dessus)
    rect(img, cx - 7, cy - 2, cx - 5, cy - 1, eye_col)
    rect(img, cx - 7, cy + 1, cx - 5, cy + 2, eye_col)
    if attack_glow:
        # Eclat plus vif en attaque
        px(img, cx - 6, cy - 2, (0xFF, 0xAA, 0xAA, 255))
        px(img, cx - 6, cy + 1, (0xFF, 0xAA, 0xAA, 255))

    # Attaque : griffes avant levees
    if attack_frame is not None:
        raise_y = [0, -3, -5, -4, -2][min(attack_frame, 4)]
        # Deux pattes avant levees
        for side in [-1, 1]:
            end_x = cx + side * 14
            end_y = cy - 8 + raise_y
            draw_line(img, cx + side * 8, cy, end_x, end_y, SPIDER_LEG)
            # Griffe en attaque
            px(img, end_x, end_y - 1, BLACK)
            px(img, end_x + side, end_y, BLACK)


def draw_line(img, x0, y0, x1, y1, color):
    """Ligne de Bresenham."""
    dx = abs(x1 - x0)
    dy = abs(y1 - y0)
    sx = 1 if x0 < x1 else -1
    sy = 1 if y0 < y1 else -1
    err = dx - dy
    while True:
        px(img, x0, y0, color)
        if x0 == x1 and y0 == y1:
            break
        e2 = 2 * err
        if e2 > -dy:
            err -= dy
            x0 += sx
        if e2 < dx:
            err += dx
            y0 += sy


def draw_ellipse_fill(img, cx, cy, rx, ry, color):
    """Ellipse pleine."""
    for dy in range(-ry, ry + 1):
        for dx in range(-rx, rx + 1):
            if (dx / rx) ** 2 + (dy / ry) ** 2 <= 1.0:
                px(img, cx + dx, cy + dy, color)


def draw_ellipse_outline(img, cx, cy, rx, ry, color):
    """Contour d'ellipse (Bresenham ellipse)."""
    for angle_deg in range(0, 360, 2):
        rad = math.radians(angle_deg)
        ex = int(cx + math.cos(rad) * rx)
        ey = int(cy + math.sin(rad) * ry)
        px(img, ex, ey, color)


def generate_rust_stalker(out_dir):
    os.makedirs(out_dir, exist_ok=True)

    # idle 4 frames
    for i in range(4):
        img = canvas(64, 64)
        draw_rust_stalker(img, move_phase=i * 2)
        save(img, os.path.join(out_dir, f"rust_stalker_idle_{i+1:02d}.png"))

    # move 6 frames
    for i in range(6):
        img = canvas(64, 64)
        draw_rust_stalker(img, move_phase=i)
        save(img, os.path.join(out_dir, f"rust_stalker_move_{i+1:02d}.png"))

    # attack 5 frames
    for i in range(5):
        img = canvas(64, 64)
        draw_rust_stalker(img, attack_frame=i)
        save(img, os.path.join(out_dir, f"rust_stalker_attack_{i+1:02d}.png"))

    # death 12 frames
    for i in range(12):
        img = canvas(64, 64)
        draw_rust_stalker(img, death_frame=i)
        save(img, os.path.join(out_dir, f"rust_stalker_death_{i+1:02d}.png"))

    print(f"  RustStalker : {4+6+5+12} frames -> {out_dir}")


# ─── 7. MINI-BOSS : MASTER SENTINEL 64x64 ────────────────────────────────────
# Sentinelle grande taille, bouclier lateral, 2 canons, visiere orange

def draw_master_sentinel(img, move_phase=0, attack_frame=None, death_frame=None):
    # Corps centre a (32, 28)
    cx, cy = 32, 28

    if death_frame is not None:
        if death_frame < 7:
            fall = death_frame * 2
            tmp = canvas(64, 64)
            draw_master_sentinel(tmp, move_phase=0)
            fade_alpha = death_frame * 18
            fade = Image.new("RGBA", (64, 64), (0, 0, 0, fade_alpha))
            merged = Image.alpha_composite(tmp, fade)
            img.paste(merged, (0, fall), merged)
        elif death_frame < 11:
            spread = (death_frame - 6) * 6
            for angle_deg in range(0, 360, 40):
                rad = math.radians(angle_deg)
                fx = int(cx + math.cos(rad) * spread)
                fy = int(cy + math.sin(rad) * spread)
                rect(img, fx - 2, fy - 2, fx + 2, fy + 2, SENT_BODY)
        elif death_frame < 14:
            for angle_deg in range(0, 360, 60):
                rad = math.radians(angle_deg + death_frame * 20)
                spread = 14 + (death_frame - 11) * 3
                fx = int(cx + math.cos(rad) * spread)
                fy = int(cy + math.sin(rad) * spread)
                rect(img, fx - 1, fy - 1, fx + 1, fy + 1, SENT_SHADOW)
        return

    # Animation de marche
    leg_bobs = [0, 2, 3, 2, 0, -2]
    bob = leg_bobs[move_phase % 6]

    # Jambes (plus longues que la standard)
    leg_y = cy + 18
    # Jambe gauche
    rect(img, cx - 10, leg_y + bob, cx - 5, leg_y + 14 + bob, SENT_SHADOW)
    rect(img, cx - 12, leg_y + 12 + bob, cx - 4, leg_y + 16 + bob, SENT_DARK)  # pied
    outline(img, cx - 10, leg_y + bob, cx - 5, leg_y + 14 + bob, BLACK)
    # Jambe droite (phase opposee)
    rect(img, cx + 5, leg_y - bob, cx + 10, leg_y + 14 - bob, SENT_SHADOW)
    rect(img, cx + 4, leg_y + 12 - bob, cx + 12, leg_y + 16 - bob, SENT_DARK)
    outline(img, cx + 5, leg_y - bob, cx + 10, leg_y + 14 - bob, BLACK)

    # Torse massif (20x22 px)
    body_x0 = cx - 10
    body_y0 = cy - 5
    body_x1 = cx + 10
    body_y1 = cy + 17

    rect(img, body_x0, body_y0, body_x1, body_y1, SENT_BODY)
    # Plaque d'armure centrale
    rect(img, body_x0 + 2, body_y0 + 1, body_x1 - 2, body_y0 + 7, SENT_DARK)
    rect(img, body_x0 + 2, body_y0 + 1, body_x1 - 2, body_y0 + 2, METAL_LIGHT)
    outline(img, body_x0, body_y0, body_x1, body_y1, BLACK)

    # Bouclier lateral gauche (grand, rectangulaire)
    shield_x = body_x0 - 10
    shield_y = body_y0 + 2
    rect(img, shield_x, shield_y, shield_x + 8, shield_y + 18, SHIELD_MID)
    rect(img, shield_x + 1, shield_y + 1, shield_x + 7, shield_y + 3, METAL_MID)  # highlight
    # Bordure du bouclier
    outline(img, shield_x, shield_y, shield_x + 8, shield_y + 18, BLACK)
    # Embleme du bouclier (ligne decorative)
    for shield_yi in range(shield_y + 6, shield_y + 14, 3):
        hline(img, shield_yi, shield_x + 2, shield_x + 6, METAL_GREY)

    # Tete (12x9 px)
    head_x = cx - 6
    head_y = body_y0 - 9
    rect(img, head_x, head_y, head_x + 11, head_y + 8, SENT_DARK)
    # Visiere orange large
    rect(img, head_x + 1, head_y + 3, head_x + 10, head_y + 6, VISOR)
    outline(img, head_x, head_y, head_x + 11, head_y + 8, BLACK)
    # Yeux (2x2 chacun dans la visiere)
    px(img, head_x + 2, head_y + 4, CANON_TIP)
    px(img, head_x + 3, head_y + 4, CANON_TIP)
    px(img, head_x + 8, head_y + 4, CANON_TIP)
    px(img, head_x + 9, head_y + 4, CANON_TIP)

    # Canon droit (sur l'epaule droite, plus gros)
    canon_attack = attack_frame is not None and attack_frame >= 3
    canon1_y = body_y0 + 2
    rect(img, body_x1, canon1_y, body_x1 + 3, canon1_y + 5, METAL_DARK)  # socle
    rect(img, body_x1 + 3, canon1_y + 1, body_x1 + 14, canon1_y + 4, METAL_MID)  # tube
    outline(img, body_x1 + 3, canon1_y + 1, body_x1 + 14, canon1_y + 4, BLACK)
    if canon_attack:
        circle(img, body_x1 + 16, canon1_y + 2, 3, CANON_TIP)
        px(img, body_x1 + 17, canon1_y + 2, WHITE)
    else:
        px(img, body_x1 + 14, canon1_y + 2, CANON_TIP)

    # Canon gauche (sur l'epaule gauche, identique)
    canon2_y = body_y0 + 8
    rect(img, body_x1, canon2_y, body_x1 + 3, canon2_y + 5, METAL_DARK)
    rect(img, body_x1 + 3, canon2_y + 1, body_x1 + 14, canon2_y + 4, METAL_MID)
    outline(img, body_x1 + 3, canon2_y + 1, body_x1 + 14, canon2_y + 4, BLACK)
    if canon_attack:
        # Les deux canons tirent ensemble
        circle(img, body_x1 + 16, canon2_y + 2, 3, CANON_TIP)
        px(img, body_x1 + 17, canon2_y + 2, WHITE)
    else:
        px(img, body_x1 + 14, canon2_y + 2, CANON_TIP)

    # Attaque : animation de tir (oscillation du corps, flash canons)
    if attack_frame is not None:
        recoil = [0, 1, 2, 3, 2, 1][min(attack_frame, 5)]
        # Corps recule legerement vers la gauche
        # (deja integre dans la position des canons)
        pass


def generate_master_sentinel(out_dir):
    os.makedirs(out_dir, exist_ok=True)

    # idle 4 frames
    for i in range(4):
        img = canvas(64, 64)
        draw_master_sentinel(img, move_phase=i)
        save(img, os.path.join(out_dir, f"master_sentinel_idle_{i+1:02d}.png"))

    # move 6 frames
    for i in range(6):
        img = canvas(64, 64)
        draw_master_sentinel(img, move_phase=i)
        save(img, os.path.join(out_dir, f"master_sentinel_move_{i+1:02d}.png"))

    # attack 6 frames
    for i in range(6):
        img = canvas(64, 64)
        draw_master_sentinel(img, attack_frame=i)
        save(img, os.path.join(out_dir, f"master_sentinel_attack_{i+1:02d}.png"))

    # death 14 frames
    for i in range(14):
        img = canvas(64, 64)
        draw_master_sentinel(img, death_frame=i)
        save(img, os.path.join(out_dir, f"master_sentinel_death_{i+1:02d}.png"))

    print(f"  MasterSentinel : {4+6+6+14} frames -> {out_dir}")


# ─── 8. FICHIERS .tres SpriteFrames ──────────────────────────────────────────

def write_spriteframes_tres(path, sprite_prefix, res_path_prefix, animations):
    """
    Genere un fichier .tres SpriteFrames Godot 4.
    animations = liste de dict :
      { "name": str, "frames": int, "speed": float, "loop": bool }
    sprite_prefix : prefixe du nom de fichier (ex: "rust_stalker")
    res_path_prefix : chemin res:// vers le dossier (ex: "res://assets/sprites/enemies/rust_stalker")
    """
    # Compter les ext_resource
    total_textures = sum(a["frames"] for a in animations)
    load_steps = 1 + total_textures

    lines = []
    lines.append(f'[gd_resource type="SpriteFrames" load_steps={load_steps} format=3]')
    lines.append('')

    # Toutes les ext_resource
    res_id = 1
    for anim in animations:
        anim_name = anim["name"]
        for f in range(1, anim["frames"] + 1):
            fname = f"{sprite_prefix}_{anim_name}_{f:02d}.png"
            fpath = f"{res_path_prefix}/{fname}"
            lines.append(f'[ext_resource type="Texture2D" path="{fpath}" id="{res_id}"]')
            res_id += 1

    lines.append('')
    lines.append('[resource]')
    lines.append('animations = [{')

    res_id = 1
    for anim_idx, anim in enumerate(animations):
        anim_name = anim["name"]
        speed = anim["speed"]
        loop = "true" if anim["loop"] else "false"
        frames_count = anim["frames"]

        # frames array
        frame_entries = []
        for f in range(frames_count):
            frame_entries.append(f'{{"duration": 1.0, "texture": ExtResource("{res_id}")}}')
            res_id += 1

        frames_str = ", ".join(frame_entries)

        lines.append(f'"frames": [{frames_str}],')
        lines.append(f'"loop": {loop},')
        lines.append(f'"name": &"{anim_name}",')
        lines.append(f'"speed": {float(speed)}')

        if anim_idx < len(animations) - 1:
            lines.append('}, {')
        else:
            lines.append('}]')

    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as f:
        f.write('\n'.join(lines) + '\n')

    print(f"  .tres genere : {path}")


# ─── MAIN ─────────────────────────────────────────────────────────────────────

def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    sprites_root = os.path.join(root, "assets", "sprites")

    print("\nGenerateur sprites v2 — Chimera Protocol")
    print(f"Racine projet : {root}\n")

    # 1. Orbes XP
    print("=== 1. Orbes XP ===")
    generate_xp_orbs(os.path.join(sprites_root, "vfx"))

    # 2. Mini-boss
    print("\n=== 2. Mini-boss ===")
    stalker_dir = os.path.join(sprites_root, "enemies", "rust_stalker")
    generate_rust_stalker(stalker_dir)
    write_spriteframes_tres(
        path=os.path.join(stalker_dir, "rust_stalker_frames.tres"),
        sprite_prefix="rust_stalker",
        res_path_prefix="res://assets/sprites/enemies/rust_stalker",
        animations=[
            {"name": "idle",   "frames": 4,  "speed": 5.0,  "loop": True},
            {"name": "move",   "frames": 6,  "speed": 8.0,  "loop": True},
            {"name": "attack", "frames": 5,  "speed": 10.0, "loop": False},
            {"name": "death",  "frames": 12, "speed": 12.0, "loop": False},
        ]
    )

    msent_dir = os.path.join(sprites_root, "enemies", "master_sentinel")
    generate_master_sentinel(msent_dir)
    write_spriteframes_tres(
        path=os.path.join(msent_dir, "master_sentinel_frames.tres"),
        sprite_prefix="master_sentinel",
        res_path_prefix="res://assets/sprites/enemies/master_sentinel",
        animations=[
            {"name": "idle",   "frames": 4,  "speed": 5.0,  "loop": True},
            {"name": "move",   "frames": 6,  "speed": 8.0,  "loop": True},
            {"name": "attack", "frames": 6,  "speed": 10.0, "loop": False},
            {"name": "death",  "frames": 14, "speed": 12.0, "loop": False},
        ]
    )

    # 3. Revamp ennemis existants
    print("\n=== 3. Revamp ennemis ===")
    generate_rustswarm_v2(os.path.join(sprites_root, "enemies", "rustswarm"))
    generate_drone_v2(os.path.join(sprites_root, "enemies", "drone"))
    generate_sentinel_v2(os.path.join(sprites_root, "enemies", "sentinel"))
    generate_colossus_v2(os.path.join(sprites_root, "enemies", "colossus"))

    # Decompte
    print("\n=== Decompte ===")
    total_png = 0
    total_tres = 0
    for dirpath, _, files in os.walk(os.path.join(sprites_root, "enemies")):
        total_png += len([f for f in files if f.endswith(".png")])
        total_tres += len([f for f in files if f.endswith(".tres")])
    for f in os.listdir(os.path.join(sprites_root, "vfx")):
        if f.endswith(".png"):
            total_png += 1

    print(f"PNG produits : {total_png}")
    print(f".tres produits : {total_tres}")
    print("Generation terminee.")


if __name__ == "__main__":
    main()
