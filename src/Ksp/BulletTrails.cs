using System.Collections;
using System.Collections.Generic;
using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// A glowing beam left behind certain projectiles (FFW_BULLET_TRAIL nodes name the BULLET types), which stays
    /// in the air and fades after the shot has passed: the gauss cannon's blue streak.
    ///
    /// BDArmory's live bullets are read by reflection each frame. Trail points are stored in the main body's frame,
    /// and the ribbon is rebuilt facing the camera every frame.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BulletTrails : MonoBehaviour
    {
        sealed class TrailDef
        {
            public string Bullet;
            public Color Color;
            public float Width = 0.6f, Fade = 2.5f, Spacing = 4;
        }

        sealed class Trail
        {
            public TrailDef Def;
            public readonly TrailPoints Points = new TrailPoints();
            public object Bullet;           // the PooledBullet it follows, null once that shot has ended
            public float LastAlive;
            public GameObject Go;
            public Mesh Mesh;
            public Material Mat;
            public int SeenFrame;
        }

        readonly Dictionary<string, TrailDef> defs = new Dictionary<string, TrailDef>();
        readonly List<Trail> trails = new List<Trail>();
        readonly Dictionary<object, Trail> byBullet = new Dictionary<object, Trail>();
        readonly List<object> ended = new List<object>();

        // Mesh scratch buffers.
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> cols = new List<Color>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        void Start()
        {
            Bda.Init();
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("FFW_BULLET_TRAIL"))
            {
                var d = new TrailDef { Bullet = n.GetValue("bullet") };
                if (string.IsNullOrEmpty(d.Bullet)) continue;
                d.Color = Rgba.Parse(n.GetValue("color"), new Rgba(0.4f, 0.6f, 1, 1)).ToColor();
                n.TryGetValue("width", ref d.Width);
                n.TryGetValue("fade", ref d.Fade);
                n.TryGetValue("spacing", ref d.Spacing);
                defs[d.Bullet] = d;
            }
            if (defs.Count > 0 && !Bda.BulletsReadable)
            {
                Log.Warn("Projectile trails are off: BDArmory's bullets couldn't be read.");
                defs.Clear();
            }
            Log.Info($"Projectile trails: {defs.Count} bullet type(s).");
        }

        void LateUpdate()
        {
            if (defs.Count == 0) return;
            float now = Time.time;
            int frame = Time.frameCount;

            IEnumerable bullets = Bda.ActiveBullets();
            if (bullets != null)
            {
                foreach (object b in bullets)
                {
                    if (!(b is Component c) || c == null || !c.gameObject.activeInHierarchy) continue;
                    if (!Bda.ReadBullet(b, out string name, out float alive) || !defs.TryGetValue(name, out TrailDef d)) continue;
                    if (byBullet.TryGetValue(b, out Trail t) && (t.Def != d || alive < t.LastAlive))
                    {
                        // The pooled bullet was reused for a new shot: the old trail stays where it is and fades.
                        t.Bullet = null;
                        byBullet.Remove(b);
                        t = null;
                    }
                    if (t == null)
                    {
                        t = NewTrail(d);
                        t.Bullet = b;
                        byBullet[b] = t;
                    }
                    t.LastAlive = alive;
                    t.SeenFrame = frame;
                    Vector3d p = Visuals.ToFrame(c.transform.position);
                    t.Points.Add(p.x, p.y, p.z, now);
                }
            }

            // Shots that ended (hit something or timed out) this frame.
            ended.Clear();
            foreach (var kv in byBullet)
                if (kv.Value.SeenFrame != frame) ended.Add(kv.Key);
            foreach (object b in ended)
            {
                byBullet[b].Bullet = null;
                byBullet.Remove(b);
            }

            Camera cam = Visuals.Camera;
            for (int i = trails.Count - 1; i >= 0; i--)
            {
                Trail t = trails[i];
                t.Points.Prune(now, t.Def.Fade);
                if (t.Bullet == null && t.Points.Faded(now, t.Def.Fade))
                {
                    Destroy(t.Go);
                    Destroy(t.Mesh);
                    Destroy(t.Mat);
                    trails.RemoveAt(i);
                    continue;
                }
                if (cam != null) Build(t, cam.transform.position, now);
            }
        }

        Trail NewTrail(TrailDef d)
        {
            var go = new GameObject("FFW projectile trail");
            var mesh = new Mesh();
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = Visuals.Additive(Visuals.Ribbon, new Color(d.Color.r, d.Color.g, d.Color.b, d.Color.a * 0.5f));
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var t = new Trail { Def = d, Go = go, Mesh = mesh, Mat = mat };
            t.Points.MinSpacing = d.Spacing;
            trails.Add(t);
            return t;
        }

        /// <summary>A camera-facing ribbon through the points, faded by each point's age.</summary>
        void Build(Trail t, Vector3 camPos, float now)
        {
            verts.Clear(); cols.Clear(); uvs.Clear(); tris.Clear();
            var pts = t.Points.Points;
            int n = pts.Count;
            if (n >= 2)
            {
                var world = new Vector3[n];
                for (int i = 0; i < n; i++) world[i] = Visuals.FromFrame(new Vector3d(pts[i].X, pts[i].Y, pts[i].Z));
                float half = t.Def.Width * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    Vector3 along = i < n - 1 ? world[i + 1] - world[i] : world[i] - world[i - 1];
                    Vector3 side = Vector3.Cross(along, camPos - world[i]).normalized * half;
                    float a = FxCurves.TrailAlpha(now - pts[i].Time, t.Def.Fade);
                    var col = new Color(1, 1, 1, a);
                    verts.Add(world[i] - side); verts.Add(world[i] + side);
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
            t.Mesh.Clear();
            t.Mesh.SetVertices(verts);
            t.Mesh.SetColors(cols);
            t.Mesh.SetUVs(0, uvs);
            t.Mesh.SetTriangles(tris, 0);
            t.Mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            foreach (Trail t in trails)
            {
                if (t.Go != null) Destroy(t.Go);
                if (t.Mesh != null) Destroy(t.Mesh);
                if (t.Mat != null) Destroy(t.Mat);
            }
            trails.Clear();
            byBullet.Clear();
        }
    }
}
