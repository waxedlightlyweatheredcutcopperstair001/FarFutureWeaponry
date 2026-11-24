"""Antimatter Bomb: an enlarged FAB-250 (2.4 m long, 0.6 m across) in black, with a blunt nose, a tapering tail with four
fins and a ring, and in the middle a protruding containment section in the style of Far Future Technologies' antimatter
tanks: charcoal panel bands, dark bronze foil at the shoulders and between the panels, four chunky rails (the top one
carries the lugs). Purple circuit traces run fore and aft from a ring at each end of the tank section, recessed in
real grooves, ending in pads; they glow dimly on the rail and pulse brighter once armed (ModuleFFWArmGlow).

Run with tools/model.py AntimatterBomb. Writes model.mu, AntimatterBomb.png (+_NRM, _Emissive).

Axes (Blender): the bomb points along +Y, its back (the rail) is +Z. Texture (2048 x 2048): v 0-0.5 the body of
revolution (u along its length, v round it); v 0.5-1 swatches in eight columns (see SWATCH).
"""
import math, os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import bmesh
import bpy
import numpy as np
from mathutils import Vector, Matrix

import mulib

Y_TAIL, Y_NOSE = -1.2, 1.2
L = Y_NOSE - Y_TAIL
R = 0.30                    # body
R_TANK = 0.35               # the tank section's shoulders reach this
R_PANEL, R_FOIL = 0.355, 0.343
TANK0, TANK1 = -0.30, 0.35  # the tank section along the body
FORE = (TANK1, 0.75)        # grooved body sections (cylinders)
AFT = (-0.55, TANK0)
GROOVE_W, GROOVE_D = 0.012, 0.007
SEG = 48                    # round the body (more where the grooves need edges)
SEED = 250
rng = np.random.default_rng(SEED)

SWATCH = {'black': 0, 'trace': 1, 'wall': 2, 'rail': 3, 'box': 4, 'light': 5, 'foil': 6, 'panel': 7}


def body_uv(y, th):
    return ((y - Y_TAIL) / L, 0.01 + (th / (2 * math.pi)) * 0.47)     # (kept clear of the swatches' rows: no bleeding)


def sw_uv(name, s=0.5, t=0.5):
    k = SWATCH[name]
    return (min(0.999, (k + 0.1 + 0.8 * s) / 8), 0.5 + 0.05 + 0.9 * min(1, max(0, t)) * 0.5)

# ---------------------------------------------------------------- the grooves (circuit traces)

def deg_w(r=R):
    return math.degrees(GROOVE_W / r)


def traces():
    """Rectangles (y0, y1, th0, th1 in degrees) of the grooves: a ring at each end of the tank section, traces with jogs
    and pads running out from them."""
    w, hw = deg_w(), deg_w() / 2
    pad, hp = 0.03, math.degrees(0.03 / R) / 2
    rects = [(0.365, 0.365 + GROOVE_W, 0, 360), (-0.315 - GROOVE_W, -0.315, 0, 360)]
    for k in range(4):
        t0 = 90 * k + 45            # (between the rails)
        # Fore: one trace with a jog, one straight; both end in pads.
        a, j = t0 - 6, t0 + 10
        rects += [(0.37, 0.52, a - hw, a + hw), (0.508, 0.52, a - hw, j + hw), (0.508, 0.66, j - hw, j + hw),
                  (0.645, 0.645 + pad, j - hp, j + hp)]
        b = t0 - 18
        rects += [(0.37, 0.58, b - hw, b + hw), (0.565, 0.565 + pad, b - hp, b + hp)]
        # Aft: a straight one and one with a jog.
        c = t0 + 6
        rects += [(-0.47, -0.32, c - hw, c + hw), (-0.485, -0.485 + pad, c - hp, c + hp)]
        d, e = t0 - 14, t0 - 26
        rects += [(-0.42, -0.32, d - hw, d + hw), (-0.42, -0.408, e - hw, d + hw), (-0.5, -0.408, e - hw, e + hw),
                  (-0.515, -0.515 + pad, e - hp, e + hp)]
    return rects

# ---------------------------------------------------------------- textures

def textures(out):
    N = 2048
    alb = np.zeros((N, N, 3), np.float32)
    gloss = np.zeros((N, N), np.float32)
    hgt = np.zeros((N, N), np.float32)
    emi = np.zeros((N, N, 3), np.float32)
    black = np.array([0.05, 0.05, 0.055]); charcoal = np.array([0.13, 0.13, 0.14])
    bronze = np.array([0.46, 0.34, 0.12])
    purple = np.array([0.75, 0.3, 1.0])
    # Body (rows 0-1023): black, with the tank section's panels and foil.
    def col(y): return int(round((y - Y_TAIL) / L * N))
    body = alb[:1024]; bg = gloss[:1024]; bh = hgt[:1024]
    body[:] = black; bg[:] = 0.25
    for y in (0.75, -0.55):
        c = col(y); bh[:, c - 2:c + 2] = -2; body[:, c - 2:c + 2] *= 0.6
    foil_n = mulib.value_noise(1024, N, 60, SEED + 2)
    wrinkle = np.abs(mulib.value_noise(1024, N, 300, SEED + 3) - 0.5) * 2          # (creases: ridged noise)
    for a, b, kind in ((TANK0, -0.22, 'foil'), (-0.22, -0.08, 'panel'), (-0.08, -0.05, 'foil'), (-0.05, 0.12, 'panel'),
                       (0.12, 0.15, 'foil'), (0.15, 0.27, 'panel'), (0.27, TANK1, 'foil')):
        s = slice(col(a), col(b))
        if kind == 'foil':
            body[:, s] = bronze * (0.88 + 0.24 * foil_n[:, s])[..., None]
            bg[:, s] = 0.95; bh[:, s] = (foil_n[:, s] * 1.5 + wrinkle[:, s] * 2.5)      # (crinkles in the normal map)
        else:
            body[:, s] = charcoal
            bg[:, s] = 0.45
            e0, e1 = col(a), col(b)
            for c in (e0 + 6, e1 - 8):                      # (a frame line inset round each panel band)
                bh[:, c:c + 2] = -1.5; body[:, c:c + 2] *= 0.7
    body *= (0.92 + 0.16 * mulib.value_noise(1024, N, 30, SEED))[..., None]
    # Swatches (rows 1024-2047): eight columns.
    w = N // 8
    sw = {'black': black, 'trace': np.array([0.35, 0.12, 0.5]), 'wall': np.array([0.03, 0.03, 0.035]),
          'rail': np.array([0.16, 0.16, 0.17]), 'box': np.array([0.2, 0.2, 0.21]), 'light': np.array([0.2, 0.08, 0.3]),
          'foil': bronze, 'panel': charcoal}
    for name, c in sw.items():
        k = SWATCH[name]
        alb[1024:, k * w:(k + 1) * w] = c
        gloss[1024:, k * w:(k + 1) * w] = 0.9 if name == 'foil' else 0.4
    k = SWATCH['trace']; emi[1024:, k * w:(k + 1) * w] = purple
    k = SWATCH['light']; emi[1024:, k * w:(k + 1) * w] = purple * 0.9
    main = mulib.save_png('AntimatterBomb', np.clip(alb, 0, 1), out, alpha=gloss)
    nrm = mulib.save_png('AntimatterBomb_NRM', mulib.normal_map(hgt, 0.35), out, data=True)
    glow = mulib.save_png('AntimatterBomb_Emissive', np.clip(emi, 0, 1), out, shrink_by=4)   # (nearly all black)
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
        for loop, uv in zip(f.loops, uvs): loop[self.uv].uv = uv
        if outward is not None and f.normal.dot(outward) < 0: f.normal_flip()
        return f


def P(y, r, th):
    return Vector((r * math.cos(th), y, r * math.sin(th)))


def band(b, y0, r0, y1, r1, smooth, uvf=body_uv, seg=SEG):
    a = [b.v(P(y0, r0, 2 * math.pi * j / seg)) for j in range(seg)]
    c = [b.v(P(y1, r1, 2 * math.pi * j / seg)) for j in range(seg)]
    for j in range(seg):
        j2 = (j + 1) % seg
        t0, t1 = 2 * math.pi * j / seg, 2 * math.pi * (j + 1) / seg
        b.face([a[j], c[j], c[j2], a[j2]], [uvf(y0, t0), uvf(y1, t0), uvf(y1, t1), uvf(y0, t1)], smooth)


def fan(b, y, r, forward, uv):
    c = b.v((0, y, 0)); ring = [b.v(P(y, r, 2 * math.pi * j / 48)) for j in range(48)]
    for j in range(48):
        j2 = (j + 1) % 48
        b.face([c, ring[j2], ring[j]] if forward else [c, ring[j], ring[j2]], [uv] * 3, False)


def profile(b):
    """The body of revolution apart from the grooved sections."""
    # Tail: fuze boss, cap, cone to the body.
    fan(b, Y_TAIL - 0.03, 0.05, False, sw_uv('black'))
    band(b, Y_TAIL - 0.03, 0.05, Y_TAIL, 0.05, True)
    band(b, Y_TAIL, 0.05, Y_TAIL, 0.13, False)
    band(b, Y_TAIL, 0.13, AFT[0], R, True)
    # Tank section: shoulders of foil, panel bands with foil gaps, steps between.
    def shoulder(y0, y1, rising):
        n = 6
        for k in range(n):
            s0, s1 = k / n, (k + 1) / n
            f = (lambda s: math.sin(s * math.pi / 2)) if rising else (lambda s: math.cos(s * math.pi / 2))
            band(b, y0 + (y1 - y0) * s0, R + (R_TANK - R) * f(s0), y0 + (y1 - y0) * s1, R + (R_TANK - R) * f(s1), True)
    shoulder(TANK0, -0.22, True)
    for (a, ra), (c, rc) in (((-0.22, R_TANK), (-0.22, R_PANEL)),):
        band(b, a, ra, c, rc, False)
    band(b, -0.22, R_PANEL, -0.08, R_PANEL, True)
    band(b, -0.08, R_PANEL, -0.08, R_FOIL, False)
    band(b, -0.08, R_FOIL, -0.05, R_FOIL, True)
    band(b, -0.05, R_FOIL, -0.05, R_PANEL, False)
    band(b, -0.05, R_PANEL, 0.12, R_PANEL, True)
    band(b, 0.12, R_PANEL, 0.12, R_FOIL, False)
    band(b, 0.12, R_FOIL, 0.15, R_FOIL, True)
    band(b, 0.15, R_FOIL, 0.15, R_PANEL, False)
    band(b, 0.15, R_PANEL, 0.27, R_PANEL, True)
    band(b, 0.27, R_PANEL, 0.27, R_TANK, False)
    shoulder(0.27, TANK1, False)
    # Nose: a blunt ogive and the fuze.
    n = 12
    yb, ln = FORE[1], Y_NOSE - FORE[1] - 0.03
    pts = []
    for k in range(n + 1):
        s = 0.96 * k / n
        pts.append((yb + ln * s, R * math.sqrt(max(0, 1 - s * s)) ** 0.85))
    for (y0, r0), (y1, r1) in zip(pts, pts[1:]):
        band(b, y0, r0, y1, r1, True)
    yt, rt = pts[-1]
    band(b, yt, rt, yt, 0.05, False)
    band(b, yt, 0.05, Y_NOSE, 0.05, True)
    fan(b, Y_NOSE, 0.05, True, sw_uv('black'))


def grooved(b, y0, y1, rects):
    """
    A cylinder section of radius R with grooves cut in it: a grid whose lines include every groove's edges; each cell is
    hull (at R) or groove floor (at R - depth), with walls where they meet. Hull and floor are smooth within themselves.
    """
    ths = set(round(360 * j / SEG, 4) for j in range(SEG))
    ys = {y0, y1}
    mine = [r for r in rects if r[1] > y0 and r[0] < y1]
    for a, c, t0, t1 in mine:
        ys.update((max(y0, a), min(y1, c)))
        if t1 - t0 < 359: ths.update((round(t0 % 360, 4), round(t1 % 360, 4)))
    ths = sorted(ths); ys = sorted(ys)
    nt, ny = len(ths), len(ys)

    def groove(i, j):
        ym = (ys[j] + ys[j + 1]) / 2
        tm = (ths[i] + (ths[(i + 1) % nt] + (360 if i + 1 == nt else 0))) / 2 % 360
        for a, c, t0, t1 in mine:
            if a < ym < c:
                if t1 - t0 >= 359: return True
                if (tm - t0) % 360 < (t1 - t0): return True
        return False

    cache = {}

    def vert(i, j, level):
        key = (i % nt, j, level)
        if key not in cache:
            cache[key] = b.v(P(ys[j], R - (GROOVE_D if level else 0), math.radians(ths[i % nt])))
        return cache[key]

    state = [[groove(i, j) for j in range(ny - 1)] for i in range(nt)]
    for i in range(nt):
        i2 = (i + 1) % nt
        ta, tb = math.radians(ths[i]), math.radians(ths[i2] + (360 if i2 == 0 else 0))
        for j in range(ny - 1):
            g = state[i][j]
            ya, yb = ys[j], ys[j + 1]
            uvs = [body_uv(ya, ta), body_uv(yb, ta), body_uv(yb, tb), body_uv(ya, tb)] if not g else \
                  [sw_uv('trace', 0.2, 0.2), sw_uv('trace', 0.2, 0.8), sw_uv('trace', 0.8, 0.8), sw_uv('trace', 0.8, 0.2)]
            lv = 1 if g else 0
            b.face([vert(i, j, lv), vert(i, j + 1, lv), vert(i2, j + 1, lv), vert(i2, j, lv)], uvs, True)
            mid = P((ya + yb) / 2, R, (ta + tb) / 2)
            # Walls: to the next cell round (at angle i2) and the next along (at y j+1), where one is a groove.
            if state[i2][j] != g:
                th = tb
                q = [b.v(P(ya, R, th)), b.v(P(yb, R, th)), b.v(P(yb, R - GROOVE_D, th)), b.v(P(ya, R - GROOVE_D, th))]
                towards = Vector((-math.sin(th), 0, math.cos(th)))          # (+angle direction)
                b.face(q, [sw_uv('wall')] * 4, False, -towards if g else towards)    # (facing into the groove)
            if j + 1 < ny - 1 and state[i][j + 1] != g:
                q = [b.v(P(yb, R, ta)), b.v(P(yb, R, tb)), b.v(P(yb, R - GROOVE_D, tb)), b.v(P(yb, R - GROOVE_D, ta))]
                b.face(q, [sw_uv('wall')] * 4, False, Vector((0, -1, 0)) if g else Vector((0, 1, 0)))


def box(b, y0, y1, th, r0, r1, half_w, swatch):
    """A block on the body: from radius r0 to r1, along y from y0 to y1, centred on angle th, half_w wide."""
    out = Vector((math.cos(th), 0, math.sin(th))); side = Vector((-math.sin(th), 0, math.cos(th)))
    def p(y, r, s): return Vector((0, y, 0)) + out * r + side * s
    c = [p(y, r, s) for r in (r0, r1) for y in (y0, y1) for s in (-half_w, half_w)]
    mid = sum(c, Vector()) / 8
    faces = [(p(y0, r1, -half_w), p(y1, r1, -half_w), p(y1, r1, half_w), p(y0, r1, half_w), out),
             (p(y0, r0, -half_w), p(y1, r0, -half_w), p(y1, r1, -half_w), p(y0, r1, -half_w), -side),
             (p(y0, r0, half_w), p(y0, r1, half_w), p(y1, r1, half_w), p(y1, r0, half_w), side),
             (p(y1, r0, -half_w), p(y1, r0, half_w), p(y1, r1, half_w), p(y1, r1, -half_w), Vector((0, 1, 0))),
             (p(y0, r0, -half_w), p(y0, r1, -half_w), p(y0, r1, half_w), p(y0, r0, half_w), Vector((0, -1, 0)))]
    for *q, n in faces:
        b.face([b.v(x) for x in q], [sw_uv(swatch, 0.1, 0.1), sw_uv(swatch, 0.9, 0.1), sw_uv(swatch, 0.9, 0.9), sw_uv(swatch, 0.1, 0.9)], False, n)


def rails(b):
    for k in range(4):
        th = math.radians(90 * k)
        box(b, -0.24, 0.29, th, R_FOIL - 0.004, 0.378, 0.025, 'rail')
        if k == 1:      # the top rail: two lugs for the rack's hooks
            for y in (-0.16, 0.16):
                box(b, y - 0.018, y + 0.018, th, 0.37, 0.395, 0.016, 'rail')
        else:           # the others: two instrument boxes each, with a purple indicator on top
            for y in (-0.13, 0.2):
                box(b, y - 0.04, y + 0.04, th, 0.37, 0.392, 0.032, 'box')
                box(b, y - 0.025, y + 0.0, th, 0.392, 0.396, 0.012, 'light')


def fin(b, th):
    out = Vector((math.cos(th), 0, math.sin(th))); side = Vector((-math.sin(th), 0, math.cos(th)))
    t = 0.007
    def cone_r(y): return 0.13 + (R - 0.13) * (y - Y_TAIL) / (AFT[0] - Y_TAIL)
    pts = [(Y_TAIL - 0.02, 0.12), (-0.8, cone_r(-0.8) - 0.005), (-0.95, R + 0.005), (Y_TAIL - 0.02, R + 0.005)]
    for s in (1, -1):
        q = [b.v(Vector((0, y, 0)) + out * r + side * (s * t)) for y, r in pts]
        b.face(q, [sw_uv('black', (y - Y_TAIL) * 2, r * 3) for y, r in pts], False, side * s)
    for i in range(4):
        (ya, ra), (yb, rb) = pts[i], pts[(i + 1) % 4]
        q = [b.v(Vector((0, ya, 0)) + out * ra + side * t), b.v(Vector((0, yb, 0)) + out * rb + side * t),
             b.v(Vector((0, yb, 0)) + out * rb - side * t), b.v(Vector((0, ya, 0)) + out * ra - side * t)]
        mid = Vector((0, sum(p[0] for p in pts) / 4, 0)) + out * (sum(p[1] for p in pts) / 4)
        edge_mid = (Vector((0, ya, 0)) + out * ra + Vector((0, yb, 0)) + out * rb) / 2
        b.face(q, [sw_uv('black')] * 4, False, edge_mid - mid)


def tail_ring(b):
    """The ring joining the fin tips (FAB-250 style)."""
    y0, y1, ri, ro = Y_TAIL - 0.02, -1.0, R + 0.005, R + 0.017
    band(b, y0, ro, y1, ro, True, lambda y, t: sw_uv('black', t / 6.3, (y - y0) * 4))
    band(b, y1, ri, y0, ri, True, lambda y, t: sw_uv('black', t / 6.3, (y - y0) * 4))     # (inside, facing in)
    band(b, y1, ro, y1, ri, False, lambda y, t: sw_uv('black'))
    band(b, y0, ri, y0, ro, False, lambda y, t: sw_uv('black'))


def collider(parent):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=R_TANK, radius2=R_TANK, depth=L)
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
    tex, nrm, glow = textures(out)
    mat = mulib.ksp_material('AntimatterBomb', tex, nrm, glow, spec=0.35, shininess=0.35)
    b = B()
    profile(b)
    rects = traces()
    grooved(b, AFT[0], AFT[1], rects)
    grooved(b, FORE[0], FORE[1], rects)
    rails(b)
    for k in range(4): fin(b, math.radians(45 + 90 * k))
    tail_ring(b)
    tris = sum(len(f.verts) - 2 for f in b.bm.faces)
    model = mulib.root('model')
    mulib.mesh_object('AntimatterBomb', b.bm, mat, model)
    collider(model)
    print(f'Antimatter bomb: {tris} triangles.')
    mulib.export(model, os.path.join(out, 'model.mu'))

    if o['preview']:
        bsdf = mat.node_tree.nodes['Principled BSDF']
        te = mat.node_tree.nodes.new('ShaderNodeTexImage'); te.image = glow
        mat.node_tree.links.new(te.outputs['Color'], bsdf.inputs['Emission Color'])
        def g(k): return lambda: bsdf.inputs['Emission Strength'].__setattr__('default_value', k)
        mulib.render_previews(o['preview'], [
            ('antimatter-side.png', (2.6, -1.6, 1.2), g(0.6), (0, 0, 0)),
            ('antimatter-armed.png', (1.4, 1.5, 0.6), g(4), (0, 0.3, 0)),
            ('antimatter-tail.png', (1.2, -2.6, -0.4), g(0.6), (0, -0.6, 0)),
        ])


main()
