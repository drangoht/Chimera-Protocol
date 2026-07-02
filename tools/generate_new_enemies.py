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
    """Ombrage pseudo-3D + ombre portee, puis fade optionnel (frames de mort) —
    toujours shader a pleine opacite AVANT de reduire l'alpha (cf. CLAUDE.md,
    piege deja rencontre sur generate_character_sprites.py)."""
    shaded = p3d.shade_sprite(img, core_colors=core_colors)
    shaded = p3d.add_cast_shadow(shaded, alpha=90)
    if alpha_factor < 1.0:
        shaded = p3d.fade_alpha(shaded, alpha_factor)
    raw_save(shaded, path)


# --------------------------------------------------------------------------- #
# Palette par colorPlaceholder (data/enemies_biome_expansion.json)
# base = matiere (corps, ombree normalement) ; core = noyau energetique
# (jamais assombri) ; eye = point d'attention (jamais assombri)
# --------------------------------------------------------------------------- #
PALETTES = {
    # Sanctuaire — rouille/mecanique
    "rust_amber":    {"base": (0x6A, 0x4A, 0x2A), "core": (0xFF, 0xAA, 0x33), "eye": (0xFF, 0xCC, 0x55)},
    "steel_blue":    {"base": (0x3A, 0x44, 0x55), "core": (0x55, 0xAA, 0xFF), "eye": (0x55, 0xCC, 0xFF)},
    "rust_copper":   {"base": (0x5A, 0x3A, 0x22), "core": (0xFF, 0x99, 0x44), "eye": (0xFF, 0xBB, 0x66)},
    "iron_grey":     {"base": (0x4A, 0x4A, 0x52), "core": (0xAA, 0xB0, 0xC0), "eye": (0xCC, 0xDD, 0xEE)},
    # Aether — spectral/energetique
    "violet_glow":   {"base": (0x4A, 0x2A, 0x66), "core": (0xAA, 0x44, 0xFF), "eye": (0xE0, 0xB0, 0xFF)},
    "aether_teal":   {"base": (0x1A, 0x4A, 0x55), "core": (0x22, 0xE5, 0xCC), "eye": (0xAA, 0xFF, 0xEE)},
    "violet_deep":   {"base": (0x35, 0x1A, 0x50), "core": (0x8A, 0x33, 0xE0), "eye": (0xC8, 0x99, 0xFF)},
    "aether_white":  {"base": (0x4A, 0x3A, 0x66), "core": (0xE8, 0xDC, 0xFF), "eye": (0xF4, 0xEE, 0xFF)},
    # Fournaise — igne/fondu
    "ember_orange":  {"base": (0x5A, 0x2A, 0x18), "core": (0xFF, 0x66, 0x22), "eye": (0xFF, 0xAA, 0x55)},
    "spark_yellow":  {"base": (0x5A, 0x44, 0x18), "core": (0xFF, 0xDD, 0x33), "eye": (0xFF, 0xEE, 0x88)},
    "magma_red":     {"base": (0x4A, 0x1A, 0x14), "core": (0xFF, 0x33, 0x22), "eye": (0xFF, 0x88, 0x55)},
    "molten_black":  {"base": (0x2A, 0x1A, 0x18), "core": (0xFF, 0x55, 0x22), "eye": (0xFF, 0x99, 0x44)},
    # Givre — gele/cristallin
    "frost_blue":    {"base": (0x2A, 0x44, 0x55), "core": (0x66, 0xCC, 0xFF), "eye": (0xBB, 0xEE, 0xFF)},
    "ice_white":     {"base": (0x3A, 0x50, 0x5A), "core": (0xCC, 0xEE, 0xFF), "eye": (0xE8, 0xF8, 0xFF)},
    "glacier_cyan":  {"base": (0x22, 0x4A, 0x55), "core": (0x44, 0xDD, 0xEE), "eye": (0xAA, 0xF5, 0xFF)},
    "deep_ice":      {"base": (0x1A, 0x33, 0x44), "core": (0x55, 0xBB, 0xEE), "eye": (0xBB, 0xE8, 0xFF)},
    # Neon — synthetique/hologramme
    "neon_magenta":  {"base": (0x3A, 0x1A, 0x3A), "core": (0xFF, 0x44, 0xCC), "eye": (0xFF, 0x99, 0xEE)},
    "neon_glitch":   {"base": (0x2A, 0x2A, 0x44), "core": (0x66, 0xFF, 0xEE), "eye": (0xFF, 0x66, 0xEE)},
    "neon_cyan":     {"base": (0x1A, 0x3A, 0x44), "core": (0x44, 0xEE, 0xFF), "eye": (0xAA, 0xFF, 0xFF)},
    "neon_violet":   {"base": (0x2A, 0x1A, 0x44), "core": (0xAA, 0x66, 0xFF), "eye": (0xD0, 0xAA, 0xFF)},
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

def draw_forager(img, pal, phase=0, move_phase=None, death_frame=None, squash=0):
    """straight_chase — masse basse arrondie (cf. draw_rust_swarm_base)."""
    cx, cy = 16, 18
    base = pal["base"]; core = pal["core"]
    osc = [-1, 0, 1][phase % 3] if move_phase is None else 0
    stretch = [0, 1, 0, -1][move_phase % 4] * 2 if move_phase is not None else 0

    body_w = 9 + abs(stretch) - squash
    body_h = 6 - abs(stretch) // 2 - squash // 2
    x0 = cx - body_w + osc; x1 = cx + body_w + osc
    y0 = cy - body_h; y1 = cy + body_h

    rect(img, x0, y0, x1, y1, (base[0], base[1], base[2], 255))
    # arrondi des coins (retire les coins carres)
    for c in [(x0, y0), (x1, y0), (x0, y1), (x1, y1)]:
        put(img, c[0], c[1], (0, 0, 0, 0))
    # noyau energetique (jamais assombri)
    glow(img, cx + osc, cy - 1, 5, core, 0.45)
    put(img, cx + osc, cy - 1, (core[0], core[1], core[2], 255))
    put(img, cx + osc - 1, cy - 1, (core[0], core[1], core[2], 255))
    if death_frame is not None:
        d = death_frame
        for yy in range(y0, y1 + 1, 2):
            rect(img, x0 + d, yy, x1 - d, yy, (0, 0, 0, 0))


def draw_harasser(img, pal, phase=0, death_frame=None):
    """erratic_chase — silhouette agile en losange, oeil vif (cf. drone)."""
    cx, cy = 16, 16
    base = pal["base"]; core = pal["core"]; eye = pal["eye"]
    wobble = [-1, 0, 1, 0][phase % 4]
    pts_r = 9
    # corps en losange (hexagone simplifie)
    for dy in range(-6, 7):
        half = max(0, pts_r - abs(dy) * 2)
        rect(img, cx - half + wobble, cy + dy, cx + half + wobble, cy + dy, (base[0], base[1], base[2], 255))
    # ailerons lateraux (lames)
    rect(img, cx - pts_r - 3 + wobble, cy - 1, cx - pts_r + wobble, cy + 1, (base[0], base[1], base[2], 255))
    rect(img, cx + pts_r + wobble, cy - 1, cx + pts_r + 3 + wobble, cy + 1, (base[0], base[1], base[2], 255))
    # oeil / noyau (jamais assombri)
    glow(img, cx + wobble, cy, 4, core, 0.4)
    put(img, cx + wobble, cy, (eye[0], eye[1], eye[2], 255))
    put(img, cx + wobble + 1, cy, (eye[0], eye[1], eye[2], 255))
    if death_frame is not None:
        d = death_frame
        for yy in range(cy - 6, cy + 7, 2):
            rect(img, cx - 9 + d, yy, cx + 9 - d, yy, (0, 0, 0, 0))


def draw_turret(img, pal, phase=0, attack=False, death_frame=None):
    """ranged_kiter — chassis boite + visiere/canon (cf. sentinel)."""
    cx, cy = 16, 17
    base = pal["base"]; core = pal["core"]; eye = pal["eye"]
    bob = [0, 1][phase % 2]
    # base/chassis
    rect(img, cx - 8, cy - 6 + bob, cx + 8, cy + 8 + bob, (base[0], base[1], base[2], 255))
    # pieds trapus
    rect(img, cx - 7, cy + 8 + bob, cx - 3, cy + 11 + bob, (base[0], base[1], base[2], 255))
    rect(img, cx + 3, cy + 8 + bob, cx + 7, cy + 11 + bob, (base[0], base[1], base[2], 255))
    # visiere / canon
    barrel_len = 6 if not attack else 8
    rect(img, cx + 8, cy - 1 + bob, cx + 8 + barrel_len, cy + 1 + bob, (base[0], base[1], base[2], 255))
    # noyau/visiere (jamais assombri)
    glow(img, cx, cy - 1 + bob, 5, core, 0.4 if not attack else 0.7)
    rect(img, cx - 4, cy - 3 + bob, cx + 4, cy + 1 + bob, (eye[0], eye[1], eye[2], 255))
    if attack:
        put(img, cx + 8 + barrel_len, cy + bob, (255, 255, 255, 230))
        glow(img, cx + 8 + barrel_len, cy + bob, 3, core, 0.6)
    if death_frame is not None:
        d = death_frame
        for yy in range(cy - 6, cy + 9, 2):
            rect(img, cx - 8 + d, yy + bob, cx + 8 - d, yy + bob, (0, 0, 0, 0))


def draw_bruiser(img, pal, phase=0, death_frame=None):
    """slow_hunter — golem trapu 32x32 (proportions Colosse, echelle reduite)."""
    cx = 16
    top = 6 + (1 if phase % 2 else 0)
    base = pal["base"]; core = pal["core"]
    D = (base[0], base[1], base[2], 255)
    # jambes courtes
    rect(img, cx - 6, top + 18, cx - 2, top + 24, D)
    rect(img, cx + 2, top + 18, cx + 6, top + 24, D)
    # torse large
    rect(img, cx - 9, top + 7, cx + 9, top + 19, D)
    # epaules
    rect(img, cx - 11, top + 6, cx - 7, top + 12, D)
    rect(img, cx + 7, top + 6, cx + 11, top + 12, D)
    # tete trapue
    rect(img, cx - 5, top, cx + 5, top + 7, D)
    # noyau energetique poitrine (jamais assombri)
    glow(img, cx, top + 13, 6, core, 0.5)
    rect(img, cx - 2, top + 11, cx + 1, top + 15, (core[0], core[1], core[2], 255))
    put(img, cx - 1, top + 12, (255, 255, 255, 180))
    if death_frame is not None:
        d = death_frame
        for yy in range(top, top + 24, 2):
            rect(img, cx - 9 + d, yy, cx + 9 - d, yy, (0, 0, 0, 0))


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
