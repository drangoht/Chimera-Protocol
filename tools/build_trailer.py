"""Monte le trailer video a partir des rushes captures par `tools/record_trailer.py`.

Pipeline en trois passes (plus simple a debugger qu'un unique filter_complex a 25 entrees) :
  1. EXTRACTION  chaque plan de l'EDL -> un mp4 intermediaire, upscale x2 en NEAREST
                 (1280x720 -> 2560x1440 : facteur ENTIER, donc pixel art net ; un 1080p
                 imposerait un x1.5 non entier et baverait), texte incruste si demande.
  2. CONCAT      concatenation par le demuxer `concat`.
  3. MIXAGE      trois morceaux de la bande-son enchaines en fondu (cf. MUSIC_EDL) par-dessus
                 l'audio des plans garde tres bas (texture des impacts sans empiler deux
                 musiques differentes), puis encodage final H.264 pour YouTube.

PIEGE drawtext sous Windows : le texte accentue passe par `textfile=` (fichiers UTF-8 ecrits
dans trailer/txt/), et les chemins y sont RELATIFS avec des slashes -- un backslash ou un `:`
dans la valeur d'une option de filtre casse le parsing des filtres ffmpeg.

Usage :
    python tools/build_trailer.py            # montage complet (cartons EN, sortie *_EN_*.mp4)
    python tools/build_trailer.py --lang=fr  # cartons FR (rushes FR requis, cf. ci-dessous)
    python tools/build_trailer.py --clips    # re-extrait seulement les plans
    python tools/build_trailer.py --no-extract   # remonte sans re-extraire (rapide)

LANGUE : `--lang` ne change QUE les cartons de texte incrustes au montage. Le texte affiche par
le jeu lui-meme (narration de la cinematique, bannieres de biome, cartes de level-up, menus) est
grave dans les rushes -- il vient de `tools/record_trailer.py --lang=<code>`. Monter des cartons
anglais sur des rushes francais donne un trailer bilingue incoherent : les deux flags doivent
toujours porter la meme langue.
"""
import os
import subprocess
import sys

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(PROJ, "trailer", "raw")
CLIPS = os.path.join(PROJ, "trailer", "clips")
TXT = os.path.join(PROJ, "trailer", "txt")
LANG = next((a.split("=", 1)[1] for a in sys.argv[1:] if a.startswith("--lang=")), "en")
OUT = os.path.join(PROJ, "trailer", f"ChimeraProtocol_trailer_{LANG.upper()}_1440p.mp4")

FONT = "unity/Assets/Resources/Fonts/ShareTechMono.ttf"

CYAN = "0x44FFEE"
GOLD = "0xFFCC44"
WHITE = "0xD9D9F2"

# Volume de l'audio des plans dans le mix final. Bas VOLONTAIREMENT : chaque rush porte deja
# la musique du jeu, et deux musiques differentes superposees a volume egal se battent. Depuis
# le passage au metal (1.17.0) les deux sont rythmiques, donc encore plus bas qu'avant : ce qui
# doit rester, ce sont les transitoires (tirs, explosions, ramassages), pas le fond musical.
CLIP_GAIN = 0.12

# ---------------------------------------------------------------------------
# CARTONS DE TEXTE — cle -> traduction. L'EDL ne porte que la cle (cf. `--lang`).
#   Un `|` separe le titre du sous-titre (police plus petite, cyan).
#   Registre voulu : imperatif, court, meme voix que la tagline officielle du jeu
#   ("Don't kill the monsters. Become them." / INTRO_TAGLINE de localization/ui.csv).
# ---------------------------------------------------------------------------
TEXTS = {
    "en": {
        "SWARM":   "SURVIVE THE SWARM",
        "ORGANS":  "TEAR OUT THEIR ORGANS",
        "CHIMERA": "BECOME THE CHIMERA",
        "BOSS":    "FACE THE LIVING RUST",
        "CONTENT": "4 CHARACTERS · 12 WEAPONS · 9 FUSIONS",
        "STORE":   "AVAILABLE ON ITCH.IO|drangoht.itch.io/chimera-protocol",
    },
    "fr": {
        "SWARM":   "SURVIVEZ À LA NUÉE",
        "ORGANS":  "ARRACHEZ LEURS ORGANES",
        "CHIMERA": "DEVENEZ LA CHIMÈRE",
        "BOSS":    "AFFRONTEZ LA ROUILLE VIVANTE",
        "CONTENT": "4 PERSONNAGES · 12 ARMES · 9 FUSIONS",
        "STORE":   "DISPONIBLE SUR ITCH.IO|drangoht.itch.io/chimera-protocol",
    },
}

# ---------------------------------------------------------------------------
# EDL — (source, debut_s, duree_s, cle_texte|None, couleur)
#   Repere sur les planches-contact de trailer/raw/sheet_*.png (`tools/trailer_sheets.py`).
#   Rythme : plans longs a l'ouverture (narration), de plus en plus courts a l'escalade.
#
#   ATTENTION : ces timecodes ne survivent PAS a une recapture. Les runs sont randomisees,
#   donc apres `tools/record_trailer.py` il faut regenerer les planches et re-caler chaque
#   plan -- surtout les modales (level-up, assimilation, fusion) qui ne durent que ~2 s et
#   dont un plan mal cale ne montrerait qu'un ecran de menu fige.
#   Dernier recalage : 2026-07-28 (rushes ANGLAIS, `record_trailer.py --lang=en`).
# ---------------------------------------------------------------------------
EDL = [
    # -- A. Ouverture narrative (cinematique d'intro, ~9 s)
    #    Ouverture DIRECTE sur la cinematique, sans plan d'action prealable : le trailer
    #    s'installe sur la narration. Les plans sont sombres, d'ou le fondu d'ouverture court
    #    (0.4 s) de finalize() -- rallonger le noir de tete tuerait la retention YouTube.
    #    Beats retenus : l'origine (1), la naissance de la Rouille (3), puis le beat 6 qui
    #    ENONCE le pitch du jeu (« tear a piece of it free — and let it become part of you »).
    ("intro",           1.6, 3.2, None, None),
    ("intro",          10.4, 2.8, None, None),
    ("intro",          24.0, 3.2, None, None),

    # -- B. Le jeu : les biomes (~14 s)
    ("gp_sanctuaire",  52.4, 2.8, "SWARM", CYAN),
    ("long_neon",     100.8, 2.8, None, None),
    ("gp_givre",       39.4, 2.6, None, None),
    ("long_fournaise",105.0, 2.8, None, None),
    ("gp_aether",      43.6, 2.6, None, None),

    # -- C. Progression : assimilation / fusion (~8 s)
    #    Bornes serrees : ces deux ecrans modaux ne durent qu'environ 2,5 s chacun a l'ecran
    #    (Stalker Wave a 10,6-13,2 ; Armored Charge a 151,8-154,0), et un level-up suit
    #    immediatement les deux -- deborder d'une demi-seconde tombe sur les cartes.
    ("long_fournaise", 10.6, 2.6, "ORGANS", GOLD),
    ("long_neon",     151.6, 2.4, "CHIMERA", GOLD),
    #    Retour au jeu APRES la fusion : la chimere evoluee en action.
    ("long_neon",     157.4, 2.6, None, None),

    # -- D. Escalade : late game + boss (~15 s)
    ("long_fournaise",177.4, 2.4, None, None),
    ("long_neon",     209.4, 2.4, None, None),
    ("long_fournaise",205.0, 2.4, None, None),
    ("long_neon",     231.0, 2.2, None, None),
    #    Le boss tue le joueur a 23 s dans ce rush : tout doit tenir avant. Le carton est sur le
    #    SECOND plan, pas le premier : c'est la que le Colosse est proche et lisible (20-22 s),
    #    alors qu'il n'est qu'un point lointain dans les premieres secondes du rush.
    ("boss_tank",      12.4, 2.4, None, None),
    ("boss_tank",      18.6, 2.8, "BOSS", CYAN),

    # -- E. Meta / menus (~12 s)
    ("charsel",         3.2, 2.2, "CONTENT", CYAN),
    ("arsenal",         4.0, 1.9, None, None),
    ("bestiary",        4.0, 1.9, None, None),
    ("chimera_codex",   4.0, 1.9, None, None),
    ("challenges",      4.0, 1.9, None, None),
    ("hub",             5.0, 2.1, None, None),

    # -- F. Final (~12 s)
    #    Le reveal du titre (29,4) puis le menu (33,2) : deux plans plutot qu'un seul long,
    #    pour que le carton itch.io tombe sur le menu et pas par-dessus la tagline du jeu.
    ("long_fournaise",265.0, 2.4, None, None),
    ("long_neon",     289.0, 2.3, None, None),
    #    30.0 et pas 29.4 : la fin du beat 6 (deja vu en ouverture) tient encore l'ecran ; on
    #    entre directement sur le flash blanc de la cinematique qui devoile le titre.
    ("intro",          30.0, 2.8, None, None),
    ("intro",          33.2, 4.0, "STORE", GOLD),
]

TOTAL = sum(e[2] for e in EDL)

# ---------------------------------------------------------------------------
# EDL MUSICALE — (piste, t_entree_s)
#   Trois morceaux de la bande-son du jeu (metal industriel, 1.17.0) enchaines par
#   fondu croise de XFADE. Les bornes sont calees sur la structure du montage :
#     0.0   theme principal      — narration de la cinematique
#     9.2   run neon (refrain)   — entree du gameplay, 160 BPM
#    39.8   theme de boss        — arrivee du boss, tenu jusqu'au carton final
#
#   Choix des pistes : PAS `music_intro.ogg` ici, alors que c'est la musique qui joue sur les
#   plans de cinematique -- la meme piste jouee deux fois avec un decalage donne un doublage
#   sale. Meme raison pour les biomes : les plans de gameplay viennent surtout de neon et
#   fournaise, et l'audio des rushes est justement attenue a CLIP_GAIN.
# ---------------------------------------------------------------------------
MUSIC_EDL = [
    ("unity/Assets/Resources/Audio/music/music_menu.ogg",             0.0),
    ("unity/Assets/Resources/Audio/music/music_run_neon_combat.ogg",  9.2),
    ("unity/Assets/Resources/Audio/music/music_run_boss.ogg",        39.8),
]

XFADE = 1.6


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
    if LANG not in TEXTS:
        raise SystemExit(f"Langue inconnue : {LANG} (dispo : {', '.join(TEXTS)})")
    for i, (src, start, dur, key, color) in enumerate(EDL):
        text = TEXTS[LANG][key] if key else None
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
    # Chaine de fondus croises : chaque morceau est coupe a la duree qui le mene jusqu'a
    # l'entree du suivant, PLUS le recouvrement du fondu (acrossfade consomme XFADE de la
    # fin du precedent, donc sans cette marge le fondu mordrait sur la section utile).
    segs = []
    for i, (path, start) in enumerate(MUSIC_EDL):
        end = MUSIC_EDL[i + 1][1] if i + 1 < len(MUSIC_EDL) else TOTAL
        segs.append(f"[{i + 1}:a]atrim=0:{end - start + XFADE},asetpts=PTS-STARTPTS[m{i}];")

    chain = ""
    prev = "[m0]"
    for i in range(1, len(MUSIC_EDL)):
        out = "[mus]" if i == len(MUSIC_EDL) - 1 else f"[x{i}]"
        chain += f"{prev}[m{i}]acrossfade=d={XFADE}:c1=tri:c2=tri{out};"
        prev = out

    filt = (
        "".join(segs) + chain +
        f"[mus]volume=0.95,afade=t=out:st={TOTAL - 2.6}:d=2.6[musf];"
        # Audio des plans (deja attenue a l'extraction) + musique.
        f"[0:a]volume=1.0[clips];"
        # Normalisation de diffusion : la somme musique + plans sortait a -8 LUFS (YouTube
        # normalise a -14 et un tel niveau ecrete). TP=-1.5 dBTP garde une marge pour le
        # reencodage lossy de la plateforme.
        f"[clips][musf]amix=inputs=2:duration=first:dropout_transition=0:normalize=0,"
        f"loudnorm=I=-14:TP=-1.5[aout];"
        # Video : ouverture depuis le noir, fermeture au noir.
        # Ouverture courte (0.4 s) : le trailer demarre sur la cinematique, deja tres sombre --
        # un fondu long y ajouterait une seconde de quasi-noir en tete.
        f"[0:v]fade=t=in:st=0:d=0.4,fade=t=out:st={TOTAL - 1.8}:d=1.8[vout]"
    )

    inputs = ["-i", concat_mp4]
    for path, _start in MUSIC_EDL:
        full = os.path.join(PROJ, path)
        if not os.path.exists(full):
            raise SystemExit(f"Musique manquante : {path}")
        inputs += ["-i", path]

    run([
        "ffmpeg", "-v", "error", "-y",
        *inputs,
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
    print(f"Trailer [{LANG}] : {len(EDL)} plans, {TOTAL:.1f}s")
    if "--no-extract" not in argv:
        extract()
    if "--clips" in argv:
        sys.exit(0)
    c = concat()
    finalize(c)
    size = os.path.getsize(OUT) / 1e6
    print(f"\n-> {OUT}  ({size:.1f} Mo, {TOTAL:.1f}s, 2560x1440 @60fps)")
