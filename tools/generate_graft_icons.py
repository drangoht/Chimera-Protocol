"""Genere les 8 icones de greffe du systeme d'Assimilation (5 greffes + 3 fusions, docs/DESIGN_ASSIMILATION.md).

Meme pipeline / meme format que les icones d'armes (tools/generate_weapon_icons.py) :
canvas RGBA 32x32, dessin par primitives (put/disc/ring/line), puis ombrage pseudo-3D
"icone UI" (tools/pseudo3d_lib.py: shade_icon(), lumiere fixe haut-gauche 45 degres,
2 faces highlight/shadow, amplitude reduite).

Sortie : assets/sprites/grafts/<id>_icon.png (chemin lu par data/grafts.json, champ
"hudIcon", et par src/Core/Rules/GraftTable.cs / src/UI/AssimilationScreen.cs).

Les couleurs sont choisies pour correspondre a la teinte de chaque greffe (brief
graphiste, alignee sur le champ "tint" de grafts.json) : rouille/orange, bleu froid,
ambre, brun blinde, magenta. Le champ JSON "tint" lui-meme n'est PAS applique comme
modulate sur la texture par le code (verifie dans AssimilationScreen.cs / Hud.cs : il
sert uniquement a colorer le label de rarete et le carre de fallback HUD) -- l'icone
doit donc porter ses propres couleurs finales.

Usage :
    python tools/generate_graft_icons.py
"""
import os
import sys
import math
from PIL import Image

S = 32
ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "assets", "sprites", "grafts")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pseudo3d_lib as _p3d


def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def save_icon(img, filename):
    """Ombrage icone UI (2 faces, amplitude reduite, §5 du brief pseudo-3D), puis sauvegarde."""
    os.makedirs(OUT, exist_ok=True)
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
            int(c[0] * a + base[0] * (1 - a)),
            int(c[1] * a + base[1] * (1 - a)),
            int(c[2] * a + base[2] * (1 - a)),
            max(base[3], c[3]),
        ))


def disc(img, cx, cy, r, c):
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            if (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                put(img, x, y, c)


def ring(img, cx, cy, r, w, c):
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            d2 = (x - cx) ** 2 + (y - cy) ** 2
            if (r - w) ** 2 <= d2 <= r * r:
                put(img, x, y, c)


def line(img, x0, y0, x1, y1, c, w=1):
    n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
    for i in range(n + 1):
        t = i / n
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t
        for dx in range(-w, w + 1):
            for dy in range(-w, w + 1):
                if dx * dx + dy * dy <= w * w:
                    put(img, x + dx, y + dy, c)


# ---------------- 1) Nuee Symbiotique : biomasse grouillante + insectes orbitants (rouille/orange)
def gen_swarm_symbiote():
    img = canvas()
    ORA_D = (140, 65, 35)
    ORA   = (225, 110, 55)
    ORA_L = (255, 175, 115)
    BUG   = (255, 150, 75)
    WH    = (255, 235, 210)
    cx, cy = 16, 17

    # halo organique tenu
    disc(img, cx, cy, 12, (ORA[0], ORA[1], ORA[2], 45))

    # biomasse centrale : blob irregulier (plusieurs disques decales, pas un cercle propre)
    disc(img, cx, cy, 6, ORA)
    disc(img, cx - 3, cy + 2, 4, ORA)
    disc(img, cx + 3, cy - 2, 4, ORA_D)
    disc(img, cx + 2, cy + 3, 3, ORA_D)
    disc(img, cx - 2, cy - 3, 3, ORA_L)
    # veines/speckles internes (texture grouillante)
    for (dx, dy) in [(-2, 0), (1, -1), (3, 1), (-1, 3), (0, -2)]:
        put(img, cx + dx, cy + dy, (ORA_D[0], ORA_D[1], ORA_D[2], 220))

    # 4 mini-essaims orbitants (insectes), corps + antennes courtes + trainee
    for k in range(4):
        a = math.pi * 2 * k / 4 + 0.4
        ox = cx + math.cos(a) * 11
        oy = cy + math.sin(a) * 11
        # trainee (sens de deplacement, perpendiculaire au rayon)
        ta = a + math.pi / 2
        tx = ox - math.cos(ta) * 3
        ty = oy - math.sin(ta) * 3
        line(img, tx, ty, ox, oy, (BUG[0], BUG[1], BUG[2], 130), w=0)
        # corps
        disc(img, ox, oy, 1.8, BUG)
        put(img, ox, oy, WH)
        # antennes
        put(img, ox + math.cos(a) * 2, oy + math.sin(a) * 2, ORA_D)
    save_icon(img, "swarm_symbiote_icon.png")


# ---------------- 2) Servos Erratiques : dash / trainee de vitesse + servo mecanique (bleu froid)
def gen_erratic_servos():
    img = canvas()
    CY   = (110, 175, 255)
    CY_L = (200, 230, 255)
    GRY  = (200, 210, 225)
    GRY_D = (95, 105, 125)
    WH   = (240, 250, 255)

    x0, y0 = 5, 27   # arriere (bas-gauche)
    x1, y1 = 22, 10  # avant (haut-droite), position du servo

    # 3 trainees de mouvement paralleles, alpha decroissant vers l'arriere (after-image)
    for off, alpha in ((0, 0), (-3, 130), (-6, 70)):
        line(img, x0 - off * 0.3, y0 + off, x1 - off * 0.3, y1 + off,
             (CY[0], CY[1], CY[2], 255 if alpha == 0 else alpha), w=1 if alpha == 0 else 0)

    # servo mecanique (petit rouage cranté) a l'avant du dash
    ring(img, x1, y1, 3.2, 1, GRY_D)
    disc(img, x1, y1, 2.0, GRY)
    put(img, x1, y1, WH)
    for k in range(6):
        a = math.pi * 2 * k / 6
        tx = x1 + math.cos(a) * 4
        ty = y1 + math.sin(a) * 4
        put(img, tx, ty, GRY_D)

    # chevrons de vitesse (direction du dash) derriere le servo
    dirx, diry = (x1 - x0), (y1 - y0)
    dl = math.hypot(dirx, diry)
    dirx, diry = dirx / dl, diry / dl
    perp = (-diry, dirx)
    for k in range(3):
        bx = x1 - dirx * (6 + k * 5)
        by = y1 - diry * (6 + k * 5)
        line(img, bx - perp[0] * 2.5, by - perp[1] * 2.5, bx + dirx * 2, by + diry * 2,
             (CY_L[0], CY_L[1], CY_L[2], 200), w=0)
        line(img, bx + perp[0] * 2.5, by + perp[1] * 2.5, bx + dirx * 2, by + diry * 2,
             (CY_L[0], CY_L[1], CY_L[2], 200), w=0)
    save_icon(img, "erratic_servos_icon.png")


# ---------------- 3) Oeil de Visee : oeil-capteur + reticule de tourelle (ambre)
def gen_aiming_eye():
    img = canvas()
    AMB   = (255, 180, 80)
    AMB_L = (255, 220, 160)
    IRIS  = (150, 90, 25)
    PUP   = (40, 20, 10)
    WH    = (255, 250, 225)
    cx, cy = 16, 16

    # halo du capteur
    disc(img, cx, cy, 12, (AMB[0], AMB[1], AMB[2], 40))
    # reticule (anneau + 4 amorces de croix, style visee de tourelle)
    ring(img, cx, cy, 11, 1, (AMB_L[0], AMB_L[1], AMB_L[2], 170))
    for (dx0, dy0, dx1, dy1) in [(0, -14, 0, -9), (0, 14, 0, 9), (-14, 0, -9, 0), (14, 0, 9, 0)]:
        line(img, cx + dx0, cy + dy0, cx + dx1, cy + dy1, (AMB_L[0], AMB_L[1], AMB_L[2], 200), w=0)

    # oeil (forme amande : deux arcs empiles formant la fente palpebrale)
    for x in range(cx - 8, cx + 9):
        t = (x - cx) / 8.0
        top = -6 * (1 - t * t) ** 0.5
        bot = 3 * (1 - t * t) ** 0.5
        line(img, x, cy + top, x, cy + bot, AMB, w=0)
    # iris + pupille
    disc(img, cx, cy + 1, 4, IRIS)
    disc(img, cx, cy + 1, 2, PUP)
    put(img, cx - 1, cy, WH)
    save_icon(img, "aiming_eye_icon.png")


# ---------------- 4) Carapace Greffee : plaque blindee + epines (brun arme)
def gen_grafted_carapace():
    img = canvas()
    BRN   = (150, 105, 80)
    BRN_D = (95, 65, 55)
    BRN_L = (195, 155, 125)
    SPK   = (215, 195, 175)
    cx, cy = 16, 18

    # plaque principale (ecu hexagonal robuste)
    pts_half = {3: 5, 8: 9, 16: 9, 24: 6, 28: 2}
    for y in range(3, 29):
        if y <= 8:
            half = 5 + (y - 3) * (9 - 5) / 5.0
        elif y <= 22:
            half = 9
        else:
            t = (y - 22) / 6.0
            half = 9 * (1 - t) + 2 * t
        half = int(round(half))
        for x in range(cx - half, cx + half + 1):
            edge = x <= cx - half + 1 or x >= cx + half - 1
            put(img, x, y, BRN_D if edge else BRN)
    # lignes de segments (rainures de plaque)
    for yy in (10, 16, 22):
        for x in range(cx - 7, cx + 8):
            put(img, x, yy, (BRN_D[0], BRN_D[1], BRN_D[2], 160))
    # reflet haut-gauche
    for y in range(4, 9):
        put(img, cx - 3, y, BRN_L)

    # 3 epines (thorns) qui depassent du contour
    for (ang, length) in [(-100, 7), (-55, 6), (250, 6)]:
        a = math.radians(ang)
        bx = cx + math.cos(a) * 8
        by = cy - 6 + math.sin(a) * 8
        tx = bx + math.cos(a) * length
        ty = by + math.sin(a) * length
        line(img, bx, by, tx, ty, SPK, w=0)
        put(img, tx, ty, BRN_L)
    save_icon(img, "grafted_carapace_icon.png")


# ---------------- 5) Onde du Rodeur : onde de choc concentrique + oeil predateur (magenta)
def gen_stalker_wave():
    img = canvas()
    MG   = (225, 55, 195)
    MG_L = (255, 150, 230)
    MG_G = (225, 55, 195, 55)
    DK   = (55, 10, 45)
    WH   = (255, 225, 250)
    cx, cy = 16, 16

    # halo + 3 anneaux de choc concentriques (amplitude decroissante vers l'exterieur)
    disc(img, cx, cy, 13, MG_G)
    ring(img, cx, cy, 13, 1, (MG[0], MG[1], MG[2], 140))
    ring(img, cx, cy, 9, 1, (MG_L[0], MG_L[1], MG_L[2], 190))
    ring(img, cx, cy, 5, 1, (MG[0], MG[1], MG[2], 230))

    # predateur territorial : oeil central (pupille fendue verticale) au coeur de l'onde
    disc(img, cx, cy, 3, DK)
    for dy in range(-2, 3):
        put(img, cx, cy + dy, (10, 2, 12, 255))
    put(img, cx - 1, cy - 1, WH)
    ring(img, cx, cy, 3, 1, (MG_L[0], MG_L[1], MG_L[2], 220))
    save_icon(img, "stalker_wave_icon.png")


# ============================================================================
# FUSIONS (Phase B volet 1, docs/DESIGN_ASSIMILATION.md §15) : chaque icone
# fusionne visuellement les deux greffes requises (couleurs + motifs des deux).
# ============================================================================

# ---------------- F1) Charge Blindee : plaque blindee (Carapace) qui CHARGE avec
# chevrons de vitesse (Servos). Brun blinde + trainee cyan. tint rouille-orange.
def gen_fusion_charge_blindee():
    img = canvas()
    BRN   = (150, 105, 80)
    BRN_D = (95, 65, 55)
    BRN_L = (200, 160, 130)
    SPK   = (220, 200, 180)
    CY    = (110, 175, 255)
    CY_L  = (200, 230, 255)

    # direction de charge : bas-gauche -> haut-droite
    dirx, diry = 0.78, -0.62
    perp = (-diry, dirx)
    # centre de la plaque, decale vers l'avant
    cx, cy = 20, 13

    # 3 trainees cyan de vitesse derriere la plaque (after-image, alpha decroissant)
    for k, alpha in ((1, 200), (2, 130), (3, 70)):
        bx = cx - dirx * (7 + k * 4)
        by = cy - diry * (7 + k * 4)
        line(img, bx - perp[0] * 6, by - perp[1] * 6,
                  bx + perp[0] * 6, by + perp[1] * 6,
                  (CY[0], CY[1], CY[2], alpha), w=0)

    # plaque blindee inclinee (ecu oriente dans le sens de la charge)
    for t in range(-7, 8):
        # axe transversal de l'ecu (perp a la charge)
        px = cx + perp[0] * t
        py = cy + perp[1] * t
        # largeur longitudinale : nez pointu a l'avant, arriere plus large
        halffront = 5.0 * (1 - (t / 8.0) ** 2) ** 0.5
        for s in range(-6, int(round(halffront)) + 1):
            x = px + dirx * s
            y = py + diry * s
            edge = (t <= -6 or t >= 6 or s >= int(round(halffront)) - 1)
            put(img, x, y, BRN_D if edge else BRN)
    # rainures de plaque (2 lignes longitudinales)
    for toff in (-3, 3):
        px = cx + perp[0] * toff
        py = cy + perp[1] * toff
        line(img, px - dirx * 5, py - diry * 5, px + dirx * 4, py + diry * 4,
             (BRN_D[0], BRN_D[1], BRN_D[2], 170), w=0)
    # reflet haut-gauche sur le nez
    put(img, cx + dirx * 3 + perp[0] * -2, cy + diry * 3 + perp[1] * -2, BRN_L)
    put(img, cx + dirx * 4, cy + diry * 4, CY_L)

    # 3 epines qui depassent du bord d'attaque (thorns conserves de la Carapace)
    for toff in (-4, 0, 4):
        bx = cx + perp[0] * toff + dirx * 5
        by = cy + perp[1] * toff + diry * 5
        tx = bx + dirx * 4
        ty = by + diry * 4
        line(img, bx, by, tx, ty, SPK, w=0)
        put(img, tx, ty, BRN_L)
    save_icon(img, "fusion_charge_blindee_icon.png")


# ---------------- F2) Ruche de Tourelles : moyeu de biomasse (Nuee) + 4 tourelles
# a reticule ambre (Oeil) tirant a 360. Orange + ambre. tint ambre-orange.
def gen_fusion_ruche_tourelles():
    img = canvas()
    ORA   = (225, 110, 55)
    ORA_D = (140, 65, 35)
    ORA_L = (255, 175, 115)
    AMB   = (255, 180, 80)
    AMB_L = (255, 225, 165)
    IRIS  = (150, 90, 25)
    PUP   = (35, 18, 8)
    WH    = (255, 245, 225)
    cx, cy = 16, 16

    # halo organique
    disc(img, cx, cy, 13, (ORA[0], ORA[1], ORA[2], 40))

    # 4 tourelles ancrees autour du moyeu, chacune tire un trait ambre vers l'exterieur
    for k in range(4):
        a = math.pi * 2 * k / 4 + math.pi / 4
        tx = cx + math.cos(a) * 8
        ty = cy + math.sin(a) * 8
        # tir (rayon ambre partant de la tourelle vers l'exterieur, resserre pour tenir dans le slot)
        ex = cx + math.cos(a) * 13
        ey = cy + math.sin(a) * 13
        line(img, tx, ty, ex, ey, (AMB_L[0], AMB_L[1], AMB_L[2], 190), w=0)
        put(img, ex, ey, WH)
        # petit reticule/oeil de la tourelle
        ring(img, tx, ty, 2.4, 1, AMB)
        disc(img, tx, ty, 1.3, IRIS)
        put(img, tx, ty, WH)

    # moyeu central : biomasse (blob irregulier, comme la Nuee)
    disc(img, cx, cy, 5, ORA)
    disc(img, cx - 2, cy + 2, 3, ORA_D)
    disc(img, cx + 2, cy - 2, 3, ORA_L)
    for (dx, dy) in [(-2, 0), (1, -1), (2, 1), (-1, 2)]:
        put(img, cx + dx, cy + dy, (ORA_D[0], ORA_D[1], ORA_D[2], 220))
    # oeil-noyau central (heritage de l'Oeil de Visee)
    disc(img, cx, cy, 2, PUP)
    put(img, cx - 1, cy - 1, WH)
    save_icon(img, "fusion_ruche_tourelles_icon.png")


# ---------------- F3) Frappe Nova : blink cyan (Servos Erratiques) qui arrive et
# detone une nova en etoile magenta/violet (Onde du Rodeur). Trainee de teleportation
# cyan + explosion violette a pointes + coeur d'etoile. tint magenta-violet.
def gen_fusion_nova_rodeur():
    img = canvas()
    VIO   = (180, 80, 230)
    MG    = (235, 70, 205)
    MG_L  = (255, 165, 235)
    CY    = (110, 175, 255)
    CY_L  = (205, 235, 255)
    WH    = (255, 240, 255)
    cx, cy = 18, 14   # point d'arrivee du blink = centre de la nova (decale haut-droite)

    # blink cyan : trainee de teleportation bas-gauche -> point de nova (after-image, alpha decroissant)
    bx0, by0 = 3, 29
    for off, alpha, w in ((0, 255, 1), (-3, 140, 0), (-6, 75, 0)):
        line(img, bx0, by0 + off, cx - 4, cy + 4 - off * 0.3,
             (CY[0], CY[1], CY[2], alpha), w=w)
    # eclat residuel au point de depart du blink
    disc(img, bx0, by0, 1.6, (CY_L[0], CY_L[1], CY_L[2], 150))

    # detonation (PAS d'anneaux concentriques : signature de l'Onde du Rodeur) :
    # halo violet diffus + un unique arc de souffle esquisse (dash), le reste = explosion
    disc(img, cx, cy, 12, (VIO[0], VIO[1], VIO[2], 50))
    for k in range(0, 24, 2):  # arc de souffle pointille (moitie superieure)
        a = math.pi + math.pi * k / 24.0
        put(img, cx + math.cos(a) * 11, cy + math.sin(a) * 11, (VIO[0], VIO[1], VIO[2], 150))

    # etoile de nova : 8 pointes rayonnantes acerees (detonation), longueur alternee,
    # effilees (chaud violet a la base -> magenta clair a la pointe)
    for k in range(8):
        a = math.pi * 2 * k / 8
        r_out = 12 if k % 2 == 0 else 7
        n = int(r_out) + 1
        for i in range(n + 1):
            t = i / n
            r = 1 + (r_out - 1) * t
            col = (
                int(VIO[0] + (MG_L[0] - VIO[0]) * t),
                int(VIO[1] + (MG_L[1] - VIO[1]) * t),
                int(VIO[2] + (MG_L[2] - VIO[2]) * t),
                255,
            )
            # pointe effilee : epaisse a la base, 1px a l'extremite
            wdt = 1 if t < 0.45 else 0
            for dw in range(-wdt, wdt + 1):
                perp = a + math.pi / 2
                put(img, cx + math.cos(a) * r + math.cos(perp) * dw,
                         cy + math.sin(a) * r + math.sin(perp) * dw, col)
        put(img, cx + math.cos(a) * r_out, cy + math.sin(a) * r_out, WH)

    # coeur d'etoile brulant (heritage du prop 'coeur d'etoile')
    disc(img, cx, cy, 3, MG)
    disc(img, cx, cy, 2, MG_L)
    disc(img, cx, cy, 1, WH)
    save_icon(img, "fusion_nova_rodeur_icon.png")


if __name__ == "__main__":
    gen_swarm_symbiote()
    gen_erratic_servos()
    gen_aiming_eye()
    gen_grafted_carapace()
    gen_stalker_wave()
    gen_fusion_charge_blindee()
    gen_fusion_ruche_tourelles()
    gen_fusion_nova_rodeur()
    print("Termine.")
