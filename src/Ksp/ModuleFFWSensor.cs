using System.Collections.Generic;
using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// A tracking sensor (optical camera, CommNet signal tracker, consciousness field sensor). An FFW-guided missile can
    /// only lock on if the craft firing it carries a working sensor of the same kind (ModuleFFWSeeker). The sensor follows
    /// the craft's target (its KSP target, or the weapon manager's) and shows what it makes of it in its part menu; a
    /// camera turns its head (<see cref="pivotTransform"/>) to follow it, a dish (<see cref="spinTransform"/>) keeps
    /// turning while it runs.
    /// </summary>
    public class ModuleFFWSensor : PartModule
    {
        [KSPField] public string tracker = "optical";
        [KSPField] public float ecPerSecond = 0.5f;
        [KSPField] public string pivotTransform = "";
        [KSPField] public float gimbalLimit = 180;       // degrees either side of the pivot's resting direction
        [KSPField] public float traverseRate = 180;      // degrees per second
        [KSPField] public string spinTransform = "";
        [KSPField] public float spinRate = 60;           // degrees per second, round the transform's own up axis

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = "Sensor")] public string status = "";

        TrackerKind kind;
        bool powered = true;
        Transform pivot, spin;
        Quaternion pivotRest;
        Vessel target;
        bool seeing;
        float nextLook;

        static readonly List<ModuleFFWSensor> all = new List<ModuleFFWSensor>();

        /// <summary>Whether the craft carries a working (powered) sensor of this kind.</summary>
        public static bool Has(Vessel v, TrackerKind k)
        {
            if (v == null) return false;
            foreach (ModuleFFWSensor s in all)
                if (s != null && s.vessel == v && s.kind == k && s.powered && s.part.State != PartStates.DEAD) return true;
            return false;
        }

        public override void OnStart(StartState state)
        {
            kind = Core.Tracking.Parse(tracker);
            status = Core.Tracking.Name(kind) + ", standing by";
            if (!string.IsNullOrEmpty(pivotTransform)) pivot = part.FindModelTransform(pivotTransform);
            if (pivot != null) pivotRest = pivot.localRotation;
            if (!string.IsNullOrEmpty(spinTransform)) spin = part.FindModelTransform(spinTransform);
            if (HighLogic.LoadedSceneIsFlight) all.Add(this);
        }

        void OnDestroy() => all.Remove(this);

        public override string GetInfo()
        {
            string what;
            switch (Core.Tracking.Parse(tracker))
            {
                case TrackerKind.Optical: what = "Optical tracking: sees the target out to 20 km unless it's cloaked. The camera turns to follow it."; break;
                case TrackerKind.CommNet: what = "CommNet signal tracking: follows the target's radio link, anywhere it's connected."; break;
                case TrackerKind.Consciousness: what = "Consciousness field sensing: finds Kerbal minds within 5 km."; break;
                default: what = ""; break;
            }
            return $"<b>Tracking sensor.</b> {what} FFW missiles with this kind of tracking can only lock on when fired from a craft carrying one. {ecPerSecond:0.##} EC/s.";
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
            if (ecPerSecond > 0)
            {
                double need = ecPerSecond * TimeWarp.fixedDeltaTime;
                powered = part.RequestResource("ElectricCharge", need, ResourceFlowMode.ALL_VESSEL) >= need * 0.99;
            }
            string name = Core.Tracking.Name(kind);
            if (!powered) { status = name + ": NO POWER"; target = null; seeing = false; return; }
            if (Time.time < nextLook) return;       // (5 times a second is plenty for the display and the camera head)
            nextLook = Time.time + 0.2f;
            target = Targets.Choose(vessel);
            seeing = target != null && Targets.CanSee(kind, part.transform.position, target);
            if (target == null) status = name + ": no target";
            else if (!seeing) status = $"{name}: can't see {target.vesselName}";
            else status = $"{name}: tracking {target.vesselName} ({Vector3.Distance(part.transform.position, target.CoM) / 1000:0.0} km)";
        }

        void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;
            if (spin != null && powered) spin.Rotate(Vector3.up, spinRate * Time.deltaTime, Space.Self);
            if (pivot == null) return;
            // The head turns round its mount's up axis (like BDArmory's targeting ball), to face the target.
            Transform mount = pivot.parent != null ? pivot.parent : part.transform;
            Quaternion rest = mount.rotation * pivotRest;
            Quaternion want = rest;
            if (seeing && target != null)
            {
                Vector3 flat = Vector3.ProjectOnPlane(target.CoM - pivot.position, mount.up);
                if (flat.sqrMagnitude > 1 && Vector3.Angle(rest * Vector3.forward, flat) <= gimbalLimit)
                    want = Quaternion.LookRotation(flat, mount.up);
            }
            pivot.rotation = Quaternion.RotateTowards(pivot.rotation, want, traverseRate * Time.deltaTime);
        }
    }
}
