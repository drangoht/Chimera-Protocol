"""
generate_sprites.py — Generateur de sprites pixel art pour Chimera Protocol.
Produit tous les sprites de l'iteration 1 (joueur, Essaim de Rouille, projectile, tiles sol)
et de l'iteration 2 (ennemis, animations death, VFX particles, UI icons).

Palette conforme a docs/STYLE_GUIDE.md.
Resolution : 32x32 px par frame (sauf Colosse : 48x48).
Format : PNG RGBA transparent.

Usage :
  python tools/generate_sprites.py [--out assets/sprites]
"""

import os
import sys
import math
from PIL import Image, ImageDraw

# ─── Palette (STYLE_GUIDE.md §1) ──────────────────────────────────────────────

# Matiere morte
C_SOL_DARK    = (0x1A, 0x1A, 0x22, 255)
C_SOL_MID     = (0x2D, 0x2A, 0x33, 255)
C_SOL_LIGHT   = (0x3D, 0x38, 0x40, 255)
C_MUR_MAIN    = (0x25, 0x20, 0x28, 255)
C_MUR_DETAIL  = (0x3A, 0x2E, 0x35, 255)
C_RUST_LIGHT  = (0x7A, 0x4A, 0x2A, 255)
C_RUST_DARK   = (0x4A, 0x2A, 0x15, 255)
C_METAL_GREY  = (0x4A, 0x4A, 0x52, 255)
C_METAL_DARK  = (0x2A, 0x2A, 0x32, 255)
C_DEBRIS_ORG  = (0x3A, 0x3A, 0x28, 255)

# Energie vivante / Aether
C_AETHER_PRI  = (0x00, 0xE5, 0xFF, 255)
C_AETHER_SEC  = (0x80, 0xF0, 0xFF, 255)
C_AETHER_DARK = (0x00, 0x7A, 0x99, 255)
C_AETHER_HOT  = (0xFF, 0x00, 0xCC, 255)
C_XP_GREEN    = (0xAA, 0xFF, 0x44, 255)
C_XP_GLOW     = (0xCC, 0xFF, 0x88, 255)
C_NOYAU_VIOL  = (0xAA, 0x44, 0xFF, 255)
C_NOYAU_GLOW  = (0xCC, 0x88, 0xFF, 255)
C_PLASMA_HOT  = (0xFF, 0x88, 0x00, 255)
C_PLASMA_EDGE = (0xFF, 0xD7, 0x00, 255)
C_SURCHARGE   = (0xFF, 0x22, 0x00, 255)
C_SURCH_PALE  = (0xFF, 0x66, 0x44, 255)

# Joueur Cyborg
C_BODY_MAIN   = (0x4A, 0x55, 0x66, 255)
C_BODY_HI     = (0x7A, 0x88, 0x99, 255)
C_BODY_SHADOW = (0x2A, 0x33, 0x44, 255)
C_SKIN        = (0x8A, 0x66, 0x55, 255)
C_IMPLANT     = (0x00, 0xE5, 0xFF, 255)  # identique Aether primaire

# Contour universel
C_BLACK       = (0x00, 0x00, 0x00, 255)
C_TRANSPARENT = (0x00, 0x00, 0x00, 0)
C_WHITE       = (0xFF, 0xFF, 0xFF, 255)

# Correction Aether fissures pour garantir le bloom
C_AETHER_FISS = (0x00, 0xA0, 0xBB, 255)

# UI
C_BARRE_HP    = (0x22, 0xCC, 0x66, 255)
C_BARRE_CRIT  = (0xFF, 0x33, 0x22, 255)
C_BARRE_XP    = (0xAA, 0xFF, 0x44, 255)
C_VICTOIRE    = (0x00, 0xE5, 0xFF, 255)
C_DEFAITE     = (0xCC, 0x33, 0x11, 255)


# ─── Utilitaires ──────────────────────────────────────────────────────────────

def new_canvas(w=32, h=32):
    """Cree une image RGBA transparente."""
    return Image.new("RGBA", (w, h), C_TRANSPARENT)


def px(img, x, y, color):
    """Dessine un pixel si dans les limites."""
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), color)


def rect(img, x0, y0, x1, y1, color):
    """Remplit un rectangle."""
    draw = ImageDraw.Draw(img)
    draw.rectangle([x0, y0, x1, y1], fill=color)


def outline_rect(img, x0, y0, x1, y1, fill, border):
    """Rectangle rempli + contour 1px."""
    rect(img, x0, y0, x1, y1, fill)
    draw = ImageDraw.Draw(img)
    draw.rectangle([x0, y0, x1, y1], outline=border)


def circle_filled(img, cx, cy, r, color):
    """Cercle plein."""
    for dy in range(-r, r+1):
        for dx in range(-r, r+1):
            if dx*dx + dy*dy <= r*r:
                px(img, cx+dx, cy+dy, color)


def save(img, path):
    """Sauvegarde le PNG."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "PNG")


def alpha_blend(color, alpha_factor):
    """Applique un facteur alpha a une couleur RGBA."""
    r, g, b, a = color
    return (r, g, b, int(a * alpha_factor))


# ─── JOUEUR CYBORG ────────────────────────────────────────────────────────────

def draw_player_base(img, implant_bright=True, offset_y=0, leg_phase=0):
    """
    Dessine le Cyborg sur img (32x32).
    Silhouette : tete 8x8 en haut, torse 10x10 centre, jambes 8x6 bas.
    Vue 3/4 du dessus. Zone utile : 20x28 px centree.

    implant_bright : True = cyan vif, False = cyan eteint (death frames)
    offset_y : decalage vertical pour animation de respiration
    leg_phase : 0-3 pour l'animation de marche
    """
    # Origine de la zone utile dans le canvas 32x32
    ox, oy = 6, 2  # marge gauche/haut
    oy += offset_y

    implant_col = C_IMPLANT if implant_bright else C_AETHER_DARK

    # --- Jambes (bas, 8x6 px) ---
    leg_y = oy + 22
    # Jambe gauche
    loff = [-1, 0, 1, 0][leg_phase % 4]
    rect(img, ox+2, leg_y + loff, ox+8, leg_y+5 + loff, C_BODY_SHADOW)
    # Jambe droite
    roff = [0, -1, 0, 1][leg_phase % 4]
    rect(img, ox+11, leg_y + roff, ox+17, leg_y+5 + roff, C_BODY_SHADOW)
    # Contours jambes
    draw = ImageDraw.Draw(img)
    draw.rectangle([ox+2, leg_y+loff, ox+8, leg_y+5+loff], outline=C_BLACK)
    draw.rectangle([ox+11, leg_y+roff, ox+17, leg_y+5+roff], outline=C_BLACK)

    # --- Torse / Armure (10x10 px centre) ---
    tor_y = oy + 10
    rect(img, ox+1, tor_y, ox+18, tor_y+9, C_BODY_MAIN)
    # Highlight torse (haut)
    rect(img, ox+2, tor_y, ox+17, tor_y+1, C_BODY_HI)
    # Ombre torse (bas)
    rect(img, ox+2, tor_y+8, ox+17, tor_y+9, C_BODY_SHADOW)
    # Contour torse
    draw.rectangle([ox+1, tor_y, ox+18, tor_y+9], outline=C_BLACK)

    # Implants (lignes d'energie sous le torse)
    for xi in range(ox+4, ox+16, 3):
        px(img, xi, tor_y+4, implant_col)
        px(img, xi+1, tor_y+4, implant_col)

    # Bras droit (arme) — sort a droite
    rect(img, ox+19, tor_y+2, ox+21, tor_y+7, C_BODY_MAIN)
    px(img, ox+22, tor_y+4, C_BODY_HI)  # embout arme
    draw.rectangle([ox+19, tor_y+2, ox+21, tor_y+7], outline=C_BLACK)

    # Bras gauche
    rect(img, ox-2, tor_y+2, ox, tor_y+7, C_BODY_SHADOW)
    draw.rectangle([ox-2, tor_y+2, ox, tor_y+7], outline=C_BLACK)

    # --- Tete/Casque (8x8 px) ---
    head_y = oy + 0
    rect(img, ox+3, head_y, ox+16, head_y+7, C_BODY_MAIN)
    # Highlight casque (haut)
    rect(img, ox+4, head_y, ox+15, head_y+1, C_BODY_HI)
    # Contour casque
    draw.rectangle([ox+3, head_y, ox+16, head_y+7], outline=C_BLACK)

    # Visiere / peau (zone organique : 4-6 px de chair entre casque et col)
    rect(img, ox+5, head_y+4, ox+14, head_y+6, C_SKIN)

    # Yeux implants (2x1 px chacun)
    if implant_bright:
        # Yeux brillants
        px(img, ox+6, head_y+4, implant_col)
        px(img, ox+7, head_y+4, implant_col)
        px(img, ox+12, head_y+4, implant_col)
        px(img, ox+13, head_y+4, implant_col)
    else:
        # Yeux eteints
        px(img, ox+6, head_y+4, C_BODY_SHADOW)
        px(img, ox+7, head_y+4, C_BODY_SHADOW)
        px(img, ox+12, head_y+4, C_BODY_SHADOW)
        px(img, ox+13, head_y+4, C_BODY_SHADOW)


def generate_player_sprites(out_dir):
    """Genere les 4 animations joueur."""

    # idle : 4 frames, respiration subtile (offset_y 0,0,1,1) + implants qui pulsent
    implant_states = [True, True, True, False]  # frame 4 = implant moins vif (clignotement)
    offsets = [0, 0, 1, 1]
    for i in range(4):
        img = new_canvas(32, 32)
        draw_player_base(img, implant_bright=implant_states[i], offset_y=offsets[i])
        save(img, os.path.join(out_dir, f"player_idle_{i+1:02d}.png"))

    # run_right : 6 frames, deplacement droite, animation jambes
    for i in range(6):
        img = new_canvas(32, 32)
        draw_player_base(img, implant_bright=True, offset_y=0, leg_phase=i)
        # Leger lean avant (decalage tete vers droite de 1px)
        # (deja integre dans draw_player_base avec le leg_phase)
        save(img, os.path.join(out_dir, f"player_run_right_{i+1:02d}.png"))

    # run_down : 6 frames, deplacement bas — torse legrement abaisse
    for i in range(6):
        img = new_canvas(32, 32)
        draw_player_base(img, implant_bright=True, offset_y=1 if i % 2 == 0 else 0, leg_phase=i)
        save(img, os.path.join(out_dir, f"player_run_down_{i+1:02d}.png"))

    # death : 8 frames
    # Frames 1-3 : recul (offset_y monte), 4-5 : a genoux, 6-8 : dissolution
    death_offsets = [0, 1, 2, 3, 4, 4, 5, 6]
    death_implants = [True, True, False, False, False, False, False, False]
    for i in range(8):
        img = new_canvas(32, 32)
        if i < 6:
            draw_player_base(img, implant_bright=death_implants[i], offset_y=death_offsets[i])
        else:
            # Frames 6-8 : dissolution (corps qui s'estompe)
            draw_player_base(img, implant_bright=False, offset_y=death_offsets[i])
            # Superpose un calque semi-transparent noir pour la dissolution
            fade = Image.new("RGBA", (32, 32), (0, 0, 0, (i - 5) * 60))
            img = Image.alpha_composite(img, fade)
        save(img, os.path.join(out_dir, f"player_death_{i+1:02d}.png"))

    print(f"  Joueur : {4 + 6 + 6 + 8} frames generees dans {out_dir}")


# ─── ESSAIM DE ROUILLE ────────────────────────────────────────────────────────

def draw_rust_swarm_base(img, phase=0, move_phase=None):
    """
    Masse arrondie basse (18x12 px), corps rouille.
    phase : 0-2 pour idle (oscillation left/right de 1px)
    move_phase : 0-3 pour le deplacement (etirement)
    """
    cx, cy = 16, 18  # centre dans le canvas 32x32

    osc = [-1, 0, 1][phase % 3] if move_phase is None else 0
    stretch = [0, 1, 0, -1][move_phase % 4] * 2 if move_phase is not None else 0

    # Corps principal - masse rouilee
    body_w = 9 + abs(stretch)
    body_h = 6 - abs(stretch) // 2
    x0 = cx - body_w + osc
    x1 = cx + body_w + osc
    y0 = cy - body_h
    y1 = cy + body_h

    rect(img, x0, y0, x1, y1, C_RUST_DARK)

    # Arrondi approximatif : coins en transparent
    for corner_x, corner_y in [(x0, y0), (x1, y0), (x0, y1), (x1, y1)]:
        px(img, corner_x, corner_y, C_TRANSPARENT)

    # Detail rouille claire (variation)
    rect(img, x0+2, y0+1, x1-2, y0+3, C_RUST_LIGHT)

    # Detail vert bile (infection)
    rect(img, x0+3, cy-1, x0+6, cy+1, C_DEBRIS_ORG)

    # Highlight metal (bord superieur)
    for xi in range(x0+1, x1):
        px(img, xi, y0, (0x8A, 0x6A, 0x4A, 255))

    # Contour
    draw = ImageDraw.Draw(img)
    draw.rectangle([x0, y0, x1, y1], outline=C_BLACK)

    # Yeux de corruption (2 pixels rouges, clignotant selon phase)
    eye_bright = (phase % 3 != 2)  # clignote sur frame 3
    eye_col = C_SURCHARGE if eye_bright else (0x44, 0x00, 0x00, 255)
    px(img, cx - 3 + osc, cy - 1, eye_col)
    px(img, cx + 2 + osc, cy - 1, eye_col)

    # "Pattes" / filaments irreguliers
    for side in [-1, 1]:
        for j in range(3):
            px_x = cx + side * (body_w + j) + osc
            px_y = cy - 1 + j
            if 0 <= px_x < 32 and 0 <= px_y < 32:
                px(img, px_x, px_y, C_RUST_DARK)


def generate_rustswarm_sprites(out_dir):
    # idle : 3 frames
    for i in range(3):
        img = new_canvas(32, 32)
        draw_rust_swarm_base(img, phase=i)
        save(img, os.path.join(out_dir, f"enemy_rustswarm_idle_{i+1:02d}.png"))

    # move : 4 frames
    for i in range(4):
        img = new_canvas(32, 32)
        draw_rust_swarm_base(img, phase=0, move_phase=i)
        save(img, os.path.join(out_dir, f"enemy_rustswarm_move_{i+1:02d}.png"))

    # death : 5 frames
    for i in range(5):
        img = new_canvas(32, 32)
        if i == 0:
            # Gonflement
            draw_rust_swarm_base(img, phase=0)
            rect(img, 5, 14, 26, 22, C_RUST_LIGHT)  # corps gonfle
        elif i < 4:
            # Pixels qui se dispersent
            spread = i * 4
            cx, cy = 16, 18
            for angle_deg in range(0, 360, 45):
                angle_rad = math.radians(angle_deg)
                dx = int(math.cos(angle_rad) * spread)
                dy = int(math.sin(angle_rad) * spread)
                px(img, cx + dx, cy + dy, C_RUST_LIGHT)
                px(img, cx + dx + 1, cy + dy, C_RUST_DARK)
        # Frame 5 : vide (transparent)
        save(img, os.path.join(out_dir, f"enemy_rustswarm_death_{i+1:02d}.png"))

    print(f"  Essaim de Rouille : {3+4+5} frames generees dans {out_dir}")


# ─── DRONE CORROMPU ───────────────────────────────────────────────────────────

def draw_drone_base(img, phase=0, eye_bright=True):
    """
    Losange horizontal 18x12 px, 2 ailes laterales, oeil central.
    """
    cx, cy = 16, 16

    # Corps principal (losange approximatif en pixel art)
    body_pts = [
        (cx-9, cy), (cx-7, cy-4), (cx+7, cy-4), (cx+9, cy),
        (cx+7, cy+4), (cx-7, cy+4)
    ]
    draw = ImageDraw.Draw(img)
    draw.polygon(body_pts, fill=C_METAL_DARK)

    # Detail acier
    rect(img, cx-5, cy-2, cx+5, cy+2, C_METAL_GREY)

    # Plaques rouillees sur les bords
    rect(img, cx-8, cy-1, cx-6, cy+1, C_RUST_LIGHT)
    rect(img, cx+6, cy-1, cx+8, cy+1, C_RUST_LIGHT)

    # Ailes laterales (2 px de large, oscillent selon phase)
    wing_off = [0, 1, 0, -1][phase % 4]
    for wing_x in [cx - 11, cx + 9]:
        rect(img, wing_x, cy - 2 + wing_off, wing_x + 1, cy + 2 + wing_off, (0x3A, 0x3A, 0x42, 255))

    # Oeil central (4x4 px)
    eye_r = C_SURCHARGE if eye_bright else (0x33, 0x00, 0x00, 255)
    eye_h = C_SURCH_PALE if eye_bright else (0x22, 0x00, 0x00, 255)
    # rotation de position de l'oeil selon phase
    eye_dx = [0, 1, 0, -1][phase % 4]
    eye_dy = [0, 0, 0, 0][phase % 4]
    rect(img, cx-1+eye_dx, cy-1+eye_dy, cx+1+eye_dx, cy+1+eye_dy, eye_r)
    px(img, cx + eye_dx, cy + eye_dy, eye_h)  # centre plus vif

    # Contour
    draw.polygon(body_pts, outline=C_BLACK)


def generate_drone_sprites(out_dir):
    # idle : 3 frames
    for i in range(3):
        img = new_canvas(32, 32)
        draw_drone_base(img, phase=i, eye_bright=(i != 2))
        save(img, os.path.join(out_dir, f"enemy_drone_idle_{i+1:02d}.png"))

    # move : 4 frames (inclinaison dans direction deplacement)
    for i in range(4):
        img = new_canvas(32, 32)
        draw_drone_base(img, phase=i, eye_bright=True)
        # Inclinaison simulee par decalage du corps
        save(img, os.path.join(out_dir, f"enemy_drone_move_{i+1:02d}.png"))

    # death : 4 frames
    for i in range(4):
        img = new_canvas(32, 32)
        if i == 0:
            # Oeil qui s'eteint (rouge)
            draw_drone_base(img, phase=0, eye_bright=True)
        elif i == 1:
            # Oeil gris
            draw_drone_base(img, phase=0, eye_bright=False)
        elif i >= 2:
            # Explosion en debris
            cx, cy = 16, 16
            spread = (i - 1) * 5
            for angle_deg in range(0, 360, 45):
                angle_rad = math.radians(angle_deg + i * 22)
                dx = int(math.cos(angle_rad) * spread)
                dy = int(math.sin(angle_rad) * spread)
                rect(img, cx+dx-1, cy+dy-1, cx+dx+1, cy+dy+1, C_METAL_GREY)
        save(img, os.path.join(out_dir, f"enemy_drone_death_{i+1:02d}.png"))

    print(f"  Drone Corrompu : {3+4+4} frames generees dans {out_dir}")


# ─── SENTINELLE CORROMPUE ─────────────────────────────────────────────────────

def draw_sentinel_base(img, phase=0, attack_phase=None):
    """
    Corps central 14x14 px sur 4 pattes rigides. Canon a droite.
    """
    cx, cy = 16, 16

    # Pattes (2 px chacune, 5 px de long)
    leg_offsets = {
        'fl': (-5, -5), 'fr': (7, -5), 'bl': (-5, 7), 'br': (7, 7)
    }
    move_off = [0, 1, 0, -1][phase % 4]

    for name, (lx, ly) in leg_offsets.items():
        leg_y_off = move_off if ('f' in name) else -move_off
        rect(img, cx+lx, cy+ly+leg_y_off, cx+lx+1, cy+ly+4+leg_y_off, C_METAL_GREY)
        # Rouille sur les pattes
        px(img, cx+lx, cy+ly+4+leg_y_off, C_RUST_LIGHT)

    # Corps principal
    rect(img, cx-7, cy-7, cx+7, cy+7, C_MUR_MAIN)
    # Detail brique
    rect(img, cx-5, cy-5, cx+5, cy+5, C_MUR_DETAIL)
    # Highlight haut
    rect(img, cx-6, cy-7, cx+6, cy-6, C_METAL_GREY)
    # Contour
    draw = ImageDraw.Draw(img)
    draw.rectangle([cx-7, cy-7, cx+7, cy+7], outline=C_BLACK)

    # Capteurs (2 yeux 2x2 px, clignotants)
    eye_bright = (phase % 4 != 3)
    eye_col = C_SURCHARGE if eye_bright else (0x44, 0x00, 0x00, 255)
    rect(img, cx-4, cy-3, cx-3, cy-2, eye_col)
    rect(img, cx+2, cy-3, cx+3, cy-2, eye_col)

    # Canon (6x3 px a droite du corps)
    canon_charge = attack_phase is not None and attack_phase >= 2
    canon_col = C_SURCH_PALE if canon_charge else C_METAL_DARK
    tip_col = C_SURCHARGE if canon_charge else (0x3A, 0x3A, 0x42, 255)
    rect(img, cx+8, cy-1, cx+13, cy+1, canon_col)
    px(img, cx+13, cy, tip_col)


def generate_sentinel_sprites(out_dir):
    # idle : 4 frames
    for i in range(4):
        img = new_canvas(32, 32)
        draw_sentinel_base(img, phase=i)
        save(img, os.path.join(out_dir, f"enemy_sentinel_idle_{i+1:02d}.png"))

    # move : 6 frames
    for i in range(6):
        img = new_canvas(32, 32)
        draw_sentinel_base(img, phase=i)
        save(img, os.path.join(out_dir, f"enemy_sentinel_move_{i+1:02d}.png"))

    # attack : 4 frames
    for i in range(4):
        img = new_canvas(32, 32)
        draw_sentinel_base(img, phase=0, attack_phase=i)
        if i == 2:
            # Flash de tir
            draw = ImageDraw.Draw(img)
            draw.ellipse([26, 13, 31, 18], fill=C_SURCHARGE)
        save(img, os.path.join(out_dir, f"enemy_sentinel_attack_{i+1:02d}.png"))

    # death : 6 frames
    for i in range(6):
        img = new_canvas(32, 32)
        if i < 4:
            draw_sentinel_base(img, phase=0)
            # Pattes qui s'effondrent une par une
            legs_down = i
            # Recouvrir les pattes tombees
            for j in range(legs_down):
                lx = [-5, 7, -5, 7][j % 4]
                ly = [-5, -5, 7, 7][j % 4]
                rect(img, 16+lx-2, 16+ly+3, 16+lx+3, 16+ly+6, C_METAL_DARK)
        elif i == 4:
            # Corps qui chute (abaisse de 2px)
            tmp = new_canvas(32, 32)
            draw_sentinel_base(tmp, phase=0)
            img.paste(tmp, (0, 2), tmp)
        else:
            # Dissolution
            tmp = new_canvas(32, 32)
            draw_sentinel_base(tmp, phase=0)
            fade = Image.new("RGBA", (32, 32), (0, 0, 0, 120))
            img = Image.alpha_composite(tmp, fade)
        save(img, os.path.join(out_dir, f"enemy_sentinel_death_{i+1:02d}.png"))

    print(f"  Sentinelle Corrompue : {4+6+4+6} frames generees dans {out_dir}")


# ─── COLOSSE GREFFE (48x48 px) ────────────────────────────────────────────────

def draw_colossus_base(img, phase=0, attack_phase=None, death_phase=None):
    """
    Humanoide asymetrique 48x48 px.
    Bras droit organique, bras gauche (pince) enorme.
    """
    cx, cy = 24, 28  # centre bas du corps

    C_FLESH = (0x3A, 0x2A, 0x20, 255)  # chair necrosee
    C_IMPLANT_V = C_NOYAU_VIOL  # violet Noyau dans les fissures

    # Respiration : offset vertical
    breathe = [0, 0, -1, -1, 0, 0, 1, 1, 0, 0][phase % 10]

    # --- Jambes ---
    rect(img, cx-5, cy+2+breathe, cx-1, cy+10+breathe, C_METAL_GREY)
    rect(img, cx+1, cy+2+breathe, cx+5, cy+10+breathe, C_METAL_GREY)
    # Rouille sur les jambes
    px(img, cx-4, cy+8+breathe, C_RUST_LIGHT)
    px(img, cx+3, cy+8+breathe, C_RUST_LIGHT)

    # --- Corps massif (16x20 px) ---
    bx0, by0 = cx-8, cy-18+breathe
    bx1, by1 = cx+8, cy+2+breathe
    rect(img, bx0, by0, bx1, by1, C_FLESH)
    # Armure greffee
    rect(img, bx0+1, by0+3, bx1-1, by0+10, C_METAL_GREY)
    rect(img, bx0+1, by0+3, bx1-1, by0+4, C_BODY_HI)
    # Fissures avec violet Noyau
    for fis_y in [by0+7, by0+12]:
        for fis_x in range(bx0+2, bx1-1, 3):
            px(img, fis_x, fis_y, C_IMPLANT_V)
    # Contour corps
    draw = ImageDraw.Draw(img)
    draw.rectangle([bx0, by0, bx1, by1], outline=C_BLACK)

    # --- Tete petite (6x6 px) ---
    tx, ty = cx-3, by0-5+breathe
    rect(img, tx, ty, tx+5, ty+5, C_FLESH)
    # Yeux (4x2 px, plus grands)
    eye_bright = death_phase is None or death_phase < 5
    eye_col = C_SURCHARGE if eye_bright else (0x22, 0x00, 0x00, 255)
    rect(img, tx+1, ty+2, tx+4, ty+3, eye_col)
    draw.rectangle([tx, ty, tx+5, ty+5], outline=C_BLACK)

    # --- Bras droit organique (4 px de large) ---
    arm_y = by0 + 4 + breathe
    rect(img, bx1+1, arm_y, bx1+4, arm_y+8, C_FLESH)
    draw.rectangle([bx1+1, arm_y, bx1+4, arm_y+8], outline=C_BLACK)

    # --- Pince mecanique gauche (10x14 px) ---
    pince_open = (attack_phase is not None and attack_phase < 3)
    pince_y = by0 + 2 + breathe
    pince_x0 = bx0 - 12
    pince_x1 = bx0 - 2

    rect(img, pince_x0, pince_y, pince_x1, pince_y+14, C_METAL_DARK)
    rect(img, pince_x0+1, pince_y+1, pince_x1-1, pince_y+5, C_METAL_GREY)
    # Rouille pince
    px(img, pince_x0+2, pince_y+10, C_RUST_LIGHT)
    px(img, pince_x1-2, pince_y+12, C_RUST_LIGHT)

    if pince_open:
        # Pince ouverte : machoires separees
        draw.rectangle([pince_x0, pince_y, pince_x1, pince_y+5], outline=C_BLACK)
        draw.rectangle([pince_x0, pince_y+9, pince_x1, pince_y+14], outline=C_BLACK)
    else:
        draw.rectangle([pince_x0, pince_y, pince_x1, pince_y+14], outline=C_BLACK)


def generate_colossus_sprites(out_dir):
    # idle : 4 frames
    for i in range(4):
        img = new_canvas(48, 48)
        draw_colossus_base(img, phase=i)
        save(img, os.path.join(out_dir, f"enemy_colossus_idle_{i+1:02d}.png"))

    # move : 6 frames
    for i in range(6):
        img = new_canvas(48, 48)
        draw_colossus_base(img, phase=i * 2)
        save(img, os.path.join(out_dir, f"enemy_colossus_move_{i+1:02d}.png"))

    # attack : 5 frames
    for i in range(5):
        img = new_canvas(48, 48)
        if i < 3:
            # Leve-bras : pince monte
            draw_colossus_base(img, phase=0, attack_phase=i)
        elif i == 3:
            # Impact : flash blanc de contour
            draw_colossus_base(img, phase=0, attack_phase=i)
            draw = ImageDraw.Draw(img)
            draw.rectangle([0, 0, 47, 47], outline=C_WHITE)
        else:
            draw_colossus_base(img, phase=0)
        save(img, os.path.join(out_dir, f"enemy_colossus_attack_{i+1:02d}.png"))

    # death : 10 frames
    for i in range(10):
        img = new_canvas(48, 48)
        if i < 5:
            # Chute
            draw_colossus_base(img, phase=0, death_phase=i)
            fall_offset = i * 3
            tmp = new_canvas(48, 48)
            draw_colossus_base(tmp, phase=0, death_phase=i)
            img.paste(tmp, (0, fall_offset), tmp)
        elif i == 6:
            # Flash violet (release Noyau)
            flash = Image.new("RGBA", (48, 48), (0xAA, 0x44, 0xFF, 100))
            img = Image.alpha_composite(img, flash)
            # Pixel violet "sort" du corps
            px(img, 24, 30, C_NOYAU_VIOL)
        else:
            # Dissolution
            fade = Image.new("RGBA", (48, 48), (0, 0, 0, min(255, (i - 4) * 50)))
            img = Image.alpha_composite(img, fade)
        save(img, os.path.join(out_dir, f"enemy_colossus_death_{i+1:02d}.png"))

    print(f"  Colosse Greffe : {4+6+5+10} frames generees dans {out_dir}")


# ─── PROJECTILE CANON ─────────────────────────────────────────────────────────

def generate_bullet_impulse(out_dir):
    """Projectile Canon a Impulsions : 8x4 px, horizontal, degrade."""
    img = new_canvas(8, 4)
    # Pixel 1 (pointe gauche) : blanc
    px(img, 0, 1, C_WHITE)
    px(img, 0, 2, C_WHITE)
    # Pixels 2-4 (noyau) : jaune solaire
    for xi in range(1, 4):
        px(img, xi, 1, C_PLASMA_EDGE)
        px(img, xi, 2, C_PLASMA_EDGE)
    # Pixels 5-7 (tracer) : orange electrique
    for xi in range(4, 7):
        px(img, xi, 1, C_PLASMA_HOT)
        px(img, xi, 2, C_PLASMA_HOT)
    # Pixel 8 (queue) : orange pale semi-transparent
    px(img, 7, 1, (0xFF, 0x44, 0x00, 120))
    px(img, 7, 2, (0xFF, 0x44, 0x00, 120))
    save(img, os.path.join(out_dir, "weapon_bullet_impulse.png"))
    print(f"  Projectile Canon : 1 fichier genere dans {out_dir}")


def generate_bullet_rail(out_dir):
    """Projectile Rail Surchage : 12x4 px, cyan."""
    img = new_canvas(12, 4)
    # Pointe
    px(img, 0, 1, C_WHITE)
    px(img, 0, 2, C_WHITE)
    # Corps cyan
    for xi in range(1, 11):
        px(img, xi, 1, C_AETHER_PRI)
        px(img, xi, 2, C_AETHER_PRI)
    # Queue
    px(img, 11, 1, C_AETHER_DARK)
    px(img, 11, 2, C_AETHER_DARK)
    save(img, os.path.join(out_dir, "weapon_bullet_rail.png"))


def generate_sentinel_bullet(out_dir):
    """Projectile Sentinelle : 6x6 px, pulsation rouge."""
    for frame in range(2):
        img = new_canvas(6, 6)
        # Anneau exterieur
        circle_filled(img, 3, 3, 2, C_SURCH_PALE)
        # Anneau interieur
        circle_filled(img, 3, 3, 1, C_SURCHARGE)
        # Centre blanc (pulse entre 1x1 et 2x2)
        if frame == 0:
            rect(img, 2, 2, 3, 3, C_WHITE)
        else:
            px(img, 3, 3, C_WHITE)
        save(img, os.path.join(out_dir, f"enemy_bullet_sentinel_{frame+1:02d}.png"))
    print(f"  Projectile Sentinelle : 2 fichiers generes dans {out_dir}")


# ─── TILES ────────────────────────────────────────────────────────────────────

def generate_floor_tiles(out_dir):
    """3 tuiles de sol 32x32."""

    # tile_floor_01 : gris-ardoise profond, texture subtile
    img = new_canvas(32, 32)
    rect(img, 0, 0, 31, 31, C_SOL_DARK)
    # Variations subtiles (1-2 px)
    for y in range(0, 32, 8):
        for x in range(0, 32, 8):
            if (x + y) % 16 == 0:
                px(img, x+1, y+1, C_SOL_MID)
                px(img, x+2, y+1, C_SOL_MID)
    save(img, os.path.join(out_dir, "tile_floor_01.png"))

    # tile_floor_02 : gris-pierre, joint visible
    img = new_canvas(32, 32)
    rect(img, 0, 0, 31, 31, C_SOL_MID)
    # Joints
    for xi in range(0, 32, 8):
        for yi in range(32):
            px(img, xi, yi, C_SOL_DARK)
    for yi in range(0, 32, 8):
        for xi in range(32):
            px(img, xi, yi, C_SOL_DARK)
    save(img, os.path.join(out_dir, "tile_floor_02.png"))

    # tile_floor_crack : sol avec fissure Aether
    img = new_canvas(32, 32)
    rect(img, 0, 0, 31, 31, C_SOL_DARK)
    # Fissure diagonale
    for i in range(8, 22):
        px(img, i, i - 4, C_AETHER_FISS)
        px(img, i+1, i - 4, (0x00, 0x50, 0x66, 180))
    save(img, os.path.join(out_dir, "tile_floor_crack.png"))

    # tile_floor_rust : tache rouille
    img = new_canvas(32, 32)
    rect(img, 0, 0, 31, 31, C_SOL_DARK)
    rect(img, 10, 12, 20, 20, C_RUST_DARK)
    px(img, 11, 13, C_RUST_LIGHT)
    save(img, os.path.join(out_dir, "tile_floor_rust.png"))

    # tile_floor_debris : fragments metalliques
    img = new_canvas(32, 32)
    rect(img, 0, 0, 31, 31, C_SOL_DARK)
    for positions in [(4, 6), (12, 4), (20, 14), (8, 20), (24, 22)]:
        x, y = positions
        rect(img, x, y, x+2, y+2, C_METAL_GREY)
    save(img, os.path.join(out_dir, "tile_floor_debris.png"))

    print(f"  Tiles sol : 5 fichiers generes dans {out_dir}")


def generate_wall_tiles(out_dir):
    """Tuiles de murs 32x32."""

    # tile_wall_01 : beton oxide
    img = new_canvas(32, 32)
    rect(img, 0, 0, 31, 31, C_MUR_MAIN)
    # Texture de pierre (blocs de 8x8)
    for y in range(0, 32, 8):
        for x in range(0, 32, 8):
            draw = ImageDraw.Draw(img)
            draw.rectangle([x, y, x+7, y+7], outline=C_METAL_DARK)
    # Highlight haut
    rect(img, 0, 0, 31, 1, C_METAL_GREY)
    save(img, os.path.join(out_dir, "tile_wall_01.png"))

    # tile_wall_rust : mur + plaque rouilee
    img = new_canvas(32, 32)
    rect(img, 0, 0, 31, 31, C_MUR_MAIN)
    rect(img, 8, 8, 24, 22, C_RUST_DARK)
    rect(img, 9, 9, 23, 21, C_RUST_LIGHT)
    save(img, os.path.join(out_dir, "tile_wall_rust.png"))

    # tile_wall_crack_aether : fissure avec halo Aether
    img = new_canvas(32, 32)
    rect(img, 0, 0, 31, 31, C_MUR_MAIN)
    for i in range(6, 26):
        px(img, 16, i, C_AETHER_FISS)
        px(img, 15, i, (0x00, 0x50, 0x66, 160))
        px(img, 17, i, (0x00, 0x50, 0x66, 120))
    save(img, os.path.join(out_dir, "tile_wall_crack_aether.png"))

    # tile_debris_01 : bloc de pierre effondre
    img = new_canvas(32, 32)
    rect(img, 4, 12, 26, 28, C_MUR_DETAIL)
    rect(img, 4, 12, 26, 13, C_METAL_GREY)
    save(img, os.path.join(out_dir, "tile_debris_01.png"))

    # tile_debris_metal : structure metallique tombee
    img = new_canvas(32, 32)
    rect(img, 2, 18, 30, 26, C_METAL_DARK)
    rect(img, 2, 18, 30, 19, C_METAL_GREY)
    px(img, 4, 22, C_RUST_LIGHT)
    save(img, os.path.join(out_dir, "tile_debris_metal.png"))

    # tile_column : colonne brisee 32x64 px (2 tiles)
    img = new_canvas(32, 64)
    rect(img, 10, 0, 22, 63, C_MUR_MAIN)
    # Detail
    for yi in range(0, 64, 8):
        draw = ImageDraw.Draw(img)
        draw.rectangle([10, yi, 22, yi+7], outline=C_METAL_DARK)
    # Fracture au milieu
    rect(img, 10, 30, 22, 33, C_AETHER_FISS)
    save(img, os.path.join(out_dir, "tile_column.png"))

    print(f"  Tiles murs : 6 fichiers generes dans {out_dir}")


def generate_decor_tiles(out_dir):
    """Tiles de decor : geyser, pilier, flaque."""

    # tile_aether_geyser_01-03 : jet Aether montant
    for i in range(3):
        img = new_canvas(32, 32)
        height = 8 + i * 6
        alpha = 120 + i * 40
        for y in range(16 - height, 16):
            w = max(1, (16 - y) // 3)
            for x in range(16 - w, 16 + w):
                base_alpha = int(alpha * (16 - y) / height)
                px(img, x, y, (0x00, 0xA0, 0xBB, base_alpha))
        # Base du geyser
        circle_filled(img, 16, 16, 4, (0x00, 0x7A, 0x99, 180))
        save(img, os.path.join(out_dir, f"tile_aether_geyser_{i+1:02d}.png"))

    # tile_tech_pillar : pilier de technologie morte 32x64
    img = new_canvas(32, 64)
    rect(img, 8, 0, 24, 63, C_METAL_DARK)
    # Detail electronique eteint
    rect(img, 10, 4, 22, 10, C_SOL_DARK)  # ecran eteint
    rect(img, 10, 20, 22, 24, C_METAL_GREY)
    draw = ImageDraw.Draw(img)
    draw.rectangle([8, 0, 24, 63], outline=C_BLACK)
    save(img, os.path.join(out_dir, "tile_tech_pillar.png"))

    # tile_rust_pool_01-02 : flaque de Rouille Vivante
    for i in range(2):
        img = new_canvas(32, 32)
        rect(img, 4, 4, 28, 28, C_DEBRIS_ORG)
        # Variation et bulles
        rect(img, 6, 6, 26, 26, (0x42, 0x42, 0x30, 255))
        if i == 0:
            circle_filled(img, 12, 14, 2, (0x50, 0x50, 0x38, 200))
        else:
            circle_filled(img, 18, 16, 2, C_DEBRIS_ORG)
            circle_filled(img, 10, 12, 1, (0x50, 0x50, 0x38, 200))
        save(img, os.path.join(out_dir, f"tile_rust_pool_{i+1:02d}.png"))

    print(f"  Tiles decor : {3+1+2} fichiers generes dans {out_dir}")


# ─── VFX PARTICLES ────────────────────────────────────────────────────────────

def generate_vfx_particles(out_dir):
    """Textures de particules 2x2 ou 3x3 px."""

    particles = [
        ("vfx_particle_rustswarm.png",  2, C_RUST_LIGHT),
        ("vfx_particle_drone.png",       2, C_METAL_GREY),
        ("vfx_particle_sentinel.png",    2, C_MUR_DETAIL),
        ("vfx_particle_xp.png",          2, C_XP_GREEN),
        ("vfx_particle_noyau.png",       3, C_NOYAU_VIOL),
        ("vfx_aura_fusionblade.png",     3, C_AETHER_HOT),
        ("vfx_aura_rail.png",            3, C_AETHER_PRI),
    ]

    for fname, size, color in particles:
        img = new_canvas(size, size)
        rect(img, 0, 0, size-1, size-1, color)
        save(img, os.path.join(out_dir, fname))

    # vfx_particle_colossus : moitie metal + moitie violet
    img = new_canvas(2, 2)
    px(img, 0, 0, C_METAL_GREY)
    px(img, 1, 0, C_NOYAU_VIOL)
    px(img, 0, 1, C_NOYAU_VIOL)
    px(img, 1, 1, C_METAL_GREY)
    save(img, os.path.join(out_dir, "vfx_particle_colossus.png"))

    print(f"  VFX particules : {len(particles)+1} fichiers generes dans {out_dir}")


# ─── WEAPON VFX ───────────────────────────────────────────────────────────────

def generate_weapon_vfx(out_dir):
    """VFX armes : swing plasma, drone orbital, metamorphoses."""

    # weapon_plasmablade_swing_01-04 : arc semi-circulaire cyan
    for i in range(4):
        img = new_canvas(32, 32)
        draw = ImageDraw.Draw(img)
        arc_start = 180 + i * 30
        arc_end   = 360 - i * 30
        r = 12 - i
        draw.arc([16-r, 16-r, 16+r, 16+r], arc_start, arc_end, fill=C_AETHER_PRI, width=2)
        # Bord exterieur blanc
        draw.arc([16-r-1, 16-r-1, 16+r+1, 16+r+1], arc_start, arc_end, fill=C_WHITE, width=1)
        save(img, os.path.join(out_dir, f"weapon_plasmablade_swing_{i+1:02d}.png"))

    # weapon_drone_idle_01-03 : mini drone 8x8 px
    for i in range(3):
        img = new_canvas(8, 8)
        rect(img, 1, 2, 6, 5, C_BODY_MAIN)
        # Oeil
        eye_col = C_AETHER_PRI if (i != 2) else C_AETHER_DARK
        px(img, 3, 3, eye_col)
        px(img, 4, 3, eye_col)
        # Ailes
        for wx in [0, 7]:
            px(img, wx, 3 + (1 if i % 2 == 0 else -1), C_BODY_HI)
        save(img, os.path.join(out_dir, f"weapon_drone_idle_{i+1:02d}.png"))

    # weapon_fusionblade_ring_texture.png : bande 64x4 cyan/magenta
    img = new_canvas(64, 4)
    for xi in range(64):
        angle = (xi / 64.0) * 360
        if int(angle / 30) % 2 == 0:
            col = C_AETHER_PRI
        else:
            col = C_AETHER_HOT
        for yi in range(4):
            px(img, xi, yi, col)
    save(img, os.path.join(out_dir, "weapon_fusionblade_ring_texture.png"))

    # weapon_fusionblade_metamorphose_01-08
    for i in range(8):
        img = new_canvas(32, 32)
        draw = ImageDraw.Draw(img)
        progress = i / 7.0
        # Arc qui se referme → anneau
        r = 12
        arc_span = 180 - int(180 * progress)  # de 180° ouvert a 0° (anneau complet)
        if arc_span > 0:
            draw.arc([4, 4, 28, 28], 90, 90 + arc_span, fill=C_AETHER_PRI, width=2)
            draw.arc([4, 4, 28, 28], 90 - arc_span, 90, fill=C_AETHER_PRI, width=2)
        else:
            draw.ellipse([4, 4, 28, 28], outline=C_AETHER_PRI, width=2)
        # Magenta qui gagne
        if i >= 4:
            mag_alpha = int(255 * (i - 3) / 5)
            mag_layer = Image.new("RGBA", (32, 32), C_TRANSPARENT)
            mag_draw  = ImageDraw.Draw(mag_layer)
            mag_draw.ellipse([6, 6, 26, 26], outline=(0xFF, 0x00, 0xCC, mag_alpha), width=2)
            img = Image.alpha_composite(img, mag_layer)
        save(img, os.path.join(out_dir, f"weapon_fusionblade_metamorphose_{i+1:02d}.png"))

    # weapon_rail_metamorphose_01-10
    for i in range(10):
        img = new_canvas(32, 32)
        draw = ImageDraw.Draw(img)
        progress = i / 9.0
        if i < 3:
            # Canon qui s'allonge
            length = 8 + i * 4
            draw.rectangle([8, 14, 8+length, 17], fill=C_METAL_DARK, outline=C_BLACK)
        elif i < 6:
            # Flash cyan
            alpha = int(255 * (1.0 - (i - 3) / 3.0))
            flash = Image.new("RGBA", (32, 32), (0x00, 0xE5, 0xFF, alpha))
            img = Image.alpha_composite(img, flash)
        else:
            # Nouveau canon rail cyan
            draw.rectangle([4, 13, 26, 18], fill=C_AETHER_PRI, outline=C_BLACK)
            # Sillage
            for si in range(3):
                draw.line([3-si, 15+si, 3-si, 16-si], fill=(0x00, 0xA0, 0xBB, 180-si*50))
        save(img, os.path.join(out_dir, f"weapon_rail_metamorphose_{i+1:02d}.png"))

    print(f"  VFX armes : {4+3+1+8+10} fichiers generes dans {out_dir}")


# ─── UI ICONS ─────────────────────────────────────────────────────────────────

def draw_icon_background(img):
    """Fond sombre pour icone 32x32."""
    rect(img, 0, 0, 31, 31, (0x25, 0x20, 0x28, 255))
    ImageDraw.Draw(img).rectangle([0, 0, 31, 31], outline=(0x4A, 0x4A, 0x52, 255))


def generate_ui_icons(out_dir):
    """Icones armes et passifs pour les cartes de level-up."""

    # ui_icon_impulse_cannon : canon jaune-orange
    img = new_canvas(32, 32)
    draw_icon_background(img)
    rect(img, 6, 13, 24, 18, C_METAL_DARK)
    rect(img, 24, 11, 28, 20, C_METAL_GREY)
    circle_filled(img, 4, 15, 3, C_PLASMA_EDGE)
    circle_filled(img, 4, 15, 1, C_PLASMA_HOT)
    save(img, os.path.join(out_dir, "ui_icon_impulse_cannon.png"))

    # ui_icon_plasmablade : arc cyan
    img = new_canvas(32, 32)
    draw_icon_background(img)
    ImageDraw.Draw(img).arc([4, 4, 28, 28], 180, 360, fill=C_AETHER_PRI, width=3)
    save(img, os.path.join(out_dir, "ui_icon_plasmablade.png"))

    # ui_icon_droneswarm : 3 petits drones en orbite
    img = new_canvas(32, 32)
    draw_icon_background(img)
    for angle_deg in [0, 120, 240]:
        angle_rad = math.radians(angle_deg)
        dx = int(math.cos(angle_rad) * 9)
        dy = int(math.sin(angle_rad) * 9)
        rect(img, 15+dx-2, 15+dy-2, 15+dx+2, 15+dy+2, C_BODY_MAIN)
        px(img, 15+dx, 15+dy, C_AETHER_PRI)
    save(img, os.path.join(out_dir, "ui_icon_droneswarm.png"))

    # ui_icon_overloadfield : cercle d'onde cyan
    img = new_canvas(32, 32)
    draw_icon_background(img)
    draw = ImageDraw.Draw(img)
    draw.ellipse([4, 4, 28, 28], outline=C_AETHER_PRI, width=2)
    draw.ellipse([8, 8, 24, 24], outline=(0x00, 0xA0, 0xBB, 160), width=1)
    draw.ellipse([12, 12, 20, 20], outline=(0x00, 0x60, 0x77, 120), width=1)
    save(img, os.path.join(out_dir, "ui_icon_overloadfield.png"))

    # ui_icon_thermal_core : noyau orange chaud
    img = new_canvas(32, 32)
    draw_icon_background(img)
    circle_filled(img, 16, 16, 8, C_PLASMA_HOT)
    circle_filled(img, 16, 16, 5, C_PLASMA_EDGE)
    circle_filled(img, 16, 16, 2, C_WHITE)
    save(img, os.path.join(out_dir, "ui_icon_thermal_core.png"))

    # ui_icon_reinforced_plate : plaque blindee grise
    img = new_canvas(32, 32)
    draw_icon_background(img)
    rect(img, 6, 6, 26, 26, C_METAL_GREY)
    rect(img, 6, 6, 26, 8, C_BODY_HI)
    rect(img, 6, 24, 26, 26, C_BODY_SHADOW)
    draw = ImageDraw.Draw(img)
    draw.rectangle([6, 6, 26, 26], outline=C_BLACK)
    save(img, os.path.join(out_dir, "ui_icon_reinforced_plate.png"))

    # ui_icon_servomotors : roue dentee / servo
    img = new_canvas(32, 32)
    draw_icon_background(img)
    draw = ImageDraw.Draw(img)
    circle_filled(img, 16, 16, 8, C_METAL_GREY)
    circle_filled(img, 16, 16, 4, C_METAL_DARK)
    # Dents
    for angle_deg in range(0, 360, 45):
        angle_rad = math.radians(angle_deg)
        tx = int(math.cos(angle_rad) * 10) + 16
        ty = int(math.sin(angle_rad) * 10) + 16
        rect(img, tx-1, ty-1, tx+1, ty+1, C_BODY_HI)
    save(img, os.path.join(out_dir, "ui_icon_servomotors.png"))

    # ui_icon_capacitor : condensateur cyan
    img = new_canvas(32, 32)
    draw_icon_background(img)
    rect(img, 10, 6, 22, 26, C_METAL_DARK)
    rect(img, 10, 6, 22, 8, C_AETHER_PRI)
    rect(img, 10, 24, 22, 26, C_AETHER_PRI)
    draw = ImageDraw.Draw(img)
    draw.rectangle([10, 6, 22, 26], outline=C_BLACK)
    save(img, os.path.join(out_dir, "ui_icon_capacitor.png"))

    # ui_icon_fusionblade : anneau bicolore cyan/magenta
    img = new_canvas(32, 32)
    draw_icon_background(img)
    draw = ImageDraw.Draw(img)
    draw.arc([4, 4, 28, 28], 0, 180, fill=C_AETHER_PRI, width=3)
    draw.arc([4, 4, 28, 28], 180, 360, fill=C_AETHER_HOT, width=3)
    save(img, os.path.join(out_dir, "ui_icon_fusionblade.png"))

    # ui_icon_rail : canon long cyan
    img = new_canvas(32, 32)
    draw_icon_background(img)
    rect(img, 4, 13, 28, 18, C_AETHER_PRI)
    rect(img, 4, 13, 6, 18, C_WHITE)
    draw = ImageDraw.Draw(img)
    draw.rectangle([4, 13, 28, 18], outline=C_BLACK)
    save(img, os.path.join(out_dir, "ui_icon_rail.png"))

    # ui_icon_hp : coeur rouge 8x8
    img = new_canvas(8, 8)
    heart_pixels = [
        (1,1),(2,1),(5,1),(6,1),
        (0,2),(1,2),(2,2),(3,2),(4,2),(5,2),(6,2),(7,2),
        (0,3),(1,3),(2,3),(3,3),(4,3),(5,3),(6,3),(7,3),
        (1,4),(2,4),(3,4),(4,4),(5,4),(6,4),
        (2,5),(3,5),(4,5),(5,5),
        (3,6),(4,6),
        (3,7),(4,7),
    ]
    for (hx, hy) in heart_pixels:
        px(img, hx, hy, C_BARRE_CRIT)
    save(img, os.path.join(out_dir, "ui_icon_hp.png"))

    # ui_icon_noyau : losange violet 8x8
    img = new_canvas(8, 8)
    for (nx, ny) in [
        (3,0),(4,0),(2,1),(5,1),(1,2),(6,2),(0,3),(7,3),
        (0,4),(7,4),(1,5),(6,5),(2,6),(5,6),(3,7),(4,7)
    ]:
        px(img, nx, ny, C_NOYAU_VIOL)
    for (nx, ny) in [
        (3,1),(4,1),(2,2),(5,2),(3,2),(4,2),(1,3),(6,3),(2,3),(5,3),(3,3),(4,3),
        (1,4),(6,4),(2,4),(5,4),(3,4),(4,4),(2,5),(5,5),(3,6),(4,6)
    ]:
        px(img, nx, ny, C_NOYAU_GLOW)
    save(img, os.path.join(out_dir, "ui_icon_noyau.png"))

    print(f"  Icones UI : {12} fichiers generes dans {out_dir}")


# ─── ORB XP & NOYAU AETHER ────────────────────────────────────────────────────

def generate_pickups(out_dir):
    """Orbe XP et Noyau Aether animes."""

    # Orbe XP idle 3 frames (jaune-vert, 8x8 px dans 16x16 canvas)
    for i in range(3):
        img = new_canvas(16, 16)
        pulse = [0, 1, 0][i]
        circle_filled(img, 8, 8, 4 + pulse, C_XP_GREEN)
        circle_filled(img, 8, 8, 2, C_XP_GLOW)
        save(img, os.path.join(out_dir, f"pickup_xporb_idle_{i+1:02d}.png"))

    # Noyau Aether idle 3 frames (violet, losange dans 16x16 canvas)
    for i in range(3):
        img = new_canvas(16, 16)
        pulse = [0, 1, 0][i]
        size = 4 + pulse
        cx, cy = 8, 8
        for dy in range(-size, size+1):
            for dx in range(-size, size+1):
                if abs(dx) + abs(dy) <= size:
                    px(img, cx+dx, cy+dy, C_NOYAU_VIOL)
        # Centre brillant
        px(img, cx, cy, C_NOYAU_GLOW)
        save(img, os.path.join(out_dir, f"pickup_noyau_idle_{i+1:02d}.png"))

    print(f"  Pickups XP + Noyau : 6 fichiers generes dans {out_dir}")


# ─── MAIN ─────────────────────────────────────────────────────────────────────

def main():
    base = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                        "assets", "sprites")

    if len(sys.argv) > 1 and sys.argv[1].startswith("--out"):
        if "=" in sys.argv[1]:
            base = sys.argv[1].split("=", 1)[1]
        elif len(sys.argv) > 2:
            base = sys.argv[2]

    print(f"\nGenerateur de sprites Chimera Protocol")
    print(f"Dossier de sortie : {base}\n")

    # === ITERATION 1 — Jouabilite visible ===
    print("=== ITERATION 1 ===")

    print("Joueur Cyborg...")
    generate_player_sprites(os.path.join(base, "player"))

    print("Essaim de Rouille...")
    generate_rustswarm_sprites(os.path.join(base, "enemies", "rustswarm"))

    print("Projectile Canon...")
    generate_bullet_impulse(os.path.join(base, "weapons"))

    print("Tiles sol et murs...")
    generate_floor_tiles(os.path.join(base, "tileset"))
    generate_wall_tiles(os.path.join(base, "tileset"))

    # === ITERATION 2 — Contenu complet ===
    print("\n=== ITERATION 2 ===")

    print("Drone Corrompu...")
    generate_drone_sprites(os.path.join(base, "enemies", "drone"))

    print("Sentinelle Corrompue...")
    generate_sentinel_sprites(os.path.join(base, "enemies", "sentinel"))

    print("Colosse Greffe...")
    generate_colossus_sprites(os.path.join(base, "enemies", "colossus"))

    print("Pickups (XP orbe + Noyau Aether)...")
    generate_pickups(os.path.join(base, "pickups") if False else os.path.join(base, "pickups"))

    print("Projectiles supplementaires...")
    generate_bullet_rail(os.path.join(base, "weapons"))
    generate_sentinel_bullet(os.path.join(base, "weapons"))

    # === ITERATION 3 — Polish ===
    print("\n=== ITERATION 3 ===")

    print("VFX particules...")
    generate_vfx_particles(os.path.join(base, "vfx"))

    print("VFX armes (swing, drone, metamorphoses)...")
    generate_weapon_vfx(os.path.join(base, "weapons"))

    print("Tiles decor...")
    generate_decor_tiles(os.path.join(base, "tileset"))

    print("Icones UI...")
    generate_ui_icons(os.path.join(base, "ui"))

    print("\nGeneration terminee.")

    # Decompte des fichiers
    total = 0
    for root, dirs, files in os.walk(base):
        total += len([f for f in files if f.endswith(".png")])
    print(f"Total PNG generes : {total} fichiers")


if __name__ == "__main__":
    main()
