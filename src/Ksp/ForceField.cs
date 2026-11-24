using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Force Field Generator: while it's up, damage to the craft (bullets, lasers, explosions, nukes: everything that goes
    /// through BDArmory's HitpointTracker) is blocked and paid for in ElectricCharge instead, in proportion to the damage.
    /// When the charge runs short the field collapses and damage gets through. Tachyon weapons pass through it, as does the
    /// Quantum Phase-Shift Missile. Needs Harmony (the damage is intercepted with prefixes on BDArmory's methods).
    /// Looks: a shimmer sweeps over the hull as it comes up, then a steady electric-blue layer over the part contours;
    /// each blocked hit flashes bright blue.
    /// </summary>
    public class ModuleFFWForceField : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Force field"),
         UI_Toggle(disabledText = "Off", enabledText = "On", scene = UI_Scene.All, affectSymCounterparts = UI_Scene.All)]
        public bool fieldOn = false;

        [KSPField] public float idleEc = 3;          // EC/s while it's up
        [KSPField] public float ecPerDamage = 2;     // EC per point of damage blocked
        [KSPField] public float ecPerArmor = 0.02f;  // EC per cm³ of armour it saves
        [KSPField] public string color = "80, 170, 255, 255";

        [KSPField(guiActive = true, guiName = "Field")] public string status = "";

        [KSPAction("Toggle force field")] public void ToggleAction(KSPActionParam p) => fieldOn = !fieldOn;
        [KSPAction("Force field on")] public void OnAction(KSPActionParam p) => fieldOn = true;
        [KSPAction("Force field off")] public void OffAction(KSPActionParam p) => fieldOn = false;

        public override string GetInfo() =>
            $"<b>Force field:</b> blocks incoming damage while on, paid for in ElectricCharge ({ecPerDamage:0.#} EC per point of damage; {idleEc:0.#} EC/s idle). Tachyon weapons and phase-shifted missiles get through. Needs Harmony.";

        bool up;                    // powered and on
        float upSince;
        readonly OverlaySet overlays = new OverlaySet();
        readonly Vector4[] hits = new Vector4[4];           // (world position, age) for the shader
        readonly Part[] hitPart = new Part[4];               // where each hit landed, on the part (it moves with the craft)
        readonly Vector3[] hitLocal = new Vector3[4];
        int nextHit;

        /// <summary>Leaving the scene: forget every craft (dead references otherwise pile up).</summary>
        internal static void ClearAll() { fields.Clear(); pierced.Clear(); }
        Color colour;

        static readonly Dictionary<Vessel, ModuleFFWForceField> fields = new Dictionary<Vessel, ModuleFFWForceField>();
        static readonly Dictionary<Vessel, float> pierced = new Dictionary<Vessel, float>();
        static int bypass;
        static readonly int WaveId = Shader.PropertyToID("_Wave"), ColourId = Shader.PropertyToID("_Color"), StateId = Shader.PropertyToID("_ShieldState");
        static readonly int[] HitIds = { Shader.PropertyToID("_Hit0"), Shader.PropertyToID("_Hit1"), Shader.PropertyToID("_Hit2"), Shader.PropertyToID("_Hit3") };

        public override void OnStart(StartState state)
        {
            colour = Core.Rgba.Parse(color, new Core.Rgba(0.3f, 0.65f, 1, 1)).ToColor();
            if (!HighLogic.LoadedSceneIsFlight) return;
            InstallPatches();
            FfwShaders.Load();
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
            bool want = fieldOn;
            if (want)
            {
                double need = idleEc * TimeWarp.fixedDeltaTime;
                if (part.RequestResource("ElectricCharge", need, ResourceFlowMode.ALL_VESSEL) < need * 0.99) want = false;
            }
            if (want != up)
            {
                up = want;
                upSince = Time.time;
                if (!HarmonyBridge.Present && up) ScreenMessages.PostScreenMessage("Force field: Harmony isn't installed, so it can't block damage.", 5, ScreenMessageStyle.UPPER_CENTER);
            }
            if (up) fields[vessel] = this;
            else if (fields.TryGetValue(vessel, out var f) && f == this) fields.Remove(vessel);
            status = !fieldOn ? "off" : up ? "up" : "NO POWER";
        }

        void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
            // (one layer per craft: the generator whose field counts draws it)
            bool mine = !fields.TryGetValue(vessel, out var owner) || owner == this;
            bool show = mine && (up || (Time.time - upSince < 0.6f && overlays.Count > 0));
            if (!show || FfwShaders.Shield == null) { ClearOverlays(); return; }
            if (overlays.Count == 0) for (int i = 0; i < 4; i++) hits[i] = new Vector4(0, 0, 0, -1);
            overlays.Sync(vessel, FfwShaders.Shield, "FFW force field");
            float t = Time.time - upSince;
            float size = Mathf.Max(5, vessel.vesselSize.magnitude);
            // Coming up: a shimmer sweeping out over the hull, then the steady layer; going down: fading out.
            Vector3 c = vessel.CoM;
            float level = up ? Mathf.Clamp01(t / 0.6f) : 1 - Mathf.Clamp01(t / 0.6f);
            for (int i = 0; i < 4; i++)
            {
                if (hits[i].w < 0) continue;
                hits[i].w += Time.deltaTime;
                if (hitPart[i] != null) { Vector3 w = hitPart[i].transform.TransformPoint(hitLocal[i]); hits[i].x = w.x; hits[i].y = w.y; hits[i].z = w.z; }
            }
            foreach (PartOverlay o in overlays.Overlays)
            {
                o.Block.SetVector(WaveId, new Vector4(c.x, c.y, c.z, up ? size * 0.7f * t / 0.6f : -1));
                o.Block.SetVector(StateId, new Vector4(level, Time.time, 0, 0));
                o.Block.SetColor(ColourId, colour);
                for (int i = 0; i < 4; i++) o.Block.SetVector(HitIds[i], hits[i]);
                o.Apply();
            }
        }

        void ClearOverlays()
        {
            overlays.Clear();
        }

        void OnDestroy()
        {
            ClearOverlays();
            if (vessel != null && fields.TryGetValue(vessel, out var f) && f == this) fields.Remove(vessel);
        }

        // ---- Blocking ----

        /// <summary>Run damage that the field must not stop (tachyon weapons).</summary>
        public static void Unshielded(Action a)
        {
            bypass++;
            try { a(); } finally { bypass--; }
        }

        /// <summary>The Phase-Shift Missile is inside: the field doesn't stop its blast for a moment.</summary>
        public static void Pierce(Vessel v, float seconds) { if (v != null) pierced[v] = Time.time + seconds; }

        static ModuleFFWForceField FieldOn(Part p)
        {
            if (bypass > 0 || p == null || p.vessel == null) return null;
            if (pierced.TryGetValue(p.vessel, out float until) && Time.time < until) return null;
            return fields.TryGetValue(p.vessel, out var f) && f != null && f.up ? f : null;
        }

        bool Pay(double ec, Part at)
        {
            if (ec <= 0) return true;
            double got = part.RequestResource("ElectricCharge", ec, ResourceFlowMode.ALL_VESSEL);
            if (got < ec * 0.99)
            {
                up = false; upSince = Time.time;   // the field collapses
                ScreenMessages.PostScreenMessage($"{vessel.vesselName}: force field collapsed (out of power).", 4, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }
            return true;
        }

        void Flash(Part p)
        {
            Vector3 at = p.transform.position;
            hits[nextHit] = new Vector4(at.x, at.y, at.z, 0);
            hitPart[nextHit] = p; hitLocal[nextHit] = p.transform.InverseTransformPoint(at);
            nextHit = (nextHit + 1) % 4;
            FieldManager.Instance?.SmallFlash(at, colour, 4, 0.2f);
        }

        // Harmony prefixes (names match BDArmory's parameters).
        static bool AddDamagePrefix(Component __instance, float partdamage)
        {
            Part p = (__instance as PartModule)?.part;
            ModuleFFWForceField f = FieldOn(p);
            if (f == null || partdamage <= 0) return true;
            if (!f.Pay(partdamage * f.ecPerDamage, p)) return true;
            f.Flash(p);
            return false;
        }

        static bool ReduceArmorPrefix(Component __instance, float massToReduce)
        {
            Part p = (__instance as PartModule)?.part;
            ModuleFFWForceField f = FieldOn(p);
            if (f == null || massToReduce <= 0) return true;
            return !f.Pay(massToReduce * f.ecPerArmor, p);
        }

        static bool KerbalDamagePrefix(KerbalEVA kerbal, float damage)
        {
            ModuleFFWForceField f = FieldOn(kerbal != null ? kerbal.part : null);
            if (f == null || damage <= 0) return true;
            if (!f.Pay(damage * f.ecPerDamage, kerbal.part)) return true;
            f.Flash(kerbal.part);
            return false;
        }

        static bool patched;
        internal static void InstallPatches()
        {
            if (patched) return;
            patched = true;
            Bda.Init();
            Type hp = Bda.FindType("BDArmory.Damage.HitpointTracker");
            if (hp == null) { Log.Warn("BDArmory's HitpointTracker not found: the force field can't block damage."); return; }
            const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic;
            HarmonyBridge.Prefix(hp.GetMethod("AddDamage", new[] { typeof(float), typeof(bool) }), typeof(ModuleFFWForceField).GetMethod(nameof(AddDamagePrefix), S));
            HarmonyBridge.Prefix(hp.GetMethod("ReduceArmor", new[] { typeof(float) }), typeof(ModuleFFWForceField).GetMethod(nameof(ReduceArmorPrefix), S));
            HarmonyBridge.Prefix(hp.GetMethod("AddDamageToKerbal", new[] { typeof(KerbalEVA), typeof(float) }), typeof(ModuleFFWForceField).GetMethod(nameof(KerbalDamagePrefix), S));
        }
    }
}
