"""
Genere une TUILE TRANSPARENTE reutilisable pour le fond parallax "sous l'arene"
(zone visible au-dela des murs, quand la camera suit le joueur pres d'un bord).

Contrairement aux tuiles de sol/mur (opaques, tintees par biome au chargement),
cette tuile est un simple masque d'alpha (RGB blanc, alpha variable) : le shader
assets/shaders/backdrop_parallax.gdshader la reechantillonne en boucle (mod UV)
et la teinte avec l'accent du biome courant a l'execution -> une seule tuile
suffit pour tous les biomes.

Motif : grille de circuit distante (lignes fines + points aux intersections),
concue pour etre parfaitement raccordable (64x64, motif au pas de 16px).

Sortie : assets/sprites/tileset/backdrop_tile.png
Lancer : python tools/generate_backdrop_tile.py
"""
import os
from PIL import Image

S = 64
STEP = 16
ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "assets", "sprites", "tileset", "backdrop_tile.png")

WHITE_LINE = (255, 255, 255, 20)
WHITE_DOT  = (255, 255, 255, 55)
WHITE_DOT_CORE = (255, 255, 255, 95)


def main():
    img = Image.new("RGBA", (S, S), (255, 255, 255, 0))
    px = img.load()

    # Lignes de grille fines (pas de 16px) — motif periodique, donc raccord parfait.
    for y in range(S):
        for x in range(S):
            if x % STEP == 0 or y % STEP == 0:
                px[x, y] = WHITE_LINE

    # Points plus marques aux intersections (croix 3x3).
    for gy in range(0, S, STEP):
        for gx in range(0, S, STEP):
            px[gx, gy] = WHITE_DOT_CORE
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = (gx + dx) % S, (gy + dy) % S
                px[nx, ny] = WHITE_DOT

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT, "PNG")
    print(f"Tuile de fond -> {OUT} ({S}x{S}, motif {STEP}px)")


if __name__ == "__main__":
    main()
