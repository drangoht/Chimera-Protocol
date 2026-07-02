"""Capture de fenetre fiable via PrintWindow (contourne pyautogui.screenshot(region=...)
qui peut capturer une AUTRE fenetre si Godot n'est pas au premier plan / est occulte).

Utilise `user32.PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT)` : fonctionne meme si la
fenetre Godot est cachee derriere une autre application (ex. Steam plein ecran), tant
qu'elle n'est pas minimisee. Voir docs/TEST_REPORT.md (note game-tester 2026-07-02).

API :
    find_window(title_substr) -> hwnd | None
    capture_window(hwnd, client_only=True) -> PIL.Image.Image

`client_only=True` recadre sur la zone client uniquement (sans barre de titre/bordures) --
c'est ce qui donne des captures exactement 1280x720 pour un viewport Godot 1280x720.
`client_only=False` retourne la fenetre complete (barre de titre + bordures incluses),
comme les anciennes captures menu/charsel/etc via pyautogui.screenshot(region=...).
"""
import ctypes
import time

import win32gui
import win32ui
from PIL import Image

PW_RENDERFULLCONTENT = 2

# Force ce process Python en "per monitor v2 DPI aware" pour que les coordonnees
# GetWindowRect/GetClientRect correspondent aux pixels physiques de Godot (aussi DPI aware),
# et non a des coordonnees virtualisees par le scaling DPI de Windows.
try:
    ctypes.windll.user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
except Exception:
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(2)
    except Exception:
        try:
            ctypes.windll.user32.SetProcessDPIAware()
        except Exception:
            pass


def find_window(title_substr, min_width=400):
    """Retourne le hwnd de la premiere fenetre visible dont le titre contient
    `title_substr`, avec une largeur > min_width (evite d'attraper des popups/tooltips)."""
    matches = []

    def _enum(hwnd, _):
        if not win32gui.IsWindowVisible(hwnd):
            return
        title = win32gui.GetWindowText(hwnd)
        if title_substr in title:
            l, t, r, b = win32gui.GetWindowRect(hwnd)
            if (r - l) > min_width:
                matches.append(hwnd)

    win32gui.EnumWindows(_enum, None)
    return matches[0] if matches else None


def wait_for_window(title_substr, timeout=20.0, min_width=400):
    """Attend jusqu'a `timeout` s qu'une fenetre correspondante apparaisse."""
    t0 = time.time()
    while time.time() - t0 < timeout:
        hwnd = find_window(title_substr, min_width=min_width)
        if hwnd:
            return hwnd
        time.sleep(0.3)
    return None


def capture_window(hwnd, client_only=True):
    """Capture le contenu de `hwnd` via PrintWindow, meme occulte/pas au premier plan.

    Renvoie une image PIL RGB. Si `client_only`, recadre sur la zone client (exclut
    barre de titre + bordures) ; sinon renvoie la fenetre complete."""
    win_l, win_t, win_r, win_b = win32gui.GetWindowRect(hwnd)
    w, h = win_r - win_l, win_b - win_t
    if w <= 0 or h <= 0:
        raise RuntimeError(f"Fenetre hwnd={hwnd} a une taille invalide ({w}x{h})")

    hwnd_dc = win32gui.GetWindowDC(hwnd)
    mfc_dc = win32ui.CreateDCFromHandle(hwnd_dc)
    save_dc = mfc_dc.CreateCompatibleDC()
    save_bitmap = win32ui.CreateBitmap()
    save_bitmap.CreateCompatibleBitmap(mfc_dc, w, h)
    save_dc.SelectObject(save_bitmap)

    ok = ctypes.windll.user32.PrintWindow(hwnd, save_dc.GetSafeHdc(), PW_RENDERFULLCONTENT)

    bmpinfo = save_bitmap.GetInfo()
    bmpstr = save_bitmap.GetBitmapBits(True)
    img = Image.frombuffer(
        "RGB", (bmpinfo["bmWidth"], bmpinfo["bmHeight"]), bmpstr, "raw", "BGRX", 0, 1
    )

    win32gui.DeleteObject(save_bitmap.GetHandle())
    save_dc.DeleteDC()
    mfc_dc.DeleteDC()
    win32gui.ReleaseDC(hwnd, hwnd_dc)

    if not ok:
        raise RuntimeError("PrintWindow a echoue (renvoye 0)")

    if not client_only:
        return img

    cl, ct, cr, cb = win32gui.GetClientRect(hwnd)  # coords relatives (0,0)-(w,h) client
    client_w, client_h = cr - cl, cb - ct
    origin_x, origin_y = win32gui.ClientToScreen(hwnd, (0, 0))
    crop_l = origin_x - win_l
    crop_t = origin_y - win_t
    crop_r = crop_l + client_w
    crop_b = crop_t + client_h
    return img.crop((crop_l, crop_t, crop_r, crop_b))
