"""Reachability audit: every walkable cell must be reachable on foot from '@'.

Run:  python Tools/check_reach.py
Exits non-zero when there are pockets the player could never walk into.
"""
import collections
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LAYOUT = os.path.join(ROOT, "Assets", "Data", "Chapter1_Route.txt")

BLOCKING = set("~#TYhfmGn")

NEIGHBOURS = ((1, 0), (-1, 0), (0, 1), (0, -1))


def load():
    rows = []
    with open(LAYOUT, encoding="utf-8") as fh:
        for raw in fh:
            line = raw.rstrip("\r\n")
            if line and not line.startswith("//"):
                rows.append(line)
    return rows


def main():
    rows = load()
    h = len(rows)
    w = len(rows[0])

    bad = [i for i, r in enumerate(rows) if len(r) != w]
    if bad:
        print("FAIL ragged rows: %s" % bad)
        return 1

    walk = [[rows[r][c] not in BLOCKING for c in range(w)] for r in range(h)]

    spawn = None
    for r in range(h):
        for c in range(w):
            if rows[r][c] == "@":
                spawn = (c, r)
    if spawn is None:
        print("FAIL no spawn '@'")
        return 1

    seen = [[False] * w for _ in range(h)]
    seen[spawn[1]][spawn[0]] = True
    q = collections.deque([spawn])
    while q:
        c, r = q.popleft()
        for dc, dr in NEIGHBOURS:
            nc, nr = c + dc, r + dr
            if 0 <= nc < w and 0 <= nr < h and walk[nr][nc] and not seen[nr][nc]:
                seen[nr][nc] = True
                q.append((nc, nr))

    pockets = [(c, r) for r in range(h) for c in range(w)
               if walk[r][c] and not seen[r][c]]

    total = sum(sum(row) for row in walk)
    print("layout          : %d x %d" % (w, h))
    print("spawn '@'       : col=%d row=%d" % spawn)
    print("walkable cells  : %d" % total)
    print("reachable       : %d" % (total - len(pockets)))
    print("UNREACHABLE     : %d" % len(pockets))
    for c, r in pockets:
        print("   pocket at ascii col %d row %d (unity cell %d,%d)" % (c, r, c, h - 1 - r))

    if pockets:
        print("\nRESULT: FAIL - %d unreachable cell(s)" % len(pockets))
        return 1

    print("\nRESULT: PASS - every walkable cell is reachable")
    return 0


if __name__ == "__main__":
    sys.exit(main())