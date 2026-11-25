"""Mushroom Bomb: the cartoon bomb from the reference (itself after Fat Man), at two-thirds Fat Man scale (2.2 m long,
1.0 m across): a fat periwinkle egg with a rounded nose cap split off by a dark seam, a small round knob at the tip,
an egg-shaped rear running into a short tail cone, and four tall, swept, darker purple fins. Light KSP detail: a ring
of bolts either side of the seam, a couple of faint panel lines, an access hatch on each side, two lugs on top.

Run with tools/model.py MushroomBomb. Writes model.mu, MushroomBomb.png (+_NRM).

Axes (Blender): the bomb points along +Y, its back (the rail) is +Z. Texture (1024 x 1024): v 0-0.5 the body of
revolution (u along its length, v round it, kept clear of the swatches); v 0.5-1 swatches in four columns.
"""
import math, os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import bmesh
import bpy
import numpy as np
from mathutils import Vector

import mulib

R = 0.5
Y_TAIL, Y_SEAM, NOSE_LEN = -1.1, 0.45, 0.55
KNOB_R = 0.065
Y_NOSE = Y_SEAM + 0.012 + NOSE_LEN + KNOB_R * 0.9
L = Y_NOSE - Y_TAIL
SEG = 48
SEED = 2
SWATCH = {'fin': 0, 'body': 1, 'metal': 2, 'dark': 3}
PERI = np.array([0.6, 0.64, 0.97])         # the cartoon's periwinkle
PURPLE = np.array([0.46, 0.34, 0.84])      # its fins


def body_uv(y, th):
    return ((y - Y_TAIL) / L, 0.01 + (th / (2 * math.pi)) * 0.47)


def sw_uv(name, s=0.5, t=0.5):
    k = SWATCH[name]
    return ((k + 0.1 + 0.8 * min(1, max(0, s))) / 4, 0.5 + 0.05 + 0.9 * min(1, max(0, t)) * 0.5)


def radius_at(y):
    """The outline: tail cone, egg-shaped rear, a barely bulging body, the seam, the rounded nose."""
    if y < -0.85: return 0.15 + (0.22 - 0.15) * (y - Y_TAIL) / (-0.85 - Y_TAIL)
    if y < -0.30:
        s = (y + 0.30) / 0.55
        return 0.22 + (R - 0.22) * math.sqrt(max(0, 1 - s * s))
    if y <= Y_SEAM: return R * (1 + 0.01 * math.cos((y - 0.075) / 0.375 * math.pi / 2))
    s = (y - Y_SEAM - 0.012) / NOSE_LEN
    return R * math.sqrt(max(0, 1 - s * s))

# ---------------------------------------------------------------- texture

def texture(out):
    N = 1024
    alb = np.zeros((N, N, 3), np.float32); gloss = np.zeros((N, N), np.float32); hgt = np.zeros((N, N), np.float32)
    body = alb[:512]; bh = hgt[:512]; bg = gloss[:512]
    body[:] = PERI; bg[:] = 0.5
    def col(y): return int(round((y - Y_TAIL) / L * N))
    def row(th): return int(round((0.01 + th / (2 * math.pi) * 0.47) * N))
    # The seam: a dark line, with a ring of bolts either side of it.
    a, b = col(Y_SEAM - 0.004), col(Y_SEAM + 0.016)
    body[:, a:b] = (0.12, 0.13, 0.3); bh[:, a:b] = -3
    yy, xx = np.mgrid[0:512, 0:N]
    for yb in (Y_SEAM - 0.03, Y_SEAM + 0.045):
        cx = col(yb)
        for k in range(28):
            cy = row(2 * math.pi * (k + 0.5) / 28)
            d = np.sqrt(((xx[:, cx - 8:cx + 8] - cx) * 1.0) ** 2 + ((yy[:, cx - 8:cx + 8] - cy) * 2.9) ** 2)
            m = d < 5
            bh[:, cx - 8:cx + 8][m] += 1.5 * (1 - d[m] / 5)
            body[:, cx - 8:cx + 8][m] *= 0.88
    # Faint panel lines, and an access hatch on each side.
    for y in (-0.30, 0.0):
        c = col(y); bh[:, c - 1:c + 1] -= 1.5; body[:, c - 1:c + 1] *= 0.85
    for th in (0.0, math.pi):
        x0, x1, r0, r1 = col(-0.2), col(0.22), row(th) - 34, row(th) + 34
        r0, r1 = max(r0, 0), min(r1, 511)
        for sl in ((slice(r0, r1), slice(x0, x0 + 2)), (slice(r0, r1), slice(x1 - 2, x1)), (slice(r0, r0 + 2), slice(x0, x1)), (slice(r1 - 2, r1), slice(x0, x1))):
            bh[sl] -= 1.5; body[sl] *= 0.85
    body *= (0.96 + 0.08 * mulib.value_noise(512, N, 16, SEED))[..., None]
    # Swatches.
    w = N // 4
    for name, c, g in (('fin', PURPLE, 0.45), ('body', PERI, 0.5), ('metal', np.array([0.35, 0.36, 0.4]), 0.6), ('dark', np.array([0.12, 0.13, 0.3]), 0.3)):
        k = SWATCH[name]; alb[512:, k * w:(k + 1) * w] = c; gloss[512:, k * w:(k + 1) * w] = g
    main = mulib.save_png('MushroomBomb', np.clip(alb, 0, 1), out, alpha=gloss)
    nrm = mulib.save_png('MushroomBomb_NRM', mulib.normal_map(hgt, 0.3), out, data=True)
    return main, nrm

# ---------------------------------------------------------------- geometry

class B:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UV')

    def v(self, co): return self.bm.verts.new(co)

    def face(self, verts, uvs, smooth, outward=None):
        f = self.bm.faces.new(verts)
        f.smooth = smooth
        f.normal_update()       # (a new face has no normal until asked: the outward check needs it)
        for loop, uv in zip(f.loops, uvs): loop[self.uv].uv = uv
        if outward is not None and f.normal.dot(outward) < 0: f.normal_flip()
        return f


def P(y, r, th):
    return Vector((r * math.cos(th), y, r * math.sin(th)))


def band(b, y0, r0, y1, r1, smooth, uvf=body_uv):
    a = [b.v(P(y0, r0, 2 * math.pi * j / SEG)) for j in range(SEG)]
    c = [b.v(P(y1, r1, 2 * math.pi * j / SEG)) for j in range(SEG)]
    for j in range(SEG):
        j2 = (j + 1) % SEG
        t0, t1 = 2 * math.pi * j / SEG, 2 * math.pi * (j + 1) / SEG
        b.face([a[j], c[j], c[j2], a[j2]], [uvf(y0, t0), uvf(y1, t0), uvf(y1, t1), uvf(y0, t1)], smooth)


def strip(b, ys, smooth=True):
    """Bands along the outline through these stations (shared rings: smooth across them)."""
    rings = [[b.v(P(y, radius_at(y), 2 * math.pi * j / SEG)) for j in range(SEG)] for y in ys]
    for i in range(len(ys) - 1):
        for j in range(SEG):
            j2 = (j + 1) % SEG
            t0, t1 = 2 * math.pi * j / SEG, 2 * math.pi * (j + 1) / SEG
            y0, y1 = ys[i], ys[i + 1]
            b.face([rings[i][j], rings[i + 1][j], rings[i + 1][j2], rings[i][j2]],
                   [body_uv(y0, t0), body_uv(y1, t0), body_uv(y1, t1), body_uv(y0, t1)], smooth)


def lin(a, b, n): return [a + (b - a) * k / n for k in range(n + 1)]


def body(b):
    # Tail cap: a flat end with a small raised ring.
    c = b.v((0, Y_TAIL - 0.01, 0))
    rim = [b.v(P(Y_TAIL - 0.01, 0.1, 2 * math.pi * j / SEG)) for j in range(SEG)]
    for j in range(SEG):
        b.face([c, rim[j], rim[(j + 1) % SEG]], [sw_uv('dark')] * 3, False, Vector((0, -1, 0)))
    band(b, Y_TAIL - 0.01, 0.1, Y_TAIL - 0.01, 0.12, False, lambda y, t: sw_uv('metal', t / 6.3, 0.5))
    band(b, Y_TAIL - 0.01, 0.12, Y_TAIL, 0.15, False, lambda y, t: sw_uv('metal', t / 6.3, 0.5))
    # Tail cone and egg-shaped rear, the body up to the seam (smooth throughout).
    rear = [-0.30 + 0.55 * -math.cos(math.pi / 2 * k / 10) for k in range(11)]      # (closer stations where it curves)
    strip(b, lin(Y_TAIL, -0.85, 3)[:-1] + sorted(rear)[:-1] + lin(-0.30, Y_SEAM, 8))
    # The seam: a small groove between body and nose.
    rs = radius_at(Y_SEAM)
    band(b, Y_SEAM, rs, Y_SEAM, rs - 0.01, False)
    band(b, Y_SEAM, rs - 0.01, Y_SEAM + 0.012, rs - 0.01, False)
    band(b, Y_SEAM + 0.012, rs - 0.01, Y_SEAM + 0.012, R, False)
    # The nose: a rounded cap, closed round the knob.
    ys = [Y_SEAM + 0.012 + NOSE_LEN * math.sin(math.pi / 2 * k / 14) for k in range(14)]
    strip(b, ys)
    yt = ys[-1]
    c = b.v((0, yt + 0.004, 0))
    last = [b.v(P(yt, radius_at(yt), 2 * math.pi * j / SEG)) for j in range(SEG)]
    for j in range(SEG):
        b.face([c, last[(j + 1) % SEG], last[j]], [body_uv(yt, 0)] * 3, True, Vector((0, 1, 0)))
    knob(b, Vector((0, Y_SEAM + 0.012 + NOSE_LEN - KNOB_R * 0.1, 0)))


def knob(b, centre):
    rings, seg = 8, 16
    rows = []
    for k in range(1, rings):
        t = -math.pi / 2 + math.pi * k / rings
        rows.append([b.v(centre + Vector((KNOB_R * math.cos(t) * math.cos(2 * math.pi * j / seg), KNOB_R * math.sin(t),
                                          KNOB_R * math.cos(t) * math.sin(2 * math.pi * j / seg)))) for j in range(seg)])
    bot, top = b.v(centre - Vector((0, KNOB_R, 0))), b.v(centre + Vector((0, KNOB_R, 0)))
    uv = [sw_uv('body')] * 4
    for k in range(len(rows) - 1):
        for j in range(seg):
            j2 = (j + 1) % seg
            q = [rows[k][j], rows[k + 1][j], rows[k + 1][j2], rows[k][j2]]
            b.face(q, uv, True, sum((v.co for v in q), Vector()) / 4 - centre)
    for j in range(seg):
        j2 = (j + 1) % seg
        b.face([bot, rows[0][j], rows[0][j2]], uv[:3], True, Vector((0, -1, 0)))
        b.face([top, rows[-1][j2], rows[-1][j]], uv[:3], True, Vector((0, 1, 0)))


def fin(b, th):
    """A tall swept fin standing out from the tail cone and rear, at angle th."""
    out = Vector((math.cos(th), 0, math.sin(th))); side = Vector((-math.sin(th), 0, math.cos(th)))
    t = 0.012
    pts = [(Y_TAIL + 0.01, radius_at(Y_TAIL + 0.01) - 0.01), (-0.62, radius_at(-0.62) - 0.02), (-0.92, 0.66), (Y_TAIL - 0.04, 0.66)]
    def Q(y, r, s): return Vector((0, y, 0)) + out * r + side * (s * t * (1 - 0.4 * (r - 0.2) / 0.46))
    for s in (1, -1):
        b.face([b.v(Q(y, r, s)) for y, r in pts], [sw_uv('fin', (y - Y_TAIL) * 1.6, (r - 0.15) * 2) for y, r in pts], False, side * s)
    mid = sum((Q(y, r, 0) for y, r in pts), Vector()) / 4
    for i in range(4):
        (ya, ra), (yb, rb) = pts[i], pts[(i + 1) % 4]
        q = [b.v(Q(ya, ra, 1)), b.v(Q(yb, rb, 1)), b.v(Q(yb, rb, -1)), b.v(Q(ya, ra, -1))]
        b.face(q, [sw_uv('fin')] * 4, False, (Q(ya, ra, 0) + Q(yb, rb, 0)) / 2 - mid)


def lug(b, y):
    """A lug on top (the rail's hooks)."""
    x, z0, z1, hy = 0.035, R - 0.01, R + 0.035, 0.03
    corners = [Vector((sx * x, y + sy * hy, z)) for z in (z0, z1) for sy in (-1, 1) for sx in (-1, 1)]
    centre = sum(corners, Vector()) / 8
    faces = [(4, 5, 7, 6), (0, 1, 5, 4), (2, 3, 7, 6), (0, 2, 6, 4), (1, 3, 7, 5)]
    for f in faces:
        q = [corners[i] for i in f]
        b.face([b.v(c) for c in q], [sw_uv('metal')] * 4, False, sum(q, Vector()) / 4 - centre)


def collider(parent):
    bm = bmesh.new()
    for y in [Y_TAIL + k * (Y_NOSE - Y_TAIL) / 14 for k in range(15)]:
        r = max(0.05, radius_at(min(y, Y_NOSE - 0.01)))
        for j in range(10): bm.verts.new(P(y, r, 2 * math.pi * j / 10))
    bmesh.ops.convex_hull(bm, input=list(bm.verts))
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
    tex, nrm = texture(out)
    mat = mulib.ksp_material('MushroomBomb', tex, nrm, spec=0.45, shininess=0.35)
    b = B()
    body(b)
    for k in range(4): fin(b, math.radians(45 + 90 * k))     # (an X: clear of the rail on top)
    for y in (-0.22, 0.22): lug(b, y)
    tris = sum(len(f.verts) - 2 for f in b.bm.faces)
    model = mulib.root('model')
    mulib.mesh_object('MushroomBomb', b.bm, mat, model)
    collider(model)
    print(f'Mushroom bomb: {tris} triangles; length {L:.2f} m.')
    mulib.export(model, os.path.join(out, 'model.mu'))
    if o['preview']:
        mulib.render_previews(o['preview'], [
            ('mushroom-side.png', (3.4, 1.4, 1.2), None, (0, 0, 0)),
            ('mushroom-reference.png', (2.6, 2.6, -0.4), None, (0, 0, 0)),
            ('mushroom-tail.png', (2.0, -3.0, 0.6), None, (0, -0.5, 0)),
        ])


main()
