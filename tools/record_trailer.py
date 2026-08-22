"""Capture les rushes video du trailer depuis le binaire Unity, puis les encode en mp4.

Pourquoi ce script a ete reecrit
--------------------------------
Sa version Godot pilotait le Movie Maker du moteur (`--write-movie`) et conduisait le jeu au
clavier via PyAutoGUI, plan par plan. Rien de tout cela ne survit au portage : Unity n'ecrit pas de
film depuis un build, et les drapeaux de mise en scene (`--debug-boss`, `--force-graft`) n'ont
jamais ete portes. La capture elle-meme vit desormais DANS le jeu (`Bench/TrailerRecorder.cs`), qui
met en scene chaque plan et ecrit une sequence PNG a cadence fixe ; ce script ne fait plus que
lancer les prises et les encoder.

Ce que ce changement apporte
----------------------------
Sous Godot, on filmait de longues runs et l'on CHERCHAIT les bons plans a la planche-contact. Les
runs etant aleatoires, chaque recapture invalidait tous les timecodes du montage — le fichier de
montage porte encore l'avertissement. Ici chaque plan est mis en scene et filme pour sa duree
exacte : le decoupage est connu d'avance et une recapture rend le meme resultat.

⚠ PAS DE SON. Un build Unity ne sait pas ecrire le mix audio en meme temps qu'il ralentit son
horloge de rendu. Les rushes sont muets ; la bande-son est remontee au mixage depuis les pistes du
jeu (cf. `MUSIC_EDL` dans `tools/build_trailer.py`). C'est la seule perte face au Movie Maker.

⚠ La capture dure PLUSIEURS FOIS la duree du plan (une image coute son rendu plus son encodage
PNG). Compter ~20 min pour la tournee complete, machine occupee : la fenetre du jeu doit rester
ouverte et personne ne doit taper au clavier pendant ce temps.

Usage :
    py tools/record_trailer.py --list
    py tools/record_trailer.py intro boss
    py tools/record_trailer.py --all
    py tools/record_trailer.py --all --lang=fr    # rushes francais (cartons FR au montage)
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
TRAILER = ROOT / "trailer"
RAW = TRAILER / "raw"
FRAMES = TRAILER / "frames"
LOGS = TRAILER / "logs"

FPS = 60

# ---------------------------------------------------------------------------
# LES PRISES — nom : (drapeaux de mise en scene, duree filmee en secondes)
#
# La duree est celle qu'annonce `TrailerRecorder` pour cette prise : elle sert ici a dimensionner
# le delai d'attente et a verifier le nombre d'images obtenues. Les deux doivent rester d'accord —
# une prise plus courte que prevu est le symptome d'une mise en scene qui s'est arretee en route.
#
# Les drapeaux communs (`--trailer`, `--record=`, `--lang=`) sont ajoutes par `run_take`.
#
# `--start-at=<minutes>` est le drapeau CENTRAL de la mise en scene : il fixe la minute de run,
# donc la densite de la faune et sa puissance. Le spectacle d'un survivor est en milieu et fin de
# partie — la premiere minute ne montre presque rien, et c'est ce que filmaient les rushes Godot
# pendant leurs 300 premieres secondes.
# ---------------------------------------------------------------------------
TAKES: dict[str, tuple[list[str], float]] = {
    # -- Ouverture narrative : la cinematique se joue seule, titre anime compris.
    "intro": ([], 44.0),

    # -- Menus et meta : Hub, carte des niveaux, trois onglets du Codex, defis.
    "menu": ([], 6.0),
    "meta": ([], 19.0),

    # -- L'ecran de choix de personnage. Prise de CONTROLE : elle n'est pas montee dans le trailer,
    #    elle existe pour qu'on puisse REGARDER l'ecran plutot que le relire.
    "charsel": ([], 5.0),

    # -- Gameplay, un biome par prise. Chacun a son angle : le Sanctuaire ouvre (faune claire,
    #    arene lisible), les autres montent en densite et en saturation d'effets.
    "gp_sanctuaire": (["--biome=sanctuaire", "--start-at=4", "--auto-play", "--invuln"], 22.0),
    "gp_aether":     (["--biome=aether", "--start-at=6", "--auto-play", "--invuln"], 22.0),
    "gp_givre":      (["--biome=givre", "--start-at=8", "--saturate-arsenal",
                       "--auto-play", "--invuln"], 22.0),
    "gp_neon":       (["--biome=neon", "--start-at=10", "--saturate-arsenal",
                       "--auto-play", "--invuln"], 22.0),
    #    Les elites portent des affixes visibles (contours, auras) : c'est le plan « ca deborde ».
    "gp_fournaise":  (["--biome=fournaise", "--start-at=11", "--saturate-arsenal",
                       "--force-elites", "--auto-play", "--invuln"], 22.0),

    # -- Progression : la decision qui rythme le jeu, et le corps qui mute.
    #
    #    ⚠ `--invuln` sur la chimere n'est PAS une precaution : la mise en scene vide l'arsenal du
    #    joueur pour degager la silhouette, donc il ne tue plus rien et meurt vers la 7e seconde. La
    #    prise se terminait sur l'ecran de fin de run — et le plan « BECOME THE CHIMERA », cale sur
    #    les dernieres secondes (c'est la que le corps porte le plus d'appendices), tombait dessus.
    "levelup": (["--biome=neon", "--start-at=6", "--invuln"], 5.0),
    "chimera": (["--biome=aether", "--start-at=2", "--invuln"], 9.0),

    # -- La Maree de Rouille : l'arene se referme, et la run a une fin. `--start-at=21` place la run
    #    a HUIT minutes d'overtime (l'overtime demarre a 13 min, la maree bouge a partir de 14) :
    #    la zone sure n'y fait plus que 30 % de ses demi-dimensions, et la perte d'espace se voit
    #    d'un coup d'oeil. A 17 on ne filmerait qu'un lisere a peine entame.
    #    ⚠ `--invuln` est ICI une necessite de mise en scene et non une precaution : le rongement
    #    est continu et traverse les i-frames, donc un plan de 25 s dans la rouille se termine sur
    #    l'ecran de fin de run.
    "tide": (["--biome=neon", "--start-at=21", "--saturate-arsenal",
              "--auto-play", "--invuln"], 25.0),

    # -- Le boss. `--start-at=13` place la run AU-DELA du temps imparti (780 s) : l'overtime demarre
    #    a la premiere image et le Noyau Rouille est invoque dans la foulee. C'est le remplacant du
    #    `--debug-boss` de Godot, qui n'a pas ete porte.
    #    ⚠ Sans `--saturate-arsenal`, un personnage nu ne l'entame pas : mesure, 4,7 % de ses PV en
    #    20 s avec un build de niveau 9. Le plan montrerait un combat sans issue.
    "boss": (["--biome=fournaise", "--start-at=13", "--saturate-arsenal",
              "--auto-play", "--invuln"], 26.0),
}


def run_take(name: str, lang: str) -> bool:
    flags, seconds = TAKES[name]
    frames_dir = FRAMES / name
    out = RAW / f"{name}.mp4"
    log = LOGS / f"{name}.log"

    for d in (RAW, FRAMES, LOGS):
        d.mkdir(parents=True, exist_ok=True)

    # Le dossier est VIDE avant chaque prise : ffmpeg lit une sequence numerotee, et les images
    # d'une capture precedente plus longue se retrouveraient collees a la fin du plan.
    if frames_dir.exists():
        shutil.rmtree(frames_dir)
    frames_dir.mkdir(parents=True)

    cmd = [
        str(GAME),
        "-logFile", str(log),
        "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0",
        "--trailer",
        f"--record={name}",
        f"--record-dir={frames_dir}",
        f"--record-fps={FPS}",
        f"--lang={lang}",
        *flags,
    ]

    print(f"=== {name} : {seconds:.0f}s filmees  {' '.join(flags) or '(sans drapeau)'}")
    started = time.time()

    # Large : la capture tourne bien plus lentement que le temps reel (rendu + encodage PNG de
    # chaque image), et une prise bloquee doit finir par rendre la main plutot que geler la tournee.
    timeout = max(300.0, seconds * 40.0)

    try:
        subprocess.run(cmd, timeout=timeout, check=False)
    except subprocess.TimeoutExpired:
        print(f"  !! {name} : delai depasse ({timeout:.0f}s) — prise abandonnee.")
        return False

    pngs = sorted(frames_dir.glob("f*.png"))
    expected = int(round(seconds * FPS))
    elapsed = time.time() - started

    if not pngs:
        print(f"  !! {name} : aucune image. Voir {log}")
        return False

    # ⚠ On COMPTE les images, on ne se contente pas de constater qu'il y en a. Une mise en scene qui
    # s'arrete en route (ecran absent, joueur mort, scene non chargee) rend un plan plus court sans
    # rien signaler — et le montage, lui, decoupe a des timecodes fixes : il tomberait sur du vide.
    if len(pngs) < expected:
        print(f"  !! {name} : {len(pngs)} images sur {expected} attendues "
              f"({len(pngs) / FPS:.1f}s au lieu de {seconds:.1f}s).")

    if not encode(frames_dir, out):
        return False

    shutil.rmtree(frames_dir)   # ~1 Go par prise : rien ne justifie de les garder une fois encodees

    size = out.stat().st_size / 1e6
    print(f"  -> {out.name}  ({len(pngs)} images, {size:.1f} Mo, "
          f"capture en {elapsed / 60:.1f} min)")
    return len(pngs) >= expected


def encode(frames_dir: Path, out: Path) -> bool:
    """Assemble la sequence PNG en mp4 sans perte visible.

    CRF 12 et non 18 : ce fichier est un INTERMEDIAIRE que `build_trailer.py` va recouper,
    agrandir x2 et reencoder. Chaque generation ajoute ses artefacts, et sur du pixel art un bord
    legerement sali au premier encodage devient un halo au second.
    """
    result = subprocess.run([
        "ffmpeg", "-v", "error", "-y",
        "-framerate", str(FPS),
        "-i", str(frames_dir / "f%05d.png"),
        "-c:v", "libx264", "-crf", "12", "-preset", "medium",
        "-pix_fmt", "yuv420p",
        str(out),
    ], check=False)

    if result.returncode != 0:
        print(f"  !! ffmpeg a echoue ({result.returncode}) sur {frames_dir.name}")
        return False

    return True


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("takes", nargs="*", help="prises a capturer")
    parser.add_argument("--all", action="store_true", help="toutes les prises")
    parser.add_argument("--list", action="store_true", help="liste les prises et sort")
    parser.add_argument("--lang", default="en",
                        help="langue du jeu pendant la capture (defaut : en). "
                             "⚠ doit correspondre au --lang du montage : le texte affiche par le "
                             "JEU est grave dans les rushes, celui des cartons est incruste apres.")
    args = parser.parse_args()

    if args.list or (not args.takes and not args.all):
        total = sum(s for _, s in TAKES.values())
        for name, (flags, seconds) in TAKES.items():
            print(f"{name:16s} {seconds:>5.1f}s  {' '.join(flags)}")
        print(f"\n{len(TAKES)} prises, {total:.0f}s de rushes.")
        return 0

    if not GAME.exists():
        print(f"Binaire absent : {GAME}\n"
              f"  Unity.exe -batchmode -quit -projectPath unity "
              f"-executeMethod BuildBench.Windows64Game")
        return 2

    names = list(TAKES) if args.all else args.takes
    unknown = [n for n in names if n not in TAKES]
    if unknown:
        print("Prises inconnues :", ", ".join(unknown))
        return 2

    started = time.time()
    failed = [n for n in names if not run_take(n, args.lang)]

    print(f"\n{len(names) - len(failed)}/{len(names)} prises en "
          f"{(time.time() - started) / 60:.1f} min → {RAW}")
    if failed:
        print("Prises incompletes :", ", ".join(failed))

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
