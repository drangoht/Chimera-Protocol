"""Cherche dans le code les textes ECRITS EN DUR qui finissent a l'ecran du joueur.

Le defaut qu'il attrape
-----------------------
`tools/audit_loc_keys.py` verifie que le CONTENU (armes, greffes, ennemis) a ses traductions. Il ne
voit rien de ce qui est ecrit directement dans un fichier `.cs` : « Jouer », « Quitter »,
« FIN DE RUN », « MONTEE DE NIVEAU »... Ces chaines-la n'ont pas de cle, donc pas d'absence a
signaler — elles sont simplement francaises, pour tout le monde, dans les trois langues.

Elles ont ete cherchees a la main une premiere fois, avec un grep sur `UiStyle.Label(` et
`UiStyle.TextButton(`. Trois entrees du menu principal y ont echappe : elles passent par une
fabrique locale (`AddEntry`), pas par la fabrique commune. **C'est le joueur qui les a trouvees.**
D'ou ce script : il ne cherche pas des appels, il cherche des CHAINES, et laisse la charge de la
preuve a l'inverse — toute chaine qui ressemble a du langage humain est suspecte tant qu'elle n'est
pas justifiee.

Comment il decide
-----------------
Est SUSPECTE toute chaine litterale qui contient au moins une lettre et qui n'est pas :
  - une cle de traduction (elle existe dans `ui.csv`, ou elle en a la forme MAJUSCULE_AVEC_UNDERSCORE) ;
  - un identifiant technique (`snake_case`, `kebab-case`, chemin, extension, drapeau `--xxx`) ;
  - un usage clairement non affichable (log, tooltip d'editeur, nom de GameObject, nom d'animation,
    chaine de format numerique, comparaison de chaine).

⚠ Il rend des FAUX POSITIFS, et c'est voulu : une liste courte qu'on relit vaut mieux qu'un filtre
malin qui laisse passer « Jouer ». Les acquittements se declarent dans `ALLOWED` avec leur raison.

Usage :
    py tools/audit_hardcoded_text.py           # rapport, code de sortie 1 s'il reste du texte en dur
    py tools/audit_hardcoded_text.py --all     # sans le filtre des acquittements (pour relire)
"""

from __future__ import annotations

import csv
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent
SCRIPTS = ROOT / "unity" / "Assets" / "Scripts"
CSV = ROOT / "unity" / "Assets" / "StreamingAssets" / "localization" / "ui.csv"

# Le banc, l'editeur et la telemetrie ne s'adressent pas au joueur : leurs textes sont des rapports
# d'outil, ecrits dans un journal que personne ne lit dans une autre langue que le francais.
SKIP_DIRS = {"Bench", "Editor"}
SKIP_FILES = {"BossTelemetry.cs", "PowerTelemetry.cs", "SceneDiagnostic.cs"}

# Contextes ou une chaine ne peut pas etre affichee au joueur.
NON_DISPLAY = re.compile(
    r"Debug\.(Log|LogWarning|LogError|Assert)"
    r"|\[(Tooltip|Header|Serialize|CreateAssetMenu|MenuItem)"
    r"|new GameObject\(|NewUiObject\(|\.name\s*=|AddComponent<"
    r"|Resources\.Load|Path\.Combine|File\.|Directory\.|LoadAll"
    r"|StartsWith\(|EndsWith\(|Contains\(|Equals\(|Split\(|Replace\(|IndexOf\("
    r"|PlayerPrefs\.|PlaySfx\(|PlayMusic\(|Play\(\"|Animation\(|FindAnimation"
    r"|GetComponent|CompareTag|SetTrigger|SetBool|SetFloat|SetColor\(|SetVector\(|SetFloat\("
    r"|TryGetProperty|GetProperty|nameof\(|typeof\(|#pragma|using "
    # Messages d'exception : ils s'adressent au developpeur, jamais au joueur.
    r"|throw new|Exception\(|ArgumentException|InvalidOperation"
    # Assemblage de rapport : journaux de banc, fichiers de mesure, presence Discord.
    r"|_events\.Add\(|sb\.Append|\.AppendLine\(|LogTo|WriteLine|Details\s*=|State\s*="
    # Fabriques qui prennent un NOM D'OBJET de scene en 2e argument, jamais un libelle.
    r"|UiStyle\.Panel\(|UiStyle\.Scrim\(|UiStyle\.Separator\(|MakeSprite\(|BuildBar\(|AddDust\("
)

# Fragments produits par le decoupage d'une chaine interpolee : `$"{Loc.T("A")} : {Loc.T("B")}"` se
# lit comme cinq litteraux, dont trois sont du CODE. Les reconnaitre evite d'inonder le rapport.
INTERPOLATION_DEBRIS = re.compile(
    r"Loc\.T|string\.Join|ToUpperInvariant|ToString|\.Count|\.Instance"
    r"|\($|^\)"                          # se termine par une parenthese ouvrante, ou commence par sa fermante
    r"|^\{[^}]*$|^[^{]*\}$"              # accolade non appariee : la chaine est coupee en plein milieu
    r"|\)\}"                             # fin d'un appel a l'interieur d'une interpolation
    r"|\.[A-Za-z]+\("                    # appel de methode : c'est du code, pas du texte
)

# Noms de ressources construits a l'execution : `$"ui_frame_button_{Slug(accent)}"`,
# `$"Audio/music/music_run_{biomeId}_calm"`. Ils ont la forme d'un identifiant, pas d'une phrase.
RESOURCE_NAME = re.compile(
    r"^[a-z][a-z0-9_]*(\{[^}]*\}[a-z0-9_]*)*$"     # snake_case avec interpolations
    r"|^[A-Za-z][A-Za-z0-9_]*/"                    # chemin de ressource ou de scene
)

# Formes qui ne sont pas du langage humain.
#
# ⚠ Le cas limite qui commande tout le reste : « Jouer » et « BordHaut » sont tous deux un mot
# capitalise sans espace. Le premier est un libelle affiche, le second un nom d'objet de scene. Ce
# qui les separe est la MAJUSCULE INTERNE — d'ou une exclusion du PascalCase multi-mots seulement.
# Un mot simple capitalise reste suspect, c'est ce qui rattrape « Jouer », « Hub » et « Quitter ».
TECHNICAL = re.compile(
    r"^[a-z][a-zA-Z0-9]*$"               # camelCase : cle JSON, parametre
    r"|^[a-z0-9_]+$"                     # snake_case : identifiant de donnee
    r"|^[a-z0-9-]+$"                     # kebab-case
    r"|^[A-Z0-9_]+$"                     # MAJUSCULE_AVEC_UNDERSCORE : forme d'une cle
    r"|^_[A-Za-z0-9_]+$"                 # _PropriétéDeShader
    r"|^[A-Z][a-z0-9]*[A-Z][A-Za-z0-9]*$"  # PascalCase MULTI-MOTS : nom d'objet, pas un libelle
    r"|^[A-Za-z0-9_]+(/[A-Za-z0-9_.-]+)+$"  # chemin de ressource (Environment/tile_xxx)
    r"|^--[a-z-]+=?$"                    # drapeau de ligne de commande
    r"|^[.,;:/\\|<>=+*#@%&()\[\]{}\s-]*$"  # ponctuation seule
    r"|^\{\d+\}$"                        # emplacement de format
    r"|^(res|http|https|file)://"        # chemin ou URL
    r"|^[A-Za-z0-9_./\\-]+\.(png|ogg|wav|json|csv|ttf|asset|cs|mp4|txt|log|tres|mat|shader)$"
    r"|^[0#.,:%+\- ]+$"                  # motif de format numerique (\"0.0\", \"00:00\")
    r"|^F\d$|^N\d$|^P\d$|^D\d$"          # specificateurs de format .NET
)

# Acquittements : chaine -> raison. Une entree ici est une DECISION, pas un oubli.
ALLOWED: dict[str, str] = {
    "Chimera Protocol": "nom propre du jeu — identique dans les trois langues",
    "CHIMERA PROTOCOL": "nom propre du jeu — identique dans les trois langues",
    "Aether": "nom propre de l'univers — jamais traduit",
    "itch.io": "nom propre",
    "n/a": "marqueur technique visible seulement dans un rapport",
    "In a run": "presence Discord — une seule langue assumee, audience internationale",
    "In the menus": "presence Discord — une seule langue assumee, audience internationale",
    "{biomeName} — saturation {saturation}": "presence Discord — meme raison",
    "{stats.Speed:F0} px/s": "unite de mesure, identique dans les trois langues",
    "VfxAdditive (runtime)": "nom d'un materiau cree a l'execution, jamais affiche",
}

# Noms d'objets de scene. Ils ont exactement la forme d'un libelle — un mot capitalise, sans espace —
# et c'est le prix a payer pour que « Jouer » et « Quitter » ressortent. Les acquitter un par un est
# volontaire : la liste est stable, et un nom NOUVEAU qui apparait ici demande qu'on tranche.
ALLOWED.update({
    name: "nom d'objet de scene, jamais affiche"
    for name in (
        "Band", "Banner", "Biome", "Boss", "Calm", "Combat", "Core", "Cores", "Dash", "Fps",
        "Game", "Halo", "Health", "Inner", "Intro", "Kills", "Level", "Panel", "Regen", "Row_",
        "Slot", "Sweep", "Timer", "Vitals",
    )
})


def load_keys() -> set[str]:
    with CSV.open(encoding="utf-8", newline="") as f:
        return {r[0] for r in csv.reader(f) if r}


def scan(show_all: bool) -> list[tuple[Path, int, str, str]]:
    keys = load_keys()
    hits: list[tuple[Path, int, str, str]] = []

    for path in sorted(SCRIPTS.rglob("*.cs")):
        if SKIP_DIRS & set(path.relative_to(SCRIPTS).parts) or path.name in SKIP_FILES:
            continue

        # Un appel qui deborde sur plusieurs lignes ne porte son `Debug.Log(` que sur la premiere :
        # ses lignes suivantes passeraient pour du texte affiche. On reste dans l'appel jusqu'a sa
        # parenthese fermante — c'est ce qui distingue un message de journal d'un libelle d'ecran.
        inside_call = False

        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            stripped = line.lstrip()
            if stripped.startswith(("//", "///", "*", "/*")):
                continue

            if inside_call:
                if re.search(r"\);\s*$", line):
                    inside_call = False
                continue

            if NON_DISPLAY.search(line):
                inside_call = not re.search(r"\);\s*$", line)
                continue

            # Les chaines de la ligne, interpolees comprises : `$"Niveau {n}"` porte du texte.
            for literal in re.findall(r'"((?:[^"\\]|\\.)*)"', line):
                if not re.search(r"[A-Za-zÀ-ÿ]", literal):
                    continue
                if literal in keys or TECHNICAL.match(literal):
                    continue
                if INTERPOLATION_DEBRIS.search(literal) or RESOURCE_NAME.match(literal):
                    continue
                # Cle construite a l'execution : `$"BIOME_{slug}_NAME"`, `$"RARITY_{x}"`.
                if re.match(r"^[A-Z0-9_]*\{[^}]*\}[A-Z0-9_]*$", literal):
                    continue
                # Une interpolation seule (`{0}`, `{Score}`) n'est pas du texte.
                if not re.search(r"[A-Za-zÀ-ÿ]{2,}", re.sub(r"\{[^}]*\}", "", literal)):
                    continue
                if not show_all and literal in ALLOWED:
                    continue

                hits.append((path.relative_to(ROOT), number, literal, line.strip()[:100]))

    return hits


def main() -> int:
    show_all = "--all" in sys.argv[1:]
    hits = scan(show_all)

    if not hits:
        print("OK — aucun texte affichable ecrit en dur dans le code du jeu.")
        return 0

    print(f"!! {len(hits)} chaines suspectes — du texte affiche qui ne passe pas par ui.csv :\n")
    for path, number, literal, context in hits:
        print(f"{path}:{number}")
        print(f"   « {literal} »")
        print(f"   {context}")

    print("\nChacune est soit a traduire (creer la cle, appeler Loc.T), "
          "soit a acquitter dans ALLOWED avec sa raison.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
