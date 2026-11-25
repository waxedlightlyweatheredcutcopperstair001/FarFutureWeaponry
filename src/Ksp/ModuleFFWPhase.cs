using System.Collections.Generic;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// The Quantum Phase-Shift Missile: <see cref="teleportRange"/> from its target (ModuleFFWSeeker's) it goes out of
    /// phase and vanishes (hidden, its collisions off, held where it was), stays gone for <see cref="phaseDelay"/> seconds,
    /// then reappears inside the target's hull and detonates there on the next physics step. Each end gets a flash of
    /// bent space with the colours torn apart (the phase lens).
    /// </summary>
    public class ModuleFFWPhase : PartModule
    {
        [KSPField] public float teleportRange = 100;   // m from the target where it phases out
        [KSPField] public float phaseDelay = 0.5f;     // s out of phase
        [KSPField] public string flashColor = "90, 230, 255, 255";

        PartModule missile;
        ModuleFFWSeeker seeker;
        enum Phase { Closing, Gone, Back, Done }
        Phase phase;
        float goneAt;
        Vessel target;
        Color colour;

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            Vfxr.Init();
            missile = Bda.FindMissile(part);
            seeker = part.FindModuleImplementing<ModuleFFWSeeker>();
            colour = Core.Rgba.Parse(flashColor, new Core.Rgba(0.35f, 0.9f, 1, 1)).ToColor();
        }

        public override string GetInfo() =>
            $"<b>Phase shift:</b> {teleportRange:0} m from its target it vanishes, and {phaseDelay:0.#} s later reappears inside the hull and goes off there.";

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || missile == null || seeker == null || phase == Phase.Done || !Bda.MissileFired(missile)) return;
            switch (phase)
            {
                case Phase.Closing:
                {
                    Vessel t = seeker.Target;
                    if (t == null || !t.loaded || !seeker.Tracking) return;
                    Vector3 me = part.transform.position;
                    if ((me - t.CoM).sqrMagnitude > teleportRange * teleportRange) return;
                    target = t;
                    FieldManager.Instance?.PhaseFlash(me, colour);
                    Vanish();
                    goneAt = Time.time;
                    phase = Phase.Gone;
                    Log.Info($"{part.partInfo.title}: phased out {Vector3.Distance(me, t.CoM):0} m from {t.vesselName}.");
                    return;
                }
                case Phase.Gone:
                {
                    if (Time.time - goneAt < phaseDelay) return;
                    if (target == null || !target.loaded) { phase = Phase.Done; Bda.DetonateMissile(missile); return; }
                    // Back in phase in the middle of the target (its collisions with the target ignored).
                    var mine = part.GetComponentsInChildren<Collider>(true);
                    foreach (Part p in target.parts)
                        foreach (Collider c in p.GetComponentsInChildren<Collider>())
                            foreach (Collider m in mine)
                                if (c != null && m != null) Physics.IgnoreCollision(m, c, true);
                    Vector3 inside = target.CoM;
                    vessel.SetPosition(inside, false);
                    ModuleFFWForceField.Pierce(target, 3);   // (it passes through force fields too)
                    FieldManager.Instance?.PhaseFlash(inside, colour);
                    phase = Phase.Back;
                    return;
                }
                case Phase.Back:
                    phase = Phase.Done;
                    Log.Info($"{part.partInfo.title}: back in phase inside {(target != null ? target.vesselName : "its target")}, detonating.");
                    Bda.DetonateMissile(missile);
                    return;
            }
        }

        /// <summary>Out of phase: unseen, unheard, touching nothing, held still (it stays clear of BDArmory's miss logic: its target stays ahead).</summary>
        void Vanish()
        {
            part.FindModuleImplementing<ModuleFFWTrail>()?.Hide();
            bool Mine(Component c) => !Visuals.OnCamera(c) && c.GetComponentInParent<Part>() == part;
            foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true)) if (Mine(r)) r.enabled = false;
            foreach (Collider c in part.GetComponentsInChildren<Collider>(true)) if (Mine(c)) c.enabled = false;
            foreach (AudioSource a in part.GetComponentsInChildren<AudioSource>(true)) if (Mine(a)) a.mute = true;
            if (part.rb != null) { part.rb.velocity = Vector3.zero; part.rb.angularVelocity = Vector3.zero; part.rb.isKinematic = true; }
        }
    }

    /// <summary>
    /// A nuclear ramjet for a BDArmory cruise missile: full thrust in thick air, less as the air thins, none above
    /// the atmosphere (it needs air to heat). Scales the missile's thrust each step by the air density.
    /// </summary>
    public class ModuleFFWRamjet : PartModule
    {
        [KSPField] public float fullDensity = 0.6f;    // kg/m³ for full thrust
        [KSPField] public float minDensity = 0.02f;    // below this, no thrust

        // Climb-out: the boosters pitch it up off the launcher (BDArmory steers it by its fins, which needs airspeed, and
        // its cruise guidance starts by levelling off: launched from the ground it ploughed into it). Fired below
        // climbBelow metres above the ground, it's turned up to climbAngle degrees along its heading (at up to climbRate
        // degrees a second) and held there for the rest of the boost; the part's guidanceDelay keeps BDArmory's guidance
        // out of it meanwhile.
        [KSPField] public float climbAngle = 35;
        [KSPField] public float climbBelow = 1500;
        [KSPField] public float climbRate = 60;

        // A cruise missile flies on to its GPS point (or until it hits something): BDArmory's "miss" check, which makes a
        // missile pointing away from its target, or without one, blow itself up 1.5 s later, is skipped for it.
        [KSPField] public bool neverMiss = true;

        // Roll hold: BDArmory steers its missiles in pitch and yaw only, so a big turn can leave it flying upside down.
        // In cruise (after the boost), it's rolled back towards fin-up, at up to rollRate degrees a second.
        [KSPField] public bool rollHold = false;
        [KSPField] public float rollRate = 60;

        PartModule missile;
        System.Reflection.FieldInfo cruiseField;
        float baseCruise, dropTime, boostTime, firedAt = -1;

        static readonly System.Collections.Generic.HashSet<PartModule> noMiss = new System.Collections.Generic.HashSet<PartModule>();

        static bool CheckMissPrefix(PartModule __instance) => !noMiss.Contains(__instance);

        static bool missPatched;
        static void InstallMissPatch()
        {
            if (missPatched) return;
            missPatched = true;
            var t = Bda.FindType("BDArmory.Weapons.Missiles.MissileLauncher");
            var m = t?.GetMethod("CheckMiss", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (m == null || !HarmonyBridge.Prefix(m, typeof(ModuleFFWRamjet).GetMethod(nameof(CheckMissPrefix), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)))
                Log.Warn("Couldn't patch BDArmory's MissileLauncher.CheckMiss: cruise missiles may still self-destruct as a \"miss\".");
        }

        void OnDestroy() { if (missile != null) noMiss.Remove(missile); }

        [KSPField(guiActive = true, guiName = "Ramjet")] public string status = "";

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            missile = Bda.FindMissile(part);
            if (missile == null) return;
            // (only the cruise thrust is the ramjet's: the boost is solid rockets, the same in any air)
            cruiseField = missile.GetType().GetField("cruiseThrust");
            if (cruiseField != null) baseCruise = (float)cruiseField.GetValue(missile);
            if (neverMiss) { InstallMissPatch(); noMiss.Add(missile); }
            dropTime = missile.GetType().GetField("dropTime")?.GetValue(missile) is float d ? d : 0;
            boostTime = missile.GetType().GetField("boostTime")?.GetValue(missile) is float bt ? bt : 0;
        }

        public override string GetInfo() => "<b>Nuclear ramjet:</b> needs air. Full thrust low down, weaker as the air thins, none above the atmosphere.";

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || missile == null || vessel == null) return;
            double rho = vessel.atmDensity;
            float k = rho <= minDensity ? 0 : Mathf.Clamp01((float)((rho - minDensity) / (fullDensity - minDensity)));
            cruiseField?.SetValue(missile, baseCruise * k);
            status = k <= 0 ? "no air: flamed out" : $"{k * 100:0}% thrust";
            ClimbOut();
            RollHold();
        }

        void RollHold()
        {
            if (!rollHold || part.rb == null || firedAt < 0) return;
            if (Time.time - firedAt < dropTime + boostTime) return;
            Vector3 fwd = part.transform.forward;
            Vector3 up = (vessel.CoM - vessel.mainBody.position).normalized;
            if (Mathf.Abs(Vector3.Dot(fwd, up)) > 0.95f) return;           // (pointing nearly straight up or down: no "up" to roll to)
            Vector3 want = Vector3.ProjectOnPlane(up, fwd).normalized;     // where the fin should point
            Vector3 have = Vector3.ProjectOnPlane(part.transform.up, fwd).normalized;
            float error = Vector3.SignedAngle(have, want, fwd);             // (degrees, about the nose)
            float rate = Mathf.Clamp(error * 2, -rollRate, rollRate) * Mathf.Deg2Rad;
            Vector3 w = part.rb.angularVelocity;
            part.rb.angularVelocity = w - fwd * Vector3.Dot(w, fwd) + fwd * rate;   // (only the roll part changes)
        }

        bool climbing;      // (launched low: it keeps its climb for the rest of the boost)

        void ClimbOut()
        {
            if (climbAngle <= 0 || part.rb == null) return;
            if (firedAt < 0) { if (Bda.MissileFired(missile)) firedAt = Time.time; else return; }
            float t = Time.time - firedAt;
            if (t < dropTime || t > dropTime + boostTime) return;
            if (vessel.heightFromTerrain > climbBelow && vessel.heightFromTerrain >= 0 && !climbing) return;
            climbing = vessel.heightFromTerrain >= 0 && vessel.heightFromTerrain <= climbBelow || climbing;
            Vector3 up = (vessel.CoM - vessel.mainBody.position).normalized;
            Vector3 fwd = part.transform.forward;
            Vector3 heading = Vector3.ProjectOnPlane(fwd, up);
            if (heading.sqrMagnitude < 1e-4f) heading = Vector3.ProjectOnPlane(part.transform.up, up);
            heading.Normalize();
            float a = climbAngle * Mathf.Deg2Rad;
            Vector3 want = heading * Mathf.Cos(a) + up * Mathf.Sin(a);                                  // (heading, tipped up)
            if (Vector3.Dot(fwd, up) > Mathf.Sin(climbAngle * Mathf.Deg2Rad)) return;                  // (climbing steeply enough already)
            Vector3 axis = Vector3.Cross(fwd, want);
            float angle = Vector3.Angle(fwd, want);
            if (axis.sqrMagnitude < 1e-8f || angle < 0.5f) return;
            float rate = Mathf.Min(climbRate, angle * 3) * Mathf.Deg2Rad;
            part.rb.angularVelocity = axis.normalized * rate;
        }
    }
}
