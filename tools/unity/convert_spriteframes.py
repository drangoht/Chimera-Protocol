#!/usr/bin/env python3
"""Convertit les ressources SpriteFrames de Godot en manifestes JSON pour le port Unity.

docs/UNITY_MIGRATION_PLAN.md §7.2.

Pourquoi un manifeste JSON plutôt qu'un asset Unity écrit directement : un `.asset` Unity est du
YAML référençant les sprites par GUID et fileID. Les fabriquer depuis Python voudrait dire deviner
des identifiants gérés par l'AssetDatabase — fragile et silencieusement faux si un GUID change.
On produit donc une description neutre, et c'est un script d'éditeur Unity qui construit les assets
en résolvant les références proprement (`tools/unity/…` -> `unity/Assets/Editor/BuildSpriteFrames.cs`).

Pourquoi pas Mecanim : le jeu appelle `PlayAnim("attack")` de façon data-driven, avec repli quand
l'animation n'existe pas — le projet a déjà connu 144 erreurs par session sur une animation `attack`
absente. Un lecteur de données reproduit ce contrat exactement, là où 40 AnimatorController seraient
plus lourds et moins tolérants.

Usage :
    py tools/unity/convert_spriteframes.py [--check]
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SRC_ASSETS = REPO / "assets"
OUT_DIR = REPO / "unity" / "Assets" / "Editor" / "spriteframes"

# res://assets/sprites/... -> Assets/Art/sprites/...
RES_PREFIX = "res://assets/"
UNITY_PREFIX = "Assets/Art/"

EXT_RE = re.compile(r'\[ext_resource\s+type="Texture2D"\s+path="([^"]+)"\s+id="([^"]+)"\]')
ANIM_BLOCK_RE = re.compile(r"animations\s*=\s*\[(.*)\]\s*$", re.S)
FRAME_RE = re.compile(r'ExtResource\("([^"]+)"\)')


def unity_path(res_path: str) -> str:
    if not res_path.startswith(RES_PREFIX):
        raise ValueError(f"chemin inattendu (hors assets/) : {res_path}")
    return UNITY_PREFIX + res_path[len(RES_PREFIX):]


def parse_tres(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")

    ext = {rid: p for p, rid in EXT_RE.findall(text)}
    if not ext:
        raise ValueError("aucune texture référencée")

    block = ANIM_BLOCK_RE.search(text)
    if not block:
        raise ValueError("bloc `animations` introuvable")

    animations = []
    # Chaque animation est un objet { "frames": [...], "loop": ..., "name": &"...", "speed": ... }.
    # On les découpe sur la clé "frames", qui ouvre chaque entrée.
    chunks = block.group(1).split('"frames":')
    for chunk in chunks[1:]:
        name_m = re.search(r'"name":\s*&?"([^"]+)"', chunk)
        speed_m = re.search(r'"speed":\s*([0-9.]+)', chunk)
        loop_m = re.search(r'"loop":\s*(true|false)', chunk)
        if not name_m:
            raise ValueError("animation sans nom")

        frame_ids = FRAME_RE.findall(chunk.split('"loop"')[0])
        frames = [unity_path(ext[fid]) for fid in frame_ids]
        if not frames:
            raise ValueError(f"animation '{name_m.group(1)}' sans image")

        animations.append({
            "name": name_m.group(1),
            "speed": float(speed_m.group(1)) if speed_m else 8.0,
            "loop": (loop_m.group(1) == "true") if loop_m else True,
            "frames": frames,
        })

    return {"id": path.stem.removesuffix("_frames"), "animations": animations}


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--check", action="store_true",
                    help="analyse sans rien écrire (contrôle d'intégrité)")
    args = ap.parse_args()

    sources = sorted(SRC_ASSETS.rglob("*_frames.tres"))
    if not sources:
        print("aucun SpriteFrames trouvé", file=sys.stderr)
        return 1

    if not args.check:
        OUT_DIR.mkdir(parents=True, exist_ok=True)

    total_anims = 0
    total_frames = 0
    failures = []

    for src in sources:
        try:
            data = parse_tres(src)
        except Exception as exc:  # noqa: BLE001 — on veut le rapport complet, pas le premier échec
            failures.append((src.name, str(exc)))
            continue

        total_anims += len(data["animations"])
        total_frames += sum(len(a["frames"]) for a in data["animations"])

        if not args.check:
            (OUT_DIR / f"{data['id']}.json").write_text(
                json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"{len(sources) - len(failures)}/{len(sources)} SpriteFrames converties — "
          f"{total_anims} animations, {total_frames} images")

    if failures:
        print(f"\n{len(failures)} echec(s) :", file=sys.stderr)
        for name, err in failures:
            print(f"  {name} : {err}", file=sys.stderr)
        return 1

    if not args.check:
        print(f"manifestes ecrits dans {OUT_DIR.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
