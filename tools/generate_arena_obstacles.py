"""
generate_arena_obstacles.py — Nouveaux sprites Phase 4 pour Chimera Protocol.
Produit les obstacles d'arene et VFX particules conformes au brief ARENA_DA_BRIEF.md
et au STYLE_GUIDE.md.

Ne touche pas aux sprites existants (generate_sprites.py reste intact).

Palette de reference : STYLE_GUIDE.md §1
Direction artistique : ARENA_DA_BRIEF.md §2 (obstacles) et §4 (particules)

Usage :
  C:\\Users\\drang\\AppData\\Local\\Programs\\Python\\Python313\\python.exe tools/generate_arena_obstacles.py
"""

import os
import sys
import math
from PIL import Image, ImageDraw

# Racine du projet (ce script est dans tools/, les assets dans assets/)
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPT_DIR)
import pseudo3d_lib as _p3d
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)
OUT_ENV = os.path.join(PROJECT_ROOT, "assets", "sprites", "environment")
OUT_VFX = os.path.join(PROJECT_ROOT, "assets", "sprites", "vfx")

# ─── Palette (STYLE_GUIDE.md §1) ─────────────────────────────────────────────

# Matiere morte
C_SOL_DARK    = (0x1A, 0x1A, 0x22, 255)   # #1A1A22
C_MUR_MAIN    = (0x25, 0x20, 0x28, 255)   # #252028  pierre pilier
C_MUR_DETAIL  = (0x3A, 0x2E, 0x35, 255)   # #3A2E35  joints de pierre
C_RUST_LIGHT  = (0x7A, 0x4A, 0x2A, 255)   # #7A4A2A  rouille claire
C_RUST_DARK   = (0x4A, 0x2A, 0x15, 255)   # #4A2A15  rouille sombre
C_METAL_GREY  = (0x4A, 0x4A, 0x52, 255)   # #4A4A52  metal acier mort
C_METAL_DARK  = (0x2A, 0x2A, 0x32, 255)   # #2A2A32  fonte froide
C_METAL_MID   = (0x3A, 0x3A, 0x42, 255)   # #3A3A42  metal intermediaire
C_DEBRIS_ORG  = (0x3A, 0x3A, 0x28, 255)   # #3A3A28  corps organique Rouille Vivante
C_VOID        = (0x0A, 0x0A, 0x0F, 255)   # #0A0A0F  noir profond
C_PILLAR_SHAD = (0x0A, 0x0A, 0x0F, 128)   # #0A0A0F a 50% alpha — ombre portee
C_MOSS_RUST   = (0x3A, 0x3A, 0x28, 255)   # depot mousse/rouille

# Matiere morte — variante sombre pour bords pilier
C_PILLAR_EDGE = (0x1E, 0x1C, 0x23, 255)   # #1E1C23  ombre bords pilier

# Energie vivante
C_AETHER_FISS = (0x00, 0xA0, 0xBB, 255)   # #00A0BB  fissure Aether (bloom trigger §7.3)
C_AETHER_HOT  = (0x44, 0xFF, 0xEE, 255)   # #44FFEE  points chauds dans la fissure
C_AETHER_AMBNT= (0x00, 0xA0, 0xBB, 255)   # #00A0BB  particule ambiante
C_XP_GREEN    = (0xAA, 0xFF, 0x44, 255)   # #AAFF44  energie XP
C_PLASMA_EDGE = (0xFF, 0xD7, 0x00, 255)   # #FFD700  impact plasma (jaune solaire)
C_SURCHARGE_P = (0xFF, 0x66, 0x44, 255)   # #FF6644  impact sentinelle (rouge vif)

# Particules par type d'ennemi
C_RUSTSWARM_C = (0xCC, 0x33, 0x11, 255)   # #CC3311  rouge-rouille centre
C_RUSTSWARM_E = (0xFF, 0x44, 0x22, 255)   # #FF4422  rouge-rouille bords
C_DRONE_C     = (0x44, 0xFF, 0xEE, 255)   # #44FFEE  cyan Aether centre
C_DRONE_E     = (0x22, 0xAA, 0xAA, 255)   # #22AAAA  cyan bords
C_SENTINEL_C  = (0xAA, 0x44, 0x88, 255)   # #AA4488  magenta moyen centre
C_COLOSSUS_C  = (0xAA, 0x44, 0xFF, 255)   # #AA44FF  violet Aether centre
C_COLOSSUS_E  = (0x66, 0x22, 0xAA, 255)   # #6622AA  violet bords

# Caisse technologique
C_CRATE_MAIN  = (0x4A, 0x55, 0x66, 255)   # #4A5566  metal peint (meme famille armure)
C_CRATE_BAND  = (0x4A, 0x2A, 0x15, 255)   # #4A2A15  bande signalisation rouille
C_CRATE_LOGO  = (0x3A, 0x3A, 0x42, 255)   # #3A3A42  logo efface
C_CRATE_RUST  = (0x7A, 0x4A, 0x2A, 255)   # #7A4A2A  rouille coins

# Arche et terminal
C_ARCH_STONE  = (0x25, 0x20, 0x28, 255)   # idem pilier
C_ARCH_MOSS   = (0x3A, 0x3A, 0x28, 255)   # mousse joints
C_TERM_BODY   = (0x4A, 0x4A, 0x52, 255)   # #4A4A52
C_TERM_SCREEN = (0x1A, 0x1A, 0x22, 255)   # #1A1A22  ecran mort
C_TERM_DEAD   = (0x0A, 0x0A, 0x0F, 255)   # #0A0A0F  pixel mort
C_TERM_AETHER = (0x00, 0xA0, 0xBB, 255)   # pixel parasite Aether (frame 2)
C_TERM_FILAM  = (0x3A, 0x3A, 0x28, 255)   # filaments rouille

C_TRANSPARENT = (0x00, 0x00, 0x00, 0)


# ─── Utilitaires ──────────────────────────────────────────────────────────────

def new_canvas(w, h):
    return Image.new("RGBA", (w, h), C_TRANSPARENT)


def px(img, x, y, color):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), color)


def rect(img, x0, y0, x1, y1, color):
    draw = ImageDraw.Draw(img)
    draw.rectangle([x0, y0, x1, y1], fill=color)


def ellipse_filled(img, cx, cy, rx, ry, color):
    """Ellipse pleine en coordonnees centre + demi-axes."""
    for dy in range(-ry, ry + 1):
        for dx in range(-rx, rx + 1):
            # Equation ellipse : (dx/rx)^2 + (dy/ry)^2 <= 1
            if rx > 0 and ry > 0:
                if (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry) <= 1.0:
                    px(img, cx + dx, cy + dy, color)


def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "PNG")
    w, h = img.size
    print(f"  [OK] {os.path.relpath(path, PROJECT_ROOT)}  ({w}x{h} px)")


# Pseudo-3D (docs/ART_BRIEF_PSEUDO3D.md) : ombrage matiere applique aux obstacles
# (categorie "environment"), noyau energetique / accents Aether jamais assombris.
_CORE_COLORS = [
    C_AETHER_FISS[:3], C_AETHER_HOT[:3], C_AETHER_AMBNT[:3], C_XP_GREEN[:3],
    C_PLASMA_EDGE[:3], C_SURCHARGE_P[:3], C_RUSTSWARM_C[:3], C_RUSTSWARM_E[:3],
    C_DRONE_C[:3], C_DRONE_E[:3], C_SENTINEL_C[:3], C_COLOSSUS_C[:3], C_COLOSSUS_E[:3],
    C_TERM_AETHER[:3],
]
save = _p3d.wrap_save(save, core_colors=_CORE_COLORS)


# ─── P0 — Pilier de Sanctuaire : tile_pillar_stone.png (32x64) ───────────────

def gen_pillar_stone(out_dir):
    """
    32x64 px — colonne architecturale pre-Convergence.
    - Y 0-11  : toit elliptique, vue de dessus legere
    - Y 12-55 : corps rectangle arrondi, fissure Aether verticale
    - Y 56-63 : ombre portee elliptique
    """
    img = new_canvas(32, 64)

    # --- Corps du pilier (Y=4 a Y=55) ---
    # Rectangle base : 16 px de large centre (ox=8)
    body_x0, body_x1 = 8, 23
    body_y0, body_y1 = 12, 55

    # Remplissage base
    rect(img, body_x0, body_y0, body_x1, body_y1, C_MUR_MAIN)

    # Bords lateraux plus sombres (ombre)
    for y in range(body_y0, body_y1 + 1):
        px(img, body_x0, y, C_PILLAR_EDGE)
        px(img, body_x0 + 1, y, C_PILLAR_EDGE)
        px(img, body_x1, y, C_PILLAR_EDGE)
        px(img, body_x1 - 1, y, C_PILLAR_EDGE)

    # Bords superieur/inferieur du corps
    for x in range(body_x0, body_x1 + 1):
        px(img, x, body_y0, C_MUR_DETAIL)
        px(img, x, body_y1, C_MUR_DETAIL)

    # --- Toit de colonne (Y=2 a Y=13) — ellipse vue du dessus ---
    # Ellipse 20x12 px centree en haut du sprite (cx=15, cy=8)
    # Toit : ellipse principale + contour detail
    ellipse_filled(img, 15, 7, 10, 6, C_MUR_DETAIL)   # contour
    ellipse_filled(img, 15, 7,  9, 5, C_MUR_MAIN)     # remplissage

    # Ligne centrale toit (detail architectural)
    for x in range(9, 23):
        px(img, x, 7, C_MUR_DETAIL)

    # --- Fissure Aether verticale (x=14, Y=20 a Y=55) ---
    for y in range(20, 56):
        px(img, 14, y, C_AETHER_FISS)

    # Points de lumiere chauds dans la fissure (2-3 pixels eparpilles)
    px(img, 14, 24, C_AETHER_HOT)
    px(img, 14, 35, C_AETHER_HOT)
    px(img, 14, 48, C_AETHER_HOT)

    # --- Depots de mousse/rouille sur les bords inferieurs ---
    # Quelques pixels #3A3A28 sur Y=48-55, bords du corps
    moss_positions = [
        (8, 50), (8, 52), (9, 55),
        (22, 49), (23, 51), (22, 54),
        (10, 54), (21, 53),
    ]
    for mx, my in moss_positions:
        px(img, mx, my, C_MOSS_RUST)

    # Ombre portee : desormais geree automatiquement par pseudo3d_lib.add_cast_shadow()
    # (ellipse 2.2:1, alpha=100) via le save() enveloppe plus bas — cf.
    # docs/ART_BRIEF_PSEUDO3D.md §3/§5. L'ancienne ellipse ad hoc (alpha=128,
    # ratio ~3.5:1) est retiree pour eviter un double-ombrage.

    save(img, os.path.join(out_dir, "tile_pillar_stone.png"))


# ─── P0 — Ombre pilier : tile_pillar_stone_shadow.png (32x8) ─────────────────

def gen_pillar_stone_shadow(out_dir):
    """
    32x8 px — ombre seule du pilier, utilisee en ZIndex=-1.
    Ellipse 28x6 centree (#0A0A0F a alpha 50%).
    """
    img = new_canvas(32, 8)

    # Ellipse 28x6 => demi-axes rx=13, ry=2, centre (15, 3)
    for dy in range(-3, 4):
        for dx in range(-14, 15):
            if dx * dx / (14 * 14) + dy * dy / (9) <= 1.0:
                nx, ny = 15 + dx, 3 + dy
                if 0 <= nx < 32 and 0 <= ny < 8:
                    px(img, nx, ny, C_PILLAR_SHAD)

    save(img, os.path.join(out_dir, "tile_pillar_stone_shadow.png"))


# ─── P0 — Epave de machine : tile_wreck_machine.png (64x32) ──────────────────

def gen_wreck_machine(out_dir):
    """
    64x32 px — carcasse de machine aplatie, vue du dessus.
    Volumes : carter moteur (gauche) + bras mecanique (milieu) + panneau (droite).
    Rouille Vivante sur ~20% de la surface.
    """
    img = new_canvas(64, 32)

    # --- Carter moteur : 24x20 px, centre-gauche (x=2..25, y=6..25) ---
    rect(img, 2, 6, 25, 25, C_METAL_GREY)
    # Variation interne
    rect(img, 4, 8, 23, 23, (0x42, 0x42, 0x4A, 255))
    # Bords sombres du carter
    for x in range(2, 26):
        px(img, x, 6, C_METAL_DARK)
        px(img, x, 25, C_RUST_LIGHT)
    for y in range(6, 26):
        px(img, 2, y, C_METAL_DARK)
        px(img, 25, y, C_METAL_DARK)

    # Trou central dans le carter (zone creuse tres sombre)
    rect(img, 8, 12, 18, 20, C_VOID)
    # Vis/boulons symboliques
    px(img, 5, 9, C_METAL_DARK)
    px(img, 22, 9, C_METAL_DARK)
    px(img, 5, 22, C_METAL_DARK)
    px(img, 22, 22, C_METAL_DARK)

    # --- Bras mecanique : 22x8 px (x=22..43, y=12..19) ---
    rect(img, 22, 12, 43, 19, C_METAL_GREY)
    for y in range(12, 20):
        px(img, 22, y, C_METAL_DARK)
        px(img, 43, y, C_RUST_LIGHT)
    for x in range(22, 44):
        px(img, x, 12, C_METAL_DARK)
        px(img, x, 19, C_METAL_DARK)
    # Extremite bras : connecteur rouille
    rect(img, 40, 11, 44, 20, C_RUST_LIGHT)
    px(img, 43, 14, C_RUST_DARK)
    px(img, 43, 17, C_RUST_DARK)

    # --- Panneau tombe : 16x24 px offset de 4 px (x=44..59, y=2..25) ---
    rect(img, 44, 4, 59, 27, C_METAL_MID)
    # Bords panneau
    for x in range(44, 60):
        px(img, x, 4, C_METAL_DARK)
        px(img, x, 27, C_RUST_LIGHT)
    for y in range(4, 28):
        px(img, 44, y, C_METAL_DARK)
        px(img, 59, y, C_RUST_LIGHT)
    # Fissures diagonales sur le panneau
    for i in range(8):
        px(img, 47 + i, 8 + i, C_VOID)

    # --- Corps organique Rouille Vivante (~20% surface) ---
    # Patches #3A3A28 sur le carter
    organic_patches = [
        (3, 8), (4, 9), (3, 10), (5, 8),
        (20, 22), (21, 23), (20, 24), (22, 22),
        (10, 7), (11, 7), (10, 8),
        (6, 23), (7, 24), (6, 24),
    ]
    for ox, oy in organic_patches:
        px(img, ox, oy, C_DEBRIS_ORG)

    # Patch organique sur le panneau
    organic_panel = [
        (50, 18), (51, 19), (52, 18), (50, 20),
        (56, 10), (57, 11), (56, 11),
    ]
    for ox, oy in organic_panel:
        px(img, ox, oy, C_DEBRIS_ORG)

    # --- Zones tres sombres (creux et trous) ---
    rect(img, 0, 0, 1, 31, C_TRANSPARENT)  # bord gauche propre
    px(img, 26, 15, C_VOID)
    px(img, 27, 16, C_VOID)

    # --- Cable : ligne 1 px qui sort par la gauche ---
    # Fil horizontal depuis le carter (y=18) vers le bord gauche
    for x in range(0, 5):
        px(img, x, 18, C_METAL_GREY)
    # Connecteur rouille en bout
    px(img, 0, 17, C_RUST_LIGHT)
    px(img, 0, 18, C_RUST_LIGHT)
    px(img, 0, 19, C_RUST_LIGHT)

    # --- Rouille sur les bords exterieurs ---
    rust_border = [
        (3, 26), (4, 27), (5, 26),
        (55, 3), (56, 3), (57, 4),
        (60, 15), (61, 16), (60, 17),
        (2, 5), (2, 6),
    ]
    for rx, ry in rust_border:
        if 0 <= rx < 64 and 0 <= ry < 32:
            px(img, rx, ry, C_RUST_LIGHT)

    save(img, os.path.join(out_dir, "tile_wreck_machine.png"))


# ─── P1 — Caisse technologique : tile_crate_tech.png (32x40) ────────────────

def gen_crate_tech(out_dir):
    """
    32x40 px — conteneur militaire pre-Convergence, vue du dessus legerement inclinee.
    Dessus de caisse (24x20) + rebord de profondeur 8 px (fausse perspective).
    """
    img = new_canvas(32, 40)

    # --- Face du dessus de la caisse (y=0..19, x=4..27) ---
    rect(img, 4, 0, 27, 19, C_CRATE_MAIN)

    # Rebord metallique (1 px de contour)
    draw = ImageDraw.Draw(img)
    draw.rectangle([4, 0, 27, 19], outline=C_METAL_DARK)

    # Logo efface : rectangle 8x4 px centre (x=12..19, y=8..11)
    rect(img, 12, 8, 19, 11, C_CRATE_LOGO)

    # Bande de signalisation rouille sur le cote droit de la face
    rect(img, 24, 2, 26, 17, C_CRATE_BAND)

    # Rouille sur les coins
    corner_rust = [(4, 0), (5, 0), (4, 1),
                   (26, 0), (27, 0), (27, 1),
                   (4, 18), (5, 19), (4, 19),
                   (26, 18), (27, 18), (27, 19)]
    for rx, ry in corner_rust:
        if 0 <= rx < 32 and 0 <= ry < 40:
            px(img, rx, ry, C_CRATE_RUST)

    # --- Rebord de profondeur (y=20..27) — fausse perspective ---
    rect(img, 4, 20, 27, 27, C_METAL_DARK)
    for x in range(4, 28):
        px(img, x, 20, C_CRATE_MAIN)   # arete superieure du rebord
        px(img, x, 27, C_VOID)          # ombre sous le rebord
    # Bande signalisation sur le rebord visible
    rect(img, 24, 21, 26, 26, C_CRATE_BAND)

    # --- Sol sous la caisse visible en bas (y=28..39) — ombre ---
    for dy in range(0, 8):
        alpha = int(128 * (1.0 - dy / 8.0))
        for x in range(6, 26):
            if 0 <= 28 + dy < 40:
                current = img.getpixel((x, 28 + dy))
                if current[3] == 0:  # seulement sur transparent
                    img.putpixel((x, 28 + dy), (0x0A, 0x0A, 0x0F, alpha))

    save(img, os.path.join(out_dir, "tile_crate_tech.png"))


# ─── P1 — Arche effondree : tile_arch_fallen.png (96x32) ─────────────────────

def gen_arch_fallen(out_dir):
    """
    96x32 px — arcade architecturale du Sanctuaire effondree.
    Deux piliers aux extremites (24x32 px) + linteau affaisse au centre (48x20 px).
    Zone centrale semi-transparente (passage libre sous le linteau).
    """
    img = new_canvas(96, 32)

    # --- Pilier gauche (x=0..23, y=0..31) ---
    rect(img, 0, 0, 23, 31, C_ARCH_STONE)
    # Bords sombres
    for y in range(0, 32):
        px(img, 0, y, C_PILLAR_EDGE)
        px(img, 1, y, C_PILLAR_EDGE)
        px(img, 22, y, C_MUR_DETAIL)
        px(img, 23, y, C_MUR_DETAIL)
    for x in range(0, 24):
        px(img, x, 0, C_MUR_DETAIL)
        px(img, x, 31, C_PILLAR_EDGE)
    # Mousse sur les joints
    moss_left = [(2, 28), (3, 29), (4, 30), (2, 5), (3, 4)]
    for mx, my in moss_left:
        px(img, mx, my, C_ARCH_MOSS)
    # Fissure Aether (meme motif que le pilier)
    for y in range(5, 22):
        px(img, 11, y, C_AETHER_FISS)
    px(img, 11, 10, C_AETHER_HOT)
    px(img, 11, 18, C_AETHER_HOT)

    # --- Pilier droit (x=72..95, y=0..31) ---
    rect(img, 72, 0, 95, 31, C_ARCH_STONE)
    for y in range(0, 32):
        px(img, 72, y, C_MUR_DETAIL)
        px(img, 73, y, C_MUR_DETAIL)
        px(img, 94, y, C_PILLAR_EDGE)
        px(img, 95, y, C_PILLAR_EDGE)
    for x in range(72, 96):
        px(img, x, 0, C_MUR_DETAIL)
        px(img, x, 31, C_PILLAR_EDGE)
    moss_right = [(92, 28), (91, 29), (90, 30), (93, 5), (92, 4)]
    for mx, my in moss_right:
        px(img, mx, my, C_ARCH_MOSS)
    for y in range(5, 22):
        px(img, 84, y, C_AETHER_FISS)
    px(img, 84, 10, C_AETHER_HOT)
    px(img, 84, 18, C_AETHER_HOT)

    # --- Linteau effondre (x=24..71, y=0..19) ---
    # Affaisse au centre : hauteur varie de 12 (bords) a 20 (centre)
    lintel_x0, lintel_x1 = 24, 71
    for x in range(lintel_x0, lintel_x1 + 1):
        # Sagitta : centre affaisse de 8 px supplementaires
        rel = (x - lintel_x0) / (lintel_x1 - lintel_x0)  # 0..1
        sag = int(8 * math.sin(rel * math.pi))  # arche inverse
        y_top = 0
        y_bot = 12 + sag  # fond du linteau varie de 12 a 20
        for y in range(y_top, y_bot + 1):
            if y == y_top:
                px(img, x, y, C_MUR_DETAIL)
            elif y == y_bot:
                px(img, x, y, C_MUR_DETAIL)
            else:
                px(img, x, y, C_ARCH_STONE)
        # Mousse aleatoire deterministe sur le linteau
        if (x % 7 == 0):
            px(img, x, y_bot - 1, C_ARCH_MOSS)

    # La zone sous le linteau (y > y_bot) reste transparente (sol visible).

    save(img, os.path.join(out_dir, "tile_arch_fallen.png"))


# ─── P2 — Terminal corrompu (2 frames) ───────────────────────────────────────

def gen_terminal_corrupt(out_dir, frame=1):
    """
    32x48 px — borne d'acces pre-Convergence.
    frame=1 : ecran mort (pixel eteint)
    frame=2 : pixel Aether parasite allume
    """
    img = new_canvas(32, 48)

    # --- Socle (x=2..29, y=32..47) ---
    rect(img, 2, 32, 29, 47, C_TERM_BODY)
    # Bords socle
    for x in range(2, 30):
        px(img, x, 32, C_METAL_DARK)
        px(img, x, 47, C_METAL_DARK)
    for y in range(32, 48):
        px(img, 2, y, C_METAL_DARK)
        px(img, 29, y, C_METAL_DARK)
    # Filaments Rouille sur le socle
    filament_pos = [(3, 38), (4, 39), (3, 40), (28, 37), (27, 38), (28, 40),
                    (10, 47), (11, 46), (20, 47), (21, 46)]
    for fx, fy in filament_pos:
        px(img, fx, fy, C_TERM_FILAM)

    # --- Encadrement ecran (x=6..25, y=4..29) ---
    rect(img, 6, 4, 25, 29, C_TERM_BODY)
    for x in range(6, 26):
        px(img, x, 4, C_METAL_DARK)
        px(img, x, 29, C_METAL_DARK)
    for y in range(4, 30):
        px(img, 6, y, C_METAL_DARK)
        px(img, 25, y, C_METAL_DARK)

    # --- Ecran (x=8..23, y=6..27) ---
    rect(img, 8, 6, 23, 27, C_TERM_SCREEN)
    # Pixel mort centre
    px(img, 15, 16, C_TERM_DEAD)
    px(img, 16, 16, C_TERM_DEAD)
    px(img, 15, 17, C_TERM_DEAD)
    px(img, 16, 17, C_TERM_DEAD)

    # Lignes horizontales mortes (artefact ecran brise)
    for x in range(9, 23):
        if x % 3 == 0:
            px(img, x, 10, C_METAL_DARK)
            px(img, x, 22, C_METAL_DARK)

    # --- Frame 2 : pixel parasite Aether 2x2 ---
    if frame == 2:
        px(img, 15, 16, C_TERM_AETHER)
        px(img, 16, 16, C_TERM_AETHER)
        px(img, 15, 17, C_TERM_AETHER)
        px(img, 16, 17, C_TERM_AETHER)

    # --- Jonction socle/ecran ---
    rect(img, 4, 29, 27, 33, C_METAL_DARK)

    fname = f"tile_terminal_corrupt_0{frame}.png"
    save(img, os.path.join(out_dir, fname))


# ─── P0 — Particules VFX ennemis (4x4 px) ───────────────────────────────────

def gen_particle_4x4(out_dir, filename, color_center, color_edge):
    """
    4x4 px — carré flou pour burst de mort ennemi.
    Coins transparents, bords = color_edge, centre 2x2 = color_center.
    """
    img = new_canvas(4, 4)
    # Bords (croix)
    px(img, 0, 1, color_edge)
    px(img, 0, 2, color_edge)
    px(img, 3, 1, color_edge)
    px(img, 3, 2, color_edge)
    px(img, 1, 0, color_edge)
    px(img, 2, 0, color_edge)
    px(img, 1, 3, color_edge)
    px(img, 2, 3, color_edge)
    # Centre 2x2
    px(img, 1, 1, color_center)
    px(img, 2, 1, color_center)
    px(img, 1, 2, color_center)
    px(img, 2, 2, color_center)
    save(img, os.path.join(out_dir, filename))


def gen_particle_sentinel(out_dir):
    """
    4x4 px — sentinelle : magenta #AA4488, bords transparents (spec DA).
    """
    img = new_canvas(4, 4)
    # Centre 2x2 seulement — bords transparents comme spec
    px(img, 1, 1, C_SENTINEL_C)
    px(img, 2, 1, C_SENTINEL_C)
    px(img, 1, 2, C_SENTINEL_C)
    px(img, 2, 2, C_SENTINEL_C)
    save(img, os.path.join(out_dir, "vfx_particle_sentinel.png"))


# ─── P0 — Particule XP (3x3 px) ─────────────────────────────────────────────

def gen_particle_xp(out_dir):
    """
    3x3 px — point lumineux #AAFF44 au centre, bords transparents.
    """
    img = new_canvas(3, 3)
    px(img, 1, 1, C_XP_GREEN)
    # Bords tres legers (semi-transparent) — un seul pixel central fort
    save(img, os.path.join(out_dir, "vfx_particle_xp.png"))


# ─── P1 — Particule Aether ambiante (3x3 px) ─────────────────────────────────

def gen_particle_aether_ambient(out_dir):
    """
    3x3 px — point flou #00A0BB au centre, bords transparents.
    """
    img = new_canvas(3, 3)
    # Centre fort
    px(img, 1, 1, C_AETHER_AMBNT)
    # Bords semi-transparents (demi-intensite) pour effet flou
    edge_color = (0x00, 0xA0, 0xBB, 128)
    px(img, 0, 1, edge_color)
    px(img, 2, 1, edge_color)
    px(img, 1, 0, edge_color)
    px(img, 1, 2, edge_color)
    save(img, os.path.join(out_dir, "vfx_particle_aether_ambient.png"))


# ─── P1 — Particules impact (2x2 px) ─────────────────────────────────────────

def gen_particle_2x2(out_dir, filename, color):
    """
    2x2 px — 4 pixels de la couleur donnee, fond transparent.
    """
    img = new_canvas(2, 2)
    px(img, 0, 0, color)
    px(img, 1, 0, color)
    px(img, 0, 1, color)
    px(img, 1, 1, color)
    save(img, os.path.join(out_dir, filename))


# ─── Point d'entree ───────────────────────────────────────────────────────────

def main():
    print("=== generate_arena_obstacles.py — Phase 4 Chimera Protocol ===")
    print(f"Racine projet : {PROJECT_ROOT}")
    print()

    # Verification des dossiers de sortie
    for d in [OUT_ENV, OUT_VFX]:
        os.makedirs(d, exist_ok=True)

    # ── P0 : Obstacles ────────────────────────────────────────────────────────
    print("--- P0 : Obstacles d'arene ---")
    gen_pillar_stone(OUT_ENV)
    gen_pillar_stone_shadow(OUT_ENV)
    gen_wreck_machine(OUT_ENV)

    # ── P0 : VFX particules ennemis (remplacent/confirment ceux existants) ───
    print()
    print("--- P0 : VFX particules ennemis ---")
    gen_particle_4x4(OUT_VFX, "vfx_particle_rustswarm.png",
                     C_RUSTSWARM_C, C_RUSTSWARM_E)
    gen_particle_4x4(OUT_VFX, "vfx_particle_drone.png",
                     C_DRONE_C, C_DRONE_E)
    gen_particle_sentinel(OUT_VFX)
    gen_particle_4x4(OUT_VFX, "vfx_particle_colossus.png",
                     C_COLOSSUS_C, C_COLOSSUS_E)
    gen_particle_xp(OUT_VFX)

    # ── P1 : VFX ambiance et impacts ─────────────────────────────────────────
    print()
    print("--- P1 : VFX ambiance et impacts ---")
    gen_particle_aether_ambient(OUT_VFX)
    gen_particle_2x2(OUT_VFX, "vfx_particle_impact_plasma.png",
                     C_PLASMA_EDGE)
    gen_particle_2x2(OUT_VFX, "vfx_particle_impact_sentinel.png",
                     C_SURCHARGE_P)

    # ── P1 : Obstacles supplementaires ────────────────────────────────────────
    print()
    print("--- P1 : Obstacles supplementaires ---")
    gen_crate_tech(OUT_ENV)
    gen_arch_fallen(OUT_ENV)

    # ── P2 : Terminal corrompu (2 frames) ────────────────────────────────────
    print()
    print("--- P2 : Terminal corrompu ---")
    gen_terminal_corrupt(OUT_ENV, frame=1)
    gen_terminal_corrupt(OUT_ENV, frame=2)

    print()
    print("=== Generation terminee ===")


if __name__ == "__main__":
    main()
