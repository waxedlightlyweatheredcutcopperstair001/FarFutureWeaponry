using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Shared pieces of the procedural effects: textures drawn in code (no image assets), additive materials, and
    /// the frame effects keep their positions in (the main body's), so floating-origin and Krakensbane shifts don't
    /// move them.
    /// </summary>
    internal static class Visuals
    {
        static Texture2D glow, ribbon;
        static Shader additive;

        public static Color ToColor(this Rgba c) => new Color(c.R, c.G, c.B, c.A);

        public static Shader AdditiveShader
        {
            get
            {
                if (additive == null)
                    additive = Shader.Find("KSP/Particles/Additive") ?? Shader.Find("Legacy Shaders/Particles/Additive");
                return additive;
            }
        }

        /// <summary>Additive material: KSP's particle shaders multiply the texture by _TintColor and the vertex colour.</summary>
        public static Material Additive(Texture tex, Color tint)
        {
            var m = new Material(AdditiveShader) { mainTexture = tex };
            m.SetColor("_TintColor", tint);
            return m;
        }

        /// <summary>A soft round glow: white centre falling to transparent at the edge.</summary>
        public static Texture2D Glow
        {
            get
            {
                if (glow == null)
                {
                    const int n = 64;
                    glow = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                    var px = new Color32[n * n];
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                            float r = Mathf.Sqrt(dx * dx + dy * dy);
                            float core = Mathf.Exp(-r * r * 18f);                 // hot centre
                            float halo = Mathf.Pow(Mathf.Clamp01(1 - r), 2.2f) * 0.55f;
                            byte v = (byte)(Mathf.Clamp01(core + halo) * 255);
                            px[y * n + x] = new Color32(v, v, v, v);
                        }
                    glow.SetPixels32(px);
                    glow.Apply(false, true);
                }
                return glow;
            }
        }

        /// <summary>Cross-section of a beam: a bright core with soft edges (v runs across the ribbon).</summary>
        public static Texture2D Ribbon
        {
            get
            {
                if (ribbon == null)
                {
                    const int w = 4, h = 32;
                    ribbon = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                    var px = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                    {
                        float d = Mathf.Abs((y + 0.5f) / h * 2 - 1);
                        float v = Mathf.Exp(-d * d * 10f) + 0.35f * Mathf.Clamp01(1 - d);
                        byte b = (byte)(Mathf.Clamp01(v) * 255);
                        for (int x = 0; x < w; x++) px[y * w + x] = new Color32(b, b, b, b);
                    }
                    ribbon.SetPixels32(px);
                    ribbon.Apply(false, true);
                }
                return ribbon;
            }
        }

        static Texture2D white;

        /// <summary>A plain white texture (for vertex-coloured meshes).</summary>
        public static Texture2D White
        {
            get
            {
                if (white == null)
                {
                    white = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    white.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
                    white.Apply(false, true);
                }
                return white;
            }
        }

        /// <summary>A unit-radius UV sphere (normals = positions), for the tachyon field.</summary>
        public static Mesh Sphere(int around = 48, int down = 24)
        {
            var v = new Vector3[(around + 1) * (down + 1)];
            var uv = new Vector2[v.Length];
            for (int j = 0; j <= down; j++)
            {
                float th = Mathf.PI * j / down;
                for (int i = 0; i <= around; i++)
                {
                    float ph = 2 * Mathf.PI * i / around;
                    v[j * (around + 1) + i] = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    uv[j * (around + 1) + i] = new Vector2((float)i / around, (float)j / down);
                }
            }
            var t = new int[around * down * 6];
            int k = 0;
            for (int j = 0; j < down; j++)
                for (int i = 0; i < around; i++)
                {
                    int a = j * (around + 1) + i, b = a + around + 1;
                    t[k++] = a; t[k++] = b; t[k++] = a + 1;
                    t[k++] = a + 1; t[k++] = b; t[k++] = b + 1;
                }
            var m = new Mesh { vertices = v, uv = uv, triangles = t, normals = v, colors = new Color[v.Length] };
            m.RecalculateBounds();
            m.MarkDynamic();
            return m;
        }

        static ParticleSystem sparkles;

        /// <summary>
        /// A burst of twinkling glints at a point (one shared world-space particle system, additive): the Tachyon Gun's
        /// green sparkle where it hits.
        /// </summary>
        public static void Sparkle(Vector3 at, int count, Color colour, float size = 0.2f, float speed = 5)
        {
            if (count <= 0) return;
            if (sparkles == null)
            {
                var go = new GameObject("FFW sparkles");
                Object.DontDestroyOnLoad(go);
                sparkles = go.AddComponent<ParticleSystem>();
                sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = sparkles.main;
                main.playOnAwake = false; main.loop = true; main.duration = 1;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 1500;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
                var em = sparkles.emission; em.rateOverTime = 0;
                var sh = sparkles.shape; sh.enabled = false;
                var col = sparkles.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                          new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.3f, 0.3f), new GradientAlphaKey(1, 0.5f), new GradientAlphaKey(0.4f, 0.75f), new GradientAlphaKey(0, 1) });
                col.color = g;
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = Additive(Glow, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                sparkles.Play();
            }
            var p = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                p.position = at + Random.insideUnitSphere * size * 2;
                p.velocity = Random.insideUnitSphere * speed;
                p.startSize = size * Random.Range(0.5f, 1.4f);
                p.startColor = Color.Lerp(colour, Color.white, Random.Range(0f, 0.6f));
                sparkles.Emit(p, 1);
            }
        }

        static Transform Frame => FlightGlobals.currentMainBody != null ? FlightGlobals.currentMainBody.transform : null;

        public static Vector3d ToFrame(Vector3 world)
        {
            Transform f = Frame;
            return f != null ? (Vector3d)f.InverseTransformPoint(world) : (Vector3d)world;
        }

        public static Vector3 FromFrame(Vector3d local)
        {
            Transform f = Frame;
            return f != null ? f.TransformPoint(local) : (Vector3)local;
        }

        public static Camera Camera => FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : Camera.main;

        /// <summary>A unit quad facing +z, for camera-facing sprites.</summary>
        public static Mesh Quad()
        {
            var m = new Mesh();
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }
    }
}
