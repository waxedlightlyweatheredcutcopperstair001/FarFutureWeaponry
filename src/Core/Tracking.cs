using System;

namespace FarFutureWeaponry.Core
{
    /// <summary>
    /// FFW's own ways of following a target (missiles with ModuleFFWSeeker):
    /// - Optical: cameras across a wide spectrum that see through most materials (no line of sight needed), out to
    ///   20 km. Fooled only by a cloak, holograms, or distance. Ignores flares and chaff.
    /// - CommNet: follows the target's own radio link. Lost when it has no CommNet connection (or, with CommNet
    ///   off, no working antenna), or when a signal scrambler hides it.
    /// - Consciousness: senses the minds of Kerbals aboard within 5 km. Nothing hides a crewed craft; an uncrewed
    ///   one can't be sensed at all.
    /// </summary>
    public enum TrackerKind { None, Optical, CommNet, Consciousness }

    /// <summary>What a tracker knows about a possible target at one moment.</summary>
    public struct TargetSense
    {
        public double Distance;     // m
        public bool Cloaked;        // an active optical cloak
        public bool Hologram;       // (phase 4) hidden among holograms
        public bool Connected;      // CommNet link (or a working antenna with CommNet off)
        public bool Scrambled;      // (phase 4) hidden by a signal scrambler
        public int Crew;            // Kerbals aboard (EVA counts)
    }

    public static class Tracking
    {
        public const double OpticalRange = 20000, ConsciousnessRange = 5000;

        public static TrackerKind Parse(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "optical": return TrackerKind.Optical;
                case "commnet": return TrackerKind.CommNet;
                case "consciousness": return TrackerKind.Consciousness;
                default: return TrackerKind.None;
            }
        }

        public static string Name(TrackerKind k) =>
            k == TrackerKind.Optical ? "optical" : k == TrackerKind.CommNet ? "CommNet" : k == TrackerKind.Consciousness ? "consciousness field" : "none";

        /// <summary>Whether this tracker can follow the target now.</summary>
        public static bool CanSee(TrackerKind kind, TargetSense t)
        {
            switch (kind)
            {
                case TrackerKind.Optical: return t.Distance <= OpticalRange && !t.Cloaked && !t.Hologram;
                case TrackerKind.CommNet: return t.Connected && !t.Scrambled;
                case TrackerKind.Consciousness: return t.Crew > 0 && t.Distance <= ConsciousnessRange;
                default: return false;
            }
        }

        /// <summary>
        /// Where to aim to meet a target moving at constant velocity (relative positions and velocities, any one frame):
        /// the target's position after the time the missile needs to close the gap at <paramref name="speed"/>. Falls
        /// back on the target's position when it can't catch it.
        /// </summary>
        public static void Lead(double px, double py, double pz, double vx, double vy, double vz, double speed, double maxSeconds,
                                out double ax, out double ay, out double az)
        {
            // |p + v t| = s t  ->  (v.v - s²) t² + 2 (p.v) t + p.p = 0
            double a = vx * vx + vy * vy + vz * vz - speed * speed, b = 2 * (px * vx + py * vy + pz * vz), c = px * px + py * py + pz * pz;
            double t = -1;
            if (Math.Abs(a) < 1e-9) t = b < 0 ? -c / b : -1;
            else
            {
                double disc = b * b - 4 * a * c;
                if (disc >= 0)
                {
                    double r = Math.Sqrt(disc), t1 = (-b - r) / (2 * a), t2 = (-b + r) / (2 * a);
                    t = Math.Min(t1, t2) > 0 ? Math.Min(t1, t2) : Math.Max(t1, t2);
                }
            }
            if (!(t > 0)) t = 0;
            t = Math.Min(t, maxSeconds);
            ax = px + vx * t; ay = py + vy * t; az = pz + vz * t;
        }
    }

    /// <summary>Rust (the Tachyon Gun, the Chronos Rust-Dart): a level 0-1 per part, building up with each hit, saved with the craft.</summary>
    public static class Rust
    {
        /// <summary>A hit adds <paramref name="amount"/> of what's left to rust (it builds up fast, then slows near full).</summary>
        public static double Add(double level, double amount)
        {
            double l = Clamp01(level);
            return Clamp01(l + Clamp01(amount) * (1 - l));
        }

        /// <summary>
        /// The part's colour multiplier at this rust level (rgb 0-1): towards a dark orange-brown, never quite all the way
        /// (the paint shows through in places).
        /// </summary>
        public static void Tint(double level, out double r, out double g, out double b)
        {
            double k = 0.8 * Clamp01(level);
            r = 1 + (0.62 - 1) * k; g = 1 + (0.36 - 1) * k; b = 1 + (0.2 - 1) * k;
        }

        /// <summary>Shininess left at this rust level (rust is matte).</summary>
        public static double Shine(double level) => 1 - 0.9 * Clamp01(level);

        static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;
    }

    /// <summary>The Kessler Swarm Pod's darts: a cone of directions, and how hard each hits what.</summary>
    public static class Darts
    {
        /// <summary>
        /// n unit directions spread evenly inside a cone of half-angle <paramref name="spreadDeg"/> round +z
        /// (a sunflower pattern: no clumps, no gaps), as x,y,z triples.
        /// </summary>
        public static double[] Cone(int n, double spreadDeg)
        {
            var d = new double[n * 3];
            double golden = Math.PI * (3 - Math.Sqrt(5)), max = spreadDeg * Math.PI / 180;
            for (int i = 0; i < n; i++)
            {
                double r = Math.Sqrt((i + 0.5) / n) * max, phi = i * golden;
                d[i * 3] = Math.Sin(r) * Math.Cos(phi);
                d[i * 3 + 1] = Math.Sin(r) * Math.Sin(phi);
                d[i * 3 + 2] = Math.Cos(r);
            }
            return d;
        }

        /// <summary>
        /// A dart's damage to a part: the base, times <paramref name="fragileMult"/> for what darts shred (solar panels,
        /// radiators, wings and control surfaces, exposed engines).
        /// </summary>
        public static double Damage(double baseDamage, bool fragile, double fragileMult) => baseDamage * (fragile ? fragileMult : 1);
    }

    /// <summary>Tachyon slowing: each hit takes speed away (along the way the craft is moving, never past a stop).</summary>
    public static class Slow
    {
        public static double NewSpeed(double speed, double perHit) => Math.Max(0, speed - Math.Max(0, perHit));

        /// <summary>The velocity factor that takes <paramref name="perHit"/> off <paramref name="speed"/> (1 when stopped).</summary>
        public static double Factor(double speed, double perHit) => speed > 1e-6 ? NewSpeed(speed, perHit) / speed : 1;
    }
}
