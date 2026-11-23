using FarFutureWeaponry.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FarFutureWeaponry
{
    /// <summary>
    /// Guidance for a BDArmory missile by one of FFW's trackers (optical, CommNet, consciousness). The missile is set up
    /// with targetingType = none (BDArmory then leaves its target point alone) and a homing type that flies to that point;
    /// this module picks the target at launch (the firing craft's KSP target, or its weapon manager's in guard mode) and
    /// every physics step, while the tracker can see it, points the missile at where it will meet it. Flares, chaff and
    /// radar jamming do nothing to it. When the tracker loses the target the missile flies on to where it last saw it.
    /// </summary>
    public class ModuleFFWSeeker : PartModule
    {
        [KSPField] public string tracker = "optical";
        [KSPField] public float leadSeconds = 30;     // the furthest ahead it aims

        [KSPField(guiActive = true, guiName = "Seeker")] public string status = "";

        PartModule missile;
        TrackerKind kind;
        Vector3d lastSeen;            // where it last saw the target (main body frame)
        bool picked, seen, decoyRolled;
        float pickedAt;
        int decoy = -1;                 // the hologram it's fooled into following (optical), or -1
        [KSPField] public float decoyChance = 0.85f;
        public Vessel Target { get; private set; }
        public bool Tracking { get; private set; }

        public override void OnStart(StartState state)
        {
            kind = Core.Tracking.Parse(tracker);
            status = Core.Tracking.Name(kind) + ", ready";
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            missile = Bda.FindMissile(part);
            if (missile == null) Log.Warn($"{part.partInfo.title}: ModuleFFWSeeker needs a BDArmory missile on the part.");
        }

        public override string GetInfo()
        {
            switch (Core.Tracking.Parse(tracker))
            {
                case TrackerKind.Optical: return "<b>Optical tracking:</b> follows the target it was fired at (your KSP target, or the weapon manager's) out to 20 km. Ignores flares, chaff and jamming; fooled by cloaks.";
                case TrackerKind.CommNet: return "<b>CommNet tracking:</b> rides the target's own radio link. Lost if the target drops off the CommNet.";
                case TrackerKind.Consciousness: return "<b>Consciousness field detection:</b> senses Kerbal minds within 5 km. Uncrewed craft can't be found.";
            }
            return "";
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || missile == null || kind == TrackerKind.None) return;
            if (!Bda.MissileFired(missile)) return;
            if (!picked)
            {
                picked = true;
                Target = Targets.Choose(Bda.MissileSource(missile));
                pickedAt = Time.time;
                Log.Info($"{part.partInfo.title}: {Core.Tracking.Name(kind)} seeker on {(Target != null ? Target.vesselName : "nothing (no target set)")}.");
            }
            Vector3 me = part.transform.position;
            // The target jumped away (micro-hop drive): every lock on it is broken.
            if (Target != null && Targets.LockBrokenAt(Target) > pickedAt)
            {
                Log.Info($"{part.partInfo.title}: lost {Target.vesselName} (its locks were broken).");
                Target = null;
            }
            Tracking = Target != null && Targets.CanSee(kind, me, Target);
            // Holograms: optical tracking is likely to take one for the real craft (decided once, when it first sees them).
            Vector3 aimAt = Tracking ? Target.CoM : Vector3.zero;
            if (Tracking && kind == TrackerKind.Optical)
            {
                Vector3? h0 = Targets.Hologram(Target, 0);
                if (h0 != null && !decoyRolled)
                {
                    decoyRolled = true;
                    if (Random.value < decoyChance) decoy = Random.Range(0, 6);
                    if (decoy >= 0) Log.Info($"{part.partInfo.title}: fooled by a hologram of {Target.vesselName}.");
                }
                Vector3? h = decoy >= 0 ? Targets.Hologram(Target, decoy) : null;
                if (h != null) aimAt = h.Value;
            }
            if (Tracking)
            {
                // Aim where it will be: relative motion over the surface, at the missile's own speed (at least 200 m/s).
                Vector3 rel = aimAt - me;
                Vector3 tv = Target.srf_velocity, mv = vessel.srf_velocity;
                double speed = Mathf.Max(200, mv.magnitude);
                Core.Tracking.Lead(rel.x, rel.y, rel.z, tv.x, tv.y, tv.z, speed, leadSeconds, out double ax, out double ay, out double az);
                Vector3 aim = me + new Vector3((float)ax, (float)ay, (float)az);
                lastSeen = Visuals.ToFrame(aim);
                seen = true;
                Bda.SteerMissile(missile, aim, Vector3.zero, true);
                status = Core.Tracking.Name(kind) + ": tracking " + Target.vesselName;
            }
            else if (seen)
            {
                Bda.SteerMissile(missile, Visuals.FromFrame(lastSeen), Vector3.zero, true);
                status = Core.Tracking.Name(kind) + ": lost, last known position";
            }
            else status = Core.Tracking.Name(kind) + ": no target";
        }
    }
}
