"""Warp Bomb: a 1.25 m sphere of grey plating, panelled in latitude bands and covered in small blocks and towers, with
a dark trench round its equator whose floor and walls glow blue once it's armed (ModuleFFWArmGlow lights
_EmissiveColor), and a small suspension lug on top for the rail.

Run with tools/model.py WarpBomb. Writes model.mu, WarpBomb.png, WarpBomb_NRM.png and WarpBomb_Emissive.png.

Texture atlas (2048 x 2048, v up):
    v 0.25-1      the hull, equirectangular: u = longitude, v = latitude (-90 at 0.25, +90 at 1)
    v 0-0.25      tiles: u 0-0.25 block tops, 0.25-0.5 block sides, 0.5-0.75 trench walls (v = floor to rim),
                  0.75-1 trench floor (upper half; repeated per segment, with the glowing strip) and lug metal (lower)
"""
import math, os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import bmesh
import bpy
import numpy as np
from mathutils import Vector

import mulib

R = 0.625               # hull radius (1.25 m across)
TRENCH_HALF = 0.04      # half the trench's width (m)
TRENCH_DEPTH = 0.04
SEGMENTS = 48           # round the sphere
RINGS = 12              # per hemisphere, trench edge to pole
LAT0 = math.asin(TRENCH_HALF / R)     # the hull stops this far either side of the equator
LUG_TOP = 0.67          # the attach node's height
N = 2048
SEED = 83

rng = np.random.default_rng(SEED)

# ---------------------------------------------------------------- textures

def hull_region():
    """Albedo, height and grime for the hull (2048 x 1536)."""
    W, H = N, N * 3 // 4
    alb = np.full((H, W), 0.5, np.float32)
    hgt = np.zeros((H, W), np.float32)
    px_per_deg_y = H / 180.0
    px_per_deg_x = W / 360.0

    def lat_row(lat):
        return int(round((lat + 90) * px_per_deg_y))

    def panel(x0, x1, y0, y1, tone, height):
        alb[y0:y1, x0:x1] = tone
        hgt[y0:y1, x0:x1] = height

    def blocks(x0, x1, y0, y1, depth):
        """City blocks: a panel split into smaller rectangles of different tones and heights."""
        if depth == 0 or (x1 - x0) < 10 or (y1 - y0) < 8:
            panel(x0, x1, y0, y1, rng.uniform(0.4, 0.58), rng.uniform(-1.5, 2.5))
            return
        if (x1 - x0) > (y1 - y0) * rng.uniform(0.6, 1.6):
            s = int(rng.uniform(0.3, 0.7) * (x1 - x0)) + x0
            blocks(x0, s, y0, y1, depth - 1); blocks(s, x1, y0, y1, depth - 1)
        else:
            s = int(rng.uniform(0.3, 0.7) * (y1 - y0)) + y0
            blocks(x0, x1, y0, s, depth - 1); blocks(x0, x1, s, y1, depth - 1)

    lines = np.zeros((H, W), bool)
    lat0 = math.degrees(LAT0)
    for sign in (1, -1):
        lat = lat0
        while lat < 90:
            band = rng.uniform(2.5, 6.0) if lat < 70 else rng.uniform(4, 7)
            hi = min(90, lat + band)
            la, lb = (lat, hi) if sign > 0 else (-hi, -lat)
            y0, y1 = lat_row(la), lat_row(lb)
            mid = math.radians((la + lb) / 2)
            if abs(math.degrees(mid)) > 72:
                # Near the poles: whole rings of plating.
                panel(0, W, y0, y1, rng.uniform(0.44, 0.56), 0)
            else:
                # Panels round the band, each 6-20 cm wide on the hull.
                widths = []
                while sum(widths) < 360:
                    w_m = rng.uniform(0.06, 0.2)
                    widths.append(math.degrees(w_m / (R * math.cos(mid))))
                k = 360 / sum(widths)
                x = 0.0
                for wd in widths:
                    xa, xb = int(round(x * px_per_deg_x)), int(round((x + wd * k) * px_per_deg_x))
                    x += wd * k
                    r = rng.random()
                    if r < 0.28:
                        blocks(xa, xb, y0, y1, 4)
                    elif r < 0.38:
                        panel(xa, xb, y0, y1, rng.uniform(0.28, 0.36), -1.2)      # recessed, darker
                    else:
                        panel(xa, xb, y0, y1, rng.uniform(0.45, 0.57), 0)
                    lines[y0:y1, xa:min(xa + 2, W)] = True
            lines[y0:y0 + 2, :] = True
            lat = hi
    # The plating's edge by the trench: a darker lip.
    e = lat_row(lat0 + 1.6); s = lat_row(-lat0 - 1.6)
    alb[s:e, :] *= 0.6
    alb[lines] *= 0.5
    hgt[lines] -= 2.0
    grime = mulib.value_noise(H, W, 24, SEED + 1)
    alb *= 0.9 + 0.2 * grime
    return alb, hgt


def tiles():
    """The atlas's bottom quarter: albedo, height, emission (2048 x 512)."""
    H, W = N // 4, N
    alb = np.zeros((H, W), np.float32)
    hgt = np.zeros((H, W), np.float32)
    emi = np.zeros((H, W), np.float32)
    q = W // 4
    # Block tops: light plating with a few seams.
    alb[:, 0:q] = 0.58
    for _ in range(140):
        x, y = rng.integers(0, q - 20), rng.integers(0, H - 20)
        w, h = rng.integers(8, 60), rng.integers(8, 60)
        alb[y:y + h, x:x + w] = rng.uniform(0.48, 0.66)
        hgt[y:y + h, x:x + w] = rng.uniform(-1, 1.5)
    # Block sides: mid grey with horizontal grooves.
    alb[:, q:2 * q] = 0.42
    for y in range(6, H, 22):
        alb[y:y + 2, q:2 * q] = 0.26; hgt[y:y + 2, q:2 * q] = -1.5
    # Trench walls (v = floor to rim): dark, with pipes running round; the glow climbs a little from the floor.
    alb[:, 2 * q:3 * q] = 0.11
    for y in range(40, H - 14, 46):
        t = np.arange(14) - 6.5
        hgt[y:y + 14, 2 * q:3 * q] += (3 * np.sqrt(np.clip(1 - (t / 7) ** 2, 0, 1)))[:, None]
        alb[y:y + 14, 2 * q:3 * q] = 0.17
    ramp = np.clip(1 - np.arange(H) / 110.0, 0, 1) ** 1.5
    emi[:, 2 * q:3 * q] = 0.55 * ramp[:, None]
    # Trench floor (top half, repeated along each segment): dark, a glowing strip in four slots.
    top = slice(H // 2, H)
    alb[top, 3 * q:] = 0.07
    fy0, fy1 = H // 2 + 70, H - 70
    alb[fy0:fy1, 3 * q:] = 0.75
    emi[fy0 - 10:fy1 + 10, 3 * q:] = 0.45
    emi[fy0:fy1, 3 * q:] = 1.0
    for k in range(4):
        x = 3 * q + k * (q // 4)
        alb[fy0:fy1, x:x + 10] = 0.07; emi[fy0:fy1, x:x + 10] = 0.15; hgt[fy0:fy1, x:x + 10] = -2
    # Lug metal (bottom half): darker gunmetal.
    alb[0:H // 2, 3 * q:] = 0.28
    alb[0:H // 2, 3 * q:] += (rng.random((H // 2, q)).astype(np.float32) - 0.5) * 0.04
    return alb, hgt, emi


def make_textures(out):
    h_alb, h_hgt = hull_region()
    t_alb, t_hgt, t_emi = tiles()
    alb = np.vstack([t_alb, h_alb])
    hgt = np.vstack([t_hgt, h_hgt])
    emi = np.vstack([t_emi, np.zeros_like(h_alb)])
    tint = np.array([0.93, 0.96, 1.0], np.float32)      # cool grey
    rgb = alb[..., None] * tint
    main = mulib.save_png('WarpBomb', rgb, out)
    nrm = mulib.save_png('WarpBomb_NRM', mulib.normal_map(hgt, 0.35), out, data=True)
    glow = mulib.save_png('WarpBomb_Emissive', np.dstack([emi * 0.55, emi * 0.8, emi]), out)
    return main, nrm, glow

# ---------------------------------------------------------------- geometry

def uv_hull(lon, lat):
    return (lon / (2 * math.pi), 0.25 + 0.75 * (math.degrees(lat) + 90) / 180)


def tile_uv(tile, s, t, half=None):
    """A point (s, t in 0-1) in one of the bottom tiles (0-3); half = 'top'/'bottom' of tile 3."""
    u = (tile + s) / 4
    if half == 'top': v = 0.125 + t * 0.125
    elif half == 'bottom': v = t * 0.125
    else: v = t * 0.25
    return (u, v)


class Builder:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UV')

    def face(self, verts, uvs, smooth):
        f = self.bm.faces.new(verts)
        f.smooth = smooth
        for loop, uv in zip(f.loops, uvs):
            loop[self.uv].uv = uv
        return f

    def v(self, co):
        return self.bm.verts.new(co)


def hull(b):
    for sign in (1, -1):
        lats = [LAT0 + (math.pi / 2 - LAT0) * k / RINGS for k in range(RINGS)]
        rings = []
        for lat in lats:
            rings.append([b.v((R * math.cos(lat) * math.cos(2 * math.pi * j / SEGMENTS),
                               R * math.cos(lat) * math.sin(2 * math.pi * j / SEGMENTS),
                               sign * R * math.sin(lat))) for j in range(SEGMENTS)])
        pole = b.v((0, 0, sign * R))
        for k in range(RINGS - 1):
            la, lb = lats[k] * sign, lats[k + 1] * sign
            for j in range(SEGMENTS):
                j2 = (j + 1) % SEGMENTS
                lo0, lo1 = 2 * math.pi * j / SEGMENTS, 2 * math.pi * (j + 1) / SEGMENTS
                quad = [rings[k][j], rings[k][j2], rings[k + 1][j2], rings[k + 1][j]]
                uvs = [uv_hull(lo0, la), uv_hull(lo1, la), uv_hull(lo1, lb), uv_hull(lo0, lb)]
                if sign < 0: quad.reverse(); uvs.reverse()
                b.face(quad, uvs, True)
        la = lats[-1] * sign
        for j in range(SEGMENTS):
            j2 = (j + 1) % SEGMENTS
            lo0, lo1 = 2 * math.pi * j / SEGMENTS, 2 * math.pi * (j + 1) / SEGMENTS
            tri = [rings[-1][j], rings[-1][j2], pole]
            uvs = [uv_hull(lo0, la), uv_hull(lo1, la), uv_hull((lo0 + lo1) / 2, sign * math.pi / 2)]
            if sign < 0: tri.reverse(); uvs.reverse()
            b.face(tri, uvs, True)


def trench(b):
    rim = R * math.cos(LAT0)
    floor = rim - TRENCH_DEPTH
    z = TRENCH_HALF

    def ring(r, zz):
        return [b.v((r * math.cos(2 * math.pi * j / SEGMENTS), r * math.sin(2 * math.pi * j / SEGMENTS), zz)) for j in range(SEGMENTS)]

    # Floor: a band facing out; each segment shows the whole floor tile (the slots repeat round it).
    lo, hi = ring(floor, -z), ring(floor, z)
    for j in range(SEGMENTS):
        j2 = (j + 1) % SEGMENTS
        b.face([lo[j], lo[j2], hi[j2], hi[j]], [tile_uv(3, 0, 0, 'top'), tile_uv(3, 1, 0, 'top'), tile_uv(3, 1, 1, 'top'), tile_uv(3, 0, 1, 'top')], False)
    # Walls: flat rings from the hull's edge down to the floor, facing into the trench.
    for sign in (1, -1):
        outer, inner = ring(rim, sign * z), ring(floor, sign * z)
        for j in range(SEGMENTS):
            j2 = (j + 1) % SEGMENTS
            quad = [inner[j], inner[j2], outer[j2], outer[j]]
            uvs = [tile_uv(2, 0, 0), tile_uv(2, 1, 0), tile_uv(2, 1, 1), tile_uv(2, 0, 1)]
            if sign < 0: quad.reverse(); uvs.reverse()     # (the north wall faces down, the south one up)
            b.face(quad, uvs, False)


def box(b, centre, east, north, up, a, c, h, sink):
    """A block standing on the hull: footprint 2a x 2c, height h above the surface, its base sunk into the curve."""
    base = centre - up * sink
    corners = []
    for dz in (0, h + sink):
        for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            corners.append(b.v(base + east * (sx * a) + north * (sy * c) + up * dz))
    lo, hi = corners[:4], corners[4:]
    # Top: a random patch of the block-top tile.
    s0, t0 = rng.uniform(0, 0.8), rng.uniform(0, 0.8)
    sz = min(0.2, max(0.05, 2 * max(a, c) * 1.5))
    b.face(hi, [tile_uv(0, s0, t0), tile_uv(0, s0 + sz, t0), tile_uv(0, s0 + sz, t0 + sz), tile_uv(0, s0, t0 + sz)], False)
    s0 = rng.uniform(0, 0.7)
    for i in range(4):
        i2 = (i + 1) % 4
        w = (2 * a if i % 2 == 0 else 2 * c) * 1.2
        t1 = min(1, (h + sink) * 1.6)
        b.face([lo[i], lo[i2], hi[i2], hi[i]], [tile_uv(1, s0, 0), tile_uv(1, s0 + w, 0), tile_uv(1, s0 + w, t1), tile_uv(1, s0, t1)], False)


def frame(lon, lat):
    up = Vector((math.cos(lat) * math.cos(lon), math.cos(lat) * math.sin(lon), math.sin(lat)))
    east = Vector((-math.sin(lon), math.cos(lon), 0))
    north = up.cross(east)
    return up * R, east, north, up


def greebles(b):
    n = 0
    lat0 = math.degrees(LAT0)
    # Along both edges of the trench: a row of small blocks with gaps.
    for sign in (1, -1):
        lon = 0.0
        while lon < 360 - 4:
            a, c = rng.uniform(0.008, 0.02), rng.uniform(0.008, 0.014)
            lat = sign * (lat0 + 1.6 + math.degrees(c / R) + rng.uniform(0.2, 1.2))
            p, e, no, up = frame(math.radians(lon), math.radians(lat))
            box(b, p, e, no, up, a, c, rng.uniform(0.008, 0.02), (max(a, c) ** 2) / (2 * R) + 0.003)
            n += 1
            lon += math.degrees(2 * a / R) + rng.uniform(2, 9)
    # Scattered over the hull, clear of the lug: blocks, and a few taller towers.
    tries = 0
    placed = []
    while n < 165 and tries < 5000:
        tries += 1
        lat = math.degrees(math.asin(rng.uniform(-0.97, 0.9)))
        if abs(lat) < lat0 + 5: continue
        lon = rng.uniform(0, 360)
        tower = rng.random() < 0.08
        a = rng.uniform(0.008, 0.013) if tower else rng.uniform(0.01, 0.035)
        c = rng.uniform(0.008, 0.013) if tower else rng.uniform(0.01, 0.03)
        h = rng.uniform(0.035, 0.06) if tower else rng.uniform(0.006, 0.025)
        p, e, no, up = frame(math.radians(lon), math.radians(lat))
        if any((p - q).length < r + max(a, c) + 0.01 for q, r in placed): continue
        placed.append((p, max(a, c)))
        box(b, p, e, no, up, a, c, h, (max(a, c) ** 2) / (2 * R) + 0.003)
        n += 1
    return n


def lug(b):
    """A collar on the north pole, a plate on it, and two eyes for the rail's hooks."""
    seg = 24
    rc = 0.11
    z0 = math.sqrt(R * R - rc * rc) - 0.006
    z1 = 0.64
    bot = [b.v((rc * math.cos(2 * math.pi * j / seg), rc * math.sin(2 * math.pi * j / seg), z0)) for j in range(seg)]
    top = [b.v((rc * math.cos(2 * math.pi * j / seg), rc * math.sin(2 * math.pi * j / seg), z1)) for j in range(seg)]
    for j in range(seg):
        j2 = (j + 1) % seg
        b.face([bot[j], bot[j2], top[j2], top[j]], [tile_uv(3, j / seg, 0, 'bottom'), tile_uv(3, (j + 1) / seg, 0, 'bottom'), tile_uv(3, (j + 1) / seg, 0.4, 'bottom'), tile_uv(3, j / seg, 0.4, 'bottom')], True)
    centre = b.v((0, 0, z1))
    for j in range(seg):
        j2 = (j + 1) % seg
        a0, a1 = 2 * math.pi * j / seg, 2 * math.pi * j2 / seg
        b.face([top[j], top[j2], centre], [tile_uv(3, 0.5 + 0.4 * math.cos(a0), 0.5 + 0.4 * math.sin(a0), 'bottom'),
                                          tile_uv(3, 0.5 + 0.4 * math.cos(a1), 0.5 + 0.4 * math.sin(a1), 'bottom'),
                                          tile_uv(3, 0.5, 0.5, 'bottom')], False)
    up, e, no = Vector((0, 0, 1)), Vector((1, 0, 0)), Vector((0, 1, 0))
    box(b, Vector((0, 0, z1)), e, no, up, 0.045, 0.1, 0.025, 0.002)          # plate (long along the flight axis)
    for y in (-0.07, 0.07):
        box(b, Vector((0, y, z1 + 0.025)), e, no, up, 0.012, 0.012, 0.025, 0.001)   # eyes


# ---------------------------------------------------------------- main

def main():
    o = mulib.args()
    out = os.path.abspath(o['out'])
    os.makedirs(out, exist_ok=True)
    mulib.reset()
    main_img, nrm, glow = make_textures(out)
    mat = mulib.ksp_material('WarpBomb', main_img, nrm, glow)

    b = Builder()
    hull(b); trench(b); n = greebles(b); lug(b)
    tris = sum(len(f.verts) - 2 for f in b.bm.faces)
    model = mulib.root('model')
    body = mulib.mesh_object('WarpBomb', b.bm, mat, model)
    mulib.sphere_collider(model, R)
    mulib.box_collider(model, (-0.11, -0.11, 0.6), (0.11, 0.11, LUG_TOP))
    print(f'Warp bomb: {tris} triangles, {n} blocks.')
    mulib.export(model, os.path.join(out, 'model.mu'))

    if o['preview']:
        bsdf = mat.node_tree.nodes['Principled BSDF']
        te = mat.node_tree.nodes.new('ShaderNodeTexImage'); te.image = glow
        mix = mat.node_tree.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
        mix.inputs['Factor'].default_value = 1
        mix.inputs['B'].default_value = (0.35, 0.65, 1.0, 1)
        mat.node_tree.links.new(te.outputs['Color'], mix.inputs['A'])
        mat.node_tree.links.new(mix.outputs['Result'], bsdf.inputs['Emission Color'])

        def glow_on(k):
            return lambda: bsdf.inputs['Emission Strength'].__setattr__('default_value', k)
        mulib.render_previews(o['preview'], [
            ('warpbomb-unarmed.png', (2.6, -3.2, 1.6), glow_on(0)),
            ('warpbomb-armed.png', (2.9, -2.4, 0.55), glow_on(6)),
            ('warpbomb-top.png', (0.6, -1.2, 3.4), glow_on(0)),
        ])


main()
