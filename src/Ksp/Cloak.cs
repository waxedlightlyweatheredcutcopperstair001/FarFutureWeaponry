using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Cloaking Device: light ripples over the craft and it all but disappears, engine plumes included, leaving a faint
    /// bluish outline and a slight bending of what's behind it (FarFutureWeaponry/CloakOverlay over the part contours, the
    /// parts' own renderers hidden). While it's on:
    /// - FFW optical tracking can't see the craft (Targets.Sense);
    /// - BDArmory's heat and visual detection are fooled through BDArmory's own ModuleCloakingDevice on the same part
    ///   (optical and thermal; switched with this one);
    /// - anti-radiation missiles get no radar pings from it (a Harmony prefix on MissileBase.ReceiveRadarPing).
    /// It draws ElectricCharge while on.
    /// </summary>
    public class ModuleFFWCloak : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Cloak"),
         UI_Toggle(disabledText = "Off", enabledText = "On", scene = UI_Scene.Flight)]
        public bool cloakOn = false;

        [KSPField] public float ecPerSecond = 10;
        [KSPField] public float rippleSeconds = 0.8f;
        [KSPField] public string color = "120, 180, 255, 255";
        [KSPField(guiActive = true, guiName = "Cloak status")] public string status = "";

        [KSPAction("Toggle cloak")] public void ToggleAction(KSPActionParam p) => cloakOn = !cloakOn;
        [KSPAction("Cloak on")] public void OnAction(KSPActionParam p) => cloakOn = true;
        [KSPAction("Cloak off")] public void OffAction(KSPActionParam p) => cloakOn = false;

        public override string GetInfo() =>
            $"<b>Cloak:</b> the craft and its engine plumes all but vanish. Fools optical, infrared and anti-radiation tracking. {ecPerSecond:0.#} EC/s.";

        static readonly HashSet<Vessel> cloaked = new HashSet<Vessel>();
        static readonly Dictionary<Vessel, ModuleFFWCloak> owners = new Dictionary<Vessel, ModuleFFWCloak>();   // (one per craft draws and hides)
        public static bool IsCloaked(Vessel v) => v != null && cloaked.Contains(v);

        bool active;            // powered and on
        float switchedAt = -10;
        readonly List<PartOverlay> overlays = new List<PartOverlay>();
        int builtFor = -1;
        PartModule bdaCloak;
        Color colour;
        static readonly int WaveId = Shader.PropertyToID("_Wave"), StateId = Shader.PropertyToID("_CloakState"), ColourId = Shader.PropertyToID("_Color");

        public override void OnStart(StartState state)
        {
            colour = Core.Rgba.Parse(color, new Core.Rgba(0.45f, 0.7f, 1, 1)).ToColor();
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            FfwShaders.Load();
            InstallPatch();
            Type t = Bda.FindType("BDArmory.CounterMeasure.ModuleCloakingDevice");
            if (t != null) foreach (PartModule m in part.Modules) if (t.IsInstanceOfType(m)) { bdaCloak = m; break; }
            // (BDArmory's cloak is switched by this one: its own button would leave the craft visible)
            if (bdaCloak != null && bdaCloak.Events["Toggle"] != null) bdaCloak.Events["Toggle"].guiActive = false;
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
            bool want = cloakOn;
            if (want)
            {
                double need = ecPerSecond * TimeWarp.fixedDeltaTime;
                if (part.RequestResource("ElectricCharge", need, ResourceFlowMode.ALL_VESSEL) < need * 0.99) want = false;
            }
            if (want != active)
            {
                active = want;
                switchedAt = Time.time;
                if (bdaCloak != null) bdaCloak.GetType().GetMethod(active ? "EnableCloak" : "DisableCloak")?.Invoke(bdaCloak, null);
                Log.Info($"{vessel.vesselName}: cloak {(active ? "on" : "off")}.");
            }
            if (active) { cloaked.Add(vessel); if (!owners.TryGetValue(vessel, out var o) || o == null) owners[vessel] = this; }
            else if (owners.TryGetValue(vessel, out var o2) && o2 == this) { owners.Remove(vessel); if (!AnyActive()) cloaked.Remove(vessel); }
            status = !cloakOn ? "off" : active ? "cloaked" : "NO POWER";
        }

        void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
            if (owners.TryGetValue(vessel, out var owner) && owner != null && owner != this) { Show(); ClearOverlays(); return; }
            float t = Time.time - switchedAt;
            bool rippling = t < rippleSeconds;
            bool overlay = active || rippling;
            Vector3 c = vessel.CoM;
            // The wave sweeps out from the middle to past the far end: each part vanishes (or comes back) as it reaches it.
            float far = 2;
            foreach (Part p in vessel.parts) if (p != null) far = Mathf.Max(far, Vector3.Distance(p.transform.position, c) + 2);
            float wave = rippling ? far * Mathf.Clamp01(t / rippleSeconds) : -1;
            if (FfwShaders.Cloak == null) { if (active && !rippling) Hide(null, 0); else Show(); ClearOverlays(); return; }
            if (active) { Hide(rippling ? (Vector3?)c : null, wave); if (Time.time >= nextRescan) nextRescan = Time.time + 0.5f; }
            else if (rippling) HideOutside(c, wave);
            else Show();
            if (!overlay) { ClearOverlays(); return; }
            if (builtFor != vessel.parts.Count) { ClearOverlays(); BuildOverlays(); }
            float sweep = rippling ? (active ? 1 : -1) : 0;
            foreach (PartOverlay o in overlays)
            {
                o.Block.SetVector(WaveId, new Vector4(c.x, c.y, c.z, wave));
                o.Block.SetVector(StateId, new Vector4(1, active && !rippling ? 0.25f : 0.6f, sweep, 0));
                o.Block.SetColor(ColourId, colour);
                o.Apply();
            }
        }

        // Each part's own renderers, hidden while it's cloaked (by part: they vanish one by one as the wave reaches them).
        readonly Dictionary<Part, List<Renderer>> hiddenBy = new Dictionary<Part, List<Renderer>>();

        void Hide(Vector3? centre, float radius)
        {
            foreach (Part p in vessel.parts)
            {
                if (p == null) continue;
                bool reached = centre == null || Vector3.Distance(p.transform.position, centre.Value) <= radius;
                SetHidden(p, reached);
            }
        }

        void HideOutside(Vector3 centre, float radius)
        {
            foreach (Part p in vessel.parts)
                if (p != null) SetHidden(p, Vector3.Distance(p.transform.position, centre) > radius);
        }

        float nextRescan;

        void SetHidden(Part p, bool hide)
        {
            bool isHidden = hiddenBy.TryGetValue(p, out var list);
            if (hide && isHidden && Time.time >= nextRescan)
            {
                // (plumes and effects that come on while cloaked: an engine lighting up)
                foreach (Renderer r in p.GetComponentsInChildren<Renderer>())
                    if (r != null && r.enabled && !r.name.StartsWith("FFW cloak")) { r.enabled = false; list.Add(r); }
                return;
            }
            if (hide == isHidden) return;
            if (hide)
            {
                list = new List<Renderer>();
                foreach (Renderer r in p.GetComponentsInChildren<Renderer>())
                    if (r != null && r.enabled && !r.name.StartsWith("FFW cloak")) { r.enabled = false; list.Add(r); }
                hiddenBy[p] = list;
            }
            else
            {
                foreach (Renderer r in list) if (r != null) r.enabled = true;
                hiddenBy.Remove(p);
            }
        }

        /// <summary>Every part back in view.</summary>
        void Show()
        {
            foreach (var kv in hiddenBy) foreach (Renderer r in kv.Value) if (r != null) r.enabled = true;
            hiddenBy.Clear();
        }

        void BuildOverlays()
        {
            builtFor = vessel.parts.Count;
            foreach (Part p in vessel.parts)
            {
                if (p == null) continue;
                var o = new PartOverlay(p, FfwShaders.Cloak, "FFW cloak", false);
                if (o.Empty) o.Destroy(); else overlays.Add(o);
            }
        }

        void ClearOverlays()
        {
            foreach (PartOverlay o in overlays) o.Destroy();
            overlays.Clear();
            if (!active) builtFor = -1;
        }

        bool AnyActive()
        {
            foreach (Part p in vessel.parts)
                foreach (PartModule m in p.Modules)
                    if (m is ModuleFFWCloak c && c != this && c.active) return true;
            return false;
        }

        void OnDestroy()
        {
            Show();
            ClearOverlays();
            if (vessel != null)
            {
                if (owners.TryGetValue(vessel, out var o) && o == this) owners.Remove(vessel);
                if (!AnyActive()) cloaked.Remove(vessel);
            }
        }

        // Anti-radiation missiles: no pings from a cloaked craft.
        static bool PingPrefix(Vessel vSource) => !IsCloaked(vSource);

        static bool patched;
        internal static void InstallPatch()
        {
            if (patched) return;
            patched = true;
            Type mb = Bda.FindType("BDArmory.Weapons.Missiles.MissileBase");
            MethodInfo ping = null;
            if (mb != null)
                foreach (MethodInfo m in mb.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    if (m.Name == "ReceiveRadarPing" && m.GetParameters().Length == 5) { ping = m; break; }
            if (ping == null) { Log.Warn("BDArmory's MissileBase.ReceiveRadarPing not found: cloaks don't hide radar emissions."); return; }
            HarmonyBridge.Prefix(ping, typeof(ModuleFFWCloak).GetMethod(nameof(PingPrefix), BindingFlags.Static | BindingFlags.NonPublic));
        }
    }
}
