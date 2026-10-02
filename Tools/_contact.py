"""Build a contact sheet of generated creatures for visual review."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import artlib                                                        # noqa: E402

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CREAT = os.path.join(PROJ, "Assets", "Art", "Creatures")

NAMES = ["Cindle", "Cindrake", "Emberling", "Emberwyrm",
         "Drippa", "Drippool", "Ripple", "Tidalisk",
         "Mossling", "Mosshulk", "Sproutle", "Sproutree",
         "Pebbit", "Pebboulder", "Shale", "Shaleguard",
         "Zapkin", "Zapzilla", "Voltike", "Voltivault",
         "Glomoth", "Glomothra", "Bramble", "Bramblethorn"]

cell = 192
cols = 8
rows = (len(NAMES) + cols - 1) // cols
sheet = artlib.Canvas(cols * cell, rows * cell, (46, 52, 66, 255))
for i, name in enumerate(NAMES):
    fp = os.path.join(CREAT, name + ".png")
    if not os.path.exists(fp):
        continue
    w, h, px = artlib.read_png(fp)
    small = artlib.resample_bicubic(w, h, px, cell, cell)
    sub = artlib.Canvas(cell, cell)
    sub.px[:] = small
    sheet.blit(sub, (i % cols) * cell, (i // cols) * cell)
sheet.save(os.path.join(PROJ, "Tools", "out", "creature_contact.png"))
print("wrote Tools/out/creature_contact.png (%dx%d)" % (sheet.w, sheet.h))