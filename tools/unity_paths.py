"""Où un asset généré doit atterrir dans le projet Unity.

Pourquoi ce module existe
-------------------------
Tant que le dépôt portait Godot, les générateurs écrivaient tous dans `assets/`, et le moteur
lisait ce même dossier. Unity ne fonctionne pas ainsi : il y a **deux** emplacements, et ils ne se
valent pas.

* ``Assets/Art/`` — importé par l'éditeur, consommé par **référence de GUID**. C'est de là que
  ``BuildSpriteFrames`` tire les planches d'animation (ennemis, joueur, armes, ramassages). Un
  fichier posé là est visible dans le jeu **seulement** si un prefab, une scène ou un
  ``SpriteFramesAsset`` le cite.
* ``Assets/Resources/`` — chargé **par chemin** à l'exécution (``Resources.Load<Sprite>("Ui/…")``).
  Tout ce que le code va chercher par son nom doit être là, et **uniquement** ce qui sert : le
  dossier ``Resources`` est embarqué en entier dans le binaire.

Écrire dans le mauvais des deux ne lève aucune erreur. Le générateur annonce « écrit », le fichier
existe, le jeu affiche l'ancienne image — c'est le défaut favori de ce projet (« déclaré n'est pas
consommé »). D'où cette table, unique et explicite, plutôt qu'un chemin recopié dans vingt scripts.

Constat au moment de la bascule (audit des GUID, 2026-08-10) : la migration avait copié
``assets/`` en entier dans ``Art/``, puis n'avait câblé que le nécessaire. Les copies
``Art/sprites/ui`` et ``Art/sprites/grafts`` n'étaient citées par **rien** — le jeu chargeait les
exemplaires de ``Resources/Ui``. Elles ont été supprimées ; les icônes se génèrent désormais
directement là où le jeu les lit.
"""

from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
UNITY_ASSETS = REPO_ROOT / "unity" / "Assets"

ART = UNITY_ASSETS / "Art"
RESOURCES = UNITY_ASSETS / "Resources"
STREAMING = UNITY_ASSETS / "StreamingAssets"

# --- Familles de sprites dont TOUT le contenu est chargé par chemin -------------------------
_RESOURCE_FAMILIES = {
    "ui": RESOURCES / "Ui",
    "ui/frames": RESOURCES / "UiFrames",
    "grafts": RESOURCES / "Ui",       # les icônes de greffe partagent le dossier des icônes d'UI
}

# --- Familles consommées par GUID (planches d'animation, décor de scène) --------------------
_ART_FAMILIES = ("enemies", "player", "weapons", "pickups", "tileset", "environment", "vfx")

# --- Exceptions : fichiers de familles « Art » que le code charge malgré tout par chemin ----
# Ce sont exactement les noms cités dans ArenaRenderer / IntroScreen / VfxPrimitives. Les y
# ajouter sans câbler le chargement ne servirait qu'à alourdir le binaire.
_RUNTIME_FILES = {
    "environment/decor_column.png": RESOURCES / "Environment",
    "environment/tile_floor_stone.png": RESOURCES / "Environment",
    "environment/tile_pillar_stone.png": RESOURCES / "Environment",
    "environment/tile_terminal_corrupt_01.png": RESOURCES / "Environment",
    "environment/tile_wreck_machine.png": RESOURCES / "Environment",
    "tileset/tile_floor_glass.png": RESOURCES / "Environment",
    "vfx/intro_noyau.png": RESOURCES / "Vfx",
    "vfx/vfx_aura_fusionblade.png": RESOURCES / "Vfx",
    "vfx/vfx_particle_noyau.png": RESOURCES / "Vfx",
}


def sprite_dir(family: str) -> Path:
    """Dossier de sortie d'une famille de sprites (``"ui"``, ``"enemies/drone"``, ``"vfx"``…).

    Pour les familles dont quelques fichiers seulement sont chargés par chemin, préférer
    :func:`sprite_path`, qui traite ces exceptions nom par nom.
    """
    family = family.strip("/")

    if family in _RESOURCE_FAMILIES:
        return _RESOURCE_FAMILIES[family]

    head = family.split("/", 1)[0]
    if head in _ART_FAMILIES:
        return ART / "sprites" / family

    raise KeyError(
        f"Famille de sprites inconnue : {family!r}. "
        "Ajouter sa destination dans tools/unity_paths.py plutôt que de deviner un chemin."
    )


def sprite_path(relative: str) -> Path:
    """Chemin complet d'un sprite, exception de chargement comprise.

    ``sprite_path("environment/decor_column.png")`` renvoie ``Resources/Environment/…`` parce que
    ``ArenaRenderer`` le charge par ce nom, tandis que les autres décors restent sous ``Art/``.
    """
    relative = relative.replace("\\", "/").lstrip("/")

    if relative in _RUNTIME_FILES:
        return _RUNTIME_FILES[relative] / Path(relative).name

    family, _, name = relative.rpartition("/")
    return sprite_dir(family) / name


def audio_dir(kind: str) -> Path:
    """``"music"`` ou ``"sfx"`` — tous deux chargés par chemin (``Audio/sfx/<id>``)."""
    if kind not in ("music", "sfx"):
        raise KeyError(f"Type d'audio inconnu : {kind!r}")
    return RESOURCES / "Audio" / kind


def font_dir() -> Path:
    """Polices, chargées par ``Resources.Load<Font>("Fonts/…")``."""
    return RESOURCES / "Fonts"


def data_dir() -> Path:
    """Données de tuning JSON, embarquées telles quelles dans le binaire."""
    return STREAMING / "data"


def ensure(path: Path) -> Path:
    """Crée le dossier parent d'un fichier (ou le dossier lui-même) et le renvoie."""
    target = path if path.suffix == "" else path.parent
    target.mkdir(parents=True, exist_ok=True)
    return path
