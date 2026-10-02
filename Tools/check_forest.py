"""Mirror of ForestGen.cs so counts can be verified without Unity.

Run:  python Tools/check_forest.py
"""
import os
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACK = os.path.join(ROOT, "Assets", "Art", "_Mine", "Sprites", "Environment", "Trees")
LAYOUT = os.path.join(ROOT, "Assets", "Data", "Chapter1_Route.txt")

SEED = 20260214
BASE_BLOCK = 0.40
COVERAGE = 0.55   # must match ForestGen.BlockCoverage
GAP = 0.10
MAX_BIG = 3
POOL = ["Tree_Small", "Tree_Small", "Tree_Medium", "Tree_Medium",
        "Pine", "Tree_Autumn", "Bush", "Bush_Flowers", "Tree_Big"]
TREE_PPU = 96.0
MASK = 0xFFFFFFFF


def sizes():
    out = {}
    for n in POOL + ["Tree_Big"]:
        with open(os.path.join(PACK, n + ".png"), "rb") as fh:
            d = fh.read(33)
        w, h = struct.unpack(">II", d[16:24])
        out[n] = (w / TREE_PPU, h / TREE_PPU)
    return out


def load():
    rows = []
    with open(LAYOUT, encoding="utf-8") as fh:
        for raw in fh:
            line = raw.rstrip("\r\n")
            if line and not line.startswith("//"):
                rows.append(line)
    return rows


def hash01(a, b):
    h = ((a * 73856093) & MASK) ^ ((b * 19349663) & MASK)
    h &= MASK
    h ^= h >> 13
    h = (h * 0x5BD1E995) & MASK
    h ^= h >> 15
    return (h & 0x00FFFFFF) / 16777216.0


def top_depth(c):
    return 3 + int(hash01(c, 11) * 3.0)


def side_depth(r):
    return 2 + int(hash01(r, 57) * 3.0)


def bottom_depth(c):
    if c < 4 or c > 19:
        return 0
    return 1 + int(hash01(c, 91) * 2.0)


def in_band(c, r, w, h):
    if r < top_depth(c):
        return True
    if c < side_depth(r) or c >= w - side_depth(r):
        return True
    return r >= h - bottom_depth(c)


class Rng(object):
    def __init__(self, seed):
        self.s = seed & MASK
        if self.s == 0:
            self.s = 0x9E3779B9

    def next(self):
        s = self.s
        s ^= (s << 13) & MASK
        s ^= s >> 17
        s ^= (s << 5) & MASK
        self.s = s & MASK
        return self.s

    def value01(self):
        return (self.next() & 0x00FFFFFF) / 16777216.0

    def frange(self, lo, hi):
        return lo + (hi - lo) * self.value01()

    def irange(self, lo, hi):
        if hi <= lo:
            return lo
        return lo + (self.next() % (hi - lo + 1))

    def chance(self, p):
        return self.value01() < p


def coverage(rect, cell):
    ox = min(rect[2], cell[0] + 1.0) - max(rect[0], cell[0])
    oy = min(rect[3], cell[1] + 1.0) - max(rect[1], cell[1])
    if ox <= 0 or oy <= 0:
        return 0.0
    return ox * oy


def main():
    sz = sizes()
    rows = load()
    h = len(rows)
    w = len(rows[0])

    rng = Rng(SEED)
    last = [None] * h
    run = [0] * h
    big_used = 0

    trees = {}          # (cx,cy) -> name
    blocked = set()
    canopy = set()
    gaps = 0
    skipped = 0

    for r in range(h):
        for c in range(w):
            ch = rows[r][c]
            if ch in ("@", "="):
                continue
            band = in_band(c, r, w, h)
            accent = ch in ("T", "Y") and not band
            if not band and not accent:
                continue

            cell = (c, h - 1 - r)

            if rng.chance(GAP):
                gaps += 1
                skipped += 1
                continue

            name = None
            for _ in range(6):
                cand = POOL[rng.irange(0, len(POOL) - 1)]
                if last[r] == cand and run[r] >= 2:
                    continue
                if cand == "Tree_Big":
                    if big_used >= MAX_BIG or last[r] == "Tree_Big":
                        continue
                    big_used += 1
                name = cand
                break
            if name is None:
                name = "Tree_Small"

            if last[r] == name:
                run[r] += 1
            else:
                last[r] = name
                run[r] = 1

            tw, th = sz[name]
            if tw <= 0 or th <= 0:
                skipped += 1
                continue

            rng.frange(-0.25, 0.25)      # offset.x
            rng.frange(-0.25, 0.25)      # offset.y
            rng.frange(0.9, 1.1)         # scale
            rng.chance(0.5)              # flip

            trees[cell] = name
            blocked.add(cell)

            fx = cell[0] + 0.5 - tw / 2.0
            fy = cell[1]
            base_h = th * BASE_BLOCK
            bx0, bx1 = fx, fx + tw
            by0, by1 = fy, fy + base_h
            cy0, cy1 = fy + base_h, fy + th

            for y in range(int(by0 // 1), -(-int(by1) // 1)):
                for x in range(int(bx0 // 1), -(-int(bx1) // 1)):
                    c2 = (x, y)
                    if c2 == cell:
                        continue
                    if coverage((bx0, by0, bx1, by1), c2) >= COVERAGE:
                        blocked.add(c2)
            for y in range(int(cy0 // 1), -(-int(cy1) // 1)):
                for x in range(int(bx0 // 1), -(-int(bx1) // 1)):
                    c2 = (x, y)
                    if coverage((bx0, cy0, bx1, cy1), c2) >= COVERAGE:
                        canopy.add(c2)

    big_count = sum(1 for n in trees.values() if n == "Tree_Big")

    print("forest placed    : %d" % len(trees))
    print("forest gaps      : %d" % gaps)
    print("forest skipped   : %d" % skipped)
    print("Tree_Big         : %d (cap %d)" % (big_count, MAX_BIG))
    print("blocked cells    : %d" % len(blocked))
    print("canopy cells     : %d (walkable, drawn over player)" % len(canopy))

    names = {}
    for n in trees.values():
        names[n] = names.get(n, 0) + 1
    print("sprite mix       : %s" % sorted(names.items(), key=lambda kv: -kv[1]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
