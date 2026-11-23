"""Shared helpers for the part-model scripts (run inside Blender by tools/model.py).

Each model is a Python script, models/<Part>/build.py, that builds its meshes with bmesh, paints its textures with
numpy, and calls these helpers to export a KSP .mu (io_object_mu add-on) and render previews. Nothing is modelled by
hand, so a model can be rebuilt, tweaked and re-exported from the command line.

Coordinates: Blender's Z is KSP's (Unity's) Y, and Blender's Y is Unity's Z; io_object_mu converts on export.
"""
import math, os, sys

import addon_utils
import bpy
import bmesh
import numpy as np
from mathutils import Vector


def args():
    """--out <folder for model.mu and textures>, --preview <folder for preview renders or empty>."""
    a = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    opts = {'out': None, 'preview': None}
    for i, k in enumerate(a):
        if k in ('--out', '--preview') and i + 1 < len(a):
            opts[k[2:]] = a[i + 1]
    return opts


def reset():
    """The io_object_mu add-on on, and an empty scene (no default cube, light or camera)."""
    addon_utils.enable('io_object_mu', default_set=True)
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for item in list(coll):
            coll.remove(item)


# ---- Textures (numpy arrays, row 0 = the bottom of the image, i.e. v = 0) ----

def save_png(name, rgb, folder, alpha=None, data=False):
    """Write an H x W x 3 float array (0-1) as <folder>/<name>.png and return the Blender image."""
    h, w, _ = rgb.shape
    img = bpy.data.images.new(name, w, h, alpha=alpha is not None)
    px = np.ones((h, w, 4), np.float32)
    px[..., :3] = np.clip(rgb, 0, 1)
    if alpha is not None:
        px[..., 3] = np.clip(alpha, 0, 1)
    img.pixels.foreach_set(px.ravel())
    if data:
        img.colorspace_settings.is_data = True
    img.filepath_raw = os.path.join(folder, name + '.png')
    img.file_format = 'PNG'
    img.save()
    return img


def normal_map(height, strength):
    """Tangent-space normal map (OpenGL/Unity: +Y up) from a height map in pixels."""
    gx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5 * strength
    gy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * 0.5 * strength
    n = np.dstack([-gx, -gy, np.ones_like(height)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5


def value_noise(h, w, cells, seed):
    """Smooth noise in [0, 1], tiling horizontally."""
    rng = np.random.default_rng(seed)
    cy, cx = max(2, cells * h // w), cells
    g = rng.random((cy + 1, cx))
    y = np.linspace(0, cy, h, endpoint=False)
    x = np.linspace(0, cx, w, endpoint=False)
    y0 = np.floor(y).astype(int); x0 = np.floor(x).astype(int)
    fy = (y - y0)[:, None]; fx = (x - x0)[None, :]
    fy = fy * fy * (3 - 2 * fy); fx = fx * fx * (3 - 2 * fx)
    a = g[y0][:, x0]; b = g[y0][:, (x0 + 1) % cx]; c = g[y0 + 1][:, x0]; d = g[y0 + 1][:, (x0 + 1) % cx]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


# ---- Materials ----

def ksp_material(name, main, nrm=None, emissive=None, spec=0.3, shininess=0.25, preview_glow=None):
    """
    A KSP/Emissive/Bumped Specular (or Bumped Specular) material for export, with a Principled node set-up for the
    preview renders. preview_glow: emission colour for the previews (None = dark).
    """
    m = bpy.data.materials.new(name)
    p = m.mumatprop
    p.shaderName = 'KSP/Emissive/Bumped Specular' if emissive else ('KSP/Bumped Specular' if nrm else 'KSP/Specular')

    def colour(k, v):
        it = p.color.properties.add(); it.name = k; it.value = v

    def flt(k, v):
        it = p.float3.properties.add(); it.name = k; it.value = v

    def tex(k, img, normal=False):
        it = p.texture.properties.add(); it.name = k; it.tex = img.name + '.png'; it.type = normal

    colour('_Color', (1, 1, 1, 1))
    colour('_SpecColor', (spec, spec, spec, 1))
    flt('_Shininess', shininess)
    tex('_MainTex', main)
    if nrm: tex('_BumpMap', nrm, True)
    if emissive:
        tex('_Emissive', emissive)
        colour('_EmissiveColor', (0, 0, 0, 1))      # dark until the plugin lights it

    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes['Principled BSDF']
    bsdf.inputs['Roughness'].default_value = 0.55
    bsdf.inputs['Metallic'].default_value = 0.35
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = main
    nt.links.new(t.outputs['Color'], bsdf.inputs['Base Color'])
    if nrm:
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = nrm; nrm.colorspace_settings.is_data = True
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    if emissive and preview_glow:
        te = nt.nodes.new('ShaderNodeTexImage'); te.image = emissive
        nt.links.new(te.outputs['Color'], bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value = preview_glow
    return m


# ---- Objects ----

def mesh_object(name, bm, material, parent):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    return ob


def root(name):
    ob = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(ob)
    ob.muproperties.modelType = 'PART'
    return ob


def sphere_collider(parent, radius, centre=(0, 0, 0)):
    ob = bpy.data.objects.new('collider', None)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    ob.location = centre
    ob.muproperties.collider = 'MU_COL_SPHERE'
    ob.muproperties.radius = radius
    return ob


def box_collider(parent, lo, hi, name='collider'):
    """A convex mesh collider: the box from lo to hi."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts:
        v.co = Vector([lo[i] + (hi[i] - lo[i]) * (v.co[i] + 0.5) for i in range(3)])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    ob.muproperties.collider = 'MU_COL_MESH'
    ob.muproperties.isConvex = True
    ob.hide_render = True
    return ob


def export(root_obj, path):
    from io_object_mu.export_mu import export_object
    mu = export_object(root_obj, path)
    for m in mu.messages:
        print('io_object_mu:', m)
    print('Exported', path)


# ---- Previews ----

def render_previews(folder, views, size=900, samples=48, target=(0, 0, 0)):
    """views: list of (file name, camera position, emission on) -- emission toggled by the caller's callback."""
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = size
    sc.render.film_transparent = False
    world = bpy.data.worlds.new('preview')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.05, 0.06, 0.08, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.0
    sc.world = world
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sun.data.energy = 4
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
    sc.collection.objects.link(sun)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    cam.data.lens = 70
    sc.collection.objects.link(cam)
    sc.camera = cam
    os.makedirs(folder, exist_ok=True)
    for name, pos, before in views:
        if before: before()
        cam.location = pos
        d = Vector(target) - Vector(pos)
        cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(folder, name)
        bpy.ops.render.render(write_still=True)
        print('Rendered', sc.render.filepath)
