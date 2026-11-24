using System.Collections.Generic;
using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// The Kessler Swarm Pod: a BDArmory radar-guided missile that, within <see cref="splitDistance"/> of its target,
    /// splits with a small pop into a cone of unguided tungsten darts at hypervelocity, aimed where the target will be when
    /// they get there. Each dart does little on its own, goes through up to <see cref="penetrations"/> parts (a third less
    /// damage each time), and shreds what's fragile (solar panels, radiators, wings and control surfaces, exposed engines).
    /// </summary>
    public class ModuleFFWDarts : PartModule
    {
        [KSPField] public float splitDistance = 1500;
        [KSPField] public int darts = 100;
        [KSPField] public float dartSpeed = 1800;     // m/s added to the missile's own speed
        [KSPField] public float spread = 2.5f;        // cone half-angle (degrees)
        [KSPField] public float damage = 80;          // per dart
        [KSPField] public float fragileMultiplier = 5;
        [KSPField] public int penetrations = 3;       // parts each dart goes through
        [KSPField] public string trailColor = "255, 150, 50, 255";

        PartModule missile;
        bool split;
        float lastRange = float.MaxValue;
        [KSPField] public float closestApproachRange = 300;   // (passing closest approach within this: split there)

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            Bda.Init();
            missile = Bda.FindMissile(part);
        }

        public override string GetInfo() =>
            $"<b>Swarm:</b> splits {splitDistance:0} m from its target into {darts} unguided tungsten darts ({damage:0} damage each, x{fragileMultiplier:0} against solar panels, radiators, wings and exposed engines).";

        public void FixedUpdate()
        {
            if (split || !HighLogic.LoadedSceneIsFlight || missile == null || !Bda.MissileFired(missile)) return;
            Vector3 target = Bda.MissileTargetPosition(missile);
            if (target == Vector3.zero) return;
            Vector3 me = part.transform.position;
            float range = (target - me).magnitude;
            // Split at the set distance; or, if it's going to pass wide of that, at its closest approach.
            bool passing = range > lastRange && range < closestApproachRange;
            lastRange = range;
            if (range > splitDistance && !passing) return;
            split = true;
            // Where the target will be when the darts arrive (in the missile's own frame: the darts leave it at dartSpeed).
            Vector3 tv = Bda.MissileTargetVelocity(missile), mv = vessel.srf_velocity;
            Vector3 rel = target - me;
            double speed = dartSpeed;
            Core.Tracking.Lead(rel.x, rel.y, rel.z, tv.x - mv.x, tv.y - mv.y, tv.z - mv.z, speed, 5, out double ax, out double ay, out double az);
            Vector3 aim = new Vector3((float)ax, (float)ay, (float)az).normalized;
            Color c = Rgba.Parse(trailColor, new Rgba(1, 0.6f, 0.2f, 1)).ToColor();
            DartManager.Instance?.Launch(me, vessel.srf_velocity, aim, darts, dartSpeed, spread, damage, fragileMultiplier, penetrations, c, vessel);
            Log.Info($"{part.partInfo.title}: split into {darts} darts {Vector3.Distance(target, me):0} m from its target.");
            Bda.DetonateMissile(missile);   // (its small charge: the pop)
        }
    }

    /// <summary>Flies the darts: straight lines under gravity, raycast each step, BDArmory damage on what they hit.</summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class DartManager : MonoBehaviour
    {
        public static DartManager Instance { get; private set; }

        sealed class Dart
        {
            public Vector3d Pos;       // main body frame
            public Vector3d Vel;       // main body frame (over the surface)
            public float Born, Damage, Fragile;
            public int Pens;
            public readonly HashSet<Part> Hit = new HashSet<Part>();
            public RibbonTrail Trail;
            public Vessel Source;
        }

        const float Life = 3;
        const int MaxTrails = 100;      // (darts are cheap, trails aren't: one object, mesh and draw call each)
        readonly List<Dart> darts = new List<Dart>();
        int mask;

        void Awake() { Instance = this; }
        void Start() { mask = LayerMask.GetMask("Default", "Local Scenery", "TerrainColliders", "EVA", "WheelCollidersIgnore", "WheelColliders"); }

        internal void Launch(Vector3 from, Vector3 surfaceVelocity, Vector3 aim, int n, float speed, float spreadDeg, float damage, float fragile, int pens, Color colour, Vessel source)
        {
            double[] dirs = Darts.Cone(n, spreadDeg);
            Quaternion rot = Quaternion.LookRotation(aim);
            Transform frame = FlightGlobals.currentMainBody != null ? FlightGlobals.currentMainBody.transform : null;
            int every = Mathf.Max(1, Mathf.CeilToInt(n / (float)MaxTrails));
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = rot * new Vector3((float)dirs[i * 3], (float)dirs[i * 3 + 1], (float)dirs[i * 3 + 2]);
                Vector3 v = surfaceVelocity + dir * speed;
                var d = new Dart
                {
                    Pos = Visuals.ToFrame(from + dir * 2),
                    Vel = frame != null ? (Vector3d)frame.InverseTransformDirection(v) : (Vector3d)v,
                    Born = Time.time, Damage = damage, Fragile = fragile, Source = source, Pens = Mathf.Max(1, pens),
                    Trail = i % every == 0 ? RibbonTrail.Create(null, colour, 0.2f, 0.3f, 8) : null
                };
                d.Trail?.Add(from);
                darts.Add(d);
            }
            FieldManager.Instance?.SmallFlash(from, colour, 6, 0.25f);
        }

        void FixedUpdate()
        {
            if (darts.Count == 0) return;
            float dt = Time.fixedDeltaTime, now = Time.time;
            Transform frame = FlightGlobals.currentMainBody != null ? FlightGlobals.currentMainBody.transform : null;
            for (int i = darts.Count - 1; i >= 0; i--)
            {
                Dart d = darts[i];
                Vector3 a = Visuals.FromFrame(d.Pos);
                Vector3 g = FlightGlobals.getGeeForceAtPosition(a);
                Vector3d gFrame = frame != null ? (Vector3d)frame.InverseTransformDirection(g) : (Vector3d)g;
                d.Vel += gFrame * dt;
                Vector3d next = d.Pos + d.Vel * dt;
                Vector3 b = Visuals.FromFrame(next);
                bool done = now - d.Born > Life;
                // Through what it hits: each part once, up to its penetrations, a third weaker each time; the ground stops it.
                Vector3 seg = b - a;
                float left = seg.magnitude;
                Vector3 from = a, dir = left > 1e-4f ? seg / left : Vector3.forward;
                while (!done && left > 0 && Physics.Raycast(from, dir, out RaycastHit hit, left, mask, QueryTriggerInteraction.Ignore))
                {
                    Part p = hit.collider.GetComponentInParent<Part>();
                    float step = hit.distance + 0.05f;
                    from += dir * step; left -= step;
                    if (p != null && (p.vessel == d.Source || d.Hit.Contains(p))) continue;
                    if (p == null) { b = hit.point; done = true; FieldManager.Instance?.SmallFlash(hit.point, new Color(1, 0.7f, 0.35f), 2f, 0.12f); break; }
                    d.Hit.Add(p);
                    Bda.AddDamage(p, (float)Darts.Damage(d.Damage, Fragile(p), d.Fragile));
                    if (d.Hit.Count == 1) FieldManager.Instance?.SmallFlash(hit.point, new Color(1, 0.7f, 0.35f), 2f, 0.12f);
                    d.Damage *= 0.67f;
                    if (--d.Pens <= 0) { b = hit.point; done = true; }
                }
                d.Pos = Visuals.ToFrame(b);
                d.Trail?.Add(b);
                if (done) { d.Trail?.Detach(); darts.RemoveAt(i); }
            }
        }

        /// <summary>What darts shred: solar panels, radiators, wings and control surfaces, engines.</summary>
        static bool Fragile(Part p)
        {
            foreach (PartModule m in p.Modules)
                if (m is ModuleDeployableSolarPanel || m is ModuleDeployableRadiator || m is ModuleActiveRadiator ||
                    m is ModuleLiftingSurface || m is ModuleEngines) return true;
            return false;
        }

        void OnDestroy()
        {
            foreach (Dart d in darts) if (d.Trail != null) d.Trail.Detach();
            darts.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
