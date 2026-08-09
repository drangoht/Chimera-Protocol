#!/usr/bin/env python3
"""Régénère la galerie de la page itch.io depuis le binaire Unity.

Pourquoi cet outil existe
-------------------------
Les captures de `docs/store_screens/` dataient du moteur Godot : elles montraient une interface qui
n'existe plus, des écrans disparus (sélection de personnage) et un rendu que le portage a changé.
Une galerie qui ne décrit pas le jeu qu'on télécharge est pire qu'une galerie absente — elle promet
autre chose.

Ce script rejoue la tournée de captures du jeu (`--screenshots`) une fois par biome, puis range les
images sous les noms attendus par `docs/ITCH_STORE_PAGE.md`. Il est reproductible : la prochaine
version se recapture d'une commande, au lieu de dépendre de ce dont on se souvient d'avoir pris.

⚠ Le jeu doit tourner AVEC un rendu : en `-batchmode -nographics` les images seraient noires. La
fenêtre s'ouvre donc réellement, et la machine doit rester disponible pendant la tournée.

⚠ La tournée pose une progression de VITRINE en mémoire (Codex complet, biomes ouverts). Sans elle,
une installation neuve photographie un jeu vide — exact, et inutilisable. Rien n'est écrit sur
disque.

Usage
-----
    py tools/capture_store.py                 # les 5 biomes, galerie complète
    py tools/capture_store.py --biomes neon   # un seul, pour retoucher une image
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import time
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "unity" / "Build" / "game" / "ChimeraProtocol.exe"
OUT = ROOT / "docs" / "store_screens"

# Durée d'une tournée, en secondes réelles. L'intro se joue sur une installation neuve (six plans),
# la scène de run est jouée quelques secondes, et chaque cliché attend que l'écran soit stable :
# couper trop tôt donne une galerie tronquée sans le dire.
TOUR_SECONDS = 90

BIOMES = ["sanctuaire", "aether", "fournaise", "givre", "neon"]

# Ce que la tournée produit → le nom attendu par la page store. Les écrans (menu, Codex, Hub…) ne
# sont pris qu'UNE fois : ils ne dépendent pas du biome, et cinq exemplaires identiques ne
# vaudraient que cinq fois plus d'attente.
#
# ⚠ Les clichés sont désignés par leur NOM, pas par leur numéro. La tournée les préfixe d'un compteur
# (« 06-codex-arsenal.png »), et se fier à ce numéro fait tout casser dès qu'une capture est insérée
# au milieu — ce qui vient d'arriver : sept images se sont retrouvées « absentes » alors qu'elles
# avaient toutes été prises, simplement décalées de deux rangs.
SHARED = {
    "menu": "menu.png",
    "hub": "hub.png",
    "niveaux": "levelsel.png",
    "options": "options.png",
    "codex": "bestiary.png",
    "codex-arsenal": "arsenal.png",
    "codex-chimere": "chimera_codex.png",
    "defis": "challenges.png",
    "montee-de-niveau": "levelup.png",
    "fusions": "hud_fusion.png",
    "chimere-carapace": "chimera_body.png",
}

# Une image de jeu par biome : c'est là que le portage se voit le mieux (sols, brumes, faune).
PER_BIOME = "run-2"


def find_shot(shots_dir: Path, name: str) -> Path | None:
    """Cliché portant ce nom, quel que soit le numéro que la tournée lui a donné."""
    matches = sorted(shots_dir.glob(f"*-{name}.png"))
    return matches[0] if matches else None


def run_tour(biome: str, shots_dir: Path) -> None:
    shots_dir.mkdir(parents=True, exist_ok=True)

    cmd = [
        str(GAME),
        "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0",
        f"--biome={biome}",
        f"--screenshots={shots_dir}",
        # ⚠ Arsenal complet. Sans lui, la galerie montre la PREMIÈRE MINUTE de jeu : un personnage
        # seul avec le canon de départ, aucun effet d'arme à l'écran. C'est exact, et c'est la pire
        # image possible d'un jeu dont l'argument est l'accumulation — on photographierait l'instant
        # où il n'a pas encore commencé.
        "--saturate-arsenal",
    ]

    print(f"  tournée {biome}…", end="", flush=True)
    started = time.time()

    proc = subprocess.Popen(cmd)
    try:
        proc.wait(timeout=TOUR_SECONDS)
    except subprocess.TimeoutExpired:
        proc.kill()

    taken = len(list(shots_dir.glob("*.png")))
    print(f" {taken} image(s) en {time.time() - started:.0f} s")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--biomes", nargs="*", default=BIOMES, choices=BIOMES)
    parser.add_argument("--keep", action="store_true",
                        help="conserve les tournées brutes (utile pour choisir une autre image)")
    args = parser.parse_args()

    if not GAME.exists():
        sys.exit(f"Binaire introuvable : {GAME}\n"
                 "Construire d'abord : Unity.exe -batchmode -quit -projectPath unity "
                 "-executeMethod BuildBench.Windows64Game")

    OUT.mkdir(parents=True, exist_ok=True)
    work = ROOT / "build" / "store_tours"

    copied: list[str] = []
    missing: list[str] = []

    for index, biome in enumerate(args.biomes):
        shots = work / biome
        if shots.exists():
            shutil.rmtree(shots)

        run_tour(biome, shots)

        # Les écrans communs viennent de la PREMIÈRE tournée seulement.
        wanted = dict(SHARED) if index == 0 else {}
        wanted[PER_BIOME] = f"gameplay_{biome}.png"

        for source, target in wanted.items():
            path = find_shot(shots, source)
            if path is None:
                missing.append(f"{biome}/{source} → {target}")
                continue

            shutil.copy2(path, OUT / target)
            copied.append(target)

    if not args.keep and work.exists():
        shutil.rmtree(work, ignore_errors=True)

    print(f"\n{len(copied)} image(s) écrites dans {OUT.relative_to(ROOT)} :")
    for name in copied:
        print(f"  {name}")

    # ⚠ Les manquantes sont annoncées, jamais tues : une galerie incomplète qui se croit complète
    # laisse en ligne des captures de l'ancien moteur, et personne ne le remarque avant un joueur.
    if missing:
        print(f"\nATTENTION : {len(missing)} capture(s) attendue(s) et absente(s) —")
        print("la tournée a-t-elle été coupée trop tôt (--tour-seconds), ou l'écran a-t-il changé ?")
        for item in missing:
            print(f"  {item}")
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
