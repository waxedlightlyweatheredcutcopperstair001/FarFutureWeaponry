"""Kraken Missile: an enlarged Hellfire (2.2 m long, 0.25 m across) in matte black with red bands, red fin tips and a red
ring under a clear, faintly red glass nose dome. In the dome, a lump of red flesh, veined and wet-looking, held on a
bundle of wires that run from a bulkhead plate and end in electrodes stuck into it. The flesh ("flesh" transform)
throbs and its veins glow (ModuleFFWPulse).

Run with tools/model.py KrakenMissile. Writes model.mu, Kraken.png (+_NRM), KrakenFlesh.png (+_NRM, _Emissive) and
KrakenGlass.png.

Axes (Blender): the missile points along +Y (KSP's forward), its back (the rail) is +Z (KSP's up). Body texture
(2048 x 1024): v 0-0.5 the body (u along its length, v round it); v 0.5-1: fins (u 0-0.5: chord, v: root to tip) and
swatches (u 0.5-1, six columns: dark metal, red metal, red wire, black wire, grey wire, electrode silver).
"""
import math, os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import bmesh
import bpy
import mathutils.noise as mnoise
import numpy as np
from mathutils import Vector, Matrix

import mulib

L = 2.2                  # length
R = 0.125                # body radius
Y_TAIL, Y_NOSE = -1.1, 1.1
Y_BODY0, Y_BODY1 = -1.075, 0.80       # the plain cylinder
Y_RING0, Y_RING1 = 0.80, 0.855        # red ring under the dome
DOME_R, DOME_LEN = 0.118, 0.235       # the dome: half an ellipsoid on the ring
Y_PLATE = 0.85
FLESH_C, FLESH_R = Vector((0, 0.985, 0)), 0.056
SEG = 32
SEED = 114
rng = np.random.default_rng(SEED)

BANDS = [(0.64, 0.68), (-0.56, -0.52)]          # red bands (along y)
SEAMS = [0.25, -0.2]                              # section joints
SWATCH = {'dark': 0, 'red': 1, 'wire_red': 2, 'wire_black': 3, 'wire_grey': 4, 'silver': 5}


def body_uv(y, theta):
    return ((y - Y_TAIL) / L, (theta / (2 * math.pi)) * 0.5)


def swatch_uv(name, s, t):
    k = SWATCH[name]
    return (0.5 + (k + 0.15 + 0.7 * s) / 6 * 0.5, 0.5 + 0.05 + 0.9 * t * 0.5)


def fin_uv(chord, span):
    return (0.02 + 0.46 * chord, 0.5 + 0.02 + 0.46 * span)

# ---------------------------------------------------------------- textures

def body_texture(out):
    W, H = 2048, 1024
    alb = np.zeros((H, W, 3), np.float32)
    hgt = np.zeros((H, W), np.float32)
    black = np.array([0.045, 0.045, 0.05]); red = np.array([0.55, 0.04, 0.04])
    # Body (rows 0-511): matte black, red bands and ring, a couple of section joints and access panels.
    body = alb[:512]
    body[:] = black
    def col(y): return int(round((y - Y_TAIL) / L * W))
    for a, b in BANDS + [(Y_RING0 - 0.01, Y_RING1 + 0.02)]:
        body[:, col(a):col(b)] = red
    for y in SEAMS + [Y_BODY0 + 0.03]:
        c = col(y); hgt[:512, c - 2:c + 2] = -3; body[:, c - 2:c + 2] *= 0.6
    for x0, x1, v0, v1 in ((-0.15, 0.05, 0.08, 0.18), (-0.15, 0.05, 0.32, 0.42), (0.35, 0.5, 0.20, 0.30)):
        a, b, c, d = col(x0), col(x1), int(v0 * 512), int(v1 * 512)
        for sl in ((slice(c, d), slice(a, a + 3)), (slice(c, d), slice(b - 3, b)), (slice(c, c + 3), slice(a, b)), (slice(d - 3, d), slice(a, b))):
            hgt[sl] = -2; body[sl] *= 0.5
    body *= (0.9 + 0.2 * mulib.value_noise(512, W, 40, SEED))[..., None]
    # Fins (rows 512-1023, cols 0-1023): black, the outer quarter of the span red.
    fins = alb[512:, :1024]
    fins[:] = black
    fins[int(512 * ((0.02 + 0.46 * 0.74) / 0.5)) - 0:, :] = red
    # Swatches (rows 512-1023, cols 1024-2047).
    sw = alb[512:, 1024:]
    cols = [(0.08, 0.08, 0.09), (0.5, 0.04, 0.04), (0.6, 0.06, 0.05), (0.03, 0.03, 0.03), (0.32, 0.32, 0.33), (0.42, 0.42, 0.43)]
    w = 1024 // 6
    for k, c in enumerate(cols):
        sw[:, k * w:(k + 1) * w] = c
    alb = np.clip(alb, 0, 1)
    main = mulib.save_png('Kraken', alb, out, shrink_by=2)          # (flat colours: half size is plenty)
    nrm = mulib.save_png('Kraken_NRM', mulib.normal_map(hgt, 0.4), out, data=True, shrink_by=2)
    return main, nrm


def noise3(p, seed):
    """Value noise in 3D for an (..., 3) array of points: smooth, in [0, 1]."""
    i = np.floor(p).astype(np.int64); f = p - i
    f = f * f * (3 - 2 * f)
    def h(ix, iy, iz):
        n = (ix * 73856093) ^ (iy * 19349663) ^ (iz * 83492791) ^ (seed * 2654435761)
        n = (n ^ (n >> 13)) * 1274126177
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0
    x, y, z = i[..., 0], i[..., 1], i[..., 2]
    fx, fy, fz = f[..., 0], f[..., 1], f[..., 2]
    c = lambda dx, dy, dz: h(x + dx, y + dy, z + dz)
    l = lambda a, b, t: a + (b - a) * t
    return l(l(l(c(0, 0, 0), c(1, 0, 0), fx), l(c(0, 1, 0), c(1, 1, 0), fx), fy),
             l(l(c(0, 0, 1), c(1, 0, 1), fx), l(c(0, 1, 1), c(1, 1, 1), fx), fy), fz)


def fbm(p, seed, octaves=4):
    s, a, tot = 0, 0.5, 0
    for k in range(octaves):
        s = s + a * noise3(p, seed + k); tot += a; p = p * 2.03 + 11.7; a *= 0.5
    return s / tot


def flesh_texture(out):
    """Painted on the sphere directions behind the flesh's spherical UVs, so there's no seam."""
    W, H = 1024, 512
    u = (np.arange(W) + 0.5) / W; v = (np.arange(H) + 0.5) / H
    lon = (u - 0.5) * 2 * math.pi; lat = (v - 0.5) * math.pi
    LON, LAT = np.meshgrid(lon, lat)
    d = np.dstack([np.cos(LAT) * np.cos(LON), np.sin(LAT), np.cos(LAT) * np.sin(LON)])
    base = fbm(d * 3.0 + 5, SEED)
    mott = fbm(d * 7.0 + 2, SEED + 10)
    v1 = np.abs(fbm(d * 2.6 + 9, SEED + 20, 3) - 0.5)
    v2 = np.abs(fbm(d * 6.0 + 3, SEED + 30, 2) - 0.5)
    veins = np.clip(1 - v1 / 0.022, 0, 1) ** 2 + 0.35 * np.clip(1 - v2 / 0.012, 0, 1) ** 2
    veins = np.clip(veins, 0, 1)
    deep, pink, vein = np.array([0.42, 0.05, 0.07]), np.array([0.86, 0.36, 0.38]), np.array([0.3, 0.03, 0.14])
    t = np.clip((base - 0.35) * 2.2, 0, 1)[..., None]
    alb = deep * (1 - t) + pink * t
    alb *= (0.8 + 0.4 * mott)[..., None]
    alb = alb * (1 - veins[..., None] * 0.7) + vein * veins[..., None] * 0.7
    hgt = (base - 0.5) * 6 + veins * 2.5 + (mott - 0.5) * 2
    halo = np.clip(1 - v1 / 0.07, 0, 1) ** 2                      # (a soft glow round the big veins)
    emi = np.clip(veins * 1.0 + halo * 0.45 + 0.12, 0, 1)
    main = mulib.save_png('KrakenFlesh', np.clip(alb, 0, 1), out)
    nrm = mulib.save_png('KrakenFlesh_NRM', mulib.normal_map(hgt.astype(np.float32), 0.5), out, data=True)
    glow = mulib.save_png('KrakenFlesh_Emissive', np.dstack([emi, emi * 0.25, emi * 0.2]), out)
    return main, nrm, glow

# ---------------------------------------------------------------- geometry

class B:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UV')

    def v(self, co): return self.bm.verts.new(co)

    def face(self, verts, uvs, smooth):
        f = self.bm.faces.new(verts)
        f.smooth = smooth
        for loop, uv in zip(f.loops, uvs): loop[self.uv].uv = uv
        return f


def ring_pt(y, r, j):
    th = 2 * math.pi * j / SEG
    return Vector((r * math.cos(th), y, r * math.sin(th))), th


def band(b, y0, r0, y1, r1, smooth, uvf):
    """A band of the body of revolution from (y0, r0) to (y1, r1), faces outward."""
    a = [b.v(ring_pt(y0, r0, j)[0]) for j in range(SEG)]
    c = [b.v(ring_pt(y1, r1, j)[0]) for j in range(SEG)]
    for j in range(SEG):
        j2 = (j + 1) % SEG
        th0, th1 = 2 * math.pi * j / SEG, 2 * math.pi * (j + 1) / SEG
        # (j -> j+1 runs from +X towards +Z: with +Y forward that's clockwise seen from outside, so go back the other way)
        b.face([a[j], c[j], c[j2], a[j2]], [uvf(y0, th0), uvf(y1, th0), uvf(y1, th1), uvf(y0, th1)], smooth)


def body(b):
    dark = lambda y, th: swatch_uv('dark', th / (2 * math.pi), (y - Y_TAIL) / 0.05)
    # Tail: nozzle disk, the nozzle's lip, a chamfer onto the body.
    yt = Y_TAIL
    centre = b.v((0, yt + 0.02, 0))
    lip = [b.v(ring_pt(yt + 0.02, 0.06, j)[0]) for j in range(SEG)]
    for j in range(SEG):
        j2 = (j + 1) % SEG
        b.face([centre, lip[j], lip[j2]], [swatch_uv('dark', 0.5, 0.5), swatch_uv('dark', 0, 1), swatch_uv('dark', 1, 1)], False)   # (faces back)
    band(b, yt + 0.02, 0.06, yt, 0.075, False, dark)
    band(b, yt, 0.075, yt, 0.105, False, dark)
    band(b, yt, 0.105, Y_BODY0, R, True, body_uv)
    band(b, Y_BODY0, R, Y_BODY1, R, True, body_uv)
    # The ring under the dome: a step out, the ring, a lip in to the glass.
    band(b, Y_RING0, R, Y_RING0 + 0.005, R + 0.004, False, body_uv)
    band(b, Y_RING0 + 0.005, R + 0.004, Y_RING1, R + 0.004, True, body_uv)
    band(b, Y_RING1, R + 0.004, Y_RING1, DOME_R, False, body_uv)


def dome(b):
    rings = 14
    pts = []
    for k in range(rings + 1):
        t = (math.pi / 2) * k / rings
        pts.append((Y_RING1 + DOME_LEN * math.sin(t), DOME_R * math.cos(t)))
    rows = [[b.v(ring_pt(y, r, j)[0]) for j in range(SEG)] if r > 1e-5 else None for y, r in pts]
    tipv = b.v((0, pts[-1][0], 0))
    for k in range(rings):
        for j in range(SEG):
            j2 = (j + 1) % SEG
            u0, u1, v0, v1 = j / SEG, (j + 1) / SEG, k / rings, (k + 1) / rings
            if rows[k + 1] is None:
                b.face([rows[k][j], tipv, rows[k][j2]], [(u0, v0), (u0, v1), (u1, v0)], True)
            else:
                b.face([rows[k][j], rows[k + 1][j], rows[k + 1][j2], rows[k][j2]], [(u0, v0), (u0, v1), (u1, v1), (u1, v0)], True)


def dome_radius_at(y):
    if y < Y_RING1: return DOME_R
    s = (y - Y_RING1) / DOME_LEN
    return 0 if s >= 1 else DOME_R * math.sqrt(1 - s * s)


def fin(b, theta, y_le_root, y_te, root_chord, tip_chord, span, sweep, thick):
    """A flat fin standing out from the body at angle theta: root along the body, tip `span` out, leading edge swept back."""
    out = Vector((math.cos(theta), 0, math.sin(theta)))
    side = Vector((0, 1, 0)).cross(out).normalized()
    r0 = R - 0.004
    pts = [(y_te, 0), (y_te + root_chord, 0), (y_te + root_chord - sweep, span), (y_te + root_chord - sweep - tip_chord, span)]
    # (chord coordinate for the UVs: from the trailing edge)
    def P(y, s, k): return Vector((0, y, 0)) + out * (r0 + s) + side * (k * thick / 2 * (1 - 0.5 * s / span))
    for k in (1, -1):
        vs = [b.v(P(y, s, k)) for y, s in pts]
        uvs = [fin_uv((y - y_te) / root_chord, s / span) for y, s in pts]
        if k < 0: vs.reverse(); uvs.reverse()
        b.face(vs, uvs, False)
    # Edges.
    for i in range(4):
        i2 = (i + 1) % 4
        (ya, sa), (yb, sb) = pts[i], pts[i2]
        q = [b.v(P(ya, sa, 1)), b.v(P(yb, sb, 1)), b.v(P(yb, sb, -1)), b.v(P(ya, sa, -1))]
        uv = [fin_uv(0.5, 0.95)] * 4
        f = b.face(q, uv, False)
        # (make sure it faces outward: away from the fin's middle)
        mid = sum((P(y, s, 0) for y, s in pts), Vector()) / 4
        if f.calc_center_median().dot(f.normal) - mid.dot(f.normal) < 0 and (f.calc_center_median() - mid).dot(f.normal) < 0:
            f.normal_flip()


def fins(b):
    for k in range(4):
        th = math.radians(45 + 90 * k)       # an X, clear of the rail on top
        fin(b, th, 0, -1.07, 0.44, 0.16, 0.115, 0.22, 0.014)     # long wings at the tail
        fin(b, th, 0, 0.44, 0.15, 0.07, 0.055, 0.06, 0.010)      # small canards behind the nose


def plate(b):
    """The bulkhead inside the dome the wires come from, with a few connector blocks."""
    c = b.v((0, Y_PLATE + 0.004, 0))
    rim = [b.v(ring_pt(Y_PLATE + 0.004, DOME_R - 0.002, j)[0]) for j in range(SEG)]
    for j in range(SEG):
        j2 = (j + 1) % SEG
        b.face([c, rim[j2], rim[j]], [swatch_uv('dark', 0.5, 0.5)] * 3, False)   # (faces forward, at the flesh)
    blocks = []
    for k in range(5):
        a = 2 * math.pi * k / 5 + 0.3
        rr = 0.05 if k else 0
        p = Vector((rr * math.cos(a), Y_PLATE + 0.004, rr * math.sin(a)))
        box(b, p, 0.014, 0.012, 0.016)
        blocks.append(p + Vector((0, 0.012, 0)))
    return blocks


def box(b, p, w, h, d):
    """A box sitting on the plate at p (w across x, h up the missile's axis, d across z)."""
    xs, zs = (-w / 2, w / 2), (-d / 2, d / 2)
    lo = [b.v(p + Vector((x, 0, z))) for x, z in ((xs[0], zs[0]), (xs[1], zs[0]), (xs[1], zs[1]), (xs[0], zs[1]))]
    hi = [b.v(p + Vector((x, h, z))) for x, z in ((xs[0], zs[0]), (xs[1], zs[0]), (xs[1], zs[1]), (xs[0], zs[1]))]
    uv = [swatch_uv('dark', 0.2, 0.2), swatch_uv('dark', 0.8, 0.2), swatch_uv('dark', 0.8, 0.8), swatch_uv('dark', 0.2, 0.8)]
    f = b.face(list(reversed(hi)), uv, False)
    if f.normal.y < 0: f.normal_flip()
    for i in range(4):
        i2 = (i + 1) % 4
        f = b.face([lo[i], lo[i2], hi[i2], hi[i]], uv, False)
        if (f.calc_center_median() - (p + Vector((0, h / 2, 0)))).dot(f.normal) < 0: f.normal_flip()


def flesh_mesh():
    """The lump: an icosphere pushed out by noise and a few lobes, squashed a little along the axis, kept in the dome."""
    b = B()
    bmesh.ops.create_icosphere(b.bm, subdivisions=4, radius=1.0)
    lobes = [Vector(rng.normal(size=3)).normalized() for _ in range(6)]
    lobew = rng.uniform(0.12, 0.3, 6)
    for vert in b.bm.verts:
        d = vert.co.normalized()
        n = mnoise.fractal(d * 1.8 + Vector((3.1, 7.2, 1.3)), 1.0, 2.0, 3, noise_basis='PERLIN_ORIGINAL')
        wr = mnoise.noise(d * 5.0 + Vector((9, 2, 4)), noise_basis='PERLIN_ORIGINAL')
        lobe = sum(w * math.exp(-((1 - d.dot(l)) / 0.25) ** 2) for l, w in zip(lobes, lobew))
        r = FLESH_R * (1 + 0.18 * n + 0.05 * wr + lobe)
        p = Vector((d.x * r * 1.05, d.y * r * 0.8, d.z * r * 0.95))
        # Keep it inside the dome (with a margin), in the dome's own terms.
        w = FLESH_C + p
        lim = dome_radius_at(w.y) - 0.008
        rad = math.hypot(w.x, w.z)
        if w.y > Y_RING1 + DOME_LEN - 0.01: p *= 0.9
        if lim > 0 and rad > lim: p.x *= lim / rad; p.z *= lim / rad
        vert.co = p
    for f in b.bm.faces:
        f.smooth = True
        us = []
        for loop in f.loops:
            d = loop.vert.co.normalized()
            u = math.atan2(d.z, d.x) / (2 * math.pi) + 0.5
            v = math.asin(max(-1, min(1, d.y))) / math.pi + 0.5
            us.append([u, v])
        if max(x[0] for x in us) - min(x[0] for x in us) > 0.5:
            for x in us:
                if x[0] < 0.5: x[0] += 1
        for loop, uv in zip(f.loops, us): loop[b.uv].uv = uv
    bmesh.ops.recalc_face_normals(b.bm, faces=b.bm.faces)
    b.bm.normal_update()
    return b


def flesh_surface(b_flesh, toward):
    """A point on the flesh (in its own space) roughly in a direction: the vertex best lined up with it."""
    best, bd = None, -2
    for v in b_flesh.bm.verts:
        d = v.co.normalized().dot(toward)
        if d > bd: bd, best = d, v
    return best.co.copy(), best.normal.copy()


def tube(b, pts, radius, swatch, sides=6):
    """A wire along a polyline (smooth shading), its frame carried along without twisting."""
    n = len(pts)
    tang = [(pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(n)]
    up = Vector((0, 0, 1)) if abs(tang[0].z) < 0.9 else Vector((1, 0, 0))
    nrm = tang[0].cross(up).normalized()
    rings = []
    for i in range(n):
        if i:
            nrm = (nrm - tang[i] * nrm.dot(tang[i])).normalized()
        bin_ = tang[i].cross(nrm)
        rings.append([b.v(pts[i] + (nrm * math.cos(2 * math.pi * k / sides) + bin_ * math.sin(2 * math.pi * k / sides)) * radius) for k in range(sides)])
    for i in range(n - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            uv = [swatch_uv(swatch, k / sides, i / n), swatch_uv(swatch, (k + 1) / sides, i / n),
                  swatch_uv(swatch, (k + 1) / sides, (i + 1) / n), swatch_uv(swatch, k / sides, (i + 1) / n)]
            f = b.face([rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]], uv, True)
            if (f.calc_center_median() - (pts[i] + pts[i + 1]) / 2).dot(f.normal) < 0: f.normal_flip()


def bezier(p0, p1, p2, p3, n):
    out = []
    for i in range(n + 1):
        t = i / n
        out.append(p0 * (1 - t) ** 3 + p1 * 3 * t * (1 - t) ** 2 + p2 * 3 * t * t * (1 - t) + p3 * t ** 3)
    return out


def wires(b, b_flesh, blocks):
    colours = ['wire_red', 'wire_black', 'wire_grey', 'wire_red', 'wire_black', 'wire_red', 'wire_grey', 'wire_black',
               'wire_red', 'wire_black', 'wire_grey', 'wire_red', 'wire_red', 'wire_black']
    for i, sw in enumerate(colours):
        start = blocks[i % len(blocks)] + Vector((rng.uniform(-0.004, 0.004), 0, rng.uniform(-0.004, 0.004)))
        # Aim at the back and sides of the lump.
        a = 2 * math.pi * i / len(colours) + rng.uniform(-0.3, 0.3)
        toward = Vector((math.cos(a) * 0.9, -0.55 + rng.uniform(-0.25, 0.35), math.sin(a) * 0.9)).normalized()
        sp, sn = flesh_surface(b_flesh, toward)
        end = FLESH_C + sp
        nw = sn.normalized()
        p1 = start + Vector((0, 0.045, 0)) + Vector((start.x, 0, start.z)) * 0.8
        p2 = end + nw * 0.035
        pts = bezier(start, p1, p2, end - nw * 0.002, 12)
        # (keep every point inside the dome)
        for k, p in enumerate(pts):
            lim = dome_radius_at(p.y) - 0.006
            rad = math.hypot(p.x, p.z)
            if lim > 0 and rad > lim: pts[k] = Vector((p.x * lim / rad, p.y, p.z * lim / rad))
        tube(b, pts, rng.uniform(0.0038, 0.0058), sw)
        # An electrode where it goes in.
        tube(b, [end - nw * 0.004, end + nw * 0.004, end + nw * 0.008], 0.0045, 'silver', 8)

# ---------------------------------------------------------------- main

def empty(name, parent, loc, rot=(0, 0, 0)):
    ob = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent; ob.location = loc; ob.rotation_euler = rot
    return ob


def collider(parent):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=R, radius2=R, depth=L - 0.05)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
    me = bpy.data.meshes.new('collider'); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('collider', me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    ob.muproperties.collider = 'MU_COL_MESH'; ob.muproperties.isConvex = True
    ob.hide_render = True


def main():
    o = mulib.args()
    out = os.path.abspath(o['out']); os.makedirs(out, exist_ok=True)
    mulib.reset()
    body_tex, body_nrm = body_texture(out)
    fl_tex, fl_nrm, fl_glow = flesh_texture(out)
    glass_tex = mulib.save_png('KrakenGlass', np.ones((32, 32, 3), np.float32), out)
    m_body = mulib.ksp_material('Kraken', body_tex, body_nrm, spec=0.25, shininess=0.3)
    m_flesh = mulib.ksp_material('KrakenFlesh', fl_tex, fl_nrm, fl_glow, spec=0.6, shininess=0.7)
    m_glass = mulib.glass_material('KrakenGlass', glass_tex, (0.55, 0.16, 0.16), 0.45)   # (dark red, half see-through)

    model = mulib.root('model')
    b = B(); body(b); fins(b); plate_blocks = plate(b)
    bf = flesh_mesh()
    wires(b, bf, plate_blocks)
    tris = sum(len(f.verts) - 2 for f in b.bm.faces)
    mulib.mesh_object('KrakenBody', b.bm, m_body, model)
    bd = B(); dome(bd)
    tris += sum(len(f.verts) - 2 for f in bd.bm.faces)
    mulib.mesh_object('KrakenDome', bd.bm, m_glass, model)
    tris += sum(len(f.verts) - 2 for f in bf.bm.faces)
    fl = mulib.mesh_object('flesh', bf.bm, m_flesh, model)
    fl.location = FLESH_C
    # BDArmory's exhaust and boost points, at the tail, turned as on its own missiles.
    for name in ('boostTransform', 'exhaustTransform'):
        empty(name, model, (0, Y_TAIL + 0.02, 0), (math.radians(90), 0, 0))
    collider(model)
    print(f'Kraken missile: {tris} triangles.')
    mulib.export(model, os.path.join(out, 'model.mu'))

    if o['preview']:
        bsdf = m_flesh.node_tree.nodes['Principled BSDF']
        te = m_flesh.node_tree.nodes.new('ShaderNodeTexImage'); te.image = fl_glow
        m_flesh.node_tree.links.new(te.outputs['Color'], bsdf.inputs['Emission Color'])
        def glow(k): return lambda: bsdf.inputs['Emission Strength'].__setattr__('default_value', k)
        mulib.render_previews(o['preview'], [
            ('kraken-side.png', (2.2, -1.2, 0.9), glow(0.5)),
            ('kraken-dome.png', (0.75, 1.45, 0.25), glow(1.5), (0, 0.95, 0)),
            ('kraken-dome-top.png', (0.05, 0.95, 0.85), glow(0.5), (0, 0.95, 0)),
            ('kraken-rear.png', (-1.0, -3.0, 1.1), glow(0.5)),
        ], target=(0, 0.3, 0))


main()
