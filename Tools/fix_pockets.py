"""Open the cheapest wall between each unreachable pocket and the reachable area.

Every walkable cell must be reachable on foot from '@' (see check_reach.py).
This finds each pocket, BFS's outward through blocking cells at a cost of 1,
and punches the cheapest wall cell into a walkable one.

Run:  python Tools/fix_pockets.py
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
            if not line or line.startswith("//"):
                continue
            rows.append(list(line))
    return rows


def save(rows, header):
    w = len(rows[0])
    with open(LAYOUT, "w", encoding="utf-8", newline="\n") as fh:
        for line in header:
            fh.write(line + "\n")
        fh.write("\n")
        for row in rows:
            if len(row) != w:
                print("FAIL ragged row")
                return 1
            fh.write("".join(row) + "\n")
    return 0


def reachable(rows):
    h = len(rows)
    w = len(rows[0])
    walk = [[rows[r][c] not in BLOCKING for c in range(w)] for r in range(h)]
    spawn = next((c, r) for r in range(h) for c in range(w) if rows[r][c] == "@")
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
    return seen, spawn


def main():
    rows = load()
    h = len(rows)
    w = len(rows[0])

    header = []
    with open(LAYOUT, encoding="utf-8") as fh:
        for raw in fh:
            line = raw.rstrip("\r\n")
            if line and not line.startswith("//"):
                break
            header.append(line)

    total_opened = 0
    for _ in range(40):
        seen, _spawn = reachable(rows)
        pockets = [(c, r) for r in range(h) for c in range(w)
                   if rows[r][c] not in BLOCKING and not seen[r][c]]
        if not pockets:
            break

        # Cheapest wall from any pocket to already-reachable ground. Track the
        # parent so we punch the WALL being crossed, not the open cell we
        # arrived at (that one is already reachable, so opening it is a no-op).
        best_wall = None
        queue = collections.deque()
        parent = {}
        for cell in pockets:
            queue.append(cell)
            parent[cell] = None

        while queue:
            c, r = queue.popleft()
            if seen[r][c]:
                # Walk back until the step that crossed a wall.
                cur = (c, r)
                while parent[cur] is not None and \
                        rows[parent[cur][1]][parent[cur][0]] not in BLOCKING:
                    cur = parent[cur]
                cand = parent[cur] if parent[cur] is not None else None
                if cand is not None and rows[cand[1]][cand[0]] in BLOCKING:
                    best_wall = cand
                break
            for dc, dr in NEIGHBOURS:
                nc, nr = c + dc, r + dr
                if not (0 <= nc < w and 0 <= nr < h):
                    continue
                if (nc, nr) in parent:
                    continue
                parent[(nc, nr)] = (c, r)
                queue.append((nc, nr))

        if best_wall is None:
            print("FAIL: no wall to open between pocket and open ground")
            return 1

        c, r = best_wall
        old = rows[r][c]
        rows[r][c] = "."
        total_opened += 1
        print("  opened wall at ascii col %d row %d (%s -> .)" % (c, r, old))

    print("opened %d wall cell(s)" % total_opened)
    return save(rows, header)


if __name__ == "__main__":
    sys.exit(main())