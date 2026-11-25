"""Long Range Nuclear Cruise Missile: after Project Pluto's SLAM, shrunk to a big cruise missile (8 m long, 0.5 m across).
A long silver fuselage with a sharp black nose, a dark band behind it, two small canards, a tall swept fin on top;
under the rear half the ramjet duct, with a spiked intake and a nozzle that glows red-hot once the ramjet runs
(ModuleFFWArmGlow); invented Bobrigh Weaponry markings (a stencilled name and a round emblem on each side). Three
solid boosters, evenly spaced (top left, top right, bottom, under the duct), each on its own "boosterTransform" with a
"boostExhaust" point: BDArmory fires them for the boost, then drops them (decoupleBoosters).

Run with tools/model.py CruiseMissile. Writes model.mu, CruiseMissile.png (+_NRM, _Emissive).

Axes (Blender): the missile points along +Y, its back (the rail) is +Z. Texture (2048 x 2048): v 0-0.5 the fuselage
(u along its length, v round it from the top: the seam is on top, clear of the markings), v 0.75-1 swatches in eight columns (see SWATCH).
"""
import math, os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import bmesh
import bpy
import numpy as np
from mathutils import Vector

import mulib

R = 0.25                                  # the engine section (the widest part)
Y_TAIL, Y_NOSE = -4.0, 4.0                # nozzle exit .. needle tip
Y_BOAT, Y_TAPER = -3.55, -0.7             # boat-tail starts / the long taper to the nose starts
Y_BLACK = 2.4                             # black needle from here
L_TEX = 8.2                               # the fuselage's texture length (from Y_TEX0)
Y_TEX0 = -4.1
Y_MOUTH, Y_SCOOP_END = -0.5, -2.8         # the chin intake: its mouth, and where it has curved up into the belly
SCOOP_Z, SCOOP_A, SCOOP_B = -0.08, 0.19, 0.28     # its section: an ellipse centred SCOOP_Z, half-width A, half-height B
NOZZLE_R = 0.2
B_R = 0.12                                # boosters
SEG = 48
SEED = 1958
SWATCH = {'black': 0, 'metal': 1, 'fin': 2, 'booster': 3, 'hot': 4, 'inside': 5, 'silver': 6, 'band': 7}
SILVER = np.array([0.8, 0.81, 0.83])

# A 5x7 pixel font for the stencil (rows top to bottom).
FONT = {
    'B': ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
    'O': ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
    'R': ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
    'I': ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
    'G': ["01110", "10001", "10000", "10111", "10001", "10001", "01111"],
    'H': ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
}


def body_uv(y, a):
    """a: the angle from the top (continuous within a face), 0 to 2 pi."""
    return ((y - Y_TEX0) / L_TEX, 0.01 + (a / (2 * math.pi)) * 0.47)


def sw_uv(name, s=0.5, t=0.5):
    k = SWATCH[name]
    return ((k + 0.1 + 0.8 * min(1, max(0, s))) / 8, 0.75 + 0.02 + 0.96 * min(1, max(0, t)) * 0.25)


def sw_any(name):
    return lambda y, a: sw_uv(name, a / (2 * math.pi), 0.5)


def fuselage_r(y):
    """The body: the engine section at full width, a boat-tail to the nozzle, one long taper to the needle nose."""
    if y >= Y_TAPER:
        s = min(1.0, (y - Y_TAPER) / (Y_NOSE - Y_TAPER))
        return R * (1 - s) ** 1.05
    if y >= Y_BOAT: return R
    s = (y - Y_BOAT) / (Y_TAIL - Y_BOAT)
    return R - (R - NOZZLE_R) * s * s


def scoop_b(y):
    """The intake's depth: full for the front third, then curving up into the belly (inside it by the end)."""
    t = (Y_MOUTH - y) / (Y_MOUTH - Y_SCOOP_END)
    t = min(1, max(0, (t - 0.3) / 0.7)); t = t * t * (3 - 2 * t)
    return 0.06 + (SCOOP_B - 0.06) * (1 - t)


# ---------------------------------------------------------------- texture

def texture(out):
    N = 2048
    alb = np.zeros((N, N, 3), np.float32); gloss = np.zeros((N, N), np.float32)
    hgt = np.zeros((N, N), np.float32); emi = np.zeros((N, N, 3), np.float32)
    px_u, px_v = N / L_TEX, 0.47 * N / (2 * math.pi * R)          # pixels per metre along and round
    def col(y): return int(round((y - Y_TEX0) / L_TEX * N))
    def row(theta):                                               # (theta from +X towards +Z, as in the geometry)
        a = (theta - math.pi / 2) % (2 * math.pi)
        return int(round((0.01 + a / (2 * math.pi) * 0.47) * N))
    fus = alb[:1024]; fh = hgt[:1024]; fg = gloss[:1024]
    fus[:] = SILVER; fg[:] = 0.6
    # Black nose and a dark band behind it; faint section joints.
    fus[:, col(Y_BLACK):] = (0.05, 0.05, 0.06)
    fus[:, col(2.1):col(2.25)] = (0.06, 0.06, 0.07)
    fus[:, :col(Y_TAIL + 0.12)] = (0.25, 0.24, 0.24)             # (scorched by the nozzle)
    for y in (-3.3, -2.5, -0.7, 0.6, 1.6, Y_BLACK):
        c = col(y); fh[:, c - 1:c + 2] -= 2; fus[:, c - 1:c + 2] *= 0.8
    # The stencilled name and the emblem, on each side (the left side's turned half round, so both read upright).
    def glyphs(text, y0, theta, cell_u, cell_v, colour, flip):
        rc = row(theta)
        for i, ch in enumerate(text):
            for r_, line in enumerate(FONT[ch]):
                for c_, bit in enumerate(line):
                    if bit != '1': continue
                    gx, gy = i * 6 + c_, 6 - r_                  # (gy up)
                    if flip: gx, gy = len(text) * 6 - 2 - gx, -gy + 6
                    u0 = col(y0) + int(gx * cell_u); v0 = rc + int((gy - 3.5) * cell_v)
                    fus[max(v0, 0):v0 + int(cell_v) + 1, u0:u0 + int(cell_u) + 1] = colour
    cu, cv = 0.024 * px_u, 0.032 * px_v
    glyphs("BOBRIGH", -2.25, 0.0, cu, cv, (0.08, 0.08, 0.1), False)
    glyphs("BOBRIGH", -2.25, math.pi, cu, cv, (0.08, 0.08, 0.1), True)
    yy, xx = np.mgrid[0:1024, 0:N]
    for theta, flip in ((0.0, False), (math.pi, True)):
        cy, cx = row(theta), col(-0.95)
        du, dv = (xx - cx) / px_u, (yy - cy) / px_v
        d = np.sqrt(du * du + dv * dv)
        fus[d < 0.135] = (0.1, 0.1, 0.12)
        fus[d < 0.125] = (0.95, 0.5, 0.12)
        fus[d < 0.105] = (0.12, 0.14, 0.32)
        # A "B" in the middle.
        for r_, line in enumerate(FONT['B']):
            for c_, bit in enumerate(line):
                if bit != '1': continue
                gx, gy = c_ - 2, 3 - r_
                if flip: gx, gy = -gx, -gy
                u0 = cx + int(gx * 0.026 * px_u - 0.013 * px_u); v0 = cy + int(gy * 0.026 * px_v - 0.013 * px_v)
                fus[v0:v0 + int(0.026 * px_v) + 1, u0:u0 + int(0.026 * px_u) + 1] = (0.95, 0.95, 0.95)
    fus *= (0.95 + 0.1 * mulib.value_noise(1024, N, 30, SEED))[..., None]
    # Swatches (rows 1536-2047).
    w = N // 8
    for name, c, g in (('black', (0.05, 0.05, 0.06), 0.4), ('metal', (0.3, 0.3, 0.31), 0.6), ('fin', (0.72, 0.66, 0.32), 0.45),
                       ('booster', (0.88, 0.88, 0.86), 0.4), ('hot', (0.2, 0.12, 0.1), 0.5), ('inside', (0.04, 0.04, 0.04), 0.2),
                       ('silver', tuple(SILVER), 0.6), ('band', (0.06, 0.06, 0.07), 0.4)):
        k = SWATCH[name]; alb[1536:, k * w:(k + 1) * w] = c; gloss[1536:, k * w:(k + 1) * w] = g
    k = SWATCH['hot']; emi[1536:, k * w:(k + 1) * w] = (1.0, 0.45, 0.15)
    main = mulib.save_png('CruiseMissile', np.clip(alb, 0, 1), out, alpha=gloss)
    nrm = mulib.save_png('CruiseMissile_NRM', mulib.normal_map(hgt, 0.3), out, data=True)
    glow = mulib.save_png('CruiseMissile_Emissive', emi, out, shrink_by=4)
    return main, nrm, glow

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


def P(y, r, th, cx=0.0, cz=0.0):
    return Vector((cx + r * math.cos(th), y, cz + r * math.sin(th)))


def revolve(b, prof, uvf, smooth=True, cx=0.0, cz=0.0, seg=SEG):
    """Bands through the profile [(y, r)] round an axis along Y at (cx, cz); shared rings (smooth along it). Faces point
    away from the axis when y increases along the profile (reverse it for inner walls)."""
    th = [2 * math.pi * j / seg for j in range(seg)]
    rings = [[b.v(P(y, r, t, cx, cz)) for t in th] for y, r in prof]
    for i in range(len(prof) - 1):
        (y0, _), (y1, _) = prof[i], prof[i + 1]
        for j in range(seg):
            j2 = (j + 1) % seg
            a0 = (th[j] - math.pi / 2) % (2 * math.pi); a1 = a0 + 2 * math.pi / seg      # (angle from the top, continuous)
            b.face([rings[i][j], rings[i + 1][j], rings[i + 1][j2], rings[i][j2]],
                   [uvf(y0, a0), uvf(y1, a0), uvf(y1, a1), uvf(y0, a1)], smooth)
    return rings


def cap(b, ring, centre, uv, outward):
    c = b.v(centre)
    n = len(ring)
    for j in range(n):
        b.face([c, ring[j], ring[(j + 1) % n]], [uv] * 3, False, outward)


def fuselage(b):
    """The body of revolution, ending at the back in the ramjet's nozzle (on the axis): its rim glows."""
    ys = [Y_TAIL + (Y_BOAT - Y_TAIL) * k / 6 for k in range(6)] + [Y_BOAT + (Y_TAPER - Y_BOAT) * k / 10 for k in range(10)] + \
         [Y_TAPER + (Y_NOSE - 0.02 - Y_TAPER) * k / 20 for k in range(21)]
    rings = revolve(b, [(y, fuselage_r(y)) for y in ys], body_uv)
    cap(b, rings[-1], (0, Y_NOSE, 0), body_uv(Y_NOSE - 0.01, 0.1), Vector((0, 1, 0)))
    # The nozzle: its rim (facing back, glowing), the wall inside (facing in, glowing), a dark end.
    revolve(b, [(Y_TAIL, 0.15), (Y_TAIL, NOZZLE_R)], sw_any('hot'), False)
    revolve(b, [(Y_TAIL + 0.15, 0.15), (Y_TAIL, 0.15)], sw_any('hot'), True)
    ring = [b.v(P(Y_TAIL + 0.15, 0.15, 2 * math.pi * j / SEG)) for j in range(SEG)]
    cap(b, ring, (0, Y_TAIL + 0.15, 0), sw_uv('inside'), Vector((0, -1, 0)))


def ellipse_ring(b, y, a, bb, n=40):
    return [b.v((a * math.cos(2 * math.pi * j / n), y, SCOOP_Z + bb * math.sin(2 * math.pi * j / n))) for j in range(n)]


def scoop(b):
    """
    The chin intake, with no diverter: an elliptical scoop under the belly whose top runs inside the body (so the
    fuselage skin is its top wall), open at the front, curving up into the belly towards the back.
    """
    n = 40
    ys = [Y_MOUTH + (Y_SCOOP_END - Y_MOUTH) * k / 16 for k in range(17)]
    rings = [ellipse_ring(b, y, SCOOP_A, scoop_b(y), n) for y in ys]
    uvf = lambda k, j: sw_uv('silver', j / n, k / 16)
    for k in range(16):
        yc = (ys[k] + ys[k + 1]) / 2
        for j in range(n):
            j2 = (j + 1) % n
            q = [rings[k][j], rings[k + 1][j], rings[k + 1][j2], rings[k][j2]]
            c = sum((v.co for v in q), Vector()) / 4
            b.face(q, [uvf(k, j), uvf(k + 1, j), uvf(k + 1, j2), uvf(k, j2)], True, c - Vector((0, c.y, SCOOP_Z)))
    cap(b, rings[-1], (0, Y_SCOOP_END, SCOOP_Z), sw_uv('silver'), Vector((0, -1, 0)))   # (inside the body by here)
    # The mouth: a lip (facing forward), the inside wall back to the throat (facing in), a dark throat.
    inner_a, inner_b, depth = SCOOP_A - 0.022, SCOOP_B - 0.022, 0.4
    lip_in = ellipse_ring(b, Y_MOUTH, inner_a, inner_b, n)
    for j in range(n):
        j2 = (j + 1) % n
        b.face([rings[0][j], rings[0][j2], lip_in[j2], lip_in[j]], [sw_uv('metal')] * 4, False, Vector((0, 1, 0)))
    back = ellipse_ring(b, Y_MOUTH - depth, inner_a, inner_b, n)
    for j in range(n):
        j2 = (j + 1) % n
        q = [lip_in[j], lip_in[j2], back[j2], back[j]]
        c = sum((v.co for v in q), Vector()) / 4
        b.face(q, [sw_uv('inside')] * 4, True, Vector((0, c.y, SCOOP_Z)) - c)
    cap(b, back, (0, Y_MOUTH - depth, SCOOP_Z), sw_uv('inside'), Vector((0, 1, 0)))


def fin(b, th, cx, cz, root, tip, thick, swatch):
    """A flat fin at angle th round the axis at (cx, cz): its root [(y, r)] along the body, its tip [(y, r)] (from the
    leading edge back); triangulated (the root may follow a curve)."""
    out = Vector((math.cos(th), 0, math.sin(th))); side = Vector((-math.sin(th), 0, math.cos(th)))
    pts = list(root) + list(tip)
    rmax = max(r for _, r in pts); rmin = min(r for _, r in root)
    def Q(y, r, s): return Vector((cx, y, cz)) + out * r + side * (s * thick / 2 * (1 - 0.5 * (r - rmin) / max(1e-3, rmax - rmin)))
    faces = []
    for s in (1, -1):
        faces.append(b.face([b.v(Q(y, r, s)) for y, r in pts], [sw_uv(swatch, (y + 4) / 8, (r - rmin) / max(1e-3, rmax - rmin)) for y, r in pts], False, side * s))
    for f in bmesh.ops.triangulate(b.bm, faces=faces)['faces']: f.normal_update()
    n = len(pts)
    mid = sum((Q(y, r, 0) for y, r in pts), Vector()) / n
    for i in range(n):
        (ya, ra), (yb, rb) = pts[i], pts[(i + 1) % n]
        q = [b.v(Q(ya, ra, 1)), b.v(Q(yb, rb, 1)), b.v(Q(yb, rb, -1)), b.v(Q(ya, ra, -1))]
        b.face(q, [sw_uv(swatch)] * 4, False, -out if i < len(root) - 1 else (Q(ya, ra, 0) + Q(yb, rb, 0)) / 2 - mid)


def fins(b):
    up = math.pi / 2
    tail = [Y_TAIL + 0.05 + (-2.9 - Y_TAIL - 0.05) * k / 8 for k in range(9)]
    # The tall swept fin on top, its root along the boat-tail.
    fin(b, up, 0, 0, [(y, fuselage_r(y) - 0.01) for y in tail], [(-3.35, 0.95), (-3.85, 0.95)], 0.03, 'fin')
    # Two lower fins in an inverted V (the bottom booster sits between them).
    for th in (math.radians(-45), math.radians(-135)):
        fin(b, th, 0, 0, [(y, fuselage_r(y) - 0.01) for y in tail], [(-3.4, 0.62), (-3.85, 0.62)], 0.025, 'fin')
    # Two small canards on the forebody, their roots along its taper.
    for th in (0.0, math.pi):
        root = [(y, fuselage_r(y) - 0.008) for y in (0.9, 1.05, 1.2, 1.35)]
        fin(b, th, 0, 0, root, [(1.12, 0.38), (0.96, 0.38)], 0.02, 'silver')


def box(b, lo, hi, swatch):
    corners = [Vector((x, y, z)) for z in (lo[2], hi[2]) for y in (lo[1], hi[1]) for x in (lo[0], hi[0])]
    centre = sum(corners, Vector()) / 8
    for f in ((4, 5, 7, 6), (0, 1, 3, 2), (0, 1, 5, 4), (2, 3, 7, 6), (0, 2, 6, 4), (1, 3, 7, 5)):
        q = [corners[i] for i in f]
        b.face([b.v(c) for c in q], [sw_uv(swatch)] * 4, False, sum(q, Vector()) / 4 - centre)


def lugs(b):
    for y in (-0.5, 0.5): box(b, (-0.035, y - 0.03, R - 0.01), (0.035, y + 0.03, R + 0.03), 'metal')


def booster_mesh(toward, reach):
    """One booster, in its own space (its middle at the origin, along Y), with two struts reaching `toward` the body."""
    b = B()
    y0, y1 = -1.25, 1.25          # nozzle exit .. nose tip
    prof = [(y0 + 0.15, 0.09), (y0 + 0.2, 0.09), (y0 + 0.25, B_R)] + [(y0 + 0.25 + k * (y1 - 0.45 - (y0 + 0.25)) / 8, B_R) for k in range(1, 9)]
    nose = [(y1 - 0.45 + 0.45 * k / 8, B_R * math.sqrt(max(0, 1 - (k / 8) ** 2)) * 0.98 + 0.003) for k in range(1, 9)]
    sw = lambda name: (lambda y, a: sw_uv(name, a / (2 * math.pi), (y - y0) / (y1 - y0)))
    rings = revolve(b, prof, sw('booster'), True, 0, 0, 24)
    nrings = revolve(b, [prof[-1]] + nose, sw('black'), True, 0, 0, 24)
    cap(b, nrings[-1], (0, y1, 0), sw_uv('black'), Vector((0, 1, 0)))
    # The nozzle bell: out to the exit, its inside, a dark throat.
    revolve(b, [(y0, 0.1), (y0 + 0.15, 0.09)], sw('metal'), True, 0, 0, 24)
    revolve(b, [(y0, 0.085), (y0, 0.1)], sw('metal'), False, 0, 0, 24)       # (the exit rim, facing back)
    revolve(b, [(y0 + 0.14, 0.085), (y0, 0.085)], sw('inside'), True, 0, 0, 24)
    ring = [b.v(P(y0 + 0.14, 0.085, 2 * math.pi * j / 24)) for j in range(24)]
    cap(b, ring, (0, y0 + 0.14, 0), sw_uv('inside'), Vector((0, -1, 0)))
    # Struts to the body.
    d = toward.normalized()
    for y in (-0.15, 0.75):     # (ahead of the boat-tail, where the body is full width)
        strut(b, d * (B_R - 0.01) + Vector((0, y, 0)), d * (reach + 0.01) + Vector((0, y, 0)), 0.04, 0.02)
    return b


def strut(b, a, c, half_y, half_w):
    """A small bar from a to c (across Y), half_y along the missile and half_w across."""
    u = (c - a).normalized(); w = Vector((0, 1, 0)).cross(u).normalized(); yv = Vector((0, 1, 0))
    corners = [p + yv * sy * half_y + w * sw_ * half_w for p in (a, c) for sy in (-1, 1) for sw_ in (-1, 1)]
    centre = (a + c) / 2
    for f in ((0, 1, 3, 2), (4, 5, 7, 6), (0, 1, 5, 4), (2, 3, 7, 6), (0, 2, 6, 4), (1, 3, 7, 5)):
        q = [corners[i] for i in f]
        b.face([b.v(x) for x in q], [sw_uv('metal')] * 4, False, sum(q, Vector()) / 4 - centre)


def collider(parent):
    bm = bmesh.new()
    for y in [Y_TAIL + k * (Y_NOSE - Y_TAIL) / 20 for k in range(21)]:
        r = max(0.03, fuselage_r(min(y, Y_NOSE - 0.01)))
        for j in range(10): bm.verts.new(P(y, r, 2 * math.pi * j / 10))
    for y in (Y_MOUTH, (Y_MOUTH + Y_SCOOP_END) / 2):
        for x in (-SCOOP_A * 0.7, 0, SCOOP_A * 0.7): bm.verts.new((x, y, SCOOP_Z - scoop_b(y)))
    bmesh.ops.convex_hull(bm, input=list(bm.verts))
    me = bpy.data.meshes.new('collider'); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('collider', me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    ob.muproperties.collider = 'MU_COL_MESH'; ob.muproperties.isConvex = True
    ob.hide_render = True


def empty(name, parent, loc, rot=(0, 0, 0)):
    ob = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent; ob.location = loc; ob.rotation_euler = rot
    return ob


def main():
    o = mulib.args()
    out = os.path.abspath(o['out']); os.makedirs(out, exist_ok=True)
    mulib.reset()
    tex, nrm, glow = texture(out)
    mat = mulib.ksp_material('CruiseMissile', tex, nrm, glow, spec=0.6, shininess=0.4)
    model = mulib.root('model')
    b = B(); fuselage(b); scoop(b); fins(b); lugs(b)
    tris = sum(len(f.verts) - 2 for f in b.bm.faces)
    mulib.mesh_object('CruiseMissile', b.bm, mat, model)
    # The boosters: top left, top right, bottom (under the duct), each its own transform BDArmory can drop.
    yb = -2.85
    for k, (th, dist, cz) in enumerate(((math.radians(30), R + B_R + 0.06, 0), (math.radians(150), R + B_R + 0.06, 0),
                                        (math.radians(270), 0.49, 0))):          # (bottom: below the intake)
        centre = Vector((dist * math.cos(th), yb, cz + dist * math.sin(th)))
        holder = empty('boosterTransform', model, centre)
        bb = booster_mesh(Vector((0, 0, cz)) - Vector((centre.x, 0, centre.z)), dist - R)     # (struts reach the body)
        tris += sum(len(f.verts) - 2 for f in bb.bm.faces)
        mulib.mesh_object('booster', bb.bm, mat, holder)
        empty('boostExhaust', holder, (0, -1.25, 0), (math.radians(90), 0, 0))
    # The ramjet's exhaust point (BDArmory's cruise plume), turned as on BDArmory's own missiles.
    empty('exhaustTransform', model, (0, Y_TAIL + 0.05, 0), (math.radians(90), 0, 0))
    collider(model)
    print(f'Cruise missile: {tris} triangles.')
    mulib.export(model, os.path.join(out, 'model.mu'))
    if o['preview']:
        bsdf = mat.node_tree.nodes['Principled BSDF']
        te = mat.node_tree.nodes.new('ShaderNodeTexImage'); te.image = glow
        mat.node_tree.links.new(te.outputs['Color'], bsdf.inputs['Emission Color'])
        def g(k): return lambda: bsdf.inputs['Emission Strength'].__setattr__('default_value', k)
        mulib.render_previews(o['preview'], [
            ('cruise-reference.png', (-5.0, 6.5, 3.2), g(0), (0, 0, 0)),
            ('cruise-side.png', (10.5, 0.5, 1.2), g(0), (0, 0, 0)),
            ('cruise-rear.png', (2.2, -7.5, -0.8), g(4), (0, -3.2, -0.3)),
            ('cruise-intake.png', (1.4, 1.6, -1.2), g(0), (0, -0.8, -0.2)),
        ])


main()
