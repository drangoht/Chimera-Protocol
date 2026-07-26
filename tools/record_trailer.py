"""Capture les rushes video du trailer (gameplay, menus, intro).

Utilise le **Movie Maker de Godot** (`--write-movie`) plutot qu'un enregistreur d'ecran :
le rendu est a framerate FIXE (60 fps, cf. `editor/movie_writer/fps`), sans frame drop ni
tearing, et l'audio du jeu est ecrit dans le meme AVI (PCM 48 kHz). La sortie fait la taille
du VIEWPORT (1280x720) -- le flag `--resolution` ne change QUE la fenetre, pas le film ; le
passage en 1440p se fait au montage par upscale nearest x2 (pixel-perfect), cf.
`tools/build_trailer.py`.

Chaque prise lance une instance de Godot avec `--trailer` (masque le tampon de version et
l'invite « appuyer pour passer », cf. `DebugHooks.TrailerMode`), pilote le jeu via PyAutoGUI
selon une timeline declarative, puis ferme la fenetre par WM_CLOSE pour que le MovieWriter
finalise proprement l'AVI (un kill brutal laisserait un index AVI incomplet).

PIEGE : le temps du jeu en mode film n'est PAS le temps reel (le rendu peut aller plus vite
ou plus lentement que 60 fps selon la charge). Les timelines sont donc volontairement
tolerantes -- que du mouvement long et des validations repetees, aucune action a la frame
pres. Le decoupage fin se fait au montage.

Usage :
    python tools/record_trailer.py --list
    python tools/record_trailer.py intro menu
    python tools/record_trailer.py --all

ATTENTION : le script prend le controle du clavier et de la souris pendant toute la capture.
"""
import os
import subprocess
import sys
import time

import pyautogui
import win32con
import win32gui

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from window_capture import wait_for_window_by_pid  # noqa: E402

pyautogui.FAILSAFE = False
pyautogui.PAUSE = 0.0

GODOT = r"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW = os.path.join(PROJ, "trailer", "raw")

GAME = "res://scenes/Game.tscn"
MENU = "res://scenes/MainMenu.tscn"


# ---------------------------------------------------------------------------
# Generateurs de timeline
#   Une action = (t_secondes, kind, arg). kinds :
#     "hold"    maintient une direction (relache l'eventuelle precedente)
#     "press"   tape une touche
#     "accept"  valide une modale (Entree + clic centre en secours)
#     "release" relache la direction maintenue
# ---------------------------------------------------------------------------

def kite(duration, step=1.15, accept_every=2.6, start=1.2, dash_every=None):
    """Deplacement en cercle + validation reguliere des modales (level-up, greffes).

    Le kiting circulaire est ce qui « lit » le mieux en video sur un survivor : la nuee
    s'etire derriere le joueur et les armes auto-visees balaient tout le champ."""
    dirs = ["right", "down", "left", "up", "right", "up", "left", "down"]
    acts = []
    t, i = start, 0
    while t < duration:
        acts.append((t, "hold", dirs[i % len(dirs)]))
        i += 1
        t += step
    # Validation des modales : indispensable, sinon la 1re carte de level-up met le jeu en
    # pause pour toute la prise (LevelUpScreen est prioritaire dans la ModalQueue).
    t = start + 2.0
    while t < duration:
        acts.append((t, "accept", None))
        t += accept_every
    if dash_every:
        t = start + 3.0
        while t < duration:
            acts.append((t, "press", "shiftleft"))   # greffe Servos Erratiques / Frappe Nova
            t += dash_every
    acts.append((duration, "release", None))
    return sorted(acts, key=lambda a: a[0])


def menu_tour():
    """Visite du menu principal -> Codex -> Bestiaire -> Arsenal -> Chimere."""
    seq = [
        (1.6, "press", "down"),          # focus pulse sur les entrees
        (2.4, "press", "down"),
        (3.2, "press", "up"),
        (4.0, "press", "down"),
        (4.8, "press", "down"),          # -> Codex
        (5.8, "press", "enter"),
        (7.4, "press", "down"),          # sous-menu Codex
        (8.2, "press", "up"),
        (9.0, "press", "enter"),         # -> Bestiaire
    ]
    t = 10.6
    for _ in range(7):                   # defilement des fiches d'ennemis
        seq.append((t, "press", "down"))
        t += 0.75
    seq += [
        (t + 0.8, "press", "escape"),    # retour sous-menu Codex
        (t + 2.0, "press", "down"),
        (t + 2.9, "press", "enter"),     # -> Arsenal
    ]
    t = t + 4.4
    for _ in range(6):                   # defilement des armes
        seq.append((t, "press", "down"))
        t += 0.8
    seq += [
        (t + 0.8, "press", "escape"),
        (t + 2.0, "press", "down"),
        (t + 2.8, "press", "enter"),     # -> Chimere (codex des greffes)
    ]
    t = t + 4.2
    for _ in range(5):
        seq.append((t, "press", "down"))
        t += 0.8
    seq.append((t + 1.0, "press", "escape"))
    return sorted(seq, key=lambda a: a[0])


def charsel_tour():
    """Parcours des 4 personnages jouables (cartes + stats)."""
    # Les cartes sont empilees VERTICALEMENT : la navigation est down/up, pas right/left.
    seq = []
    t = 1.6
    for key in ["down", "down", "down", "up", "up", "down"]:
        seq.append((t, "press", key))
        t += 1.6
    return seq


def hub_tour():
    """Defilement des ameliorations permanentes du Hub."""
    seq = []
    t = 1.6
    for _ in range(9):
        seq.append((t, "press", "down"))
        t += 0.9
    for _ in range(4):
        seq.append((t, "press", "up"))
        t += 0.8
    return seq


def challenges_tour():
    seq = []
    t = 1.4
    for _ in range(8):
        seq.append((t, "press", "down"))
        t += 0.85
    return seq


# ---------------------------------------------------------------------------
# Table des prises
# ---------------------------------------------------------------------------
TAKES = {
    # -- Cinematique d'intro : aucune action, elle se joue seule (~26 s).
    #    45 s : la cinematique complete fait ~36 s de temps de jeu et se termine par le
    #    reveal anime du titre -- ce plan sert de carton final au trailer.
    "intro": dict(scene=None, seconds=48, args=[], actions=[]),

    # -- Menus / meta
    "menu":       dict(scene=MENU, seconds=40, args=[], actions=menu_tour()),
    "charsel":    dict(scene="res://scenes/ui/CharacterSelectScreen.tscn",
                       seconds=12, args=[], actions=charsel_tour()),
    "hub":        dict(scene="res://scenes/ui/HubScreen.tscn",
                       seconds=15, args=[], actions=hub_tour()),
    "challenges": dict(scene="res://scenes/ui/ChallengesScreen.tscn",
                       seconds=10, args=[], actions=challenges_tour()),
    # Ecrans du Codex lances DIRECTEMENT : traverser le menu jusqu'a eux est trop
    # sensible a la derive de timing (la prise « menu » a fini dans Options).
    "bestiary":   dict(scene="res://scenes/ui/BestiaryScreen.tscn",
                       seconds=12, args=[], actions=challenges_tour()),
    "arsenal":    dict(scene="res://scenes/ui/ArsenalScreen.tscn",
                       seconds=12, args=[], actions=challenges_tour()),
    "chimera_codex": dict(scene="res://scenes/ui/ChimeraCodexScreen.tscn",
                          seconds=12, args=[], actions=challenges_tour()),

    # -- Gameplay : un biome par prise, chacune avec un angle different.
    "gp_sanctuaire": dict(scene=GAME, seconds=75, args=["--biome=sanctuaire"],
                          actions=kite(75)),
    "gp_neon":       dict(scene=GAME, seconds=55, args=["--biome=neon", "--force-graft=all"],
                          actions=kite(55, dash_every=4.0)),
    "gp_fournaise":  dict(scene=GAME, seconds=50, args=["--biome=fournaise", "--force-elites"],
                          actions=kite(50)),
    "gp_givre":      dict(scene=GAME, seconds=50, args=["--biome=givre", "--force-fusion=all"],
                          actions=kite(50, dash_every=3.5)),
    "gp_aether":     dict(scene=GAME, seconds=50, args=["--biome=aether", "--force-buff"],
                          actions=kite(50)),
    "boss":          dict(scene=GAME, seconds=55, args=["--debug-boss"],
                          actions=kite(55, accept_every=3.2)),

    # -- Le boss seul tue un joueur niveau 1 en ~15 s : sans loadout de survie la prise
    #    part en ecran de fin. Greffes + buffs pour tenir la duree du plan.
    "boss_tank":     dict(scene=GAME, seconds=70,
                          args=["--debug-boss", "--force-graft=all", "--force-buff"],
                          actions=kite(70, dash_every=5.0)),

    # -- Prises LONGUES : le spectacle d'un survivor est en mid/late game (nuees denses,
    #    armes niveau 4-5, ecran sature de VFX). La 1re minute ne montre presque rien.
    "long_neon":     dict(scene=GAME, seconds=300,
                          args=["--biome=neon", "--force-graft=all"],
                          actions=kite(300, dash_every=6.0)),
    "long_fournaise": dict(scene=GAME, seconds=300,
                           args=["--biome=fournaise", "--force-elites", "--force-buff"],
                           actions=kite(300)),
}


def run_take(name):
    take = TAKES[name]
    os.makedirs(RAW, exist_ok=True)
    avi = os.path.join(RAW, f"{name}.avi")
    if os.path.exists(avi):
        os.remove(avi)

    cmd = [GODOT, "--path", PROJ, "--rendering-driver", "d3d12", "--write-movie", avi]
    if take["scene"]:
        cmd.append(take["scene"])
    cmd += ["--", "--trailer"] + take["args"]

    print(f"=== {name} : {take['seconds']}s  {' '.join(take['args']) or '(sans flag)'}")
    proc = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    held = None
    try:
        hwnd = wait_for_window_by_pid(proc.pid, timeout=45.0)
        if not hwnd:
            print(f"  !! fenetre introuvable pour {name}")
            return False

        l, t_, r, b = win32gui.GetWindowRect(hwnd)
        pyautogui.click(l + (r - l) // 3, t_ + 15)      # focus OS reel (barre de titre)
        try:
            win32gui.SetForegroundWindow(hwnd)
        except Exception:
            pass
        time.sleep(0.6)
        l, t_, r, b = win32gui.GetWindowRect(hwnd)
        cx, cy = (l + r) // 2, (t_ + b) // 2
        pyautogui.moveTo(cx, cy)

        t0 = time.time()
        for when, kind, arg in take["actions"]:
            time.sleep(max(0.0, t0 + when - time.time()))
            if kind == "hold":
                if held and held != arg:
                    pyautogui.keyUp(held)
                if held != arg:
                    pyautogui.keyDown(arg)
                    held = arg
            elif kind == "press":
                pyautogui.press(arg)
            elif kind == "accept":
                pyautogui.press("enter")
                pyautogui.click(cx, cy)
            elif kind == "release":
                if held:
                    pyautogui.keyUp(held)
                    held = None
        time.sleep(max(0.0, t0 + take["seconds"] - time.time()))
    finally:
        if held:
            pyautogui.keyUp(held)
        # Fermeture PROPRE : le MovieWriter doit finaliser l'index de l'AVI.
        try:
            win32gui.PostMessage(hwnd, win32con.WM_CLOSE, 0, 0)
        except Exception:
            pass
        try:
            proc.wait(timeout=20)
        except Exception:
            proc.terminate()
            try:
                proc.wait(timeout=5)
            except Exception:
                proc.kill()

    size = os.path.getsize(avi) if os.path.exists(avi) else 0
    print(f"  -> {avi}  ({size / 1e6:.1f} Mo)")
    return size > 0


if __name__ == "__main__":
    argv = sys.argv[1:]
    if not argv or "--list" in argv:
        for k, v in TAKES.items():
            print(f"{k:16s} {v['seconds']:>3d}s  {' '.join(v['args'])}")
        sys.exit(0)
    names = list(TAKES) if "--all" in argv else [a for a in argv if a in TAKES]
    unknown = [a for a in argv if a not in TAKES and not a.startswith("--")]
    if unknown:
        print("Prises inconnues :", unknown)
        sys.exit(2)
    ok = True
    for n in names:
        ok = run_take(n) and ok
        time.sleep(1.0)
    sys.exit(0 if ok else 1)
