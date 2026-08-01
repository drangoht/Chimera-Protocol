#!/usr/bin/env python3
"""Banc de la BOUCLE DE PUISSANCE — pourquoi l'accumulation gagne contre les crans.

Pourquoi cet outil existe
-------------------------
Le « temps soutenable » (cf. `power_curve_multi.py`) mesure une PRESSION : la part du temps où les PV
rendus couvrent les PV perdus. Il a servi à valider les crans de saturation du lot 1 — chacun devait
le faire baisser de plus de 6 %. Le cran I y est passé (−10,0 %, 4/4).

Et pourtant, le 2026-08-01, le testeur a joué **l'échelle complète (crans 1 à 5)** et rapporté
« aucune difficulté particulière, aucun mal à finir le niveau ». Le critère avait validé une échelle
qui ne se sent pas. Motif : le temps soutenable compare deux flux À UN INSTANT DONNÉ ; il est aveugle
à la vitesse à laquelle le joueur ACCUMULE. Or c'est l'accumulation qui gagne :

    survivre → gagner des niveaux → cartes de surcharge SANS PLAFOND → survivre mieux

Un cran borné (soins ×0,6, densité ×1,25, élites ×3) ne peut pas rattraper une boucle à
contre-réaction positive : il suffit d'attendre assez de niveaux. Pire, les crans qui ajoutent des
ennemis ou des élites ajoutent aussi de l'XP (une élite vaut ×2,5 à ×3) et des orbes de soin
(`hpDropChance` 0,08 → ~0,27), donc ils ALIMENTENT la boucle qu'ils prétendent contrer.

Ce que cet outil mesure, et que l'autre ne voit pas :

* **niveaux/min en overtime** — le débit du moteur d'accumulation ;
* **pente des PV max** (PV/min) — la défense achetée par ces niveaux ;
* **soins ponctuels** (PV/s) — le canal de soin dominant, celui qu'Hémorragie vise ;
* **kills/min** — d'où vient l'XP.

Lecture attendue si l'hypothèse est juste : à cran élevé, niveaux/min et PV max/min MONTENT au lieu
de descendre. Un cran qui accélère la boucle est un cran qui se retourne contre lui-même.

Usage
-----
    py tools/power_loop.py                       # toutes les runs du journal, groupées par cran
    py tools/power_loop.py --log <chemin>         # un autre journal
    py tools/power_loop.py --min-samples 8        # ignore les runs trop courtes

Le journal doit contenir la ligne « · saturation N » dans l'en-tête (ajoutée le 2026-08-01) ; les
runs plus anciennes sont rangées sous « sat ? » et ne se comparent à rien.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path
from statistics import median

DEFAULT_LOG = Path.home() / "AppData/Roaming/Godot/app_userdata/Chimera Protocol/power_curve.log"

# Noms de colonnes utilisés, tels qu'écrits par PowerTelemetry.ComposeHeader.
# ⚠ Indexer par NOM et non par position : le CSV a gagné des colonnes au fil des versions
# (`regen_eff_ps` et `soins_ps` en 1.24), si bien qu'une position fixe lit les greffes d'une run
# ancienne comme un nombre. Chaque run porte sa propre ligne de titres.
NEEDED = ("t_s", "phase", "niveau", "degats_subis_ps", "kills_fenetre", "pv_max", "soins_ps")


class Run:
    """Une run du journal : son en-tête, sa table de colonnes et ses échantillons d'overtime."""

    def __init__(self, header: str, columns: list[str]):
        self.sat = self._grab(r"saturation (\d+)", header)
        self.seed = self._grab(r"# seed (\d+)", header)
        m = re.search(r"biome (\w+)", header)
        self.biome = m.group(1) if m else "?"
        self.col = {name: i for i, name in enumerate(columns)}
        self.samples: list[list[str]] = []

    @property
    def usable(self) -> bool:
        """Une run d'un format trop ancien pour porter toutes les colonnes n'est pas comparable."""
        return all(c in self.col for c in NEEDED)

    @staticmethod
    def _grab(pattern: str, text: str) -> int | None:
        m = re.search(pattern, text)
        return int(m.group(1)) if m else None

    def metric(self, key: str) -> list[float]:
        return [float(s[self.col[key]]) for s in self.samples]

    def per_minute(self, key: str) -> float | None:
        """Pente d'une grandeur CUMULATIVE (niveau, PV max) en unités/minute d'overtime."""
        if len(self.samples) < 2:
            return None
        vals, times = self.metric(key), self.metric("t_s")
        span = (times[-1] - times[0]) / 60.0
        return (vals[-1] - vals[0]) / span if span > 0 else None


def parse(path: Path) -> list[Run]:
    runs: list[Run] = []
    header, cur = [], None
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("=== Courbe de puissance"):
            header, cur = [line], None
            continue
        if line.startswith("#"):
            header.append(line)
            continue
        if line.startswith("t_s;"):          # ligne de titres : l'en-tête est complet
            cur = Run("\n".join(header), line.split(";"))
            runs.append(cur)
            continue
        if cur is not None and ";" in line:
            f = line.split(";")
            # Seul l'OVERTIME nous intéresse : c'est la fenêtre que les crans visent, et la seule où
            # les cartes de surcharge existent.
            if len(f) > max(cur.col.values()) and f[cur.col["phase"]] == "OT":
                cur.samples.append(f)
    return [r for r in runs if r.usable]


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--log", type=Path, default=DEFAULT_LOG)
    ap.add_argument("--min-samples", type=int, default=5,
                    help="nombre minimal d'échantillons d'overtime pour retenir une run")
    args = ap.parse_args()

    if not args.log.exists():
        print(f"Journal introuvable : {args.log}", file=sys.stderr)
        return 1

    runs = [r for r in parse(args.log) if len(r.samples) >= args.min_samples]
    if not runs:
        print("Aucune run exploitable.", file=sys.stderr)
        return 1

    by_sat: dict[str, list[Run]] = {}
    for r in runs:
        by_sat.setdefault("?" if r.sat is None else str(r.sat), []).append(r)

    print(f"{len(runs)} runs · journal {args.log.name}\n")
    print(f"{'cran':>4} {'runs':>4} {'niv/min':>9} {'PVmax/min':>10} {'soins PV/s':>11} "
          f"{'kills/min':>10} {'subis PV/s':>11}")
    print("-" * 64)

    for sat in sorted(by_sat, key=lambda k: (k == "?", k)):
        group = by_sat[sat]

        def med(fn):
            vals = [v for v in (fn(r) for r in group) if v is not None]
            return median(vals) if vals else float("nan")

        # kills_fenetre est un compte PAR ÉCHANTILLON (15 s de jeu) : ramené à la minute.
        print(f"{sat:>4} {len(group):>4} "
              f"{med(lambda r: r.per_minute('niveau')):>9.1f} "
              f"{med(lambda r: r.per_minute('pv_max')):>10.0f} "
              f"{med(lambda r: median(r.metric('soins_ps'))):>11.1f} "
              f"{med(lambda r: median(r.metric('kills_fenetre')) * 4):>10.0f} "
              f"{med(lambda r: median(r.metric('degats_subis_ps'))):>11.1f}")

    print("\nLecture : un cran qui MONTE la colonne « niv/min » nourrit la boucle de puissance —")
    print("il ajoute de la menace et, du même geste, de quoi l'absorber.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
