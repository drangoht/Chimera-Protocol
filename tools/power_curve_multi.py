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

Ne pas trancher sur la SURVIE du bot
------------------------------------
Enseignement de la première campagne réelle (2026-07-30, cf. `docs/TEST_REPORT.md`). Arsenal saturé,
le bot **survit bien mieux qu'un humain** : 22:42 d'overtime avant de mourir, contre 8:36 pour le
joueur sur le même réglage. Deux conséquences pratiques :

* avec `--minutes 25`, les runs se terminent sur la limite de temps (`bench_limit`) et non sur une
  mort. Leur survie est un **plancher**, pas une mesure ; le rapport la préfixe de « ≥ » et refuse
  d'annoncer un seuil de détection quand la moitié des runs sont dans ce cas ;
* laisser le bot mourir demanderait ~40 min de jeu par run, soit des heures de campagne — le headless
  ne tient pas `--timescale 3` en nuée (mesuré : ~1× effectif, 12 min réelles pour 12 min de jeu).

D'où la métrique à privilégier : **survie théorique** = PV max ÷ (dégâts subis − régénération rendue),
relevée sur chaque échantillon d'overtime. Elle n'est pas censurée, se mesure dans le budget de 12 min
et décrit la **pression produite par le réglage** plutôt que l'habileté de l'agent qui l'encaisse.

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

# Binaire du jeu. Il n'y en a plus qu'un : le moteur Godot et son banc ont été retirés du dépôt.
# Les campagnes Godot d'avant le portage restent lisibles dans docs/TEST_REPORT.md, mais elles ne se
# comparent pas aux campagnes Unity — les générateurs divergent dès le premier tirage, donc une même
# graine n'y donne pas la même run.
UNITY = PROJECT_ROOT = Path(__file__).resolve().parent.parent
UNITY = PROJECT_ROOT / "unity" / "Build" / "game" / "ChimeraProtocol.exe"

# La console Windows est en cp1252 : sans cela, les accents des libellés font tomber le script sur
# un UnicodeEncodeError au premier print (et pas à la fin, quand la campagne a déjà coûté 40 min).
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    # stderr aussi : les messages d'arrêt (`sys.exit`) y passent, et c'est justement quand la
    # campagne échoue qu'on a besoin de lire la raison — « n'a rien �crit » n'aide personne.
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

PROJECT = Path(__file__).resolve().parent.parent

# Journal écrit par le jeu, sous %USERPROFILE%\AppData\LocalLow (emplacement Unity).
LOG = (Path(os.environ["USERPROFILE"]) / "AppData" / "LocalLow"
       / "drangoht" / "Chimera Protocol" / "power_curve.log")

HEADER = "=== Courbe de puissance"
# Indices des colonnes du CSV écrit par PowerTelemetry
# (unity/Assets/Scripts/Gameplay/PowerTelemetry.cs).
C_T, C_PHASE, C_LEVEL, C_POWER, C_DPS, C_TAKEN = 0, 1, 2, 3, 4, 5
C_KILLS, C_ENEMIES = 6, 7
C_HP, C_HPMAX, C_REGEN, C_REGEN_EFF, C_HEAL = 11, 12, 13, 14, 15
MIN_FIELDS = 20


def read_run_duration_mult() -> float:
    """
    Facteur de durée de run du cran III, lu DANS `SaturationTable.cs`.

    Cette valeur était recopiée en dur ici (0,77) jusqu'au 2026-08-02, et elle a produit une campagne
    entièrement fausse le jour où le cran III est passé à 0,62 : le banc alignait la ligne de départ
    sur une entrée en overtime à la 10ᵉ minute alors que le jeu la déclenchait à la 8ᵉ. Les quatre
    runs démarraient donc DEUX MINUTES à l'intérieur de l'overtime, escalade déjà lancée, contre un
    joueur de niveau 13 — et mouraient en une dizaine de secondes. Le durcissement paraissait
    spectaculaire ; on ne mesurait que le décalage de l'outil.

    Règle générale : **un outil de banc ne recopie jamais une constante de gameplay.** Il la lit, ou
    il ment silencieusement au premier réglage — et un banc qui ment coûte plus cher que pas de banc,
    parce qu'on lui fait confiance.
    """
    src = PROJECT / "unity" / "Assets" / "Scripts" / "Shared" / "Rules" / "SaturationTable.cs"

    import re
    m = re.search(r"RunDurationMult\(int rank\)\s*=>.*?\?\s*([0-9.]+)f", src.read_text(encoding="utf-8"))
    if not m:
        sys.exit(f"Impossible de lire RunDurationMult dans {src} — corriger l'expression, pas deviner.")
    return float(m.group(1))


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
    ot_heal: float = 0.0         # soins PONCTUELS reçus en overtime (PV/s)
    ot_ttl_s: float = 0.0        # survie théorique hors soins ponctuels (s) — cf. summarize()
    ot_sustain_pct: float = 0.0  # % du temps d'overtime où les PV rendus couvrent les PV perdus
    samples: int = 0
    ot_samples: int = 0          # échantillons en phase overtime (0 = la run n'y est jamais entrée)
    rows: list[list[str]] = field(default_factory=list, repr=False)

    @property
    def truncated(self) -> bool:
        """La run n'a pas écrit son `# fin de run` : interrompue (arrêt du banc, plantage).

        Son relevé s'arrête n'importe où, donc sa survie est un artefact de l'interruption. La
        compter comme une run normale tire toute la campagne vers le bas — c'est arrivé dès la
        première campagne réelle (cf. docs/TEST_REPORT.md, 2026-07-30).
        """
        return self.outcome in ("?", "")

    @property
    def censored(self) -> bool:
        """La run s'est arrêtée sur la limite de temps, pas sur une mort.

        La survie n'est alors pas mesurée : elle est seulement **minorée** par le plafond. Une
        médiane calculée là-dessus renvoie le plafond lui-même, et la dispersion s'écrase — la
        campagne se croit précise alors qu'elle n'a rien mesuré. Remède : augmenter `--minutes`.
        """
        return self.outcome == "bench_limit"

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

    usable = [r for r in runs if r.samples > 0]
    # Une run interrompue a écrit des échantillons mais n'a pas fini de vivre : la garder revient à
    # compter une mort qui n'a pas eu lieu, à l'instant où le banc a été coupé.
    cut = [r for r in usable if r.truncated]
    if cut:
        print(f"ATTENTION : {len(cut)} run(s) interrompue(s) sans « fin de run » — écartée(s) "
              f"(banc arrêté ou plantage).")
    return [r for r in usable if not r.truncated]


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
    run.ot_samples = len(ot)
    if ot:
        # Référence = premier échantillon marqué OT, à un intervalle d'échantillonnage près de
        # l'entrée réelle. Ce décalage (≤15 s) est constant d'une run à l'autre : il ne biaise pas la
        # comparaison entre campagnes, seulement la valeur absolue.
        run.overtime_s = run.survival_s - num(ot[0], C_T)
        run.ot_dps = statistics.median(num(r, C_DPS) for r in ot)
        run.ot_taken = statistics.median(num(r, C_TAKEN) for r in ot)
        run.ot_regen_eff = statistics.median(num(r, C_REGEN_EFF) for r in ot)

        run.ot_heal = statistics.median(num(r, C_HEAL) for r in ot)

        # Survie THÉORIQUE : PV max ÷ (dégâts subis − régénération rendue), échantillon par
        # échantillon. C'est la métrique à privilégier pour trancher un réglage d'overtime, parce
        # qu'elle est la seule qui ne soit pas CENSURÉE : elle se lit sans attendre une mort que le bot
        # fait attendre bien plus longtemps qu'un humain (22:42 d'overtime relevées contre 8:36 pour le
        # joueur — cf. docs/TEST_REPORT.md, 2026-07-30). Elle décrit la pression que le réglage
        # produit, non l'habileté de l'agent qui l'encaisse.
        #
        # HORS soins ponctuels, à dessein : orbes, lifesteal et carte Blindage arrivent par pointes
        # (jusqu'à 333 PV/s relevés) dictées par les tirages, et les inclure remettrait dans la
        # métrique le bruit qu'on cherche à en sortir. La contrepartie est qu'elle MINORE la survie
        # réelle — d'où `ot_sustain_pct` juste en dessous, qui rend leur contribution visible.
        ttls = []
        sustained = 0
        for r in ot:
            taken, regen, heal = num(r, C_TAKEN), num(r, C_REGEN_EFF), num(r, C_HEAL)
            hp_max = num(r, C_HPMAX)
            net = taken - regen
            if net > 0.5 and hp_max > 0:
                ttls.append(hp_max / net)
            if regen + heal >= taken:
                sustained += 1
        run.ot_ttl_s = statistics.median(ttls) if ttls else 0.0
        # Part du temps d'overtime où le joueur regagne au moins ce qu'il perd. À 100 %, la mort ne
        # peut venir que d'un pic ponctuel, et allonger la survie ne passe plus par la défense.
        run.ot_sustain_pct = 100.0 * sustained / len(ot)


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


# Le 3ᵉ champ marque les métriques de DURÉE : ce sont les seules que la censure fausse (une run
# arrêtée par la limite de temps n'a pas fini de vivre). Les autres — PV max, DPS, dégâts subis — sont
# des états relevés en cours de run et restent valides même si la run est écourtée.
METRICS = [
    ("survie (s)", "survival_s", True),
    ("overtime (s)", "overtime_s", True),
    ("niveau final", "final_level", False),
    ("puissance", "final_power", False),
    ("PV max", "final_hp_max", False),
    ("DPS en OT", "ot_dps", False),
    ("subis/s en OT", "ot_taken", False),
    ("régén rendue/s en OT", "ot_regen_eff", False),
    ("soins ponctuels/s en OT", "ot_heal", False),
    ("survie théo. hors soins (s)", "ot_ttl_s", False),
    ("temps soutenable (%)", "ot_sustain_pct", False),
]


def print_campaign(runs: list[Run], label: str) -> dict:
    print()
    print(f"=== Campagne « {label} » — {len(runs)} run(s)")
    print()
    print(f"{'run':>4} {'seed':>8} {'survie':>9} {'overtime':>9} {'niv':>5} {'puiss':>7} "
          f"{'PVmax':>7} {'DPS(OT)':>9} {'subis/s':>8} {'issue':<14}")
    for i, r in enumerate(runs, 1):
        # « ≥ » et non « = » : sur une run censurée le chiffre est un plancher, et le lire comme une
        # mesure est exactement l'erreur que ce banc existe pour éviter.
        prefix = "≥" if r.censored else " "
        print(f"{i:>4} {str(r.seed):>8} {prefix}{fmt_mmss(r.survival_s):>8} "
              f"{prefix}{fmt_mmss(r.overtime_s):>8} "
              f"{r.final_level:>5} {r.final_power:>7} {r.final_hp_max:>7} "
              f"{r.ot_dps:>9.0f} {r.ot_taken:>8.1f} {r.outcome:<14}")

    n_censored = sum(1 for r in runs if r.censored)
    if n_censored:
        print()
        print(f"ATTENTION : {n_censored}/{len(runs)} run(s) arrêtée(s) par la limite de temps, pas par une")
        print("mort. Leur survie est un PLANCHER, pas une mesure — augmenter --minutes pour laisser le")
        print("bot mourir, sans quoi les durées ci-dessous mesurent le plafond du banc, pas le réglage.")

    # Une run qui n'a jamais atteint l'overtime relève des zéros sur toutes les colonnes « en OT ».
    # Les agréger avec les autres écrase les médianes et tire le p10 à 0 — une run de calibration du
    # bot (7:18, aucun overtime) suffisait à fausser toute la campagne.
    with_ot = [r for r in runs if r.ot_samples > 0]
    if len(with_ot) < len(runs):
        print()
        print(f"NOTE : {len(runs) - len(with_ot)} run(s) n'ont jamais atteint l'overtime — exclue(s) "
              f"des métriques « en OT » seulement.")

    print()
    print(f"{'métrique':<28}{'médiane':>10} {'p10':>10} {'p90':>10} {'bruit':>8}   plus petit écart détectable")
    summary: dict[str, dict] = {}
    for label_m, attr, is_duration in METRICS:
        scope = with_ot if attr.startswith("ot_") else runs
        if not scope:
            print(f"{label_m:<28}{'—':>10} {'—':>10} {'—':>10} {'—':>8}   aucune run en overtime")
            continue
        values = [float(getattr(r, attr)) for r in scope]
        st = Stat.of(values)
        summary[attr] = asdict(st)
        # La médiane résiste à la censure à droite tant que MOINS de la moitié des runs sont
        # censurées : le rang médian tombe alors sur une vraie mort. Au-delà, elle vaut le plafond et
        # ne veut plus rien dire — on refuse alors d'annoncer un seuil de détection qui serait faux.
        blind = is_duration and n_censored * 2 >= len(runs)
        if blind:
            print(f"{label_m:<28}{'≥' + format(st.median, '.1f'):>10} {st.p10:>10.1f} "
                  f"{'plafond':>10} {'—':>8}   non mesurable (censuré)")
            continue
        # Une campagne ne sait pas distinguer un écart plus petit que la dispersion de sa propre
        # médiane. Approximation usuelle : 1,25 × écart-type / √n (erreur type de la médiane).
        n = max(len(values), 1)
        mdd = 1.25 * (st.cv * statistics.fmean(values) if values else 0) / (n ** 0.5)
        pct = 100 * mdd / st.median if st.median else 0
        mark = " ≥" if is_duration and n_censored else "  "
        print(f"{label_m:<28}{st.median:>10.1f} {st.p10:>10.1f} {st.p90:>10.1f} "
              f"{100*st.cv:>7.0f}%   ±{mdd:>8.1f} ({pct:.0f} %){mark}")

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
    print(f"{'métrique':<28}{'avant':>10} {'après':>10} {'delta médian':>14} {'hausses/bougé':>16}")
    for label_m, attr, is_duration in METRICS:
        # Une paire dont un côté a été arrêté par la limite de temps ne dit rien sur la DURÉE : les
        # deux runs auraient peut-être continué. La garder produit un delta nul très convaincant —
        # « le réglage ne change rien » — alors que la mesure n'a simplement pas eu lieu.
        if is_duration:
            usable = [p for p in pairs
                      if not p[1].censored and p[0].get("outcome") != "bench_limit"]
        elif attr.startswith("ot_"):
            # Même raison que dans print_campaign : sans overtime, les colonnes « en OT » valent zéro
            # des deux côtés et produisent un delta nul parfaitement trompeur.
            usable = [p for p in pairs
                      if p[1].ot_samples > 0 and p[0].get("ot_samples", 1) > 0]
        else:
            usable = pairs
        if not usable:
            print(f"{label_m:<28}{'—':>10} {'—':>10} {'—':>14} {'0':>10}/{len(pairs):<5}"
                  f"  ← censuré des deux côtés")
            continue

        deltas = [float(getattr(b_a[1], attr)) - float(b_a[0][attr]) for b_a in usable]
        b_med = statistics.median(float(p[0][attr]) for p in usable)
        a_med = statistics.median(float(getattr(p[1], attr)) for p in usable)
        med_delta = statistics.median(deltas)
        # Test des signes. Les EX ÆQUO n'y portent aucune information de direction et doivent être
        # écartés du décompte, pas comptés comme « pas en hausse » : sinon une campagne rejouée à
        # l'identique (tous les deltas nuls) donne 0/n et se fait qualifier d'« effet net », le pire
        # contresens possible pour un outil censé distinguer un effet du bruit.
        moved = [d for d in deltas if d != 0]
        up = sum(1 for d in moved if d > 0)
        n_moved = len(moved)

        if n_moved == 0:
            verdict = "  ← identique"
        elif n_moved < 4:
            # Trop peu de paires ont bougé pour que le signe veuille dire quoi que ce soit.
            verdict = f"  ({n_moved} paire(s) ont bougé)"
        elif up >= n_moved - 1 or up <= 1:
            verdict = "  ← net"
        elif abs(up - n_moved / 2) <= n_moved * 0.15:
            verdict = "  ← bruit"
        else:
            verdict = ""
        if len(usable) < len(pairs):
            verdict += f"  ({len(pairs) - len(usable)} paire(s) censurée(s) écartée(s))"
        print(f"{label_m:<28}{b_med:>10.1f} {a_med:>10.1f} {med_delta:>+14.1f} "
              f"{up:>10}/{n_moved:<5}{verdict}")

    print()
    print("Lecture : « hausses/bougé » est le test des signes, ex æquo écartés (ils ne portent aucune")
    print("direction). Un effet réel pousse presque toutes les paires dans le même sens ; un 50/50 est")
    print("du bruit, même si le delta médian paraît gros.")


# ---------------------------------------------------------------------------
# Exécution des runs
# ---------------------------------------------------------------------------

def engine_command(args, seed: int) -> list[str]:
    """Ligne de commande d'une run."""
    flags = [
        "--auto-play", "--power-curve",
        f"--biome={args.biome}",
        f"--timescale={args.timescale}",
        f"--run-limit={args.minutes * 60}",
        f"--seed={seed}",
    ]

    # `-batchmode -nographics` : aucune fenêtre, aucun rendu. Le jeu quitte de lui-même à la fin
    # de la run (cf. BenchAutoPlay), donc aucune campagne n'a besoin de tuer un processus — et
    # tuer un processus au milieu d'une écriture est précisément ce qui tronque un journal.
    return [str(UNITY), "-batchmode", "-nographics"] + flags


def run_campaign(args) -> list[Run]:
    binary = UNITY
    if not binary.exists():
        sys.exit(f"Binaire introuvable : {binary}\n"
                 "Construire d'abord : Unity.exe -batchmode -quit -projectPath unity "
                 "-executeMethod BuildBench.Windows64Game")

    offset = LOG.stat().st_size if LOG.exists() else 0
    seeds = [args.seed_base + i for i in range(args.runs)]

    for i, seed in enumerate(seeds, 1):
        cmd = engine_command(args, seed)
        if args.saturate:
            cmd.append("--saturate-arsenal")
        if args.start_at:
            cmd.append(f"--start-at={args.start_at}")
        if args.saturation is not None:
            # Le cran ne se choisit qu'à l'écran de sélection de niveau, que le bot ne traverse jamais :
            # sans ce flag, aucun cran de saturation ne serait mesurable (cf. docs/ENDGAME_PLAN.md §5).
            cmd.append(f"--saturation={args.saturation}")

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
    p.add_argument("--saturation", type=int, default=None,
                   help="force le cran de saturation (0-5) sans passer par l'écran de sélection ; "
                        "critère de validation d'un cran : faire baisser le « temps soutenable » de "
                        "plus de 6 %% face à une campagne appariée au cran inférieur")
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
        # Un cran de saturation qui AVANCE l'overtime (« Compte à rebours », cran III : durée de run
        # ×0,77) déplace la ligne de départ. Sans cet alignement, `--start-at 13` fait commencer la run
        # ~3 minutes APRÈS l'entrée en overtime, escalade déjà lancée : la fenêtre observée n'est plus
        # celle des autres crans, et la mesure devient ininterprétable — relevé le 2026-07-30, bruit à
        # 36 % contre 4-9 % ailleurs, une graine morte en 26 secondes.
        if args.saturation is not None and args.saturation >= 3:
            mult = read_run_duration_mult()
            args.start_at = round(args.start_at * mult, 2)
            print(f"[saturation {args.saturation}] entrée en overtime avancée → "
                  f"--start-at={args.start_at} (×{mult:g}, lu dans SaturationTable.cs — "
                  f"sinon la run démarrerait en plein overtime)")

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
