"""Assemble les captures d'UI en une planche-contact, pour juger l'ensemble d'un coup d'oeil.

Usage :
    python tools/ui_contact_sheet.py before            # planche d'un seul jeu
    python tools/ui_contact_sheet.py before after      # comparaison par paires (avant | apres)

Sortie : docs/ui_sheet_<prefixe>.png (ou docs/ui_sheet_<a>_vs_<b>.png).
Chaque vignette est legendee avec le nom de l'ecran.
"""
import os
import sys

from PIL import Image, ImageDraw

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = os.path.join(PROJ, "docs")

THUMB_W = 520          # largeur d'une vignette
LABEL_H = 22
PAD = 8
BG = (18, 18, 30)
FG = (217, 217, 242)


def load(prefix, name):
    path = os.path.join(DOCS, f"ui_{prefix}_{name}.png")
    return Image.open(path).convert("RGB") if os.path.exists(path) else None


def thumb(img):
    ratio = THUMB_W / img.width
    return img.resize((THUMB_W, max(1, int(img.height * ratio))), Image.LANCZOS)


def main():
    prefixes = sys.argv[1:] or ["before"]
    names = sorted({
        f[len("ui_"):-4].split("_", 1)[1]
        for f in os.listdir(DOCS)
        if f.startswith(f"ui_{prefixes[0]}_") and f.endswith(".png")
    })
    if not names:
        print(f"aucune capture ui_{prefixes[0]}_*.png dans docs/")
        return 1

    cells = []
    for name in names:
        row = [load(p, name) for p in prefixes]
        if any(img is None for img in row):
            print(f"   (ignore {name} : manquant dans un des jeux)")
            continue
        cells.append((name, [thumb(img) for img in row]))

    if not cells:
        print("rien a assembler")
        return 1

    cell_w = THUMB_W * len(prefixes) + PAD * (len(prefixes) - 1)
    cell_h = max(t.height for _, row in cells for t in row) + LABEL_H
    cols = 2 if len(cells) > 3 else 1
    rows = (len(cells) + cols - 1) // cols

    sheet = Image.new("RGB", (cols * cell_w + PAD * (cols + 1),
                              rows * cell_h + PAD * (rows + 1)), BG)
    draw = ImageDraw.Draw(sheet)

    for i, (name, row) in enumerate(cells):
        cx = PAD + (i % cols) * (cell_w + PAD)
        cy = PAD + (i // cols) * (cell_h + PAD)
        legend = name if len(prefixes) == 1 else f"{name}   ({'  |  '.join(prefixes)})"
        draw.text((cx + 2, cy + 4), legend, fill=FG)
        for j, t in enumerate(row):
            sheet.paste(t, (cx + j * (THUMB_W + PAD), cy + LABEL_H))

    tag = "_vs_".join(prefixes)
    out = os.path.join(DOCS, f"ui_sheet_{tag}.png")
    sheet.save(out)
    print("SAVED", out, sheet.size, f"({len(cells)} ecrans)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
