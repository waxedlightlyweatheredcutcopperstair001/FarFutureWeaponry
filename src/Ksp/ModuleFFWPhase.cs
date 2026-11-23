using System.Collections.Generic;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// The Quantum Phase-Shift Missile: as it closes on its target (ModuleFFWSeeker's), it goes out of phase. It passes
    /// through the target's hull and armour without touching them (its colliders ignore the target's), then comes back
    /// into phase inside and detonates there: when it's within a part's collider, or once it has passed the target's
    /// centre. A spatial-distortion flash marks the moment it phases through.
    /// </summary>
    public class ModuleFFWPhase : PartModule
    {
        [KSPField] public float phaseRange = 150;     // m from the target where it goes out of phase
        [KSPField] public string flashColor = "90, 230, 255, 255";

        PartModule missile;
        ModuleFFWSeeker seeker;
        Vessel phasedWith;
        bool flashed, detonated;
        float closest = float.MaxValue;
        readonly List<Collider> mine = new List<Collider>();

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            Vfxr.Init();
            missile = Bda.FindMissile(part);
            seeker = part.FindModuleImplementing<ModuleFFWSeeker>();
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || missile == null || seeker == null || detonated || !Bda.MissileFired(missile)) return;
            Vessel t = seeker.Target;
            if (t == null || !t.loaded) return;
            Vector3 me = part.transform.position;
            float d = Vector3.Distance(me, t.CoM);
            if (d < phaseRange && phasedWith != t) PhaseWith(t);
            if (phasedWith == null) return;
            // Back in phase inside the hull: inside one of its parts' colliders, or just past its middle.
            bool inside = false;
            foreach (Part p in t.parts)
            {
                if (p == null) continue;
                foreach (Collider c in p.GetComponentsInChildren<Collider>())
                {
                    if (c == null || !c.enabled || c.isTrigger) continue;
                    if (c is MeshCollider mc && !mc.convex) { if (c.bounds.Contains(me)) { inside = true; break; } continue; }
                    if ((c.ClosestPoint(me) - me).sqrMagnitude < 0.01f) { inside = true; break; }
                }
                if (inside) break;
            }
            if (!flashed && d < t.vesselSize.magnitude * 0.6f + 5) Flash(me);
            if (inside || d > closest + 2 && closest < t.vesselSize.magnitude)
            {
                detonated = true;
                Log.Info($"{part.partInfo.title}: phased into {t.vesselName}, detonating inside.");
                Bda.DetonateMissile(missile);
                return;
            }
            closest = Mathf.Min(closest, d);
        }

        void PhaseWith(Vessel t)
        {
            phasedWith = t;
            mine.Clear();
            mine.AddRange(part.GetComponentsInChildren<Collider>());
            foreach (Part p in t.parts)
                foreach (Collider c in p.GetComponentsInChildren<Collider>())
                    foreach (Collider m in mine)
                        if (c != null && m != null) Physics.IgnoreCollision(m, c, true);
            Log.Info($"{part.partInfo.title}: out of phase with {t.vesselName}.");
        }

        void Flash(Vector3 at)
        {
            flashed = true;
            FieldManager.Instance?.PhaseFlash(at, Core.Rgba.Parse(flashColor, new Core.Rgba(0.35f, 0.9f, 1, 1)).ToColor());
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
