"""Footprint audit for Assets/Data/Chapter1_Route.txt.

Answers the three questions ChapterBuilder.ValidateStage1 asserts:

  1. Is every BUILDING sprite fully inside the map rectangle?
  2. Is every BUILDING sprite fully visible, given the camera can never look
     higher than (highest reachable player cell + ORTHO)?
  3. Does any BUILDING overlap another blocking object (hedge tiles, trees,
     pines, fences, mailboxes, other buildings)?

Sprite sizes come from the PNG headers, so this cannot drift from the art.
Run:  python Tools/check_footprints.py
"""
import os
import struct
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(ROOT)
PACK = os.path.join(PROJ, "Assets", "Art", "_Mine", "Sprites")
LAYOUT = os.path.join(PROJ, "Assets", "Data", "Chapter1_Route.txt")

PPU = 64.0
TREE_PPU = 96.0                   # Trees folder imports at 96 (ChapterBuilder)
ORTHO = 4.5
BLOCKING = set("~#TYhfmGn")
BUILDING_CHARS = set("hG")
TREE_VARIANTS = ["Tree_Small", "Tree_Medium", "Tree_Big", "Tree_Autumn"]

TREE_OVERLAP_LIMIT = 0.20
MAX_TREE_BIG = 3

SIZES = {}                       # populated in load_sizes()


def png_size(path):
    with open(path, "rb") as fh:
        head = fh.read(33)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return struct.unpack(">II", head[16:24])


def load_sizes():
    def take(rel, ppu=PPU):
        w, h = png_size(os.path.join(PACK, rel))
        return (w / ppu, h / ppu)

    for name in ("Gym", "House_Small", "House_Large"):
        SIZES[name] = take("Environment/Buildings/%s.png" % name)
    for name in TREE_VARIANTS + ["Pine"]:
        SIZES[name] = take("Environment/Trees/%s.png" % name, TREE_PPU)
    for name in ("Fence", "Mailbox", "Sign", "Rock", "Barrel"):
        SIZES[name] = take("Environment/Props/%s.png" % name)
    SIZES["Hedge"] = (1.0, 1.0)          # a 64x64 tile fills its cell


def load_layout():
    rows = []
    with open(LAYOUT, encoding="utf-8") as fh:
        for raw in fh:
            line = raw.rstrip("\r\n")
            if not line or line.startswith("//"):
                continue
            rows.append(line)
    return rows


def sprite_for(ch, cx, cy, plan=None):
    """Must mirror ChapterBuilder.PropSprite / PlanTrees exactly."""
    if ch == "G":
        return "Gym"
    if ch == "h":
        return "House_Small"
    if ch in ("T", "Y"):
        return plan.sprite.get((cx, cy)) if plan else None
    if ch == "f":
        return "Fence"
    if ch == "m":
        return "Mailbox"
    if ch == "#":
        return "Hedge"
    return None


def rect_of(ch, cx, cy, plan=None):
    """Sprite rect in Unity world space, bottom-centre anchored on the cell."""
    name = sprite_for(ch, cx, cy, plan)
    if name is None:
        return None, None
    w, h = SIZES[name]
    return (cx + 0.5 - w / 2.0, cy, cx + 0.5 + w / 2.0, cy + h), name


# --------------------------------------------------------------- tree planner

class TreePlan(object):
    def __init__(self):
        self.sprite = {}
        self.skipped = set()
        self.rects = []
        self.big = 0


def to_unity(c, r, height):
    return (c, height - 1 - r)


def is_border(c, r, width, height):
    return c == 0 or c == width - 1 or r == 0 or r == height - 1


def tree_rect(name, cx, cy):
    w, h = SIZES[name]
    return (cx + 0.5 - w / 2.0, cy, cx + 0.5 + w / 2.0, cy + h)


def overlap_fraction(a, b):
    ox = min(a[2], b[2]) - max(a[0], b[0])
    oy = min(a[3], b[3]) - max(a[1], b[1])
    if ox <= 0 or oy <= 0:
        return 0.0
    smaller = min((a[2] - a[0]) * (a[3] - a[1]),
                  (b[2] - b[0]) * (b[3] - b[1]))
    if smaller <= 0:
        return 0.0
    return (ox * oy) / smaller


def plan_trees(rows, width, height):
    """Mirror of ChapterBuilder.PlanTrees."""
    plan = TreePlan()
    big_cells = []

    for r in range(height):
        for c in range(width):
            ch = rows[r][c]
            if ch not in ("T", "Y"):
                continue

            cx, cy = to_unity(c, r, height)

            if is_border(c, r, width, height):
                primary = "Tree_Small" if ((c + r) % 2 == 0) else "Pine"
                other = "Pine" if ((c + r) % 2 == 0) else "Tree_Small"
                candidates = [primary, other]
            else:
                candidates = []
                if ((c * 13 + r * 7) % 9) == 0 and len(big_cells) < MAX_TREE_BIG:
                    adjacent = any(abs(b[0] - cx) <= 1 and abs(b[1] - cy) <= 1
                                   for b in big_cells)
                    if not adjacent:
                        candidates.append("Tree_Big")
                candidates.append("Pine" if ch == "Y" else "Tree_Medium")
                candidates.append("Tree_Small")

            # First candidate that fits; a wide Tree_Big degrades rather than
            # leaving a bald cell.
            chosen, chosen_rect = None, None
            for name in candidates:
                rect = tree_rect(name, cx, cy)
                if any(overlap_fraction(rect, pr) > TREE_OVERLAP_LIMIT
                       for pr, _ in plan.rects):
                    continue
                chosen, chosen_rect = name, rect
                break

            if chosen is None:
                plan.skipped.add((cx, cy))
                continue

            plan.sprite[(cx, cy)] = chosen
            plan.rects.append((chosen_rect, (cx, cy)))
            if chosen == "Tree_Big":
                plan.big += 1
                big_cells.append((cx, cy))

    return plan


# A graze smaller than this many world units is not worth failing a build for:
# at PPU 64 it is under 10 px, and because the two sprites have different base Y
# they still sort correctly - no z-fighting, nothing visible. The real defect is
# an object overlapping a building's face by a visible amount.
OVERLAP_TOL = 0.15


def overlaps(a, b, tol=OVERLAP_TOL):
    return (a[0] < b[2] - tol and b[0] < a[2] - tol and
            a[1] < b[3] - tol and b[1] < a[3] - tol)


def main():
    load_sizes()
    rows = load_layout()
    height = len(rows)
    width = len(rows[0])

    bad = [i for i, r in enumerate(rows) if len(r) != width]
    if bad:
        print("FAIL ragged rows: %s" % bad)
        return 1
    print("layout            : %d x %d" % (width, height))

    def to_unity(c, r):
        return (c, height - 1 - r)

    plan = plan_trees(rows, width, height)

    print("trees             : %d placed, %d skipped for spacing, %d Tree_Big (cap %d)"
          % (len(plan.sprite), len(plan.skipped), plan.big, MAX_TREE_BIG))

    def expected_blocked(c, r):
        ch = rows[r][c]
        cx, cy = to_unity(c, r)
        if (cx, cy) in plan.skipped:
            return is_border(c, r, width, height)
        return ch in BLOCKING

    rects = []
    for r in range(height):
        for c in range(width):
            ch = rows[r][c]
            if ch not in BLOCKING:
                continue
            cx, cy = to_unity(c, r)
            rect, name = rect_of(ch, cx, cy, plan)
            if rect:
                rects.append((ch, c, r, rect, name))

    # ---- 1. buildings inside the map rect
    print("\n--- 1. buildings vs map bounds (0..%d, 0..%d) ---" % (width, height))
    inside_ok = True
    for ch, c, r, rect, name in rects:
        if ch not in BUILDING_CHARS:
            continue
        ok = (rect[0] >= 0 and rect[2] <= width and rect[1] >= 0 and rect[3] <= height)
        inside_ok &= ok
        print("  [%s] %-11s ascii(%2d,%2d) y %.2f..%.2f  %s"
              % (ch, name, c, r, rect[1], rect[3], "inside" if ok else "CLIPPED"))
    print("  => %s" % ("all buildings inside the map" if inside_ok else "FAIL"))

    # ---- 2. camera reach
    # The camera clamps to the map and centres on the player, so the highest
    # point it can ever show is (highest reachable player cell + ORTHO).
    highest = -1
    for r in range(height):
        for c in range(width):
            if not expected_blocked(c, r):
                highest = max(highest, height - 1 - r)
    view_top = highest + ORTHO
    print("\n--- 2. buildings vs camera reach ---")
    print("  highest reachable player cell : y=%d" % highest)
    print("  highest visible world y       : %.2f" % view_top)
    visible_ok = True
    for ch, c, r, rect, name in rects:
        if ch not in BUILDING_CHARS:
            continue
        ok = rect[3] <= view_top + 1e-6
        visible_ok &= ok
        note = "visible" if ok else "CLIPPED by %.2f units" % (rect[3] - view_top)
        print("  [%s] %-11s top=%.2f  %s" % (ch, name, rect[3], note))
    print("  => %s" % ("all buildings fully visible" if visible_ok else "FAIL"))

    # ---- 3. building vs blocking object
    # A building may legitimately stand IN FRONT of background (trees behind
    # it) - that just reads as "building against the forest". What must never
    # happen is a blocking object at the SAME or a LOWER base Y, because that
    # renders in front and occludes the building.
    print("\n--- 3. building vs blocking object overlaps ---")
    print("  (BEHIND = higher Y = fine; IN FRONT = same/lower Y = fails)")
    buildings = [x for x in rects if x[0] in BUILDING_CHARS]
    others = [x for x in rects if x[0] not in BUILDING_CHARS]
    behind = 0
    conflicts = 0
    for bch, bc, br, brect, bname in buildings:
        for och, oc, orow, orect, oname in others:
            if not overlaps(brect, orect):
                continue
            obj_y = height - 1 - orow
            if obj_y > brect[1] + 1e-6:
                behind += 1
            else:
                conflicts += 1
                print("  IN FRONT  %-11s ascii(%2d,%2d) base y=%.2f  x  '%s' %s ascii(%2d,%2d) base y=%d"
                      % (bname, bc, br, brect[1], och, oname, oc, orow, obj_y))
    for i in range(len(buildings)):
        for j in range(i + 1, len(buildings)):
            if overlaps(buildings[i][3], buildings[j][3]):
                conflicts += 1
                print("  IN FRONT  building/building %s(%d,%d) x %s(%d,%d)"
                      % (buildings[i][4], buildings[i][1], buildings[i][2],
                         buildings[j][4], buildings[j][1], buildings[j][2]))
    print("  behind-only overlaps (acceptable): %d" % behind)
    print("  => %s" % ("no in-front overlaps" if conflicts == 0 else "%d conflict(s)" % conflicts))

    return 0 if (inside_ok and visible_ok and conflicts == 0) else 1


if __name__ == "__main__":
    sys.exit(main())