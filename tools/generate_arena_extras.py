import os, math, random
from PIL import Image, ImageDraw

def aether_bright(alpha): return (0x00, 0xE5, 0xFF, alpha)
def aether_abys(alpha): return (0x00, 0xA0, 0xBB, alpha)
def aether_pale(alpha): return (0xAA, 0xFF, 0xFF, alpha)

C_RUST_POOL   = (0x3A, 0x3A, 0x28, 200)
C_RUST_BORDER = (0x4A, 0x4A, 0x35, 160)
C_RUST_BUBBLE = (0x5A, 0x5A, 0x42, 255)
C_METAL_DARK  = (0x2A, 0x2A, 0x32, 255)
C_METAL_MID   = (0x4A, 0x4A, 0x52, 255)
C_METAL_HI    = (0x6A, 0x6A, 0x72, 255)
C_SCREEN_OFF  = (0x1A, 0x1A, 0x22, 255)
C_BOLT        = (0x2A, 0x2A, 0x32, 255)
C_TRANSPARENT = (0, 0, 0, 0)

def new_canvas(w=32, h=32):
    return Image.new("RGBA", (w, h), C_TRANSPARENT)

def px(img, x, y, color):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), color)

def rfill(img, x0, y0, x1, y1, color):
    ImageDraw.Draw(img).rectangle([x0, y0, x1, y1], fill=color)

def save_png(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, "PNG")
    print("  [OK] {}  ({}x{})".format(path, img.width, img.height))

def draw_geyser_base(img):
    cx, by, r = 16, 26, 10
    for dy in range(-r, r+1):
        for dx in range(-r, r+1):
            if dx*dx+dy*dy <= r*r:
                d = math.sqrt(dx*dx+dy*dy)
                a = min(180, max(60, int(180*(1.0-d/(r+1))+80)))
                px(img, cx+dx, by+dy, aether_abys(a))

def scatter(img, cx, y0, y1, spread, cfn, dens=0.25, seed=0):
    rng = random.Random(seed)
    for y in range(y0, y1+1):
        for dx in range(-spread, spread+1):
            if abs(dx) < 2: continue
            if rng.random() < dens:
                df = 1.0 - abs(dx)/(spread+1)
                hf = max(0.1, (y1-y)/max(1, y1-y0))
                a = int(110*df*hf)
                if a > 18: px(img, cx+dx, y, cfn(a))

def geyser_frame(jh, ja, sc, pale_top, pale_h, seed):
    img = new_canvas()
    draw_geyser_base(img)
    cx, by = 16, 26
    jtop = by - jh
    for y in range(jtop, by+1):
        hr = (by-y)/max(1,jh)
        a = min(255, max(55, int(ja*(0.55+0.45*(1.0-hr)))))
        for dx in range(-1,3): px(img, cx+dx, y, aether_bright(a))
    scatter(img, cx+1, jtop, by-2, 4, sc, 0.25, seed)
    if pale_top and pale_h > 0:
        rng = random.Random(seed+100)
        for y in range(jtop, jtop+pale_h):
            for dx in range(-2,4):
                if rng.random() < 0.40:
                    fr = (y-jtop)/max(1,pale_h)
                    a = int(140*(1.0-fr))
                    if a > 20: px(img, cx+dx, y, aether_pale(a))
    return img

def generate_geysers(od):
    save_png(geyser_frame(12,200,aether_abys,False,0,1), os.path.join(od,"tile_aether_geyser_01.png"))
    save_png(geyser_frame(22,220,aether_bright,True,5,2), os.path.join(od,"tile_aether_geyser_02.png"))
    img3 = new_canvas()
    draw_geyser_base(img3)
    cx, by, jh = 16, 26, 16
    jtop = by - jh
    for y in range(jtop, by+1):
        hr = (by-y)/max(1,jh)
        a = min(220, max(45, int(180*(0.50+0.50*(1.0-hr)))))
        for dx in range(-1,3): px(img3, cx+dx, y, aether_abys(a))
    scatter(img3, cx+1, jtop, by-2, 6, aether_abys, 0.30, 3)
    save_png(img3, os.path.join(od,"tile_aether_geyser_03.png"))

def draw_rust_pool(bubbles):
    img = new_canvas()
    cx, cy, rx, ry = 16, 16, 8, 7
    for dy in range(-ry-1, ry+2):
        for dx in range(-rx-1, rx+2):
            if (dx/rx)**2+(dy/ry)**2 <= 1.0:
                px(img, cx+dx, cy+dy, C_RUST_POOL)
    for dy in range(-ry-2, ry+3):
        for dx in range(-rx-2, rx+3):
            ins = (dx/rx)**2+(dy/ry)**2
            bns = (dx/(rx+1))**2+(dy/(ry+1))**2
            if ins > 1.0 and bns <= 1.0:
                px(img, cx+dx, cy+dy, C_RUST_BORDER)
    rng = random.Random(42)
    for dy in range(-ry+1, ry):
        for dx in range(-rx+2, rx-1):
            if (dx/(rx-1))**2+(dy/(ry-1))**2 <= 1.0 and rng.random() < 0.08:
                px(img, cx+dx, cy+dy, (0x45,0x45,0x32,180))
    for bx, by in bubbles:
        px(img, bx, by, C_RUST_BUBBLE)
    return img

def generate_rust_pools(od):
    save_png(draw_rust_pool([(14,16),(18,14),(12,18)]), os.path.join(od,"tile_rust_pool_01.png"))
    save_png(draw_rust_pool([(15,15),(19,15),(11,17)]), os.path.join(od,"tile_rust_pool_02.png"))

def generate_tech_pillar(od):
    img = new_canvas(32, 64)
    p0, p1, py0, py1 = 6, 25, 2, 61
    rfill(img, p0, py0, p1, py1, C_METAL_MID)
    rfill(img, p0, py0, p0+1, py1, C_METAL_DARK)
    for y in range(py0, py1+1):
        px(img, p0, y, C_METAL_HI)
        px(img, p1, y, C_METAL_HI)
    for y in range(py0+2, py1-1):
        if y % 4 == 0:
            for x in range(p0+3, p1-1, 8):
                px(img, x, y, C_METAL_DARK)
    sw, sh = 12, 8
    sx0 = 16 - sw//2; sx1 = sx0+sw-1; sy0, sy1 = 6, 6+sh-1
    rfill(img, sx0, sy0, sx1, sy1, C_SCREEN_OFF)
    ImageDraw.Draw(img).rectangle([sx0-1,sy0-1,sx1+1,sy1+1], outline=C_METAL_DARK)
    for bx in [p0+2, p1-2]:
        for by in range(py0+4, py1-2, 8):
            px(img, bx, by, C_BOLT)
            px(img, bx-1, by, (0x3A,0x3A,0x42,160))
            px(img, bx+1, by, (0x3A,0x3A,0x42,160))
    for ry in [20, 44]:
        for x in range(p0+2, p1-1, 4):
            px(img, x, ry, C_BOLT)
    cables = [(p0+3,55,5,0),(p0+7,56,4,1),(p1-3,55,6,-1),(p1-7,57,3,0)]
    for cx, cys, ln, dv in cables:
        for i in range(ln):
            cx2 = cx+(dv if i > 2 else 0); cy2 = cys+i
            if 0 <= cy2 < 64:
                px(img, cx2, cy2, (0x4A,0x2A,0x15, 140 if i==ln-1 else 255))
    for x in range(p0+2, p1-1):
        e = img.getpixel((x,32))
        if e[3] > 0:
            px(img, x, 32, (max(0,e[0]-8),max(0,e[1]-8),max(0,e[2]-8),e[3]))
    save_png(img, os.path.join(od,"tile_tech_pillar.png"))

def main():
    sd = os.path.dirname(os.path.abspath(__file__))
    od = os.path.join(os.path.dirname(sd), "assets", "sprites", "tileset")
    print("=== generate_arena_extras.py ===")
    print("Destination: " + od)
    print()
    print("--- Geysers d Aether (3 frames) ---")
    generate_geysers(od)
    print()
    print("--- Flaques de Rouille Vivante (2 frames) ---")
    generate_rust_pools(od)
    print()
    print("--- Pilier de technologie morte (32x64) ---")
    generate_tech_pillar(od)
    print()
    print("=== Termine. 6 fichiers generes. ===")

if __name__ == "__main__":
    main()
