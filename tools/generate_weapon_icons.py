"""Genere les icones 32x32 manquantes (style maison) pour les 2 nouvelles armes :
- tesla_coil  : eclair cyan/blanc
- aether_nova : detonation violette (anneau + rayons)
Sortie : assets/sprites/ui/ui_icon_tesla.png et ui_icon_nova.png
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

# ---------------- Tesla : eclair cyan
def gen_tesla():
    img = canvas()
    CY  = (70, 200, 255)
    CY_G= (70, 200, 255, 70)
    WH  = (235, 250, 255)
    # zigzag de l'eclair
    pts = [(20,3),(13,13),(18,15),(10,29)]
    # halo
    for i in range(len(pts)-1):
        line(img, pts[i][0], pts[i][1], pts[i+1][0], pts[i+1][1], CY_G, w=2)
    # coeur
    for i in range(len(pts)-1):
        line(img, pts[i][0], pts[i][1], pts[i+1][0], pts[i+1][1], CY, w=1)
        line(img, pts[i][0], pts[i][1], pts[i+1][0], pts[i+1][1], WH, w=0)
    # etincelles
    for (x,y) in [(24,8),(7,18),(22,20),(5,25)]:
        put(img, x, y, (CY[0],CY[1],CY[2],200))
    save_icon(img, "ui_icon_tesla.png")

# ---------------- Nova : detonation violette
def gen_nova():
    img = canvas()
    V   = (170, 68, 255)
    VB  = (210, 150, 255)
    VG  = (170, 68, 255, 60)
    cx, cy = 16, 16
    # halo
    disc(img, cx, cy, 11, VG)
    # anneau de choc
    ring(img, cx, cy, 11, 2, (V[0],V[1],V[2],230))
    ring(img, cx, cy, 7, 1, (VB[0],VB[1],VB[2],180))
    # coeur lumineux
    disc(img, cx, cy, 3, VB)
    disc(img, cx, cy, 1, (255,255,255))
    # rayons (8 directions)
    for k in range(8):
        a = math.pi*2*k/8
        x0 = cx + math.cos(a)*5; y0 = cy + math.sin(a)*5
        x1 = cx + math.cos(a)*14; y1 = cy + math.sin(a)*14
        line(img, x0, y0, x1, y1, (VB[0],VB[1],VB[2],200), w=0)
    save_icon(img, "ui_icon_nova.png")

# ---------------- Volee Multiple : eventail de projectiles
def gen_scatter():
    img = canvas()
    CY  = (70, 200, 255)
    WH  = (235, 250, 255)
    GLD = (255, 210, 90)
    # emetteur en bas-centre
    ex, ey = 16, 27
    disc(img, ex, ey, 2, GLD)
    # 3 projectiles en eventail vers le haut (-28, 0, +28 deg)
    for ang in (-28, 0, 28):
        a = math.radians(-90 + ang)  # -90 = vers le haut
        # trainee
        x1 = ex + math.cos(a)*16; y1 = ey + math.sin(a)*16
        line(img, ex, ey, x1, y1, (CY[0],CY[1],CY[2],90), w=1)
        # tete du projectile (petit losange)
        hx = ex + math.cos(a)*16; hy = ey + math.sin(a)*16
        for (dx,dy) in [(0,0),(1,0),(-1,0),(0,1),(0,-1)]:
            put(img, hx+dx, hy+dy, CY)
        put(img, hx, hy, WH)
    save_icon(img, "ui_icon_scatter.png")

# ---------------- Essaim Orbital : drones en orbite (fusion epique, or)
def gen_orbital():
    img = canvas()
    GLD  = (255, 210, 90)
    GLD_G= (255, 210, 90, 60)
    CY   = (90, 210, 255)
    WH   = (235, 250, 255)
    cx, cy = 16, 16
    # halo + orbite doree
    disc(img, cx, cy, 12, GLD_G)
    ring(img, cx, cy, 11, 1, (GLD[0],GLD[1],GLD[2],180))
    # coeur (joueur)
    disc(img, cx, cy, 3, GLD)
    disc(img, cx, cy, 1, WH)
    # 6 drones cyan repartis sur l'orbite
    for k in range(6):
        a = math.pi*2*k/6
        dx = cx + math.cos(a)*11; dy = cy + math.sin(a)*11
        disc(img, dx, dy, 1.6, CY)
        put(img, dx, dy, WH)
    save_icon(img, "ui_icon_orbital.png")

# ---------------- Egide de Surcharge : bouclier-forteresse (fusion epique, or)
def gen_aegis():
    img = canvas()
    GLD  = (255, 210, 90)
    GLD_G= (255, 210, 90, 70)
    V    = (170, 68, 255)
    GRN  = (90, 255, 150)
    WH   = (235, 250, 255)
    cx, cy = 16, 15
    GLD_D = (200, 150, 40)  # contour dore plus fonce
    # champ exterieur = simple anneau violet (ne noie pas l'ecu)
    ring(img, cx, 14, 14, 1, (V[0],V[1],V[2],120))
    ring(img, cx, 14, 13, 1, (V[0],V[1],V[2],55))
    # corps du bouclier (ecu dore PLEIN)
    for y in range(3, 28):
        if y < 7:
            half = 9
        else:
            t = (y-7)/(27-7)
            half = int(round(9*(1-t)))
        for x in range(cx-half, cx+half+1):
            edge = (x <= cx-half+0.5 or x >= cx+half-0.5 or y <= 4)
            put(img, x, y, GLD_D if edge else GLD)
    # croix de soin verte au centre
    line(img, cx, 9, cx, 19, GRN, w=0)
    line(img, cx-3, 14, cx+3, 14, GRN, w=0)
    put(img, cx, 14, WH)
    save_icon(img, "ui_icon_aegis.png")

# ---------------- Lame Boomerang : lame tournoyante cyan + arc de retour
def gen_glaive():
    img = canvas()
    CY = (80, 255, 235); WH = (240, 255, 255); GL = (80, 255, 235, 50)
    cx, cy = 16, 16
    disc(img, cx, cy, 12, GL)  # halo
    for ang in (0, 90, 180, 270):
        a = math.radians(ang)
        tx = cx + math.cos(a) * 11; ty = cy + math.sin(a) * 11
        line(img, cx, cy, tx, ty, CY, w=1)
        disc(img, tx, ty, 1.5, WH)
    disc(img, cx, cy, 2.5, CY); put(img, cx, cy, WH)
    for k in range(11):  # arc de mouvement (pointillé)
        a = math.radians(20 + k * 14)
        put(img, cx + math.cos(a) * 13, cy + math.sin(a) * 13, (CY[0], CY[1], CY[2], 120))
    save_icon(img, "ui_icon_glaive.png")

# ---------------- Essaim Traqueur : missiles violets a trainee incurvee vers une cible
def gen_seeker():
    img = canvas()
    V = (170, 90, 255); WH = (235, 225, 255); RED = (255, 110, 110)
    tx, ty = 23, 8
    disc(img, tx, ty, 2, RED)  # cible
    for (sx, sy, bend) in [(8, 26, 1), (13, 28, -1)]:
        for t in range(0, 11):
            f = t / 10.0
            mx = sx + (tx - sx) * f + bend * math.sin(f * math.pi) * 5
            my = sy + (ty - sy) * f
            put(img, mx, my, (V[0], V[1], V[2], int(60 + 120 * f)))
        hx = sx + (tx - sx) * 0.85 + bend * math.sin(0.85 * math.pi) * 5
        hy = sy + (ty - sy) * 0.85
        disc(img, hx, hy, 1.6, V); put(img, hx, hy, WH)
    save_icon(img, "ui_icon_seeker.png")

# ---------------- Lance Cryo : rayon glace diagonal cyan/blanc + eclats
def gen_cryo():
    img = canvas()
    CY = (120, 220, 255); WH = (235, 250, 255); ICE = (180, 235, 255)
    # faisceau diagonal (bas-gauche -> haut-droite)
    line(img, 5, 27, 27, 5, (CY[0], CY[1], CY[2], 110), w=2)  # halo large
    line(img, 5, 27, 27, 5, WH, w=0)                          # coeur blanc
    # eclats de givre perpendiculaires
    for (mx, my) in [(11, 21), (16, 16), (21, 11)]:
        line(img, mx-2, my-2, mx+2, my+2, (ICE[0], ICE[1], ICE[2], 160), w=0)
        line(img, mx-2, my+2, mx+2, my-2, (ICE[0], ICE[1], ICE[2], 160), w=0)
    disc(img, 27, 5, 2, WH)
    save_icon(img, "ui_icon_cryo.png")

# ---------------- Jet de Pyre : cone de flammes (jaune->rouge) depuis une buse
def gen_pyre():
    img = canvas()
    YEL = (255, 220, 110); ORA = (255, 140, 40); RED = (220, 70, 30); WH = (255, 250, 230)
    nx, ny = 7, 24  # buse bas-gauche
    disc(img, nx, ny, 2, (150, 150, 160))
    # cone de flammes vers haut-droite (3 nappes concentriques)
    for (rad, col, spread) in [(20, RED, 26), (15, ORA, 18), (9, YEL, 10)]:
        for ang in range(-spread, spread+1, 4):
            a = math.radians(-45 + ang)
            tx = nx + math.cos(a) * rad; ty = ny + math.sin(a) * rad
            line(img, nx, ny, tx, ty, col, w=0)
    disc(img, nx+2, ny-2, 1, WH)
    save_icon(img, "ui_icon_pyre.png")

# ---------------- Singularite : vortex spirale violet vers un coeur sombre
def gen_singularity():
    img = canvas()
    V = (170, 90, 255); VL = (210, 160, 255); WH = (240, 230, 255); DK = (40, 12, 60)
    cx, cy = 16, 16
    disc(img, cx, cy, 13, (V[0], V[1], V[2], 45))   # halo
    # 3 bras spirales (rayon decroissant, angle croissant)
    for arm in range(3):
        base = arm * math.tau / 3 if hasattr(math, "tau") else arm * 2 * math.pi / 3
        prev = None
        for i in range(16):
            t = i / 15.0
            r = 12 * (1 - t) + 2 * t
            a = base + t * 3.2
            x = cx + math.cos(a) * r; y = cy + math.sin(a) * r
            if prev is not None:
                line(img, prev[0], prev[1], x, y, VL if t > 0.5 else V, w=0)
            prev = (x, y)
    disc(img, cx, cy, 3, DK)        # coeur sombre
    ring(img, cx, cy, 3, 1, WH)     # liseré du coeur
    save_icon(img, "ui_icon_singularity.png")

if __name__ == "__main__":
    gen_tesla()
    gen_nova()
    gen_orbital()
    gen_aegis()
    gen_glaive()
    gen_seeker()
    gen_cryo()
    gen_pyre()
    gen_singularity()
    print("Termine.")
