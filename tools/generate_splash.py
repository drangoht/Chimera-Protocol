"""
generate_splash.py -- Generateur Phase 3 : splash screen + sprites decor arene.
Projet : Chimera Protocol

Livrables :
  - assets/sprites/ui/splash_art.png          (1280x720)
  - assets/sprites/environment/tile_floor_stone.png   (32x32)
  - assets/sprites/environment/tile_wall_stone.png    (32x32)
  - assets/sprites/environment/decor_column.png       (32x80)
  - assets/sprites/environment/decor_aether_geyser.png (32x48)
  - assets/sprites/environment/decor_debris.png       (32x32)
  + un fichier .import Godot pour chaque PNG.

Palette conforme a docs/STYLE_GUIDE.md -- principe "Matiere morte / Energie vivante".
Contrainte bloom : les couleurs Aether (#AA44FF, #CC88FF, #00E5FF) doivent rester au-dessus
du seuil glow_hdr_threshold=0.6 ; les couleurs "matiere morte" en dessous.

Usage :
  C:\\Users\\drang\\AppData\\Local\\Programs\\Python\\Python313\\python.exe tools/generate_splash.py
"""

import os
import sys
import math
import random
import secrets

from PIL import Image, ImageDraw, ImageFilter

# ---------------------------------------------------------------------------
# Constantes de chemin (absolu depuis la racine du depot)
# ---------------------------------------------------------------------------

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_UI  = os.path.join(REPO_ROOT, "assets", "sprites", "ui")
OUT_ENV = os.path.join(REPO_ROOT, "assets", "sprites", "environment")

# ---------------------------------------------------------------------------
# Palette (STYLE_GUIDE.md §1)
# Toutes les valeurs RGB sont des tuples (R, G, B, A).
# ---------------------------------------------------------------------------

# --- Matiere morte (fond, arene, debris) ---
C_BG_DEEP    = (0x0A, 0x0A, 0x1A, 255)   # fond profond
C_BG_MID     = (0x1A, 0x1A, 0x2E, 255)   # centre du degrade
C_BG_TOP     = (0x0D, 0x0D, 0x1A, 255)   # haut du degrade
C_SOL_DARK   = (0x1A, 0x1A, 0x22, 255)   # sol sombre de base
C_SOL_JOINT  = (0x1E, 0x1E, 0x2A, 255)   # joints de dalles
C_SOL_CRACK  = (0x1A, 0x1A, 0x28, 255)   # fissures dans le sol
C_SOL_ECLAT  = (0x3A, 0x3A, 0x4E, 255)   # reflet ponctuel sur pierre
C_MUR_BASE   = (0x1E, 0x1E, 0x2E, 255)   # mur pierre sombre
C_MUR_BLOC   = (0x25, 0x25, 0x35, 255)   # blocs de maconnerie
C_MUR_MOUSSE = (0x33, 0x44, 0x33, 255)   # mousse/crasse
C_COLONNE    = (0x3A, 0x3A, 0x4E, 255)   # fut colonne
C_COL_OMBRE  = (0x1E, 0x1E, 0x30, 255)   # ombre colonne
C_COL_REFLET = (0x4A, 0x4A, 0x5E, 255)   # reflet colonne
C_RUINE_SILH = (0x1E, 0x1E, 0x30, 255)   # silhouette ruines horizon
C_DEBRIS_A   = (0x2D, 0x2D, 0x3E, 255)   # debris pierre clair
C_DEBRIS_B   = (0x4A, 0x4A, 0x5E, 255)   # debris pierre pale
C_DEBRIS_DRK = (0x1E, 0x1E, 0x2A, 255)   # poussiere debris
C_METAL_GREY = (0x4A, 0x4A, 0x52, 255)   # acier mort

# --- Energie vivante / Aether ---
C_AETHER_PRI  = (0x00, 0xCC, 0xFF, 255)  # cyan joueur (STYLE_GUIDE §1.3 implants = #00E5FF,
                                           # la mission demande #00CCFF -- on prend la valeur
                                           # mission pour la silhouette du menu, acceptee ici
                                           # car c'est un ecran titre hors arene)
C_AETHER_GLOW = (0x00, 0xE5, 0xFF, 255)  # cyan Aether pur (STYLE_GUIDE primaire)
C_NOYAU_VIOL  = (0xAA, 0x44, 0xFF, 255)  # violet Noyau
C_NOYAU_GLOW  = (0xCC, 0x88, 0xFF, 255)  # violet pale/glow
C_AETHER_DEEP = (0x66, 0x22, 0xAA, 255)  # violet profond particules
C_AETHER_FISS = (0x44, 0x00, 0x88, 255)  # violet tres sombre, halo fissure

# --- Cyborg (corps silhouette menu) ---
C_CYBORG_BODY = (0x00, 0xCC, 0xFF, 255)  # corps cyan (silhouette lumineuse sur fond sombre)
C_CYBORG_IMP  = (0xCC, 0x44, 0x22, 255)  # implants mecaniques/rouille (accent chaud)
C_CYBORG_DARK = (0x00, 0x44, 0x66, 255)  # zones d'ombre du corps

# --- UI texte ---
C_TITRE       = (0xCC, 0x88, 0xFF, 255)  # titre principal (violet pale)
C_TITRE_OMBRE = (0x44, 0x00, 0x88, 255)  # ombre du titre
C_TAGLINE     = (0x88, 0x88, 0xCC, 255)  # sous-titre bleu-violet
C_PRESS_ANY   = (0x44, 0x44, 0xAA, 255)  # "PRESS ANY KEY" bas de page

# --- Universel ---
C_BLACK       = (0x00, 0x00, 0x00, 255)
C_TRANSPARENT = (0x00, 0x00, 0x00, 0)
C_WHITE       = (0xFF, 0xFF, 0xFF, 255)


# ---------------------------------------------------------------------------
# Utilitaires bas niveau
# ---------------------------------------------------------------------------

def new_canvas(w, h, bg=C_TRANSPARENT):
    return Image.new("RGBA", (w, h), bg)


def px(img, x, y, color):
    """Pose un pixel si dans les limites."""
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), color)


def rect(img, x0, y0, x1, y1, color):
    draw = ImageDraw.Draw(img)
    draw.rectangle([x0, y0, x1, y1], fill=color)


def hline(img, y, x0, x1, color):
    draw = ImageDraw.Draw(img)
    draw.line([(x0, y), (x1, y)], fill=color)


def vline(img, x, y0, y1, color):
    draw = ImageDraw.Draw(img)
    draw.line([(x, y0), (x, y1)], fill=color)


def blend_color(c1, c2, t):
    """Interpolation lineaire entre deux couleurs RGBA."""
    return tuple(int(a + (b - a) * t) for a, b in zip(c1, c2))


def alpha_color(c, a):
    """Retourne la couleur avec un alpha donne (0-255)."""
    return (c[0], c[1], c[2], a)


def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "PNG")
    print(f"  [OK] {os.path.relpath(path, REPO_ROOT)}")


# ---------------------------------------------------------------------------
# Generation de fichiers .import Godot
# ---------------------------------------------------------------------------

def _uid():
    """Genere un uid hex aleatoire de 12 caracteres (format Godot uid://)."""
    return secrets.token_hex(6)


def _hash8():
    """Genere un hash de 8 caracteres pour le nom du .ctex."""
    return secrets.token_hex(4)


def generate_import(png_path, res_prefix="res://"):
    """
    Cree un fichier .import minimal pour que Godot detecte le PNG.
    Le .import est place a cote du PNG (meme chemin + extension .import).
    """
    fname = os.path.basename(png_path)
    uid = _uid()
    h = _hash8()
    name_no_ext = os.path.splitext(fname)[0]

    # Chemin res:// du PNG source
    rel = png_path.replace(REPO_ROOT, "").replace("\\", "/").lstrip("/")
    src_res = res_prefix + rel
    ctex_name = f"{name_no_ext}-{h}.ctex"
    dest_res = f"res://.godot/imported/{ctex_name}"

    content = f"""[remap]
importer="texture"
type="CompressedTexture2D"
uid="uid://{uid}"
path="{dest_res}"
metadata={{
"vram_texture": false
}}

[deps]
source_file="{src_res}"
dest_files=["{dest_res}"]

[params]
compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/normal_map=0
compress/hdr_compression=1
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
svg/scale=1.0
editor/scale_with_editor_scale=false
editor/convert_colors_with_editor_theme=false
"""
    import_path = png_path + ".import"
    with open(import_path, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"  [OK] {os.path.relpath(import_path, REPO_ROOT)}")


# ===========================================================================
# TACHE 1 — SPLASH SCREEN 1280x720
# ===========================================================================

def draw_gradient_bg(img):
    """Degrade vertical : #0A0A1A (bas) -> #1A1A2E (centre) -> #0D0D1A (haut)."""
    w, h = img.size
    mid = h // 2
    for y in range(h):
        if y <= mid:
            t = y / mid
            c = blend_color(C_BG_TOP, C_BG_MID, t)
        else:
            t = (y - mid) / (h - mid)
            c = blend_color(C_BG_MID, C_BG_DEEP, t)
        hline(img, y, 0, w - 1, c)


def draw_ruin_silhouettes(img):
    """
    Silhouettes de ruines a l'horizon (y autour de 420-520 px).
    Colonnes brisees, arches, debris -- tons sombres #1E1E30.
    """
    draw = ImageDraw.Draw(img)
    w = img.width
    rng = random.Random(42)   # seed fixe pour reproductibilite

    horizon_y = 460  # ligne de sol de l'horizon

    # -- Quelques colonnes brisees --
    columns = [
        (120, 300, 30, 160),
        (210, 340, 22, 120),
        (480, 280, 35, 180),
        (640, 320, 28, 140),
        (820, 310, 32, 170),
        (970, 350, 25, 110),
        (1100, 290, 38, 190),
        (1180, 360, 20, 100),
    ]
    for cx, top_y, col_w, col_h in columns:
        # Fut de la colonne
        draw.rectangle(
            [cx - col_w // 2, top_y, cx + col_w // 2, horizon_y],
            fill=C_RUINE_SILH
        )
        # Cassure au sommet (forme irreguliere)
        break_y = top_y + rng.randint(10, 30)
        # Quelques encoches
        for _ in range(rng.randint(2, 4)):
            bx = cx - col_w // 2 + rng.randint(0, col_w)
            by = top_y + rng.randint(0, 40)
            bw = rng.randint(3, 8)
            bh = rng.randint(4, 14)
            draw.rectangle([bx, by, bx + bw, by + bh], fill=C_BG_MID)

    # -- Arche centrale brisee --
    arch_cx = 640
    arch_w = 200
    arch_h = 120
    arch_top = horizon_y - arch_h - 40
    # Jambe gauche
    draw.rectangle([arch_cx - arch_w // 2 - 18, arch_top, arch_cx - arch_w // 2 + 18, horizon_y], fill=C_RUINE_SILH)
    # Jambe droite
    draw.rectangle([arch_cx + arch_w // 2 - 18, arch_top, arch_cx + arch_w // 2 + 18, horizon_y], fill=C_RUINE_SILH)
    # Linteau (partie haute, brisee au centre)
    draw.rectangle([arch_cx - arch_w // 2 - 18, arch_top, arch_cx - 30, arch_top + 22], fill=C_RUINE_SILH)
    draw.rectangle([arch_cx + 30, arch_top, arch_cx + arch_w // 2 + 18, arch_top + 22], fill=C_RUINE_SILH)

    # -- Sol sombre de l'horizon --
    draw.rectangle([0, horizon_y, w, horizon_y + 60], fill=C_RUINE_SILH)
    # Dents irreguliers sur le bord du sol
    for bx in range(0, w, rng.randint(12, 28)):
        bh = rng.randint(2, 10)
        draw.rectangle([bx, horizon_y - bh, bx + rng.randint(6, 20), horizon_y], fill=C_RUINE_SILH)


def draw_aether_particles(img, rng_seed=7):
    """Petites particules Aether (#6622AA semi-transparent) dispersees aleatoirement."""
    rng = random.Random(rng_seed)
    w, h = img.size
    n_particles = 280
    for _ in range(n_particles):
        x = rng.randint(0, w - 1)
        y = rng.randint(0, h - 1)
        a = rng.randint(30, 90)  # semi-transparent
        size = rng.randint(1, 2)
        c = (0x66, 0x22, 0xAA, a)
        for dy in range(size):
            for dx in range(size):
                px(img, x + dx, y + dy, c)


def draw_aether_rays(img, cx, cy):
    """
    6-8 rayons d'energie Aether partant du cyborg (cx, cy).
    Couleur : #AA44FF -> #CC88FF -> transparent, longueur 200-300 px.
    """
    draw = ImageDraw.Draw(img)
    rng = random.Random(13)
    n_rays = 7
    for i in range(n_rays):
        angle_base = (i / n_rays) * 2 * math.pi
        angle = angle_base + rng.uniform(-0.15, 0.15)
        length = rng.randint(200, 310)
        width_base = rng.randint(4, 10)

        # Degrade de couleur et d'alpha le long du rayon
        steps = length
        for s in range(steps):
            t = s / steps
            # Largeur qui diminue
            ray_w = max(1, int(width_base * (1 - t)))
            # Couleur : violet clair vers transparent
            a = int(180 * (1 - t))
            if t < 0.5:
                c = blend_color(C_NOYAU_VIOL, C_NOYAU_GLOW, t * 2)
            else:
                c = C_NOYAU_GLOW
            c = alpha_color(c, a)

            rx = int(cx + math.cos(angle) * s)
            ry = int(cy + math.sin(angle) * s)
            for dw in range(-ray_w // 2, ray_w // 2 + 1):
                # Perpendiculaire au rayon
                px_x = int(rx - math.sin(angle) * dw)
                px_y = int(ry + math.cos(angle) * dw)
                if 0 <= px_x < img.width and 0 <= px_y < img.height:
                    # Blend additif manuel : on recupere le pixel existant
                    existing = img.getpixel((px_x, px_y))
                    blended = (
                        min(255, existing[0] + c[0] * c[3] // 255),
                        min(255, existing[1] + c[1] * c[3] // 255),
                        min(255, existing[2] + c[2] * c[3] // 255),
                        255
                    )
                    img.putpixel((px_x, px_y), blended)


def draw_cyborg_silhouette(img, cx, cy_base):
    """
    Silhouette cyborg stylisee en pixel art.
    Point de reference : cy_base = bas du personnage.
    Le personnage monte vers le haut depuis cy_base.
    Construction par blocs rectangulaires + accents.
    """
    draw = ImageDraw.Draw(img)

    # Hauteur totale ~180 px pour la silhouette splash (echelle menu, pas sprite in-game)
    # On construit de haut en bas.

    # --- Tete (losange/ovale cyan) ---
    head_w, head_h = 52, 44
    head_cx = cx
    head_top = cy_base - 180
    head_mid = head_top + head_h // 2

    # Corps principal de la tete : ellipse approximee par plusieurs rectangles
    for row in range(head_h):
        t = abs(row - head_h // 2) / (head_h // 2)
        half_w = int(head_w // 2 * math.sqrt(max(0, 1 - t * t)))
        if half_w > 0:
            r = head_top + row
            # Ombre (bord bas de la tete)
            shade = alpha_color(C_CYBORG_DARK, 255) if row > head_h * 2 // 3 else C_CYBORG_BODY
            draw.line([(head_cx - half_w, r), (head_cx + half_w, r)], fill=shade)

    # Visiere (bande horizontale sombre au milieu de la tete)
    visor_y = head_top + head_h // 2 - 6
    visor_h = 12
    visor_w = int(head_w * 0.55)
    draw.rectangle(
        [head_cx - visor_w // 2, visor_y, head_cx + visor_w // 2, visor_y + visor_h],
        fill=C_CYBORG_DARK
    )
    # Yeux lumineux (2 points cyan vif)
    eye_y = visor_y + visor_h // 2 - 2
    for ex in [-10, 10]:
        draw.rectangle([head_cx + ex - 3, eye_y, head_cx + ex + 3, eye_y + 4], fill=C_AETHER_GLOW)

    # Contour tete
    draw.ellipse(
        [head_cx - head_w // 2, head_top, head_cx + head_w // 2, head_top + head_h],
        outline=C_BLACK, width=2
    )

    # --- Cou ---
    neck_top = head_top + head_h
    neck_h = 14
    draw.rectangle(
        [head_cx - 10, neck_top, head_cx + 10, neck_top + neck_h],
        fill=C_CYBORG_DARK
    )

    # --- Epaules + torse ---
    torso_top = neck_top + neck_h
    torso_h = 70
    torso_w = 90

    # Epaule gauche (grande, pince mecanique)
    draw.rectangle(
        [head_cx - torso_w // 2 - 20, torso_top,
         head_cx - torso_w // 2 + 18, torso_top + 28],
        fill=C_CYBORG_IMP
    )
    # Epaule droite (plus fine)
    draw.rectangle(
        [head_cx + torso_w // 2 - 18, torso_top,
         head_cx + torso_w // 2 + 10, torso_top + 22],
        fill=C_CYBORG_BODY
    )
    # Torse principal
    draw.rectangle(
        [head_cx - torso_w // 2, torso_top,
         head_cx + torso_w // 2, torso_top + torso_h],
        fill=C_CYBORG_BODY
    )
    # Ligne centrale implants
    for iy in range(torso_top + 8, torso_top + torso_h - 8, 12):
        draw.rectangle(
            [head_cx - 2, iy, head_cx + 2, iy + 6],
            fill=C_AETHER_GLOW
        )
    # Implant rouge (accent mecanique)
    draw.rectangle(
        [head_cx - torso_w // 2 + 8, torso_top + 14,
         head_cx - torso_w // 2 + 20, torso_top + 26],
        fill=C_CYBORG_IMP
    )

    # --- Bras gauche (mecanique, plus epais) ---
    arm_left_top = torso_top + 8
    draw.rectangle(
        [head_cx - torso_w // 2 - 38, arm_left_top,
         head_cx - torso_w // 2, arm_left_top + 16],
        fill=C_CYBORG_IMP
    )
    # Pince au bout
    px_cx = head_cx - torso_w // 2 - 38
    draw.rectangle(
        [px_cx - 20, arm_left_top - 8, px_cx, arm_left_top + 24],
        fill=C_CYBORG_IMP
    )
    # Dents de pince
    draw.rectangle([px_cx - 20, arm_left_top - 8, px_cx - 12, arm_left_top - 2], fill=C_CYBORG_DARK)
    draw.rectangle([px_cx - 20, arm_left_top + 18, px_cx - 12, arm_left_top + 24], fill=C_CYBORG_DARK)

    # --- Bras droit (arme) ---
    arm_right_top = torso_top + 10
    draw.rectangle(
        [head_cx + torso_w // 2, arm_right_top,
         head_cx + torso_w // 2 + 50, arm_right_top + 12],
        fill=C_CYBORG_BODY
    )
    # Canon de l'arme
    draw.rectangle(
        [head_cx + torso_w // 2 + 40, arm_right_top - 4,
         head_cx + torso_w // 2 + 80, arm_right_top + 16],
        fill=C_CYBORG_DARK
    )
    # Flash de tir
    draw.rectangle(
        [head_cx + torso_w // 2 + 78, arm_right_top + 2,
         head_cx + torso_w // 2 + 88, arm_right_top + 10],
        fill=C_AETHER_GLOW
    )

    # --- Bas du corps / jambes ---
    legs_top = torso_top + torso_h
    legs_h = 50
    # Jambe gauche
    draw.rectangle(
        [head_cx - 28, legs_top, head_cx - 6, legs_top + legs_h],
        fill=C_CYBORG_DARK
    )
    # Jambe droite
    draw.rectangle(
        [head_cx + 6, legs_top, head_cx + 28, legs_top + legs_h],
        fill=C_CYBORG_BODY
    )
    # Pieds
    draw.rectangle(
        [head_cx - 34, legs_top + legs_h - 8, head_cx - 2, legs_top + legs_h + 6],
        fill=C_CYBORG_DARK
    )
    draw.rectangle(
        [head_cx + 2, legs_top + legs_h - 8, head_cx + 34, legs_top + legs_h + 6],
        fill=C_CYBORG_BODY
    )

    # --- Halo de base (aura autour du personnage) ---
    halo_cx = cx
    halo_cy = torso_top + torso_h // 2
    for r_size in range(80, 30, -8):
        a = int(60 * (1 - (r_size - 30) / 50))
        c = alpha_color(C_NOYAU_VIOL, a)
        draw.ellipse(
            [halo_cx - r_size, halo_cy - r_size,
             halo_cx + r_size, halo_cy + r_size],
            outline=c, width=1
        )


def draw_vignette(img):
    """
    Vignette sombre sur les bords (assombrit les coins progressivement).
    Technique : calque RGBA noir dont l'alpha croit vers les bords.
    """
    w, h = img.size
    vignette = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(vignette)

    steps = 200
    for i in range(steps):
        t = i / steps
        a = int(160 * t * t)  # courbe quadratique -> plus doux au centre
        c = (0, 0, 0, a)
        # On dessine des ellipses de plus en plus grandes vers les bords
        margin = int((steps - i) * 2.2)
        draw.ellipse([margin, margin * 9 // 16, w - margin, h - margin * 9 // 16], outline=c, width=3)

    img.alpha_composite(vignette)


def draw_scanlines(img):
    """Scanlines horizontales : lignes #000000 a 15% d'opacite toutes les 4 px (effet CRT)."""
    w, h = img.size
    scanline = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(scanline)
    for y in range(0, h, 4):
        draw.line([(0, y), (w - 1, y)], fill=(0, 0, 0, 38))  # 38 ~ 15% de 255
    img.alpha_composite(scanline)


def draw_text_layer(img):
    """
    Typographie du splash screen.
    Pillow charge la fonte par defaut (ImageFont.load_default()).
    Godot utilisera sa propre police en jeu -- cette image est uniquement le key art statique.
    """
    from PIL import ImageFont

    draw = ImageDraw.Draw(img)
    w, h = img.size

    def centered_text(draw, text, y, font, color, shadow_color=None, shadow_offset=3):
        try:
            bbox = draw.textbbox((0, 0), text, font=font)
            text_w = bbox[2] - bbox[0]
        except AttributeError:
            # Ancien Pillow sans textbbox
            text_w, _ = draw.textsize(text, font=font)
        x = (w - text_w) // 2
        if shadow_color:
            draw.text((x + shadow_offset, y + shadow_offset), text, font=font, fill=shadow_color)
        draw.text((x, y), text, font=font, fill=color)

    # -- Tenter de charger une police taille raisonnable --
    # Pillow 10+ : ImageFont.load_default(size=N) disponible
    try:
        font_title   = ImageFont.load_default(size=72)
        font_tagline = ImageFont.load_default(size=22)
        font_press   = ImageFont.load_default(size=18)
    except TypeError:
        # Pillow < 10 : load_default ne prend pas de parametre size
        font_title   = ImageFont.load_default()
        font_tagline = ImageFont.load_default()
        font_press   = ImageFont.load_default()

    # Titre principal
    centered_text(
        draw, "CHIMERA PROTOCOL", 120,
        font=font_title,
        color=C_TITRE,
        shadow_color=C_TITRE_OMBRE,
        shadow_offset=3
    )

    # Sous-titre / tagline
    # Note STYLE_GUIDE §6.8 : tagline officielle = "Fusionne. Evolue. Survive."
    # La mission propose une variante poetique ; on garde la version mission
    # ("Survive. Evolue. Ne reviens jamais le meme.") en l'absence de conflits
    # de lore cotes par le directeur artistique.
    centered_text(
        draw, "Survive. Evolue. Ne reviens jamais le meme.", 210,
        font=font_tagline,
        color=C_TAGLINE
    )

    # Bas de page
    centered_text(
        draw, "PRESS ANY KEY", 660,
        font=font_press,
        color=C_PRESS_ANY
    )


def generate_splash():
    print("\n[Tache 1] Splash screen 1280x720 ...")
    W, H = 1280, 720
    img = new_canvas(W, H, C_BG_DEEP)

    # 1. Degrade de fond
    draw_gradient_bg(img)

    # 2. Particules Aether (sous les autres elements)
    draw_aether_particles(img)

    # 3. Ruines a l'horizon
    draw_ruin_silhouettes(img)

    # 4. Rayons d'energie partant du cyborg
    cyborg_cx = W // 2
    cyborg_base_y = H - 100  # le bas du personnage est a 100px du bas
    draw_aether_rays(img, cyborg_cx, cyborg_base_y - 90)

    # 5. Silhouette cyborg
    draw_cyborg_silhouette(img, cyborg_cx, cyborg_base_y)

    # 6. Vignette bords
    draw_vignette(img)

    # 7. Scanlines
    draw_scanlines(img)

    # 8. Texte
    draw_text_layer(img)

    # Sauvegarde
    # Note : le STYLE_GUIDE §8.5 place le splash a assets/ (racine du repo, pas dans sprites/).
    # La mission demande assets/sprites/ui/splash_art.png.
    # On honore la demande mission (assets/sprites/ui/) et on fait une note ci-dessous.
    out_path = os.path.join(OUT_UI, "splash_art.png")
    save(img, out_path)
    generate_import(out_path)
    return out_path


# ===========================================================================
# TACHE 2 — SPRITES DECOR ARENE
# ===========================================================================

# ---------------------------------------------------------------------------
# 2a. Tuile de sol -- tile_floor_stone.png (32x32)
# ---------------------------------------------------------------------------

def generate_tile_floor_stone():
    """
    Pierre ancienne gris-bleu fonce.
    Base #2A2A3A, joints #1E1E2A, fissures #1A1A28, eclat #3A3A4E.
    """
    print("\n[Tache 2a] tile_floor_stone.png (32x32) ...")
    img = new_canvas(32, 32, (0x2A, 0x2A, 0x3A, 255))

    BASE   = (0x2A, 0x2A, 0x3A, 255)
    JOINT  = (0x1E, 0x1E, 0x2A, 255)
    CRACK  = (0x1A, 0x1A, 0x28, 255)
    ECLAT  = (0x3A, 0x3A, 0x4E, 255)

    # Joints de dalles (lignes horizontales et verticales)
    # Horizontal : y=10, y=21
    hline(img, 10, 0, 31, JOINT)
    hline(img, 11, 0, 31, JOINT)
    hline(img, 21, 0, 31, JOINT)
    hline(img, 22, 0, 31, JOINT)

    # Vertical : decales selon la rangee de dalle (pattern brique)
    # Rangee 0-9 : joint a x=16
    vline(img, 16, 0, 9, JOINT)
    # Rangee 12-20 : joint a x=8 et x=24
    vline(img,  8, 12, 20, JOINT)
    vline(img, 24, 12, 20, JOINT)
    # Rangee 23-31 : joint a x=16
    vline(img, 16, 23, 31, JOINT)

    # Fissures (2-3 segments irreguliers)
    # Fissure 1 : dalle haut gauche
    for i, (fx, fy) in enumerate([(4, 2), (5, 3), (5, 4), (6, 5), (7, 5)]):
        px(img, fx, fy, CRACK)
    # Fissure 2 : dalle bas droite
    for (fx, fy) in [(20, 25), (21, 26), (21, 27), (22, 28)]:
        px(img, fx, fy, CRACK)

    # Eclat (1-2 pixels de reflet)
    px(img, 2, 2, ECLAT)
    px(img, 29, 24, ECLAT)

    # Variation subtile : quelques pixels aleatoires (seed fixe)
    rng = random.Random(101)
    for _ in range(12):
        xi = rng.randint(0, 31)
        yi = rng.randint(0, 31)
        # Eviter les pixels de joint/fissure
        existing = img.getpixel((xi, yi))
        if existing == BASE:
            v = rng.randint(-8, 8)
            c = tuple(max(0, min(255, ch + v)) for ch in BASE[:3]) + (255,)
            px(img, xi, yi, c)

    out_path = os.path.join(OUT_ENV, "tile_floor_stone.png")
    save(img, out_path)
    generate_import(out_path)
    return out_path


# ---------------------------------------------------------------------------
# 2b. Mur de pierre -- tile_wall_stone.png (32x32)
# ---------------------------------------------------------------------------

def generate_tile_wall_stone():
    """
    Pierre plus foncee que le sol.
    Base #1E1E2E, blocs de maconnerie #252535.
    Joints horizontaux tous les 10 px, verticaux decales (pattern brique).
    Mousse #334433.
    """
    print("\n[Tache 2b] tile_wall_stone.png (32x32) ...")

    BASE   = (0x1E, 0x1E, 0x2E, 255)
    BLOC   = (0x25, 0x25, 0x35, 255)
    JOINT  = (0x14, 0x14, 0x20, 255)
    MOUSSE = (0x33, 0x44, 0x33, 255)

    img = new_canvas(32, 32, BASE)

    # Blocs de maconnerie : 3 rangees (hauteur ~10 px chacune)
    # Rangee 0 : y 0-9
    rect(img, 1, 1, 14, 9, BLOC)
    rect(img, 17, 1, 30, 9, BLOC)
    # Rangee 1 : y 11-20 (decalee)
    rect(img, 5, 11, 26, 20, BLOC)
    # Rangee 2 : y 22-31
    rect(img, 0, 22, 12, 31, BLOC)
    rect(img, 15, 22, 31, 31, BLOC)

    # Joints horizontaux
    hline(img, 10, 0, 31, JOINT)
    hline(img, 21, 0, 31, JOINT)

    # Joints verticaux (decales par rangee)
    vline(img, 15, 0, 9, JOINT)   # rangee 0
    vline(img,  4, 11, 20, JOINT)  # rangee 1
    vline(img, 27, 11, 20, JOINT)
    vline(img, 14, 22, 31, JOINT)  # rangee 2

    # Mousse : pixels isoles en bas du mur
    moss_positions = [(2, 30), (7, 31), (16, 29), (22, 30), (28, 31), (11, 28)]
    for mx, my in moss_positions:
        px(img, mx, my, MOUSSE)

    out_path = os.path.join(OUT_ENV, "tile_wall_stone.png")
    save(img, out_path)
    generate_import(out_path)
    return out_path


# ---------------------------------------------------------------------------
# 2c. Colonne en ruine -- decor_column.png (32x80)
# ---------------------------------------------------------------------------

def generate_decor_column():
    """
    Colonne cylindrique pixel art, 32x80 px.
    Chapiteau abime en haut, base solide en bas, cassure au tiers superieur.
    Couleur principale #3A3A4E, ombre #1E1E30, reflet #4A4A5E.
    """
    print("\n[Tache 2c] decor_column.png (32x80) ...")

    MAIN   = (0x3A, 0x3A, 0x4E, 255)
    OMBRE  = (0x1E, 0x1E, 0x30, 255)
    REFLET = (0x4A, 0x4A, 0x5E, 255)
    JOINT  = (0x28, 0x28, 0x3A, 255)  # lignes horizontales du fut
    ECLAT  = (0x5A, 0x5A, 0x6E, 255)  # eclat

    img = new_canvas(32, 80, C_TRANSPARENT)

    # -- Chapiteau (haut, y 0-12) --
    # Forme aplatie, legerement plus large que le fut, irregular sur le bord droit
    rect(img, 2, 2, 29, 5, MAIN)     # talon superieur
    rect(img, 4, 6, 27, 12, MAIN)    # corps chapiteau
    # Abimages : quelques pixels manquants en haut-droite
    for (ax, ay) in [(27, 2), (28, 2), (29, 3), (28, 4), (27, 5), (25, 6), (26, 7)]:
        px(img, ax, ay, C_TRANSPARENT)
    # Ombre bord gauche chapiteau
    vline(img, 2, 2, 12, OMBRE)
    # Reflet bord superieur
    hline(img, 2, 3, 26, REFLET)

    # -- Fut (corps, y 13-67) --
    # Cylindre : largeur 20 px, centre a x=16
    FUT_W = 20
    FUT_X0 = (32 - FUT_W) // 2  # = 6
    FUT_X1 = FUT_X0 + FUT_W - 1  # = 25
    for y in range(13, 68):
        rect(img, FUT_X0, y, FUT_X1, y, MAIN)
    # Ombre gauche (1-2 px)
    vline(img, FUT_X0, 13, 67, OMBRE)
    vline(img, FUT_X0 + 1, 13, 67, (0x24, 0x24, 0x38, 255))
    # Reflet droite (1 px)
    vline(img, FUT_X1, 13, 67, REFLET)

    # Cannelures horizontales (joints de blocs de pierre)
    for jy in range(20, 68, 14):
        hline(img, jy, FUT_X0, FUT_X1, JOINT)

    # -- Cassure au tiers superieur (y ~26-32) --
    # Quelques pixels manquants sur le bord droit du fut, debris pixels
    for (ax, ay) in [(25, 26), (24, 27), (25, 27), (25, 28), (23, 29)]:
        px(img, ax, ay, C_TRANSPARENT)
    # Eclat de pierre casse
    px(img, 26, 28, ECLAT)
    px(img, 27, 29, OMBRE)

    # -- Base (y 68-79) --
    # Plus large que le fut : socle
    rect(img, 3, 68, 28, 73, MAIN)
    rect(img, 1, 74, 30, 79, MAIN)
    # Ombre sous la base
    hline(img, 79, 1, 30, OMBRE)
    hline(img, 73, 3, 28, JOINT)
    # Reflet haut base
    hline(img, 68, 4, 27, REFLET)

    out_path = os.path.join(OUT_ENV, "decor_column.png")
    save(img, out_path)
    generate_import(out_path)
    return out_path


# ---------------------------------------------------------------------------
# 2d. Geyser Aether -- decor_aether_geyser.png (32x48)
# ---------------------------------------------------------------------------

def generate_decor_aether_geyser():
    """
    Base : fissure ovale sombre.
    Jaillissement : 3 flammes de pixels violets montantes.
    y=0 = haut = pointe des flammes. y=47 = bas = fissure dans le sol.
    """
    print("\n[Tache 2d] decor_aether_geyser.png (32x48) ...")

    FISS   = (0x11, 0x00, 0x11, 255)   # fissure ovale
    HALO_A = (0x44, 0x00, 0x88, 180)   # halo externe
    HALO_B = (0x22, 0x00, 0x44, 120)   # halo plus loin
    FLAME1 = C_NOYAU_VIOL               # flamme centrale base (#AA44FF)
    FLAME1_TIP = C_NOYAU_GLOW           # pointe flamme centrale (#CC88FF)
    FLAME2 = C_AETHER_DEEP              # flammes laterales (#6622AA)

    img = new_canvas(32, 48, C_TRANSPARENT)

    # -- Fissure ovale au bas (y 40-47, centre x=16) --
    fiss_cx, fiss_cy = 16, 44
    fiss_rx, fiss_ry = 10, 4
    draw = ImageDraw.Draw(img)
    draw.ellipse(
        [fiss_cx - fiss_rx, fiss_cy - fiss_ry,
         fiss_cx + fiss_rx, fiss_cy + fiss_ry],
        fill=FISS
    )

    # Halo autour de la fissure
    for r_add in range(1, 5):
        a = max(0, 180 - r_add * 40)
        c = alpha_color(C_AETHER_FISS, a)
        draw.ellipse(
            [fiss_cx - fiss_rx - r_add, fiss_cy - fiss_ry - r_add // 2,
             fiss_cx + fiss_rx + r_add, fiss_cy + fiss_ry + r_add // 2],
            outline=c, width=1
        )

    # -- Flammes montantes (depuis y=40 vers y=0) --
    # Les flammes s'effilent vers le haut.
    # Convention : y=47 = base (fissure), y=0 = pointe

    # Flamme centrale (large en bas, pointue en haut)
    # Profile : largeur 4px a y=38, 3px a y=28, 2px a y=16, 1px a y=6, 0 a y=0
    for y in range(6, 40):
        progress = (40 - y) / 34.0  # 0 en bas, 1 en haut
        # Largeur decrois vers le haut
        half_w = max(0, int(2 * (1 - progress)))
        # Couleur : violet vif en bas, violet pale en haut
        c = blend_color(FLAME1, FLAME1_TIP, progress)
        a = int(255 * (0.4 + 0.6 * (1 - progress * 0.6)))
        c = alpha_color(c, a)
        if half_w == 0:
            px(img, fiss_cx, y, c)
        else:
            for dx in range(-half_w, half_w + 1):
                px(img, fiss_cx + dx, y, c)

    # Pointe ultime : 1-2 pixels en haut
    px(img, fiss_cx, 4, alpha_color(FLAME1_TIP, 180))
    px(img, fiss_cx, 2, alpha_color(FLAME1_TIP, 80))

    # Flamme laterale gauche (plus courte et fine)
    for y in range(14, 40):
        progress = (40 - y) / 26.0
        c = alpha_color(FLAME2, int(200 * (1 - progress * 0.5)))
        px(img, fiss_cx - 4, y, c)
        if progress < 0.5:
            px(img, fiss_cx - 5, y, alpha_color(FLAME2, int(120 * (1 - progress))))

    # Flamme laterale droite
    for y in range(16, 40):
        progress = (40 - y) / 24.0
        c = alpha_color(FLAME2, int(180 * (1 - progress * 0.5)))
        px(img, fiss_cx + 4, y, c)
        if progress < 0.4:
            px(img, fiss_cx + 5, y, alpha_color(FLAME2, int(100 * (1 - progress))))

    # Scintillement / pixels isoles autour des flammes (effet particules)
    sparks = [
        (fiss_cx - 7, 20, C_AETHER_FISS, 140),
        (fiss_cx + 7, 18, C_AETHER_DEEP, 120),
        (fiss_cx - 3, 8,  FLAME1_TIP, 100),
        (fiss_cx + 3, 10, FLAME1_TIP, 90),
        (fiss_cx - 6, 30, FLAME2, 80),
        (fiss_cx + 6, 28, FLAME2, 70),
    ]
    for (sx, sy, sc, sa) in sparks:
        px(img, sx, sy, alpha_color(sc, sa))

    out_path = os.path.join(OUT_ENV, "decor_aether_geyser.png")
    save(img, out_path)
    generate_import(out_path)
    return out_path


# ---------------------------------------------------------------------------
# 2e. Debris -- decor_debris.png (32x32)
# ---------------------------------------------------------------------------

def generate_decor_debris():
    """
    4-6 fragments de pierre eparpilles.
    Couleurs #2D2D3E a #4A4A5E. Poussiere #1E1E2A.
    Au moins 1 fragment avec eclat Aether #AA44FF.
    """
    print("\n[Tache 2e] decor_debris.png (32x32) ...")

    F_A = (0x2D, 0x2D, 0x3E, 255)   # fragment sombre
    F_B = (0x3A, 0x3A, 0x4E, 255)   # fragment moyen
    F_C = (0x4A, 0x4A, 0x5E, 255)   # fragment clair
    F_D = (0x22, 0x22, 0x30, 255)   # fragment tres sombre
    DUST = (0x1E, 0x1E, 0x2A, 255)  # poussiere
    AETHER_FRAG = C_NOYAU_VIOL       # eclat Aether sur 1 fragment

    img = new_canvas(32, 32, C_TRANSPARENT)
    draw = ImageDraw.Draw(img)

    # Fragment 1 : gros, coin haut gauche
    pts1 = [(2, 3), (10, 2), (12, 8), (8, 11), (3, 10)]
    draw.polygon(pts1, fill=F_B)
    draw.polygon(pts1, outline=F_D)
    # Reflet
    px(img, 4, 4, F_C)
    px(img, 5, 3, F_C)

    # Fragment 2 : moyen, centre haut
    pts2 = [(14, 4), (21, 3), (23, 9), (17, 10)]
    draw.polygon(pts2, fill=F_A)
    draw.polygon(pts2, outline=F_D)
    # Eclat Aether sur ce fragment (pierre Aether corrompue)
    draw.rectangle([16, 6, 19, 8], fill=AETHER_FRAG)
    px(img, 17, 5, alpha_color(C_NOYAU_GLOW, 200))

    # Fragment 3 : petit, coin haut droit
    pts3 = [(25, 2), (30, 4), (29, 9), (24, 8)]
    draw.polygon(pts3, fill=F_A)
    px(img, 26, 3, F_B)

    # Fragment 4 : moyen plat, milieu bas gauche
    pts4 = [(1, 18), (11, 16), (13, 22), (5, 24)]
    draw.polygon(pts4, fill=F_B)
    draw.polygon(pts4, outline=F_D)
    px(img, 3, 19, F_C)

    # Fragment 5 : petit, centre bas
    pts5 = [(16, 20), (23, 19), (24, 25), (18, 26)]
    draw.polygon(pts5, fill=F_A)
    draw.polygon(pts5, outline=F_D)

    # Fragment 6 : eclat tiny, coin bas droit
    pts6 = [(27, 23), (31, 22), (31, 28), (28, 29)]
    draw.polygon(pts6, fill=F_D)

    # Poussiere : pixels isoles
    dust_pixels = [
        (7, 14), (13, 13), (15, 17), (20, 14), (25, 16),
        (4, 26), (9, 27), (22, 27), (29, 19), (14, 29),
        (0, 20), (31, 10),
    ]
    for (dx, dy) in dust_pixels:
        px(img, dx, dy, DUST)

    out_path = os.path.join(OUT_ENV, "decor_debris.png")
    save(img, out_path)
    generate_import(out_path)
    return out_path


# ===========================================================================
# MAIN
# ===========================================================================

def main():
    print("=" * 60)
    print("generate_splash.py -- Chimera Protocol Phase 3")
    print("=" * 60)

    random.seed(0)  # reproductibilite globale (certaines fonctions ont leur propre seed)

    # Tache 1 : splash
    generate_splash()

    # Tache 2 : sprites decor
    generate_tile_floor_stone()
    generate_tile_wall_stone()
    generate_decor_column()
    generate_decor_aether_geyser()
    generate_decor_debris()

    print("\n[OK] Tous les fichiers ont ete generes.")
    print(f"     Splash  : assets/sprites/ui/splash_art.png")
    print(f"     Decor   : assets/sprites/environment/ (5 fichiers PNG + 5 .import)")
    print()
    print("NOTES pour le developpeur :")
    print("  - splash_art.png : 1280x720, point d'ancrage (0,0) coin haut gauche.")
    print("  - tile_floor_stone.png : 32x32, ancre (0,0) = coin haut gauche de la tile.")
    print("  - tile_wall_stone.png  : 32x32, ancre (0,0) = coin haut gauche de la tile.")
    print("  - decor_column.png     : 32x80, ancre x=16 y=79 (milieu-bas = pied colonne).")
    print("  - decor_aether_geyser.png : 32x48, ancre x=16 y=47 (milieu-bas = sol).")
    print("  - decor_debris.png     : 32x32, ancre (0,0) coin haut gauche.")
    print()
    print("NOTE graphiste -> game-designer :")
    print("  Le geyser Aether (32x48) genere un halo violet visible sur fond sombre.")
    print("  Verifier la lisibilite en combat dense : la silhouette violet #AA44FF")
    print("  est distincte du fond #1A1A2E mais peut se confondre avec les Noyaux")
    print("  d'Aether au sol (#AA44FF meme teinte). Recommandation : ajouter une")
    print("  base de pierre (pixels #3A3A4E) autour de la fissure pour ancrer le")
    print("  geyser visuellement et le distinguer du pickup Noyau.")


if __name__ == "__main__":
    main()
