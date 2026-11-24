using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Hologram Generator: at the press of its button, six semi-transparent copies of the craft appear round it, one in
    /// every direction (left, right, above, below, ahead, behind), flying in formation with it for <see cref="seconds"/>:
    /// the craft's own colours tinted a little blue, flickering and glitching (FarFutureWeaponry/Hologram). FFW optical
    /// tracking will likely take one of them for the real craft (ModuleFFWSeeker). They stay fully visible while the craft
    /// itself is cloaked.
    /// </summary>
    public class ModuleFFWHologram : PartModule
    {
        [KSPField] public float seconds = 30;
        [KSPField] public float cooldown = 60;
        [KSPField] public float ec = 1500;
        [KSPField] public string color = "150, 200, 255, 255";   // the tint
        [KSPField] public float opacity = 0.55f;
        [KSPField(guiActive = true, guiName = "Holograms")] public string status = "ready";

        [KSPEvent(guiActive = true, guiName = "Project holograms")]
        public void Project()
        {
            if (Time.time < readyAt || running || !HighLogic.LoadedSceneIsFlight) return;
            if (!PayEc(part, ec)) { ScreenMessages.PostScreenMessage($"Holograms: need {ec:0} ElectricCharge.", 3, ScreenMessageStyle.UPPER_CENTER); return; }
            Begin();
        }

        [KSPAction("Project holograms")] public void ProjectAction(KSPActionParam p) => Project();

        public override string GetInfo() =>
            $"<b>Holograms:</b> six copies of the craft round it for {seconds:0} s ({ec:0} EC, {cooldown:0} s cooldown). Optical tracking is likely to follow one instead. They show even while the craft is cloaked.";

        bool running;
        float startedAt, readyAt;
        readonly List<GameObject> copies = new List<GameObject>();
        readonly Dictionary<Material, Material> mats = new Dictionary<Material, Material>();
        Vector3[] offsets = new Vector3[6];
        static readonly int StateId = Shader.PropertyToID("_HoloState");
        static Material blank;
        static Material Blank => blank != null ? blank : (blank = new Material(Visuals.AdditiveShader) { name = "FFW hologram blank" });

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            FfwShaders.Load();
        }

        /// <summary>The hologram version of one of the craft's materials: its texture and paint, tinted and see-through.</summary>
        Material HoloMaterial(Material src, Color tint)
        {
            if (src == null) src = Blank;   // (a renderer with no material: plain paint)
            if (mats.TryGetValue(src, out Material m)) return m;
            Texture tex = src != Blank && src.HasProperty("_MainTex") ? src.mainTexture : null;
            Color paint = src != Blank && src.HasProperty("_Color") ? src.color : Color.white;
            paint.a = 1;
            if (FfwShaders.Hologram != null)
            {
                m = new Material(FfwShaders.Hologram);
                if (tex != null) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", src.mainTextureScale); m.SetTextureOffset("_MainTex", src.mainTextureOffset); }
                m.SetColor("_BaseColor", paint);
                m.SetColor("_Color", tint);
                m.SetFloat("_Opacity", opacity);
            }
            else
            {
                // Without the plugin's shaders: KSP's own see-through shader, the texture tinted.
                m = new Material(Shader.Find("KSP/Alpha/Translucent") ?? Shader.Find("Legacy Shaders/Transparent/Diffuse"));
                if (tex != null) m.mainTexture = tex;
                Color c = Color.Lerp(paint, paint * tint, 0.5f); c.a = opacity;
                m.color = c;
            }
            mats[src] = m;
            return m;
        }

        void Begin()
        {
            running = true;
            startedAt = Time.time;
            Color tint = Core.Rgba.Parse(color, new Core.Rgba(0.6f, 0.8f, 1, 1)).ToColor();
            float d = Mathf.Max(10, vessel.vesselSize.magnitude) * 1.3f + 15;
            offsets = new[] { Vector3.right * d, Vector3.left * d, Vector3.up * d, Vector3.down * d, Vector3.forward * d, Vector3.back * d };
            Transform root = vessel.rootPart.transform;
            // The craft's meshes merged once, one mesh per hologram material (in the root part's space), shared by the six
            // copies: a handful of draw calls per copy instead of one per part mesh. Meshes Unity can't read (rare) are
            // copied piece by piece instead.
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var loose = new List<(Mesh mesh, Matrix4x4 local, Material[] mats)>();
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (Part p in vessel.parts)
            {
                if (p == null) continue;
                foreach (MeshFilter mf in p.FindModelComponents<MeshFilter>())
                {
                    var src = mf.GetComponent<MeshRenderer>();
                    // (what a cloak has hidden counts as shown: the holograms still look like the craft)
                    if (src == null || mf.sharedMesh == null || mf.name.StartsWith("FFW ") || !PartOverlay.IsHull(src)) continue;   // (no plumes)
                    if (!(src.enabled && src.gameObject.activeInHierarchy) && !ModuleFFWCloak.HiddenByCloak(src)) continue;
                    Mesh mesh = mf.sharedMesh;
                    Material[] from = src.sharedMaterials;
                    int subs = Mathf.Max(1, mesh.subMeshCount);
                    Matrix4x4 local = toRoot * mf.transform.localToWorldMatrix;
                    var to = new Material[subs];
                    for (int k = 0; k < subs; k++) to[k] = HoloMaterial(from.Length > 0 ? from[Mathf.Min(k, from.Length - 1)] : null, tint);
                    if (!mesh.isReadable) { loose.Add((mesh, local, to)); continue; }
                    for (int k = 0; k < subs; k++)
                    {
                        if (!groups.TryGetValue(to[k], out var list)) groups[to[k]] = list = new List<CombineInstance>();
                        list.Add(new CombineInstance { mesh = mesh, subMeshIndex = Mathf.Min(k, mesh.subMeshCount - 1), transform = local });
                    }
                }
            }
            var merged = new List<(Mesh, Material)>();
            foreach (var kv in groups)
            {
                var m = new Mesh { name = "FFW hologram", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.CombineMeshes(kv.Value.ToArray(), true, true);
                combined.Add(m);
                merged.Add((m, kv.Key));
            }
            for (int i = 0; i < 6; i++)
            {
                var holo = new GameObject("FFW hologram " + i);
                holo.transform.SetPositionAndRotation(root.position, root.rotation);
                foreach (var (mesh, mat) in merged) Piece(holo.transform, mesh, Matrix4x4.identity, new[] { mat });
                foreach (var (mesh, local, ms) in loose) Piece(holo.transform, mesh, local, ms);
                copies.Add(holo);
            }
            Targets.SetHolograms(vessel, Where);
            ScreenGlitch.Play(0.35f, 0.4f);
            Log.Info($"{vessel.vesselName}: holograms projected ({seconds:0} s).");
        }

        readonly List<Mesh> combined = new List<Mesh>();

        static void Piece(Transform parent, Mesh mesh, Matrix4x4 local, Material[] mats)
        {
            var go = new GameObject("FFW hologram piece");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local.GetColumn(3);
            go.transform.localRotation = local.rotation;
            go.transform.localScale = local.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }

        /// <summary>Pay a lump of ElectricCharge, if the craft has it (always, with the infinite electricity cheat on).</summary>
        internal static bool PayEc(Part part, double amount)
        {
            if (CheatOptions.InfiniteElectricity) return true;
            part.vessel.GetConnectedResourceTotals(PartResourceLibrary.ElectricityHashcode, out double have, out _);
            if (have < amount) return false;
            part.RequestResource("ElectricCharge", amount, ResourceFlowMode.ALL_VESSEL);
            return true;
        }

        /// <summary>Where hologram i is now (in formation with the craft), or null when they're gone.</summary>
        Vector3? Where(int i)
        {
            if (!running || vessel == null || i < 0 || i >= offsets.Length) return null;
            return vessel.rootPart.transform.position + vessel.ReferenceTransform.rotation * offsets[i];
        }

        void LateUpdate()
        {
            if (!running || vessel == null) return;
            float t = Time.time - startedAt;
            if (t > seconds) { Stop(); return; }
            Transform root = vessel.rootPart.transform;
            for (int i = 0; i < copies.Count; i++)
            {
                if (copies[i] == null) continue;
                Vector3 jitter = UnityEngine.Random.value < 0.03f ? UnityEngine.Random.insideUnitSphere * 0.6f : Vector3.zero;
                copies[i].transform.SetPositionAndRotation(Where(i).Value + jitter, root.rotation);
            }
            // Flicker in over half a second, fade out over the last second; glitchier as they come and go.
            float level = Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((seconds - t) / 1f);
            float strength = level * (0.85f + 0.15f * Mathf.PerlinNoise(t * 5, 0));
            foreach (Material m in mats.Values)
            {
                if (m == null) continue;
                if (FfwShaders.Hologram != null) m.SetVector(StateId, new Vector4(strength, 3.1f, 1.2f - level, 0));
                else { Color c = m.color; c.a = opacity * strength; m.color = c; }
            }
            status = $"projecting ({seconds - t:0} s)";
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || running) return;
            status = Time.time < readyAt ? $"recharging ({readyAt - Time.time:0} s)" : "ready";
        }

        void Stop()
        {
            running = false;
            readyAt = Time.time + cooldown;
            Targets.SetHolograms(vessel, null);
            foreach (GameObject g in copies) if (g != null) Destroy(g);
            copies.Clear();
            foreach (Material m in mats.Values) if (m != null) Destroy(m);
            mats.Clear();
            foreach (Mesh m in combined) if (m != null) Destroy(m);
            combined.Clear();
        }

        void OnDestroy() { if (running) Stop(); }
    }
}
