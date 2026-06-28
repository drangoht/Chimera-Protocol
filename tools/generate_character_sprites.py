"""
Genere des sprites pixel art DEDIES pour les 2 personnages alternatifs (32x32,
style maison cohérent avec generate_sprites.py / generate_boss_sprites.py).

- titan    : robot-gardien lourd, chassis blindé acier + visière orange.
- vagabond : humain survivant, capuche/cape kaki + écharpe verte.

Memes animations que le joueur d'origine pour que Player.cs marche sans changement :
  idle (4), run_right (6), run_down (6), death (8).

Sortie : assets/sprites/player/titan/  et  .../vagabond/  + leurs SpriteFrames .tres.
Lancer : python tools/generate_character_sprites.py
"""
import os, math

from PIL import Image

S = 32
ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))

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

def glow(img, cx, cy, r, c, strength=0.5):
    for y in range(int(cy-r-1), int(cy+r+2)):
        for x in range(int(cx-r-1), int(cx+r+2)):
            d = math.hypot(x-cx, y-cy)
            if d <= r:
                a = int(255 * strength * (1 - d/r))
                if a > 0:
                    put(img, x, y, (c[0], c[1], c[2], a))

def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "PNG")

# ================================================================ TITAN (robot)
T_DARK = (54, 60, 70)
T_MID  = (92, 102, 116)
T_LITE = (140, 152, 168)
T_ORN  = (255, 150, 50)
T_ORNB = (255, 205, 120)

def draw_titan(img, leg=0, bob=0, facing="down", dead=0, alpha=255):
    cx = 16
    top = 6 + bob
    a = alpha
    D=(T_DARK[0],T_DARK[1],T_DARK[2],a); M=(T_MID[0],T_MID[1],T_MID[2],a); L=(T_LITE[0],T_LITE[1],T_LITE[2],a)

    # jambes courtes et trapues (leg = phase de marche -1..1)
    lo = int(leg*2)
    rect(img, cx-6, top+18+max(0,lo), cx-2, top+24+max(0,lo), D)
    rect(img, cx+2, top+18+max(0,-lo), cx+6, top+24+max(0,-lo), D)

    # torse blindé large
    rect(img, cx-9, top+8, cx+9, top+19, D)
    rect(img, cx-7, top+9, cx+7, top+18, M)
    # plaques d'épaules massives
    rect(img, cx-11, top+8, cx-7, top+13, D); rect(img, cx-11, top+8, cx-7, top+9, L)
    rect(img, cx+7,  top+8, cx+11, top+13, D); rect(img, cx+7, top+8, cx+11, top+9, L)
    # noyau orange poitrine
    glow(img, cx, top+13, 6, T_ORN, 0.5)
    rect(img, cx-2, top+11, cx+1, top+15, (T_ORN[0],T_ORN[1],T_ORN[2],a))
    put(img, cx-1, top+12, T_ORNB); put(img, cx, top+12, T_ORNB)

    # tete trapue + visière orange
    rect(img, cx-5, top, cx+5, top+8, D)
    rect(img, cx-4, top+1, cx+4, top+7, M)
    if facing == "right":
        rect(img, cx+1, top+3, cx+4, top+5, (T_ORN[0],T_ORN[1],T_ORN[2],a))
        glow(img, cx+3, top+4, 4, T_ORN, 0.4)
    else:
        rect(img, cx-3, top+3, cx+3, top+5, (T_ORN[0],T_ORN[1],T_ORN[2],a))
        glow(img, cx, top+4, 5, T_ORN, 0.35)
    # antenne
    put(img, cx, top-1, L); put(img, cx, top-2, (T_ORN[0],T_ORN[1],T_ORN[2],a))

    if dead:  # s'affaisse + se fissure
        for yy in range(top, top+24, 2):
            rect(img, cx-9, yy, cx+9, yy, (0,0,0, int(a*0.0)))

# ================================================================ VAGABOND (humain)
V_SKIN = (224, 178, 140)
V_SKINS= (180, 130, 96)
V_CLK  = (96, 108, 70)     # kaki
V_CLKD = (66, 76, 48)
V_SCRF = (90, 200, 130)    # écharpe verte
V_BOOT = (60, 48, 40)

def draw_vagabond(img, leg=0, bob=0, facing="down", dead=0, alpha=255):
    cx = 16
    top = 6 + bob
    a = alpha
    SK=(V_SKIN[0],V_SKIN[1],V_SKIN[2],a); CL=(V_CLK[0],V_CLK[1],V_CLK[2],a); CD=(V_CLKD[0],V_CLKD[1],V_CLKD[2],a)

    lo = int(leg*2)
    # jambes fines + bottes
    rect(img, cx-4, top+18+max(0,lo), cx-2, top+23+max(0,lo), CD)
    rect(img, cx+2, top+18+max(0,-lo), cx+4, top+23+max(0,-lo), CD)
    rect(img, cx-4, top+23+max(0,lo),  cx-2, top+24+max(0,lo), (V_BOOT[0],V_BOOT[1],V_BOOT[2],a))
    rect(img, cx+2, top+23+max(0,-lo), cx+4, top+24+max(0,-lo),(V_BOOT[0],V_BOOT[1],V_BOOT[2],a))

    # cape / torse
    rect(img, cx-6, top+8, cx+6, top+19, CL)
    rect(img, cx-6, top+8, cx-5, top+19, CD); rect(img, cx+5, top+8, cx+6, top+19, CD)
    # écharpe verte
    rect(img, cx-5, top+8, cx+5, top+9, (V_SCRF[0],V_SCRF[1],V_SCRF[2],a))
    glow(img, cx, top+9, 4, V_SCRF, 0.25)

    # tête (capuche) + visage
    rect(img, cx-4, top, cx+4, top+8, CD)        # capuche
    if facing == "right":
        rect(img, cx-1, top+3, cx+3, top+7, SK)  # visage tourné
        put(img, cx+2, top+4, (40,40,40,a))      # œil
    else:
        rect(img, cx-3, top+3, cx+3, top+7, SK)
        put(img, cx-2, top+5, (40,40,40,a)); put(img, cx+2, top+5, (40,40,40,a))
    rect(img, cx-4, top, cx+4, top+1, CL)        # bord capuche

    if dead:
        for yy in range(top, top+24, 2):
            rect(img, cx-6, yy, cx+6, yy, (0,0,0, 0))

# ================================================================ frames
def gen_char(folder, draw, speeds):
    base = os.path.join(ROOT, "assets", "sprites", "player", folder)
    prefix = folder
    counts = {"idle": 4, "run_right": 6, "run_down": 6, "death": 8}

    # idle : léger bob
    for i in range(4):
        img = canvas()
        draw(img, leg=0, bob=(0 if i in (0,2) else 1), facing="down")
        save(img, os.path.join(base, f"{prefix}_idle_{i+1:02d}.png"))

    # run_right : cycle de jambes, profil
    for i in range(6):
        img = canvas()
        phase = math.sin(i / 6 * 2*math.pi)
        draw(img, leg=phase, bob=(1 if i % 2 else 0), facing="right")
        save(img, os.path.join(base, f"{prefix}_run_right_{i+1:02d}.png"))

    # run_down : cycle de jambes, face
    for i in range(6):
        img = canvas()
        phase = math.sin(i / 6 * 2*math.pi)
        draw(img, leg=phase, bob=(1 if i % 2 else 0), facing="down")
        save(img, os.path.join(base, f"{prefix}_run_down_{i+1:02d}.png"))

    # death : s'affaisse + fade alpha
    for i in range(8):
        img = canvas()
        al = int(255 * (1 - i/8))
        draw(img, leg=0, bob=min(6, i), facing="down", dead=1, alpha=max(40, al))
        save(img, os.path.join(base, f"{prefix}_death_{i+1:02d}.png"))

    write_tres(folder, prefix, counts, speeds)
    print(f"{folder}: frames + .tres OK")

def write_tres(folder, prefix, counts, speeds):
    order = ["idle", "run_right", "run_down", "death"]
    paths = []
    for anim in order:
        for i in range(counts[anim]):
            paths.append(f"res://assets/sprites/player/{folder}/{prefix}_{anim}_{i+1:02d}.png")

    lines = [f'[gd_resource type="SpriteFrames" load_steps={len(paths)+1} format=3]', ""]
    for idx, p in enumerate(paths, start=1):
        lines.append(f'[ext_resource type="Texture2D" path="{p}" id="{idx}"]')
    lines.append("")
    lines.append("[resource]")
    lines.append("animations = [")
    idx = 1
    blocks = []
    for anim in order:
        frames = []
        for _ in range(counts[anim]):
            frames.append(f'{{"duration": 1.0, "texture": ExtResource("{idx}")}}')
            idx += 1
        loop = "false" if anim == "death" else "true"
        blocks.append('{\n'
                       f'"frames": [{", ".join(frames)}],\n'
                       f'"loop": {loop},\n'
                       f'"name": &"{anim}",\n'
                       f'"speed": {speeds[anim]:.1f}\n'
                       '}')
    lines.append(", ".join(blocks))
    lines.append("]")
    path = os.path.join(ROOT, "assets", "sprites", "player", folder, f"{folder}_frames.tres")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("  .tres ecrit :", path)

def main():
    speeds = {"idle": 6.0, "run_right": 12.0, "run_down": 12.0, "death": 10.0}
    gen_char("titan", draw_titan, speeds)
    gen_char("vagabond", draw_vagabond, speeds)
    print("Termine.")

if __name__ == "__main__":
    main()
