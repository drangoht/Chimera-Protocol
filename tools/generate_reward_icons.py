"""Genere les icones 32x32 manquantes du systeme Defis/Perks (docs/DESIGN_CHALLENGES.md) :
- ui_icon_extra_slot : perk "+1 slot d'arme/greffe" (accent cyan)
- ui_icon_echo       : monnaie "Echo", recompense de 7 defis (accent violet)
- ui_icon_title      : recompense cosmetique "titre", 3 defis (accent or)
Sortie : assets/sprites/ui/

Calque sur tools/generate_weapon_icons.py (memes helpers canvas/put/disc/ring/line,
canvas 32x32, ombrage final via pseudo3d_lib.shade_icon avant sauvegarde).
"""
import os, sys, math
from PIL import Image

S = 32
ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "assets", "sprites", "ui")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pseudo3d_lib as _p3d


def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def save_icon(img, filename):
    """Ombrage icone UI (2 faces, amplitude reduite, §5 du brief), puis sauvegarde."""
    img = _p3d.shade_icon(img)
    img.save(os.path.join(OUT, filename))
    print(filename)


def put(img, x, y, c):
    x, y = int(round(x)), int(round(y))
    if 0 <= x < S and 0 <= y < S:
        if len(c) == 3:
            c = (c[0], c[1], c[2], 255)
        base = img.getpixel((x, y))
        a = c[3] / 255.0
        img.putpixel((x, y), (
            int(c[0]*a + base[0]*(1-a)),
            int(c[1]*a + base[1]*(1-a)),
            int(c[2]*a + base[2]*(1-a)),
            max(base[3], c[3]),
        ))


def disc(img, cx, cy, r, c):
    for y in range(int(cy-r-1), int(cy+r+2)):
        for x in range(int(cx-r-1), int(cx+r+2)):
            if (x-cx)**2 + (y-cy)**2 <= r*r:
                put(img, x, y, c)


def ring(img, cx, cy, r, w, c):
    for y in range(int(cy-r-1), int(cy+r+2)):
        for x in range(int(cx-r-1), int(cx+r+2)):
            d2 = (x-cx)**2 + (y-cy)**2
            if (r-w)**2 <= d2 <= r*r:
                put(img, x, y, c)


def line(img, x0, y0, x1, y1, c, w=1):
    n = int(max(abs(x1-x0), abs(y1-y0))) + 1
    for i in range(n+1):
        t = i/n
        x = x0 + (x1-x0)*t
        y = y0 + (y1-y0)*t
        for dx in range(-w, w+1):
            for dy in range(-w, w+1):
                if dx*dx+dy*dy <= w*w:
                    put(img, x+dx, y+dy, c)


def hexagon(img, cx, cy, r, c, w=None):
    """Hexagone (pointe en haut) ; plein si w est None, contour d'epaisseur w sinon."""
    pts = []
    for k in range(6):
        a = math.pi/2 + math.pi*2*k/6  # premier sommet vers le haut
        pts.append((cx + math.cos(a)*r, cy - math.sin(a)*r))
    if w is None:
        # remplissage par scanline (polygone convexe)
        ys = [p[1] for p in pts]
        y0, y1 = int(min(ys)), int(max(ys))
        for y in range(y0, y1+1):
            xs = []
            for i in range(6):
                ax, ay = pts[i]
                bx, by = pts[(i+1) % 6]
                if ay == by:
                    continue
                if min(ay, by) <= y < max(ay, by):
                    t = (y - ay) / (by - ay)
                    xs.append(ax + (bx-ax)*t)
            xs.sort()
            for i in range(0, len(xs)-1, 2):
                for x in range(int(round(xs[i])), int(round(xs[i+1]))+1):
                    put(img, x, y, c)
    else:
        for i in range(6):
            line(img, pts[i][0], pts[i][1], pts[(i+1) % 6][0], pts[(i+1) % 6][1], c, w=w)


# ---------------- Slot Supplementaire : 3 cases hexagonales, la 3e surlignee cyan avec un "+"
def gen_extra_slot():
    img = canvas()
    CY    = (70, 200, 255)
    CY_G  = (70, 200, 255, 90)
    DIM   = (90, 100, 130)
    DIM_D = (60, 68, 92)
    WH    = (235, 250, 255)

    r = 6.4
    # 3 cases alignees horizontalement, legerement decalees en quinconce (lisibilite silhouette)
    centers = [(8, 20), (16, 16), (24, 12)]

    # cases 1 et 2 : slots deja occupes, contour sobre gris-bleu
    for (cx, cy) in centers[:2]:
        hexagon(img, cx, cy, r, DIM_D)
        hexagon(img, cx, cy, r - 1.4, DIM, w=None) if False else None
        hexagon(img, cx, cy, r, (DIM[0], DIM[1], DIM[2], 255), w=1)
        # petite pastille interieure pour signifier "occupe"
        disc(img, cx, cy, 1.6, DIM)

    # case 3 : nouveau slot debloque, halo + contour cyan lumineux
    cx, cy = centers[2]
    disc(img, cx, cy, r + 3, CY_G)          # halo
    hexagon(img, cx, cy, r, (10, 30, 45))    # fond sombre plein
    hexagon(img, cx, cy, r, CY, w=1)         # contour cyan
    # signe "+" lumineux au centre
    line(img, cx, cy-3, cx, cy+3, WH, w=0)
    line(img, cx-3, cy, cx+3, cy, WH, w=0)
    put(img, cx, cy, WH)

    save_icon(img, "ui_icon_extra_slot.png")


# ---------------- Echo : cristal/fragment violet lumineux (monnaie meta)
def gen_echo():
    img = canvas()
    V    = (170, 68, 255)
    V_D  = (110, 40, 180)
    VB   = (210, 150, 255)
    V_G  = (170, 68, 255, 70)
    WH   = (240, 225, 255)
    cx, cy = 16, 16

    # halo/onde
    disc(img, cx, cy, 12, V_G)
    ring(img, cx, cy, 12, 1, (V[0], V[1], V[2], 90))

    # cristal facette (losange allonge vertical avec pointes)
    pts_outline = [(cx, cy-11), (cx+5, cy-2), (cx+3, cy+11), (cx-3, cy+11), (cx-5, cy-2)]
    # remplissage par scanline du polygone (convexe-ish, 5 sommets)
    ys = [p[1] for p in pts_outline]
    y0, y1 = int(min(ys)), int(max(ys))
    n = len(pts_outline)
    for y in range(y0, y1+1):
        xs = []
        for i in range(n):
            ax, ay = pts_outline[i]
            bx, by = pts_outline[(i+1) % n]
            if ay == by:
                continue
            if min(ay, by) <= y < max(ay, by):
                t = (y - ay) / (by - ay)
                xs.append(ax + (bx-ax)*t)
        xs.sort()
        for i in range(0, len(xs)-1, 2):
            for x in range(int(round(xs[i])), int(round(xs[i+1]))+1):
                # facette gauche plus sombre que la facette droite (relief cristal)
                col = V_D if x < cx else V
                put(img, x, y, col)

    # ligne de facette centrale + reflet
    line(img, cx, cy-11, cx, cy+11, (VB[0], VB[1], VB[2], 160), w=0)
    line(img, cx, cy-11, cx+5, cy-2, VB, w=0)
    disc(img, cx, cy-6, 1, WH)

    save_icon(img, "ui_icon_echo.png")


# ---------------- Titre : medaille doree, ruban + etoile
def gen_title():
    img = canvas()
    GLD   = (255, 204, 68)
    GLD_D = (200, 150, 40)
    GLD_G = (255, 204, 68, 70)
    RED   = (200, 60, 60)
    RED_D = (150, 35, 35)
    WH    = (255, 250, 230)
    cx, cy = 16, 19

    # ruban (deux pans en V sous la medaille)
    for (x0, y0, x1, y1) in [(cx-5, cy-9, cx-8, cy-1), (cx+5, cy-9, cx+8, cy-1)]:
        line(img, x0, y0, x1, y1, RED, w=1)
    line(img, cx-8, cy-1, cx-6, cy-3, RED_D, w=0)
    line(img, cx+8, cy-1, cx+6, cy-3, RED_D, w=0)

    # halo + disque medaille
    disc(img, cx, cy, 10, GLD_G)
    disc(img, cx, cy, 8, GLD_D)
    disc(img, cx, cy, 7, GLD)
    ring(img, cx, cy, 7, 1, GLD_D)

    # etoile a 5 branches au centre (silhouette simple par rayons pleins)
    for k in range(5):
        a = -math.pi/2 + math.pi*2*k/5
        x1 = cx + math.cos(a)*4.4; y1 = cy + math.sin(a)*4.4
        line(img, cx, cy, x1, y1, WH, w=1)
    disc(img, cx, cy, 1.6, WH)

    save_icon(img, "ui_icon_title.png")


if __name__ == "__main__":
    gen_extra_slot()
    gen_echo()
    gen_title()
    print("Termine.")
