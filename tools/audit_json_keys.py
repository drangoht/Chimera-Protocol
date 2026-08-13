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


# Clés de palier d'arme portées par des CHAMPS TYPÉS de `WeaponTable.WeaponLevelStats`, et non par
# le sac générique. Elles n'apparaissent donc pas dans un appel `Shape(...)`.
WEAPON_TYPED_KEYS = {
    "damage", "cooldown", "projectileCount", "projectileSpeed", "piercing", "spreadDegrees",
}


def weapon_level_keys() -> dict[str, set[str]]:
    """Clés déclarées par palier, **arme par arme** — c'est la granularité qui compte."""
    path = DATA / "weapons.json"
    if not path.is_file():
        return {}

    doc = json.loads(path.read_text(encoding="utf-8"))
    by_weapon: dict[str, set[str]] = {}

    for group in ("weapons", "fusions"):
        for entry in doc.get(group, []) or []:
            if not isinstance(entry, dict) or "id" not in entry:
                continue

            keys: set[str] = set()
            for level in entry.get("levels", []) or []:
                if isinstance(level, dict):
                    keys.update(k for k in level if k != "level")

            # Mêmes exclusions que le rapport général : le documentaire s'adresse au lecteur.
            keys = {
                k for k in keys
                if k not in IGNORED_EXACT and not k.lower().startswith(IGNORED_PREFIXES)
            }

            if keys:
                by_weapon[entry["id"]] = keys

    return by_weapon


def weapon_sources() -> dict[str, tuple[str, set[str]]]:
    """
    Pour chaque classe d'arme : sa classe de base, et les clés qu'elle demande à `Shape`.

    Le nom du fichier porte celui de la classe — c'est une règle du projet, tenue par le piège
    « un `MonoBehaviour` par fichier, portant son nom ».
    """
    sources: dict[str, tuple[str, set[str]]] = {}

    for path in SCRIPTS.rglob("*.cs"):
        text = path.read_text(encoding="utf-8", errors="replace")

        match = re.search(r'class\s+(\w+)\s*:\s*(\w+)', text)
        if match is None:
            continue

        shapes = set(re.findall(r'Shape(?:Int)?\(\s*"([A-Za-z_][A-Za-z0-9_]*)"', text))
        sources[match.group(1)] = (match.group(2), shapes)

    return sources


def class_of(weapon_id: str) -> str:
    """`overload_field` → `OverloadField`, la convention du registre d'armes."""
    return "".join(part.capitalize() for part in weapon_id.split("_"))


def report_weapon_shape_gaps() -> dict[str, list[str]]:
    """
    Clés de palier que **la classe de l'arme ne demande jamais** — l'arme ne grandit pas par elles.

    ⚠ Ce contrôle existe parce que le rapport général est passé à côté de cinq d'entre elles
    (`radius`, `knockbackPx`, `duration`, `slowMult`, `burnDps`, le 2026-08-13), et il est **par
    arme** parce qu'une première version globale y était encore aveugle. Deux angles morts
    successifs, tous deux dus à la même cause — une clé n'était reliée ni à son fichier ni à son
    consommateur :

      1. « la chaîne apparaît quelque part dans le code » : `"knockbackPx"` était lu par
         `GraftManager` pour une *greffe*, ce qui suffisait à faire passer pour lue la clé homonyme
         des *armes* ;
      2. « la chaîne apparaît dans un `Shape()` quelque part » : `Singularity` demande `radius`,
         ce qui suffisait à couvrir le `radius` que le Champ de Surcharge ne demandait pas.

    Le Champ de Surcharge est resté cinq paliers durant à son rayon de niveau 1 — la moitié du rayon
    promis, sur la seule arme dont le rayon *est* la mécanique — et aucune des deux versions ne
    pouvait le dire. La confrontation n'est fermée qu'arme par arme.

    L'héritage est suivi : `OverloadAegis : OverloadField` profite du `Shape` de sa base, comme à
    l'exécution.
    """
    sources = weapon_sources()
    gaps: dict[str, list[str]] = {}

    def consumed_by(cls: str) -> set[str]:
        keys: set[str] = set()
        seen: set[str] = set()

        while cls in sources and cls not in seen:
            seen.add(cls)
            base, shapes = sources[cls]
            keys |= shapes
            cls = base

        return keys

    for weapon_id, declared in weapon_level_keys().items():
        cls = class_of(weapon_id)

        # Une arme sans classe repérable relève du rapport général, pas de celui-ci : ici on ne
        # veut aucun faux positif, sous peine de rendre la sortie illisible et donc inutile.
        if cls not in sources:
            continue

        missing = sorted(declared - consumed_by(cls) - WEAPON_TYPED_KEYS)
        if missing:
            gaps[weapon_id] = missing

    return gaps


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

    shape_gaps = report_weapon_shape_gaps()

    if args.json:
        print(json.dumps({"fichiers": report, "paliers_d_arme_non_consommes": shape_gaps},
                         indent=2, ensure_ascii=False))
        return 0

    if shape_gaps:
        print("=== weapons.json — PALIERS NON CONSOMMES (par arme) ===")
        for weapon_id in sorted(shape_gaps):
            print(f"    !! {weapon_id} : {', '.join(shape_gaps[weapon_id])}")
        print("  Ces armes ne grandissent PAS par ces cles : valeur de niveau 1 jusqu'au dernier.")
        print()

    if not report:
        if not shape_gaps:
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
