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
        readonly List<GameObject> objects = new List<GameObject>();
        public readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        public readonly Material Material;
        readonly bool follow;

        /// <param name="followSource">shown only while the part's own renderer is (false for the cloak, which hides them)</param>
        public PartOverlay(Part part, Shader shader, string name, bool followSource = true)
        {
            follow = followSource;
            Material = new Material(shader) { name = name };
            foreach (MeshFilter mf in part.FindModelComponents<MeshFilter>())
            {
                var src = mf != null ? mf.GetComponent<MeshRenderer>() : null;
                if (src == null || mf.sharedMesh == null || mf.name.StartsWith("FFW ")) continue;   // (not other overlays)
                if (!IsHull(src)) continue;                                                        // (not plumes and effects)
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
                var src = r.transform.parent != null ? r.transform.parent.GetComponent<MeshRenderer>() : null;
                r.enabled = !follow || src == null || src.enabled;
                r.SetPropertyBlock(Block);
            }
        }

        public void Destroy()
        {
            foreach (GameObject go in objects) if (go != null) Object.Destroy(go);
            objects.Clear(); renderers.Clear();
            if (Material != null) Object.Destroy(Material);
        }
    }
}
