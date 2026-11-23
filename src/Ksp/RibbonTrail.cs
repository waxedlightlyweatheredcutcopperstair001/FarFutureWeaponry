using System.Collections.Generic;
using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// A glowing ribbon left behind something moving (a missile's trail, a dart's streak): points laid in the main body's
    /// frame (so floating-origin shifts don't move it), faded by age, rebuilt facing the camera every frame. It lives on
    /// after what it followed is gone, until it has faded, then removes itself.
    ///  - flicker: the trail's brightness jumps about (the phase-shift missile's unstable trail);
    ///  - crackle: a jagged, sparking line (the Telemetry Leech's ion trail).
    /// </summary>
    internal sealed class RibbonTrail : MonoBehaviour
    {
        public Color Colour = Color.white;
        public float Width = 1, Fade = 2, Flicker, Crackle;

        readonly TrailPoints points = new TrailPoints();
        Transform follow;
        Mesh mesh;
        Material mat;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> cols = new List<Color>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        public static RibbonTrail Create(Transform follow, Color colour, float width, float fade, float spacing, float flicker = 0, float crackle = 0)
        {
            var go = new GameObject("FFW trail");
            var t = go.AddComponent<RibbonTrail>();
            t.follow = follow; t.Colour = colour; t.Width = width; t.Fade = fade; t.Flicker = flicker; t.Crackle = crackle;
            t.points.MinSpacing = spacing;
            t.mesh = new Mesh(); t.mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = t.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            t.mat = Visuals.Additive(Visuals.Ribbon, new Color(colour.r, colour.g, colour.b, colour.a * 0.5f));
            mr.sharedMaterial = t.mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            return t;
        }

        /// <summary>Stop following (it keeps fading where it is).</summary>
        public void Detach() => follow = null;

        /// <summary>Add a point by hand (when following isn't by transform, e.g. a dart).</summary>
        public void Add(Vector3 world)
        {
            Vector3d p = Visuals.ToFrame(world);
            points.Add(p.x, p.y, p.z, Time.time);
        }

        void LateUpdate()
        {
            float now = Time.time;
            if (follow != null) Add(follow.position);
            points.Prune(now, Fade);
            if (follow == null && points.Faded(now, Fade)) { Destroy(gameObject); return; }
            Camera cam = Visuals.Camera;
            if (cam != null) Build(cam.transform.position, now);
            if (Flicker > 0)
            {
                float k = 1 - Flicker * Random.value;
                mat.SetColor("_TintColor", new Color(Colour.r, Colour.g, Colour.b, Colour.a * 0.5f * k));
            }
        }

        void Build(Vector3 camPos, float now)
        {
            verts.Clear(); cols.Clear(); uvs.Clear(); tris.Clear();
            var pts = points.Points;
            int n = pts.Count;
            if (n >= 2)
            {
                var world = new Vector3[n];
                for (int i = 0; i < n; i++) world[i] = Visuals.FromFrame(new Vector3d(pts[i].X, pts[i].Y, pts[i].Z));
                float half = Width * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    Vector3 along = i < n - 1 ? world[i + 1] - world[i] : world[i] - world[i - 1];
                    Vector3 side = Vector3.Cross(along, camPos - world[i]).normalized;
                    Vector3 at = world[i];
                    if (Crackle > 0 && i > 0 && i < n - 1)   // a jagged line that jumps every frame
                        at += (side * (Random.value * 2 - 1) + Vector3.Cross(side, along.normalized) * (Random.value * 2 - 1)) * Crackle;
                    float a = FxCurves.TrailAlpha(now - pts[i].Time, Fade);
                    var col = new Color(1, 1, 1, a);
                    verts.Add(at - side * half); verts.Add(at + side * half);
                    cols.Add(col); cols.Add(col);
                    float u = (float)i / (n - 1);
                    uvs.Add(new Vector2(u, 0)); uvs.Add(new Vector2(u, 1));
                    if (i < n - 1)
                    {
                        int v = i * 2;
                        tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
                        tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 3);
                    }
                }
            }
            mesh.Clear();
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (mat != null) Destroy(mat);
        }
    }

    /// <summary>A trail behind a missile once it's fired (until it's gone, then it fades where it was).</summary>
    public class ModuleFFWTrail : PartModule
    {
        [KSPField] public string color = "255, 255, 255, 255";
        [KSPField] public float width = 1;
        [KSPField] public float fade = 3;
        [KSPField] public float spacing = 5;
        [KSPField] public float flicker = 0;
        [KSPField] public float crackle = 0;

        PartModule missile;
        RibbonTrail trail;

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            missile = Bda.FindMissile(part);
        }

        void Update()
        {
            if (trail != null || missile == null || !Bda.MissileFired(missile)) return;
            trail = RibbonTrail.Create(part.transform, Rgba.Parse(color, new Rgba(1, 1, 1, 1)).ToColor(), width, fade, spacing, flicker, crackle);
        }

        void OnDestroy()
        {
            if (trail != null) trail.Detach();
        }
    }
}
