"""Genere l'icone d'application de Chimera Protocol (executable Windows).

Motif : tete de chimere fendue en deux au centre d'une plaque blindee chanfreinee.
  - moitie gauche  = machine  (plaque acier, ailette angulaire, visiere CYAN)
  - moitie droite  = organique (chair violette, corne recourbee, oeil VIOLET)
  - couture centrale OR = la greffe, cicatrice de l'Assimilation
La plaque reprend le langage des cadres d'UI (chanfreins + bevel + rivets,
docs/ART_BRIEF_UI_FRAMES.md) et la palette de CLAUDE.md (via src/UI/UiPalette.cs).
Toutes les faces (highlight/base/shadow/contact) sont derivees par
pseudo3d_lib.shade() — jamais de couleur plate ad hoc (docs/ART_BRIEF_PSEUDO3D.md).

Trois niveaux de detail — un 256 px reduit a 16 px devient une bouillie illisible :
  - "full"  (>= 48 px) : rivets, crocs, grille d'aeration, tout le modele
  - "small" (32 px)    : motif epure et grossi, yeux et couture epaissis
  - "tiny"  (<= 24 px) : silhouette + 2 fentes lumineuses + couture fine, sans halo
    dore (a cette taille le halo mangeait un tiers du visage)

Sorties :
  unity/Assets/Art/branding/icon.png (256) — c'est CE fichier que ProjectSettings
              designe par GUID pour l'icone de l'executable Windows. Unity ne lit pas
              de .ico : il derive lui-meme les tailles a la construction, et un .ico
              pose a cote ne serait embarque nulle part.
  --sheet   : planche de controle multi-tailles -> docs/ui_sheet_app_icon.png
              (verification visuelle, gitignoree)

Usage : python tools/generate_app_icon.py [--sheet]
"""
import os
import struct
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pseudo3d_lib as _p3d

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))

# Resolution de travail : tout est dessine ici puis reduit (antialiasing x4 pour le 256).
M = 1024

# --- palette (miroir de src/UI/UiPalette.cs — ne pas inventer de teinte) ------ #
CYAN = (0x44, 0xFF, 0xEE)
VIOLET = (0xAA, 0x44, 0xFF)
GOLD = (0xFF, 0xCC, 0x44)
OFF_WHITE = (0xD9, 0xD9, 0xF2)
STEEL = (0x24, 0x24, 0x40)          # fill de plaque
STEEL_HL = _p3d.shade(STEEL, "highlight")
STEEL_SH = _p3d.shade(STEEL, "shadow")
STEEL_CT = _p3d.shade(STEEL, "contact")

# Base des deux moities de la tete. Le cote machine tire vers le bleu acier clair
# (lisible sur la plaque sombre), le cote organique vers le violet de la charte.
MACHINE = (0xB6, 0xC2, 0xE0)
ORGANIC = (0x63, 0x30, 0x96)        # assombri vs le violet de charte : sans cela
                                    # l'oeil violet ne ressortait pas de la chair
INK = (0x0A, 0x0A, 0x14)            # trait de contour / creux de bouche


def _lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def _smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def _px(pts):
    """Coordonnees normalisees (0..1) -> pixels de la toile de travail."""
    return [(x * M, y * M) for x, y in pts]


def _layer():
    return Image.new("RGBA", (M, M), (0, 0, 0, 0))


def _mask_poly(polys):
    """Masque L depuis une liste de polygones normalises."""
    mask = Image.new("L", (M, M), 0)
    d = ImageDraw.Draw(mask)
    for pts in polys:
        d.polygon(_px(pts), fill=255)
    return mask


def _glow(polys, color, radius, alpha):
    """Halo neon : silhouette floutee posee SOUS le motif."""
    lay = _layer()
    d = ImageDraw.Draw(lay)
    for pts in polys:
        d.polygon(_px(pts), fill=color + (alpha,))
    return lay.filter(ImageFilter.GaussianBlur(radius * M))


def _vshade(mask, base, y0, y1, top="highlight", bottom="shadow"):
    """Colore un masque par un degrade vertical highlight -> base -> shadow (§2/§5).

    Reproduit la regle du brief pseudo-3D (haut d'un volume eclaire, bas en ombre)
    sans redecouper la silhouette : la tete est un volume unique vu de face.
    """
    c_top = _p3d.shade(base, top)
    # amplitude d'ombre temperee (75 %) : a pleine amplitude le bas du crane
    # tombait au niveau de la plaque et la machoire disparaissait.
    c_bot = _lerp(base, _p3d.shade(base, bottom), 0.75)
    ramp = _layer()
    d = ImageDraw.Draw(ramp)
    ya, yb = int(y0 * M), int(y1 * M)
    span = max(1, yb - ya)
    for y in range(ya, yb + 1):
        t = (y - ya) / span
        if t < 0.42:
            c = _lerp(c_top, base, _smooth(t / 0.42))
        else:
            c = _lerp(base, c_bot, _smooth((t - 0.42) / 0.58))
        d.line([(0, y), (M, y)], fill=c + (255,))
    out = _layer()
    out.paste(ramp, (0, 0), mask)
    return out


def _split(img, side):
    """Ne garde que la moitie gauche ("l") ou droite ("r") d'un calque."""
    mask = Image.new("L", (M, M), 0)
    d = ImageDraw.Draw(mask)
    if side == "l":
        d.rectangle([0, 0, M // 2 - 1, M], fill=255)
    else:
        d.rectangle([M // 2, 0, M, M], fill=255)
    out = _layer()
    out.paste(img, (0, 0), mask)
    return out


def _taper(p0, ctrl, p1, w0, w1, steps=28):
    """Polygone effile le long d'une bezier quadratique (cornes, appendices)."""
    left, right = [], []
    for i in range(steps + 1):
        t = i / steps
        x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * ctrl[0] + t ** 2 * p1[0]
        y = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * ctrl[1] + t ** 2 * p1[1]
        dx = 2 * (1 - t) * (ctrl[0] - p0[0]) + 2 * t * (p1[0] - ctrl[0])
        dy = 2 * (1 - t) * (ctrl[1] - p0[1]) + 2 * t * (p1[1] - ctrl[1])
        n = max(1e-6, (dx * dx + dy * dy) ** 0.5)
        nx, ny = -dy / n, dx / n
        w = (w0 + (w1 - w0) * t) / 2
        left.append((x + nx * w, y + ny * w))
        right.append((x - nx * w, y - ny * w))
    return left + right[::-1]


# --------------------------------------------------------------------------- #
# Geometrie du motif
# --------------------------------------------------------------------------- #
def _plate(inset, chamfer):
    return [
        (inset + chamfer, inset), (1 - inset - chamfer, inset),
        (1 - inset, inset + chamfer), (1 - inset, 1 - inset - chamfer),
        (1 - inset - chamfer, 1 - inset), (inset + chamfer, 1 - inset),
        (inset, 1 - inset - chamfer), (inset, inset + chamfer),
    ]


# Recentrage vertical du motif : le crane seul laissait un vide sous le menton.
HEAD_DY = 0.022


def _s(pts, scale):
    """Homothetie autour du centre de la toile (`scale` grossit le motif) + recentrage."""
    return [(0.5 + (x - 0.5) * scale, 0.5 + (y - 0.5) * scale + HEAD_DY) for x, y in pts]


def _head(scale):
    """Crane casque vu de face : cranium large, pommettes marquees, menton court.

    Volontairement PAS effile en V — la machoire pointue de la 1re passe lisait
    comme un crane de bovide.
    """
    return _s([
        (0.500, 0.212), (0.590, 0.226), (0.664, 0.282), (0.700, 0.382),
        (0.702, 0.478), (0.672, 0.566), (0.628, 0.652), (0.588, 0.742),
        (0.412, 0.742), (0.372, 0.652), (0.328, 0.566), (0.298, 0.478),
        (0.300, 0.382), (0.336, 0.282), (0.410, 0.226),
    ], scale)


def _horn(scale):
    """Corne organique — UNE SEULE, a droite : l'asymetrie EST le motif chimere."""
    return _s(_taper((0.648, 0.308), (0.792, 0.288), (0.788, 0.142), 0.118, 0.014), scale)


def _crest(scale):
    """Plaque de tempe machine (gauche) : angulaire, plaquee au crane, jamais dressee.

    Contrepoint PLAT de la corne : c'est le contraste corne/plaque qui dit "hybride" ;
    deux appendices dresses en miroir lisaient comme un crane de bovide.
    """
    return _s([(0.336, 0.272), (0.238, 0.328), (0.226, 0.442),
               (0.284, 0.506), (0.306, 0.442), (0.302, 0.336)], scale)


def _eyes(scale, thick):
    """Deux fentes lumineuses de meme masse : visiere CYAN a gauche, oeil VIOLET a droite.

    Deux barres claires de force egale = la seule information qui survive a 16 px.
    La 1re passe donnait un oeil organique en amande large : noye dans la chair
    violette, il ne lisait ni comme un oeil ni comme une lueur.
    Le caractere "hybride" est porte par la corne, la crete et la couture — pas
    par la forme des yeux.
    """
    # Coins internes plus BAS que les externes : lecture predatrice (sourcil fronce).
    left = [(0.328, 0.402), (0.474, 0.436), (0.474, 0.436 + thick), (0.328, 0.402 + thick)]
    right = [(0.526, 0.436), (0.672, 0.402), (0.672, 0.402 + thick), (0.526, 0.436 + thick)]
    return _s(left, scale), _s(right, scale)


def _fangs(scale):
    """Bouche : creux sombre + 3 crocs clairs (detail "full" uniquement)."""
    mouth = [(0.418, 0.604), (0.582, 0.604), (0.560, 0.700), (0.440, 0.700)]
    fangs = [
        [(0.442, 0.610), (0.482, 0.610), (0.464, 0.690)],
        [(0.487, 0.610), (0.517, 0.610), (0.502, 0.700)],
        [(0.522, 0.610), (0.558, 0.610), (0.538, 0.690)],
    ]
    return _s(mouth, scale), [_s(t, scale) for t in fangs]


# --------------------------------------------------------------------------- #
# Rendu
# --------------------------------------------------------------------------- #
def render(detail):
    full = detail == "full"
    tiny = detail == "tiny"          # 16-24 px : que la silhouette et les lueurs
    scale = 1.0 if full else (1.18 if tiny else 1.12)
    img = _layer()

    # --- 1. plaque blindee : contact -> fill degrade -> bevel -------------- #
    inset = 0.030
    chamfer = 0.185
    plate = _plate(inset, chamfer)
    d = ImageDraw.Draw(img)
    d.polygon(_px(_plate(inset * 0.35, chamfer)), fill=STEEL_CT + (255,))

    grad = _layer()
    gd = ImageDraw.Draw(grad)
    for y in range(M):
        t = y / (M - 1)
        gd.line([(0, y), (M, y)], fill=_lerp(STEEL, _p3d.shade(STEEL, "shadow"), _smooth(t)) + (255,))
    img.paste(grad, (0, 0), _mask_poly([plate]))

    # bevel : cotes eclaires (haut/gauche) vs ombres (bas/droite), lumiere haut-gauche
    bevel = max(2, int(0.020 * M))
    for i in range(len(plate)):
        a, b = plate[i], plate[(i + 1) % len(plate)]
        mid = ((a[0] + b[0]) / 2 - 0.5, (a[1] + b[1]) / 2 - 0.5)
        s = mid[0] + mid[1]
        col = STEEL_HL if s < -0.08 else STEEL_SH if s > 0.08 else STEEL
        d.line(_px([a, b]), fill=col + (255,), width=bevel)

    # --- 2. rivets d'angle (langage des cadres d'UI) ---------------------- #
    if full:
        r = 0.020
        for cx, cy in ((0.5 - 0.315, 0.5 - 0.315), (0.5 + 0.315, 0.5 - 0.315),
                       (0.5 - 0.315, 0.5 + 0.315), (0.5 + 0.315, 0.5 + 0.315)):
            box = [((cx - r) * M, (cy - r) * M), ((cx + r) * M, (cy + r) * M)]
            d.ellipse(box, fill=_p3d.shade(GOLD, "shadow") + (255,))
            k = r * 0.55
            d.ellipse([((cx - k) * M, (cy - k) * M), ((cx + k) * M, (cy + k) * M)],
                      fill=_p3d.shade(GOLD, "highlight") + (255,))

    # --- 3. halos neon derriere la tete ----------------------------------- #
    head = _head(scale)
    horn = _horn(scale)
    crest = _crest(scale)
    img.alpha_composite(_glow([crest], CYAN, 0.030, 150))
    img.alpha_composite(_glow([horn], VIOLET, 0.030, 150))

    # --- 4. tete bicolore, ombree par degrade vertical -------------------- #
    # Les appendices portent une teinte PLUS SOMBRE que le crane : sans cela ils
    # se noient dans la meme masse coloree et la corne lisait comme une meche.
    body = _mask_poly([head])
    y0, y1 = 0.5 - 0.29 * scale, 0.5 + 0.27 * scale

    def _ink(polys, width):
        m = Image.new("L", (M, M), 0)
        od = ImageDraw.Draw(m)
        for pts in polys:
            od.line(_px(pts + [pts[0]]), fill=255, width=width, joint="curve")
        lay = _layer()
        lay.paste(Image.new("RGBA", (M, M), INK + (255,)), (0, 0), m)
        return lay

    w = max(2, int((0.016 if full else 0.026 if tiny else 0.024) * M))
    # rim externe sous les remplissages : ancre la silhouette sur la plaque
    img.alpha_composite(_ink([head, horn, crest], int(w * 1.9)))
    img.alpha_composite(_vshade(_mask_poly([crest]), _p3d.shade(MACHINE, "shadow"), y0, y1))
    img.alpha_composite(_vshade(_mask_poly([horn]), _p3d.shade(ORGANIC, "shadow"), y0, y1))
    img.alpha_composite(_split(_vshade(body, MACHINE, y0, y1), "l"))
    img.alpha_composite(_split(_vshade(body, ORGANIC, y0, y1), "r"))
    # trait fin PAR-DESSUS : separe crane / corne / crete, sinon tout fusionne
    img.alpha_composite(_ink([head, horn, crest], w))

    dd = ImageDraw.Draw(img)

    # Joue machine : grille d'aeration (3 fentes horizontales alignees). Les
    # diagonales de la 1re passe lisaient comme des griffures, pas comme du metal.
    if full:
        vent = _p3d.shade(STEEL, "highlight")
        lw = max(2, int(0.014 * M))
        for i, y in enumerate((0.548, 0.582, 0.616)):
            x1 = 0.452 - i * 0.014
            dd.line(_px(_s([(0.376 + i * 0.010, y), (x1, y)], scale)),
                    fill=vent + (235,), width=lw)
        # Joue organique : chair plus claire (volume), sur le MEME degrade que le
        # crane — un degrade local desaligne creusait un patch sombre sous l'oeil.
        cheek = _mask_poly([_s([(0.556, 0.492), (0.646, 0.512), (0.634, 0.596),
                                (0.560, 0.606)], scale)])
        img.alpha_composite(_vshade(cheek, _p3d.shade(ORGANIC, "base_light"), y0, y1))
        dd = ImageDraw.Draw(img)

    # --- 5. bouche / crocs (full) ----------------------------------------- #
    if full:
        mouth, fangs = _fangs(scale)
        dd.polygon(_px(mouth), fill=INK + (235,))
        for t in fangs:
            dd.polygon(_px(t), fill=OFF_WHITE + (245,))

    # --- 6. couture d'Assimilation : trait or au milieu, clippe a la tete -- #
    seam = _layer()
    sd = ImageDraw.Draw(seam)
    sw = (0.018 if full else 0.020 if tiny else 0.028) * M
    sd.rectangle([M / 2 - sw / 2, y0 * M, M / 2 + sw / 2, y1 * M], fill=GOLD + (255,))
    clipped = _layer()
    clipped.paste(seam, (0, 0), body)
    if not tiny:  # a 16 px le halo dore etalait la couture sur un tiers du visage
        img.alpha_composite(clipped.filter(ImageFilter.GaussianBlur(0.006 * M)))
    img.alpha_composite(clipped)

    # --- 7. yeux : cyan (machine) / violet (organique) + bloom ------------ #
    thick = 0.052 if full else 0.082 if tiny else 0.070
    eye_l, eye_r = _eyes(scale, thick)
    # Orbite : trait sombre qui borde chaque fente. Sans lui l'oeil violet, de meme
    # teinte que la chair, se dissolvait dans la joue.
    img.alpha_composite(_ink([eye_l, eye_r], max(2, int(0.020 * M))))
    img.alpha_composite(_glow([eye_l], CYAN, 0.022, 210))
    img.alpha_composite(_glow([eye_r], VIOLET, 0.022, 210))
    ed = ImageDraw.Draw(img)
    ed.polygon(_px(eye_l), fill=CYAN + (255,))
    ed.polygon(_px(eye_r), fill=VIOLET + (255,))
    if full:  # coeur surexpose (jamais du blanc pur : teinte conservee)
        for pts, col in ((eye_l, CYAN), (eye_r, VIOLET)):
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            cx = (min(xs) + max(xs)) / 2
            cy = (min(ys) + max(ys)) / 2
            rx, ry = (max(xs) - min(xs)) * 0.28, (max(ys) - min(ys)) * 0.22
            ed.ellipse([((cx - rx)) * M, (cy - ry) * M, (cx + rx) * M, (cy + ry) * M],
                       fill=_p3d.shade(col, "highlight") + (255,))

    # --- 8. clip final : rien ne depasse de la plaque --------------------- #
    out = _layer()
    out.paste(img, (0, 0), _mask_poly([_plate(inset * 0.35, chamfer)]))
    return out


# --------------------------------------------------------------------------- #
# Sorties
# --------------------------------------------------------------------------- #
SIZES = [(256, "full"), (128, "full"), (64, "full"), (48, "full"),
         (32, "small"), (24, "tiny"), (16, "tiny")]


def _sharpen_rgb(im, percent):
    """Recontraste APRES reduction, sur RGB seulement.

    Applique a l'alpha, le masque flou frangeait les bords de pixels verts/ocres
    (le halo cyan et la couture or se melangeaient a la transparence).
    """
    r, g, b, a = im.split()
    rgb = Image.merge("RGB", (r, g, b)).filter(
        ImageFilter.UnsharpMask(radius=1.0, percent=percent, threshold=2))
    return Image.merge("RGBA", rgb.split() + (a,))


def variants():
    masters = {d: render(d) for d in ("full", "small", "tiny")}
    out = {}
    for size, detail in SIZES:
        im = masters[detail].resize((size, size), Image.LANCZOS)
        if 24 <= size <= 48:  # la reduction ramollit les aretes
            im = _sharpen_rgb(im, 60)
        out[size] = im
    return out


def write_ico(images, path):
    """ICO multi-resolution a entrees PNG (supportees par Windows Vista+).

    Ecrit a la main : PIL ne sait pas empiler des rendus DIFFERENTS par taille
    (il rescale une image unique, ce qui annule le niveau de detail "small").
    """
    blobs = []
    for size in sorted(images, reverse=True):
        import io
        buf = io.BytesIO()
        images[size].save(buf, format="PNG")
        blobs.append((size, buf.getvalue()))
    n = len(blobs)
    header = struct.pack("<HHH", 0, 1, n)
    offset = 6 + 16 * n
    entries, data = b"", b""
    for size, blob in blobs:
        dim = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
        data += blob
    with open(path, "wb") as f:
        f.write(header + entries + data)
    print(f"{os.path.relpath(path, ROOT)}  ({n} tailles : {', '.join(str(s) for s, _ in blobs)})")


def write_sheet(images, path):
    """Planche de controle : chaque taille rendue telle quelle + agrandie x8 nearest."""
    pad, cell = 12, 8
    cols = [(s, images[s]) for s in sorted(images)]
    w = pad + sum(min(s * cell, 256) + pad for s, _ in cols)
    h = pad * 3 + 256 + 32
    sheet = Image.new("RGB", (w, h), (0x1A, 0x1A, 0x2E))
    x = pad
    for s, im in cols:
        zoom = min(s * cell, 256)
        big = im.resize((zoom, zoom), Image.NEAREST)
        sheet.paste(big, (x, pad + (256 - zoom) // 2), big)
        sheet.paste(im, (x, pad * 2 + 256), im)
        x += zoom + pad
    sheet.save(path)
    print(os.path.relpath(path, ROOT))


def main():
    images = variants()

    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import unity_paths
    target = unity_paths.ART / "branding" / "icon.png"
    target.parent.mkdir(parents=True, exist_ok=True)
    images[256].save(target)
    print(f"{target.relative_to(unity_paths.REPO_ROOT)}  (256, icone de l'executable)")
    if "--sheet" in sys.argv:
        write_sheet(images, os.path.join(ROOT, "docs", "ui_sheet_app_icon.png"))


if __name__ == "__main__":
    main()
