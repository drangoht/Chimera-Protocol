"""
generate_vfx_sprites_polish.py
Genere les 3 sprites VFX pour le polish visuel de Chimera Protocol.

Sprites produits :
  1. vfx_particle_trail_player.png  — 3x3 px, blanc pur #FFFFFF, RGBA
  2. vfx_white_circle.png           — 64x64 px, cercle blanc anti-aliasd, fond transparent, RGBA
  3. vfx_particle_xp_orb_light.png  — 32x32 px, gradient radial jaune-vert XP, fond transparent, RGBA

Usage :
  python.exe tools/generate_vfx_sprites_polish.py
"""

import math
import struct
import os
import zlib

# ---------------------------------------------------------------------------
# Helpers PNG natifs (pas besoin de Pillow si absent, mais on l'utilise ici)
# ---------------------------------------------------------------------------

try:
    from PIL import Image
    PILLOW_AVAILABLE = True
except ImportError:
    PILLOW_AVAILABLE = False

OUTPUT_DIR = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "assets", "sprites", "vfx"
)

os.makedirs(OUTPUT_DIR, exist_ok=True)


# ---------------------------------------------------------------------------
# Fallback PNG pur Python (si Pillow absent)
# ---------------------------------------------------------------------------

def _png_chunk(chunk_type: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(chunk_type + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + chunk_type + data + struct.pack(">I", crc)


def _write_png_rgba(path: str, width: int, height: int, pixels: list):
    """Ecrit un PNG RGBA a partir d'une liste de (R,G,B,A) par pixel (row-major)."""
    raw = b""
    for y in range(height):
        raw += b"\x00"  # filter type None
        for x in range(width):
            r, g, b, a = pixels[y * width + x]
            raw += struct.pack("BBBB", r, g, b, a)
    compressed = zlib.compress(raw, 9)

    header = b"\x89PNG\r\n\x1a\n"
    ihdr_data = struct.pack(">IIBBBBB", width, height, 8, 2 | 4, 0, 0, 0)
    # Bit depth=8, color type=6 (RGBA), compression=0, filter=0, interlace=0
    chunks = (
        header
        + _png_chunk(b"IHDR", ihdr_data)
        + _png_chunk(b"IDAT", compressed)
        + _png_chunk(b"IEND", b"")
    )
    with open(path, "wb") as f:
        f.write(chunks)


# ---------------------------------------------------------------------------
# 1. vfx_particle_trail_player.png — 3x3 blanc pur
# ---------------------------------------------------------------------------

def make_trail_player(path: str):
    width, height = 3, 3
    if PILLOW_AVAILABLE:
        img = Image.new("RGBA", (width, height), (255, 255, 255, 255))
        img.save(path)
    else:
        pixels = [(255, 255, 255, 255)] * (width * height)
        _write_png_rgba(path, width, height, pixels)
    return width, height


# ---------------------------------------------------------------------------
# 2. vfx_white_circle.png — 64x64, cercle blanc anti-aliasd
# ---------------------------------------------------------------------------

def make_white_circle(path: str):
    width, height = 64, 64
    cx, cy = width / 2.0, height / 2.0
    radius = 30.0          # rayon interieur plein
    feather = 1.5          # zone de feathering (anti-aliasing)

    if PILLOW_AVAILABLE:
        img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        px = img.load()
        for y in range(height):
            for x in range(width):
                dist = math.sqrt((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2)
                if dist <= radius - feather:
                    alpha = 255
                elif dist <= radius + feather:
                    t = (dist - (radius - feather)) / (feather * 2.0)
                    alpha = int((1.0 - t) * 255)
                else:
                    alpha = 0
                px[x, y] = (255, 255, 255, alpha)
        img.save(path)
    else:
        pixels = []
        for y in range(height):
            for x in range(width):
                dist = math.sqrt((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2)
                if dist <= radius - feather:
                    alpha = 255
                elif dist <= radius + feather:
                    t = (dist - (radius - feather)) / (feather * 2.0)
                    alpha = int((1.0 - t) * 255)
                else:
                    alpha = 0
                pixels.append((255, 255, 255, alpha))
        _write_png_rgba(path, width, height, pixels)
    return width, height


# ---------------------------------------------------------------------------
# 3. vfx_particle_xp_orb_light.png — 32x32, gradient radial jaune-vert XP
# ---------------------------------------------------------------------------

def make_xp_orb_light(path: str):
    width, height = 32, 32
    cx, cy = 16.0, 16.0
    radius = 15.5          # rayon exterieur du halo

    # Couleur centre : #AAFF44 = (170, 255, 68)
    R_CENTER, G_CENTER, B_CENTER = 170, 255, 68

    if PILLOW_AVAILABLE:
        img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        px = img.load()
        for y in range(height):
            for x in range(width):
                dist = math.sqrt((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2)
                # t = 0 au centre (opaque), t = 1 au bord (transparent)
                t = min(1.0, dist / radius)
                # Courbe exponentielle pour un halo doux : alpha decroit vite vers bord
                alpha_f = max(0.0, 1.0 - t ** 1.5)
                alpha = int(alpha_f * 255)
                r = R_CENTER
                g = G_CENTER
                b = B_CENTER
                px[x, y] = (r, g, b, alpha)
        img.save(path)
    else:
        pixels = []
        for y in range(height):
            for x in range(width):
                dist = math.sqrt((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2)
                t = min(1.0, dist / radius)
                alpha_f = max(0.0, 1.0 - t ** 1.5)
                alpha = int(alpha_f * 255)
                pixels.append((R_CENTER, G_CENTER, B_CENTER, alpha))
        _write_png_rgba(path, width, height, pixels)
    return width, height


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    print(f"Repertoire de sortie : {OUTPUT_DIR}")
    print(f"Pillow disponible    : {PILLOW_AVAILABLE}")
    print()

    specs = [
        (
            "vfx_particle_trail_player.png",
            make_trail_player,
            "3x3 px, blanc pur #FFFFFF, RGBA — trail joueur",
        ),
        (
            "vfx_white_circle.png",
            make_white_circle,
            "64x64 px, cercle blanc anti-aliasd, fond transparent — shockwave ring Colosse",
        ),
        (
            "vfx_particle_xp_orb_light.png",
            make_xp_orb_light,
            "32x32 px, gradient radial #AAFF44, fond transparent — PointLight2D orbes XP",
        ),
    ]

    for filename, fn, description in specs:
        path = os.path.join(OUTPUT_DIR, filename)
        w, h = fn(path)
        size_kb = os.path.getsize(path) / 1024.0
        print(f"[OK] {filename}")
        print(f"     Taille : {w}x{h} px | Fichier : {size_kb:.1f} Ko")
        print(f"     Usage  : {description}")
        print()

    print("Generation terminee. 3 sprites VFX produits.")
    print()
    print("Integration Godot :")
    print("  - vfx_particle_trail_player.png -> ParticleProcessMaterial.Texture du GPUParticles2D trail joueur")
    print("  - vfx_white_circle.png          -> ShaderMaterial Texture du ShockwaveRing (mort Colosse)")
    print("  - vfx_particle_xp_orb_light.png -> Texture du PointLight2D sur chaque XpOrb")
    print()
    print("Specifications pour le developpeur :")
    print("  Tous les sprites : PNG RGBA, texture_filter = Nearest (projet Godot global)")
    print("  Point d ancrage  : centre (0.5, 0.5) pour les 3 sprites")
    print("  Frames           : 1 frame unique (pas d animation — effets geres en code)")


if __name__ == "__main__":
    main()
