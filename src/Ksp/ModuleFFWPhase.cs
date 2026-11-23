using System.Collections.Generic;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// The Quantum Phase-Shift Missile: <see cref="teleportRange"/> from its target (ModuleFFWSeeker's) it goes out of
    /// phase and vanishes, reappears inside the target's hull (its colliders ignore the target's, so it doesn't crash on
    /// the way in) and detonates there on the next physics step. Each end gets a flash of bent space with the colours
    /// torn apart (the phase lens).
    /// </summary>
    public class ModuleFFWPhase : PartModule
    {
        [KSPField] public float teleportRange = 400;   // m from the target where it phases out
        [KSPField] public string flashColor = "90, 230, 255, 255";

        PartModule missile;
        ModuleFFWSeeker seeker;
        bool jumped, detonated;
        Vessel target;

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            Vfxr.Init();
            missile = Bda.FindMissile(part);
            seeker = part.FindModuleImplementing<ModuleFFWSeeker>();
        }

        public override string GetInfo() => $"<b>Phase shift:</b> {teleportRange:0} m from its target it vanishes, reappears inside the hull and goes off there.";

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || missile == null || seeker == null || detonated || !Bda.MissileFired(missile)) return;
            if (jumped)
            {
                detonated = true;
                Log.Info($"{part.partInfo.title}: back in phase inside {(target != null ? target.vesselName : "its target")}, detonating.");
                Bda.DetonateMissile(missile);
                return;
            }
            Vessel t = seeker.Target;
            if (t == null || !t.loaded || !seeker.Tracking) return;
            Vector3 me = part.transform.position;
            if ((me - t.CoM).sqrMagnitude > teleportRange * teleportRange) return;
            // Out of phase: no collisions with the target, then across to its middle.
            target = t;
            var mine = part.GetComponentsInChildren<Collider>();
            foreach (Part p in t.parts)
                foreach (Collider c in p.GetComponentsInChildren<Collider>())
                    foreach (Collider m in mine)
                        if (c != null && m != null) Physics.IgnoreCollision(m, c, true);
            Color colour = Core.Rgba.Parse(flashColor, new Core.Rgba(0.35f, 0.9f, 1, 1)).ToColor();
            FieldManager.Instance?.PhaseFlash(me, colour);
            Vector3 inside = t.CoM;
            part.FindModuleImplementing<ModuleFFWTrail>()?.Restart();
            vessel.SetPosition(inside, false);
            FieldManager.Instance?.PhaseFlash(inside, colour);
            jumped = true;
            Log.Info($"{part.partInfo.title}: phased out {Vector3.Distance(me, inside):0} m from {t.vesselName}.");
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
