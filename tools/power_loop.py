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

# Journal Unity par défaut ; celui de Godot reste lisible en le passant à `--log`. Les deux
# coexistent sur disque, et pointer le mauvais ne lève rien — il rend les chiffres de l'autre
# campagne, tout aussi plausibles.
LOG_UNITY = Path.home() / "AppData/LocalLow/drangoht/Chimera Protocol/power_curve.log"
LOG_GODOT = Path.home() / "AppData/Roaming/Godot/app_userdata/Chimera Protocol/power_curve.log"

DEFAULT_LOG = LOG_UNITY if LOG_UNITY.exists() else LOG_GODOT

# Noms de colonnes utilisés, tels qu'écrits par PowerTelemetry.ComposeHeader.
# ⚠ Indexer par NOM et non par position : le CSV a gagné des colonnes au fil des versions
# (`regen_eff_ps` et `soins_ps` en 1.24), si bien qu'une position fixe lit les greffes d'une run
# ancienne comme un nombre. Chaque run porte sa propre ligne de titres.
NEEDED = ("t_s", "phase", "niveau", "degats_subis_ps", "kills_fenetre", "pv_max", "soins_ps")

# Colonnes apparues en cours de route : les runs plus anciennes restent lisibles, la colonne est
# simplement absente de leur rapport.
#   `soins_bruts_ps`                        — 2026-08-01 (PV OFFERTS, gaspillage inclus)
#   `pv_min_pct`, `frolements`, `part_danger` — 2026-08-02 (pression ressentie, cf. PressureMeter)
OPTIONAL = ("soins_bruts_ps", "pv_min_pct", "frolements", "part_danger")


class Run:
    """Une run du journal : son en-tête, sa table de colonnes et ses échantillons d'overtime."""

    def __init__(self, header: str, columns: list[str]):
        self.sat = self._grab(r"saturation (\d+)", header)
        self.seed = self._grab(r"# seed (\d+)", header)
        m = re.search(r"biome (\w+)", header)
        self.biome = m.group(1) if m else "?"
        self.col = {name: i for i, name in enumerate(columns)}
        self.samples: list[list[str]] = []
        # Issue de la run (« death », « bench_limit », « victory »…). C'est la mesure la moins
        # discutable du lot : elle ne dépend d'aucune convention de résumé.
        self.outcome: str | None = None

    @property
    def died(self) -> bool:
        return self.outcome == "death"

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
        if line.startswith("# fin de run"):
            # Doit être testé AVANT le cas « # » général : cette ligne clôt la run courante et ne
            # fait pas partie de l'en-tête de la suivante. Absorbée dans le header, l'issue était
            # perdue — et avec elle la seule mesure qui ne dépend d'aucune convention de résumé.
            if cur is not None:
                cur.outcome = line.split(":", 1)[-1].strip()
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


# Les grandeurs comparées, et comment les résumer pour UNE run.
METRICS = {
    "niveaux/min":  lambda r: r.per_minute("niveau"),
    "PV max/min":   lambda r: r.per_minute("pv_max"),
    # « rendus » mesure une CONVERSION (borné par les PV manquants : à PV pleins, zéro) ; « offerts »
    # mesure la GÉNÉROSITÉ du jeu. Confondre les deux fait lire « ce cran soigne plus » là où le joueur
    # a seulement plus de PV à remplir — le faux diagnostic du cran V, le 2026-08-01.
    "soins rendus": lambda r: median(r.metric("soins_ps")),
    "soins offerts": lambda r: (median(r.metric("soins_bruts_ps"))
                                if "soins_bruts_ps" in r.col else None),
    "kills/min":    lambda r: median(r.metric("kills_fenetre")) * 4,
    "subis PV/s":   lambda r: median(r.metric("degats_subis_ps")),
    # ── Pression RESSENTIE (2026-08-02) ─────────────────────────────────────────────────────────
    # Les cinq lignes ci-dessus sont des débits moyennés : elles répondent à « le joueur s'use-t-il ? ».
    # Il ne s'use pas — il jette 80 % des soins offerts et meurt d'un pic. Les suivantes comptent des
    # ÉVÉNEMENTS (cf. PressureMeter) et sont les seules à pouvoir contredire un « je n'ai eu aucun
    # mal », parce qu'un plongeon à 10 % suivi d'une remontée ne déplace aucune moyenne.
    #
    # ⚠ UN ÉVÉNEMENT RARE NE SE RÉSUME PAS PAR UNE MÉDIANE. Sur ~27 fenêtres dont la plupart valent
    # zéro, `median(frolements)` vaut 0 même dans une run où le bot MEURT — la médiane dirait alors
    # « aucun danger » d'une run mortelle. Les comptes se SOMMENT et se ramènent au temps ; seul
    # `pv_min_pct`, qui est déjà un extremum par fenêtre, se résume par une médiane (et par son
    # minimum, pour le pire moment de la run).
    "frôlements/min": lambda r: rate_per_minute(r, "frolements"),
    "PV bas %":       lambda r: (median(r.metric("pv_min_pct"))
                                 if "pv_min_pct" in r.col else None),
    "PV bas min %":   lambda r: (min(r.metric("pv_min_pct"))
                                 if "pv_min_pct" in r.col else None),
    "part danger":    lambda r: (sum(r.metric("part_danger")) / len(r.samples)
                                 if "part_danger" in r.col and r.samples else None),
}


def rate_per_minute(run: Run, key: str) -> float | None:
    """Total d'un COMPTE par échantillon, ramené à la minute d'overtime.

    Volontairement une somme et non une médiane : un frôlement est un événement rare, et la médiane
    d'une colonne majoritairement nulle est nulle — y compris dans une run où le joueur meurt.
    """
    if key not in run.col or len(run.samples) < 2:
        return None
    times = run.metric("t_s")
    span = (times[-1] - times[0]) / 60.0
    if span <= 0:
        return None
    # Le premier échantillon couvre la fenêtre AVANT times[0] : l'exclure aligne le total sur la
    # durée réellement mesurée par `span`.
    return sum(run.metric(key)[1:]) / span


def paired_report(by_sat: dict[str, list[Run]], a: str, b: str) -> int:
    """Compare deux crans GRAINE PAR GRAINE — la seule lecture qui annule le bruit de tirage.

    Le delta médian entre deux groupes mélange l'effet du réglage et la chance des tirages ; le test
    des signes ne demande que « la métrique va-t-elle dans le même sens sur chaque paire ? ».
    C'est la lecture retenue par le projet depuis la campagne du 2026-07-30.
    """
    def latest_by_seed(runs: list[Run]) -> dict[int, Run]:
        # Une graine relancée apparaît deux fois (ex. une run interrompue puis refaite) : le journal
        # étant chronologique, la dernière écrite est la bonne.
        out: dict[int, Run] = {}
        for r in runs:
            if r.seed is not None:
                out[r.seed] = r
        return out

    ra, rb = latest_by_seed(by_sat.get(a, [])), latest_by_seed(by_sat.get(b, []))
    seeds = sorted(set(ra) & set(rb))
    if not seeds:
        print(f"Aucune graine commune aux crans {a} et {b}.", file=sys.stderr)
        return 1

    print(f"Comparaison appariée — cran {a} → cran {b} · {len(seeds)} graine(s) : "
          f"{', '.join(map(str, seeds))}\n")

    # LE résultat, avant toute métrique de résumé : le bot est-il mort ? Affiché en premier parce
    # qu'aucune convention de lecture ne peut le déformer — contrairement aux médianes, aux taux et
    # au choix de la fenêtre. ⚠ Il porte sur les runs RETENUES : voir l'avertissement de `main`, une
    # run trop courte pour le seuil d'échantillons est justement une run où le joueur est mort vite.
    ma_morts = sum(1 for s in seeds if ra[s].died)
    mb_morts = sum(1 for s in seeds if rb[s].died)
    print(f"{'runs mortelles':<16} {ma_morts:>9}/{len(seeds)} {mb_morts:>7}/{len(seeds)}\n")
    print(f"{'métrique':<16} {'cran '+a:>9} {'cran '+b:>9} {'écart':>8} {'signes':>8}")
    print("-" * 54)

    for name, fn in METRICS.items():
        # Filtrage PAIRE PAR PAIRE, et non métrique par métrique : une colonne récente
        # (`soins_bruts_ps`, puis la pression) n'existe que dans les runs postérieures à son ajout.
        # Écarter la métrique entière dès qu'UNE paire ancienne y manque la rendrait illisible tant
        # que le journal contient une seule vieille campagne — c'est-à-dire au moment précis où on
        # veut s'en servir. Chaque ligne affiche donc le nombre de paires qui la portent.
        pairs = [(fn(ra[s]), fn(rb[s])) for s in seeds]
        pairs = [(x, y) for x, y in pairs if x is not None and y is not None]
        if not pairs:
            continue
        va = [x for x, _ in pairs]
        vb = [y for _, y in pairs]
        ma, mb = median(va), median(vb)
        up = sum(1 for x, y in zip(va, vb) if y > x)
        # Un effet n'est retenu que s'il va dans le MÊME sens sur toutes les paires.
        net = "net" if up in (0, len(pairs)) else ""
        # Une référence NULLE est le cas normal des métriques d'événement — au cran 0 le joueur ne
        # frôle jamais la mort. Un pourcentage y vaudrait « nan » ou l'infini : afficher « — » et
        # laisser lire les deux valeurs, qui disent tout (0,0 → 0,2 est un passage de rien à quelque
        # chose, pas une variation).
        ecart = f"{(mb - ma) / ma * 100:+7.1f}%" if ma else f"{'—':>8}"
        print(f"{name:<16} {ma:>9.1f} {mb:>9.1f} {ecart} "
              f"{up:>4}/{len(pairs)} {net}")

    print("\n« signes » = nombre de graines où la métrique MONTE du premier cran au second.")
    print("0/N ou N/N = effet net ; toute valeur intermédiaire = indécidable à cette taille.")
    print("\nUn cran QUI SE SENT monte « frôlements/min » et « part danger », et fait BAISSER")
    print("« PV bas % » — le joueur doit voir sa barre descendre, pas seulement encaisser plus.")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--log", type=Path, default=DEFAULT_LOG)
    ap.add_argument("--min-samples", type=int, default=5,
                    help="nombre minimal d'échantillons d'overtime pour retenir une run "
                         "(monter à ~30 pour écarter les runs interrompues en cours)")
    ap.add_argument("--paired", nargs=2, metavar=("CRAN_A", "CRAN_B"),
                    help="comparaison APPARIÉE graine par graine entre deux crans + test des signes")
    args = ap.parse_args()

    if not args.log.exists():
        print(f"Journal introuvable : {args.log}", file=sys.stderr)
        return 1

    toutes = parse(args.log)
    runs = [r for r in toutes if len(r.samples) >= args.min_samples]
    if not runs:
        print("Aucune run exploitable.", file=sys.stderr)
        return 1

    # ⚠ BIAIS DE SURVIE DANS LA LECTURE. `--min-samples` existe pour écarter les runs interrompues
    # par un banc coupé — mais une run PEUT être courte parce que le joueur est mort vite, et c'est
    # alors le meilleur résultat du réglage testé. Écartées en silence, les runs les plus mortelles
    # disparaissent de la comparaison et le réglage paraît plus doux qu'il n'est. Cas réel
    # (2026-08-02) : au cran VI le bot meurt en 1 min d'overtime sur une graine — run exclue par
    # `--min-samples 20`, donc invisible dans le verdict apparié du cran qui l'avait tuée.
    # Les runs sans cran ni graine identifiés viennent de campagnes antérieures à ces en-têtes : elles
    # ne sont comparables à rien, les signaler n'apporterait que du bruit.
    ecartees = [r for r in toutes
                if len(r.samples) < args.min_samples and r.died
                and r.sat is not None and r.seed is not None]
    if ecartees:
        detail = ", ".join(f"cran {r.sat}/graine {r.seed} ({len(r.samples)} éch.)" for r in ecartees)
        print(f"⚠ {len(ecartees)} run(s) MORTELLE(S) écartée(s) par --min-samples={args.min_samples} : "
              f"{detail}.\n  Une run courte parce que le joueur MEURT est un résultat, pas un déchet — "
              f"baisser le seuil pour les inclure.\n", file=sys.stderr)

    by_sat: dict[str, list[Run]] = {}
    for r in runs:
        by_sat.setdefault("?" if r.sat is None else str(r.sat), []).append(r)

    if args.paired:
        return paired_report(by_sat, *args.paired)

    print(f"{len(runs)} runs · journal {args.log.name}\n")
    print(f"{'cran':>4} {'runs':>4} {'morts':>6} {'niv/min':>9} {'PVmax/min':>10} {'soins PV/s':>11} "
          f"{'kills/min':>10} {'subis PV/s':>11} {'frôl./min':>10} {'PV bas %':>9}")
    print("-" * 92)

    for sat in sorted(by_sat, key=lambda k: (k == "?", k)):
        group = by_sat[sat]

        def med(fn):
            vals = [v for v in (fn(r) for r in group) if v is not None]
            return median(vals) if vals else float("nan")

        # kills_fenetre et frolements sont des comptes PAR ÉCHANTILLON (15 s de jeu) : ramenés à la minute.
        morts = sum(1 for r in group if r.died)
        print(f"{sat:>4} {len(group):>4} {morts:>3}/{len(group):<2} "
              f"{med(lambda r: r.per_minute('niveau')):>9.1f} "
              f"{med(lambda r: r.per_minute('pv_max')):>10.0f} "
              f"{med(lambda r: median(r.metric('soins_ps'))):>11.1f} "
              f"{med(lambda r: median(r.metric('kills_fenetre')) * 4):>10.0f} "
              f"{med(lambda r: median(r.metric('degats_subis_ps'))):>11.1f} "
              f"{med(METRICS['frôlements/min']):>10.1f} "
              f"{med(METRICS['PV bas %']):>9.0f}")

    print("\nLecture : un cran qui MONTE la colonne « niv/min » nourrit la boucle de puissance —")
    print("il ajoute de la menace et, du même geste, de quoi l'absorber.")
    print("Et un cran qui laisse « frôl./min » à zéro ne se sentira pas, quoi que disent les débits :")
    print("le joueur n'a jamais vu sa barre de vie descendre.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
