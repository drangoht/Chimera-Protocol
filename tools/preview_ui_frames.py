"""Planche d'apercu des cadres 9-slice, agrandis en NEAREST pour juger le pixel.

Usage : python tools/preview_ui_frames.py [zoom]   (zoom par defaut : 8)
Sortie : docs/ui_frames_preview.png
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import unity_paths

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FRAMES = str(unity_paths.sprite_dir("ui/frames"))
ZOOM = int(sys.argv[1]) if len(sys.argv) > 1 else 8

SHOW = [
    "ui_frame_button_violet.png",
    "ui_frame_button_violet_focus.png",
    "ui_frame_button_cyan.png",
    "ui_frame_button_danger.png",
    "ui_frame_button_disabled.png",
    "ui_frame_card_epic.png",
    "ui_frame_card_epic_focus.png",
    "ui_frame_popup_violet.png",
]

PAD = 12
LABEL_H = 16
BG = (26, 26, 46)

tiles = []
for name in SHOW:
    path = os.path.join(FRAMES, name)
    if not os.path.exists(path):
        print("manquant :", name)
        continue
    img = Image.open(path).convert("RGBA")
    big = img.resize((img.width * ZOOM, img.height * ZOOM), Image.NEAREST)
    tiles.append((name.replace("ui_frame_", "").replace(".png", ""), big))

cols = 4
rows = (len(tiles) + cols - 1) // cols
cw = max(t.width for _, t in tiles) + PAD
ch = max(t.height for _, t in tiles) + PAD + LABEL_H

sheet = Image.new("RGBA", (cols * cw + PAD, rows * ch + PAD), BG + (255,))
draw = ImageDraw.Draw(sheet)
for i, (label, tile) in enumerate(tiles):
    x = PAD + (i % cols) * cw
    y = PAD + (i // cols) * ch
    draw.text((x, y), label, fill=(217, 217, 242))
    sheet.alpha_composite(tile, (x, y + LABEL_H))

out = os.path.join(PROJ, "docs", "ui_frames_preview.png")
sheet.convert("RGB").save(out)
print("SAVED", out, sheet.size, f"zoom x{ZOOM}")
