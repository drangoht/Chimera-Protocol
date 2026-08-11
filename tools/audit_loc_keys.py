"""Verifie que TOUT le contenu nomme du jeu a bien ses cles de traduction, dans les trois langues.

Le defaut qu'il attrape
-----------------------
Les noms et descriptions du contenu (armes, fusions, passifs, greffes, ameliorations du Hub,
ennemis) vivent dans les JSON de `StreamingAssets/data`, ecrits EN FRANCAIS. L'interface les
affiche via une cle de traduction et **replie sur le texte du JSON** quand la cle manque.

Ce repli est volontaire : il vaut mieux un nom francais qu'un blanc ou une cle brute a l'ecran.
Mais il est SILENCIEUX -- et c'est ainsi qu'un jeu publie en trois langues a montre pendant tout
le portage un Hub dont l'en-tete etait anglais et les dix-neuf lignes francaises. Rien ne le
signalait : les cles GRAFT_ et ENEMY_ existaient bien dans `ui.csv`, elles n'etaient simplement
lues par personne, et celles des armes n'avaient jamais ete ecrites.

Ce script fait donc l'inverse du repli : il declare une **absence** comme une erreur.

Il verifie deux choses, et les deux comptent :
  1. la cle existe dans `ui.csv` ;
  2. aucune de ses trois colonnes n'est vide -- une ligne `WEAPON_X_NAME,,,` passe l'analyse du
     CSV, ne fait rien planter, et affiche du vide a l'ecran.

⚠ Il ne verifie PAS que la traduction est juste, ni qu'elle n'est pas restee en francais dans la
colonne anglaise. Un copier-coller passe ce controle.

Usage :
    py tools/audit_loc_keys.py           # rapport, code de sortie 1 s'il manque quelque chose
    py tools/audit_loc_keys.py --csv     # les lignes manquantes, pretes a coller dans ui.csv
"""

from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "unity" / "Assets" / "StreamingAssets" / "data"
CSV = ROOT / "unity" / "Assets" / "StreamingAssets" / "localization" / "ui.csv"

# ---------------------------------------------------------------------------
# LES FAMILLES — prefixe de cle : (fichier, chemin dans le JSON, champs attendus)
#
# Le prefixe et les suffixes sont la CONVENTION DE NOMMAGE du jeu, et ce tableau en est la seule
# ecriture : `Platform/ContentText.cs` construit exactement les memes cles cote moteur. Les deux
# doivent bouger ensemble -- d'ou le fait de nommer ici le fichier C# qui les consomme.
#
# `TAG` n'existe que pour les ennemis : le bestiaire affiche un role court (« nuee », « elite »)
# a cote du nom.
# ---------------------------------------------------------------------------
# ⚠ Armes et fusions partagent le prefixe `WPN` : c'est celui que les cartes de montee de niveau
# lisaient deja. En creer un second, plus lisible, aurait donne deux tables pour les memes armes.
FAMILIES: dict[str, tuple[str, str, tuple[str, ...]]] = {
    "WPN":   ("weapons.json", "weapons", ("NAME", "DESC")),
    "PAS":   ("weapons.json", "passives", ("NAME", "DESC")),
    "GRAFT": ("grafts.json", "grafts", ("NAME", "DESC")),
    "META":  ("meta_upgrades.json", "upgrades", ("NAME", "DESC")),
    "ENEMY": ("enemies.json", "enemies", ("NAME", "TAG", "DESC")),
}

# Les greffes de fusion vivent dans un second tableau du meme fichier, sous le MEME prefixe que les
# greffes simples : le Codex Chimere les affiche dans la meme liste, et leur donner un prefixe a
# part aurait fait deux conventions pour un seul ecran.
EXTRA = [
    ("WPN", "weapons.json", "fusions", ("NAME", "DESC")),
    ("GRAFT", "grafts.json", "fusions", ("NAME", "DESC")),
    ("ENEMY", "enemies_biome_expansion.json", "enemies", ("NAME", "TAG", "DESC")),
]


def load_ids(filename: str, path: str) -> list[tuple[str, str]]:
    """(id, nom francais du JSON) de chaque entree — le nom sert de colonne FR toute faite."""
    doc = json.loads((DATA / filename).read_text(encoding="utf-8"))
    entries = doc.get(path, [])
    return [(e["id"], e.get("name", "")) for e in entries if isinstance(e, dict) and "id" in e]


def load_csv() -> dict[str, list[str]]:
    with CSV.open(encoding="utf-8", newline="") as f:
        rows = list(csv.reader(f))
    return {r[0]: r[1:] for r in rows if r and r[0] and not r[0].startswith("#")}


def main() -> int:
    table = load_csv()
    emit_csv = "--csv" in sys.argv[1:]

    missing: list[tuple[str, str]] = []   # (cle, texte francais si on le connait)
    empty: list[str] = []
    expected: set[str] = set()
    total = 0

    families = [(p, f, path, fields) for p, (f, path, fields) in FAMILIES.items()]
    families += [(p, f, path, fields) for p, f, path, fields in EXTRA]

    for prefix, filename, path, fields in families:
        for entry_id, french in load_ids(filename, path):
            for field in fields:
                key = f"{prefix}_{entry_id.upper()}_{field}"
                expected.add(key)
                total += 1

                if key not in table:
                    missing.append((key, french if field == "NAME" else ""))
                    continue

                columns = table[key]
                # Trois langues attendues : EN, FR, ES. Une colonne vide affiche du vide.
                if len(columns) < 3 or any(not c.strip() for c in columns[:3]):
                    empty.append(key)

    # ⚠ Le controle en SENS INVERSE : une cle qui porte un prefixe de la convention sans correspondre
    # a aucun contenu. C'est la trace d'un contenu retire — ou d'une SECONDE convention creee a cote
    # de celle qui existait deja. Ce dernier cas est arrive : les armes avaient leurs vingt-et-une
    # entrees sous `WPN_`, lues par les cartes de montee de niveau, et un `WEAPON_` complet a ete
    # ajoute par-dessus sans que rien ne s'en plaigne. Deux tables pour les memes douze armes, dont
    # une seule serait mise a jour le jour ou un texte change.
    prefixes = {p for p, *_ in families}
    orphans = sorted(
        key for key in table
        if key.split("_")[0] in prefixes and key not in expected
    )

    if emit_csv:
        for key, french in missing:
            print(f"{key},,{french},")
        return 0

    print(f"{total} cles de contenu attendues, {len(table)} lignes dans ui.csv")

    if missing:
        print(f"\n!! {len(missing)} cles ABSENTES — l'ecran affichera le texte francais du JSON :")
        for key, _ in missing:
            print(f"   {key}")

    if empty:
        print(f"\n!! {len(empty)} cles a colonne VIDE — l'ecran affichera du vide :")
        for key in empty:
            print(f"   {key}")

    if orphans:
        print(f"\n!! {len(orphans)} cles ORPHELINES — aucun contenu ne porte cet identifiant "
              f"(contenu retire, ou seconde convention creee a cote de la premiere) :")
        for key in orphans:
            print(f"   {key}")

    if not missing and not empty and not orphans:
        print("\nOK — tout le contenu nomme est traduit dans les trois langues, "
              "et aucune cle ne traine.")

    return 1 if missing or empty or orphans else 0


if __name__ == "__main__":
    sys.exit(main())
