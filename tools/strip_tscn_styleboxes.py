"""Retire d'une scene .tscn ses StyleBoxFlat inline et les overrides qui les referencent.

Les cadres sont desormais produits par la fabrique unique src/UI/UiStyle.cs. Laisser un
`theme_override_styles/*` dans la scene est pire qu'inutile : il ECRASE l'override pose au
runtime (cf. docs/PITFALLS.md - UI, pieges StyleBox / focus), donc le nouveau style ne
s'appliquerait jamais.

Usage :
    python tools/strip_tscn_styleboxes.py scenes/MainMenu.tscn [autre.tscn ...]
    python tools/strip_tscn_styleboxes.py --dry-run scenes/MainMenu.tscn
"""
import os
import re
import sys

SUB_HEADER = re.compile(r'^\[sub_resource type="StyleBoxFlat" id="([^"]+)"\]')
OVERRIDE = re.compile(r'^theme_override_styles/\w+ = SubResource\("([^"]+)"\)')
LOAD_STEPS = re.compile(r'(\[gd_scene [^\]]*load_steps=)(\d+)')


def strip(path, dry_run=False):
    with open(path, encoding="utf-8") as fh:
        lines = fh.read().splitlines()

    removed_ids, out, i = set(), [], 0
    while i < len(lines):
        header = SUB_HEADER.match(lines[i])
        if not header:
            out.append(lines[i])
            i += 1
            continue
        # Un bloc sub_resource court jusqu'a la prochaine ligne ouvrant un bloc.
        removed_ids.add(header.group(1))
        i += 1
        while i < len(lines) and not lines[i].startswith("["):
            i += 1
        while out and out[-1].strip() == "":       # evite d'empiler les lignes vides
            out.pop()
        out.append("")

    kept, dropped_overrides = [], 0
    for line in out:
        ref = OVERRIDE.match(line)
        if ref and ref.group(1) in removed_ids:
            dropped_overrides += 1
            continue
        kept.append(line)

    if removed_ids and kept:
        kept[0] = LOAD_STEPS.sub(
            lambda m: m.group(1) + str(max(1, int(m.group(2)) - len(removed_ids))), kept[0])

    name = os.path.basename(path)
    print(f"{name} : {len(removed_ids)} StyleBoxFlat retirees, {dropped_overrides} overrides nettoyes")
    if not dry_run and removed_ids:
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write("\n".join(kept) + "\n")
    return len(removed_ids)


def main(argv):
    dry_run = "--dry-run" in argv
    targets = [a for a in argv if not a.startswith("--")]
    if not targets:
        print(__doc__)
        return 1
    for target in targets:
        strip(target, dry_run)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
