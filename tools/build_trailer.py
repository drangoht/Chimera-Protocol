"""Monte le trailer video a partir des rushes captures par `tools/record_trailer.py`.

Pipeline en trois passes (plus simple a debugger qu'un unique filter_complex a 25 entrees) :
  1. EXTRACTION  chaque plan de l'EDL -> un mp4 intermediaire, upscale x2 en NEAREST
                 (1280x720 -> 2560x1440 : facteur ENTIER, donc pixel art net ; un 1080p
                 imposerait un x1.5 non entier et baverait), texte incruste si demande.
  2. CONCAT      concatenation par le demuxer `concat`.
  3. MIXAGE      trois morceaux de la bande-son enchaines en fondu (cf. MUSIC_EDL), puis
                 encodage final H.264 pour YouTube.

CE QUI A CHANGE AVEC LE PORTAGE UNITY
-------------------------------------
Les rushes sont des **mp4 muets**, et non plus les .avi sonores du Movie Maker de Godot : un build
Unity ne sait pas ecrire le mix audio en meme temps qu'il ralentit son horloge de rendu
(cf. `Bench/TrailerRecorder.cs`). La bande-son du montage est donc **entierement** remontee depuis
les pistes du jeu -- la ou l'ancien montage y superposait l'audio des plans a bas volume pour en
garder les transitoires (tirs, impacts, ramassages). C'est la seule perte du portage, et elle ne
s'entend que sur les plans d'action.

Et surtout : chaque prise est desormais **mise en scene** (un plan = une prise, filmee pour sa
duree exacte). Les timecodes de l'EDL ne sont donc plus releves apres coup sur une planche-contact,
ils sont connus d'avance -- et ils survivent a une recapture.

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

# ---------------------------------------------------------------------------
# CARTONS DE TEXTE — cle -> traduction. L'EDL ne porte que la cle (cf. `--lang`).
#   Un `|` separe le titre du sous-titre (police plus petite, cyan).
#   Registre voulu : imperatif, court, meme voix que la tagline officielle du jeu
#   ("Don't kill the monsters. Become them." / INTRO_TAGLINE de localization/ui.csv).
#
# ⚠ CONTENT a annonce « 4 CHARACTERS » alors que c'etait faux (l'ecran de choix n'avait jamais ete
#   porte sous Unity), le chiffre a ete RETIRE le 2026-08-22 a 12:56 — et les personnages ont ete
#   portes le MEME JOUR a 14:17, publies en 2.5.0. Le carton a donc menti, puis cache, en cinq
#   heures. Il est retabli, et cette fois le montage MONTRE l'ecran (plan `charsel`, section F) :
#   un chiffre dans un carton n'est une preuve de rien, c'est le plan qui l'atteste.
#   Les quatre chiffres sont comptes dans les donnees : 4 profils (Rules/Characters), 5 biomes
#   (LevelThreat.Order), 12 armes et 9 fusions (weapons.json) — les 4 armes de signature etant
#   DEJA dans les 12, le chiffre des armes ne bouge pas.
#   Un trailer qui promet un contenu absent se paie en remboursements ; un trailer qui en cache un
#   se paie en clics jamais faits.
# ---------------------------------------------------------------------------
TEXTS = {
    "en": {
        "SWARM":   "SURVIVE THE SWARM",
        "ORGANS":  "TEAR OUT THEIR ORGANS",
        "CHIMERA": "BECOME THE CHIMERA",
        "BOSS":    "FACE THE LIVING RUST",
        "TIDE":    "THE ARENA CLOSES IN",
        "ENDS":    "EVERY RUN ENDS|how long you hold is the score",
        "CONTENT": "4 CHARACTERS · 5 BIOMES · 12 WEAPONS · 9 FUSIONS",
        "STORE":   "PLAY FREE IN YOUR BROWSER|drangoht.itch.io/chimera-protocol",
    },
    "fr": {
        "SWARM":   "SURVIVEZ À LA NUÉE",
        "ORGANS":  "ARRACHEZ LEURS ORGANES",
        "CHIMERA": "DEVENEZ LA CHIMÈRE",
        "BOSS":    "AFFRONTEZ LA ROUILLE VIVANTE",
        "TIDE":    "L'ARÈNE SE REFERME",
        "ENDS":    "TOUTE RUN A UNE FIN|le score, c'est le temps que vous tenez",
        "CONTENT": "4 PERSONNAGES · 5 BIOMES · 12 ARMES · 9 FUSIONS",
        "STORE":   "JOUEZ DANS VOTRE NAVIGATEUR|drangoht.itch.io/chimera-protocol",
    },
}

# ---------------------------------------------------------------------------
# EDL — (source, debut_s, duree_s, cle_texte|None, couleur)
#
#   Les timecodes viennent de la MISE EN SCENE des prises (`Bench/TrailerRecorder.cs`), pas d'un
#   reperage a la planche-contact : la prise `meta` montre six ecrans de 3 s dans un ordre fixe, la
#   prise `chimera` trois jeux de greffes de 3 s, `intro` deroule ses six beats aux memes instants
#   a chaque capture. Une recapture rend donc le meme decoupage -- ce qui n'etait pas vrai des
#   rushes Godot, tires de runs aleatoires.
#
#   Rythme : plans longs a l'ouverture (narration), de plus en plus courts a l'escalade.
#   Verifie sur planches : `python tools/trailer_sheets.py <source> --step 1`.
# ---------------------------------------------------------------------------
EDL = [
    # -- A. Ouverture narrative (cinematique d'intro, ~9 s)
    #    Ouverture DIRECTE sur la cinematique, sans plan d'action prealable : le trailer s'installe
    #    sur la narration. Les plans sont sombres, d'ou le fondu d'ouverture court (0.4 s) de
    #    finalize() -- rallonger le noir de tete tuerait la retention YouTube.
    #    Beats retenus : l'origine (0.5-3.5), la naissance de la Rouille (10-13), puis le beat 6
    #    (22-26) qui ENONCE le pitch du jeu (« tear a piece of it free — and let it become part of
    #    you »). Les six beats tiennent 0-26 s ; le titre est devoile a 27.5.
    ("intro",          0.8, 3.0, None, None),
    ("intro",         10.2, 3.0, None, None),
    ("intro",         22.2, 3.4, None, None),

    # -- B. Le jeu : les cinq biomes (~11 s)
    #    Un plan par biome, dans l'ordre de menace croissante du jeu. Chaque prise est deja calee sur
    #    sa minute de run (`--start-at`), donc n'importe quel instant du rush est « en pleine action »
    #    : les bornes ne servent qu'a varier les compositions.
    #    Raccourcis de 2.6 a 2.2 a la refonte du 2026-08-22 : cinq plans d'arene qui se ressemblent
    #    sont la partie du trailer qui ressemble le plus a tous les autres survivors. Le temps gagne
    #    va a la Maree (section E), qui est ce que ce jeu a et que les autres n'ont pas.
    #    ⚠ Sanctuaire s'arrete a 5.4 : une modale d'Assimilation s'ouvre vers 10 s dans ce rush.
    #    `KeepClear` la referme en une image, mais une image de modale plein ecran au milieu d'un
    #    plan se voit — c'est un flash blanc.
    ("gp_sanctuaire",  2.6, 2.2, "SWARM", CYAN),
    ("gp_aether",      8.4, 2.2, None, None),
    ("gp_givre",      11.4, 2.2, None, None),
    ("gp_neon",       14.2, 2.2, None, None),
    ("gp_fournaise",  11.4, 2.2, None, None),

    # -- C. Progression : la decision, puis le corps qui mute (~7 s)
    #    L'ecran de montee de niveau est presente des la premiere image de sa prise. Le gros plan de
    #    chimere enchaine trois jeux de greffes de 3 s : corps nu+oeil (0-3), onde/servos/symbiote
    #    (3-6), les deux fusions (6-9). Le dernier porte le plus d'appendices — d'ou le carton
    #    « BECOME THE CHIMERA » dessus, et non sur le premier.
    ("levelup",        0.4, 2.4, None, None),
    ("chimera",        3.4, 2.2, "ORGANS", GOLD),
    ("chimera",        6.2, 2.4, "CHIMERA", GOLD),

    # -- D. Le boss (~5 s)
    #    ⚠ Ces timecodes ont ete refaits le 2026-08-22, et l'ancien commentaire disait pourquoi il
    #    fallait s'en mefier : « il reste LOIN du joueur la plupart du temps, a 20 s ils sont enfin
    #    dans le meme cadre ». C'etait de la CHANCE, et elle n'a pas survecu a la recapture — le boss
    #    n'apparaissait sur aucune des 26 secondes, alors que le journal confirmait son invocation a
    #    t=784 s. `TrailerRecorder.TakeBoss` le ramene desormais dans le cadre : il y est en
    #    permanence, et n'importe quel instant du rush le montre.
    #    Instants choisis pour l'action, pas pour sa presence : 11.0 (boss + nuee), 17.4 (explosion).
    #    ⚠ Eviter 6.0 et 14.0 : un bandeau de fusion violet y occupe le haut du cadre.
    #    ⚠ Les deux plans d'action qui precedaient le boss (gp_fournaise 17.4, gp_neon 5.4) ont ete
    #    RETIRES a la refonte : a ce point du montage le spectateur a deja vu trente secondes
    #    d'arene, et ces plans ne faisaient que retarder le seul argument neuf du trailer.
    ("boss",          11.0, 2.0, None, None),
    ("boss",          17.4, 2.8, "BOSS", CYAN),

    # -- E. LA MAREE DE ROUILLE (~8 s) — l'argument que le jeu n'avait pas
    #    Place APRES le boss, parce que c'est ce qui vient apres lui dans le jeu : on le bat, la run
    #    continue, et l'arene se referme quand meme. Le mettre plus tot dirait le contraire.
    #
    #    ⚠ Les deux premiers plans sont DEZOOMES (cf. `TrailerRecorder.TakeTide`) : au cadrage de
    #    jeu, la maree n'est qu'un bord orange qui entre par un cote, et « l'arene se referme » ne se
    #    lit pas. Les deux plans larges sont espaces (3.6 et 8.6) parce que la geometrie, elle, ne
    #    bouge quasiment pas en cinq secondes — la fraction sure perd 1 % : ce qui les distingue est
    #    l'ACTION, pas la fermeture. Deux plans larges colles se liraient comme un seul plan fixe.
    #    Le troisieme revient au cadrage de jeu et entre a 18.0 : la nuee y est massee SUR l'ilot de
    #    terrain sur, ce qui dit « il n'y a plus ou aller » mieux qu'aucun carton. Pas avant 13.0, la
    #    camera mettant environ une demi-seconde a glisser du centre vers le joueur apres la bascule.
    ("tide",           3.6, 2.8, "TIDE", GOLD),
    ("tide",           8.6, 2.4, None, None),
    ("tide",          18.0, 3.8, "ENDS", GOLD),

    # -- F. Meta / menus (~7 s)
    #    Ordre de la prise `meta`, releve sur planche : hub 0-2.8, carte des niveaux 2.8-5.2, puis
    #    les trois onglets du Codex (bestiaire 5.2-8.2, arsenal 8.2-11.2, chimere 11.2-14.2) et les
    #    defis 14.2-17.2. Les bornes sont prises au MILIEU de chaque fenetre : la mise en scene monte
    #    l'ecran suivant sur une frame, et une coupe posee sur la bascule attraperait le precedent.
    #    Le bestiaire (5.2-8.2) est ECARTE : il montre ce qu'on affronte, quand l'arsenal et la
    #    chimere montrent ce qu'on devient. Les defis (15.0) le sont aussi depuis la refonte.
    #    ⚠ Et l'onglet arsenal (9.0) a saute au CONTROLE SUR PLANCHE du montage final : cote a cote,
    #    les deux onglets du Codex rendent deux images de liste sombres et pratiquement identiques.
    #    Elles se distinguent en jouant, pas a 1,8 s sur une planche-contact — trois ecrans de menu
    #    apres le boss ET la maree, c'est deja la limite. Le temps rendu va aux deux plans qui
    #    portent le trailer : la maree serree et le carton final.
    #
    #    2026-08-22 (2.5.0) — L'ecran de CHOIX DE PERSONNAGE prend la place de la carte des niveaux,
    #    par ECHANGE et non par ajout : la limite ci-dessus tient toujours, et sur une image fixe de
    #    1,8 s quatre silhouettes a choisir se lisent, une carte de biomes non. Il est place EN TETE
    #    de la section parce qu'il porte le carton CONTENT : le seul endroit du montage ou le chiffre
    #    annonce et l'image qui l'atteste sont dans le meme plan. Le carton tombe a h-260, dans la
    #    bande vide entre la derniere carte (y=540 en source) et le bouton « Back » (y=650) — verifie
    #    sur planche, pas deduit.
    #    ⚠ Le rush `charsel` est fixe des sa 6e image (aucun fondu d'ouverture) : n'importe quel
    #    instant convient, 1.2 est arbitraire. Et il date du 2026-08-22 14:15, soit APRES la
    #    correction des trois defauts de mise en page de cet ecran — ils ne sont pas dans l'image.
    ("charsel",        1.2, 2.0, "CONTENT", CYAN),
    ("meta",           0.8, 1.8, None, None),
    ("meta",          12.0, 1.8, None, None),

    # -- F. Final (~8 s)
    #    27.0 et pas 26.2 : le beat 6 tient l'ecran jusqu'a 26.0 et le flash blanc part a 26.5. Une
    #    coupe posee avant lui rendait une seconde de plan MORT — le drone, sans texte, sans
    #    mouvement — juste avant le seul moment spectaculaire de la cinematique.
    #    Deux plans plutot qu'un seul long : le carton itch.io tombe ainsi sur le menu, et pas
    #    par-dessus la tagline du jeu qu'affiche l'ecran-titre.
    ("intro",         27.0, 3.6, None, None),
    ("menu",           0.6, 5.0, "STORE", GOLD),
]

TOTAL = sum(e[2] for e in EDL)

# ---------------------------------------------------------------------------
# EDL MUSICALE — (piste, t_entree_s)
#   Trois morceaux de la bande-son du jeu (metal industriel, 1.17.0) enchaines par
#   fondu croise de XFADE. Les bornes sont calees sur la structure du montage :
#     0.0   theme principal      — narration de la cinematique
#     9.4   run neon (refrain)   — premiere image de gameplay, 160 BPM
#    27.4   theme de boss        — premier plan de boss, tenu par-dessus la Maree jusqu'au final
#
#   Choix des pistes : PAS `music_intro.ogg`, alors que c'est la musique qui joue sur les plans de
#   cinematique -- depuis que les rushes sont muets, elle ne s'entend plus dans le rush, mais la
#   remonter ici ferait entrer le trailer sur la piste la plus calme du jeu.
#
#   ⚠ C'est la SEULE source sonore du montage : les rushes Unity n'ont pas d'audio, donc plus
#   aucun transitoire de jeu (tirs, impacts, ramassages) ne vient texturer les plans d'action.
# ---------------------------------------------------------------------------
MUSIC_EDL = [
    ("unity/Assets/Resources/Audio/music/music_menu.ogg",             0.0),
    ("unity/Assets/Resources/Audio/music/music_run_neon_combat.ogg",  9.4),
    ("unity/Assets/Resources/Audio/music/music_run_boss.ogg",        27.4),
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
        rush = os.path.join(RAW, f"{src}.mp4")
        if not os.path.exists(rush):
            raise SystemExit(f"Rush manquant : {rush} (relancer tools/record_trailer.py {src})")
        out = os.path.join(CLIPS, f"{i:02d}_{src}.mp4")

        # NEAREST : upscale x2 exact, aucun lissage -> le pixel art reste net.
        vf = ["scale=2560:1440:flags=neighbor"]
        if text:
            vf.extend(text_filters(i, text, color, dur))
        vf.append("fps=60,format=yuv420p")

        # `-an` : les rushes Unity sont muets, mais un plan SANS piste audio et un plan qui en a une
        # ne se concatenent pas par copie -- le demuxer `concat` exige des flux identiques. On coupe
        # donc explicitement, pour que tous les intermediaires aient la meme forme.
        run([
            "ffmpeg", "-v", "error", "-y",
            "-ss", f"{start}", "-i", rush, "-t", f"{dur}",
            "-vf", ",".join(vf), "-an",
            "-c:v", "libx264", "-crf", "14", "-preset", "veryfast",
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
    """Musique continue par-dessus le montage muet, fondus d'ouverture/fermeture, encodage YouTube."""
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
        # Normalisation de diffusion : YouTube ramene tout a -14 LUFS, et un master plus fort y est
        # simplement attenue -- en gardant ses ecretages. TP=-1.5 dBTP laisse une marge au
        # reencodage lossy de la plateforme.
        f"[mus]volume=0.95,afade=t=out:st={TOTAL - 2.6}:d=2.6,"
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
