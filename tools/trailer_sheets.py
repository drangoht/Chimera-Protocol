"""Planches-contact des rushes du trailer -- une vignette horodatee toutes les N secondes.

A quoi ca sert : l'EDL de `tools/build_trailer.py` designe ses plans par des TIMECODES dans les
rushes. Depuis la capture Unity (`Bench/TrailerRecorder.cs`) chaque prise est MISE EN SCENE, donc
ces timecodes sont connus d'avance et survivent a une recapture -- ce qui n'etait pas le cas des
rushes Godot, tires de runs aleatoires qu'il fallait re-caler a chaque fois.

La planche ne sert donc plus a CHERCHER les plans, mais a VERIFIER qu'ils montrent bien ce que la
mise en scene promet. C'est la seule etape qui regarde l'image : un rush peut avoir le bon nombre
d'images, la bonne duree, et ne montrer qu'une arene vide ou une modale restee ouverte.

Le timecode incruste sur chaque vignette est celui a lire dans l'EDL.

Usage :
    python tools/trailer_sheets.py                    # toutes les sources de l'EDL
    python tools/trailer_sheets.py meta boss          # sources choisies
    python tools/trailer_sheets.py gp_neon --step 2   # vignette toutes les 2 s
    python tools/trailer_sheets.py meta --range 0-19 --step 1

`--range` sert a verifier les plans COURTS : un ecran de la prise `meta` ne tient que 3 s, une
modale de montee de niveau environ 2 s -- invisibles sur une planche au pas de 5 s.
"""
import os
import subprocess
import sys

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(PROJ, "trailer", "raw")
FONT = "unity/Assets/Resources/Fonts/ShareTechMono.ttf"

COLS = 6
THUMB_W = 320


def sheet(name, step, window=None):
    rush = os.path.join(RAW, f"{name}.mp4")
    if not os.path.exists(rush):
        print(f"  !! rush manquant : {name}.mp4")
        return False

    dur = float(subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", rush],
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
         "-ss", f"{start}", "-i", rush, "-t", f"{end - start}",
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
