"""Verifie l'etat des regles de permission du settings global."""
import json
import os

path = os.path.join(os.path.expanduser("~"), ".claude", "settings.json")
with open(path, encoding="utf-8") as fh:
    perms = json.load(fh)["permissions"]

print("JSON valide —",
      "deny:", len(perms["deny"]),
      "ask:", len(perms["ask"]),
      "allow:", len(perms["allow"]))

blind = [r for r in perms["deny"] if r.endswith("(rm -rf *)") or "Item * -Rec" in r]
print("regles aveugles restantes :", blind or "aucune")

targeted = [r for r in perms["deny"] if "Remove-Item" in r or r.startswith("Bash(rm")]
print("suppressions ciblees :", len(targeted))
for rule in targeted:
    print("   -", rule)
