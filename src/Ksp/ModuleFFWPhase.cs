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
            foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true)) if (!Visuals.OnCamera(r)) r.enabled = false;
            foreach (Collider c in part.GetComponentsInChildren<Collider>(true)) if (!Visuals.OnCamera(c)) c.enabled = false;
            foreach (AudioSource a in part.GetComponentsInChildren<AudioSource>(true)) if (!Visuals.OnCamera(a)) a.mute = true;
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

        PartModule missile;
        System.Reflection.FieldInfo thrustField, cruiseField;
        float baseThrust, baseCruise;

        [KSPField(guiActive = true, guiName = "Ramjet")] public string status = "";

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            missile = Bda.FindMissile(part);
            if (missile == null) return;
            thrustField = missile.GetType().GetField("thrust");
            cruiseField = missile.GetType().GetField("cruiseThrust");
            if (thrustField != null) baseThrust = (float)thrustField.GetValue(missile);
            if (cruiseField != null) baseCruise = (float)cruiseField.GetValue(missile);
        }

        public override string GetInfo() => "<b>Nuclear ramjet:</b> needs air. Full thrust low down, weaker as the air thins, none above the atmosphere.";

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || missile == null || vessel == null) return;
            double rho = vessel.atmDensity;
            float k = rho <= minDensity ? 0 : Mathf.Clamp01((float)((rho - minDensity) / (fullDensity - minDensity)));
            thrustField?.SetValue(missile, baseThrust * k);
            cruiseField?.SetValue(missile, baseCruise * k);
            status = k <= 0 ? "no air: flamed out" : $"{k * 100:0}% thrust";
        }
    }
}
