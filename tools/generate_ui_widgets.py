"""Genere les widgets d'UI qui reposent sur des ICONES et non sur un StyleBox :
poignee de curseur (HSlider) et interrupteur (CheckButton).

Meme charte que les cadres (ART_BRIEF_UI_FRAMES) : acier chanfreine, bevel
haut-gauche eclaire / bas-droite ombre, angle droit, aucun anti-aliasing.
Les constantes et la derivation de teinte viennent de generate_ui_frames, qui
les tient lui-meme de pseudo3d_lib.shade() — rien n'est redefini ici.

Usage :
    python tools/generate_ui_widgets.py
Sortie : assets/sprites/ui/frames/ui_slider_*.png, ui_toggle_*.png
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image

from generate_ui_frames import (  # noqa: E402
    ACCENTS, DISABLED_NEUTRAL, OUT, STEEL_BASE, STEEL_CONTACT, STEEL_HI, STEEL_SH, _set,
)

DIM = (0x8C, 0x91, 0xA8)   # accent eteint : interrupteur au repos


def _canvas(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def _chamfered_plate(img, x0, y0, x1, y1, chamfer, fill, accent, accent_alpha=255):
    """Plaque pleine a coins coupes : contact 1 px, bevel 1 px directionnel,
    remplissage, puis lisere accent sur le pourtour interieur."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            # Coins coupes en diagonale — vrai 45 deg, donc escalier net.
            lx, ly = x - x0, y - y0
            rx, ry = x1 - x, y1 - y
            if lx + ly < chamfer or rx + ly < chamfer or lx + ry < chamfer or rx + ry < chamfer:
                continue

            on_outer = lx == 0 or ly == 0 or rx == 0 or ry == 0
            on_inner = lx <= 2 or ly <= 2 or rx <= 2 or ry <= 2

            if on_outer:
                _set(img, x, y, STEEL_CONTACT, 200)
            elif lx == 1 or ly == 1:
                _set(img, x, y, STEEL_HI)                 # cote eclaire
            elif rx == 1 or ry == 1:
                _set(img, x, y, STEEL_SH)                 # cote ombre
            elif on_inner:
                _set(img, x, y, accent, accent_alpha)     # lisere de categorie
            else:
                _set(img, x, y, fill)


def gen_slider_grabber(name, accent, size=14):
    """Poignee de curseur : petite plaque carree chanfreinee."""
    img = _canvas(size, size)
    _chamfered_plate(img, 0, 0, size - 1, size - 1, chamfer=3, fill=STEEL_BASE, accent=accent)
    return img


def gen_toggle(name, on, width=44, height=22):
    """Interrupteur : rail chanfreine + pave mobile. L'etat se lit a la fois a la
    POSITION du pave et a la couleur du lisere — jamais a la couleur seule."""
    img = _canvas(width, height)
    accent = ACCENTS["cyan"] if on else DIM
    alpha = 255 if on else 150
    _chamfered_plate(img, 0, 0, width - 1, height - 1, chamfer=4,
                     fill=STEEL_BASE, accent=accent, accent_alpha=alpha)

    # Pave mobile : a droite quand actif, a gauche au repos.
    pad = 4
    knob_w = 16
    kx0 = width - pad - knob_w if on else pad
    kx1 = kx0 + knob_w - 1
    ky0, ky1 = pad, height - pad - 1
    for y in range(ky0, ky1 + 1):
        for x in range(kx0, kx1 + 1):
            lx, ly = x - kx0, y - ky0
            rx, ry = kx1 - x, ky1 - y
            if lx + ly < 2 or rx + ly < 2 or lx + ry < 2 or rx + ry < 2:
                continue
            if lx == 0 or ly == 0:
                _set(img, x, y, STEEL_HI)
            elif rx == 0 or ry == 0:
                _set(img, x, y, STEEL_SH)
            else:
                _set(img, x, y, accent if on else DISABLED_NEUTRAL)
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    produced = [
        ("ui_slider_grabber.png",       gen_slider_grabber("g", ACCENTS["cyan"])),
        ("ui_slider_grabber_focus.png", gen_slider_grabber("gf", ACCENTS["violet"])),
        ("ui_toggle_on.png",            gen_toggle("on", True)),
        ("ui_toggle_off.png",           gen_toggle("off", False)),
    ]
    for filename, img in produced:
        img.save(os.path.join(OUT, filename))
        print(filename, img.size)
    print(f"{len(produced)} widgets -> {OUT}")


if __name__ == "__main__":
    main()
