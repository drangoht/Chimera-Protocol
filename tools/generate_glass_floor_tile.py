"""
Genere une tuile de sol "vitre" (32x32, transparente) : cadre metallique + rivets
aux coins + reflet diagonal, centre quasi transparent pour laisser voir le fond
parallax (BiomeAtmosphere.BuildBackdropVoid) a travers des trous du sol.

Tuile neutre (grise) : tintee au runtime comme les autres tuiles de sol via
GroundRenderer (Modulate = biome.FloorTint), donc reutilisable sur tous les biomes.

Sortie : assets/sprites/tileset/tile_floor_glass.png
Lancer : python tools/generate_glass_floor_tile.py
"""
import os
from PIL import Image, ImageDraw

S = 32
ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "assets", "sprites", "tileset", "tile_floor_glass.png")

FRAME       = (0x4A, 0x4A, 0x52, 235)
FRAME_DARK  = (0x26, 0x26, 0x2C, 235)
RIVET       = (0x8A, 0x8C, 0x96, 255)
GLASS_TINT  = (0x30, 0x50, 0x60, 26)   # tres legere teinte vitree au centre
STREAK      = (255, 255, 255, 70)
STREAK_SOFT = (255, 255, 255, 34)


def main():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Centre quasi transparent (laisse voir le fond au travers), leger voile vitre.
    d.rectangle([2, 2, S - 3, S - 3], fill=GLASS_TINT)

    # Cadre metallique (2px), coin interieur plus sombre pour donner du relief.
    d.rectangle([0, 0, S - 1, S - 1], outline=FRAME, width=2)
    d.rectangle([1, 1, S - 2, S - 2], outline=FRAME_DARK, width=1)

    # Rivets aux 4 coins.
    for cx, cy in ((2, 2), (S - 3, 2), (2, S - 3), (S - 3, S - 3)):
        d.point((cx, cy), fill=RIVET)

    # Reflet diagonal (verre).
    for i in range(4, S - 4):
        img.putpixel((i, S - 6 - i // 2 if S - 6 - i // 2 >= 0 else 0), STREAK)
    for i in range(8, S - 8):
        y = S - 10 - i // 2
        if 0 <= y < S:
            img.putpixel((i, y), STREAK_SOFT)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT, "PNG")
    print(f"Tuile vitre -> {OUT} ({S}x{S})")


if __name__ == "__main__":
    main()
