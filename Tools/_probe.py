"""Quick reconnaissance of the PNG/JPEG inputs. Stdlib only."""
import struct, zlib, os, sys

ROOT = r"d:/project/game pika/pikagame-main/"

TARGETS = [
    "Assets/Sprites/Environment/map.png",
    "Assets/Sprites/Environment/lab-oke.png",
    "Assets/Sprites/UI/button-ui.png",
    "Assets/Sprites/UI/MainUi.jpg",
]


def probe(rel):
    fp = ROOT + rel
    if not os.path.exists(fp):
        print("%-46s MISSING" % rel)
        return
    raw = open(fp, "rb").read()
    if raw[:2] == b"\xff\xd8":
        print("%-46s JPEG %d bytes" % (rel, len(raw)))
        return
    if raw[:8] != b"\x89PNG\r\n\x1a\n":
        print("%-46s NOT PNG (%r)" % (rel, raw[:8]))
        return
    w, h, bd, ct, comp, filt, inter = struct.unpack(">IIBBBBB", raw[16:29])
    # collect chunk names
    names = []
    off = 8
    while off < len(raw):
        ln = struct.unpack(">I", raw[off:off + 4])[0]
        nm = raw[off + 4:off + 8].decode("latin1")
        names.append(nm)
        off += 12 + ln
        if nm == "IEND":
            break
    print("%-46s PNG %dx%d depth=%d ctype=%d interlace=%d chunks=%s"
          % (rel, w, h, bd, ct, inter, ",".join(names[:8])))


for t in TARGETS:
    probe(t)