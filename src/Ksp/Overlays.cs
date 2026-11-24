using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// The plugin's own shaders (Shaders/farfutureweaponry-windows.bundle, built by tools/shaders.py on Windows): rust,
    /// the contour wave, the phase lens. Missing (a Linux test build, or a load failure): the effects fall back on
    /// plainer ones.
    /// </summary>
    internal static class FfwShaders
    {
        static bool loaded;
        public static Shader Rust, Wave, PhaseLens, Shield, Cloak, Hologram;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            string plugins = Path.GetDirectoryName(typeof(FfwShaders).Assembly.Location);
            string path = Path.Combine(Path.GetDirectoryName(plugins), "Shaders", "farfutureweaponry-windows.bundle");
            if (!File.Exists(path)) { Log.Warn("No shader bundle (" + path + "): rust, the Kraken wave and the phase lens use plainer effects."); return; }
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) { Log.Warn("The shader bundle could not be loaded."); return; }
            foreach (Shader s in bundle.LoadAllAssets<Shader>())
            {
                if (!s.isSupported) { Log.Warn($"Shader {s.name} isn't supported on this graphics API."); continue; }
                switch (s.name)
                {
                    case "FarFutureWeaponry/RustOverlay": Rust = s; break;
                    case "FarFutureWeaponry/ContourWave": Wave = s; break;
                    case "FarFutureWeaponry/PhaseLens": PhaseLens = s; break;
                    case "FarFutureWeaponry/ShieldOverlay": Shield = s; break;
                    case "FarFutureWeaponry/CloakOverlay": Cloak = s; break;
                    case "FarFutureWeaponry/Hologram": Hologram = s; break;
                }
            }
            Log.Info($"Shaders: rust {(Rust != null ? "ok" : "MISSING")}, contour wave {(Wave != null ? "ok" : "MISSING")}, phase lens {(PhaseLens != null ? "ok" : "MISSING")}, " +
                     $"shield {(Shield != null ? "ok" : "MISSING")}, cloak {(Cloak != null ? "ok" : "MISSING")}, hologram {(Hologram != null ? "ok" : "MISSING")}.");
        }
    }

    /// <summary>
    /// A second material drawn over a part's own meshes: for each of the part's mesh renderers, a child object sharing
    /// its mesh with the overlay material on every submesh (drawn just in front of the surface). Parameters go through one
    /// property block for the whole part.
    /// </summary>
    internal sealed class PartOverlay
    {
        readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
        readonly List<MeshRenderer> sources = new List<MeshRenderer>();     // (each overlay renderer's part renderer)
        readonly List<GameObject> objects = new List<GameObject>();
        public readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        public readonly Material Material;
        readonly bool follow;
        readonly Part part;
        public Part Part => part;

        // One material per overlay kind, shared by every part (their parameters go through each one's property block).
        static readonly Dictionary<string, Material> shared = new Dictionary<string, Material>();
        static Material Shared(Shader shader, string name)
        {
            string key = name + "|" + shader.name;
            if (!shared.TryGetValue(key, out Material m) || m == null) shared[key] = m = new Material(shader) { name = name };
            return m;
        }

        /// <param name="followSource">shown only while the part's own renderer is (false for the cloak, which hides them)</param>
        public PartOverlay(Part part, Shader shader, string name, bool followSource = true)
        {
            follow = followSource;
            this.part = part;
            Material = Shared(shader, name);
            foreach (MeshFilter mf in part.FindModelComponents<MeshFilter>())
            {
                var src = mf != null ? mf.GetComponent<MeshRenderer>() : null;
                if (src == null || mf.sharedMesh == null || mf.name.StartsWith("FFW ")) continue;   // (not other overlays)
                if (!IsHull(src)) continue;                                                        // (not plumes and effects)
                if (!mf.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal)) continue;   // (no normals: nothing to outline)
                var go = new GameObject(name);
                go.layer = mf.gameObject.layer;
                go.transform.SetParent(mf.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                var mats = new Material[Mathf.Max(1, mf.sharedMesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++) mats[i] = Material;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = !follow || src.enabled;
                sources.Add(src);
                renderers.Add(r);
                objects.Add(go);
            }
        }

        public bool Empty => renderers.Count == 0;

        static readonly string[] EffectShaders = { "Waterfall", "Firefly", "Particles", "Particle", "Additive", "Unlit", "Plume", "Smoke", "FX/" };

        /// <summary>
        /// Whether a renderer is part of the hull rather than an effect: engine plumes (Waterfall's meshes, which its
        /// shaders stretch far beyond the mesh itself), glows and particles get no overlay.
        /// </summary>
        public static bool IsHull(Renderer r)
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || m.shader == null) continue;
                string s = m.shader.name;
                foreach (string e in EffectShaders) if (s.IndexOf(e, System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            }
            return true;
        }

        /// <summary>Push the block's values to every renderer (and follow the part's own renderers being shown or hidden).</summary>
        public void Apply()
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null) continue;
                MeshRenderer src = sources[i];
                r.enabled = !follow || src == null || src.enabled;
                r.SetPropertyBlock(Block);
            }
        }

        public void Destroy()
        {
            foreach (GameObject go in objects) if (go != null) { go.transform.SetParent(null, false); Object.Destroy(go); }
            // (KSP caches each part's renderers; ours were in that list. Let it re-gather, or its own code, such as
            // highlighting and part opacity, hits the destroyed ones.)
            if (objects.Count > 0 && part != null) part.ResetModelRenderersCache();
            objects.Clear(); renderers.Clear(); sources.Clear();
        }
    }

    /// <summary>
    /// The overlays of one kind over a whole craft, one per part, kept in step with it: parts that come (docking) get one,
    /// parts that go (shot off, dropped) lose theirs; the others are left as they are.
    /// </summary>
    internal sealed class OverlaySet
    {
        readonly Dictionary<Part, PartOverlay> byPart = new Dictionary<Part, PartOverlay>();
        readonly List<PartOverlay> list = new List<PartOverlay>();
        readonly List<Part> gone = new List<Part>();
        int count = -1;

        public List<PartOverlay> Overlays => list;
        public int Count => list.Count;

        public void Sync(Vessel v, Shader shader, string name, bool follow = true)
        {
            if (v == null || v.parts.Count == count) return;
            count = v.parts.Count;
            gone.Clear();
            foreach (var kv in byPart) if (kv.Key == null || kv.Key.vessel != v) gone.Add(kv.Key);
            foreach (Part p in gone) { byPart[p]?.Destroy(); byPart.Remove(p); }
            foreach (Part p in v.parts)
            {
                if (p == null || byPart.ContainsKey(p)) continue;
                var o = new PartOverlay(p, shader, name, follow);
                if (o.Empty) { o.Destroy(); byPart[p] = null; } else byPart[p] = o;
            }
            list.Clear();
            foreach (PartOverlay o in byPart.Values) if (o != null) list.Add(o);
        }

        public void Clear()
        {
            foreach (PartOverlay o in byPart.Values) o?.Destroy();
            byPart.Clear(); list.Clear(); count = -1;
        }
    }
}
