"""
extract_hud_assets.py
Chimera Protocol — Etape 1 : analyse de l'image concept HUD
Affiche les dimensions et un dump des zones de couleur dominante.
"""

import os
import sys
import math
from PIL import Image
import numpy as np

SCRIPT_DIR  = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.dirname(SCRIPT_DIR)
SRC_IMG     = os.path.join(PROJECT_DIR, "idea", "idee_hud_chimera_core.png")
OUT_DIR     = os.path.join(PROJECT_DIR, "assets", "sprites", "ui")

os.makedirs(OUT_DIR, exist_ok=True)


# ---------------------------------------------------------------------------
# Etape 1 — dimensions et dump régions
# ---------------------------------------------------------------------------

def analyse_image(path: str) -> Image.Image:
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    print(f"=== IMAGE SOURCE ===")
    print(f"  Fichier : {path}")
    print(f"  Dimensions : {w} x {h} px")
    print(f"  Mode : {img.mode}")

    arr = np.array(img)

    # Scanne une grille 8x4 pour identifier les zones de couleur dominante
    grid_cols, grid_rows = 8, 4
    cell_w = w // grid_cols
    cell_h = h // grid_rows

    print(f"\n=== DUMP COULEURS DOMINANTES (grille {grid_cols}x{grid_rows}) ===")
    print(f"  (chaque cellule = {cell_w}x{cell_h} px)")
    print(f"  Format : col,row -> R,G,B (A) hex")

    for row in range(grid_rows):
        for col in range(grid_cols):
            x0 = col * cell_w
            y0 = row * cell_h
            x1 = x0 + cell_w
            y1 = y0 + cell_h
            region = arr[y0:y1, x0:x1]
            # Couleur médiane (plus robuste que moyenne contre les artefacts)
            median_r = int(np.median(region[:, :, 0]))
            median_g = int(np.median(region[:, :, 1]))
            median_b = int(np.median(region[:, :, 2]))
            median_a = int(np.median(region[:, :, 3]))
            hex_col = f"#{median_r:02X}{median_g:02X}{median_b:02X}"
            print(f"  [{col},{row}] → {median_r:3d},{median_g:3d},{median_b:3d} (a={median_a}) {hex_col}")
        print()

    # Pixels cyan détectés (couleur UI signature : R<100, G>150, B>180)
    cyan_mask = (arr[:,:,0] < 100) & (arr[:,:,1] > 150) & (arr[:,:,2] > 180)
    cyan_count = int(cyan_mask.sum())
    print(f"  Pixels cyan UI (#44FFEE-like) : {cyan_count}")

    # Pixels violets détectés (R>100, G<100, B>150)
    purple_mask = (arr[:,:,0] > 100) & (arr[:,:,1] < 100) & (arr[:,:,2] > 150)
    purple_count = int(purple_mask.sum())
    print(f"  Pixels violets UI (#AA44FF-like) : {purple_count}")

    # Pixels sombres (fond) détectés (R<30, G<30, B<50)
    dark_mask = (arr[:,:,0] < 30) & (arr[:,:,1] < 30) & (arr[:,:,2] < 60)
    dark_count = int(dark_mask.sum())
    print(f"  Pixels sombres (fond navyblack) : {dark_count}")

    return img


# ---------------------------------------------------------------------------
# Etape 2 — extraction des éléments
# Coordonnées déduites de l'analyse visuelle de l'image concept
# (calibrées sur la résolution réelle, à ajuster si différente de 640x400)
# ---------------------------------------------------------------------------

def make_transparent(img_rgba: Image.Image, dark_thresh: int = 45) -> Image.Image:
    """
    Rend transparent le fond sombre (#000000 à environ #1A1A2E).
    Seuil : pixels où R<thresh ET G<thresh ET B<thresh+15.
    Conserve tous les pixels cyan/violet/blanc.
    """
    arr = np.array(img_rgba, dtype=np.uint8)
    r, g, b, a = arr[:,:,0], arr[:,:,1], arr[:,:,2], arr[:,:,3]

    dark = (r.astype(int) < dark_thresh) & \
           (g.astype(int) < dark_thresh) & \
           (b.astype(int) < dark_thresh + 15)

    arr[dark, 3] = 0
    return Image.fromarray(arr, "RGBA")


def crop_and_save(img: Image.Image, box: tuple, out_name: str,
                  remove_dark_bg: bool = False, note: str = "") -> bool:
    """
    Crop la région box=(x0,y0,x1,y1), optionnellement retire le fond sombre,
    sauvegarde dans OUT_DIR/out_name.
    Retourne False si la région est trop petite (< 12px dans une dimension).
    """
    x0, y0, x1, y1 = box
    w_crop = x1 - x0
    h_crop = y1 - y0

    if w_crop < 12 or h_crop < 12:
        print(f"  [SKIP] {out_name} — trop petit ({w_crop}x{h_crop} px) {note}")
        return False

    region = img.crop(box)

    if remove_dark_bg:
        region = make_transparent(region, dark_thresh=50)

    out_path = os.path.join(OUT_DIR, out_name)
    region.save(out_path)
    print(f"  [OK] {out_name}  ({w_crop}x{h_crop} px)  → {out_path}  {note}")
    return True


def resize_for_godot(img_rgba: Image.Image, target_size: tuple) -> Image.Image:
    """
    Redimensionne en conservant le ratio, en utilisant LANCZOS pour la qualite.
    target_size = (w, h) max. L'image est mise a l'echelle pour tenir dans ce rectangle.
    """
    w_src, h_src = img_rgba.size
    w_max, h_max = target_size
    ratio = min(w_max / w_src, h_max / h_src)
    new_w = max(1, int(w_src * ratio))
    new_h = max(1, int(h_src * ratio))
    return img_rgba.resize((new_w, new_h), Image.LANCZOS)


def crop_resize_save(img: Image.Image, box: tuple, out_name: str,
                     target_size: tuple, remove_dark_bg: bool = False,
                     dark_thresh: int = 50, note: str = "") -> bool:
    """
    Crop, optionnellement retire le fond sombre, redimensionne, sauvegarde.
    """
    x0, y0, x1, y1 = box
    w_crop = x1 - x0
    h_crop = y1 - y0

    if w_crop < 12 or h_crop < 12:
        print(f"  [SKIP] {out_name} — trop petit ({w_crop}x{h_crop} px)")
        return False

    region = img.crop(box)

    if remove_dark_bg:
        region = make_transparent(region, dark_thresh=dark_thresh)

    region = resize_for_godot(region, target_size)

    out_path = os.path.join(OUT_DIR, out_name)
    region.save(out_path)
    final_w, final_h = region.size
    print(f"  [OK] {out_name}  crop={w_crop}x{h_crop} -> {final_w}x{final_h} px  {note}")
    return True


def extract_elements(img: Image.Image) -> None:
    w, h = img.size
    print(f"\n=== EXTRACTION DES ELEMENTS (image {w}x{h}) ===")
    # Coordonnees calibrees sur l image reelle 3060x1408, verifiees visuellement
    # via diagnostics intermediaires.

    # -------------------------------------------------------------------------
    # 1. ui_panel_frame.png — cadre panneau gauche "HACKER RIG STATUS"
    #    Contient : barre HP, barre XP/MEMORY BUFFER, indicateur LV
    #    Coord reelles : x=4-1092, y=21-810 (tout le panneau gauche)
    #    Redimensionne a 300x240 pour NinePatch (marges ~8px)
    # -------------------------------------------------------------------------
    crop_resize_save(img, (4, 21, 1092, 810), "ui_panel_frame.png",
                     target_size=(300, 240),
                     remove_dark_bg=False,
                     note="(cadre panneau gauche complet — NinePatch 300x240)")

    crop_resize_save(img, (4, 21, 1092, 810), "ui_panel_frame_nobg.png",
                     target_size=(300, 240),
                     remove_dark_bg=True, dark_thresh=35,
                     note="(cadre seul fond transparent)")

    # -------------------------------------------------------------------------
    # 2. ui_lv_hex.png — indicateur hexagonal LV (remplace la version procedurale)
    #    L'hexagone LV1 avec fleche monte et contour double
    #    Coord reelles : x=840-1065, y=200-510
    #    Redimensionne a 64x64 (taille optimale pour affichage Godot)
    # -------------------------------------------------------------------------
    crop_resize_save(img, (848, 205, 1062, 500), "ui_lv_hex.png",
                     target_size=(64, 64),
                     remove_dark_bg=True, dark_thresh=40,
                     note="(hex LV — 64x64 fond transparent)")

    # -------------------------------------------------------------------------
    # 3. ui_hex_button_ping.png — bouton hexagonal PING avec icone ECG
    #    Coord reelles : x=290-535, y=555-785
    #    Redimensionne a 64x64
    # -------------------------------------------------------------------------
    crop_resize_save(img, (290, 555, 535, 785), "ui_hex_button_ping.png",
                     target_size=(64, 64),
                     remove_dark_bg=True, dark_thresh=35,
                     note="(bouton PING — 64x64)")

    # -------------------------------------------------------------------------
    # 4. ui_hex_button_firewall.png — bouton hexagonal FIREWALL avec icone bouclier
    #    Le FIREWALL est a droite du PING, x=430-730, y=555-785
    #    Redimensionne a 64x64
    # -------------------------------------------------------------------------
    crop_resize_save(img, (545, 555, 730, 785), "ui_hex_button_firewall.png",
                     target_size=(64, 64),
                     remove_dark_bg=True, dark_thresh=35,
                     note="(bouton FIREWALL — 64x64)")

    # -------------------------------------------------------------------------
    # 5. ui_chimera_core_icon.png — icone hexagonale violette CHIMERA CORE
    #    Hexagone violet avec circuit imprime integre, x=2350-2750, y=50-370
    #    Redimensionne a 64x64
    # -------------------------------------------------------------------------
    crop_resize_save(img, (2370, 55, 2740, 365), "ui_chimera_core_icon.png",
                     target_size=(64, 64),
                     remove_dark_bg=True, dark_thresh=30,
                     note="(icone CHIMERA CORE violet — 64x64)")

    # -------------------------------------------------------------------------
    # 6. ui_timer_frame.png — cadre du timer LCD avec les crochets lateraux
    #    Le cadre gris avec accents teal sur les cotes, x=1310-1720, y=20-220
    #    Redimensionne a 180x80 (ratio original preserved)
    # -------------------------------------------------------------------------
    crop_resize_save(img, (1310, 20, 1720, 215), "ui_timer_frame.png",
                     target_size=(180, 80),
                     remove_dark_bg=False,
                     note="(cadre timer LCD — 180x80)")

    crop_resize_save(img, (1310, 20, 1720, 215), "ui_timer_frame_nobg.png",
                     target_size=(180, 80),
                     remove_dark_bg=True, dark_thresh=40,
                     note="(cadre timer fond transparent)")

    # -------------------------------------------------------------------------
    # 7. ui_hex_button_frame.png — cadre hexagonal generique sans icone
    #    On extrait l outline du bouton PING en retirant l icone
    #    (utile comme template NinePatch pour les slots d arme du HUD futur)
    # -------------------------------------------------------------------------
    crop_resize_save(img, (290, 555, 535, 785), "ui_hex_button_frame.png",
                     target_size=(64, 64),
                     remove_dark_bg=True, dark_thresh=55,
                     note="(frame hex generique — seuil haut pour garder contour)")


# ---------------------------------------------------------------------------
# Point d'entrée
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    if not os.path.exists(SRC_IMG):
        print(f"ERREUR: image source introuvable : {SRC_IMG}")
        sys.exit(1)

    img = analyse_image(SRC_IMG)
    extract_elements(img)

    print(f"\n=== Terminé — assets dans {OUT_DIR} ===")
