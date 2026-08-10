#!/usr/bin/env python3
"""Confronte les clés DÉCLARÉES dans les fichiers de données aux clés LUES par le code Unity.

Le portage a livré quatre fois le même défaut par une clé mal lue, et chacun était muet :

  • `biomes` (tableau) déclaré, `biome` (chaîne) lu  → le filtre de biome ne s'appliquait à
    AUCUN des 26 ennemis tagués, et les cinq champions apparaissaient dans les cinq biomes ;
  • `damagePerProjectile` déclaré, jamais lu        → huit tireurs à 5 dégâts au lieu de 11-18 ;
  • `fireIntervalSec` / `rangePx` lus, inexistants  → la Ruche de Tourelles sur ses valeurs par défaut ;
  • `projectileCount` déclaré, jamais lu            → armes bloquées à deux projectiles au niveau 20.

⚠ **Aucun de ces défauts ne lève quoi que ce soit.** Un lecteur tolérant rend sa valeur par
défaut, et une valeur par défaut est toujours plausible : une arme faible a l'air mal
équilibrée, pas cassée. C'est ce qui les rend indétectables autrement qu'en jouant — ou par
une confrontation mécanique comme celle-ci.

Deux colonnes à lire :

  ORPHELINES  déclarées dans les données, lues nulle part → une intention sans effet.
  FANTÔMES    lues par le code, absentes des données     → le code lit du vide, toujours.

    python tools/audit_json_keys.py [--json]
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "unity" / "Assets" / "StreamingAssets" / "data"
SCRIPTS = ROOT / "unity" / "Assets" / "Scripts"

# Clés purement documentaires : elles existent pour le lecteur humain, pas pour le moteur.
IGNORED_PREFIXES = ("_", "comment", "note")

# Clés de structure, jamais lues comme un champ scalaire.
IGNORED_EXACT = {"id", "name", "description"}


def declared_keys(node, out: set[str]) -> None:
    """Toutes les clés d'un document, à toute profondeur."""
    if isinstance(node, dict):
        for key, value in node.items():
            out.add(key)
            declared_keys(value, out)
    elif isinstance(node, list):
        for item in node:
            declared_keys(item, out)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true", help="sortie machine")
    args = parser.parse_args()

    if not DATA.is_dir():
        print(f"introuvable : {DATA}", file=sys.stderr)
        return 2

    # Toutes les chaînes littérales du code : c'est ainsi qu'une clé JSON est nommée.
    def literals(root: Path) -> set[str]:
        found: set[str] = set()
        for path in root.rglob("*.cs"):
            text = path.read_text(encoding="utf-8", errors="replace")
            found.update(re.findall(r'"([A-Za-z_][A-Za-z0-9_]*)"', text))
        return found

    read = literals(SCRIPTS)

    # ⚠ Ce rapport distinguait autrefois deux causes, en comparant au code Godot resté dans le
    # dépôt : une clé lue par Godot et pas par Unity était un TROU DE PORTAGE, une clé orpheline
    # des deux côtés une intention de design jamais câblée. Godot supprimé, ce témoin n'existe
    # plus — toute clé orpheline est désormais à instruire au cas par cas, et l'outil ne peut plus
    # dire laquelle des deux causes s'applique.
    godot_read: set[str] = set()

    report: dict[str, dict[str, list[str]]] = {}

    for path in sorted(DATA.glob("*.json")):
        try:
            doc = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            print(f"{path.name} : illisible ({e})", file=sys.stderr)
            continue

        keys: set[str] = set()
        declared_keys(doc, keys)

        keys = {
            k for k in keys
            if k not in IGNORED_EXACT and not k.lower().startswith(IGNORED_PREFIXES)
        }

        orphans = sorted(k for k in keys if k not in read)
        gaps = sorted(k for k in orphans if k in godot_read)
        orphans = [k for k in orphans if k not in godot_read]

        # ⚠ Les FANTÔMES ne se cherchent que parmi les clés qui RESSEMBLENT à celles du
        # fichier : le code est plein de chaînes qui ne sont pas des clés de données. Le
        # critère retenu — une chaîne lue, absente du fichier, mais dont un proche parent y
        # figure — attrape `biome` face à `biomes` sans noyer le rapport.
        ghosts = []
        for candidate in read:
            if candidate in keys or len(candidate) < 5:
                continue
            for real in keys:
                if candidate == real:
                    continue
                # Singulier/pluriel UNIQUEMENT, et à casse identique. La comparaison
                # insensible à la casse noyait le rapport : le code est plein de chaînes
                # comme "Damage" ou "Level" qui sont des identifiants C#, pas des clés.
                if candidate + "s" == real or candidate == real + "s":
                    ghosts.append(f"{candidate} (le fichier dit « {real} »)")
                    break

        if orphans or ghosts or gaps:
            report[path.name] = {
                "trous_de_portage": gaps,
                "orphelines": orphans,
                "fantomes": sorted(set(ghosts)),
            }

    if args.json:
        print(json.dumps(report, indent=2, ensure_ascii=False))
        return 0

    if not report:
        print("Aucune cle orpheline ni fantome.")
        return 0

    for name in sorted(report):
        entry = report[name]
        print(f"=== {name} ===")

        if entry["trous_de_portage"]:
            print("  TROUS DE PORTAGE — lues par GODOT, pas par Unity :")
            for k in entry["trous_de_portage"]:
                print(f"    !! {k}")

        if entry["fantomes"]:
            print("  FANTOMES — le code lit une cle que le fichier ne declare pas :")
            for g in entry["fantomes"]:
                print(f"    !! {g}")

        if entry["orphelines"]:
            print(f"  ORPHELINES — declarees, lues nulle part ({len(entry['orphelines'])}) :")
            for k in entry["orphelines"]:
                print(f"      {k}")
        print()

    print("Une orpheline peut etre legitime (donnee lue par un OUTIL, pas par le jeu).")
    print("Un fantome ne l'est jamais : le code lit du vide et prend sa valeur par defaut.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
