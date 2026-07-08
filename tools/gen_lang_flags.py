"""Genere 3 petits drapeaux (FR / EN / ES) pour le selecteur de langue du menu.

Sortie : assets/sprites/ui/flag_{fr,en,es}.png (32x20, bordure sombre 1px, texture_filter Nearest).
Style volontairement plat (petits drapeaux d'UI) avec un lisere sombre pour se marier au theme.
"""
import os
from PIL import Image, ImageDraw

W, H = 32, 20
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "assets", "sprites", "ui")

BORDER = (10, 10, 18, 255)


def frame(img):
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W - 1, H - 1], outline=BORDER)
    return img


def flag_fr():
    img = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W // 3, H], fill=(0, 85, 164, 255))          # bleu
    d.rectangle([W // 3, 0, 2 * W // 3, H], fill=(245, 245, 245, 255))  # blanc
    d.rectangle([2 * W // 3, 0, W, H], fill=(239, 65, 53, 255))     # rouge
    return frame(img)


def flag_es():
    img = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W, H], fill=(198, 11, 30, 255))             # rouge
    d.rectangle([0, H // 4, W, 3 * H // 4], fill=(255, 196, 0, 255))  # jaune (bande centrale 2x)
    return frame(img)


def flag_en():
    # Union Jack simplifie (represente l'anglais). Petit format : on garde les 3 croix.
    img = Image.new("RGBA", (W, H), (1, 33, 105, 255))            # champ bleu
    d = ImageDraw.Draw(img)
    white = (245, 245, 245, 255)
    red = (200, 16, 46, 255)
    # Diagonales blanches (St Andrew) puis rouges (St Patrick, plus fines)
    d.line([(0, 0), (W, H)], fill=white, width=5)
    d.line([(W, 0), (0, H)], fill=white, width=5)
    d.line([(0, 0), (W, H)], fill=red, width=2)
    d.line([(W, 0), (0, H)], fill=red, width=2)
    # Croix droite blanche (St George) large, puis rouge plus fine
    cx, cy = W // 2, H // 2
    d.rectangle([cx - 4, 0, cx + 3, H], fill=white)
    d.rectangle([0, cy - 4, W, cy + 3], fill=white)
    d.rectangle([cx - 2, 0, cx + 1, H], fill=red)
    d.rectangle([0, cy - 2, W, cy + 1], fill=red)
    return frame(img)


os.makedirs(OUT, exist_ok=True)
flag_fr().save(os.path.join(OUT, "flag_fr.png"))
flag_en().save(os.path.join(OUT, "flag_en.png"))
flag_es().save(os.path.join(OUT, "flag_es.png"))
print("flags written to", OUT)
