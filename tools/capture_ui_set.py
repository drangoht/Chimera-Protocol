"""Capture une serie d'ecrans d'UI en une passe, pour comparaison avant/apres refonte.

Usage :
    python tools/capture_ui_set.py [prefixe]

Ecrit dans docs/ui_<prefixe>_<ecran>.png (prefixe par defaut : "before").
Chaque ecran est lance dans une instance Godot dediee via tools/screenshot_scene.py,
qui gere le focus fenetre et la capture PrintWindow.
"""
import os
import subprocess
import sys

TOOLS = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(TOOLS)
PYTHON = sys.executable

PREFIX = sys.argv[1] if len(sys.argv) > 1 else "before"

# (nom court, scene, secondes d'attente, touches a envoyer avant capture)
SCREENS = [
    ("menu", "res://scenes/MainMenu.tscn", 5, ""),
    ("codexmenu", "res://scenes/ui/CodexMenuScreen.tscn", 4, ""),
    ("hub", "res://scenes/ui/HubScreen.tscn", 4, ""),
    ("options", "res://scenes/ui/OptionsScreen.tscn", 4, ""),
    ("charselect", "res://scenes/ui/CharacterSelectScreen.tscn", 4, ""),
    ("levelselect", "res://scenes/ui/LevelSelectScreen.tscn", 4, ""),
    ("challenges", "res://scenes/ui/ChallengesScreen.tscn", 4, ""),
]

# LevelUpScreen / AssimilationScreen / PauseScreen sont des modales : chargees seules
# elles s'affichent vides (aucune carte a peupler). Il faut une vraie partie, puis
# capturer quand le modal s'ouvre — le jeu se met alors en pause (ModalQueue), donc une
# capture apres une duree large suffit. capture_assimilation.py implemente ce pattern :
# avec une duree courte, le premier modal rencontre est le level-up.
IN_RUN = [
    ("levelup", 35),
]

failures = []
for name, scene, wait, keys in SCREENS:
    out = f"ui_{PREFIX}_{name}.png"
    env = dict(os.environ)
    env.update({
        "SCENE": scene,
        "OUT": out,
        "WAIT": str(wait),
        "KEYS": keys,
        "CLIENT_ONLY": "1",
        "PYTHONIOENCODING": "utf-8",
    })
    print(f"--- {name} : {scene}")
    res = subprocess.run(
        [PYTHON, "-X", "utf8", os.path.join(TOOLS, "screenshot_scene.py")],
        env=env, capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    tail = (res.stdout or "").strip().splitlines()
    print("   ", tail[-1] if tail else f"(pas de sortie) rc={res.returncode}")
    if res.returncode != 0 or not os.path.exists(os.path.join(PROJ, "docs", out)):
        failures.append(name)

for name, duration in IN_RUN:
    out = f"docs/ui_{PREFIX}_{name}.png"
    env = dict(os.environ)
    env.update({
        "DURATION": str(duration),
        "OUT": out,
        "PYTHONIOENCODING": "utf-8",
    })
    print(f"--- {name} : en run ({duration}s)")
    res = subprocess.run(
        [PYTHON, "-X", "utf8", os.path.join(TOOLS, "capture_assimilation.py")],
        env=env, capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    tail = (res.stdout or "").strip().splitlines()
    print("   ", tail[-1] if tail else f"(pas de sortie) rc={res.returncode}")
    if not os.path.exists(os.path.join(PROJ, out)):
        failures.append(name)

print("\n=== termine ===")
print("echecs :", failures or "aucun")
