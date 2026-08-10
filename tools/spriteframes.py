"""Écriture des manifestes d'animation lus par l'éditeur Unity.

Ce que ce module remplace
-------------------------
Les générateurs de sprites animés écrivaient une ressource ``SpriteFrames`` de Godot
(``<id>_frames.tres``), qu'un second script convertissait ensuite en manifeste JSON pour Unity.
Godot parti, l'aller-retour n'a plus d'objet : on écrit directement le manifeste.

Pourquoi un manifeste JSON et pas un ``.asset`` Unity : un ``.asset`` est du YAML qui référence les
sprites par GUID et fileID, identifiants gérés par l'AssetDatabase. Les fabriquer depuis Python
reviendrait à les deviner — fragile, et silencieusement faux si un GUID change. On produit donc une
description neutre, et ``unity/Assets/Editor/BuildSpriteFrames.cs`` construit les assets en
résolvant les références proprement (menu *Chimera → Rebuild SpriteFrames*).

⚠ Un manifeste écrit ne devient une animation jouable **qu'après** ce passage dans l'éditeur.
Régénérer des images sans reconstruire les SpriteFrames laisse le jeu sur les anciennes.
"""

from __future__ import annotations

import json
from pathlib import Path

import unity_paths

MANIFEST_DIR = unity_paths.UNITY_ASSETS / "Editor" / "spriteframes"

# Préfixe attendu par BuildSpriteFrames : un chemin de projet Unity, pas un chemin disque.
_ASSET_PREFIX = "Assets/Art/sprites"


def write_manifest(entity_id: str, art_subdir: str, animations: list[dict]) -> Path:
    """Écrit ``<entity_id>.json``.

    :param entity_id:  identifiant du jeu (``"drone"``, ``"titan"``…), qui sert de nom d'asset.
    :param art_subdir: dossier sous ``Assets/Art/sprites/`` où vivent les images
                       (``"enemies/drone"``, ``"player/titan"``…).
    :param animations: liste de ``{"name", "speed", "loop", "frames": [<nom de fichier>, …]}``.
                       Les noms d'images sont relatifs à ``art_subdir``.
    """
    if not animations:
        raise ValueError(f"{entity_id} : aucune animation — un manifeste vide casse le chargement")

    payload = {
        "id": entity_id,
        "animations": [
            {
                "name": a["name"],
                "speed": float(a["speed"]),
                "loop": bool(a.get("loop", True)),
                "frames": [f"{_ASSET_PREFIX}/{art_subdir}/{f}" for f in a["frames"]],
            }
            for a in animations
        ],
    }

    for anim in payload["animations"]:
        if not anim["frames"]:
            raise ValueError(f"{entity_id} : l'animation '{anim['name']}' n'a aucune image")

    MANIFEST_DIR.mkdir(parents=True, exist_ok=True)
    out = MANIFEST_DIR / f"{entity_id}.json"
    out.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return out


def write_numbered(entity_id: str, art_subdir: str, prefix: str,
                   counts: dict[str, int], speeds: dict[str, float],
                   order: list[str] | None = None,
                   loop_false: tuple[str, ...] = ("death",)) -> Path:
    """Cas courant : des images numérotées ``<prefix>_<anim>_01.png``.

    ``counts`` donne le nombre d'images par animation, ``speeds`` leur vitesse. Les animations de
    ``loop_false`` ne bouclent pas — une mort qui repart en boucle est le défaut classique.
    """
    names = order or list(counts.keys())

    animations = [
        {
            "name": anim,
            "speed": speeds[anim],
            "loop": anim not in loop_false,
            "frames": [f"{prefix}_{anim}_{i + 1:02d}.png" for i in range(counts[anim])],
        }
        for anim in names if counts.get(anim)
    ]

    return write_manifest(entity_id, art_subdir, animations)
