"""artlib.py -- stdlib-only 2D art toolkit for the PikaGame remake.

No third-party dependencies (no Pillow). Contains:
  * read_png / write_png          (8/16-bit, colour types 0/2/3/4/6, non-interlaced)
  * Canvas                        (RGBA surface with analytic-AA SDF shape filling)
  * periodic value noise / fBm    (so every generated tile tiles seamlessly)
  * palette helpers + colour-space conversions

Everything is deterministic: the same source always regenerates identical bytes.
"""

import math
import struct
import zlib

_PNG_SIG = b"\x89PNG\r\n\x1a\n"


def _paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    if pb <= pc:
        return b
    return c


def _unfilter(raw, height, stride, bpp):
    """Reverse the five PNG scanline filters. Returns one flat bytearray."""
    out = bytearray(height * stride)
    pos = 0
    prev = bytearray(stride)
    for row in range(height):
        ft = raw[pos]
        pos += 1
        line = bytearray(raw[pos:pos + stride])
        pos += stride
        if ft == 1:                        # Sub
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 0xFF
        elif ft == 2:                      # Up
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif ft == 3:                      # Average
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 0xFF
        elif ft == 4:                      # Paeth
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                c = prev[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + _paeth(a, prev[i], c)) & 0xFF
        elif ft != 0:
            raise ValueError("bad PNG filter type %d" % ft)
        out[row * stride:(row + 1) * stride] = line
        prev = line
    return out


def read_png(path):
    """Return (width, height, bytearray RGBA). Raises on unsupported variants."""
    data = open(path, "rb").read()
    if data[:8] != _PNG_SIG:
        raise ValueError("%s is not a PNG" % path)

    width = height = depth = ctype = 0
    interlace = 0
    palette = b""
    trns = b""
    idat = []
    pos = 8
    while pos < len(data):
        (ln,) = struct.unpack(">I", data[pos:pos + 4])
        name = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if name == b"IHDR":
            width, height, depth, ctype, _c, _f, interlace = struct.unpack(
                ">IIBBBBB", body)
        elif name == b"PLTE":
            palette = body
        elif name == b"tRNS":
            trns = body
        elif name == b"IDAT":
            idat.append(body)
        elif name == b"IEND":
            break

    if interlace:
        raise ValueError("interlaced PNG not supported (%s)" % path)
    if depth not in (8, 16):
        raise ValueError("unsupported bit depth %d (%s)" % (depth, path))

    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ctype]
    step = 2 if depth == 16 else 1
    bpp = channels * step
    stride = width * bpp
    raw = _unfilter(zlib.decompress(b"".join(idat)), height, stride, bpp)

    out = bytearray(width * height * 4)
    for y in range(height):
        base = y * stride
        o = y * width * 4
        if ctype == 6:
            for x in range(width):
                i = base + x * bpp
                out[o:o + 4] = bytes((raw[i], raw[i + step], raw[i + 2 * step],
                                      raw[i + 3 * step]))
                o += 4
        elif ctype == 2:
            for x in range(width):
                i = base + x * bpp
                out[o:o + 4] = bytes((raw[i], raw[i + step], raw[i + 2 * step], 255))
                o += 4
        elif ctype == 0:
            for x in range(width):
                v = raw[base + x * bpp]
                out[o:o + 4] = bytes((v, v, v, 255))
                o += 4
        elif ctype == 4:
            for x in range(width):
                i = base + x * bpp
                v = raw[i]
                out[o:o + 4] = bytes((v, v, v, raw[i + step]))
                o += 4
        else:  # palette
            for x in range(width):
                idx = raw[base + x]
                p = idx * 3
                out[o:o + 4] = bytes((palette[p], palette[p + 1], palette[p + 2],
                                      trns[idx] if idx < len(trns) else 255))
                o += 4
    return width, height, out


def write_png(path, width, height, rgba):
    """Write an 8-bit RGBA PNG using filter type 0 on every scanline."""
    stride = width * 4
    raw = bytearray(height * (stride + 1))
    for y in range(height):
        raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)] = \
            rgba[y * stride:(y + 1) * stride]
    comp = zlib.compress(bytes(raw), 6)

    def chunk(tag, body):
        return (struct.pack(">I", len(body)) + tag + body +
                struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF))

    blob = (_PNG_SIG + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", comp) + chunk(b"IEND", b""))
    open(path, "wb").write(blob)


# --------------------------------------------------------------------------- #
#  Deterministic hash + periodic value noise (seamless tiling)
# --------------------------------------------------------------------------- #

_M64 = 0xFFFFFFFFFFFFFFFF


def _hash2(x, y, seed):
    n = (x * 0x9E3779B185EBCA87 + y * 0xC2B2AE3D27D4EB4F
         + seed * 0x165667B19E3779F9)
    n &= _M64
    n = ((n ^ (n >> 29)) * 0xBF58476D1CE4E5B9) & _M64
    n = ((n ^ (n >> 32)) * 0x94D049BB133111EB) & _M64
    n ^= n >> 31
    return (n & 0xFFFF) / 65535.0


def pnoise(x, y, period, seed):
    """Value noise that repeats exactly every `period` units in x and y."""
    xi = int(math.floor(x))
    yi = int(math.floor(y))
    xf = x - xi
    yf = y - yi
    x0 = xi % period
    y0 = yi % period
    x1 = (x0 + 1) % period
    y1 = (y0 + 1) % period
    v00 = _hash2(x0, y0, seed)
    v10 = _hash2(x1, y0, seed)
    v01 = _hash2(x0, y1, seed)
    v11 = _hash2(x1, y1, seed)
    u = xf * xf * (3.0 - 2.0 * xf)
    v = yf * yf * (3.0 - 2.0 * yf)
    return ((v00 * (1.0 - u) + v10 * u) * (1.0 - v)
            + (v01 * (1.0 - u) + v11 * u) * v)


def pfbm(x, y, period, octaves, seed, gain=0.5):
    """Periodic fractal noise. `x`,`y` are already in period units."""
    total = 0.0
    amp = 1.0
    norm = 0.0
    p = period
    for o in range(octaves):
        total += amp * pnoise(x * p, y * p, p, seed + o * 977)
        norm += amp
        amp *= gain
        p *= 2
    return total / norm


def phash(i, j, seed):
    """Stable per-cell hash -- used to scatter props without overlap."""
    return _hash2(i, j, seed)


# --------------------------------------------------------------------------- #
#  Colour helpers
# --------------------------------------------------------------------------- #

def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else (hi if v > hi else v)


def mix(a, b, t):
    t = clamp(t)
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def rgba(c, a=255):
    return c if len(c) == 4 else (c[0], c[1], c[2], a)


def shade(c, k):
    """Multiply an RGB(A) colour by k, preserving alpha."""
    c = rgba(c)
    return (int(clamp(c[0] * k, 0, 255)), int(clamp(c[1] * k, 0, 255)),
            int(clamp(c[2] * k, 0, 255)), c[3])


def over(dst, src):
    """src-over composite of two RGBA tuples; returns RGBA."""
    sa = src[3] / 255.0
    if sa <= 0.0:
        return dst
    da = dst[3] / 255.0
    oa = sa + da * (1.0 - sa)
    if oa <= 0.0:
        return (0, 0, 0, 0)
    return tuple(int(round((src[i] * sa + dst[i] * da * (1.0 - sa)) / oa))
                 for i in range(3)) + (int(round(oa * 255)),)


def hsv(h, s, v, a=255):
    """h in [0,1), s/v in [0,1] -> RGBA tuple."""
    h = h % 1.0
    i = int(h * 6.0) % 6
    f = h * 6.0 - int(h * 6.0)
    p = v * (1.0 - s)
    q = v * (1.0 - s * f)
    t = v * (1.0 - s * (1.0 - f))
    r, g, b = [(v, t, p), (q, v, p), (p, v, t),
               (p, q, v), (t, p, v), (v, p, q)][i]
    return (int(r * 255), int(g * 255), int(b * 255), a)


# --------------------------------------------------------------------------- #
#  Canvas: RGBA surface with analytic anti-aliased SDF filling
# --------------------------------------------------------------------------- #

class Canvas(object):
    def __init__(self, width, height, fill=None):
        self.w = width
        self.h = height
        self.px = bytearray(width * height * 4)
        if fill and rgba(fill)[3]:
            self.rect(0, 0, width, height, fill)

    # ---- raw pixel access ------------------------------------------------- #
    def get(self, x, y):
        if x < 0 or y < 0 or x >= self.w or y >= self.h:
            return (0, 0, 0, 0)
        o = (y * self.w + x) * 4
        return (self.px[o], self.px[o + 1], self.px[o + 2], self.px[o + 3])

    def put(self, x, y, c):
        if x < 0 or y < 0 or x >= self.w or y >= self.h:
            return
        o = (y * self.w + x) * 4
        c = rgba(c)
        self.px[o:o + 4] = bytes(c)

    def blend(self, x, y, c):
        if x < 0 or y < 0 or x >= self.w or y >= self.h:
            return
        o = (y * self.w + x) * 4
        cur = (self.px[o], self.px[o + 1], self.px[o + 2], self.px[o + 3])
        self.px[o:o + 4] = bytes(over(cur, rgba(c)))

    def rect(self, x0, y0, x1, y1, c):
        c = rgba(c)
        for y in range(max(0, int(y0)), min(self.h, int(y1))):
            for x in range(max(0, int(x0)), min(self.w, int(x1))):
                self.blend(x, y, c)

    # ---- SDF filling ------------------------------------------------------ #
    def fill_sdf(self, sdf, c, x0, y0, x1, y1, aa=1.0):
        """Fill {sdf(x,y) <= 0}, anti-aliased across `aa` pixels."""
        c = rgba(c)
        if c[3] == 0:
            return
        for y in range(max(0, int(y0)), min(self.h, int(y1) + 1)):
            fy = y + 0.5
            for x in range(max(0, int(x0)), min(self.w, int(x1) + 1)):
                d = sdf(x + 0.5, fy)
                if d > aa:
                    continue
                cov = clamp(0.5 - d / aa)
                if cov <= 0.0:
                    continue
                self.blend(x, y, (c[0], c[1], c[2], int(round(c[3] * cov))))

    # ---- primitives ------------------------------------------------------- #
    def circle(self, cx, cy, r, c):
        self.fill_sdf(lambda x, y: math.hypot(x - cx, y - cy) - r, c,
                      cx - r - 2, cy - r - 2, cx + r + 2, cy + r + 2)

    def ring(self, cx, cy, r, w, c):
        self.fill_sdf(lambda x, y: abs(math.hypot(x - cx, y - cy) - r) - w * 0.5,
                      c, cx - r - w, cy - r - w, cx + r + w, cy + r + w)

    def ellipse(self, cx, cy, rx, ry, c):
        rr = max(rx, ry)

        def d(x, y):
            dx = (x - cx) / rx
            dy = (y - cy) / ry
            k = math.hypot(dx, dy)
            if k < 1e-6:
                return -min(rx, ry)
            return (k - 1.0) * min(rx, ry) / k
        self.fill_sdf(d, c, cx - rr - 2, cy - rr - 2, cx + rr + 2, cy + rr + 2)

    def rrect(self, x0, y0, x1, y1, r, c):
        def d(x, y):
            dx = max(x0 + r - x, 0.0, x - (x1 - r))
            dy = max(y0 + r - y, 0.0, y - (y1 - r))
            return math.hypot(dx, dy) - r
        self.fill_sdf(d, c, x0 - 2, y0 - 2, x1 + 2, y1 + 2)

    def capsule(self, ax, ay, bx, by, r, c):
        def d(x, y):
            vx, vy = bx - ax, by - ay
            wx, wy = x - ax, y - ay
            L = vx * vx + vy * vy
            t = 0.0 if L == 0.0 else clamp((wx * vx + wy * vy) / L)
            return math.hypot(wx - vx * t, wy - vy * t) - r
        self.fill_sdf(d, c, min(ax, bx) - r - 2, min(ay, by) - r - 2,
                      max(ax, bx) + r + 2, max(ay, by) + r + 2)

    def line(self, ax, ay, bx, by, w, c):
        self.capsule(ax, ay, bx, by, w * 0.5, c)

    def poly(self, pts, c):
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        n = len(pts)

        def d(x, y):
            inside = False
            j = n - 1
            for i in range(n):
                xi, yi = pts[i]
                xj, yj = pts[j]
                if (yi > y) != (yj > y):
                    if x < xi + (y - yi) * (xj - xi) / (yj - yi):
                        inside = not inside
                j = i
            if inside:
                return -1.0
            best = 1e9
            j = n - 1
            for i in range(n):
                xi, yi = pts[i]
                xj, yj = pts[j]
                vx, vy = xj - xi, yj - yi
                L = vx * vx + vy * vy
                t = 0.0 if L == 0.0 else clamp(((x - xi) * vx + (y - yi) * vy) / L)
                best = min(best, math.hypot(x - xi - vx * t, y - yi - vy * t))
                j = i
            return best
        self.fill_sdf(d, c, min(xs) - 2, min(ys) - 2, max(xs) + 2, max(ys) + 2)


# ---- compositing ------------------------------------------------------ #
    def blit(self, src, dx, dy, alpha=1.0):
        for y in range(src.h):
            ty = dy + y
            if ty < 0 or ty >= self.h:
                continue
            base = y * src.w * 4
            for x in range(src.w):
                tx = dx + x
                if tx < 0 or tx >= self.w:
                    continue
                o = base + x * 4
                a = src.px[o + 3]
                if a == 0:
                    continue
                if alpha < 1.0:
                    a = int(a * alpha)
                self.blend(tx, ty, (src.px[o], src.px[o + 1], src.px[o + 2], a))

    def crop(self, x, y, w, h):
        c = Canvas(w, h)
        for yy in range(h):
            sy = y + yy
            if sy < 0 or sy >= self.h:
                continue
            row = (sy * self.w + x) * 4
            drow = yy * w * 4
            for xx in range(w):
                sx = x + xx
                if sx < 0 or sx >= self.w:
                    continue
                so = row + sx * 4
                do = drow + xx * 4
                c.px[do:do + 4] = self.px[so:so + 4]
        return c

    def save(self, path):
        write_png(path, self.w, self.h, self.px)

    # ---- post-process ----------------------------------------------------- #
    def outline(self, c, width=2.0):
        """Trace an outline around every opaque region."""
        src = bytes(self.px)
        w, h = self.w, self.h
        r = int(math.ceil(width))
        rad = width * width

        def solid(x, y):
            return (0 <= x < w and 0 <= y < h
                    and src[(y * w + x) * 4 + 3] > 8)

        for y in range(h):
            for x in range(w):
                if solid(x, y):
                    continue
                near = False
                for dy in range(-r, r + 1):
                    for dx in range(-r, r + 1):
                        if dx * dx + dy * dy <= rad and solid(x + dx, y + dy):
                            near = True
                            break
                    if near:
                        break
                if near:
                    self.blend(x, y, c)

    def shadow_drop(self, dx=0, dy=6, blur=4, alpha=90):
        """Drop a soft shadow using the current alpha as the silhouette."""
        src = bytes(self.px)
        w, h = self.w, self.h
        for y in range(h):
            for x in range(w):
                a = 0
                for by in range(-blur, blur + 1):
                    sy = y - dy + by
                    if sy < 0 or sy >= h:
                        continue
                    for bx in range(-blur, blur + 1):
                        sx = x - dx + bx
                        if sx < 0 or sx >= w:
                            continue
                        if by * by + bx * bx > blur * blur:
                            continue
                        s = src[(sy * w + sx) * 4 + 3]
                        if s > a:
                            a = s
                if a > 0:
                    self.blend(x, y, (12, 14, 22, int(a * alpha / 255.0)))

    def despeckle(self):
        """Remove isolated pixels (kills stray single-pixel noise)."""
        src = bytes(self.px)
        w, h = self.w, self.h
        for y in range(1, h - 1):
            for x in range(1, w - 1):
                o = (y * w + x) * 4
                if src[o + 3] == 0:
                    continue
                n = 0
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        if (dx or dy) and src[((y + dy) * w + (x + dx)) * 4 + 3] > 8:
                            n += 1
                if n == 0:
                    self.px[o:o + 4] = b"\x00\x00\x00\x00"

    def scale_alpha(self, factor):
        for i in range(3, len(self.px), 4):
            self.px[i] = int(clamp(self.px[i] * factor, 0, 255))


def tint(c, target, amount):
    """Blend an RGB colour toward `target` by `amount`."""
    return mix(rgba(c)[:3], rgba(target)[:3], amount)


# --------------------------------------------------------------------------- #
#  Fast periodic noise field (precomputed lattices: ~10x faster than _hash2)
# --------------------------------------------------------------------------- #

class NoiseField(object):
    """Seamlessly tiling fBm noise built from precomputed periodic lattices."""

    def __init__(self, seed, octaves=3, base_period=4, gain=0.5):
        self.seed = seed
        self.gain = gain
        self.layers = []
        amp = 1.0
        norm = 0.0
        p = base_period
        for o in range(octaves):
            self.layers.append((p, amp, self._lattice(p, seed + o * 7919)))
            norm += amp
            amp *= gain
            p *= 2
        self.norm = norm or 1.0

    @staticmethod
    def _lattice(p, seed):
        return [_hash2(i, j, seed) for j in range(p) for i in range(p)]

    def at(self, u, v):
        """u,v in tile units [0,1) -> noise in [0,1]; repeats every 1.0."""
        acc = 0.0
        for p, amp, lat in self.layers:
            x = u * p
            y = v * p
            xi = int(x)
            yi = int(y)
            xf = x - xi
            yf = y - yi
            x0 = xi % p
            y0 = yi % p
            x1 = (x0 + 1) % p
            y1 = (y0 + 1) % p
            u2 = xf * xf * (3.0 - 2.0 * xf)
            v2 = yf * yf * (3.0 - 2.0 * yf)
            r0 = y0 * p
            r1 = y1 * p
            a = lat[r0 + x0]
            b = lat[r0 + x1]
            c = lat[r1 + x0]
            d = lat[r1 + x1]
            acc += amp * ((a * (1.0 - u2) + b * u2) * (1.0 - v2)
                          + (c * (1.0 - u2) + d * u2) * v2)
        return acc / self.norm

# --------------------------------------------------------------------------- #
#  Resampling + image analysis (used to derive terrain from the painted map)
# --------------------------------------------------------------------------- #

def _catmull(p0, p1, p2, p3, t):
    return (p1 + 0.5 * t * (p2 - p0 + t * (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3
                                         + t * (3.0 * (p1 - p2) + p3 - p0))))


def resample_bicubic(w, h, rgba, nw, nh):
    """Separable Catmull-Rom resample of an RGBA bytearray -> new bytearray."""
    # --- horizontal pass: w -> nw
    tmp = bytearray(nw * h * 4)
    xr = float(w) / nw
    for y in range(h):
        srow = y * w * 4
        drow = y * nw * 4
        for x in range(nw):
            sx = (x + 0.5) * xr - 0.5
            i1 = int(math.floor(sx))
            t = sx - i1
            for ch in range(4):
                p0 = rgba[srow + max(0, i1 - 1) * 4 + ch]
                p1 = rgba[srow + max(0, i1) * 4 + ch]
                p2 = rgba[srow + min(w - 1, i1 + 1) * 4 + ch]
                p3 = rgba[srow + min(w - 1, i1 + 2) * 4 + ch]
                v = _catmull(p0, p1, p2, p3, t)
                tmp[drow + x * 4 + ch] = int(clamp(v, 0, 255))
    # --- vertical pass: h -> nh
    out = bytearray(nw * nh * 4)
    yr = float(h) / nh
    for y in range(nh):
        sy = (y + 0.5) * yr - 0.5
        j1 = int(math.floor(sy))
        t = sy - j1
        drow = y * nw * 4
        for x in range(nw):
            for ch in range(4):
                p0 = tmp[(max(0, j1 - 1) * nw + x) * 4 + ch]
                p1 = tmp[(max(0, j1) * nw + x) * 4 + ch]
                p2 = tmp[(min(h - 1, j1 + 1) * nw + x) * 4 + ch]
                p3 = tmp[(min(h - 1, j1 + 2) * nw + x) * 4 + ch]
                v = _catmull(p0, p1, p2, p3, t)
                out[drow + x * 4 + ch] = int(clamp(v, 0, 255))
    return out


def block_mean(w, h, rgba, bx, by):
    """Return a grid of (mean_r, mean_g, mean_b) for bx-by-by pixel blocks."""
    cols = w // bx
    rows = h // by
    grid = []
    for r in range(rows):
        row = []
        for c in range(cols):
            sr = sg = sb = 0
            for y in range(r * by, (r + 1) * by):
                base = y * w * 4
                for x in range(c * bx, (c + 1) * bx):
                    o = base + x * 4
                    sr += rgba[o]
                    sg += rgba[o + 1]
                    sb += rgba[o + 2]
            n = bx * by
            row.append((sr // n, sg // n, sb // n))
        grid.append(row)
    return grid


def palette_histogram(w, h, rgba, step=4, bits=4):
    """Coarse colour histogram (quantised per channel) -- for art matching."""
    shift = 8 - bits
    counts = {}
    for y in range(0, h, step):
        base = y * w * 4
        for x in range(0, w, step):
            o = base + x * 4
            key = (rgba[o] >> shift, rgba[o + 1] >> shift, rgba[o + 2] >> shift)
            counts[key] = counts.get(key, 0) + 1
    top = sorted(counts.items(), key=lambda kv: -kv[1])[:24]
    scale = 255.0 / (2 ** bits - 1)
    return [((int(k[0] * scale), int(k[1] * scale), int(k[2] * scale)), v)
            for k, v in top]