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

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Cloak outline"),
         UI_Toggle(disabledText = "Off", enabledText = "On")]
        public bool outline = true;     // the faint outline and ripple (off: the parts simply vanish)

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
        readonly OverlaySet overlays = new OverlaySet();

        /// <summary>Leaving the scene: forget every craft and renderer (dead references otherwise pile up).</summary>
        internal static void ClearAll() { cloaked.Clear(); owners.Clear(); allHidden.Clear(); }
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
                reported.Clear();
            }
            ReconcileBda();
            if (active) { cloaked.Add(vessel); if (!owners.TryGetValue(vessel, out var o) || o == null) owners[vessel] = this; }
            else if (owners.TryGetValue(vessel, out var o2) && o2 == this) { owners.Remove(vessel); if (!AnyActive()) cloaked.Remove(vessel); }
            status = !cloakOn ? "off" : active ? "cloaked" : "NO POWER";
        }

        static FieldInfo bdaEnabled, bdaEnabling, bdaDisabling;

        /// <summary>
        /// BDArmory's cloak ignores a "disable" while it's still switching on (and an "enable" while switching off): once
        /// it's settled, bring it in line with ours, or a quick on-off leaves its stealth on for free.
        /// </summary>
        void ReconcileBda()
        {
            if (bdaCloak == null) return;
            Type t = bdaCloak.GetType();
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            if (bdaEnabled == null) { bdaEnabled = t.GetField("cloakEnabled", F); bdaEnabling = t.GetField("enabling", F); bdaDisabling = t.GetField("disabling", F); }
            if (bdaEnabled == null) return;
            bool busy = (bdaEnabling != null && (bool)bdaEnabling.GetValue(bdaCloak)) || (bdaDisabling != null && (bool)bdaDisabling.GetValue(bdaCloak));
            if (busy || (bool)bdaEnabled.GetValue(bdaCloak) == active) return;
            t.GetMethod(active ? "EnableCloak" : "DisableCloak")?.Invoke(bdaCloak, null);
        }

        void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
            if (owners.TryGetValue(vessel, out var owner) && owner != null && owner != this) { Show(); ClearOverlays(); return; }
            float t = Time.time - switchedAt;
            bool rippling = t < rippleSeconds;
            bool overlay = active || rippling;
            Vector3 c = vessel.CoM;
            // BDArmory's cloak fades every part's material opacity (Part.SetOpacity) as it switches; ours already hides
            // them, and with Deferred a see-through part material darkens the scene. Keep them opaque.
            if (bdaCloak != null && Time.time - switchedAt < 3)
                foreach (Part p in vessel.parts)
                {
                    if (p == null) continue;
                    try { p.SetOpacity(1); }
                    catch (NullReferenceException) { p.ResetModelRenderersCache(); }   // (its cached renderers included destroyed ones)
                }
            // Looking from inside (IVA): give the interiors back; outside again: hide them again.
            bool iva = CameraManager.Instance != null && (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA ||
                                                         CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Internal);
            if (iva == interiors) { Show(); interiors = !iva; }
            // The wave sweeps out from the middle to past the far end: each part vanishes (or comes back) as it reaches it.
            float far = 2;
            foreach (Part p in vessel.parts) if (p != null) far = Mathf.Max(far, Vector3.Distance(p.transform.position, c) + 2);
            float wave = rippling ? far * Mathf.Clamp01(t / rippleSeconds) : -1;
            if (FfwShaders.Cloak == null) { if (active && !rippling) Hide(null, 0); else Show(); ClearOverlays(); }
            else if (active) Hide(rippling ? (Vector3?)c : null, wave);
            else if (rippling) HideOutside(c, wave);
            else Show();
            if (Time.time >= nextRescan) nextRescan = Time.time + 0.5f;
            if (FfwShaders.Cloak == null) return;
            if (!overlay || !outline) { ClearOverlays(); return; }
            overlays.Sync(vessel, FfwShaders.Cloak, "FFW cloak", false);
            float sweep = rippling ? (active ? 1 : -1) : 0;
            foreach (PartOverlay o in overlays.Overlays)
            {
                o.Block.SetVector(WaveId, new Vector4(c.x, c.y, c.z, wave));
                o.Block.SetVector(StateId, new Vector4(1, active && !rippling ? 0.25f : 0.6f, sweep, 0));
                o.Block.SetColor(ColourId, colour);
                o.Apply();
            }
        }

        // Every renderer any cloak has switched off (the hologram generator still copies those).
        static readonly HashSet<Renderer> allHidden = new HashSet<Renderer>();
        internal static bool HiddenByCloak(Renderer r) => r != null && allHidden.Contains(r);

        // Each part's own renderers hidden by the cloak: its hull (vanishing part by part as the wave reaches it) and its
        // effects (plumes, glows, particles: gone the moment it starts cloaking, back only once it has fully uncloaked).
        readonly Dictionary<Part, List<Renderer>> hullHidden = new Dictionary<Part, List<Renderer>>();
        readonly Dictionary<Part, List<Renderer>> fxHidden = new Dictionary<Part, List<Renderer>>();
        float nextRescan;

        void Hide(Vector3? centre, float radius)
        {
            bool rescan = Time.time >= nextRescan;
            foreach (Part p in vessel.parts)
            {
                if (p == null) continue;
                SetHidden(hullHidden, p, centre == null || Vector3.Distance(p.transform.position, centre.Value) <= radius, true, rescan);
                SetHidden(fxHidden, p, true, false, rescan);
            }
        }

        void HideOutside(Vector3 centre, float radius)
        {
            bool rescan = Time.time >= nextRescan;
            foreach (Part p in vessel.parts)
            {
                if (p == null) continue;
                SetHidden(hullHidden, p, Vector3.Distance(p.transform.position, centre) > radius, true, rescan);
                SetHidden(fxHidden, p, true, false, rescan);
            }
        }

        // Diagnostics: renderers the cloak switches off that aren't the parts' own meshes (effects, interiors, anything
        // other mods hang on a part) are logged once per activation (path, kind, shader), so a KSP.log shows them.
        readonly HashSet<Renderer> reported = new HashSet<Renderer>();

        void Report(Part p, Renderer r, string kind)
        {
            Transform model = p.transform.Find("model");
            if (kind == "hull" && model != null && r.transform.IsChildOf(model)) return;   // (the part's own meshes: expected)
            if (!reported.Add(r)) return;
            string path = r.name;
            for (Transform t = r.transform.parent; t != null && t != p.transform; t = t.parent) path = t.name + "/" + path;
            var shaders = new List<string>();
            foreach (Material m in r.sharedMaterials) shaders.Add(m != null && m.shader != null ? m.shader.name : "-");
            Log.Info($"cloak hides [{kind}] {p.partInfo?.name}: {path} ({r.GetType().Name}, layer {r.gameObject.layer}, {string.Join(", ", shaders)})");
        }

        bool interiors = true;      // hide the parts' interiors (IVAs) too: not while the player is looking from inside

        void SetHidden(Dictionary<Part, List<Renderer>> hidden, Part p, bool hide, bool hull, bool rescan)
        {
            bool isHidden = hidden.TryGetValue(p, out var list);
            if (hide && (!isHidden || rescan))
            {
                // (also catches what switches on while cloaked: an engine lighting up)
                if (!isHidden) { list = new List<Renderer>(); hidden[p] = list; }
                foreach (Renderer r in p.GetComponentsInChildren<Renderer>())
                {
                    if (r == null || !r.enabled || r.name.StartsWith("FFW ") || Visuals.OnCamera(r)) continue;
                    if (r.GetComponentInParent<Part>() != p) continue;      // (a part attached below this one: its own turn)
                    bool fx = !(r is MeshRenderer || r is SkinnedMeshRenderer) || !PartOverlay.IsHull(r);
                    if (fx == hull) continue;
                    r.enabled = false; list.Add(r); allHidden.Add(r);
                    Report(p, r, fx ? "effect" : "hull");
                }
                // The interior (IVA): stock KSP and FreeIva draw it in the flight view, fenced in by the hull and by depth
                // masks (they write depth, no colour, so it only shows through the windows). With the hull gone the masks
                // stand in the open and blank out whatever is behind them; with Deferred they also wreck the lighting.
                if (hull && interiors && p.internalModel != null)
                    foreach (Renderer r in p.internalModel.GetComponentsInChildren<Renderer>())
                        if (r != null && r.enabled && !Visuals.OnCamera(r)) { r.enabled = false; list.Add(r); allHidden.Add(r); Report(p, r, "interior"); }
            }
            else if (!hide && isHidden)
            {
                foreach (Renderer r in list) if (r != null) { r.enabled = true; allHidden.Remove(r); }
                hidden.Remove(p);
            }
        }

        /// <summary>Every part back in view.</summary>
        void Show()
        {
            foreach (var d in new[] { hullHidden, fxHidden })
            {
                foreach (var kv in d) foreach (Renderer r in kv.Value) if (r != null) { r.enabled = true; allHidden.Remove(r); }
                d.Clear();
            }
        }

        void ClearOverlays()
        {
            overlays.Clear();
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
