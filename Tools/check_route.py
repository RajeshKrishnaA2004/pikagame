"""Independent check of Assets/Data/Chapter1_Route.txt.

Mirrors ChapterBuilder.CanReachGymDoor so the layout can be proven
completable without opening Unity. Prints the shortest spawn->gym-door
path so it can be eyeballed against the map.
"""
import collections
import os
import sys

LAYOUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Data",
                      "Chapter1_Route.txt")
BLOCKING = set("~#TYhfmGn")


def load(path):
    rows = []
    with open(path, encoding="utf-8") as fh:
        for raw in fh:
            line = raw.rstrip("\r\n")
            if not line or line.startswith("//"):
                continue
            rows.append(line)
    return rows


def main():
    rows = load(LAYOUT)
    width = len(rows[0])
    ragged = [(i, len(r)) for i, r in enumerate(rows) if len(r) != width]
    print("rows            : %d" % len(rows))
    print("cols            : %d" % width)
    print("ragged rows     : %s" % (ragged if ragged else "none"))

    heights = len(rows)
    used = set()
    for r in range(heights):
        for c in range(width):
            used.add(rows[r][c])
    unknown = used - set(". , = ~ # T Y h f m G n @".replace(" ", ""))
    print("unknown glyphs  : %s" % (sorted(unknown) if unknown else "none"))

    def find(ch):
        for r in range(heights):
            for c in range(width):
                if rows[r][c] == ch:
                    return c, r
        return None

    spawn = find("@")
    gym = find("G")
    door = (gym[0], gym[1] + 1) if gym else None
    print("spawn '@'       : col=%s row=%s" % spawn)
    print("gym   'G'       : col=%s row=%s" % gym)
    print("gym door        : col=%s row=%s  walkable=%s"
          % (door[0], door[1], door[1] < heights and rows[door[1]][door[0]] not in BLOCKING))

    blocked = sum(1 for r in rows for ch in r if ch in BLOCKING)
    print("blocking cells  : %d" % blocked)
    print("walkable cells  : %d" % (width * heights - blocked))

    if spawn is None or door is None:
        print("RESULT: FAIL (missing spawn or door)")
        return 1

    start = (spawn[0], spawn[1])
    goal = (door[0], door[1])
    prev = {start: None}
    q = collections.deque([start])

    while q:
        cur = q.popleft()
        if cur == goal:
            break
        c, r = cur
        for dc, dr in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nc, nr = c + dc, r + dr
            if not (0 <= nc < width and 0 <= nr < heights):
                continue
            if rows[nr][nc] in BLOCKING:
                continue
            if (nc, nr) in prev:
                continue
            prev[(nc, nr)] = cur
            q.append((nc, nr))

    if goal not in prev:
        print("RESULT: FAIL - gym door unreachable from spawn")
        return 1

    path = []
    node = goal
    while node is not None:
        path.append(node)
        node = prev[node]
    path.reverse()
    print("RESULT: PASS - reachable in %d steps" % (len(path) - 1))
    print("path (col,row) top-down:")
    print("  " + " -> ".join("%d,%d" % p for p in path))
    return 0


if __name__ == "__main__":
    sys.exit(main())