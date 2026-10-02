"""Palette reconnaissance: sample the painted maps so generated tiles match."""
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__))))
import artlib

ROOT = r"d:/project/game pika/pikagame-main/"

for rel in ("Assets/Sprites/Environment/map.png",
            "Assets/Sprites/Environment/lab-oke.png"):
    t0 = time.time()
    w, h, px = artlib.read_png(ROOT + rel)
    dt = time.time() - t0
    print("%s  %dx%d  decoded in %.1fs" % (rel, w, h, dt))
    hist = artlib.palette_histogram(w, h, px, step=6, bits=4)
    total = sum(c for _, c in hist)
    for col, cnt in hist[:14]:
        print("    rgb(%3d,%3d,%3d)  %5.1f%%" % (col[0], col[1], col[2],
                                                 100.0 * cnt / total))

    # block classification preview at 32px blocks
    grid = artlib.block_mean(w, h, px, 32, 32)
    print("    block grid: %d cols x %d rows" % (len(grid[0]), len(grid)))
    print()