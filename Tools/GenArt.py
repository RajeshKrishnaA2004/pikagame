"""GenArt.py -- deterministic procedural art generator for the PikaGame remake.

Stdlib only (no Pillow, no network). Produces:

  Assets/Art/Tiles/terrain_tileset.png     10 seamlessly tiling terrain tiles
  Assets/Art/Characters/player_walk.png    4 directions x 4 walk frames
  Assets/Art/Characters/player_idle.png    4 idle frames
  Assets/Art/Characters/professor.png      NPC portrait sprite
  Assets/Art/Creatures/<name>.png          12 creatures + evolved forms
  Assets/Art/UI/minimap_mask.png           circular minimap mask
  Assets/Art/UI/minimap_bezel.png          circular bezel + compass ring
  Assets/Art/UI/hud_panel.png              9-slice HUD panel
  Assets/Art/World/props.png               trees / rocks / signs / gate markers
  Tools/out/terrain_map.json               terrain grid derived from map.png

Usage:
    python Tools/GenArt.py              # everything
    python Tools/GenArt.py --only tiles # one group
    python Tools/GenArt.py --tile 256   # smaller tiles (faster preview)
"""

import json
import math
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import artlib                                                        # noqa: E402
from artlib import (Canvas, NoiseField, clamp, hsv, mix, over,        # noqa: E402
                    rgba, shade)

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(PROJ, "Assets", "Art")
OUT = os.path.join(PROJ, "Tools", "out")

# Texture pixels per tile, and pixels-per-unit.  A tile is always 2 world
# units, so the effective PPU is TILE/2 -- this is what keeps the map crisp
# across the whole zoom band (see README-REMAKE.md for the math).
TILE = 512
PPU = TILE // 2

# --------------------------------------------------------------------------- #
#  Palette -- sampled directly from the original painted map.png so the new
#  tiles sit next to the existing art without a colour clash.
# --------------------------------------------------------------------------- #

PAL = {
    "grass_a": (58, 146, 54), "grass_b": (36, 112, 38),
    "grass_dark": (24, 86, 28), "grass_hi": (86, 176, 74),
    "tall_a": (34, 104, 40), "tall_b": (18, 72, 30), "tall_hi": (72, 150, 66),
    "path_a": (255, 221, 119), "path_b": (231, 193, 96),
    "path_dot": (208, 168, 74),
    "sand_a": (255, 234, 168), "sand_b": (240, 208, 124),
    "water_deep": (0, 68, 140), "water_a": (0, 136, 255), "water_b": (0, 153, 255),
    "foam": (186, 228, 255),
    "forest_a": (0, 85, 51), "forest_b": (0, 68, 34), "forest_hi": (30, 124, 66),
    "trunk": (74, 52, 32), "trunk_hi": (112, 80, 50),
    "rock_a": (136, 85, 51), "rock_b": (102, 68, 51), "rock_hi": (178, 126, 80),
    "wall_a": (116, 108, 100), "wall_b": (76, 70, 64), "wall_hi": (156, 148, 138),
    "floor_a": (208, 192, 166), "floor_b": (176, 158, 132),
    "plank_a": (168, 116, 66), "plank_b": (124, 82, 46),
    "ink": (30, 24, 22),
    "gate_locked": (206, 78, 62), "gate_open": (86, 190, 120),
}

# --- terrain codes --------------------------------------------------------- #
T_GRASS, T_TALL, T_PATH, T_SAND, T_WATER = 0, 1, 2, 3, 4
T_FOREST, T_ROCK, T_WALL, T_FLOOR, T_BRIDGE = 5, 6, 7, 8, 9

TERRAIN_NAMES = ["Grass", "TallGrass", "Path", "Sand", "Water",
                 "Forest", "Rock", "Wall", "Floor", "Bridge"]
WALKABLE = {T_GRASS, T_TALL, T_PATH, T_SAND, T_FLOOR, T_BRIDGE}


# --------------------------------------------------------------------------- #
#  Cached noise: one grid per seed, sampled by index (fast + still seamless)
# --------------------------------------------------------------------------- #

_NOISE_CACHE = {}


def noise_grid(size, seed, octaves=3):
    key = (size, seed, octaves)
    if key not in _NOISE_CACHE:
        nf = NoiseField(seed, octaves=octaves)
        inv = 1.0 / size
        g = [0] * (size * size)
        i = 0
        for y in range(size):
            v = y * inv
            for _x in range(size):
                g[i] = int(nf.at(_x * inv, v) * 255.999)
                i += 1
        _NOISE_CACHE[key] = g
    return _NOISE_CACHE[key]


def ramp(c0, c1):
    """256-entry RGB lookup table lerping c0 -> c1."""
    return [mix(c0, c1, i / 255.0) for i in range(256)]


def base_pass(canvas, lut, grid, ox=0, oy=0):
    """Fill the whole tile from a noise grid through a colour ramp."""
    T = canvas.w
    px = canvas.px
    for y in range(T):
        srow = ((y + oy) % T) * T
        drow = y * T * 4
        for x in range(T):
            c = lut[grid[srow + ((x + ox) % T)]]
            o = drow + x * 4
            px[o] = c[0]
            px[o + 1] = c[1]
            px[o + 2] = c[2]
            px[o + 3] = 255


def wrap_draw(fn, T):
    """Call fn(dx,dy) for the 9 wrapped positions so shapes cross tile edges."""
    for dy in (-T, 0, T):
        for dx in (-T, 0, T):
            fn(dx, dy)


# --------------------------------------------------------------------------- #
#  Terrain tile painters
# --------------------------------------------------------------------------- #

def scatter(T, count, seed, margin=0.0):
    """Deterministic pseudo-random points inside a tile."""
    pts = []
    span = T - 2.0 * margin
    for i in range(count):
        x = margin + artlib._hash2(i, 1, seed) * span
        y = margin + artlib._hash2(i, 2, seed) * span
        pts.append((x, y))
    return pts


def blend_pass(canvas, grid, colour, lo, hi, alpha=255, soft=12):
    """Blend `colour` where grid is within [lo,hi], with soft edges."""
    T = canvas.w
    col = rgba(colour, alpha)
    for y in range(T):
        srow = y * T
        drow = y * T * 4
        for x in range(T):
            v = grid[srow + x]
            if v < lo - soft or v > hi + soft:
                continue
            if lo <= v <= hi:
                k = 1.0
            else:
                k = clamp((soft - abs(v - (lo if v < lo else hi))) / float(soft))
            if k <= 0.0:
                continue
            o = drow + x * 4
            if k >= 1.0:
                canvas.px[o:o + 4] = bytes(col)
            else:
                cur = (canvas.px[o], canvas.px[o + 1], canvas.px[o + 2], 255)
                canvas.px[o:o + 4] = bytes(
                    over(cur, (col[0], col[1], col[2], int(col[3] * k))))


def tile_grass(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["grass_b"], PAL["grass_a"]), noise_grid(T, seed))
    blend_pass(c, noise_grid(T, seed + 311), PAL["grass_dark"], 0, 92, 200, 14)
    blend_pass(c, noise_grid(T, seed + 617), PAL["grass_hi"], 176, 255, 150, 14)
    # blade tufts
    for i, (x, y) in enumerate(scatter(T, 190, seed + 5)):
        h = 7.0 + artlib._hash2(i, 7, seed) * 9.0
        lean = (artlib._hash2(i, 8, seed) - 0.5) * 6.0
        col = PAL["grass_hi"] if artlib._hash2(i, 9, seed) > 0.5 else PAL["grass_dark"]
        wrap_draw(lambda dx, dy, x=x, y=y, h=h, lean=lean, col=col:
                  c.line(x + dx, y + dy, x + dx + lean, y + dy - h, 2.4, col), T)
    return c


def tile_tallgrass(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["tall_b"], PAL["tall_a"]), noise_grid(T, seed + 41))
    blend_pass(c, noise_grid(T, seed + 733), PAL["grass_dark"], 0, 96, 210, 14)
    # dense clumps of pointed blades
    for i, (x, y) in enumerate(scatter(T, 34, seed + 11)):
        n = int(14 + artlib._hash2(i, 3, seed) * 10)
        for b in range(n):
            ax = x + (artlib._hash2(i * 31 + b, 4, seed) - 0.5) * 34.0
            ay = y + (artlib._hash2(i * 31 + b, 5, seed) - 0.5) * 30.0
            hh = 20.0 + artlib._hash2(i * 31 + b, 6, seed) * 22.0
            ln = (artlib._hash2(i * 31 + b, 12, seed) - 0.5) * 9.0
            col = PAL["tall_hi"] if artlib._hash2(b, i, seed) > 0.45 else PAL["tall_b"]
            wrap_draw(lambda dx, dy, ax=ax, ay=ay, hh=hh, ln=ln, col=col:
                      c.line(ax + dx, ay + dy, ax + dx + ln, ay + dy - hh, 3.0, col), T)
    return c


def tile_path(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["path_b"], PAL["path_a"]), noise_grid(T, seed + 97))
    blend_pass(c, noise_grid(T, seed + 151), PAL["path_dot"], 0, 88, 120, 16)
    # pebbles + worn track marks
    for i, (x, y) in enumerate(scatter(T, 46, seed + 23)):
        r = 2.0 + artlib._hash2(i, 5, seed) * 3.4
        col = PAL["path_dot"] if i % 3 else shade(PAL["path_dot"], 0.82)
        wrap_draw(lambda dx, dy, x=x, y=y, r=r, col=col: c.circle(x + dx, y + dy, r, col), T)
    for i, (x, y) in enumerate(scatter(T, 22, seed + 29)):
        w = 10.0 + artlib._hash2(i, 2, seed) * 26.0
        wrap_draw(lambda dx, dy, x=x, y=y, w=w:
                  c.ellipse(x + dx, y + dy, w, 3.0 + artlib._hash2(0, int(w), seed) * 2.5,
                            shade(PAL["path_b"], 0.93)), T)
    return c


def tile_sand(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["sand_b"], PAL["sand_a"]), noise_grid(T, seed + 191))
    blend_pass(c, noise_grid(T, seed + 233), PAL["sand_b"], 0, 92, 130, 16)
    # wind ripples
    for i, (x, y) in enumerate(scatter(T, 18, seed + 37)):
        for k in range(3):
            yy = y + k * 9.0
            w = 26.0 + artlib._hash2(i, k, seed) * 40.0
            wrap_draw(lambda dx, dy, x=x, yy=yy, w=w:
                      c.ellipse(x + dx, yy + dy, w, 2.6, shade(PAL["sand_b"], 0.93)), T)
    return c


def tile_water(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["water_deep"], PAL["water_b"]), noise_grid(T, seed + 53))
    blend_pass(c, noise_grid(T, seed + 401), PAL["water_deep"], 0, 96, 190, 18)
    # rolling ripple bands
    for i, (x, y) in enumerate(scatter(T, 40, seed + 59)):
        w = 20.0 + artlib._hash2(i, 4, seed) * 46.0
        h = 1.8 + artlib._hash2(i, 6, seed) * 2.2
        a = 40 + int(artlib._hash2(i, 8, seed) * 70)
        wrap_draw(lambda dx, dy, x=x, y=y, w=w, h=h, a=a:
                  c.ellipse(x + dx, y + dy, w, h, rgba(PAL["foam"], a)), T)
    # sparse specular glints keep the surface from looking flat
    for i, (x, y) in enumerate(scatter(T, 12, seed + 67)):
        r = 2.2 + artlib._hash2(i, 3, seed) * 3.2
        wrap_draw(lambda dx, dy, x=x, y=y, r=r:
                  c.ellipse(x + dx, y + dy, r * 2.2, r, rgba(PAL["foam"], 120)), T)
    return c


def tile_forest(T, seed):
    c = Canvas(T, T)
    # dark undergrowth so seams between forest tiles never show
    base_pass(c, ramp(PAL["forest_b"], PAL["forest_a"]), noise_grid(T, seed + 71))
    blend_pass(c, noise_grid(T, seed + 811), PAL["forest_b"], 0, 90, 200, 16)
    # canopy: overlapping blobs, wrapped so the tile joins seamlessly
    for i, (x, y) in enumerate(scatter(T, 30, seed + 73, margin=-30)):
        r = 26.0 + artlib._hash2(i, 5, seed) * 26.0
        tone = [PAL["forest_a"], PAL["forest_hi"], PAL["forest_b"]][i % 3]
        wrap_draw(lambda dx, dy, x=x, y=y, r=r, tone=tone:
                  c.circle(x + dx, y + dy + r * 0.25, r, shade(tone, 0.72)), T)
        wrap_draw(lambda dx, dy, x=x, y=y, r=r, tone=tone:
                  c.circle(x + dx, y + dy, r, tone), T)
        wrap_draw(lambda dx, dy, x=x, y=y, r=r, tone=tone:
                  c.ellipse(x + dx - r * 0.28, y + dy - r * 0.34, r * 0.46, r * 0.32,
                            shade(tone, 1.22)), T)
    # a couple of trunk glimpses
    for i, (x, y) in enumerate(scatter(T, 5, seed + 79)):
        wrap_draw(lambda dx, dy, x=x, y=y:
                  c.rrect(x + dx - 5, y + dy - 30, x + dx + 5, y + dy + 22, 3,
                          PAL["trunk"]), T)
    return c


def tile_rock(T, seed):
    c = Canvas(T, T)
    base_pass(c, ramp(PAL["rock_b"], PAL["rock_a"]), noise_grid(T, seed + 83))
    blend_pass(c, noise_grid(T, seed + 887), PAL["rock_b"], 0, 94, 170, 16)
    for i, (x, y) in enumerate(scatter(T, 14, seed + 89, margin=-40)):
        s = 34.0 + artlib._hash2(i, 7, seed) * 46.0
        rot = artlib._hash2(i, 8, seed) * math.tau
        pts = []
        for k in range(6):
            a = rot + k * math.tau / 6.0
            rr = s * (0.72 + 0.34 * artlib._hash2(i * 7 + k, 9, seed))
            pts.append((x + math.cos(a) * rr, y + math.sin(a) * rr))
        wrap_draw(lambda dx, dy, pts=pts:
                  c.poly([(px + dx, py + dy) for px, py in pts],
                         shade(PAL["rock_b"], 0.7)), T)
        wrap_draw(lambda dx, dy, pts=pts, s=s:
                  c.poly([(px + dx, py + dy - s * 0.16) for px, py in pts],
                         PAL["rock_a"]), T)
        top = [pts[5], pts[0], pts[1]]
        wrap_draw(lambda dx, dy, top=top, s=s:
                  c.poly([(px + dx, py + dy - s * 0.30) for px, py in top],
                         PAL["rock_hi"]), T)
    return c


def tile_wall(T, seed):
    c = Canvas(T, T)
    c.rect(0, 0, T, T, PAL["wall_b"])
    rows = 5
    bh = T / float(rows)
    cols = 4
    bw = T / float(cols)
    for r in range(rows):
        y0 = r * bh
        offs = (bw * 0.5) if r % 2 else 0.0
        for k in range(-1, cols + 1):
            x0 = k * bw + offs
            tone = mix(PAL["wall_b"], PAL["wall_a"],
                       artlib._hash2(r, k, seed + 97))
            c.rrect(x0 + 3, y0 + 3, x0 + bw - 3, y0 + bh - 3, 5, tone)
            c.rrect(x0 + 3, y0 + 3, x0 + bw - 3, y0 + bh * 0.4, 5,
                    shade(tone, 1.18))
            c.rrect(x0 + 3, y0 + bh - 10, x0 + bw - 3, y0 + bh - 3, 4,
                    shade(tone, 0.74))
    return c


def tile_floor(T, seed):
    c = Canvas(T, T)
    c.rect(0, 0, T, T, shade(PAL["floor_b"], 0.86))
    n = 4
    s = T / float(n)
    for r in range(n):
        for k in range(n):
            tone = mix(PAL["floor_b"], PAL["floor_a"],
                       artlib._hash2(r, k, seed + 131))
            c.rrect(k * s + 2, r * s + 2, (k + 1) * s - 2, (r + 1) * s - 2, 4, tone)
            c.ellipse(k * s + s * 0.32, r * s + s * 0.3, s * 0.3, s * 0.16,
                      shade(tone, 1.08))
    return c


def tile_bridge(T, seed):
    c = tile_water(T, seed + 500)
    c.rect(0, T * 0.14, T, T * 0.86, PAL["plank_b"])
    planks = 7
    ph = (T * 0.72) / planks
    for k in range(planks):
        y0 = T * 0.14 + k * ph
        tone = mix(PAL["plank_b"], PAL["plank_a"],
                   0.35 + 0.65 * artlib._hash2(k, 3, seed + 149))
        c.rrect(0, y0 + 2, T, y0 + ph - 2, 3, tone)
        c.rect(0, y0 + 2, T, y0 + 5, shade(tone, 1.2))
    nail = shade(PAL["plank_b"], 0.6)
    for k in range(planks):
        yc = T * 0.14 + (k + 0.5) * ph
        for side in (0.06, 0.5, 0.94):
            wrap_draw(lambda dx, dy, yc=yc, side=side:
                      c.circle(dx + T * side, dy + yc, 3.2, nail), T)
    c.rect(0, T * 0.14, T, T * 0.176, shade(PAL["ink"], 0.85))
    c.rect(0, T * 0.844, T, T * 0.86, shade(PAL["ink"], 0.85))
    return c


TILE_PAINTERS = [tile_grass, tile_tallgrass, tile_path, tile_sand, tile_water,
                 tile_forest, tile_rock, tile_wall, tile_floor, tile_bridge]


def build_terrain_tileset(tile=TILE, cols=5, seed=1337):
    """Return (atlas Canvas, tile rects, rows). Tile index order matches the
    terrain codes, laid out left-to-right, top-to-bottom in Unity sprite
    order (row 0 = the *last* painted row, because Unity enumerates sprite
    rects bottom-up)."""
    rows = (len(TILE_PAINTERS) + cols - 1) // cols
    sheet = Canvas(cols * tile, rows * tile)
    rects = []
    for idx, painter in enumerate(TILE_PAINTERS):
        t0 = time.time()
        sub = painter(tile, seed + idx * 4099)
        col = idx % cols
        row = idx // cols
        x0 = col * tile
        y0 = (rows - 1 - row) * tile
        sheet.blit(sub, x0, y0)
        rects.append({"index": idx, "name": TERRAIN_NAMES[idx],
                      "col": col, "row": row, "x": x0, "y": y0})
        print("    tile %-10s %dx%d  %.1fs" % (TERRAIN_NAMES[idx], tile, tile,
                                               time.time() - t0))
    return sheet, rects, rows


# --------------------------------------------------------------------------- #
#  Terrain classification: derive the world layout from the original map.png
# --------------------------------------------------------------------------- #

def classify_pixel(r, g, b):
    """Map one sampled colour to a terrain code using the sampled palette."""
    if b > 140 and b > r + 30 and b > g + 20:
        return T_WATER
    if r > 195 and g > 160 and b < 190 and (r - b) > 40:
        return T_PATH                      # warm pale sand / path
    if r < 70 and g > 45 and b < 90 and g >= b:
        return T_FOREST                    # dark green canopy
    if r > b + 34 and g < 150 and r < 195:
        return T_ROCK                      # brown rock / cliff
    if g >= r and g > 90:
        return T_GRASS
    if r > 150 and g > 130:
        return T_SAND
    return T_GRASS


def classify_map(path, cols=48, rows=32):
    w, h, px = artlib.read_png(path)
    bx = w // cols
    by = h // rows
    grid = artlib.block_mean(w, h, px, bx, by)
    out = []
    for r in range(rows):
        row = []
        for c in range(cols):
            mr, mg, mb = grid[r][c]
            code = classify_pixel(mr, mg, mb)
            row.append(code)
        out.append(row)
    return out, (w, h)


def ensure_connected(grid, prefer=(T_PATH, T_SAND)):
    """Flood-fill the walkable region so the player can never be walled in."""
    rows = len(grid)
    cols = len(grid[0])
    walk = [[grid[r][c] in WALKABLE for c in range(cols)] for r in range(rows)]

    seen = [[False] * cols for _ in range(rows)]
    best = []
    for sr in range(rows):
        for sc in range(cols):
            if not walk[sr][sc] or seen[sr][sc]:
                continue
            stack = [(sr, sc)]
            seen[sr][sc] = True
            comp = []
            while stack:
                r, c = stack.pop()
                comp.append((r, c))
                for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nr, nc = r + dr, c + dc
                    if (0 <= nr < rows and 0 <= nc < cols and walk[nr][nc]
                            and not seen[nr][nc]):
                        seen[nr][nc] = True
                        stack.append((nr, nc))
            if len(comp) > len(best):
                best = comp

    keep = set(best)
    converted = 0
    for r in range(rows):
        for c in range(cols):
            if walk[r][c] and (r, c) not in keep:
                grid[r][c] = T_GRASS
                converted += 1
    return grid, keep, converted


def spawn_point(grid, keep):
    """Pick the southernmost walkable tile on the main landmass as spawn."""
    best = None
    for (r, c) in keep:
        if grid[r][c] == T_GRASS:
            if best is None or r > best[0]:
                best = (r, c)
    if best is None and keep:
        best = max(keep)
    return best or (len(grid) - 1, len(grid[0]) // 2)


# --------------------------------------------------------------------------- #
#  Entry point
# --------------------------------------------------------------------------- #

def ensure_dirs():
    for d in (ART, os.path.join(ART, "Tiles"), os.path.join(ART, "Characters"),
              os.path.join(ART, "Creatures"), os.path.join(ART, "UI"),
              os.path.join(ART, "World"), OUT):
        if not os.path.isdir(d):
            os.makedirs(d)


def gen_tiles(tile):
    print("  terrain tileset (%dx%d px per tile, %.0f px/unit)" % (tile, tile, tile / 2.0))
    sheet, rects, rows = build_terrain_tileset(tile, cols=5)
    path = os.path.join(ART, "Tiles", "terrain_tileset.png")
    sheet.save(path)
    meta = {"tile_px": tile, "ppu": tile // 2, "cols": 5, "rows": rows,
            "tiles": rects, "ppu_note": "tile is 2 world units"}
    with open(os.path.join(OUT, "tileset.json"), "w") as fh:
        json.dump(meta, fh, indent=2)
    print("  -> %s (%dx%d)" % (path, sheet.w, sheet.h))
    return meta


def gen_terrain_map(cols, rows):
    src = os.path.join(PROJ, "Assets", "Sprites", "Environment", "map.png")
    print("  classifying %s into a %dx%d grid" % (os.path.basename(src), cols, rows))
    grid, (w, h) = classify_map(src, cols, rows)
    grid, keep, converted = ensure_connected(grid)
    sr, sc = spawn_point(grid, keep)
    counts = {}
    for r in range(rows):
        for c in range(cols):
            counts[TERRAIN_NAMES[grid[r][c]]] = \
                counts.get(TERRAIN_NAMES[grid[r][c]], 0) + 1
    data = {"source": "Assets/Sprites/Environment/map.png", "source_size": [w, h],
            "cols": cols, "rows": rows, "walkable": sorted(WALKABLE),
            "terrain_names": TERRAIN_NAMES,
            "spawn": {"col": sc, "row": sr},
            "unreachable_removed": converted,
            "counts": counts, "grid": grid}
    with open(os.path.join(OUT, "terrain_map.json"), "w") as fh:
        json.dump(data, fh, indent=1)

    # A flat CSV alongside the JSON: the Unity editor builder parses this with
    # plain string splitting, so there is no JSON dependency in C#.
    with open(os.path.join(OUT, "terrain_map.csv"), "w") as fh:
        fh.write("# cols=%d rows=%d spawn_col=%d spawn_row=%d\n" % (cols, rows, sc, sr))
        fh.write("# walkable=%s\n" % ",".join(str(t) for t in sorted(WALKABLE)))
        fh.write("# names=%s\n" % "|".join(TERRAIN_NAMES))
        for r in range(rows):
            fh.write(",".join(str(v) for v in grid[r]) + "\n")

    print("  -> %s  spawn=(col %d,row %d)  terrain: %s"
          % (os.path.join(OUT, "terrain_map.json"), sc, sr,
             ", ".join("%s=%d" % kv for kv in sorted(counts.items()))))
    return data


def main(argv):
    global TILE, PPU
    only = "all"
    tile = TILE
    for i, a in enumerate(argv):
        if a == "--only" and i + 1 < len(argv):
            only = argv[i + 1]
        if a == "--tile" and i + 1 < len(argv):
            tile = int(argv[i + 1])
    TILE = tile
    PPU = tile // 2

    t0 = time.time()
    ensure_dirs()
    groups = ["terrain", "tiles", "split", "chars", "creatures", "ui", "world"]
    want = groups if only == "all" else [only]

    print("GenArt: tile=%d ppu=%d" % (tile, PPU))
    if "terrain" in want:
        gen_terrain_map(48, 32)
    if "tiles" in want:
        gen_tiles(tile)
    if "split" in want:
        print("  split sprites (Sprite:Single -- no slicing)")
        gen_split_tiles(tile)
        gen_split_characters()
    if "chars" in want:
        print("  characters")
        gen_chars()
    if "creatures" in want:
        print("  creatures")
        gen_creatures()
    if "ui" in want:
        print("  ui")
        gen_ui()
    if "world" in want:
        print("  world props")
        gen_props()
    print("done in %.1fs" % (time.time() - t0))
    return 0


# --------------------------------------------------------------------------- #
#  Characters -- player (4 dir x 4 walk frames), idle, professor, NPCs
# --------------------------------------------------------------------------- #

CH_W, CH_H = 192, 240          # 192 wide at PPU 192 => 1.0 x 1.25 world units
CH_PPU = CH_W

DIR_DOWN, DIR_LEFT, DIR_RIGHT, DIR_UP = 0, 1, 2, 3
DIR_NAMES = ["Down", "Left", "Right", "Up"]

PLAYER_CFG = {
    "skin": (240, 198, 160), "skin_shade": (214, 168, 130),
    "hair": (58, 40, 30),
    "shirt": (72, 132, 214), "shirt_dark": (50, 98, 168),
    "pants": (54, 62, 92), "shoe": (44, 40, 44),
    "cap": (216, 74, 74), "cap_dark": (176, 52, 52),
    "ink": (30, 24, 22),
}

PROF_CFG = {
    "skin": (238, 196, 156), "skin_shade": (210, 164, 126),
    "hair": (196, 196, 200),
    "shirt": (238, 240, 244), "shirt_dark": (198, 202, 210),
    "pants": (86, 82, 96), "shoe": (52, 48, 52),
    "cap": (236, 238, 242), "cap_dark": (196, 200, 208),
    "ink": (30, 24, 22),
}


def npc_cfg(hue, hair=(58, 40, 30)):
    """Build an NPC palette from a shirt hue."""
    return {
        "skin": (238, 196, 156), "skin_shade": (212, 166, 128),
        "hair": hair,
        "shirt": hsv(hue, 0.55, 0.82), "shirt_dark": hsv(hue, 0.62, 0.62),
        "pants": (62, 66, 84), "shoe": (44, 40, 44),
        "cap": hsv((hue + 0.5) % 1.0, 0.5, 0.78),
        "cap_dark": hsv((hue + 0.5) % 1.0, 0.6, 0.6),
        "ink": (30, 24, 22),
    }


def draw_character(c, cfg, direction, phase, cx=None, ground=None, scale=1.0):
    """Draw one character frame. `phase` 0..3 drives the walk cycle."""
    cx = c.w * 0.5 if cx is None else cx
    ground = c.h * 0.965 if ground is None else ground
    k = scale
    skin = rgba(cfg["skin"])
    skin_s = rgba(cfg["skin_shade"])
    hair = rgba(cfg["hair"])
    shirt = rgba(cfg["shirt"])
    shirt_d = rgba(cfg["shirt_dark"])
    pants = rgba(cfg["pants"])
    shoe = rgba(cfg["shoe"])
    cap = rgba(cfg["cap"])
    cap_d = rgba(cfg["cap_dark"])
    ink = rgba(cfg["ink"])

    def y(v):
        return ground - v * k

    leg_top, torso_bot, torso_top = 40.0, 46.0, 96.0
    head_r = 30.0
    head_cy = torso_top + head_r * 0.86

    bob = -3.0 * k if phase in (1, 3) else 0.0
    swing = 1.0 if phase == 1 else (-1.0 if phase == 3 else 0.0)
    leg_fwd = 10.0 * swing

    # --- legs ------------------------------------------------------------ #
    for sgn in (-1, 1):
        off = leg_fwd if sgn > 0 else -leg_fwd
        lx = cx + sgn * 13.0 * k
        c.capsule(lx, y(leg_top), lx + off, y(4.0), 8.0 * k, pants)
        c.rrect(lx + off - 10.0 * k, y(9.0), lx + off + 10.0 * k, y(0.0),
                4.0 * k, shoe)

    # --- torso ----------------------------------------------------------- #
    c.rrect(cx - 27.0 * k, y(torso_top) + bob, cx + 27.0 * k,
            y(torso_bot) + bob, 10.0 * k, shirt)
    c.rrect(cx - 27.0 * k, y(torso_top) + bob, cx - 8.0 * k,
            y(torso_bot) + bob, 10.0 * k, shirt_d)

    # --- arms ------------------------------------------------------------ #
    for sgn in (-1, 1):
        ax = cx + sgn * 30.0 * k
        off = -leg_fwd * 0.8 if sgn > 0 else leg_fwd * 0.8
        c.capsule(ax, y(torso_top - 8.0) + bob, ax + off, y(torso_bot + 6.0) + bob,
                  7.5 * k, shirt_d)
        c.circle(ax + off, y(torso_bot + 4.0) + bob, 8.0 * k, skin)

    # --- head ------------------------------------------------------------ #
    hy = y(head_cy) + bob
    c.circle(cx, hy, head_r * k, skin)
    c.ellipse(cx, hy + head_r * 0.34 * k, head_r * 0.94 * k, head_r * 0.5 * k, skin_s)

    if direction == DIR_DOWN:
        c.poly([(cx - head_r * 1.04 * k, hy - head_r * 0.14 * k),
                (cx - head_r * 0.72 * k, hy - head_r * 1.12 * k),
                (cx + head_r * 0.72 * k, hy - head_r * 1.12 * k),
                (cx + head_r * 1.04 * k, hy - head_r * 0.14 * k)], cap)
        c.rrect(cx - head_r * 1.12 * k, hy - head_r * 0.32 * k,
                cx + head_r * 1.12 * k, hy + head_r * 0.04 * k, 4.0 * k, cap_d)
        for sgn in (-1, 1):
            ex = cx + sgn * head_r * 0.38 * k
            c.ellipse(ex, hy + head_r * 0.16 * k, 6.2 * k, 7.6 * k, ink)
            c.circle(ex - 2.0 * k, hy + head_r * 0.05 * k, 2.4 * k, (255, 255, 255))
            c.ellipse(ex - sgn * 5.0 * k, hy + head_r * 0.54 * k, 7.0 * k, 4.0 * k,
                      skin_s)
        c.capsule(cx - 3.2 * k, hy + head_r * 0.74 * k, cx + 3.2 * k,
                  hy + head_r * 0.74 * k, 2.4 * k, ink)
    elif direction == DIR_UP:
        c.poly([(cx - head_r * 1.04 * k, hy - head_r * 0.14 * k),
                (cx - head_r * 0.72 * k, hy - head_r * 1.12 * k),
                (cx + head_r * 0.72 * k, hy - head_r * 1.12 * k),
                (cx + head_r * 1.04 * k, hy - head_r * 0.14 * k)], cap)
        c.rrect(cx - head_r * 1.12 * k, hy - head_r * 0.32 * k,
                cx + head_r * 1.12 * k, hy - head_r * 0.02 * k, 4.0 * k, cap_d)
        c.rrect(cx - head_r * 0.84 * k, hy - head_r * 0.06 * k,
                cx + head_r * 0.84 * k, hy + head_r * 0.44 * k, 3.0 * k, hair)
    else:
        sgn = -1.0 if direction == DIR_LEFT else 1.0
        c.poly([(cx - head_r * sgn * 0.96 * k, hy - head_r * 0.18 * k),
                (cx - head_r * sgn * 0.58 * k, hy - head_r * 1.1 * k),
                (cx + head_r * sgn * 0.5 * k, hy - head_r * 1.1 * k),
                (cx + head_r * sgn * 1.04 * k, hy - head_r * 0.18 * k)], cap)
        c.rrect(cx + head_r * sgn * 0.18 * k, hy - head_r * 0.36 * k,
                cx + head_r * sgn * 1.18 * k, hy + head_r * 0.02 * k, 4.0 * k, cap_d)
        c.ellipse(cx + head_r * sgn * 0.24 * k, hy + head_r * 0.16 * k, 5.6 * k,
                  7.0 * k, ink)
        c.circle(cx + head_r * sgn * 0.36 * k, hy + head_r * 0.03 * k, 2.2 * k,
                 (255, 255, 255))
        c.ellipse(cx + head_r * sgn * 0.72 * k, hy + head_r * 0.52 * k, 5.0 * k,
                  3.4 * k, skin_s)
    return c


def character_frame(cfg, direction, phase, w=CH_W, h=CH_H, scale=1.0):
    c = Canvas(w, h)
    draw_character(c, cfg, direction, phase, scale=scale)
    c.despeckle()
    c.outline(rgba(cfg["ink"], 235), 2.2)
    return c


def build_character_sheet(cfg, frames=4, w=CH_W, h=CH_H, scale=1.0):
    """4 columns (walk frames) x 4 rows (Down, Left, Right, Up)."""
    sheet = Canvas(frames * w, 4 * h)
    for d in range(4):
        for f in range(frames):
            fr = character_frame(cfg, d, f, w, h, scale)
            sheet.blit(fr, f * w, (3 - d) * h)      # Unity rows count bottom-up
    return sheet


def gen_chars():
    """Player + professor + a few NPC recolours, plus a 1-frame portrait each."""
    jobs = [("player_walk", PLAYER_CFG), ("professor_walk", PROF_CFG)]
    for i, hue in enumerate((0.02, 0.33, 0.58, 0.77, 0.11, 0.92)):
        jobs.append(("npc_%02d_walk" % i, npc_cfg(hue)))

    out = os.path.join(ART, "Characters")
    for name, cfg in jobs:
        t0 = time.time()
        sheet = build_character_sheet(cfg)
        sheet.save(os.path.join(out, name + ".png"))
        print("    %-18s %dx%d (4x4 frames)  %.1fs"
              % (name, sheet.w, sheet.h, time.time() - t0))

    # a single idle frame (facing south) for menus / dialogue portraits
    for name, cfg in (("player_idle", PLAYER_CFG), ("professor_idle", PROF_CFG)):
        fr = character_frame(cfg, DIR_DOWN, 0, CH_W, CH_H, 1.6)
        fr.save(os.path.join(out, name + ".png"))
    print("    idle frames saved (192x240 @1.6x -> %dx%d)"
          % (int(CH_W * 1.6), int(CH_H * 1.6)))

    meta = {"frame_w": CH_W, "frame_h": CH_H, "ppu": CH_PPU, "cols": 4, "rows": 4,
            "rows_order": DIR_NAMES[::-1],
            "note": "4 walk frames per row; rows bottom-up = Down,Left,Right,Up"}
    with open(os.path.join(OUT, "characters.json"), "w") as fh:
        json.dump(meta, fh, indent=2)
    return meta


# --------------------------------------------------------------------------- #
#  Creatures -- 12 designs x (base, evolved).  Types follow the design doc:
#  Flame, Tide, Leaf, Stone, Spark.
# --------------------------------------------------------------------------- #

CREATURE_PX = 384
CREATURE_PPU = 192            # 384/192 = 2.0 x 2.0 world units

TYPE_SPECS = {
    "Flame": {"body": (232, 96, 52), "belly": (255, 198, 128),
              "trim": (176, 44, 36), "accent": (255, 216, 96)},
    "Tide": {"body": (58, 148, 226), "belly": (186, 232, 255),
             "trim": (30, 96, 172), "accent": (140, 226, 240)},
    "Leaf": {"body": (86, 176, 78), "belly": (206, 240, 168),
             "trim": (46, 116, 50), "accent": (250, 232, 120)},
    "Stone": {"body": (168, 138, 108), "belly": (214, 196, 170),
              "trim": (110, 88, 66), "accent": (140, 140, 148)},
    "Spark": {"body": (242, 200, 62), "belly": (255, 244, 190),
              "trim": (196, 146, 26), "accent": (120, 210, 255)},
}

# (name, type, stage, seed) -- 12 base forms plus one evolution each.
CREATURES = [
    ("Cindle", "Flame", 1, 11), ("Cindrake", "Flame", 2, 12),
    ("Emberling", "Flame", 1, 13), ("Emberwyrm", "Flame", 2, 14),
    ("Drippa", "Tide", 1, 21), ("Drippool", "Tide", 2, 22),
    ("Ripple", "Tide", 1, 23), ("Tidalisk", "Tide", 2, 24),
    ("Mossling", "Leaf", 1, 31), ("Mosshulk", "Leaf", 2, 32),
    ("Sproutle", "Leaf", 1, 33), ("Sproutree", "Leaf", 2, 34),
    ("Pebbit", "Stone", 1, 41), ("Pebboulder", "Stone", 2, 42),
    ("Shale", "Stone", 1, 43), ("Shaleguard", "Stone", 2, 44),
    ("Zapkin", "Spark", 1, 51), ("Zapzilla", "Spark", 2, 52),
    ("Voltike", "Spark", 1, 53), ("Voltivault", "Spark", 2, 54),
    ("Glomoth", "Leaf", 1, 61), ("Glomothra", "Leaf", 2, 62),
    ("Bramble", "Leaf", 1, 63), ("Bramblethorn", "Leaf", 2, 64),
]


def creature_frame(name, ctype, stage, seed, px=CREATURE_PX):
    """Draw one creature. Stage 2 forms are broader, horned and meaner."""
    spec = TYPE_SPECS[ctype]
    body = rgba(spec["body"])
    belly = rgba(spec["belly"])
    trim = rgba(spec["trim"])
    accent = rgba(spec["accent"])
    ink = (34, 28, 26, 255)

    c = Canvas(px, px)
    cx = px * 0.5
    scale = 0.74 if stage == 1 else 0.88
    R = px * 0.30 * scale
    ground_y = px * 0.93          # ankle line: feet straddle it
    seed = seed + sum(ord(ch) for ch in name) % 101   # per-creature variation

    # --- shadow + feet (body is anchored just above the ankles) ------------ #
    c.ellipse(cx, ground_y + R * 0.10, R * 0.98, R * 0.20, (18, 20, 28, 90))
    for sgn in (-1, 1):
        c.ellipse(cx + sgn * R * 0.44, ground_y - R * 0.04, R * 0.30, R * 0.19, trim)

    by = ground_y - R * 1.05
    c.ellipse(cx, by, R, R * (0.96 if stage == 1 else 1.06), body)
    c.ellipse(cx, by + R * 0.28, R * 0.62, R * 0.56, belly)
    for sgn in (-1, 1):
        c.circle(cx + sgn * R * 0.94, by + R * 0.06,
                 R * 0.24 * (0.9 + 0.2 * stage), body)

    # --- head + eyes ------------------------------------------------------ #
    hy = by - R * (0.92 if stage == 1 else 1.0)
    hr = R * (0.72 if stage == 1 else 0.8)
    c.circle(cx, hy, hr, body)
    c.ellipse(cx, hy + hr * 0.36, hr * 0.62, hr * 0.42, belly)
    for sgn in (-1, 1):
        ex = cx + sgn * hr * 0.42
        ey = hy - hr * 0.08
        c.ellipse(ex, ey, hr * 0.30, hr * 0.34, (252, 252, 255, 255))
        c.circle(ex + sgn * hr * 0.05, ey + hr * 0.05, hr * 0.16, ink)
        c.circle(ex - sgn * hr * 0.06, ey - hr * 0.09, hr * 0.06,
                 (255, 255, 255, 255))

    draw_creature_features(c, ctype, stage, seed, cx, by, hy, R, hr,
                           body, belly, trim, accent)

    c.despeckle()
    c.outline(ink, 3.0)
    return c


def draw_creature_features(c, ctype, stage, seed, cx, by, hy, R, hr,
                           body, belly, trim, accent):
    """Per-type silhouette details so each type reads at a glance."""
    if ctype == "Flame":
        for k in range(3):
            fh = hr * (1.0 - k * 0.24)
            fx = cx + (artlib._hash2(k, seed, 7) - 0.5) * hr * 0.9
            c.poly([(fx - hr * 0.32, hy - hr * 0.5),
                    (fx, hy - hr * 0.5 - fh),
                    (fx + hr * 0.32, hy - hr * 0.5)],
                   accent if k % 2 == 0 else body)
        c.ellipse(cx, by + R * 0.1, R * 0.3, R * 0.5, rgba(accent, 150))
    elif ctype == "Tide":
        c.poly([(cx - R * 0.36, by - R * 0.9), (cx, by - R * 1.5),
                (cx + R * 0.36, by - R * 0.9)], accent)
        for sgn in (-1, 1):
            c.ellipse(cx + sgn * hr * 0.78, hy + hr * 0.5, hr * 0.22, hr * 0.3,
                      accent)
        c.ellipse(cx, by + R * 0.34, R * 0.46, R * 0.3, rgba(accent, 120))
    elif ctype == "Leaf":
        c.capsule(cx, hy - hr * 0.9, cx, hy - hr * 1.28, hr * 0.09, trim)
        for sgn in (-1, 1):
            c.ellipse(cx + sgn * hr * 0.44, hy - hr * 1.14, hr * 0.42, hr * 0.22,
                      accent)
        c.ellipse(cx, hy - hr * 1.30, hr * 0.24, hr * 0.18, accent)
        if stage == 2:
            for sgn in (-1, 1):
                c.ellipse(cx + sgn * R * 0.86, by + R * 0.34, R * 0.34, R * 0.2,
                          accent)
    elif ctype == "Stone":
        for k in range(3):
            a = math.pi * (0.18 + 0.32 * k)
            pxc = cx + math.cos(a) * R * 0.62
            pyc = by + math.sin(a) * R * 0.5 - R * 0.1
            s = R * 0.26
            c.poly([(pxc, pyc - s), (pxc + s * 0.9, pyc - s * 0.3),
                    (pxc + s * 0.6, pyc + s * 0.7), (pxc - s * 0.8, pyc + s * 0.5),
                    (pxc - s * 0.9, pyc - s * 0.4)], trim)
        if stage == 2:
            for sgn in (-1, 1):
                c.poly([(cx + sgn * hr * 0.66, hy - hr * 0.7),
                        (cx + sgn * hr * 1.3, hy - hr * 1.5),
                        (cx + sgn * hr * 0.98, hy - hr * 0.4)], trim)
    else:  # Spark
        for k in range(5):
            a = -math.pi * 0.5 + (k - 2) * 0.5
            c.poly([(cx + math.cos(a) * hr * 0.7, hy + math.sin(a) * hr * 0.7),
                    (cx + math.cos(a) * hr * 1.5, hy + math.sin(a) * hr * 1.5),
                    (cx + math.cos(a + 0.32) * hr * 0.74,
                     hy + math.sin(a + 0.32) * hr * 0.74)], accent)
        c.poly([(cx - hr * 0.4, by + R * 0.5), (cx + hr * 0.12, by + R * 0.86),
                (cx - hr * 0.1, by + R * 0.86), (cx + hr * 0.42, by + R * 1.24),
                (cx + hr * 0.02, by + R * 0.78), (cx + hr * 0.24, by + R * 0.78)],
               accent)

    if stage == 2:
        # horns + a heavier build so evolutions read as "bigger, meaner"
        for sgn in (-1, 1):
            c.poly([(cx + sgn * hr * 0.52, hy - hr * 0.66),
                    (cx + sgn * hr * 0.96, hy - hr * 1.42),
                    (cx + sgn * hr * 1.12, hy - hr * 0.6)], trim)
        c.ellipse(cx, by + R * 0.42, R * 0.5, R * 0.16, rgba(accent, 110))


def gen_creatures():
    out = os.path.join(ART, "Creatures")
    index = []
    for name, ctype, stage, seed in CREATURES:
        t0 = time.time()
        fr = creature_frame(name, ctype, stage, seed)
        fr.save(os.path.join(out, name + ".png"))
        index.append({"name": name, "type": ctype, "stage": stage,
                      "file": name + ".png", "px": CREATURE_PX,
                      "ppu": CREATURE_PPU})
        print("    %-14s %-6s stage%d  %dx%d  %.1fs"
              % (name, ctype, stage, fr.w, fr.h, time.time() - t0))
    with open(os.path.join(OUT, "creatures.json"), "w") as fh:
        json.dump({"px": CREATURE_PX, "ppu": CREATURE_PPU, "creatures": index},
                  fh, indent=2)
    return index


# --------------------------------------------------------------------------- #
#  UI -- minimap mask/bezel, HUD panel, gate icons, map arrow
# --------------------------------------------------------------------------- #

UI = 512


def gen_ui():
    out = os.path.join(ART, "UI")
    meta = {}

    # --- circular mask (white disc, transparent outside) ------------------ #
    mask = Canvas(UI, UI)
    mask.circle(UI * 0.5, UI * 0.5, UI * 0.5 - 1.0, (255, 255, 255, 255))
    mask.save(os.path.join(out, "minimap_mask.png"))

    # --- bezel + compass ring --------------------------------------------- #
    bz = Canvas(UI, UI)
    R = UI * 0.5 - 2.0
    bz.ring(UI * 0.5, UI * 0.5, R - 9.0, 20.0, (28, 34, 46, 235))
    bz.ring(UI * 0.5, UI * 0.5, R - 19.0, 3.0, (196, 214, 240, 200))
    # cardinal ticks: longer at N/S, shorter at E/W
    for k in range(8):
        a = -math.pi * 0.5 + k * math.pi / 4.0
        major = (k % 2 == 0)
        rad_out = R - 21.0
        rad_in = rad_out - (26.0 if major else 14.0)
        col = (255, 236, 176, 255) if k == 0 else (206, 220, 238, 210)
        bz.line(UI * 0.5 + math.cos(a) * rad_in, UI * 0.5 + math.sin(a) * rad_in,
                UI * 0.5 + math.cos(a) * rad_out, UI * 0.5 + math.sin(a) * rad_out,
                6.0 if major else 4.0, col)
    # north marker: a filled triangle sitting on the ring
    tip = UI * 0.5 - R + 12.0
    bz.poly([(UI * 0.5, tip - 26.0), (UI * 0.5 - 17.0, tip + 12.0),
             (UI * 0.5 + 17.0, tip + 12.0)], (236, 92, 78, 255))
    bz.poly([(UI * 0.5, tip - 26.0), (UI * 0.5 - 17.0, tip + 12.0),
             (UI * 0.5, tip + 12.0)], (186, 58, 48, 255))
    bz.save(os.path.join(out, "minimap_bezel.png"))

    # --- player arrow marker ---------------------------------------------- #
    ar = Canvas(128, 128)
    ac = 64.0
    ar.poly([(ac, ac - 30.0), (ac + 24.0, ac + 26.0), (ac, ac + 14.0),
             (ac - 24.0, ac + 26.0)], (255, 246, 214, 255))
    ar.poly([(ac, ac - 30.0), (ac + 24.0, ac + 26.0), (ac, ac + 14.0)],
            (232, 190, 92, 255))
    ar.outline((28, 34, 46, 240), 3.0)
    ar.save(os.path.join(out, "minimap_arrow.png"))

    # --- 9-slice HUD panel ------------------------------------------------- #
    P = 128
    pn = Canvas(P, P)
    pn.rect(0, 0, P, P, (18, 22, 32, 214))
    pn.rrect(1.0, 1.0, P - 1.0, P - 1.0, 12.0, (18, 22, 32, 214))
    pn.rrect(3.0, 3.0, P - 3.0, P - 3.0, 11.0, (60, 72, 96, 190))
    pn.rrect(5.0, 5.0, P - 5.0, P - 5.0, 10.0, (18, 22, 32, 214))
    pn.save(os.path.join(out, "hud_panel.png"))
    meta["hud_panel_border"] = 20

    # --- gate state icons -------------------------------------------------- #
    for name, open_state in (("gate_locked", False), ("gate_open", True)):
        g = Canvas(256, 256)
        body_col = (86, 190, 120, 255) if open_state else (206, 78, 62, 255)
        g.rrect(66.0, 118.0, 190.0, 224.0, 16.0, body_col)
        g.rrect(72.0, 124.0, 184.0, 218.0, 12.0, shade(body_col, 1.18))
        if open_state:
            # shackle swung open to the right
            g.ring(176.0, 96.0, 40.0, 16.0, (196, 204, 216, 255))
            g.rect(60.0, 96.0, 96.0, 128.0, (0, 0, 0, 0))
        else:
            g.ring(128.0, 96.0, 40.0, 16.0, (196, 204, 216, 255))
            g.rect(60.0, 96.0, 196.0, 122.0, (0, 0, 0, 0))
        g.circle(128.0, 168.0, 15.0, (30, 34, 44, 255))
        g.rrect(122.0, 168.0, 134.0, 200.0, 5.0, (30, 34, 44, 255))
        g.outline((24, 28, 36, 240), 3.0)
        g.save(os.path.join(out, name + ".png"))

    # --- type badge discs (used on creature cards and gates) -------------- #
    for ctype, spec in TYPE_SPECS.items():
        b = Canvas(192, 192)
        b.circle(96.0, 96.0, 88.0, rgba(spec["trim"], 255))
        b.circle(96.0, 96.0, 78.0, rgba(spec["body"], 255))
        b.ellipse(74.0, 70.0, 34.0, 22.0, rgba(spec["belly"], 190))
        b.save(os.path.join(out, "type_%s.png" % ctype.lower()))

    with open(os.path.join(OUT, "ui.json"), "w") as fh:
        json.dump(meta, fh, indent=2)
    print("    minimap mask/bezel/arrow, hud panel, gate icons, 5 type badges")
    return meta


# --------------------------------------------------------------------------- #
#  World props -- trees, bushes, rocks, signs, flowers, gate barriers
# --------------------------------------------------------------------------- #

def prop_tree(w=256, h=320, seed=7):
    c = Canvas(w, h)
    trunk_w = w * 0.11
    c.capsule(w * 0.5 - trunk_w, h * 0.72, w * 0.5 + trunk_w, h * 0.99,
              trunk_w, PAL["trunk"])
    c.capsule(w * 0.5 - trunk_w * 0.4, h * 0.78, w * 0.5 + trunk_w * 0.4, h * 0.96,
              trunk_w * 0.5, PAL["trunk_hi"])
    blobs = [(0.50, 0.40, 0.30), (0.28, 0.52, 0.22), (0.72, 0.52, 0.22),
             (0.40, 0.26, 0.22), (0.63, 0.27, 0.21), (0.50, 0.60, 0.19)]
    for i, (bx, by, br) in enumerate(blobs):
        tone = [PAL["forest_a"], PAL["forest_hi"], shade(PAL["forest_a"], 0.82)][i % 3]
        c.circle(w * bx, h * by, w * br, tone)
    for i, (bx, by, br) in enumerate(blobs):
        c.ellipse(w * bx - w * br * 0.22, h * by - h * br * 0.24,
                  w * br * 0.42, h * br * 0.3, shade(PAL["forest_hi"], 1.14))
    c.outline(rgba(PAL["ink"], 225), 3.0)
    return c


def prop_bush(w=192, h=160, seed=8):
    c = Canvas(w, h)
    for i, (bx, by, br) in enumerate([(0.32, 0.62, 0.32), (0.62, 0.60, 0.30),
                                      (0.48, 0.42, 0.30)]):
        c.circle(w * bx, h * by, w * br,
                 [PAL["forest_a"], PAL["forest_hi"]][i % 2])
    for i in range(5):
        a = artlib._hash2(i, 1, seed)
        c.circle(w * (0.22 + a * 0.56), h * (0.34 + a * 0.18), w * 0.055,
                 rgba(PAL["forest_b"], 210))
    c.outline(rgba(PAL["ink"], 215), 3.0)
    return c


def prop_rock(w=192, h=160, seed=9):
    c = Canvas(w, h)
    c.poly([(w * 0.12, h * 0.92), (w * 0.06, h * 0.5), (w * 0.34, h * 0.18),
            (w * 0.7, h * 0.16), (w * 0.94, h * 0.5), (w * 0.88, h * 0.92)],
           PAL["rock_b"])
    c.poly([(w * 0.12, h * 0.92), (w * 0.26, h * 0.5), (w * 0.5, h * 0.22),
            (w * 0.7, h * 0.16), (w * 0.5, h * 0.62), (w * 0.44, h * 0.92)],
           PAL["rock_a"])
    c.poly([(w * 0.3, h * 0.28), (w * 0.62, h * 0.2), (w * 0.56, h * 0.36),
            (w * 0.34, h * 0.4)], PAL["rock_hi"])
    c.outline(rgba(PAL["ink"], 225), 3.0)
    return c


def prop_sign(w=160, h=192, seed=10):
    c = Canvas(w, h)
    c.capsule(w * 0.5, h * 0.55, w * 0.5, h * 0.99, w * 0.055, PAL["trunk"])
    c.rrect(w * 0.1, h * 0.1, w * 0.9, h * 0.6, 10.0, PAL["plank_b"])
    c.rrect(w * 0.13, h * 0.13, w * 0.87, h * 0.57, 9.0, PAL["plank_a"])
    for k in range(3):
        y = h * (0.24 + k * 0.11)
        c.rrect(w * 0.22, y, w * (0.5 + 0.26 * (k % 2)), y + h * 0.045, 4.0,
                shade(PAL["plank_b"], 0.72))
    c.outline(rgba(PAL["ink"], 225), 3.0)
    return c


def prop_flowers(w=128, h=128, seed=11):
    c = Canvas(w, h)
    for i in range(7):
        a = artlib._hash2(i, 1, seed)
        b = artlib._hash2(i, 2, seed)
        fx = w * (0.14 + a * 0.72)
        fy = h * (0.30 + b * 0.6)
        col = hsv((a * 0.8 + 0.02) % 1.0, 0.7, 0.95)
        for k in range(5):
            ang = k * math.tau / 5.0
            c.circle(fx + math.cos(ang) * w * 0.05, fy + math.sin(ang) * w * 0.05,
                     w * 0.038, col)
        c.circle(fx, fy, w * 0.028, (255, 236, 150, 255))
    c.outline(rgba(PAL["ink"], 200), 2.2)
    return c


def prop_gate(w=640, h=224, seed=12):
    """A two-tile barrier with a padlock plate -- the visual for a locked path."""
    c = Canvas(w, h)
    for x in (w * 0.06, w * 0.5, w * 0.94):
        c.rrect(x - w * 0.035, h * 0.16, x + w * 0.035, h * 0.98, 6.0,
                PAL["trunk"])
        c.rrect(x - w * 0.026, h * 0.18, x + w * 0.014, h * 0.96, 4.0,
                PAL["trunk_hi"])
    for k, y in enumerate((0.4, 0.66)):
        tone = mix(PAL["plank_b"], PAL["plank_a"], 0.4 + 0.3 * k)
        c.rrect(w * 0.03, h * y, w * 0.97, h * (y + 0.11), 5.0, tone)
        c.rect(w * 0.05, h * y + 3, w * 0.95, h * y + 7, shade(tone, 1.18))
    c.rrect(w * 0.36, h * 0.30, w * 0.64, h * 0.74, 12.0, PAL["gate_locked"])
    c.rrect(w * 0.375, h * 0.32, w * 0.625, h * 0.72, 10.0,
            shade(PAL["gate_locked"], 1.16))
    c.ring(w * 0.5, h * 0.40, w * 0.05, 11.0, (208, 214, 226, 255))
    c.circle(w * 0.5, h * 0.54, 12.0, shade(PAL["ink"], 0.9))
    c.outline(rgba(PAL["ink"], 230), 3.5)
    return c


def gen_props():
    out = os.path.join(ART, "World")
    jobs = [("tree", prop_tree()), ("bush", prop_bush()), ("rock", prop_rock()),
            ("sign", prop_sign()), ("flowers", prop_flowers()), ("gate", prop_gate())]
    for name, cvs in jobs:
        cvs.save(os.path.join(out, name + ".png"))
        print("    %-10s %dx%d" % (name, cvs.w, cvs.h))
    meta = {"props": [{"name": n, "w": c.w, "h": c.h, "ppu": 128} for n, c in jobs]}
    with open(os.path.join(OUT, "props.json"), "w") as fh:
        json.dump(meta, fh, indent=2)
    return meta


def gen_split_tiles(tile):
    """One PNG per terrain type -- lets Unity import every tile as Sprite:Single,
    which avoids the fragile grid-slicing API entirely."""
    out = os.path.join(ART, "Tiles")
    for idx, painter in enumerate(TILE_PAINTERS):
        fr = painter(tile, 1337 + idx * 4099)
        fr.save(os.path.join(out, "tile_%02d_%s.png" % (idx, TERRAIN_NAMES[idx].lower())))
    print("    wrote %d individual tile PNGs" % len(TILE_PAINTERS))

    # Collision-only tile: a plain opaque square. It is painted into an
    # invisible "Blockers" tilemap whose renderer is disabled, so it only ever
    # contributes collider geometry. Kept small (32px) to stay cheap.
    coll = Canvas(32, 32, (255, 255, 255, 255))
    coll.save(os.path.join(out, "tile_collision.png"))


def gen_split_characters():
    """One PNG per (direction, frame) so the walk cycle needs no slicing."""
    out = os.path.join(ART, "Characters")
    jobs = [("player", PLAYER_CFG), ("professor", PROF_CFG)]
    for i, hue in enumerate((0.02, 0.33, 0.58, 0.77, 0.11, 0.92)):
        jobs.append(("npc_%02d" % i, npc_cfg(hue)))

    count = 0
    for prefix, cfg in jobs:
        for d in range(4):
            for f in range(4):
                fr = character_frame(cfg, d, f, CH_W, CH_H, 1.0)
                fr.save(os.path.join(out, "%s_%s_%d.png"
                                     % (prefix, DIR_NAMES[d].lower(), f)))
                count += 1
    print("    wrote %d individual character frames" % count)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))