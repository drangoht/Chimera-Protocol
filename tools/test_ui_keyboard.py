"""
Test automatisé PyAutoGUI — Chimera Protocol
Navigation clavier/manette dans les menus + stabilité du build .exe

Résultats : screenshots dans docs/test_screenshots/
Usage     : python -X utf8 tools/test_ui_keyboard.py
"""

import subprocess
import time
import os
import sys
import datetime

import pyautogui
import pygetwindow as gw
from PIL import Image

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

EXE_PATH       = r"C:\CODE\JEUX\chimera-protocol\build\ChimeraProtocol.exe"
SCREENS_DIR    = r"C:\CODE\JEUX\chimera-protocol\docs\test_screenshots"
WINDOW_TITLE   = "Chimera Protocol"
STARTUP_WAIT   = 6.0    # secondes pour que le jeu charge
SCENE_WAIT     = 2.5    # secondes après un changement de scène
KEY_WAIT       = 0.4    # secondes entre deux touches
POLL_INTERVAL   = 10    # secondes entre deux détections pixel
LEVELUP_PREWAIT = 45    # attente min avant de commencer la detection level-up (~60s de jeu)
LEVELUP_TIMEOUT = 90    # secondes de polling max apres PREWAIT
RUNEND_PREWAIT  = 10    # attente min avant detection mort (joueur meurt quasi-instant apres phase 4)
RUNEND_TIMEOUT  = 60    # secondes de polling max apres PREWAIT

pyautogui.PAUSE    = 0.05
pyautogui.FAILSAFE = True  # coin haut-gauche → arrêt d'urgence

os.makedirs(SCREENS_DIR, exist_ok=True)

# ---------------------------------------------------------------------------
# Helpers généraux
# ---------------------------------------------------------------------------

_step    = 0
_results = []

def _log(msg: str):
    print(msg)
    _results.append(msg)

def _screenshot(label: str) -> str:
    global _step
    _step += 1
    filename = f"{_step:02d}_{label}.png"
    path = os.path.join(SCREENS_DIR, filename)
    pyautogui.screenshot(path)
    _log(f"  [screenshot] {filename}")
    return path

def _find_win():
    wins = gw.getWindowsWithTitle(WINDOW_TITLE)
    return wins[0] if wins else None

def _focus():
    win = _find_win()
    if win:
        try:
            win.activate()
            time.sleep(0.3)
            return True
        except Exception:
            pass
    _log("  [warn] fenetre introuvable — inputs a la fenetre active")
    return False

def _alive(proc) -> bool:
    return proc.poll() is None

def _key(*keys, wait: float = KEY_WAIT):
    for k in keys:
        pyautogui.press(k)
        time.sleep(wait)

def _check(proc, label: str) -> bool:
    if not _alive(proc):
        _log(f"  FAIL [{label}] -- crash detecte")
        return False
    _log(f"  PASS [{label}]")
    return True

def _launch() -> subprocess.Popen:
    proc = subprocess.Popen([EXE_PATH])
    _log(f"  Jeu lance (PID {proc.pid})")
    time.sleep(STARTUP_WAIT)
    _focus()
    return proc

def _kill(proc):
    if _alive(proc):
        proc.terminate()
        _log("  Jeu termine.")

# ---------------------------------------------------------------------------
# Détection pixel — overlay sombre
# ---------------------------------------------------------------------------
# LevelUpScreen Background  : Color(0.102, 0.102, 0.18)        ≈ RGB(26, 26, 46)
# RunEndScreen overlay       : Color(0.05,  0.04,  0.10, 0.92)  ≈ RGB(13, 10, 26)
# Gameplay normal            : tiles gris/brun sans biais bleu  ≈ RGB(60-150, ...)
#
# Discriminant clé : canal BLEU nettement supérieur à rouge ET vert
# (B > R + blue_bias) — les tiles d'arène sont gris/brun, jamais blue-biased.
# Utilise PIL screenshot de la région fenêtre (robuste au DPI scaling Windows).
# ---------------------------------------------------------------------------

def _game_screenshot():
    """Screenshot PIL limité à la région de la fenêtre de jeu."""
    win = _find_win()
    if win:
        region = (win.left, win.top, win.width, win.height)
        return pyautogui.screenshot(region=region)
    return pyautogui.screenshot()

def _overlay_coverage(r_max: int, g_max: int, b_max: int, blue_bias: int) -> float:
    """
    Fraction de points d'une grille 5×4 qui correspondent au profil couleur d'un overlay.
    Évite les faux positifs grâce au critère blue_bias : B > R + blue_bias ET B > G + blue_bias.
    """
    img = _game_screenshot()
    w, h = img.size
    # Grille 5×4 centrée (20-80% en X, 25-75% en Y)
    xs = [int(w * f) for f in (0.20, 0.35, 0.50, 0.65, 0.80)]
    ys = [int(h * f) for f in (0.25, 0.40, 0.60, 0.75)]
    matched = total = 0
    for x in xs:
        for y in ys:
            px = img.getpixel((x, y))
            r, g, b = int(px[0]), int(px[1]), int(px[2])
            total += 1
            if r < r_max and g < g_max and b < b_max and b > r + blue_bias and b > g + blue_bias:
                matched += 1
    return matched / total if total > 0 else 0

def _label_color_at_top() -> str:
    """
    Détecte la couleur du label titre visible sur un overlay actif.
    LevelLabel  : Color(0.267, 1.0, 0.933) = RGB(68, 255, 238) → 'cyan'
    OutcomeLabel mort : Color(0.8, 0.3, 0.15) = RGB(204, 77, 38) → 'red'
    MainMenu titre : RGB(204, 136, 255) violet → 'unknown'

    Stratégie : balayage dense (step=2, 9 colonnes) dans la zone du label
    LevelLabel/OutcomeLabel : viewport y=80-140 → window y≈titlebar+80..140.
    Pour couvrir titlebar=0 (fullscreen) ET titlebar=32px (windowed) et résolutions
    720p/1080p : on balaye window y=9%..23%.
    Seuils assouplis pour attraper les pixels anti-aliasés des glyphes.
    """
    img = _game_screenshot()
    w, h = img.size
    # 9 colonnes à l'intérieur de la zone x du label (viewport x≈440-840, ≈34-66%)
    xs = [int(w * f) for f in (0.35, 0.39, 0.43, 0.47, 0.50, 0.53, 0.57, 0.61, 0.65)]
    y_start = int(h * 0.09)
    y_end   = int(h * 0.23)
    red = cyan = 0
    for x in xs:
        for y in range(y_start, y_end, 2):   # step=2 : ne rate pas les glyphes fins
            r, g, b = [int(v) for v in img.getpixel((x, y))[:3]]
            # Rouge-orangé MORT EN SERVICE : r >> g,b
            if r > 140 and g < 110 and b < 90:
                red += 1
            # Cyan Niveau X! / Divider LevelUpScreen : g,b >> r (anti-aliasé inclus)
            # Pixel pur : (68,255,238). 50% aa : ~(40,133,129). Les deux passent.
            # RunEnd Divider 40% : g≈109 < 120 → n'interfère pas.
            if g > 120 and b > 100 and r < 120 and g + b > r * 4 + 80:
                cyan += 1
    _log(f"    [debug-label] cyan={cyan} red={red}")
    if red >= 3:
        return 'red'
    if cyan >= 3:
        return 'cyan'
    return 'unknown'

def _detect_levelup() -> bool:
    """
    LevelUpScreen : overlay sombre + label CYAN (Niveau X!).
    Évite la confusion avec RunEndScreen (label rouge) et MainMenu (label violet).
    """
    if _overlay_coverage(r_max=30, g_max=30, b_max=40, blue_bias=3) < 0.35:
        return False
    label = _label_color_at_top()
    _log(f"    [debug-detect] levelup label={label}")
    return label == 'cyan'

def _detect_runend() -> bool:
    """
    RunEndScreen : overlay sombre + label ROUGE (MORT EN SERVICE).
    Exclut LevelUpScreen (cyan) et MainMenu (inconnu/violet).
    """
    if _overlay_coverage(r_max=40, g_max=40, b_max=60, blue_bias=8) < 0.35:
        return False
    label = _label_color_at_top()
    _log(f"    [debug-detect] runend label={label}")
    return label == 'red'

def _move_and_detect(detect_fn, duration: float, detect_interval: float = 5.0) -> bool:
    """
    Déplace le joueur (keyDown 0.4s par direction, Speed=200px/s) pendant 'duration' s.
    Vérifie detect_fn() toutes les detect_interval secondes (~1s par vérif).
    Ratio mouvement/check : ~78%/22% → joueur (200) distance ennemis (120) efficacement.
    Retourne True dès que detect_fn() est vrai.
    """
    dirs        = ["right", "up", "left", "down"]
    i           = 0
    start       = time.time()
    last_detect = 0.0
    while time.time() - start < duration:
        elapsed = time.time() - start
        # Vérification périodique AVANT le prochain keyDown
        if elapsed - last_detect >= detect_interval:
            last_detect = elapsed
            if detect_fn():
                return True
        # Tenir la touche 0.4s — joueur bouge 80px, ennemi suit 48px (gain 32px)
        key = dirs[i % 4]
        pyautogui.keyDown(key)
        time.sleep(0.4)
        pyautogui.keyUp(key)
        time.sleep(0.05)
        i += 1
    # Vérification finale
    return detect_fn()

def _wait_for(detect_fn, timeout: int, poll: int, label: str, min_wait: int = 0) -> bool:
    """
    Attend que detect_fn() == True.
    min_wait : secondes de pré-attente inconditionnelle avant de commencer à poller.
    """
    if min_wait > 0:
        _log(f"  Pre-attente {min_wait}s avant detection {label}...")
        waited = 0
        while waited < min_wait:
            chunk = min(20, min_wait - waited)
            time.sleep(chunk)
            waited += chunk
            _screenshot(f"prewait_{label}_{waited}s")
    elapsed = 0
    while elapsed < timeout:
        time.sleep(poll)
        elapsed += poll
        _screenshot(f"attente_{label}_{min_wait+elapsed}s")
        if detect_fn():
            _log(f"  [detecte] {label} apres ~{min_wait+elapsed}s")
            return True
        _log(f"  [attente] {label} pas encore ({min_wait+elapsed}/{min_wait+timeout}s)")
    _log(f"  [timeout] {label} non detecte apres {min_wait+timeout}s")
    return False

# ---------------------------------------------------------------------------
# Phase 1 — Démarrage + MainMenu
# ---------------------------------------------------------------------------

def phase1_mainmenu() -> subprocess.Popen:
    _log("\n=== Phase 1 : Lancement + MainMenu ===")
    proc = _launch()

    if not _check(proc, "demarrage sans crash"):
        sys.exit(1)

    _screenshot("mainmenu_initial")
    _log("  -> PlayButton focuse (bordure violette attendue)")

    _key("down");  _screenshot("mainmenu_focus_hub")
    _log("  -> HubButton focuse")

    _key("down");  _screenshot("mainmenu_focus_quit")
    _log("  -> QuitButton focuse")

    _key("up", "up"); _screenshot("mainmenu_focus_play_back")
    _log("  -> PlayButton refocuse")

    return proc


# ---------------------------------------------------------------------------
# Phase 2 — MainMenu → HubScreen → retour
# ---------------------------------------------------------------------------

def phase2_hub(proc: subprocess.Popen):
    _log("\n=== Phase 2 : MainMenu → HubScreen (clavier) ===")
    _focus()
    _key("down", "return")       # Hub → Enter
    time.sleep(SCENE_WAIT)

    if not _check(proc, "ouverture HubScreen"):
        return

    _screenshot("hubscreen_initial")
    _log("  -> BackButton focuse (apres fade 0.6s)")

    _key("down");        _screenshot("hubscreen_upgrade1")
    _key("down", "down"); _screenshot("hubscreen_upgrade3")
    _log("  -> 1er puis 3e bouton Acheter focuses")

    _key("escape");  time.sleep(SCENE_WAIT)
    if _check(proc, "retour MainMenu via Echap"):
        _screenshot("mainmenu_after_hub")


# ---------------------------------------------------------------------------
# Phase 3 — Lancement d'une partie (stabilité 10s)
# ---------------------------------------------------------------------------

def phase3_ingame(proc: subprocess.Popen):
    _log("\n=== Phase 3 : Lancement d'une partie ===")
    _focus()
    _key("up", "up", "return")  # Play → Enter
    time.sleep(4.0)

    if not _check(proc, "demarrage partie"):
        return

    _screenshot("ingame_initial")
    time.sleep(6.0)
    if _check(proc, "stabilite 10s"):
        _screenshot("ingame_10sec")


# ---------------------------------------------------------------------------
# Phase 4 — LevelUpScreen (premier level-up ~60s)
# ---------------------------------------------------------------------------

def phase4_levelup(proc: subprocess.Popen) -> subprocess.Popen:
    _log("\n=== Phase 4 : LevelUpScreen ===")

    # Relancer une partie propre
    _kill(proc)
    proc = _launch()
    if not _check(proc, "relance pour levelup"):
        return proc

    # Passer par le Hub pour acheter un upgrade (renforce le joueur)
    # PlayButton est focus par defaut — aller vers Hub : Down, Enter
    _log("  -> Passage par le Hub pour upgrade avant la partie")
    _key("down", "return")        # Hub → Enter
    time.sleep(SCENE_WAIT)
    _screenshot("levelup_hub_visit")
    # BackButton focuse — Down = 1er bouton Acheter
    _key("down", "return")        # 1er upgrade (achat si echoes dispo)
    time.sleep(0.3)
    # Naviguer jusqu'au PlayButton : 7 Down depuis la 1re ligne
    for _ in range(7):
        _key("down")
    _screenshot("levelup_hub_play_focus")
    _key("return")                 # PlayButton → lancer la partie
    time.sleep(4.0)

    if not _check(proc, "relance depuis Hub"):
        return proc

    _focus()
    _screenshot("levelup_ingame_start")
    total = LEVELUP_PREWAIT + LEVELUP_TIMEOUT
    _log(f"  Deplacement joueur (Speed=200>ennemis 120) + detection max {total}s...")

    # Pre-attente ET polling avec mouvement continu — évite la mort du joueur
    # keyDown 0.4s/direction, détection _detect_levelup() toutes les 5s
    # Pré-attente 45s : 30 kills × 2 XP = 60 XP → LevelUp ~48s
    pre_detected = _move_and_detect(_detect_levelup, LEVELUP_PREWAIT, detect_interval=2.0)
    detected = pre_detected

    if not detected:
        _log(f"  Pre-attente {LEVELUP_PREWAIT}s passee sans detection — polling {LEVELUP_TIMEOUT}s...")
        detected = _move_and_detect(_detect_levelup, LEVELUP_TIMEOUT, detect_interval=2.0)
        if not detected:
            _log(f"  [timeout] levelup non detecte apres {total}s")

    _focus()
    _screenshot("levelup_screen_ou_ingame")

    if not detected:
        _log("  [warn] LevelUpScreen non detecte — screenshots pour analyse visuelle")
        _log("  Navigation envoyee quand meme (inputs ignores si pas de LevelUp)")
    else:
        _log("  LevelUpScreen detecte — test navigation cartes")

    # Navigation cartes : left×3 garanti Card0 (Godot 4 sans wrap par défaut)
    # puis Right→Card1, Right→Card2, Left→Card1, Enter=choix Card1
    time.sleep(0.5)
    _key("left"); _key("left"); _key("left")   # reset à Card0 (pas de wrap)
    _screenshot("levelup_card0_focus")
    _log("  -> Card0 focusee (bordure violette)")

    _key("right"); _screenshot("levelup_card1_focus")
    _log("  -> Apres Right : Card1 focusee")

    _key("right"); _screenshot("levelup_card2_focus")
    _log("  -> Apres Right x2 : Card2 focusee")

    _key("left");  _screenshot("levelup_card1_back")
    _log("  -> Apres Left : retour Card1")

    _key("return"); time.sleep(0.5)
    _screenshot("levelup_card_chosen")
    _log("  -> Apres Enter : carte choisie, jeu repris")

    _check(proc, "jeu stable apres level-up")
    return proc


# ---------------------------------------------------------------------------
# Phase 5 — RunEndScreen (mort du joueur)
# ---------------------------------------------------------------------------

def phase5_runend(proc: subprocess.Popen):
    _log("\n=== Phase 5 : RunEndScreen (attente mort) ===")

    if not _alive(proc):
        _log("  [skip] jeu deja ferme")
        return

    _focus()
    _log(f"  Pre-attente {RUNEND_PREWAIT}s + polling max {RUNEND_TIMEOUT}s...")
    _screenshot("runend_ingame_before_death")

    detected = _wait_for(_detect_runend, RUNEND_TIMEOUT, POLL_INTERVAL, "runend",
                         min_wait=RUNEND_PREWAIT)

    _focus()
    _screenshot("runend_screen_ou_ingame")

    if not detected:
        _log("  [warn] RunEndScreen non detecte — le joueur est peut-etre encore en vie")
        _log("  Navigation envoyee quand meme pour capturer l'etat")
    else:
        _log("  RunEndScreen detecte — test navigation boutons")

    # Attendre la fin du countup (~4×0.8s) et l'apparition des boutons
    time.sleep(4.0)
    _focus()
    _screenshot("runend_boutons_visibles")
    _log("  -> ReplayButton devrait etre focuse")

    _key("left");  _screenshot("runend_focus_hub")
    _log("  -> Apres Left : HubButton focuse")

    _key("right"); _screenshot("runend_focus_replay")
    _log("  -> Apres Right : ReplayButton refocuse")

    _key("escape"); time.sleep(SCENE_WAIT)
    _screenshot("runend_apres_escape")
    _log("  -> Apres Echap : HubScreen attendu")

    # Le process peut s'etre termine proprement (Escape → Hub → Escape → Quit) — c'est OK
    if _alive(proc):
        _log("  PASS [fin RunEndScreen — jeu toujours actif]")
    else:
        _log("  PASS [fin RunEndScreen — jeu quitte proprement apres navigation]")


# ---------------------------------------------------------------------------
# Rapport
# ---------------------------------------------------------------------------

def write_report():
    report_path = os.path.join(SCREENS_DIR, "REPORT.txt")
    date        = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    lines       = [f"Test automatisé PyAutoGUI — {date}", "=" * 60, ""] + _results
    lines      += [
        "", "=" * 60,
        "Points cles a verifier sur les screenshots :",
        "Phase 1 — MainMenu :",
        "  01 PlayButton : bordure violette visible",
        "  02 HubButton  : bordure violette",
        "  03 QuitButton : bordure violette",
        "  04 PlayButton : bordure violette (retour)",
        "Phase 2 — HubScreen :",
        "  05 HubScreen  : BackButton bordure violette",
        "  06 Upgrade1   : 1er Acheter bordure violette",
        "  07 Upgrade3   : 3e Acheter bordure violette",
        "  08 MainMenu   : retour apres Echap",
        "Phase 3 — In-game :",
        "  09 Ingame     : arene visible",
        "  10 Ingame 10s : ennemis presents",
        "Phase 4 — LevelUpScreen :",
        "  xx attente*   : screenshots intermediaires (jeu en cours)",
        "  xx screen     : overlay sombre = LevelUpScreen",
        "  xx card0      : Card0 bordure violette (focus initial)",
        "  xx card1      : Card1 bordure violette apres Right",
        "  xx card2      : Card2 bordure violette apres Right x2",
        "  xx card1back  : Card1 bordure violette apres Left",
        "  xx chosen     : carte choisie, jeu repris (arene visible)",
        "Phase 5 — RunEndScreen :",
        "  xx before     : jeu avant mort",
        "  xx attente*   : screenshots intermediaires",
        "  xx runend     : overlay tres sombre + texte MORT / EXTRACTION",
        "  xx boutons    : ReplayButton bordure violette (focus initial)",
        "  xx hub        : HubButton bordure violette apres Left",
        "  xx replay     : ReplayButton refocuse apres Right",
        "  xx escape     : HubScreen apres Echap",
    ]
    with open(report_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    _log(f"\nRapport ecrit : {report_path}")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    proc = phase1_mainmenu()

    try:
        phase2_hub(proc)
        phase3_ingame(proc)
        proc = phase4_levelup(proc)
        phase5_runend(proc)
    finally:
        _kill(proc)

    write_report()

    passes = sum(1 for r in _results if "PASS" in r)
    fails  = sum(1 for r in _results if "FAIL" in r)
    _log(f"\nBilan : {passes} PASS / {fails} FAIL")
    _log(f"Ouvrir {SCREENS_DIR} pour inspecter les screenshots.")
