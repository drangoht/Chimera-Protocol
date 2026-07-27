"""
preview_adaptive_mix — écoute du mixage en couches de la bande-son SYNTHÉTISÉE.

⚠️ Cet outil s'applique aux stems produits par `tools/generate_music_v3.py`
(bed/pulse/lead/boss), qui ne sont plus ceux joués en jeu : depuis le passage à
une bande-son metal générée sur Suno, `MusicDirector` alterne deux pistes
complètes (calm/combat) + un thème de boss, cf. `docs/AUDIO_AI_PROMPTS.md`. Il
reste utile pour valider la bande-son de secours, régénérable sans contrainte de
licence — pointer `--dir` sur le dossier où elle a été rendue.

Assemble les 4 stems d'un biome en simulant la montée d'intensité gérée en jeu
par `MusicDirector` : on entend `bed` seul, puis l'entrée de `pulse`, puis celle
de `lead`, puis l'arrivée du boss. C'est le seul moyen de valider le mixage
adaptatif sans lancer Godot — et c'est le fichier à faire écouter pour arbitrer
la direction sonore.

Les courbes de fondu reproduisent exactement celles du C# (`StemMix` dans
`src/Core/Rules/MusicIntensity.cs`) : si l'une change, mettre l'autre à jour.

Usage :
  python tools/preview_adaptive_mix.py sanctuaire
  python tools/preview_adaptive_mix.py sanctuaire --dir build/music_preview
  python tools/preview_adaptive_mix.py --all
"""

from __future__ import annotations

import argparse
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import synth_lib as S  # noqa: E402
from analyze_music import load  # noqa: E402
from generate_music_v3 import BIOMES, STEM_MIX_DB  # noqa: E402

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STEMS = ("bed", "pulse", "lead", "boss")

# Seuils d'entrée des couches (brief §3 : pulse ~0.3, lead ~0.6). Le fondu
# s'étale sur une plage pour éviter tout effet de bascule audible.
FADE_IN = {"bed": (0.00, 0.00), "pulse": (0.22, 0.40), "lead": (0.50, 0.70)}


def smoothstep(x: np.ndarray | float, lo: float, hi: float):
    """Interpolation douce (dérivée nulle aux bornes) — pas de rupture de pente."""
    if hi <= lo:
        return np.where(np.asarray(x) >= hi, 1.0, 0.0)
    t = np.clip((np.asarray(x, dtype=float) - lo) / (hi - lo), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def stem_gain(stem: str, intensity: np.ndarray, boss: np.ndarray) -> np.ndarray:
    """Gain linéaire d'une couche pour une courbe d'intensité donnée."""
    base = S.db2lin(STEM_MIX_DB[stem])
    if stem == "boss":
        return base * boss
    lo, hi = FADE_IN[stem]
    return base * smoothstep(intensity, lo, hi)


def build(biome_id: str, src_dir: str, cycles_each: float = 1.0) -> np.ndarray:
    """
    Monte une démo : chaque palier dure `cycles_each` boucle(s), l'intensité
    monte en rampe continue et le boss arrive sur le dernier palier.
    """
    tracks = {}
    sr = S.SR
    for stem in STEMS:
        path = os.path.join(src_dir, f"music_run_{biome_id}_{stem}.ogg")
        if not os.path.exists(path):
            raise SystemExit(f"Stem manquant : {path}\n"
                             f"Générer d'abord : python tools/generate_music_v3.py "
                             f"--only {biome_id} --preview")
        tracks[stem], sr = load(path)

    loop_n = min(len(t) for t in tracks.values())
    seg = max(1, int(round(cycles_each)))
    total = loop_n * seg * 4
    t = np.arange(total) / loop_n / seg   # 0..4, un palier par unité

    # Palier 0 : exploration (bed seul) — 1 : la pression monte — 2 : combat
    # dense — 3 : boss. Rampe continue, comme en jeu.
    intensity = np.interp(t, [0.0, 0.9, 1.1, 1.9, 2.1, 3.0, 4.0],
                          [0.05, 0.10, 0.35, 0.45, 0.80, 0.92, 1.00])
    boss = smoothstep(t, 2.95, 3.15)

    out = np.zeros((total, 2))
    for stem in STEMS:
        tiled = np.tile(tracks[stem][:loop_n], (seg * 4, 1))[:total]
        out += tiled * stem_gain(stem, intensity, boss)[:, None]

    return out, sr


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("biome", nargs="?", help=f"un de : {', '.join(BIOMES)}")
    ap.add_argument("--all", action="store_true", help="tous les biomes")
    ap.add_argument("--dir", default=os.path.join(PROJECT_ROOT, "build", "music_preview"),
                    help="dossier des stems")
    ap.add_argument("--out", default=os.path.join(PROJECT_ROOT, "build", "music_preview"))
    args = ap.parse_args()

    if not args.all and not args.biome:
        ap.error("préciser un biome ou --all")

    for biome_id in (list(BIOMES) if args.all else [args.biome]):
        if biome_id not in BIOMES:
            print(f"Biome inconnu : {biome_id}", file=sys.stderr)
            return 2
        mixdown, sr = build(biome_id, args.dir)
        path = S.render(os.path.join(args.out, f"DEMO_{biome_id}_adaptatif"), mixdown,
                        sr=sr, loudnorm_lufs=-16.0, quality=6)
        print(f"  -> {os.path.basename(path)}  ({len(mixdown) / sr:.0f}s, "
              f"{os.path.getsize(path) // 1024} Ko)")
        print("     paliers : exploration -> pression -> combat dense -> BOSS")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
