"""
analyze_music — contrôle qualité objectif des pistes générées.

Sans écouter, on peut vérifier l'essentiel : niveaux, hiérarchie fréquentielle
entre stems (brief §4.1), propreté du bouclage, et absence de saturation quand
les 4 stems d'un biome sont sommés à pleine intensité (brief §4.4).

Usage :
  python tools/analyze_music.py                        # assets/audio/music
  python tools/analyze_music.py --dir build/music_preview
  python tools/analyze_music.py --dir build/music_preview --biome sanctuaire
"""

from __future__ import annotations

import argparse
import glob
import os
import subprocess
import sys
import tempfile
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import synth_lib as S  # noqa: E402

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Bandes d'analyse alignées sur la hiérarchie fréquentielle du brief
BANDS = [
    ("sub", 20, 80),
    ("bass", 80, 250),
    ("lowmid", 250, 800),
    ("mid", 800, 2500),
    ("high", 2500, 6000),
    ("air", 6000, 16000),
]


def load(path: str) -> tuple[np.ndarray, int]:
    """Décode un OGG/WAV en (n, 2) float via ffmpeg."""
    if path.lower().endswith(".wav"):
        with wave.open(path, "rb") as f:
            sr = f.getframerate()
            raw = np.frombuffer(f.readframes(f.getnframes()), dtype="<i2")
            return raw.reshape(-1, f.getnchannels()).astype(np.float64) / 32768.0, sr

    with tempfile.TemporaryDirectory() as tmp:
        wav = os.path.join(tmp, "d.wav")
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", path,
                        "-c:a", "pcm_s16le", wav], check=True)
        return load(wav)


def band_energy(x: np.ndarray, sr: int) -> dict[str, float]:
    """Énergie relative par bande, en dB sous le total."""
    mono = S.to_mono(x)
    spec = np.abs(np.fft.rfft(mono * np.hanning(len(mono)))) ** 2
    freqs = np.fft.rfftfreq(len(mono), 1 / sr)
    total = float(spec.sum()) + 1e-12
    return {name: 10 * np.log10(float(spec[(freqs >= lo) & (freqs < hi)].sum()) / total + 1e-12)
            for name, lo, hi in BANDS}


def loop_seam(x: np.ndarray, sr: int, window: float = 0.05) -> float:
    """
    Écart RMS entre la fin et le début de la boucle, en dB.
    Proche de 0 = raccord indolore ; très négatif ou très positif = marche audible.
    """
    w = S.n_samples(window, sr)
    head = float(np.sqrt(np.mean(x[:w] ** 2))) + 1e-9
    tail = float(np.sqrt(np.mean(x[-w:] ** 2))) + 1e-9
    return 20 * np.log10(tail / head)


def describe(path: str) -> dict:
    x, sr = load(path)
    mono = S.to_mono(x)
    rms = float(np.sqrt(np.mean(mono ** 2)))
    peak = float(np.max(np.abs(x)))
    # Corrélation inter-canaux : 1 = mono, 0 = très large, < 0 = déphasage suspect
    corr = float(np.corrcoef(x[:, 0], x[:, 1])[0, 1]) if x.shape[1] == 2 else 1.0
    return {
        "name": os.path.basename(path).replace(".ogg", ""),
        "dur": len(x) / sr,
        "rms_db": 20 * np.log10(rms + 1e-9),
        "peak_db": 20 * np.log10(peak + 1e-9),
        "crest": 20 * np.log10(peak / (rms + 1e-9)),
        "seam_db": loop_seam(mono, sr),
        "corr": corr,
        "bands": band_energy(x, sr),
        "audio": x,
        "sr": sr,
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default=os.path.join(PROJECT_ROOT, "assets", "audio", "music"))
    ap.add_argument("--biome", help="vérifie en plus la somme des 4 stems de ce biome")
    args = ap.parse_args()

    files = sorted(glob.glob(os.path.join(args.dir, "*.ogg")))
    if not files:
        print(f"Aucun .ogg dans {args.dir}", file=sys.stderr)
        return 1

    hdr = f"{'piste':34s} {'durée':>6s} {'RMS':>7s} {'pic':>7s} {'crête':>6s} {'raccord':>8s} {'L/R':>5s}  "
    hdr += " ".join(f"{n:>6s}" for n, _, _ in BANDS)
    print(hdr)
    print("-" * len(hdr))

    reports = {}
    for f in files:
        d = describe(f)
        reports[d["name"]] = d
        bands = " ".join(f"{d['bands'][n]:6.1f}" for n, _, _ in BANDS)
        print(f"{d['name']:34s} {d['dur']:5.1f}s {d['rms_db']:6.1f} {d['peak_db']:6.1f} "
              f"{d['crest']:5.1f} {d['seam_db']:7.1f} {d['corr']:5.2f}  {bands}")

    print("\nBandes en dB sous l'énergie totale de la piste. `raccord` = écart de "
          "niveau fin/début\nde boucle (|x| < 3 dB = indolore). `L/R` = corrélation "
          "stéréo (1 = mono, < 0 = déphasé).")

    if args.biome:
        stems = [f"music_run_{args.biome}_{s}" for s in ("bed", "pulse", "lead", "boss")]
        missing = [s for s in stems if s not in reports]
        if missing:
            print(f"\nStems manquants : {', '.join(missing)}", file=sys.stderr)
            return 1

        # Niveaux relatifs du brief §4.2
        gains = {"bed": S.db2lin(-6), "pulse": S.db2lin(-4),
                 "lead": S.db2lin(-3), "boss": S.db2lin(0)}
        n = min(len(reports[s]["audio"]) for s in stems)
        for label, active in (("bed seul", ["bed"]),
                              ("bed+pulse", ["bed", "pulse"]),
                              ("bed+pulse+lead", ["bed", "pulse", "lead"]),
                              ("les 4 (boss)", ["bed", "pulse", "lead", "boss"])):
            mixdown = np.zeros((n, 2))
            for s in active:
                mixdown += reports[f"music_run_{args.biome}_{s}"]["audio"][:n] * gains[s]
            peak = float(np.max(np.abs(mixdown)))
            rms = float(np.sqrt(np.mean(S.to_mono(mixdown) ** 2)))
            flag = "  ÉCRÊTAGE" if peak > 1.0 else ""
            print(f"  {label:16s} pic {20 * np.log10(peak + 1e-9):6.2f} dBFS   "
                  f"RMS {20 * np.log10(rms + 1e-9):6.2f} dBFS{flag}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
