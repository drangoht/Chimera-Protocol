"""
Genere des sprites pixel art DEDIES pour les 2 nouveaux boss, dans le style maison
(64x64, palette cyberpunk fantasy-SF, cohérent avec generate_sprites.py).

- Revenant d'Aether  : spectre cyborg flottant, noyau violet, lames d'energie.
- Le Noyau Rouille    : titan-gardien massif, enorme noyau en fusion or-rouille, plaques fissurees.

Sortie : assets/sprites/enemies/aether_revenant/ et .../rusted_core/
+ les SpriteFrames .tres correspondants.

Lancer : python tools/generate_boss_sprites.py
"""
import os, sys, math, random

from PIL import Image

S = 64
ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pseudo3d_lib as _p3d

# ---------------------------------------------------------------- primitives
def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))

def put(img, x, y, c):
    x, y = int(round(x)), int(round(y))
    if 0 <= x < S and 0 <= y < S:
        if len(c) == 3:
            c = (c[0], c[1], c[2], 255)
        if c[3] == 255:
            img.putpixel((x, y), c)
        else:
            base = img.getpixel((x, y))
            a = c[3] / 255.0
            img.putpixel((x, y), (
                int(c[0]*a + base[0]*(1-a)),
                int(c[1]*a + base[1]*(1-a)),
                int(c[2]*a + base[2]*(1-a)),
                max(base[3], c[3]),
            ))

def rect(img, x0, y0, x1, y1, c):
    for y in range(int(y0), int(y1)+1):
        for x in range(int(x0), int(x1)+1):
            put(img, x, y, c)

def disc(img, cx, cy, r, c):
    for y in range(int(cy-r-1), int(cy+r+1)):
        for x in range(int(cx-r-1), int(cx+r+1)):
            if (x-cx)**2 + (y-cy)**2 <= r*r:
                put(img, x, y, c)

def ring(img, cx, cy, r, w, c):
    for y in range(int(cy-r-1), int(cy+r+1)):
        for x in range(int(cx-r-1), int(cx+r+1)):
            d2 = (x-cx)**2 + (y-cy)**2
            if (r-w)**2 <= d2 <= r*r:
                put(img, x, y, c)

def glow(img, cx, cy, r, c, strength=0.5):
    if r <= 0.5:
        return
    for y in range(int(cy-r-1), int(cy+r+1)):
        for x in range(int(cx-r-1), int(cx+r+1)):
            d = math.hypot(x-cx, y-cy)
            if d <= r:
                a = int(255 * strength * (1 - d/r))
                if a > 0:
                    put(img, x, y, (c[0], c[1], c[2], a))

def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "PNG")

# ================================================================ REVENANT
# Spectre cyborg flottant : capuche sombre, noyau violet, bras-lames, voile spectral.
R_DARK   = (38, 30, 54)
R_MID    = (74, 58, 102)
R_LIGHT  = (118, 96, 158)
R_CORE   = (170, 68, 255)
R_CORE_B = (210, 150, 255)
R_EYE    = (255, 110, 255)
R_BLADE  = (150, 90, 240)
R_BLADE_B= (220, 180, 255)

def draw_revenant(img, bob=0, core=1.0, arm=0.0, wisp=0, alpha=255, dissolve=0.0):
    cx = 32
    top = 12 + bob
    a = alpha

    # voile spectral inferieur (au lieu de jambes) — bandes ondulantes
    for i in range(8):
        yy = 44 + i*2 + bob
        amp = 6 - i*0.4
        off = math.sin((i + wisp) * 0.9) * amp
        col = (R_MID[0], R_MID[1], R_MID[2], int(a * (0.7 - i*0.07)))
        rect(img, cx-7+off, yy, cx+7+off, yy+1, col)

    # corps / robe blindee
    rect(img, cx-10, top+10, cx+10, top+30, (R_DARK[0],R_DARK[1],R_DARK[2],a))
    rect(img, cx-8,  top+10, cx+8,  top+28, (R_MID[0],R_MID[1],R_MID[2],a))
    # plaques epaules
    rect(img, cx-13, top+10, cx-9, top+16, (R_DARK[0],R_DARK[1],R_DARK[2],a))
    rect(img, cx+9,  top+10, cx+13, top+16, (R_DARK[0],R_DARK[1],R_DARK[2],a))
    rect(img, cx-13, top+10, cx-9, top+11, (R_LIGHT[0],R_LIGHT[1],R_LIGHT[2],a))
    rect(img, cx+9,  top+10, cx+13, top+11, (R_LIGHT[0],R_LIGHT[1],R_LIGHT[2],a))

    # capuche / tete
    rect(img, cx-7, top, cx+7, top+11, (R_DARK[0],R_DARK[1],R_DARK[2],a))
    rect(img, cx-5, top+2, cx+5, top+9, (24,18,34,a))   # ombre interne capuche
    # yeux lumineux
    put(img, cx-3, top+5, R_EYE); put(img, cx-2, top+5, R_EYE)
    put(img, cx+2, top+5, R_EYE); put(img, cx+3, top+5, R_EYE)
    glow(img, cx, top+5, 6, R_EYE, 0.25*core)

    # noyau violet dans la poitrine (losange) + halo
    ncy = top+20
    glow(img, cx, ncy, 11*core, R_CORE, 0.45*core)
    for dy in range(-4, 5):
        w = 4 - abs(dy)
        col = R_CORE_B if abs(dy) <= 1 else R_CORE
        rect(img, cx-w, ncy+dy, cx+w, ncy+dy, (col[0],col[1],col[2],a))

    # bras-lames d'energie (arm = extension 0..1)
    reach = int(6 + arm*10)
    # bras gauche
    rect(img, cx-11, top+16, cx-9, top+22, (R_DARK[0],R_DARK[1],R_DARK[2],a))
    for k in range(reach):
        yy = top+22 + k
        xx = cx-10 - k*0.7
        col = R_BLADE_B if k > reach-3 else R_BLADE
        put(img, xx, yy, (col[0],col[1],col[2],a))
        put(img, xx-1, yy, (R_BLADE[0],R_BLADE[1],R_BLADE[2],int(a*0.6)))
    # bras droit
    rect(img, cx+9, top+16, cx+11, top+22, (R_DARK[0],R_DARK[1],R_DARK[2],a))
    for k in range(reach):
        yy = top+22 + k
        xx = cx+10 + k*0.7
        col = R_BLADE_B if k > reach-3 else R_BLADE
        put(img, xx, yy, (col[0],col[1],col[2],a))
        put(img, xx+1, yy, (R_BLADE[0],R_BLADE[1],R_BLADE[2],int(a*0.6)))

    # dissolve : ronge des pixels aleatoirement (frames de mort)
    if dissolve > 0:
        rnd = random.Random(1234)
        px = img.load()
        for y in range(S):
            for x in range(S):
                if px[x,y][3] > 0 and rnd.random() < dissolve:
                    px[x,y] = (0,0,0,0)

def gen_revenant(out):
    n = {}
    # idle : 4 frames (flottement + pulsation noyau)
    for i in range(4):
        img = canvas()
        bob = [0,-1,0,1][i]
        draw_revenant(img, bob=bob, core=0.7+0.3*math.sin(i*1.6), wisp=i)
        save(img, f"{out}/aether_revenant_idle_{i+1:02d}.png")
    n["idle"] = 4
    # move : 6 frames (voile qui ondule + leger balancement)
    for i in range(6):
        img = canvas()
        bob = [0,-1,-1,0,1,1][i]
        draw_revenant(img, bob=bob, core=0.8, wisp=i*1.5, arm=0.0)
        save(img, f"{out}/aether_revenant_move_{i+1:02d}.png")
    n["move"] = 6
    # attack : 5 frames (lames qui s'etendent puis frappent)
    arms = [0.2, 0.6, 1.0, 0.7, 0.3]
    for i in range(5):
        img = canvas()
        draw_revenant(img, bob=0, core=1.0, arm=arms[i], wisp=i)
        save(img, f"{out}/aether_revenant_attack_{i+1:02d}.png")
    n["attack"] = 5
    # death : 12 frames (dissolution spectrale)
    for i in range(12):
        img = canvas()
        d = i / 11.0
        # alpha=255 (pleine opacite) pour beneficier du shading pseudo-3D : la
        # degradation visuelle vient deja de `dissolve` (erosion de pixels) et
        # des particules ci-dessous, cf. docs/ART_BRIEF_PSEUDO3D.md
        draw_revenant(img, bob=int(d*4), core=max(0.0,1.0-d), alpha=255, dissolve=d*0.85)
        # eclats violets qui montent
        rnd = random.Random(50+i)
        for _ in range(int(d*22)):
            x = 32 + rnd.randint(-14,14); y = 30 - int(d*18) + rnd.randint(-6,6)
            put(img, x, y, (R_CORE_B[0],R_CORE_B[1],R_CORE_B[2],int(200*(1-d))))
        save(img, f"{out}/aether_revenant_death_{i+1:02d}.png")
    n["death"] = 12
    return n

# ================================================================ RUSTED CORE
# Titan-gardien : plaques rouille, enorme noyau en fusion or-orange, fissures lumineuses.
C_DARK   = (42, 26, 18)
C_RUST   = (106, 58, 34)
C_RUST_L = (150, 86, 48)
C_METAL  = (90, 74, 66)
C_CORE   = (255, 136, 34)
C_CORE_B = (255, 210, 90)
C_CRACK  = (255, 90, 30)
C_EYE    = (255, 180, 60)

# Noyau energetique / accents jamais assombris par l'ombrage (§5/§6 du brief).
_CORE_COLORS = [
    R_CORE, R_CORE_B, R_EYE, R_BLADE, R_BLADE_B,
    C_CORE, C_CORE_B, C_CRACK, C_EYE,
]
save = _p3d.wrap_save(save, core_colors=_CORE_COLORS)


def draw_rusted_core(img, core=1.0, stomp=0, charge=0.0, alpha=255, blast=0.0, broken=0.0):
    cx = 32
    base = 52 + stomp
    a = alpha

    # ombre / pieds trapus
    rect(img, cx-16, base, cx-6, base+4, (C_DARK[0],C_DARK[1],C_DARK[2],a))
    rect(img, cx+6,  base, cx+16, base+4, (C_DARK[0],C_DARK[1],C_DARK[2],a))

    # corps massif (tronc) — fissures
    body_top = 18
    rect(img, cx-18, body_top, cx+18, base, (C_RUST[0],C_RUST[1],C_RUST[2],a))
    rect(img, cx-18, body_top, cx+18, body_top+2, (C_RUST_L[0],C_RUST_L[1],C_RUST_L[2],a))
    # plaques laterales
    rect(img, cx-20, body_top+4, cx-16, base-2, (C_METAL[0],C_METAL[1],C_METAL[2],a))
    rect(img, cx+16, body_top+4, cx+20, base-2, (C_METAL[0],C_METAL[1],C_METAL[2],a))
    # epaules massives
    rect(img, cx-22, body_top, cx-12, body_top+10, (C_DARK[0],C_DARK[1],C_DARK[2],a))
    rect(img, cx+12, body_top, cx+22, body_top+10, (C_DARK[0],C_DARK[1],C_DARK[2],a))
    rect(img, cx-22, body_top, cx-12, body_top+1, (C_RUST_L[0],C_RUST_L[1],C_RUST_L[2],a))
    rect(img, cx+12, body_top, cx+22, body_top+1, (C_RUST_L[0],C_RUST_L[1],C_RUST_L[2],a))

    # fissures lumineuses sur le corps (pulsation)
    crack_a = int(a * (0.5 + 0.5*core))
    for (x0,y0,x1,y1) in [(cx-10,28,cx-4,40),(cx+4,30,cx+11,44),(cx-2,40,cx+2,50)]:
        for t in range(0,11):
            x = x0 + (x1-x0)*t/10 + math.sin(t)*1
            y = y0 + (y1-y0)*t/10
            put(img, x, y, (C_CRACK[0],C_CRACK[1],C_CRACK[2],crack_a))

    # tete / dome avec oeil unique
    rect(img, cx-8, body_top-8, cx+8, body_top+1, (C_METAL[0],C_METAL[1],C_METAL[2],a))
    rect(img, cx-8, body_top-8, cx+8, body_top-7, (C_RUST_L[0],C_RUST_L[1],C_RUST_L[2],a))
    glow(img, cx, body_top-3, 5, C_EYE, 0.4*core)
    rect(img, cx-3, body_top-4, cx+3, body_top-2, (C_EYE[0],C_EYE[1],C_EYE[2],a))

    # ENORME noyau en fusion central
    ncy = 34
    rr = 12 * (0.85 + 0.15*core) * (1 + charge*0.4)
    glow(img, cx, ncy, rr+8, C_CORE, 0.5*core)
    disc(img, cx, ncy, rr, (C_CORE[0],C_CORE[1],C_CORE[2],a))
    disc(img, cx, ncy, rr*0.6, (C_CORE_B[0],C_CORE_B[1],C_CORE_B[2],a))
    ring(img, cx, ncy, rr+1, 1, (C_CRACK[0],C_CRACK[1],C_CRACK[2],a))
    if charge > 0.3:
        glow(img, cx, ncy, rr+12*charge, C_CORE_B, 0.6*charge)

    # blast (mort) : onde lumineuse + surcharge du noyau
    if blast > 0:
        glow(img, cx, ncy, 14 + blast*40, C_CORE_B, 0.7*(1-blast*0.5))
        ring(img, cx, ncy, int(10 + blast*46), 2, (C_CORE_B[0],C_CORE_B[1],C_CORE_B[2],int(220*(1-blast))))

    # broken : effrite le bas du corps
    if broken > 0:
        rnd = random.Random(77)
        px = img.load()
        for y in range(int(body_top), S):
            for x in range(S):
                if px[x,y][3] > 0 and rnd.random() < broken * ((y-body_top)/40.0):
                    px[x,y] = (0,0,0,0)

def gen_rusted_core(out):
    n = {}
    # idle : 4 frames (noyau qui pulse)
    for i in range(4):
        img = canvas()
        draw_rusted_core(img, core=0.6+0.4*math.sin(i*1.6))
        save(img, f"{out}/rusted_core_idle_{i+1:02d}.png")
    n["idle"] = 4
    # move : 6 frames (martelement)
    for i in range(6):
        img = canvas()
        draw_rusted_core(img, core=0.8, stomp=[0,1,1,0,1,1][i])
        save(img, f"{out}/rusted_core_move_{i+1:02d}.png")
    n["move"] = 6
    # attack : 6 frames (charge du noyau puis decharge radiale)
    ch = [0.2,0.5,0.8,1.0,0.5,0.1]
    for i in range(6):
        img = canvas()
        draw_rusted_core(img, core=1.0, charge=ch[i])
        if i == 3:  # flash de decharge
            glow(img, 32, 34, 30, C_CORE_B, 0.5)
        save(img, f"{out}/rusted_core_attack_{i+1:02d}.png")
    n["attack"] = 6
    # death : 14 frames (surcharge -> explosion -> effritement)
    for i in range(14):
        img = canvas()
        d = i / 13.0
        # alpha=255 pour le shading pseudo-3D : `broken`/`blast` portent deja
        # la degradation visuelle, cf. docs/ART_BRIEF_PSEUDO3D.md
        draw_rusted_core(img, core=max(0.0,1.0-d*0.5), charge=min(1.0,d*1.5),
                         blast=min(1.0,d), broken=max(0.0,(d-0.4)*1.6),
                         alpha=255)
        rnd = random.Random(90+i)
        for _ in range(int(d*30)):
            x = 32 + rnd.randint(-20,20); y = 34 + rnd.randint(-18,18)
            col = C_CORE_B if rnd.random()<0.5 else C_CRACK
            put(img, x, y, (col[0],col[1],col[2],int(220*(1-d))))
        save(img, f"{out}/rusted_core_death_{i+1:02d}.png")
    n["death"] = 14
    return n

# ================================================================ .tres
def write_tres(folder, prefix, counts, speeds):
    """Genere le SpriteFrames .tres referencant toutes les frames."""
    order = ["idle", "move", "attack", "death"]
    paths = []
    for anim in order:
        for i in range(counts[anim]):
            paths.append(f"res://assets/sprites/enemies/{folder}/{prefix}_{anim}_{i+1:02d}.png")

    lines = [f'[gd_resource type="SpriteFrames" load_steps={len(paths)+1} format=3]', ""]
    for idx, p in enumerate(paths, start=1):
        lines.append(f'[ext_resource type="Texture2D" path="{p}" id="{idx}"]')
    lines.append("")
    lines.append("[resource]")
    lines.append("animations = [")
    idx = 1
    anim_blocks = []
    for anim in order:
        frames = []
        for _ in range(counts[anim]):
            frames.append(f'{{"duration": 1.0, "texture": ExtResource("{idx}")}}')
            idx += 1
        loop = "true" if anim in ("idle", "move") else "false"
        block = ('{\n'
                 f'"frames": [{", ".join(frames)}],\n'
                 f'"loop": {loop},\n'
                 f'"name": &"{anim}",\n'
                 f'"speed": {speeds[anim]:.1f}\n'
                 '}')
        anim_blocks.append(block)
    lines.append(", ".join(anim_blocks))
    lines.append("]")
    path = os.path.join(ROOT, "assets", "sprites", "enemies", folder, f"{prefix}_frames.tres")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("  .tres ecrit :", path)

# ================================================================ main
def main():
    rev_dir  = os.path.join(ROOT, "assets", "sprites", "enemies", "aether_revenant")
    core_dir = os.path.join(ROOT, "assets", "sprites", "enemies", "rusted_core")

    print("Revenant d'Aether...")
    rc = gen_revenant(rev_dir)
    write_tres("aether_revenant", "aether_revenant", rc,
               {"idle": 5.0, "move": 9.0, "attack": 11.0, "death": 12.0})

    print("Le Noyau Rouille...")
    cc = gen_rusted_core(core_dir)
    write_tres("rusted_core", "rusted_core", cc,
               {"idle": 4.0, "move": 7.0, "attack": 12.0, "death": 13.0})

    print("Termine.")

if __name__ == "__main__":
    main()
