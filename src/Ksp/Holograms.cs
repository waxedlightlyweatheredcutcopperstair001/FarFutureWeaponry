using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Hologram Generator: six translucent copies of the craft appear round it, one in every direction (left, right, above,
    /// below, ahead, behind), flying in formation with it, flickering and glitching (FarFutureWeaponry/Hologram), for
    /// <see cref="seconds"/>. FFW optical tracking will likely take one of them for the real craft (ModuleFFWSeeker), and
    /// BDArmory's heat and radar seekers get decoys too: the part's own flare and chaff dispensers (BDArmory CMDroppers)
    /// fire from the holograms' positions while they run.
    /// </summary>
    public class ModuleFFWHologram : PartModule
    {
        [KSPField] public float seconds = 30;
        [KSPField] public float cooldown = 60;
        [KSPField] public float ec = 1500;
        [KSPField] public float decoyInterval = 0.8f;   // s between decoys
        [KSPField] public string color = "90, 200, 255, 255";
        [KSPField(guiActive = true, guiName = "Holograms")] public string status = "ready";

        [KSPEvent(guiActive = true, guiName = "Project holograms")]
        public void Project()
        {
            if (Time.time < readyAt || running) return;
            if (!PayEc(part, ec)) { ScreenMessages.PostScreenMessage($"Holograms: need {ec:0} ElectricCharge.", 3, ScreenMessageStyle.UPPER_CENTER); return; }
            Start();
        }

        [KSPAction("Project holograms")] public void ProjectAction(KSPActionParam p) => Project();

        public override string GetInfo() =>
            $"<b>Holograms:</b> six copies of the craft round it for {seconds:0} s ({ec:0} EC, {cooldown:0} s cooldown). Optical tracking is likely to follow one instead; they also throw decoy flares and chaff.";

        bool running;
        float startedAt, readyAt, nextDecoy;
        readonly List<GameObject> copies = new List<GameObject>();
        Material mat;
        Vector3[] offsets = new Vector3[6];
        readonly List<(PartModule dropper, bool flare)> droppers = new List<(PartModule, bool)>();
        FieldInfo eject;
        int decoyTurn;
        static readonly int ColourId = Shader.PropertyToID("_Color"), StateId = Shader.PropertyToID("_HoloState");

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            FfwShaders.Load();
            Type cmd = Bda.FindType("BDArmory.CounterMeasure.CMDropper");
            eject = cmd?.GetField("ejectTransform", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            FieldInfo type = cmd?.GetField("countermeasureType");
            if (cmd != null)
                foreach (PartModule m in part.Modules)
                    if (cmd.IsInstanceOfType(m)) droppers.Add((m, (type?.GetValue(m) as string ?? "flare").ToLowerInvariant() == "flare"));
        }

        internal static bool PayEc(Part part, double amount)
        {
            part.vessel.GetConnectedResourceTotals(PartResourceLibrary.ElectricityHashcode, out double have, out _);
            if (have < amount) return false;
            part.RequestResource("ElectricCharge", amount, ResourceFlowMode.ALL_VESSEL);
            return true;
        }

        void Start()
        {
            running = true;
            startedAt = Time.time;
            Color c = Core.Rgba.Parse(color, new Core.Rgba(0.35f, 0.8f, 1, 1)).ToColor();
            if (FfwShaders.Hologram != null) { mat = new Material(FfwShaders.Hologram); mat.SetColor(ColourId, c); }
            else mat = Visuals.Additive(Visuals.White, new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, 0.5f));
            float d = Mathf.Max(10, vessel.vesselSize.magnitude) * 1.3f + 15;
            offsets = new[] { Vector3.right * d, Vector3.left * d, Vector3.up * d, Vector3.down * d, Vector3.forward * d, Vector3.back * d };
            Transform root = vessel.rootPart.transform;
            for (int i = 0; i < 6; i++)
            {
                var holo = new GameObject("FFW hologram " + i);
                holo.transform.SetPositionAndRotation(root.position, root.rotation);
                foreach (Part p in vessel.parts)
                {
                    if (p == null) continue;
                    foreach (MeshFilter mf in p.FindModelComponents<MeshFilter>())
                    {
                        var src = mf.GetComponent<MeshRenderer>();
                        if (src == null || !src.enabled || mf.sharedMesh == null || mf.name.StartsWith("FFW ")) continue;
                        var go = new GameObject("FFW hologram piece");
                        go.transform.SetParent(holo.transform, false);
                        go.transform.position = mf.transform.position;
                        go.transform.rotation = mf.transform.rotation;
                        go.transform.localScale = mf.transform.lossyScale;
                        go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                        var r = go.AddComponent<MeshRenderer>();
                        var mats = new Material[Mathf.Max(1, mf.sharedMesh.subMeshCount)];
                        for (int k = 0; k < mats.Length; k++) mats[k] = mat;
                        r.sharedMaterials = mats;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                    }
                }
                copies.Add(holo);
            }
            Targets.SetHolograms(vessel, Where);
            ScreenGlitch.Play(0.35f, 0.4f);
            Log.Info($"{vessel.vesselName}: holograms projected ({seconds:0} s).");
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
            if (FfwShaders.Hologram != null) mat.SetVector(StateId, new Vector4(level * (0.8f + 0.2f * Mathf.PerlinNoise(t * 5, 0)), 3.1f, 1.2f - level, 0));
            status = $"projecting ({seconds - t:0} s)";
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (!running) { status = Time.time < readyAt ? $"recharging ({readyAt - Time.time:0} s)" : "ready"; return; }
            if (Time.time < nextDecoy || droppers.Count == 0 || eject == null) return;
            nextDecoy = Time.time + decoyInterval;
            // A decoy from one of the holograms in turn: a flare, then chaff (moving the dispenser's eject point there).
            var (dropper, _) = droppers[decoyTurn % droppers.Count];
            Vector3? at = Where(decoyTurn % 6);
            decoyTurn++;
            if (dropper == null || at == null) return;
            if (!(eject.GetValue(dropper) is Transform tf) || tf == null) { dropper.GetType().GetMethod("DropCM")?.Invoke(dropper, null); tf = eject.GetValue(dropper) as Transform; if (tf == null) return; }
            Vector3 was = tf.position;
            tf.position = at.Value;
            dropper.GetType().GetMethod("DropCM")?.Invoke(dropper, null);
            tf.position = was;
        }

        void Stop()
        {
            running = false;
            readyAt = Time.time + cooldown;
            Targets.SetHolograms(vessel, null);
            foreach (GameObject g in copies) if (g != null) Destroy(g);
            copies.Clear();
            if (mat != null) Destroy(mat);
        }

        void OnDestroy() { if (running) Stop(); }
    }
}
