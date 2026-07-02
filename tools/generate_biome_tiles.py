"""
Genere des TUILES DEDIEES par biome (32x32, style maison cohérent avec
generate_sprites.py) : couleurs et motifs baked-in dans le PNG, pour remplacer
le simple tintage runtime.

4 biomes (le Sanctuaire Rouillé garde les tuiles d'origine) :
  - aether    : Friche d'Aether (violet corrompu, veines magenta)
  - fournaise : Fournaise (rouille-orange, fissures en fusion)
  - givre     : Givre Cryogénique (bleu-glace, givre clair)
  - neon      : Secteur Néon (base sombre, grilles néon magenta + cyan)

Sortie : assets/sprites/tileset/biomes/<biome>/floor_01..03.png, wall_01..02.png
Lancer : python tools/generate_biome_tiles.py
"""
import os, sys, random
from PIL import Image, ImageDraw

S = 32
ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pseudo3d_lib as _p3d

def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 255))

def rect(img, x0, y0, x1, y1, c):
    ImageDraw.Draw(img).rectangle([x0, y0, x1, y1], fill=c)

def px(img, x, y, c):
    if 0 <= x < S and 0 <= y < S:
        img.putpixel((x, y), c if len(c) == 4 else (c[0], c[1], c[2], 255))

def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "PNG")

# Palettes : (base, mid, dark, accent, accent_clair)
PALETTES = {
    "aether":    ((34, 26, 52),  (54, 42, 80),  (22, 16, 36),  (150, 90, 240), (205, 160, 255)),
    "fournaise": ((44, 26, 18),  (74, 42, 26),  (28, 16, 12),  (255, 120, 40), (255, 195, 120)),
    "givre":     ((30, 44, 52),  (48, 66, 78),  (20, 30, 38),  (150, 220, 235),(225, 248, 255)),
    # neon : Secteur Néon — base quasi noire bleutée, grilles/néons magenta + cyan électriques
    "neon":      ((8, 10, 20),   (16, 20, 38),  (5, 6, 14),    (255, 60, 210), (120, 245, 255)),
}

def gen_biome(name):
    base, mid, dark, acc, accl = PALETTES[name]
    out = os.path.join(ROOT, "assets", "sprites", "tileset", "biomes", name)
    rng = random.Random(hash(name) & 0xffff)

    # Accents de biome (energie vivante) jamais concernes par l'ombrage
    # volumetrique/gradient (docs/ART_BRIEF_PSEUDO3D.md §6).
    accent_colors = [acc[:3], accl[:3]]

    def _tile_shade(img):
        return _p3d.shade_tile(img, amplitude=0.13, exclude_colors=accent_colors)

    # floor_01 : base + moucheture subtile
    img = canvas(); rect(img, 0, 0, 31, 31, base)
    for _ in range(14):
        x, y = rng.randint(0, 31), rng.randint(0, 31)
        px(img, x, y, mid)
    save(_tile_shade(img), os.path.join(out, "floor_01.png"))

    # floor_02 : joints en grille (néon = lignes lumineuses sur base sombre)
    neon = (name == "neon")
    img = canvas(); rect(img, 0, 0, 31, 31, base if neon else mid)
    grid_c = acc if neon else dark
    for xi in range(0, 32, 8):
        for yi in range(32): px(img, xi, yi, grid_c)
    for yi in range(0, 32, 8):
        for xi in range(32): px(img, xi, yi, grid_c)
    if neon:  # ponctuation cyan aux intersections pour un look circuit
        for xi in range(0, 32, 8):
            for yi in range(0, 32, 8): px(img, xi, yi, accl)
    save(_tile_shade(img), os.path.join(out, "floor_02.png"))

    # floor_03 : veine/fissure d'accent diagonale + lueur
    img = canvas(); rect(img, 0, 0, 31, 31, base)
    for i in range(6, 26):
        jit = rng.randint(-1, 1)
        px(img, i, i - 4 + jit, accl)
        px(img, i, i - 5 + jit, (acc[0], acc[1], acc[2], 150))
        px(img, i, i - 3 + jit, (acc[0], acc[1], acc[2], 110))
    save(_tile_shade(img), os.path.join(out, "floor_03.png"))

    # wall_01 : blocs 8x8 + highlight haut
    img = canvas(); rect(img, 0, 0, 31, 31, mid)
    d = ImageDraw.Draw(img)
    for y in range(0, 32, 8):
        for x in range(0, 32, 8):
            d.rectangle([x, y, x+7, y+7], outline=dark)
    rect(img, 0, 0, 31, 1, accl)
    save(_tile_shade(img), os.path.join(out, "wall_01.png"))

    # wall_02 : plaque d'accent (veine d'énergie verticale)
    img = canvas(); rect(img, 0, 0, 31, 31, mid)
    rect(img, 8, 6, 23, 25, dark)
    for i in range(6, 26):
        px(img, 15, i, acc); px(img, 16, i, accl); px(img, 17, i, acc)
    save(_tile_shade(img), os.path.join(out, "wall_02.png"))

    print(f"{name}: 5 tuiles -> {out}")

def main():
    for b in PALETTES:
        gen_biome(b)
    print("Termine.")

if __name__ == "__main__":
    main()
