"""Apply the Stage 1 layout fixes to Assets/Data/Chapter1_Route.txt.

  1. Clear ASCII row 5, cols 8..14 to grass. That is the row directly above
     the gym. It gives the player a standable cell at world y=17, which lifts
     the highest visible world point to 21.5 and clears the gym roof (top
     21.23). It also removes the gym/treeline overlap.
  2. (12,0) T -> Y : Tree_Big is 3.05 units wide and its canopy clipped the
     house at col 3. A Pine is 2.17 wide and the col 0 border stays solid.
  3. Move the mailbox (13,19) -> (14,19) : it sat directly in front of the
     house at (12,19) and occluded it.
  4. Move the pine (14,1) -> (15,1) : its canopy overlapped the house at
     (12,3).

Run:  python Tools/patch_route.py      (idempotent-safe: prints a summary)
"""
import os
import sys

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LAYOUT = os.path.join(PROJ, "Assets", "Data", "Chapter1_Route.txt")


def load():
    rows = []
    with open(LAYOUT, encoding="utf-8") as fh:
        for raw in fh:
            line = raw.rstrip("\r\n")
            if not line or line.startswith("//"):
                continue
            rows.append(list(line))
    return rows


def main():
    rows = load()
    height = len(rows)
    width = len(rows[0])
    print("before: %d x %d" % (width, height))

    def at(c, r):
        return rows[r][c]

    def put(c, r, ch):
        rows[r][c] = ch

    changes = []

    # 1. clearing above the gym
    for c in range(8, 15):
        if at(c, 5) == "T":
            put(c, 5, ".")
            changes.append("(5,%d) T -> ." % c)

    # 2. narrower border sprite
    if at(0, 12) == "T":
        put(0, 12, "Y")
        changes.append("(12,0) T -> Y")

    # 3. mailbox out from in front of the house
    if at(19, 13) == "m" and at(19, 14) != "m":
        put(19, 13, ".")
        put(19, 14, "m")
        changes.append("mailbox (13,19) -> (14,19)")

    # 4. pine canopy still poked into the house at (12,3); push it one row lower
    if at(1, 15) == "Y" and at(1, 16) != "Y":
        put(1, 15, ",")
        put(1, 16, "Y")
        changes.append("pine (15,1) -> (16,1)")

    out = []
    for line in rows:
        if len(line) != width:
            print("FAIL ragged row: %s" % "".join(line))
            return 1
        out.append("".join(line))

    for ch in changes:
        print("  " + ch)
    if not changes:
        print("  (already applied)")

    with open(LAYOUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("// CHAPTER 1 ROUTE - text layout for MapBuilder.\n")
        fh.write("// Lines starting with \"//\" and blank lines are ignored.\n")
        fh.write("//\n")
        fh.write("// Row 5, cols 8..14 are cleared so the gym has standable ground\n")
        fh.write("// above it: that lifts the camera's reach and clears the gym roof.\n")
        fh.write("//\n")
        fh.write("// LEGEND\n")
        fh.write("//   .  grass            (walkable)\n")
        fh.write("//   ,  tall grass       (walkable, decoration only)\n")
        fh.write("//   =  brick path       (walkable)\n")
        fh.write("//   ~  shallow water    (BLOCKING)\n")
        fh.write("//   #  hedge            (BLOCKING)\n")
        fh.write("//   T  tree             (BLOCKING, sprite from Environment/Trees)\n")
        fh.write("//   Y  pine             (BLOCKING, sprite from Environment/Trees)\n")
        fh.write("//   h  house            (BLOCKING, sprite from Environment/Buildings)\n")
        fh.write("//   f  fence            (BLOCKING, sprite from Environment/Props)\n")
        fh.write("//   m  mailbox          (BLOCKING, sprite from Environment/Props)\n")
        fh.write("//   G  gym              (BLOCKING, sprite from Environment/Buildings)\n")
        fh.write("//   n  NPC              (BLOCKING, tint + stand-in sprite)\n")
        fh.write("//   @  player spawn     (walkable)\n")
        fh.write("//\n")
        fh.write("// The gym door is the tile directly BELOW the 'G'.\n")
        fh.write("\n")
        for line in out:
            fh.write(line + "\n")

    print("after : %d x %d  (%d change(s))" % (width, height, len(changes)))
    return 0


if __name__ == "__main__":
    sys.exit(main())