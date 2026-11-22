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
