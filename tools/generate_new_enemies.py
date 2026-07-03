"""
generate_new_enemies.py — Sprites pour les 20 nouveaux ennemis biome (livrable
game-designer, data/enemies_biome_expansion.json / docs/GDD.md §21).

Chaque ennemi reutilise la silhouette de son archetype d'IA (identique a un des
4 ennemis de base) avec une palette/detail distinctifs par biome :
  - straight_chase  -> silhouette "fourrage" (type Essaim de Rouille)
  - erratic_chase   -> silhouette "harceleur" (type Drone Corrompu)
  - ranged_kiter    -> silhouette "tourelle"  (type Sentinelle Corrompue)
  - slow_hunter     -> silhouette "bruiser"   (type Colosse Greffe, en 32x32)

Ombrage pseudo-3D via tools/pseudo3d_lib.py (docs/ART_BRIEF_PSEUDO3D.md) : le
noyau energetique ("core"/"eye" de chaque palette) n'est jamais assombri.

Sortie : assets/sprites/enemies/<id>/<id>_idle_01.png (+ move/death/attack)
       + assets/sprites/enemies/<id>/<id>_frames.tres (animations idle/move/death,
         + attack pour les 5 variantes ranged_kiter)

Lancer : python tools/generate_new_enemies.py
"""
import os
import sys
import math

from PIL import Image

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(SCRIPT_DIR)
sys.path.insert(0, SCRIPT_DIR)

import pseudo3d_lib as p3d
from generate_sprites_v2 import write_spriteframes_tres

S = 32


def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


put = p3d.put
rect = p3d.rect
glow = p3d.glow


def raw_save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "PNG")


def shaded_save(img, path, core_colors, alpha_factor=1.0):
    """Ombrage pseudo-3D + contour lisibilite + ombre portee, puis fade optionnel
    (frames de mort) — toujours shader a pleine opacite AVANT de reduire l'alpha
    (cf. CLAUDE.md, piege deja rencontre sur generate_character_sprites.py)."""
    shaded = p3d.shade_sprite(img, core_colors=core_colors)
    shaded = p3d.add_outline(shaded)
    shaded = p3d.add_cast_shadow(shaded, alpha=90)
    if alpha_factor < 1.0:
        shaded = p3d.fade_alpha(shaded, alpha_factor)
    raw_save(shaded, path)


def _death_scatter(img, d):
    """Dissolution a la mort : efface une part croissante des pixels (seed stable
    pour un rendu deterministe frame a frame)."""
    if not d:
        return
    import random
    rng = random.Random(d * 7 + 1)
    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            if px[x, y][3] > 0 and rng.random() < d * 0.16:
                px[x, y] = (0, 0, 0, 0)


# --------------------------------------------------------------------------- #
# Palette par colorPlaceholder (data/enemies_biome_expansion.json)
# base = matiere (corps, ombree normalement) ; core = noyau energetique
# (jamais assombri) ; eye = point d'attention (jamais assombri)
# --------------------------------------------------------------------------- #
# base = matiere du corps. Volontairement en TON MOYEN (V ~0.42-0.55) : shade_sprite
# assombrit ensuite la face droite (V x0.55) et le bas/contact (V x0.35) — partir
# d'une base trop sombre donnait des sprites "boueux" illisibles (corrige 2026-07-03).
PALETTES = {
    # Sanctuaire — rouille/mecanique
    "rust_amber":    {"base": (0x9A, 0x6C, 0x3E), "core": (0xFF, 0xAA, 0x33), "eye": (0xFF, 0xCC, 0x55)},
    "steel_blue":    {"base": (0x5E, 0x6E, 0x86), "core": (0x55, 0xAA, 0xFF), "eye": (0x99, 0xDD, 0xFF)},
    "rust_copper":   {"base": (0x92, 0x60, 0x38), "core": (0xFF, 0x99, 0x44), "eye": (0xFF, 0xBB, 0x66)},
    "iron_grey":     {"base": (0x6E, 0x70, 0x7E), "core": (0xAA, 0xB0, 0xC0), "eye": (0xDD, 0xE8, 0xF4)},
    # Aether — spectral/energetique
    "violet_glow":   {"base": (0x74, 0x48, 0x9E), "core": (0xAA, 0x44, 0xFF), "eye": (0xE0, 0xB0, 0xFF)},
    "aether_teal":   {"base": (0x2E, 0x74, 0x82), "core": (0x22, 0xE5, 0xCC), "eye": (0xAA, 0xFF, 0xEE)},
    "violet_deep":   {"base": (0x5A, 0x36, 0x86), "core": (0x8A, 0x33, 0xE0), "eye": (0xC8, 0x99, 0xFF)},
    "aether_white":  {"base": (0x74, 0x60, 0x9C), "core": (0xE8, 0xDC, 0xFF), "eye": (0xF4, 0xEE, 0xFF)},
    # Fournaise — igne/fondu
    "ember_orange":  {"base": (0x96, 0x50, 0x2E), "core": (0xFF, 0x66, 0x22), "eye": (0xFF, 0xAA, 0x55)},
    "spark_yellow":  {"base": (0x94, 0x72, 0x30), "core": (0xFF, 0xDD, 0x33), "eye": (0xFF, 0xEE, 0x88)},
    "magma_red":     {"base": (0x88, 0x3A, 0x2A), "core": (0xFF, 0x33, 0x22), "eye": (0xFF, 0x88, 0x55)},
    "molten_black":  {"base": (0x66, 0x42, 0x34), "core": (0xFF, 0x55, 0x22), "eye": (0xFF, 0x99, 0x44)},
    # Givre — gele/cristallin
    "frost_blue":    {"base": (0x4C, 0x72, 0x88), "core": (0x66, 0xCC, 0xFF), "eye": (0xBB, 0xEE, 0xFF)},
    "ice_white":     {"base": (0x62, 0x80, 0x8E), "core": (0xCC, 0xEE, 0xFF), "eye": (0xE8, 0xF8, 0xFF)},
    "glacier_cyan":  {"base": (0x3C, 0x74, 0x84), "core": (0x44, 0xDD, 0xEE), "eye": (0xAA, 0xF5, 0xFF)},
    "deep_ice":      {"base": (0x36, 0x5E, 0x76), "core": (0x55, 0xBB, 0xEE), "eye": (0xBB, 0xE8, 0xFF)},
    # Neon — synthetique/hologramme
    "neon_magenta":  {"base": (0x72, 0x3A, 0x72), "core": (0xFF, 0x44, 0xCC), "eye": (0xFF, 0x99, 0xEE)},
    "neon_glitch":   {"base": (0x44, 0x46, 0x72), "core": (0x66, 0xFF, 0xEE), "eye": (0xFF, 0x66, 0xEE)},
    "neon_cyan":     {"base": (0x30, 0x66, 0x74), "core": (0x44, 0xEE, 0xFF), "eye": (0xAA, 0xFF, 0xFF)},
    "neon_violet":   {"base": (0x50, 0x3A, 0x86), "core": (0xAA, 0x66, 0xFF), "eye": (0xD0, 0xAA, 0xFF)},
}

# id -> (archetype, colorPlaceholder) — cf. data/enemies_biome_expansion.json
ENEMIES = [
    ("sanctuary_marked_walker",   "straight_chase", "rust_amber"),
    ("sanctuary_scout_drone",     "erratic_chase",  "steel_blue"),
    ("sanctuary_walker_turret",   "ranged_kiter",   "rust_copper"),
    ("sanctuary_maintenance_golem","slow_hunter",   "iron_grey"),

    ("aether_shard",              "straight_chase", "violet_glow"),
    ("aether_drifting_wraith",    "erratic_chase",  "aether_teal"),
    ("aether_spectral_watcher",   "ranged_kiter",   "violet_deep"),
    ("aether_golem",              "slow_hunter",    "aether_white"),

    ("cinder_crawler",            "straight_chase", "ember_orange"),
    ("volatile_spark",            "erratic_chase",  "spark_yellow"),
    ("lava_spitter",              "ranged_kiter",   "magma_red"),
    ("magma_colossus",            "slow_hunter",    "molten_black"),

    ("frost_crawler",             "straight_chase", "frost_blue"),
    ("wandering_ice_shard",       "erratic_chase",  "ice_white"),
    ("cryo_marksman",             "ranged_kiter",   "glacier_cyan"),
    ("ice_titan",                 "slow_hunter",    "deep_ice"),

    ("neon_security_drone",       "straight_chase", "neon_magenta"),
    ("holographic_glitch",        "erratic_chase",  "neon_glitch"),
    ("neon_laser_turret",         "ranged_kiter",   "neon_cyan"),
    ("synthetic_golem",           "slow_hunter",    "neon_violet"),
]


# --------------------------------------------------------------------------- #
# Silhouettes par archetype (32x32) — geometrie generique, palette parametree
# --------------------------------------------------------------------------- #

fill_ellipse = p3d.fill_ellipse
fill_diamond = p3d.fill_diamond


def _dark(base):
    d = p3d.shade(base, "shadow")
    return (d[0], d[1], d[2], 255)


def draw_forager(img, pal, phase=0, move_phase=None, death_frame=None):
    """straight_chase — rampant a carapace + pattes (lit comme une creature,
    pas comme une carte). Vue 3/4 : 'avant' vers le bas."""
    cx, cy = 16, 16
    base = pal["base"]; core = pal["core"]; eye = pal["eye"]
    B = (base[0], base[1], base[2], 255)
    SG = _dark(base)
    legph = move_phase if move_phase is not None else phase
    # 6 pattes qui depassent la carapace (animees en opposition)
    for i, (lx, ly) in enumerate([(-10, -1), (-11, 3), (-9, 6), (10, -1), (11, 3), (9, 6)]):
        off = 1 if (i + legph) % 2 else 0
        x = cx + lx; y = cy + ly + off
        rect(img, x - 1, y, x + 1, y + 2, B)
    # carapace bombee
    fill_ellipse(img, cx, cy, 8, 6, B)
    # tete a l'avant (bas)
    fill_ellipse(img, cx, cy + 5, 4, 3, B)
    # segmentation dorsale (detail interne sombre)
    rect(img, cx - 6, cy - 2, cx + 6, cy - 2, SG)
    rect(img, cx - 7, cy + 1, cx + 7, cy + 1, SG)
    # noyau energetique dorsal (jamais assombri)
    glow(img, cx, cy - 1, 4, core, 0.55)
    rect(img, cx - 1, cy - 2, cx, cy, (core[0], core[1], core[2], 255))
    # yeux sur la tete
    put(img, cx - 2, cy + 5, (eye[0], eye[1], eye[2], 255))
    put(img, cx + 2, cy + 5, (eye[0], eye[1], eye[2], 255))
    if death_frame is not None:
        _death_scatter(img, death_frame)


def draw_harasser(img, pal, phase=0, death_frame=None):
    """erratic_chase — drone volant : corps losange + ailes fuyantes + oeil vif."""
    cx, cy = 16, 15
    base = pal["base"]; core = pal["core"]; eye = pal["eye"]
    B = (base[0], base[1], base[2], 255)
    wob = [-1, 0, 1, 0][phase % 4]
    cx += wob
    # ailes en fleche
    for s in (-1, 1):
        rect(img, cx + s * 6, cy - 1, cx + s * 11, cy, B)
        rect(img, cx + s * 7, cy + 1, cx + s * 10, cy + 2, B)
    # corps losange
    fill_diamond(img, cx, cy, 6, 7, B)
    # oeil / noyau (jamais assombri)
    glow(img, cx, cy, 4, core, 0.65)
    fill_ellipse(img, cx, cy, 2, 2, (eye[0], eye[1], eye[2], 255))
    # micro-reacteurs sous le corps
    put(img, cx - 3, cy + 6, (core[0], core[1], core[2], 210))
    put(img, cx + 3, cy + 6, (core[0], core[1], core[2], 210))
    if death_frame is not None:
        _death_scatter(img, death_frame)


def draw_turret(img, pal, phase=0, attack=False, death_frame=None):
    """ranged_kiter — sentinelle sur trepied : dome + fente lumineuse (fine, pas
    un grand ecran) + canon lateral."""
    cx, cy = 16, 14
    base = pal["base"]; core = pal["core"]; eye = pal["eye"]
    B = (base[0], base[1], base[2], 255)
    bob = [0, 1][phase % 2]
    yb = cy + bob
    # trepied
    for lx in (-6, 0, 6):
        rect(img, cx + lx - 1, yb + 5, cx + lx + 1, yb + 12, B)
    # embase
    fill_ellipse(img, cx, yb + 5, 7, 3, B)
    # corps
    fill_ellipse(img, cx, yb + 1, 6, 5, B)
    # dome (tete)
    fill_ellipse(img, cx, yb - 3, 7, 5, B)
    # canon lateral
    barrel_len = 8 if attack else 6
    rect(img, cx + 5, yb - 4, cx + 5 + barrel_len, yb - 2, B)
    # fente/oeil (mince) + noyau
    rect(img, cx - 3, yb - 4, cx + 3, yb - 3, (eye[0], eye[1], eye[2], 255))
    glow(img, cx, yb - 3, 4, core, 0.8 if attack else 0.5)
    if attack:
        mx = cx + 5 + barrel_len
        put(img, mx, yb - 3, (255, 255, 255, 235))
        glow(img, mx, yb - 3, 3, core, 0.7)
    if death_frame is not None:
        _death_scatter(img, death_frame)


def draw_bruiser(img, pal, phase=0, death_frame=None):
    """slow_hunter — golem trapu (epaules bombees + tete + noyau poitrine)."""
    cx = 16
    top = 6 + (1 if phase % 2 else 0)
    base = pal["base"]; core = pal["core"]; eye = pal["eye"]
    D = (base[0], base[1], base[2], 255)
    SG = _dark(base)
    # jambes courtes
    rect(img, cx - 6, top + 18, cx - 2, top + 24, D)
    rect(img, cx + 2, top + 18, cx + 6, top + 24, D)
    # torse large
    rect(img, cx - 8, top + 8, cx + 8, top + 19, D)
    # epaules bombees
    fill_ellipse(img, cx - 8, top + 9, 4, 4, D)
    fill_ellipse(img, cx + 8, top + 9, 4, 4, D)
    # tete
    fill_ellipse(img, cx, top + 3, 5, 4, D)
    # separation torse (detail)
    rect(img, cx - 8, top + 14, cx + 8, top + 14, SG)
    # noyau energetique poitrine (jamais assombri)
    glow(img, cx, top + 12, 6, core, 0.55)
    rect(img, cx - 2, top + 10, cx + 1, top + 14, (core[0], core[1], core[2], 255))
    # yeux
    put(img, cx - 2, top + 3, (eye[0], eye[1], eye[2], 255))
    put(img, cx + 2, top + 3, (eye[0], eye[1], eye[2], 255))
    if death_frame is not None:
        _death_scatter(img, death_frame)


DRAW_FN = {
    "straight_chase": draw_forager,
    "erratic_chase": draw_harasser,
    "ranged_kiter": draw_turret,
    "slow_hunter": draw_bruiser,
}


# --------------------------------------------------------------------------- #
# Generation des frames par ennemi
# --------------------------------------------------------------------------- #
def gen_enemy(eid, archetype, color_key):
    pal = PALETTES[color_key]
    core_colors = [pal["core"], pal["eye"]]
    out_dir = os.path.join(ROOT, "assets", "sprites", "enemies", eid)

    animations = []

    if archetype == "straight_chase":
        for i in range(3):
            img = canvas(); draw_forager(img, pal, phase=i)
            shaded_save(img, os.path.join(out_dir, f"{eid}_idle_{i+1:02d}.png"), core_colors)
        for i in range(4):
            img = canvas(); draw_forager(img, pal, move_phase=i)
            shaded_save(img, os.path.join(out_dir, f"{eid}_move_{i+1:02d}.png"), core_colors)
        for i in range(5):
            img = canvas(); draw_forager(img, pal, death_frame=i * 2)
            shaded_save(img, os.path.join(out_dir, f"{eid}_death_{i+1:02d}.png"), core_colors,
                        alpha_factor=max(0.15, 1.0 - i / 4.5))
        animations = [
            {"name": "idle", "frames": 3, "speed": 6.0, "loop": True},
            {"name": "move", "frames": 4, "speed": 10.0, "loop": True},
            {"name": "death", "frames": 5, "speed": 9.0, "loop": False},
        ]

    elif archetype == "erratic_chase":
        for i in range(3):
            img = canvas(); draw_harasser(img, pal, phase=i)
            shaded_save(img, os.path.join(out_dir, f"{eid}_idle_{i+1:02d}.png"), core_colors)
        for i in range(4):
            img = canvas(); draw_harasser(img, pal, phase=i)
            shaded_save(img, os.path.join(out_dir, f"{eid}_move_{i+1:02d}.png"), core_colors)
        for i in range(5):
            img = canvas(); draw_harasser(img, pal, death_frame=i * 2)
            shaded_save(img, os.path.join(out_dir, f"{eid}_death_{i+1:02d}.png"), core_colors,
                        alpha_factor=max(0.15, 1.0 - i / 4.5))
        animations = [
            {"name": "idle", "frames": 3, "speed": 8.0, "loop": True},
            {"name": "move", "frames": 4, "speed": 14.0, "loop": True},
            {"name": "death", "frames": 5, "speed": 10.0, "loop": False},
        ]

    elif archetype == "ranged_kiter":
        for i in range(3):
            img = canvas(); draw_turret(img, pal, phase=i)
            shaded_save(img, os.path.join(out_dir, f"{eid}_idle_{i+1:02d}.png"), core_colors)
        for i in range(4):
            img = canvas(); draw_turret(img, pal, phase=i)
            shaded_save(img, os.path.join(out_dir, f"{eid}_move_{i+1:02d}.png"), core_colors)
        # attack : 1 frame (canon charge) — evite un Play("attack") sur anim absente
        img = canvas(); draw_turret(img, pal, phase=0, attack=True)
        shaded_save(img, os.path.join(out_dir, f"{eid}_attack_01.png"), core_colors)
        for i in range(5):
            img = canvas(); draw_turret(img, pal, death_frame=i * 2)
            shaded_save(img, os.path.join(out_dir, f"{eid}_death_{i+1:02d}.png"), core_colors,
                        alpha_factor=max(0.15, 1.0 - i / 4.5))
        animations = [
            {"name": "idle", "frames": 3, "speed": 5.0, "loop": True},
            {"name": "move", "frames": 4, "speed": 8.0, "loop": True},
            {"name": "attack", "frames": 1, "speed": 4.0, "loop": False},
            {"name": "death", "frames": 5, "speed": 9.0, "loop": False},
        ]

    elif archetype == "slow_hunter":
        for i in range(3):
            img = canvas(); draw_bruiser(img, pal, phase=i)
            shaded_save(img, os.path.join(out_dir, f"{eid}_idle_{i+1:02d}.png"), core_colors)
        for i in range(4):
            img = canvas(); draw_bruiser(img, pal, phase=i)
            shaded_save(img, os.path.join(out_dir, f"{eid}_move_{i+1:02d}.png"), core_colors)
        for i in range(5):
            img = canvas(); draw_bruiser(img, pal, death_frame=i * 2)
            shaded_save(img, os.path.join(out_dir, f"{eid}_death_{i+1:02d}.png"), core_colors,
                        alpha_factor=max(0.15, 1.0 - i / 4.5))
        animations = [
            {"name": "idle", "frames": 3, "speed": 4.0, "loop": True},
            {"name": "move", "frames": 4, "speed": 6.0, "loop": True},
            {"name": "death", "frames": 5, "speed": 7.0, "loop": False},
        ]

    write_spriteframes_tres(
        path=os.path.join(out_dir, f"{eid}_frames.tres"),
        sprite_prefix=eid,
        res_path_prefix=f"res://assets/sprites/enemies/{eid}",
        animations=animations,
    )
    print(f"  {eid} ({archetype}, {color_key}) : {sum(a['frames'] for a in animations)} frames")


def main():
    print("=== generate_new_enemies.py — 20 ennemis biome ===\n")
    for eid, archetype, color_key in ENEMIES:
        gen_enemy(eid, archetype, color_key)
    print("\nTermine : 20 ennemis generes.")


if __name__ == "__main__":
    main()
