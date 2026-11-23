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
        readonly List<Renderer> hidden = new List<Renderer>();
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
            // Cloaking: the ripple passes, then the craft is gone. Uncloaking: it's back at once, the ripple passes over it.
            bool hide = active && !rippling;
            bool overlay = active || rippling;
            if (hide) Hide(); else Show();
            if (!overlay || FfwShaders.Cloak == null) { ClearOverlays(); return; }
            if (builtFor != vessel.parts.Count) { ClearOverlays(); BuildOverlays(); }
            float size = Mathf.Max(5, vessel.vesselSize.magnitude);
            Vector3 c = vessel.CoM;
            float wave = rippling ? size * 0.75f * t / rippleSeconds : -1;
            foreach (PartOverlay o in overlays)
            {
                o.Block.SetVector(WaveId, new Vector4(c.x, c.y, c.z, wave));
                o.Block.SetVector(StateId, new Vector4(1, hide ? 0.25f : 0.6f, 0, 0));
                o.Block.SetColor(ColourId, colour);
                o.Apply();
            }
        }

        /// <summary>Hide every renderer of the craft (parts, their effects and plumes), except the cloak's own overlay.</summary>
        void Hide()
        {
            if (hidden.Count > 0 && builtFor == vessel.parts.Count) return;
            Show();
            foreach (Part p in vessel.parts)
            {
                if (p == null) continue;
                foreach (Renderer r in p.GetComponentsInChildren<Renderer>())
                    if (r != null && r.enabled && !r.name.StartsWith("FFW cloak")) { r.enabled = false; hidden.Add(r); }
            }
        }

        void Show()
        {
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
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
