#!/usr/bin/env python3
"""Banc MULTI-RUN de la courbe de puissance — N runs agrégées, avec bande de bruit.

Pourquoi cet outil existe
-------------------------
Les trois derniers chantiers d'équilibrage (overtime, cartes de surcharge, escalade) se sont réglés
à **une session jouée par valeur**. Le relevé du 2026-07-29 a montré que c'est insuffisant : à
l'entrée en overtime — *là où le réglage testé n'a encore aucun effet* — deux sessions du même
joueur différaient d'un facteur **2,4** en survie (1060 PV / 28,9 dég/s contre 745 / 48,9), selon que
l'arsenal saturait vers la 11ᵉ ou la 13ᵉ minute. Autrement dit : une run isolée mesure surtout le
tirage, et un écart de 30 % entre deux réglages n'est pas distinguable du bruit.

Cet outil enchaîne N runs pilotées (`AutoPilotPolicy`, cf. `--auto-play`), agrège les relevés de
`PowerTelemetry` et affiche, à côté de chaque médiane, **la dispersion** — donc la plus petite
différence qu'une campagne de cette taille sait détecter.

Comparaison APPARIÉE
--------------------
Les runs sont lancées sur une liste de graines déterministes (`--seed`). Relancer la campagne avec
les mêmes graines après avoir changé un réglage compare des runs **appariées** (mêmes vagues, mêmes
tirages de cartes) : le bruit de tirage s'annule dans la différence, et quelques runs suffisent là
où il en faudrait des dizaines en comparaison libre. C'est le mode à privilégier :

    py tools/power_curve_multi.py --runs 6 --out avant.json      # réglage actuel
    …modifier OvertimeEscalation.StatAcceleration…
    py tools/power_curve_multi.py --runs 6 --compare avant.json  # verdict apparié

Mesurer l'OVERTIME
------------------
Le bot ne survit pas 13 minutes de lui-même (il meurt vers la 7ᵉ) : un banc lancé au début de la run
ne verrait jamais la fenêtre à instruire. `--overtime` (= `--start-at 13 --saturate`) démarre
directement à l'entrée en overtime avec un arsenal saturé. Effet de bord bienvenu : l'état d'entrée
devient **identique d'une run à l'autre**, alors que c'est précisément là que se logeait la variance
qui empêchait de conclure. En contrepartie, la survie ainsi mesurée n'est pas celle d'un joueur qui a
construit son build lui-même — elle sert à COMPARER des réglages, pas à prédire une durée de vie.

Exemples
--------
    py tools/power_curve_multi.py --runs 5 --biome fournaise
    py tools/power_curve_multi.py --overtime --runs 6 --out ref_225.json
    py tools/power_curve_multi.py --report-only
"""

from __future__ import annotations

import argparse
import json
import os
import statistics
import subprocess
import sys
import time
from dataclasses import dataclass, field, asdict
from pathlib import Path

GODOT = Path(os.environ.get(
    "GODOT_MONO",
    r"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"))

# La console Windows est en cp1252 : sans cela, les accents des libellés font tomber le script sur
# un UnicodeEncodeError au premier print (et pas à la fin, quand la campagne a déjà coûté 40 min).
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

PROJECT = Path(__file__).resolve().parent.parent
LOG = Path(os.environ["APPDATA"]) / "Godot" / "app_userdata" / "Chimera Protocol" / "power_curve.log"

HEADER = "=== Courbe de puissance"
# Indices des colonnes du CSV écrit par PowerTelemetry (cf. src/Systems/PowerTelemetry.cs).
C_T, C_PHASE, C_LEVEL, C_POWER, C_DPS, C_TAKEN = 0, 1, 2, 3, 4, 5
C_KILLS, C_ENEMIES = 6, 7
C_HP, C_HPMAX, C_REGEN, C_REGEN_EFF, C_HEAL = 11, 12, 13, 14, 15
MIN_FIELDS = 20


# ---------------------------------------------------------------------------
# Relevé d'une run
# ---------------------------------------------------------------------------

@dataclass
class Run:
    """Résumé d'une run : ce qui sert à trancher un réglage, pas le détail échantillon par échantillon."""

    seed: int | None = None
    outcome: str = "?"
    survival_s: float = 0.0
    overtime_s: float = 0.0
    final_level: int = 0
    final_power: int = 0
    final_hp_max: int = 0
    ot_dps: float = 0.0          # DPS médian en overtime
    ot_taken: float = 0.0        # dégâts subis médians en overtime (PV/s)
    ot_regen_eff: float = 0.0    # régénération réellement rendue en overtime (PV/s)
    samples: int = 0
    rows: list[list[str]] = field(default_factory=list, repr=False)

    def to_dict(self) -> dict:
        d = asdict(self)
        d.pop("rows", None)
        return d


def parse_blocks(text: str) -> list[Run]:
    """Découpe le journal en runs (un bloc par en-tête) et résume chacune."""
    runs: list[Run] = []
    current: Run | None = None

    for line in text.splitlines():
        if line.startswith(HEADER):
            current = Run()
            runs.append(current)
            continue
        if current is None:
            continue
        if line.startswith("# fin de run"):
            current.outcome = line.split(":", 1)[1].strip() if ":" in line else "?"
            continue
        if line.startswith("# seed "):
            try:
                current.seed = int(line.split()[2])
            except (IndexError, ValueError):
                pass
            continue
        if line.startswith("#") or line.startswith("t_s;") or not line.strip():
            continue

        fields = line.split(";")
        if len(fields) < MIN_FIELDS:
            continue  # relevé d'une version antérieure du format : ignoré plutôt que mal lu
        current.rows.append(fields)

    for run in runs:
        summarize(run)
    return [r for r in runs if r.samples > 0]


def summarize(run: Run) -> None:
    if not run.rows:
        return

    def num(row: list[str], idx: int) -> float:
        try:
            return float(row[idx])
        except (ValueError, IndexError):
            return 0.0

    run.samples = len(run.rows)
    last = run.rows[-1]
    run.survival_s = num(last, C_T)
    run.final_level = int(num(last, C_LEVEL))
    run.final_power = int(num(last, C_POWER))
    run.final_hp_max = int(num(last, C_HPMAX))

    ot = [r for r in run.rows if r[C_PHASE] == "OT"]
    if ot:
        # Référence = premier échantillon marqué OT, à un intervalle d'échantillonnage près de
        # l'entrée réelle. Ce décalage (≤15 s) est constant d'une run à l'autre : il ne biaise pas la
        # comparaison entre campagnes, seulement la valeur absolue.
        run.overtime_s = run.survival_s - num(ot[0], C_T)
        run.ot_dps = statistics.median(num(r, C_DPS) for r in ot)
        run.ot_taken = statistics.median(num(r, C_TAKEN) for r in ot)
        run.ot_regen_eff = statistics.median(num(r, C_REGEN_EFF) for r in ot)


# ---------------------------------------------------------------------------
# Statistiques de campagne
# ---------------------------------------------------------------------------

def quantile(values: list[float], q: float) -> float:
    """Quantile par interpolation linéaire — `statistics.quantiles` exige n>=2 et découpe autrement."""
    if not values:
        return 0.0
    s = sorted(values)
    if len(s) == 1:
        return s[0]
    pos = q * (len(s) - 1)
    lo = int(pos)
    hi = min(lo + 1, len(s) - 1)
    return s[lo] + (s[hi] - s[lo]) * (pos - lo)


@dataclass
class Stat:
    median: float
    p10: float
    p90: float
    cv: float      # coefficient de variation (écart-type / moyenne) — la « largeur du bruit »

    @staticmethod
    def of(values: list[float]) -> "Stat":
        if not values:
            return Stat(0, 0, 0, 0)
        mean = statistics.fmean(values)
        sd = statistics.stdev(values) if len(values) > 1 else 0.0
        return Stat(statistics.median(values), quantile(values, 0.1), quantile(values, 0.9),
                    sd / mean if mean else 0.0)


METRICS = [
    ("survie (s)", "survival_s"),
    ("overtime (s)", "overtime_s"),
    ("niveau final", "final_level"),
    ("puissance", "final_power"),
    ("PV max", "final_hp_max"),
    ("DPS en OT", "ot_dps"),
    ("subis/s en OT", "ot_taken"),
    ("régén rendue/s en OT", "ot_regen_eff"),
]


def print_campaign(runs: list[Run], label: str) -> dict:
    print()
    print(f"=== Campagne « {label} » — {len(runs)} run(s)")
    print()
    print(f"{'run':>4} {'seed':>8} {'survie':>9} {'overtime':>9} {'niv':>5} {'puiss':>7} "
          f"{'PVmax':>7} {'DPS(OT)':>9} {'subis/s':>8} {'issue':<14}")
    for i, r in enumerate(runs, 1):
        print(f"{i:>4} {str(r.seed):>8} {fmt_mmss(r.survival_s):>9} {fmt_mmss(r.overtime_s):>9} "
              f"{r.final_level:>5} {r.final_power:>7} {r.final_hp_max:>7} "
              f"{r.ot_dps:>9.0f} {r.ot_taken:>8.1f} {r.outcome:<14}")

    print()
    print(f"{'métrique':<24} {'médiane':>10} {'p10':>10} {'p90':>10} {'bruit':>8}   plus petit écart détectable")
    summary: dict[str, dict] = {}
    for label_m, attr in METRICS:
        values = [float(getattr(r, attr)) for r in runs]
        st = Stat.of(values)
        summary[attr] = asdict(st)
        # Une campagne ne sait pas distinguer un écart plus petit que la dispersion de sa propre
        # médiane. Approximation usuelle : 1,25 × écart-type / √n (erreur type de la médiane).
        n = max(len(values), 1)
        mdd = 1.25 * (st.cv * statistics.fmean(values) if values else 0) / (n ** 0.5)
        pct = 100 * mdd / st.median if st.median else 0
        print(f"{label_m:<24} {st.median:>10.1f} {st.p10:>10.1f} {st.p90:>10.1f} "
              f"{100*st.cv:>7.0f}%   ±{mdd:>8.1f} ({pct:.0f} %)")

    print()
    print("Lecture : « bruit » = dispersion relative entre runs. Un écart de réglage plus petit que")
    print("la dernière colonne n'est PAS mesurable par une campagne de cette taille — augmenter --runs,")
    print("ou mieux : comparer deux campagnes appariées (--out puis --compare).")
    return summary


def fmt_mmss(seconds: float) -> str:
    m, s = divmod(int(seconds), 60)
    return f"{m:d}:{s:02d}"


def compare(before_path: Path, after: list[Run], label: str) -> None:
    """Comparaison appariée par graine : l'écart de tirage s'annule, seul le réglage subsiste."""
    data = json.loads(before_path.read_text(encoding="utf-8"))
    before = {r["seed"]: r for r in data["runs"] if r.get("seed") is not None}
    pairs = [(before[r.seed], r) for r in after if r.seed in before]

    print()
    print(f"=== Comparaison appariée — « {data.get('label', before_path.stem)} » → « {label} »")
    if not pairs:
        print("Aucune graine commune : les deux campagnes n'ont pas été lancées sur les mêmes seeds.")
        print("Relancer avec --runs identique (les graines sont dérivées de --seed-base).")
        return
    print(f"{len(pairs)} run(s) appariée(s) sur {len(after)}")
    print()
    print(f"{'métrique':<24} {'avant':>10} {'après':>10} {'delta médian':>14} {'runs en hausse':>16}")
    for label_m, attr in METRICS:
        deltas = [float(getattr(b_a[1], attr)) - float(b_a[0][attr]) for b_a in pairs]
        b_med = statistics.median(float(p[0][attr]) for p in pairs)
        a_med = statistics.median(float(getattr(p[1], attr)) for p in pairs)
        up = sum(1 for d in deltas if d > 0)
        med_delta = statistics.median(deltas)
        # Test des signes : avec n runs appariées, un effet réel doit pousser la MÊME direction sur
        # la grande majorité des paires. Un 50/50 signe du bruit, quelle que soit la taille du delta.
        verdict = "" if len(pairs) < 4 else (
            "  ← net" if up >= len(pairs) - 1 or up <= 1 else
            ("  ← bruit" if abs(up - len(pairs) / 2) <= len(pairs) * 0.15 else ""))
        print(f"{label_m:<24} {b_med:>10.1f} {a_med:>10.1f} {med_delta:>+14.1f} "
              f"{up:>10}/{len(pairs):<5}{verdict}")

    print()
    print("Lecture : « runs en hausse » est le test des signes. Un effet réel pousse presque toutes")
    print("les paires dans le même sens ; un 50/50 est du bruit, même si le delta médian paraît gros.")


# ---------------------------------------------------------------------------
# Exécution des runs
# ---------------------------------------------------------------------------

def run_campaign(args) -> list[Run]:
    if not GODOT.exists():
        sys.exit(f"Godot introuvable : {GODOT}")

    offset = LOG.stat().st_size if LOG.exists() else 0
    seeds = [args.seed_base + i for i in range(args.runs)]

    for i, seed in enumerate(seeds, 1):
        cmd = [
            str(GODOT), "--headless", "--path", str(PROJECT), "res://scenes/Game.tscn", "--",
            "--auto-play", "--power-curve",
            f"--biome={args.biome}",
            f"--timescale={args.timescale}",
            f"--run-limit={args.minutes * 60}",
            f"--seed={seed}",
        ]
        if args.saturate:
            cmd.append("--saturate-arsenal")
        if args.start_at:
            cmd.append(f"--start-at={args.start_at}")

        started = time.time()
        print(f"[{i}/{len(seeds)}] seed {seed} — biome {args.biome}, "
              f"{args.minutes} min de jeu max…", end="", flush=True)
        subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
        print(f" terminé en {time.time() - started:.0f} s réelles")

    with LOG.open("r", encoding="utf-8", errors="replace") as f:
        f.seek(offset)
        fresh = f.read()

    runs = parse_blocks(fresh)
    # La graine vient du journal (`# seed N`). Repli sur l'ordre de lancement pour les relevés
    # produits par une version antérieure du jeu — au prix d'un décalage si une run n'a rien écrit.
    for run, seed in zip(runs, seeds):
        if run.seed is None:
            run.seed = seed
    missing = len(seeds) - len(runs)
    if missing > 0:
        print(f"\nATTENTION : {missing} run(s) n'ont rien écrit dans le journal.")
    return runs


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--runs", type=int, default=5, help="nombre de runs (défaut : 5)")
    p.add_argument("--biome", default="fournaise",
                   choices=["sanctuaire", "aether", "givre", "fournaise", "neon"])
    p.add_argument("--minutes", type=int, default=25, help="durée max d'une run, en minutes de jeu")
    p.add_argument("--timescale", type=float, default=3.0,
                   help="accélération (≤4 : au-delà les projectiles traversent leurs cibles)")
    p.add_argument("--seed-base", type=int, default=1000,
                   help="première graine ; les runs utilisent seed-base+0…N-1")
    p.add_argument("--saturate", action="store_true",
                   help="ajoute --saturate-arsenal (observe les cartes de surcharge dès le 1er niveau)")
    p.add_argument("--start-at", type=float, default=0.0,
                   help="démarre la run à cette minute (13 = entrée en overtime). Avec --saturate, "
                        "standardise l'état d'entrée : c'est le mode à utiliser pour trancher un "
                        "réglage d'overtime — le bot ne survit pas 13 minutes de lui-même.")
    p.add_argument("--overtime", action="store_true",
                   help="raccourci : --start-at 13 --saturate (banc d'overtime standard)")
    p.add_argument("--label", default=None, help="nom de la campagne dans les rapports")
    p.add_argument("--out", type=Path, default=None, help="écrit le résumé JSON (pour --compare)")
    p.add_argument("--compare", type=Path, default=None,
                   help="compare la campagne à un résumé JSON antérieur, par graine")
    p.add_argument("--report-only", action="store_true",
                   help="ne lance rien : ré-analyse le journal existant")
    args = p.parse_args()

    if args.overtime:
        args.start_at = args.start_at or 13.0
        args.saturate = True

    label = args.label or (
        f"{args.biome} ×{args.runs}" + (f" (dès {args.start_at:g} min)" if args.start_at else ""))

    if args.report_only:
        if not LOG.exists():
            sys.exit(f"Aucun journal : {LOG}")
        runs = parse_blocks(LOG.read_text(encoding="utf-8", errors="replace"))
        if args.runs and len(runs) > args.runs:
            runs = runs[-args.runs:]
    else:
        runs = run_campaign(args)

    if not runs:
        sys.exit("Aucune run exploitable (le banc n'a rien écrit — vérifier --power-curve).")

    summary = print_campaign(runs, label)

    if args.out:
        args.out.write_text(json.dumps({
            "label": label,
            "biome": args.biome,
            "minutes": args.minutes,
            "timescale": args.timescale,
            "summary": summary,
            "runs": [r.to_dict() for r in runs],
        }, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"\nRésumé écrit : {args.out}")

    if args.compare:
        compare(args.compare, runs, label)


if __name__ == "__main__":
    main()
