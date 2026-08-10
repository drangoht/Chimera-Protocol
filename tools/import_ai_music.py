"""
import_ai_music — intègre les musiques générées par IA dans le jeu.

Prend les fichiers déposés dans `music_ai/` (n'importe quel format), les prépare
et les installe dans `unity/Assets/Resources/Audio/music/` sous le nom attendu par le moteur.

Le travail utile, celui qu'aucun générateur IA ne fait :

  1. **Bouclage.** Suno/Udio/Lyria produisent des morceaux avec une intro, une
     outro et un fade — injouables tels quels en boucle. Le script cherche le
     meilleur point de raccord par corrélation, coupe la piste là, et fond la fin
     sur le début. Résultat : une boucle qui tourne indéfiniment sans blanc.
  2. **Loudness homogène.** Deux générations successives peuvent avoir 6 dB
     d'écart ; toutes les pistes sont recalées à la même cible EBU R128.
  3. Conversion en OGG Vorbis 44,1 kHz stéréo + nommage attendu par `MusicDirector`.

Usage :
  python tools/import_ai_music.py                 # traite tout ce qui est présent
  python tools/import_ai_music.py --list          # état de chaque piste attendue
  python tools/import_ai_music.py --only sanctuaire_calm
  python tools/import_ai_music.py --only menu --keep-preview   # écoute avant install

Les prompts et la liste des pistes attendues : `docs/AUDIO_AI_PROMPTS.md`.
"""

from __future__ import annotations

import argparse
import glob
import os
import re
import subprocess
import sys
import tempfile

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import synth_lib as S  # noqa: E402
import unity_paths  # noqa: E402

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
INBOX = os.path.join(PROJECT_ROOT, "music_ai")
MUSIC_DIR = str(unity_paths.audio_dir("music"))
PREVIEW_DIR = os.path.join(PROJECT_ROOT, "build", "music_preview")

BIOMES = ["sanctuaire", "aether", "givre", "fournaise", "neon"]
SOURCE_EXTS = (".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac", ".opus", ".wma")

# Durée de la cinématique d'intro (IntroScreen) — la piste est calée dessus.
INTRO_LENGTH_SEC = 94.0

# Loudness cible des pistes de run.
#
# Volontairement BAS pour de la musique : ces morceaux sont du metal très
# compressé, dont le RMS reste haut en permanence, là où les SFX du jeu sont des
# transitoires courts (un ramassage d'XP tourne autour de -30 dB RMS). À -16 LUFS
# — le niveau habituel d'une musique de jeu — la bande-son couvrait purement et
# simplement les SFX. Mesuré en jeu, `-22` laisse les effets passer devant sans
# rendre la musique timide.
MUSIC_LUFS = -22.0

# Marge de crête à l'encodage.
TRUE_PEAK_DB = -1.5


class Track:
    """Une piste attendue : son identifiant de dépôt et son nom final en jeu."""

    def __init__(self, key: str, target: str, loop: bool = True,
                 lufs: float = MUSIC_LUFS, fixed_length: float | None = None,
                 note: str = ""):
        self.key = key                    # nom du fichier déposé (sans extension)
        self.target = target              # nom final dans unity/Assets/Resources/Audio/music/
        self.loop = loop
        self.lufs = lufs
        self.fixed_length = fixed_length
        self.note = note


TRACKS: list[Track] = [
    Track("menu", "music_menu", lufs=-23.0, note="thème principal"),
    Track("hub", "music_hub", lufs=-23.0, note="l'enclave"),
    Track("intro", "music_intro", loop=False, lufs=-21.0,
          fixed_length=INTRO_LENGTH_SEC, note="cinématique, non bouclée"),
]
for _b in BIOMES:
    TRACKS.append(Track(f"{_b}_calm", f"music_run_{_b}_calm", note=f"{_b} — exploration"))
    TRACKS.append(Track(f"{_b}_combat", f"music_run_{_b}_combat", note=f"{_b} — combat"))
TRACKS.append(Track("boss", "music_run_boss", note="boss, commun à tous les biomes"))

BY_KEY = {t.key: t for t in TRACKS}


# ---------------------------------------------------------------------------
# Entrées / sorties
# ---------------------------------------------------------------------------

def decode(path: str, sr: int = S.SR) -> np.ndarray:
    """N'importe quel format audio → (n, 2) float64 au taux du projet, via ffmpeg."""
    with tempfile.TemporaryDirectory() as tmp:
        wav = os.path.join(tmp, "decoded.wav")
        subprocess.run(
            ["ffmpeg", "-y", "-loglevel", "error", "-i", path,
             "-ac", "2", "-ar", str(sr), "-c:a", "pcm_s16le", wav],
            check=True)
        import wave
        with wave.open(wav, "rb") as f:
            raw = np.frombuffer(f.readframes(f.getnframes()), dtype="<i2")
    return raw.reshape(-1, 2).astype(np.float64) / 32768.0


def measure_lufs(path: str) -> float | None:
    """Loudness intégré (EBU R128) d'un fichier, via le filtre `ebur128` de ffmpeg."""
    out = subprocess.run(
        ["ffmpeg", "-hide_banner", "-nostats", "-i", path,
         "-af", "ebur128", "-f", "null", "-"],
        capture_output=True, text=True).stderr

    # Le résumé de fin réécrit "I:" une dernière fois — c'est celui-là qui compte.
    matches = re.findall(r"I:\s*(-?\d+\.?\d*)\s*LUFS", out)
    return float(matches[-1]) if matches else None


def apply_loudness(x: np.ndarray, target_lufs: float, sr: int = S.SR) -> np.ndarray:
    """
    Cale le loudness d'un signal sur une cible, par un gain CONSTANT.

    On ne passe pas par le filtre `loudnorm` de ffmpeg : en une passe il travaille
    en mode dynamique, donc il compresse et limite — sur un morceau déjà masterisé
    c'est une seconde compression non désirée, et la cible est ratée de plus d'un
    dB (mesuré : -14.3 pour -16 demandé). Mesurer puis appliquer un gain fixe est
    exact et laisse la dynamique du morceau intacte.
    """
    with tempfile.TemporaryDirectory() as tmp:
        probe = S.write_wav(os.path.join(tmp, "probe.wav"), x, sr, bits=16)
        measured = measure_lufs(probe)

    if measured is None:
        return x

    x = x * S.db2lin(target_lufs - measured)

    # Le gain peut faire dépasser la marge de crête : on redescend le tout plutôt
    # que d'écrêter (un limiteur ici recompresserait le master).
    ceiling = S.db2lin(TRUE_PEAK_DB)
    peak = float(np.max(np.abs(x)))
    if peak > ceiling:
        x = x * (ceiling / peak)

    return x


def find_source(key: str) -> str | None:
    """Cherche le fichier déposé pour une piste (tolère les suffixes _v1, _v2…)."""
    exact = [p for ext in SOURCE_EXTS
             for p in glob.glob(os.path.join(INBOX, key + ext))]
    if exact:
        return exact[0]

    # Variantes : on prend la plus récente, l'utilisateur itère en général vers le haut
    variants = [p for ext in SOURCE_EXTS
                for p in glob.glob(os.path.join(INBOX, key + "_v*" + ext))]
    return max(variants, key=os.path.getmtime) if variants else None


# ---------------------------------------------------------------------------
# Préparation du signal
# ---------------------------------------------------------------------------

def trim_silence(x: np.ndarray, threshold_db: float = -45.0,
                 sr: int = S.SR) -> np.ndarray:
    """
    Retire le silence (et le quasi-silence des fondus) au début et à la fin.
    Les générateurs IA ouvrent et ferment presque toujours sur un fade.
    """
    env = np.abs(S.to_mono(x))
    # Enveloppe lissée sur 50 ms : un passage doux ne doit pas être pris pour du silence.
    # Moyenne glissante par somme cumulée — O(n) ; np.convolve serait O(n·win),
    # soit ~10^10 opérations sur un morceau de trois minutes.
    win = max(1, S.n_samples(0.05, sr))
    csum = np.concatenate(([0.0], np.cumsum(env)))
    env = (csum[win:] - csum[:-win]) / win
    env = np.pad(env, (win // 2, len(x) - len(env) - win // 2), mode="edge")

    thresh = S.db2lin(threshold_db) * max(np.max(env), 1e-9)
    loud = np.where(env > thresh)[0]
    if len(loud) == 0:
        return x

    return x[loud[0]:loud[-1] + 1]


def find_loop_length(x: np.ndarray, sr: int = S.SR, min_loop: float = 25.0,
                     window: float = 3.0, loop_tolerance: float = 0.90) -> int:
    """
    Cherche la longueur de boucle qui recolle le mieux.

    On compare une fenêtre prise au début du morceau à toutes les positions
    candidates : la meilleure correspondance indique un endroit où la musique
    « repasse au même point », donc un raccord naturel. C'est ce qui évite le
    coup de ciseau brutal d'une coupe arbitraire.

    Renvoie la longueur en échantillons.
    """
    n = len(x)
    mono = S.to_mono(x)
    w = S.n_samples(window, sr)
    lo = S.n_samples(min_loop, sr)

    # Marge : il faut de la matière après le point de boucle pour le fondu
    hi = n - w
    if hi <= lo or n < lo + w:
        return n  # morceau trop court pour chercher : on le prend en entier

    seg = mono[:hi + w]
    seg = seg - seg.mean()
    ref = seg[:w]
    ref_norm = np.linalg.norm(ref) + 1e-9

    # Corrélation par FFT. `np.correlate` travaille en direct : O(n·w), soit plus
    # de 10^12 opérations pour un morceau de trois minutes contre une fenêtre de
    # trois secondes — le script ne finirait jamais.
    m = len(seg)
    nfft = 1 << int(np.ceil(np.log2(m + w)))
    spec = np.fft.rfft(seg, nfft) * np.conj(np.fft.rfft(ref, nfft))
    corr = np.fft.irfft(spec, nfft)[:m - w + 1]

    # Normalisation par l'énergie locale : sinon les passages forts gagnent toujours.
    # Somme cumulée, pour la même raison de complexité que la corrélation.
    csum = np.concatenate(([0.0], np.cumsum(seg ** 2)))
    energy = np.sqrt(np.maximum(csum[w:] - csum[:-w], 0.0)) + 1e-9

    score = corr / (energy * ref_norm)

    # Garde-fou : un point de boucle ne doit pas tomber dans une baisse d'énergie.
    # Les générateurs terminent presque toujours sur une outro qui retombe ; y
    # boucler ferait chuter le morceau à chaque tour. `energy` est l'énergie de la
    # fenêtre de raccord, on s'en sert directement.
    quiet = energy < np.median(energy) * 0.70
    score = np.where(quiet, -np.inf, score)

    candidates = score[lo:hi]
    if len(candidates) == 0 or not np.isfinite(np.max(candidates)):
        return n

    # À qualité de raccord comparable, on prend la boucle LA PLUS LONGUE : sur un
    # morceau très répétitif, le meilleur score absolu tombe souvent sur le premier
    # retour du riff (~40 s) et on jetterait les deux tiers de la matière générée.
    # Le joueur, lui, entend la répétition bien avant d'entendre une couture.
    best = float(np.max(candidates))
    if best > 0.0:
        acceptable = np.where(candidates >= best * loop_tolerance)[0]
        if len(acceptable) > 0:
            return int(lo + acceptable[-1])

    return int(lo + np.argmax(candidates))


def make_loop(x: np.ndarray, sr: int = S.SR, crossfade: float = 2.0,
              min_loop: float = 25.0,
              loop_tolerance: float = 0.90) -> tuple[np.ndarray, float]:
    """
    Transforme un morceau linéaire en boucle sans couture.

    On coupe à la longueur de boucle détectée, puis on fond la matière qui suit
    (`x[L:L+cf]`) par-dessus le tout début : à la relecture, la fin enchaîne
    exactement sur le début.
    """
    n = len(x)
    loop_n = find_loop_length(x, sr, min_loop, window=3.0,
                              loop_tolerance=loop_tolerance)
    cf = min(S.n_samples(crossfade, sr), loop_n // 4)

    if loop_n >= n or cf <= 0:
        # Pas assez de matière : on se contente d'un fondu bout-à-bout
        cf = min(S.n_samples(crossfade, sr), n // 4)
        out = x[:n - cf].copy()
        if cf > 0 and len(out) > cf:
            ramp = np.linspace(0.0, 1.0, cf)[:, None]
            out[:cf] = out[:cf] * ramp + x[n - cf:n] * (1.0 - ramp)
        return out, len(out) / sr

    out = x[:loop_n].copy()
    tail = x[loop_n:loop_n + cf]
    if len(tail) == cf:
        ramp = np.linspace(0.0, 1.0, cf)[:, None]
        out[:cf] = out[:cf] * ramp + tail * (1.0 - ramp)

    return out, loop_n / sr


def fit_length(x: np.ndarray, target: float, sr: int = S.SR) -> np.ndarray:
    """Cale une piste non bouclée sur une durée exacte (fondu sortant si trop longue)."""
    n_target = S.n_samples(target, sr)
    if len(x) <= n_target:
        return S.pad_to(x, n_target)

    out = x[:n_target].copy()
    fade = min(S.n_samples(2.5, sr), n_target // 4)
    out[-fade:] *= np.linspace(1.0, 0.0, fade)[:, None] ** 0.7
    return out


# ---------------------------------------------------------------------------
# Traitement d'une piste
# ---------------------------------------------------------------------------

def process(track: Track, out_dir: str, crossfade: float, min_loop: float,
            quality: int, loop_tolerance: float = 0.90) -> str | None:
    src = find_source(track.key)
    if src is None:
        return None

    x = decode(src)
    raw_sec = len(x) / S.SR
    x = trim_silence(x)

    info = ""
    if track.fixed_length is not None:
        x = fit_length(x, track.fixed_length)
        info = f"calée à {track.fixed_length:.0f}s"
    elif track.loop:
        x, loop_sec = make_loop(x, crossfade=crossfade, min_loop=min_loop,
                                loop_tolerance=loop_tolerance)
        info = f"boucle {loop_sec:.1f}s"

    # Pas de limiteur ni de compression : la piste est déjà mixée et masterisée
    # par le générateur. On ne touche qu'au niveau global, pour que toutes les
    # pistes du jeu se répondent — et laissent la place aux SFX.
    x = apply_loudness(x, track.lufs)

    wav = S.write_wav(os.path.join(out_dir, track.target + ".wav"), x, bits=24)
    ogg = S.to_ogg(wav, os.path.join(out_dir, track.target + ".ogg"),
                   quality=quality, loudnorm_lufs=None)

    print(f"  {track.key:20s} -> {track.target}.ogg   "
          f"({raw_sec:.0f}s source, {info}, {track.lufs:.0f} LUFS, "
          f"{os.path.getsize(ogg) // 1024} Ko)")
    return ogg


# ---------------------------------------------------------------------------
# Pilotage
# ---------------------------------------------------------------------------

def show_list() -> None:
    print(f"Boîte d'entrée : {INBOX}\n")
    print(f"{'piste attendue':22s} {'source déposée':16s} {'en jeu':10s} rôle")
    print("-" * 78)

    missing = 0
    for t in TRACKS:
        src = find_source(t.key)
        installed = os.path.exists(os.path.join(MUSIC_DIR, t.target + ".ogg"))
        src_txt = os.path.basename(src) if src else "—"
        if not src:
            missing += 1
        print(f"{t.key:22s} {src_txt[:15]:16s} "
              f"{'oui' if installed else 'non':10s} {t.note}")

    print(f"\n{len(TRACKS) - missing}/{len(TRACKS)} piste(s) déposée(s). "
          f"Prompts : docs/AUDIO_AI_PROMPTS.md")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", nargs="*", metavar="ID", help="ne traiter que ces pistes")
    ap.add_argument("--list", action="store_true", help="état de chaque piste attendue")
    ap.add_argument("--keep-preview", action="store_true",
                    help="écrit dans build/music_preview/ au lieu des assets du jeu")
    ap.add_argument("--crossfade", type=float, default=2.0,
                    help="durée du fondu de raccord de boucle, en secondes")
    ap.add_argument("--min-loop", type=float, default=25.0,
                    help="longueur minimale d'une boucle, en secondes")
    ap.add_argument("--quality", type=int, default=6, help="qualité Vorbis (0-10)")
    ap.add_argument("--loop-tolerance", type=float, default=0.90,
                    help="0.90 = accepte un raccord à 90%% du meilleur score si la "
                         "boucle est plus longue ; 1.0 = qualité de raccord seule")
    args = ap.parse_args()

    if not os.path.isdir(INBOX):
        print(f"Dossier absent : {INBOX}", file=sys.stderr)
        return 1

    if args.list:
        show_list()
        return 0

    selected = TRACKS
    if args.only:
        unknown = [k for k in args.only if k not in BY_KEY]
        if unknown:
            print(f"Piste(s) inconnue(s) : {', '.join(unknown)}\n"
                  f"Attendues : {', '.join(BY_KEY)}", file=sys.stderr)
            return 2
        selected = [BY_KEY[k] for k in args.only]

    out_dir = PREVIEW_DIR if args.keep_preview else MUSIC_DIR
    os.makedirs(out_dir, exist_ok=True)

    print(f"Intégration vers {out_dir}\n")
    done = [t for t in selected
            if process(t, out_dir, args.crossfade, args.min_loop, args.quality,
                       args.loop_tolerance)]

    absent = [t.key for t in selected if find_source(t.key) is None]
    print(f"\n{len(done)} piste(s) intégrée(s).")
    if absent:
        print(f"Pas encore déposée(s) dans music_ai/ : {', '.join(absent)}")
    if done and not args.keep_preview:
        print("\nÉtape suivante : lancer Godot une fois pour importer les nouveaux .ogg\n"
              '  "…/Godot_v4.7-stable_mono_win64.exe" --headless --import')

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
