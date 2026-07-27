"""Planches-contact des rushes du trailer -- une vignette horodatee toutes les N secondes.

A quoi ca sert : l'EDL de `tools/build_trailer.py` designe ses plans par des TIMECODES dans
les rushes (`long_neon` a 185.4 s...). Or chaque recapture rejoue des runs randomisees : apres
un passage de `tools/record_trailer.py`, ces timecodes ne montrent plus la meme chose. La
planche permet de re-reperer les bons instants d'un coup d'oeil au lieu de scruter 5 min de
video.

Le timecode incruste sur chaque vignette est celui a lire dans l'EDL.

Usage :
    python tools/trailer_sheets.py                    # toutes les sources de l'EDL
    python tools/trailer_sheets.py long_neon boss     # sources choisies
    python tools/trailer_sheets.py long_neon --step 3 # vignette toutes les 3 s
    python tools/trailer_sheets.py long_fournaise --range 180-200 --step 1

`--range` sert a caler les plans COURTS sur un evenement bref : une modale d'Assimilation ou
de fusion ne reste que ~2 s a l'ecran, donc invisible sur une planche au pas de 5 s.
"""
import os
import subprocess
import sys

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(PROJ, "trailer", "raw")
FONT = "assets/fonts/ShareTechMono.ttf"

COLS = 6
THUMB_W = 320


def sheet(name, step, window=None):
    avi = os.path.join(RAW, f"{name}.avi")
    if not os.path.exists(avi):
        print(f"  !! rush manquant : {name}.avi")
        return False

    dur = float(subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", avi],
        capture_output=True, text=True, check=True).stdout.strip())

    start, end = window if window else (0.0, dur)
    end = min(end, dur)
    suffix = f"_{start:.0f}-{end:.0f}" if window else ""
    out = os.path.join(RAW, f"sheet_{name}{suffix}.png")

    # Nombre de lignes : deduit de la duree pour ne pas perdre la fin de la fenetre (tile
    # jette les vignettes qui depassent la grille).
    rows = int(((end - start) / step) // COLS) + 1

    # `fps` reechantillonne AVANT drawtext. Avec `-ss` les PTS repartent de zero, d'ou le 3e
    # champ de %{pts} : l'offset qui restitue le timecode SOURCE -- celui a recopier dans
    # l'EDL. Les `:` internes doivent etre echappes, sinon ils terminent l'option de filtre
    # (meme piege que dans build_trailer.py).
    vf = (
        f"fps=1/{step},scale={THUMB_W}:-1,"
        f"drawtext=fontfile={FONT}:text='%{{pts\\:hms\\:{start}}}':x=6:y=6:fontsize=20:"
        f"fontcolor=0xFFCC44:box=1:boxcolor=0x000000@0.65:boxborderw=4,"
        f"tile={COLS}x{rows}"
    )

    subprocess.run(
        ["ffmpeg", "-v", "error", "-y",
         "-ss", f"{start}", "-i", avi, "-t", f"{end - start}",
         "-vf", vf, "-frames:v", "1", out],
        cwd=PROJ, check=True)
    print(f"  -> {os.path.basename(out)}  ({start:.0f}-{end:.0f}s, "
          f"1 vignette / {step}s, {COLS}x{rows})")
    return True


if __name__ == "__main__":
    argv = sys.argv[1:]
    step = 5.0
    if "--step" in argv:
        i = argv.index("--step")
        step = float(argv[i + 1])   # fractionnaire pour caler une modale de ~2 s
        del argv[i:i + 2]

    window = None
    if "--range" in argv:
        i = argv.index("--range")
        a, _, b = argv[i + 1].partition("-")
        window = (float(a), float(b))
        del argv[i:i + 2]

    if argv:
        names = argv
    else:
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from build_trailer import EDL  # noqa: E402
        names = sorted({e[0] for e in EDL})

    for n in names:
        sheet(n, step, window)
