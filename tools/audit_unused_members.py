#!/usr/bin/env python3
"""Repère les membres DÉCLARÉS et jamais CONSOMMÉS dans le portage Unity.

Le portage Godot → Unity a livré neuf fois le même défaut : une donnée, une règle ou un
système entier existe, est testé, a l'air fini — et personne ne l'appelle. Le code sort
alors ses valeurs par défaut, plausibles, donc rien ne dépasse. Aucun test unitaire ne
peut l'attraper : il vérifie que la règle DIT la bonne chose, jamais qu'elle est APPELÉE.

⚠ Le point qui fait tout l'intérêt de cet outil : **les commentaires sont retirés avant de
compter**. `SaturationTable.LevelUpHealsEnabled` était cité dans une douzaine de blocs de
documentation et appelé nulle part — un `grep` naïf le déclarait « utilisé ». Une règle
pure bien écrite AGGRAVE le camouflage, et sa documentation en fait partie.

Sortie : un candidat par ligne, groupé par fichier. Ce sont des CANDIDATS, pas des
verdicts — voir les faux positifs connus plus bas.

    python tools/audit_unused_members.py [--all] [--json]
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCRIPTS = ROOT / "unity" / "Assets" / "Scripts"
ASSETS = ROOT / "unity" / "Assets"

# Rappelées par le moteur, jamais par le code : les chercher n'apprendrait rien.
UNITY_MESSAGES = {
    "Awake", "Start", "Update", "LateUpdate", "FixedUpdate", "OnEnable", "OnDisable",
    "OnDestroy", "OnGUI", "OnApplicationQuit", "OnApplicationFocus", "OnApplicationPause",
    "OnDrawGizmos", "OnDrawGizmosSelected", "OnValidate", "Reset", "OnTriggerEnter2D",
    "OnTriggerExit2D", "OnTriggerStay2D", "OnCollisionEnter2D", "OnCollisionExit2D",
    "OnCollisionStay2D", "OnBecameVisible", "OnBecameInvisible", "OnPointerClick",
    "OnPointerEnter", "OnPointerExit", "OnSubmit", "OnSelect", "OnDeselect", "OnMove",
}

# Un `override` est appelé par le type de base : son absence d'appelant direct est normale.
# On les signale à part (--all) plutôt que de les mélanger aux vrais candidats.
DECL = re.compile(
    r"^[ \t]*(?:\[[^\]]*\][ \t]*)*"
    r"(?P<mods>(?:public|internal|protected)(?:\s+(?:static|virtual|override|sealed|abstract|"
    r"readonly|const|new|partial|async|extern|unsafe|volatile|event|required|init))*)\s+"
    # ⚠ La négation qui suit exclut les déclarations de TYPE : sans elle, `public static
    # class CrowdControlCaps` se lit comme une propriété nommée d'après sa propre classe,
    # et tout fichier à classe unique paraît mort.
    r"(?!(?:class|struct|interface|enum|record|delegate|namespace)\b)"
    r"(?P<type>[\w<>\[\], \.\?]+?)\s+"
    r"(?P<name>[A-Z]\w*)\s*"
    r"(?P<tail>[({=;]|=>)",
    re.MULTILINE,
)

def strip_noise(text: str) -> str:
    """Retire commentaires et littéraux : seul le CODE compte comme un appel.

    C'est la fonction la plus importante du fichier. Un membre abondamment documenté paraît
    consommé sans l'être, et c'est exactement le camouflage qui a laissé passer trois crans
    de saturation inopérants.

    ⚠ **Un balayage en un seul passage, et surtout pas trois expressions à la file.** Le
    premier essai retirait les commentaires PUIS les chaînes : sur
    ``Spawner.Load("res://scenes/entities/MiniBoss.tscn")`` le ``//`` de l'URL décapitait la
    ligne, le guillemet ouvrant restait orphelin, et il avalait tout le code jusqu'au
    guillemet suivant — des dizaines de lignes plus bas. L'outil rendait alors des membres
    « jamais appelés » dont les appels avaient simplement été mangés : un résultat net,
    cohérent et flatteur, exactement le mode de défaillance qu'il existe pour traquer.

    ⚠ **Et le contenu d'une interpolation est du CODE.** Deuxième faux positif de la même
    famille : ``$"PHASE {BossPhases.RomanNumeral(boss.Phase)}"`` est un appel bien réel, et
    le jeter avec la chaîne faisait passer ``RomanNumeral`` pour mort alors que le HUD s'en
    sert à chaque image. Les ``{…}`` sont donc conservés, le texte autour non.
    """
    out = []
    i, n = 0, len(text)

    def read_string(start: int, verbatim: bool, interpolated: bool) -> int:
        """Consomme une chaîne à partir du guillemet ouvrant ; garde les interpolations."""
        j = start + 1
        while j < n:
            ch = text[j]

            if verbatim and ch == '"' and j + 1 < n and text[j + 1] == '"':
                j += 2
                continue
            if not verbatim and ch == "\\":
                j += 2
                continue
            if ch == '"':
                return j + 1
            if not verbatim and ch == "\n":     # chaîne non terminée : ne pas déborder
                return j

            if interpolated and ch == "{":
                if j + 1 < n and text[j + 1] == "{":
                    j += 2
                    continue
                depth, k = 1, j + 1
                while k < n and depth > 0:
                    if text[k] == "{":
                        depth += 1
                    elif text[k] == "}":
                        depth -= 1
                    k += 1
                out.append(" " + text[j + 1 : max(j + 1, k - 1)] + " ")
                j = k
                continue

            j += 1
        return j

    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""

        if c == "/" and nxt == "/":
            i = text.find("\n", i)
            if i < 0:
                break
            continue

        if c == "/" and nxt == "*":
            end = text.find("*/", i + 2)
            i = n if end < 0 else end + 2
            out.append(" ")
            continue

        # Préfixes de chaîne : $ (interpolée), @ (verbatim), et leurs deux combinaisons.
        if c in "$@":
            j, interp, verb = i, False, False
            while j < n and text[j] in "$@":
                interp |= text[j] == "$"
                verb |= text[j] == "@"
                j += 1
            if j < n and text[j] == '"':
                out.append('""')
                i = read_string(j, verbatim=verb, interpolated=interp)
                continue

        if c == '"':
            out.append('""')
            i = read_string(i, verbatim=False, interpolated=False)
            continue

        if c == "'":                                # littéral caractère
            j = i + 1
            while j < n:
                if text[j] == "\\":
                    j += 2
                    continue
                if text[j] == "'" or text[j] == "\n":
                    j += 1
                    break
                j += 1
            out.append("' '")
            i = j
            continue

        out.append(c)
        i += 1

    return "".join(out)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--all", action="store_true",
                        help="inclut les override et les membres du banc (bruyant)")
    parser.add_argument("--json", action="store_true", help="sortie machine")
    args = parser.parse_args()

    if not SCRIPTS.is_dir():
        print(f"introuvable : {SCRIPTS}", file=sys.stderr)
        return 2

    sources = sorted(SCRIPTS.rglob("*.cs"))

    # Corpus de CONSOMMATION : tout le code, commentaires retirés.
    code = {p: strip_noise(p.read_text(encoding="utf-8", errors="replace")) for p in sources}
    haystack = "\n".join(code.values())

    # Les scènes et prefabs référencent des champs sérialisés par leur nom, en YAML : un
    # champ posé dans l'inspecteur n'a aucun appelant C# et n'est pourtant pas mort.
    yaml = []
    for pattern in ("*.unity", "*.prefab", "*.asset"):
        for p in ASSETS.rglob(pattern):
            try:
                yaml.append(p.read_text(encoding="utf-8", errors="replace"))
            except OSError:
                pass
    scenes = "\n".join(yaml)

    counts: dict[str, int] = {}

    def uses(name: str) -> int:
        if name not in counts:
            counts[name] = len(re.findall(rf"\b{re.escape(name)}\b", haystack))
        return counts[name]

    # ⚠ Un homonyme masque un membre mort. `BossPhases.BurstInterval` n'était appelé nulle
    # part, et le compteur nu le déclarait vivant parce qu'une constante privée du même nom
    # existe dans une arme de fusion. Pour une classe STATIQUE — la forme de toute la logique
    # pure du projet — l'appel est forcément qualifié : on compte `Type.Membre`, ce qui rend
    # la collision de noms impossible.
    TYPE_DECL = re.compile(r"\b(?:(static)\s+)?(?:sealed\s+|abstract\s+|partial\s+)*"
                           r"class\s+(\w+)")

    def enclosing_type(text: str, pos: int) -> tuple[str | None, bool]:
        last = None
        for m in TYPE_DECL.finditer(text, 0, pos):
            last = m
        if last is None:
            return None, False
        return last.group(2), last.group(1) == "static"

    findings: dict[str, list[dict]] = defaultdict(list)

    for path, text in code.items():
        # Le nom du fichier vaut souvent celui de la classe : ne pas confondre l'un avec
        # l'autre, sans quoi toute classe paraîtrait utilisée.
        for match in DECL.finditer(text):
            name = match.group("name")
            mods = match.group("mods")
            tail = match.group("tail")

            # ⚠ Écarter les déclarations de TYPE. Le faire ici et non dans l'expression :
            # celle-ci sait revenir sur ses pas et lire « static class » comme un type, si
            # bien que tout fichier à classe unique paraissait mort.
            if re.search(r"\b(class|struct|interface|enum|record|delegate)\b", match.group("type")):
                continue
            if name in UNITY_MESSAGES:
                continue
            if "override" in mods and not args.all:
                continue

            kind = ("methode" if tail == "(" else
                    "propriete" if tail in ("{", "=>") else "champ")

            owner, is_static = enclosing_type(text, match.start())

            if owner is not None and is_static:
                # Classe statique : depuis l'extérieur l'appel est forcément qualifié, depuis
                # l'intérieur il est nu. On exige donc l'absence des DEUX — sans quoi un membre
                # qu'une classe utilise pour elle-même paraîtrait mort.
                qualified = len(re.findall(
                    rf"\b{re.escape(owner)}\s*\.\s*{re.escape(name)}\b", haystack))
                internal = len(re.findall(rf"\b{re.escape(name)}\b", text))
                if qualified > 0 or internal > 1:
                    continue
            elif uses(name) > 1:
                # Sinon on retombe sur le nom nu : une déclaration compte pour une occurrence,
                # au-delà de 1 quelqu'un l'appelle.
                continue
            if re.search(rf"\b{re.escape(name)}\b", scenes):
                continue

            bench = "Bench" in path.parts or path.name.endswith("Tests.cs")
            if bench and not args.all:
                continue

            line = text[: match.start()].count("\n") + 1
            findings[str(path.relative_to(ROOT))].append(
                {"line": line, "kind": kind, "name": name, "mods": mods}
            )

    if args.json:
        print(json.dumps(findings, indent=2, ensure_ascii=False))
        return 0

    total = sum(len(v) for v in findings.values())
    print(f"=== {total} membre(s) declare(s) et jamais consomme(s) — {len(findings)} fichier(s) ===\n")

    for path in sorted(findings):
        print(path)
        for f in sorted(findings[path], key=lambda x: x["line"]):
            print(f"  {f['line']:5}  {f['kind']:10} {f['name']}")
        print()

    print("Ce sont des CANDIDATS. Restent legitimes : membres lus par reflexion, implementations")
    print("d'interface, et API laissee a dessein pour le banc. Verifier avant de conclure.")
    print()
    print("Limite connue : un membre d'instance homonyme d'un autre fichier passe pour vivant.")
    print("Le comptage QUALIFIE (Type.Membre) ne couvre que les classes statiques — c'est lui qui")
    print("a fini par attraper BossPhases.BurstInterval, masque par une constante privee du meme")
    print("nom dans une arme de fusion.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
