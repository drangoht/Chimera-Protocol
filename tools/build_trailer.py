"""Monte le trailer video a partir des rushes captures par `tools/record_trailer.py`.

Pipeline en trois passes (plus simple a debugger qu'un unique filter_complex a 25 entrees) :
  1. EXTRACTION  chaque plan de l'EDL -> un mp4 intermediaire, upscale x2 en NEAREST
                 (1280x720 -> 2560x1440 : facteur ENTIER, donc pixel art net ; un 1080p
                 imposerait un x1.5 non entier et baverait), texte incruste si demande.
  2. CONCAT      concatenation par le demuxer `concat`.
  3. MIXAGE      musique du jeu continue (intro -> crossfade -> theme intense) par-dessus
                 l'audio des plans garde tres bas (texture des impacts sans empiler deux
                 musiques differentes), puis encodage final H.264 pour YouTube.

PIEGE drawtext sous Windows : le texte accentue passe par `textfile=` (fichiers UTF-8 ecrits
dans trailer/txt/), et les chemins y sont RELATIFS avec des slashes -- un backslash ou un `:`
dans la valeur d'une option de filtre casse le parsing des filtres ffmpeg.

Usage :
    python tools/build_trailer.py            # montage complet
    python tools/build_trailer.py --clips    # re-extrait seulement les plans
    python tools/build_trailer.py --no-extract   # remonte sans re-extraire (rapide)
"""
import os
import subprocess
import sys

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(PROJ, "trailer", "raw")
CLIPS = os.path.join(PROJ, "trailer", "clips")
TXT = os.path.join(PROJ, "trailer", "txt")
OUT = os.path.join(PROJ, "trailer", "ChimeraProtocol_trailer_1440p.mp4")

MUSIC_INTRO = "assets/audio/music/music_intro.ogg"
MUSIC_MAIN = "assets/audio/music/music_run_intense.wav"
FONT = "assets/fonts/ShareTechMono.ttf"

CYAN = "0x44FFEE"
GOLD = "0xFFCC44"
WHITE = "0xD9D9F2"

# Volume de l'audio des plans dans le mix final. Bas VOLONTAIREMENT : chaque rush porte deja
# la musique du jeu, et deux musiques differentes superposees a volume egal se battent.
CLIP_GAIN = 0.16

# ---------------------------------------------------------------------------
# EDL — (source, debut_s, duree_s, texte|None, couleur)
#   Repere sur les planches-contact de trailer/raw/sheet_*.png.
#   Rythme : plans longs a l'ouverture (narration), de plus en plus courts a l'escalade.
# ---------------------------------------------------------------------------
EDL = [
    # -- Cold open : 1,6 s d'action AVANT la narration. Sans ce hook, les 10 premieres
    #    secondes du trailer sont des plans de cinematique tres sombres -- fatal sur YouTube.
    ("long_neon",     185.4, 1.6, None, None),

    # -- A. Ouverture narrative (cinematique d'intro, ~9 s)
    ("intro",           2.2, 3.2, None, None),
    ("intro",          10.6, 2.8, None, None),
    ("intro",          21.8, 3.0, None, None),

    # -- B. Le jeu : les biomes (~14 s)
    ("gp_sanctuaire",  12.0, 3.0, "SURVIVEZ À LA NUÉE", CYAN),
    ("long_neon",     183.0, 2.8, None, None),
    ("gp_givre",       30.0, 2.6, None, None),
    ("long_fournaise",176.0, 2.8, None, None),
    ("gp_aether",      28.0, 2.6, None, None),

    # -- C. Progression : level-up / assimilation / fusion (~9 s)
    #    Bornes serrees : ces deux ecrans modaux ne durent qu'environ 2 s chacun a l'ecran.
    ("long_fournaise",153.7, 2.2, "ARRACHEZ LEURS ORGANES", GOLD),
    ("long_fournaise",162.9, 2.4, "DEVENEZ LA CHIMÈRE", GOLD),
    ("long_neon",     192.4, 2.6, None, None),

    # -- D. Escalade : late game + boss (~15 s)
    ("long_fournaise",160.0, 2.4, None, None),
    ("long_neon",     208.0, 2.4, None, None),
    ("long_fournaise",170.5, 2.4, None, None),
    ("long_neon",     214.0, 2.2, None, None),
    ("boss_tank",       9.0, 2.6, "AFFRONTEZ LA ROUILLE VIVANTE", CYAN),
    ("boss_tank",      33.0, 2.6, None, None),

    # -- E. Meta / menus (~12 s)
    ("charsel",         3.2, 2.2, "4 PERSONNAGES · 12 ARMES · 9 FUSIONS", CYAN),
    ("arsenal",         4.0, 1.9, None, None),
    ("bestiary",        4.0, 1.9, None, None),
    ("chimera_codex",   4.0, 1.9, None, None),
    ("challenges",      4.0, 1.9, None, None),
    ("hub",             5.0, 2.1, None, None),

    # -- F. Final (~11 s)
    ("long_fournaise",178.0, 2.4, None, None),
    ("long_neon",     203.0, 2.3, None, None),
    ("intro",          33.2, 6.4, "DISPONIBLE SUR ITCH.IO|drangoht.itch.io/chimera-protocol", GOLD),
]

TOTAL = sum(e[2] for e in EDL)


def run(args, **kw):
    r = subprocess.run(args, cwd=PROJ, **kw)
    if r.returncode != 0:
        raise SystemExit(f"ffmpeg a echoue ({r.returncode}) : {' '.join(args[:12])} ...")


def write_text_file(idx, text):
    os.makedirs(TXT, exist_ok=True)
    path = os.path.join(TXT, f"t{idx:02d}.txt")
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    return f"trailer/txt/t{idx:02d}.txt"


def text_filters(idx, text, color, dur):
    """Texte centre en bas, en fondu, ombre + contour pour rester lisible sur tout fond.

    Un `|` dans le texte separe le titre d'un sous-titre (police plus petite, cyan) --
    utilise pour l'URL du carton final."""
    title, _, subtitle = text.partition("|")
    fade_in, hold = 0.35, dur - 0.75
    alpha = (f"if(lt(t,{fade_in}),t/{fade_in},"
             f"if(lt(t,{hold}),1,max(0,({dur}-t)/0.4)))")

    def one(rel_txt, col, size, y):
        return (
            f"drawtext=fontfile={FONT}:textfile={rel_txt}:"
            f"fontcolor={col}:fontsize={size}:alpha='{alpha}':"
            f"x=(w-text_w)/2:y={y}:"
            f"shadowcolor=0x000000:shadowx=4:shadowy=4:"
            f"borderw=3:bordercolor=0x0A0A18"
        )

    # Avec sous-titre le bloc descend : sur l'ecran-titre il doit passer SOUS les boutons.
    y_title = "h-230" if subtitle else "h-260"
    out = [one(write_text_file(idx, title), color, 76, y_title)]
    if subtitle:
        out.append(one(write_text_file(idx + 100, subtitle), CYAN, 42, "h-150"))
    return out


def extract():
    os.makedirs(CLIPS, exist_ok=True)
    for i, (src, start, dur, text, color) in enumerate(EDL):
        avi = os.path.join(RAW, f"{src}.avi")
        if not os.path.exists(avi):
            raise SystemExit(f"Rush manquant : {avi} (relancer tools/record_trailer.py {src})")
        out = os.path.join(CLIPS, f"{i:02d}_{src}.mp4")

        # NEAREST : upscale x2 exact, aucun lissage -> le pixel art reste net.
        vf = ["scale=2560:1440:flags=neighbor"]
        if text:
            vf.extend(text_filters(i, text, color, dur))
        vf.append("fps=60,format=yuv420p")

        run([
            "ffmpeg", "-v", "error", "-y",
            "-ss", f"{start}", "-i", avi, "-t", f"{dur}",
            "-vf", ",".join(vf),
            "-af", f"volume={CLIP_GAIN},afade=t=in:d=0.05,afade=t=out:st={max(0, dur - 0.1)}:d=0.1",
            "-c:v", "libx264", "-crf", "14", "-preset", "veryfast",
            "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2",
            out,
        ])
        print(f"  [{i:02d}] {src} {start:>6.1f}s +{dur:.1f}s"
              + (f"  \"{text}\"" if text else ""))


def concat():
    lst = os.path.join(CLIPS, "concat.txt")
    with open(lst, "w", encoding="utf-8") as f:
        for i, (src, *_rest) in enumerate(EDL):
            f.write(f"file '{i:02d}_{src}.mp4'\n")
    out = os.path.join(CLIPS, "_concat.mp4")
    run(["ffmpeg", "-v", "error", "-y", "-f", "concat", "-safe", "0",
         "-i", lst, "-c", "copy", out])
    return out


def finalize(concat_mp4):
    """Musique continue + audio des plans, fondus d'ouverture/fermeture, encodage YouTube."""
    xfade = 1.6
    intro_len = 11.4                      # les 3 plans de cinematique
    main_len = TOTAL - intro_len + xfade

    filt = (
        # Musique 1 : theme d'intro (celui qui joue deja sur les plans de cinematique).
        f"[1:a]atrim=0:{intro_len + xfade},asetpts=PTS-STARTPTS[m1];"
        # Musique 2 : theme de run intense, cale sur l'entree du gameplay.
        f"[2:a]atrim=0:{main_len},asetpts=PTS-STARTPTS,afade=t=in:d=1.0[m2];"
        f"[m1][m2]acrossfade=d={xfade}:c1=tri:c2=tri[mus];"
        f"[mus]volume=0.95,afade=t=out:st={TOTAL - 2.6}:d=2.6[musf];"
        # Audio des plans (deja attenue a l'extraction) + musique.
        f"[0:a]volume=1.0[clips];"
        # Normalisation de diffusion : la somme musique + plans sortait a -8 LUFS (YouTube
        # normalise a -14 et un tel niveau ecrete). TP=-1.5 dBTP garde une marge pour le
        # reencodage lossy de la plateforme.
        f"[clips][musf]amix=inputs=2:duration=first:dropout_transition=0:normalize=0,"
        f"loudnorm=I=-14:TP=-1.5[aout];"
        # Video : ouverture depuis le noir, fermeture au noir.
        f"[0:v]fade=t=in:st=0:d=0.8,fade=t=out:st={TOTAL - 1.8}:d=1.8[vout]"
    )

    run([
        "ffmpeg", "-v", "error", "-y",
        "-i", concat_mp4, "-i", MUSIC_INTRO, "-i", MUSIC_MAIN,
        "-filter_complex", filt,
        "-map", "[vout]", "-map", "[aout]",
        "-c:v", "libx264", "-crf", "16", "-preset", "slow",
        "-pix_fmt", "yuv420p", "-profile:v", "high", "-level", "5.1",
        "-movflags", "+faststart",
        "-c:a", "aac", "-b:a", "320k", "-ar", "48000", "-ac", "2",
        "-t", f"{TOTAL}",
        OUT,
    ])


if __name__ == "__main__":
    argv = sys.argv[1:]
    print(f"Trailer : {len(EDL)} plans, {TOTAL:.1f}s")
    if "--no-extract" not in argv:
        extract()
    if "--clips" in argv:
        sys.exit(0)
    c = concat()
    finalize(c)
    size = os.path.getsize(OUT) / 1e6
    print(f"\n-> {OUT}  ({size:.1f} Mo, {TOTAL:.1f}s, 2560x1440 @60fps)")
