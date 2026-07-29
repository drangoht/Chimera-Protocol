"""
generate_midboss_sprites.py - Sprites pixel art des 3 mid-boss de biome (48x48).

Un mid-boss par biome, rendez-vous de mi-run (~8 min) — cf. docs/GDD.md section 32 et
docs/EXPANSION_PLAN.md B.3 :

  molten_colossus : Colosse en Fusion (Fournaise) - bipede trapu, roche noircie fissuree de magma
  cryo_sentinel   : Sentinelle Cryo   (Givre)     - tourelle flottante cristalline, canon frontal
  neon_warden     : Gardien Neon      (Neon)      - noyau annulaire ceint d'un bouclier orbital

Les silhouettes sont VOLONTAIREMENT distinctes les unes des autres (trapue / elancee / circulaire) :
en pleine nuee, la forme est la seule chose que le joueur lit avant la couleur.

Taille 48x48 sur le PNG, mais rendu a 72 px EN JEU (MidBossVisuals.SpriteScale = 1,5).

/!\ Le raisonnement d'origine -- "48 : plus imposant que la faune (32) sans egaler le boss de fin
(64)" -- etait FAUX sur sa premisse. Le boss de fin n'est pas a 64 a l'ecran : RustedCore._Ready
l'affiche a Scale = 2,4, soit 154 px. Et les pairs de role mini_boss (rust_stalker,
aether_revenant, master_sentinel) ont tous un sprite natif de 64. Les 3 mid-boss etaient donc 25 %
plus petits que TOUS les autres champions, et leur hitbox debordait du sprite -- le Colosse touche
dans un diametre de 72 px (contactRadius 36) pour un corps qui n'en occupait que 48.

Ne PAS regenerer ces sprites en 72 pour "corriger" : les primitives ci-dessous dessinent en
coordonnees entieres dans un espace logique de 48 (rect/disc iterent sur range(int(y0), int(y1)+1)),
et y injecter un facteur laisserait des rangees vides. L'echelle est appliquee au rendu, comme pour
le boss de fin. Avec texture_filter = Nearest le resultat est identique a un agrandissement au plus
proche voisin.

Ombrage pseudo-3D derive par pseudo3d_lib (JAMAIS de couleur plate ad hoc, cf.
docs/ART_BRIEF_PSEUDO3D.md) : lumiere fixe haut-gauche, ombre portee elliptique, accents
lumineux preserves via la liste _ENERGY_COLORS.

Lancer : python tools/generate_midboss_sprites.py [--only=<id>]
"""
import os, sys, math, random

from PIL import Image

S = 48
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

def ellipse(img, cx, cy, rx, ry, c):
    for y in range(int(cy-ry-1), int(cy+ry+1)):
        for x in range(int(cx-rx-1), int(cx+rx+1)):
            if rx > 0 and ry > 0 and ((x-cx)/rx)**2 + ((y-cy)/ry)**2 <= 1.0:
                put(img, x, y, c)

def ring(img, cx, cy, r, w, c):
    for y in range(int(cy-r-1), int(cy+r+1)):
        for x in range(int(cx-r-1), int(cx+r+1)):
            d2 = (x-cx)**2 + (y-cy)**2
            if (r-w)**2 <= d2 <= r*r:
                put(img, x, y, c)

def arc(img, cx, cy, r, w, a0, a1, c):
    """Secteur d'anneau entre les angles a0 et a1 (radians, 0 = droite, sens horaire ecran)."""
    steps = max(8, int(r * abs(a1 - a0) * 2))
    for i in range(steps + 1):
        a = a0 + (a1 - a0) * i / steps
        for k in range(int(w)):
            put(img, cx + math.cos(a) * (r - k), cy + math.sin(a) * (r - k), c)

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

# ================================================================ palettes
#
# REGLE (apprise a la 1re passe de captures en jeu) : un champion doit CONTRASTER avec son biome,
# pas en reprendre la palette. La 1re version donnait au Colosse la teinte de la Fournaise et a la
# Sentinelle celle du Givre -- resultat, tous deux se fondaient dans leur propre decor et n'etaient
# reperables qu'a leur aura. Les chassis sont donc nettement plus SOMBRES que le sol de leur biome,
# et seuls les accents d'energie restent vifs : silhouette sombre + noyau brulant, lisible en nuee.

# Colosse en Fusion — roche volcanique presque noire, veines en fusion (sol de la Fournaise = brun clair).
M_DARK   = (26, 14, 12)
M_MID    = (54, 30, 24)
M_LIGHT  = (88, 50, 38)
M_CORE   = (255, 96, 24)
M_CORE_B = (255, 190, 90)
M_CRACK  = (255, 70, 18)

# Sentinelle Cryo — chassis bleu nuit, cristal cyan vif (sol du Givre = bleu-gris CLAIR).
C_DARK   = (14, 26, 40)
C_MID    = (36, 64, 92)
C_LIGHT  = (72, 112, 146)
C_CORE   = (120, 225, 255)
C_CORE_B = (215, 250, 255)
C_ICE    = (168, 232, 255)

# Gardien Neon — chrome violace, bouclier magenta, accents cyan.
N_DARK   = (28, 18, 44)
N_MID    = (74, 46, 104)
N_LIGHT  = (120, 84, 156)
N_CORE   = (255, 62, 210)
N_CORE_B = (255, 160, 240)
N_CYAN   = (120, 255, 250)

# Couleurs « energie » : preservees de l'assombrissement par l'ombrage pseudo-3D.
_ENERGY_COLORS = [M_CORE, M_CORE_B, M_CRACK,
                  C_CORE, C_CORE_B, C_ICE,
                  N_CORE, N_CORE_B, N_CYAN]

save = _p3d.wrap_save(save, core_colors=_ENERGY_COLORS)

# ================================================================ COLOSSE EN FUSION
def draw_molten_colossus(img, heat=1.0, stomp=0, charge=0.0, broken=0.0, seed=0):
    """Bipede trapu : epaules tres larges, jambes courtes, fissures incandescentes.

    `stomp` decale le corps d'un pixel (martelement), `charge` allume les veines avant la charge,
    `broken` ouvre les fissures a la mort.
    """
    cx = 24
    top = 10 + stomp
    rnd = random.Random(1000 + seed)

    # Jambes trapues, bien ecartees et depassant sous le torse (sinon le corps « flotte »)
    for sx in (-1, 1):
        rect(img, cx + sx * 7 - 3, top + 21, cx + sx * 7 + 3, top + 30, M_MID)
        rect(img, cx + sx * 7 - 3, top + 27, cx + sx * 7 + 3, top + 30, M_DARK)   # pied dans l'ombre
        rect(img, cx + sx * 7 - 4, top + 29, cx + sx * 7 + 4, top + 30, M_DARK)   # semelle large

    # Torse : trapeze massif, deja large en haut pour que les epaules s'y RATTACHENT.
    # (1re version : torse etroit + epaules a +-12 -> deux colonnes detachees, lecture « portique ».)
    for i in range(16):
        w = 9 + int(i * 0.20)
        rect(img, cx - w, top + 6 + i, cx + w, top + 6 + i, M_MID)
    # Pectoraux eclaires / ventre dans l'ombre : donne le volume
    ellipse(img, cx, top + 10, 8, 3, M_LIGHT)
    ellipse(img, cx, top + 19, 9, 4, M_DARK)

    # Epaules : masses arrondies posees SUR le torse, en continuite
    for sx in (-1, 1):
        ellipse(img, cx + sx * 10, top + 8, 4, 4, M_LIGHT)
        ellipse(img, cx + sx * 10, top + 10, 4, 3, M_MID)

    # Bras lourds pendants, colles aux epaules
    for sx in (-1, 1):
        rect(img, cx + sx * 11 - 2, top + 10, cx + sx * 11 + 2, top + 20, M_MID)
        ellipse(img, cx + sx * 11, top + 21, 3, 3, M_LIGHT)      # poing

    # Tete enfoncee dans les epaules (petite : la masse est dans le torse)
    ellipse(img, cx, top + 4, 5, 4, M_LIGHT)
    rect(img, cx - 4, top + 5, cx + 4, top + 7, M_MID)           # cou epais

    # Veines de magma : reseau de fissures qui s'allument avec `heat`/`charge`
    intensity = min(1.0, heat * 0.7 + charge * 0.6)
    veins = [((-6, 10), (-2, 16)), ((3, 9), (6, 15)), ((-1, 17), (2, 22)),
             ((-9, 8), (-6, 12)), ((7, 12), (9, 17))]
    for (x0, y0), (x1, y1) in veins:
        steps = max(abs(x1-x0), abs(y1-y0)) + 1
        for s in range(steps):
            t = s / max(1, steps - 1)
            col = M_CRACK if t < 0.6 else M_CORE
            a = int(255 * intensity)
            put(img, cx + x0 + (x1-x0)*t, top + y0 + (y1-y0)*t, (col[0], col[1], col[2], a))

    # Coeur en fusion (poitrine)
    core_r = 3 + charge * 2
    disc(img, cx, top + 13, core_r, M_CORE)
    disc(img, cx, top + 13, max(1, core_r - 1.5), M_CORE_B)
    glow(img, cx, top + 13, 7 + charge * 5, M_CORE, 0.30 + 0.35 * charge)

    # Yeux
    for sx in (-2, 2):
        put(img, cx + sx, top + 4, M_CORE_B)

    # Effritement a la mort : la roche se detache, le magma dessous transparait
    if broken > 0:
        for _ in range(int(broken * 34)):
            x = cx + rnd.randint(-14, 14)
            y = top + rnd.randint(2, 28)
            put(img, x, y, (0, 0, 0, 0) if rnd.random() < 0.55
                else (M_CORE[0], M_CORE[1], M_CORE[2], 220))

def gen_molten_colossus(out, prefix="molten_colossus"):
    n = {}
    for i in range(4):                                    # idle : respiration du coeur
        img = canvas()
        draw_molten_colossus(img, heat=0.55 + 0.45 * math.sin(i * 1.6))
        save(img, f"{out}/{prefix}_idle_{i+1:02d}.png")
    n["idle"] = 4

    for i in range(6):                                    # move : martelement
        img = canvas()
        draw_molten_colossus(img, heat=0.8, stomp=[0, 1, 1, 0, 1, 1][i])
        save(img, f"{out}/{prefix}_move_{i+1:02d}.png")
    n["move"] = 6

    ch = [0.15, 0.45, 0.75, 1.0, 0.6, 0.2]                # attack : charge (sillage de magma)
    for i in range(6):
        img = canvas()
        draw_molten_colossus(img, heat=1.0, stomp=i % 2, charge=ch[i])
        if i == 3:
            glow(img, 24, 24, 22, M_CORE, 0.45)
        save(img, f"{out}/{prefix}_attack_{i+1:02d}.png")
    n["attack"] = 6

    for i in range(10):                                   # death : la roche cede
        img = canvas()
        d = i / 9.0
        draw_molten_colossus(img, heat=max(0.0, 1.0 - d * 0.4), charge=min(1.0, d),
                             broken=d, seed=i)
        save(img, f"{out}/{prefix}_death_{i+1:02d}.png")
    n["death"] = 10
    return n

# ================================================================ SENTINELLE CRYO
def draw_cryo_sentinel(img, bob=0, spin=0.0, charge=0.0, dissolve=0.0, seed=0):
    """Tourelle flottante elancee : fuseau cristallin vertical, anneau de givre, canon frontal."""
    cx = 24
    top = 6 + bob
    rnd = random.Random(2000 + seed)

    # Ombre flottante suggeree par un socle vide : le corps ne touche pas le sol
    # (l'ombre portee reelle est ajoutee par pseudo3d_lib au save()).

    # Fuseau cristallin : losange etire vertical
    for i in range(26):
        t = i / 25.0
        w = int(2 + 7 * math.sin(math.pi * t))
        col = C_MID if 0.2 < t < 0.8 else C_DARK
        rect(img, cx - w, top + 4 + i, cx + w, top + 4 + i, col)
    # Arete lumineuse cote lumiere (haut-gauche)
    for i in range(22):
        t = i / 21.0
        w = int(2 + 7 * math.sin(math.pi * t))
        put(img, cx - w + 1, top + 6 + i, C_LIGHT)

    # Anneau de givre orbital (tourne avec `spin`). Eclats plus gros et plus contrastes que
    # dans la 1re version, ou l'anneau se reduisait a quelques pixels illisibles a l'ecran.
    rr = 16
    for k in range(8):
        a = spin + k * math.tau / 8
        x = cx + math.cos(a) * rr
        y = top + 18 + math.sin(a) * rr * 0.42          # ellipse : perspective 3/4
        front = math.sin(a) > 0                          # devant = plus clair et plus gros
        col = C_ICE if front else C_MID
        ellipse(img, x, y, 2 if front else 1, 1, col)
        if front:
            put(img, x, y - 2, C_CORE_B)                 # scintillement de l'eclat de tete

    # Canon frontal (bas du fuseau)
    rect(img, cx - 3, top + 26, cx + 3, top + 31, C_MID)
    rect(img, cx - 2, top + 30, cx + 2, top + 33, C_LIGHT)
    muzzle = 2 + charge * 3
    disc(img, cx, top + 33, muzzle, C_CORE)
    disc(img, cx, top + 33, max(1, muzzle - 1.2), C_CORE_B)
    glow(img, cx, top + 33, 5 + charge * 8, C_CORE, 0.25 + 0.40 * charge)

    # Noyau central
    disc(img, cx, top + 16, 3.5, C_CORE)
    disc(img, cx, top + 16, 2, C_CORE_B)
    glow(img, cx, top + 16, 8, C_CORE, 0.28)

    # Cone de gel telegraphie pendant la charge : evasement vers le bas
    if charge > 0.35:
        span = math.radians(26)
        reach = 6 + charge * 12
        for k in range(int(reach)):
            hw = int(k * math.tan(span))
            a = int(150 * charge * (1 - k / reach))
            for dx in range(-hw, hw + 1):
                put(img, cx + dx, top + 34 + k, (C_ICE[0], C_ICE[1], C_ICE[2], a))

    # Dissolution a la mort : le cristal se fend puis s'evapore
    if dissolve > 0:
        for _ in range(int(dissolve * 40)):
            x = cx + rnd.randint(-10, 10)
            y = top + rnd.randint(2, 32)
            if rnd.random() < 0.6:
                put(img, x, y, (0, 0, 0, 0))
            else:
                put(img, x, y, (C_CORE_B[0], C_CORE_B[1], C_CORE_B[2], 200))

def gen_cryo_sentinel(out, prefix="cryo_sentinel"):
    n = {}
    for i in range(4):                                    # idle : flottaison
        img = canvas()
        draw_cryo_sentinel(img, bob=[0, 1, 2, 1][i], spin=i * 0.5)
        save(img, f"{out}/{prefix}_idle_{i+1:02d}.png")
    n["idle"] = 4

    for i in range(6):                                    # move : derive + anneau qui tourne
        img = canvas()
        draw_cryo_sentinel(img, bob=[0, 1, 2, 2, 1, 0][i], spin=i * 0.9)
        save(img, f"{out}/{prefix}_move_{i+1:02d}.png")
    n["move"] = 6

    ch = [0.2, 0.5, 0.8, 1.0, 0.7, 0.25]                  # attack : cone de gel
    for i in range(6):
        img = canvas()
        draw_cryo_sentinel(img, bob=1, spin=i * 1.4, charge=ch[i])
        save(img, f"{out}/{prefix}_attack_{i+1:02d}.png")
    n["attack"] = 6

    for i in range(10):                                   # death : le cristal s'evapore
        img = canvas()
        d = i / 9.0
        draw_cryo_sentinel(img, bob=int(d * 3), spin=d * 4, dissolve=d, seed=i)
        save(img, f"{out}/{prefix}_death_{i+1:02d}.png")
    n["death"] = 10
    return n

# ================================================================ GARDIEN NEON
def draw_neon_warden(img, spin=0.0, gap=0.0, charge=0.0, broken=0.0, seed=0):
    """Noyau du Gardien SEUL — le bouclier orbital est dessine EN CODE par NeonWarden._Draw().

    Pourquoi pas dans le sprite : le bouclier tourne selon la logique de jeu (c'est lui qui decide
    quels degats passent). Le faire tourner en faisant pivoter l'AnimatedSprite2D ferait tourner
    l'eclairage avec lui, alors que l'ombrage pseudo-3D suppose une lumiere FIXE haut-gauche
    (docs/ART_BRIEF_PSEUDO3D.md). Dessine en code, il reste parfaitement synchrone avec l'angle
    reel, peut flasher a l'absorption, et le corps garde son ombrage.

    `spin` n'oriente donc plus que l'oeil-lentille. `charge` allume le noyau avant une invocation.
    """
    cx, cy = 24, 24
    rnd = random.Random(3000 + seed)

    # Chassis : noyau octogonal
    ellipse(img, cx, cy, 8, 8, N_MID)
    ellipse(img, cx, cy - 1, 7, 6, N_LIGHT)
    ellipse(img, cx, cy + 2, 6, 4, N_DARK)

    # Ailettes fixes (4 branches courtes) : casse le cercle parfait, lisible en nuee
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        for r in range(8, 12):
            put(img, cx + math.cos(a) * r, cy + math.sin(a) * r, N_MID)

    # Noyau lumineux
    core_r = 3.5 + charge * 1.5
    disc(img, cx, cy, core_r, N_CORE)
    disc(img, cx, cy, max(1, core_r - 1.5), N_CORE_B)
    glow(img, cx, cy, 9 + charge * 6, N_CORE, 0.30 + 0.30 * charge)

    # Oeil-lentille cyan (oriente par le spin : indique ou regarde le Gardien)
    put(img, cx + math.cos(spin) * 5, cy + math.sin(spin) * 5, N_CYAN)

    # Rupture a la mort : le bouclier eclate en fragments
    if broken > 0:
        for _ in range(int(broken * 30)):
            a = rnd.uniform(0, math.tau)
            r = 12 + rnd.uniform(0, 10) * broken
            put(img, cx + math.cos(a) * r, cy + math.sin(a) * r,
                (N_CORE[0], N_CORE[1], N_CORE[2], int(220 * (1 - broken))))
        for _ in range(int(broken * 24)):
            x = cx + rnd.randint(-9, 9)
            y = cy + rnd.randint(-9, 9)
            if rnd.random() < 0.5:
                put(img, x, y, (0, 0, 0, 0))

def gen_neon_warden(out, prefix="neon_warden"):
    n = {}
    for i in range(4):                                    # idle : noyau qui respire
        img = canvas()
        draw_neon_warden(img, spin=i * 0.4, charge=0.15 * math.sin(i * 1.6) + 0.15)
        save(img, f"{out}/{prefix}_idle_{i+1:02d}.png")
    n["idle"] = 4

    for i in range(6):                                    # move : l'oeil balaie
        img = canvas()
        draw_neon_warden(img, spin=i * math.tau / 6, charge=0.2)
        save(img, f"{out}/{prefix}_move_{i+1:02d}.png")
    n["move"] = 6

    ch = [0.2, 0.55, 0.85, 1.0, 0.7, 0.25]                # attack : le noyau charge (invocation)
    for i in range(6):
        img = canvas()
        draw_neon_warden(img, spin=i * 0.8, charge=ch[i])
        if i == 3:
            glow(img, 24, 24, 18, N_CYAN, 0.40)
        save(img, f"{out}/{prefix}_attack_{i+1:02d}.png")
    n["attack"] = 6

    for i in range(10):                                   # death : le bouclier eclate
        img = canvas()
        d = i / 9.0
        draw_neon_warden(img, spin=d * 6, gap=d, charge=max(0.0, 1.0 - d), broken=d, seed=i)
        save(img, f"{out}/{prefix}_death_{i+1:02d}.png")
    n["death"] = 10
    return n

# ================================================================ .tres
def write_tres(folder, prefix, counts, speeds):
    """SpriteFrames referencant toutes les frames (meme format que generate_boss_sprites.py)."""
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
        anim_blocks.append('{\n'
                           f'"frames": [{", ".join(frames)}],\n'
                           f'"loop": {loop},\n'
                           f'"name": &"{anim}",\n'
                           f'"speed": {speeds[anim]:.1f}\n'
                           '}')
    lines.append(", ".join(anim_blocks))
    lines.append("]")
    path = os.path.join(ROOT, "assets", "sprites", "enemies", folder, f"{prefix}_frames.tres")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("  .tres ecrit :", path)

# ================================================================ main
MIDBOSSES = {
    "molten_colossus": (gen_molten_colossus, {"idle": 4.0, "move": 7.0, "attack": 11.0, "death": 11.0}),
    "cryo_sentinel":   (gen_cryo_sentinel,   {"idle": 5.0, "move": 8.0, "attack": 12.0, "death": 12.0}),
    "neon_warden":     (gen_neon_warden,     {"idle": 5.0, "move": 9.0, "attack": 12.0, "death": 12.0}),
}

def main():
    only = None
    for a in sys.argv[1:]:
        if a.startswith("--only="):
            only = a.split("=", 1)[1]

    for mid_id, (gen, speeds) in MIDBOSSES.items():
        if only not in (None, mid_id):
            continue
        out = os.path.join(ROOT, "assets", "sprites", "enemies", mid_id)
        print(f"{mid_id}...")
        counts = gen(out)
        write_tres(mid_id, mid_id, counts, speeds)

    print("Termine. Penser a : godot --headless --import")

if __name__ == "__main__":
    main()
