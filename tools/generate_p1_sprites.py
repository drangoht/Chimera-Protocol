"""
generate_p1_sprites.py — Sprites P1 de Chimera Protocol (Phase 4).
Produit les 5 sprites P1 definis dans ARENA_DA_BRIEF.md §2 et §4 :

  Obstacles (§2) :
    tile_crate_tech.png        32x40 px  #4A5566 + bande #4A2A15
    tile_arch_fallen.png       96x32 px  #252028 + mousses #3A3A28

  VFX particules (§4) :
    vfx_particle_aether_ambient.png   3x3 px   #00A0BB (bloom trigger)
    vfx_particle_impact_plasma.png    2x2 px   #FFD700
    vfx_particle_impact_sentinel.png  2x2 px   #FF6644

Ce script delegue aux generateurs de generate_arena_obstacles.py (meme palette,
meme logique pixel-art) sans dupliquer de code.

Usage :
  C:\\Users\\drang\\AppData\\Local\\Programs\\Python\\Python313\\python.exe tools/generate_p1_sprites.py
"""

import os
import sys

# Ajouter tools/ au path pour l'import
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.dirname(SCRIPT_DIR)
sys.path.insert(0, SCRIPT_DIR)

import generate_arena_obstacles as gao

OUT_ENV = os.path.join(PROJECT_ROOT, "assets", "sprites", "environment")
OUT_VFX = os.path.join(PROJECT_ROOT, "assets", "sprites", "vfx")


def main():
    print("=== generate_p1_sprites.py — Phase 4 Chimera Protocol ===")
    print(f"Racine projet : {PROJECT_ROOT}")
    print()

    for d in [OUT_ENV, OUT_VFX]:
        os.makedirs(d, exist_ok=True)

    # ── Obstacles P1 (ARENA_DA_BRIEF.md §2, obstacles C et D) ────────────────
    print("--- Obstacles P1 ---")
    gao.gen_crate_tech(OUT_ENV)    # 32x40 px
    gao.gen_arch_fallen(OUT_ENV)   # 96x32 px

    # ── VFX particules P1 (ARENA_DA_BRIEF.md §4, effets 2 et 3) ─────────────
    print()
    print("--- VFX particules P1 ---")
    gao.gen_particle_aether_ambient(OUT_VFX)   # 3x3 px #00A0BB
    gao.gen_particle_2x2(OUT_VFX, "vfx_particle_impact_plasma.png",
                         gao.C_PLASMA_EDGE)     # 2x2 px #FFD700
    gao.gen_particle_2x2(OUT_VFX, "vfx_particle_impact_sentinel.png",
                         gao.C_SURCHARGE_P)     # 2x2 px #FF6644

    # ── Verification post-generation ─────────────────────────────────────────
    print()
    print("--- Verification ---")
    from PIL import Image
    specs = [
        (os.path.join(OUT_ENV, "tile_crate_tech.png"),              32, 40),
        (os.path.join(OUT_ENV, "tile_arch_fallen.png"),             96, 32),
        (os.path.join(OUT_VFX, "vfx_particle_aether_ambient.png"),   3,  3),
        (os.path.join(OUT_VFX, "vfx_particle_impact_plasma.png"),    2,  2),
        (os.path.join(OUT_VFX, "vfx_particle_impact_sentinel.png"),  2,  2),
    ]
    all_ok = True
    for path, ew, eh in specs:
        size = os.path.getsize(path)
        img = Image.open(path)
        w, h = img.size
        ok = (w == ew and h == eh and size > 0 and img.mode == "RGBA")
        status = "OK" if ok else "ERREUR"
        rel = os.path.relpath(path, PROJECT_ROOT)
        print(f"  [{status}] {rel}  {w}x{h} px  {size} octets")
        if not ok:
            all_ok = False

    print()
    if all_ok:
        print("Resultat : TOUS LES SPRITES P1 SONT VALIDES")
    else:
        print("Resultat : ERREURS DETECTEES — verifier les logs ci-dessus")
        sys.exit(1)


if __name__ == "__main__":
    main()
