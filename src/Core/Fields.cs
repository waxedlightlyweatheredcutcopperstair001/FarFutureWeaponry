using System;

namespace FarFutureWeaponry.Core
{
    public enum FieldKind { None, Gravity, Warp, Tachyon }

    /// <summary>The settings of a bomb's field (ModuleFFWBomb fields).</summary>
    public sealed class FieldSpec
    {
        public FieldKind Kind;
        public double Radius = 300;        // m: the pull's reach, the lens, the tachyon sphere
        public double InnerRadius;         // m: the warp bomb's event horizon (everything inside is destroyed)
        public double AccelG = 6;          // pull towards the centre, in g (9.81 m/s²), the same at any distance inside
        public double Duration = 3;        // s the field lasts
        public double BlastTntKg;          // kg TNT set off at the centre when the field ends (gravity bomb)

        public static FieldKind Parse(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "gravity": return FieldKind.Gravity;
                case "warp": return FieldKind.Warp;
                case "tachyon": return FieldKind.Tachyon;
                default: return FieldKind.None;
            }
        }
    }

    /// <summary>What a field looks like at one moment (drawn through VFX Reconfigured's EffectsApi and the plugin).</summary>
    public struct FieldLook
    {
        public double LensRadius, LensStrength;        // gravitational lens: reach (m), 0-1
        public double CoreShare;                       // its black core, as a share of the radius at full strength
        public double InRingRadius, InRingAmp;         // a ripple ring travelling inward (air being pulled in), m, 0-1
        public double RingRadius, RingAmp;             // the ripple ring: radius (m), 0-1.2
        public double Flash, FlashRadius;              // a flash: power (1 bright, 3 blinding), radius (m)
        public double SphereRadius, SphereAlpha;       // a sphere (tachyon field; the gravity bomb's pulse): radius (m), opacity 0-1
        public bool Pulling, Destroying, Freezing;     // physics running at this moment
        public bool Over;                              // nothing left to draw or do
    }

    /// <summary>The timelines of the three field bombs. Pure maths: tested without KSP.</summary>
    public static class Fields
    {
        public const double G0 = 9.81;
        public const double FlashTail = 0.6;      // s a closing flash lasts after the field ends
        public const double PulseTime = 0.9;      // s the gravity bomb's closing pulse takes to spread out
        public const double GravityLens = 0.6;    // the gravity bubble's lens strength at most (toned down)
        public const double GravityCore = 0.01;   // its black core: a speck (a full black hole is the warp bomb's)

        static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;
        public static double Smooth(double a, double b, double x) { double t = Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

        /// <summary>When everything about the field is over (s after detonation).</summary>
        public static double End(FieldSpec s) =>
            s.Duration + (s.Kind == FieldKind.Gravity ? PulseTime + 0.2 : FlashTail) + (s.Kind == FieldKind.Tachyon ? 0.5 : 0);   // (the tachyon flash comes as the sphere closes)

        public static FieldLook At(FieldSpec s, double t)
        {
            var l = new FieldLook();
            double D = Math.Max(0.1, s.Duration), R = s.Radius;
            if (t < 0) return l;
            if (t > End(s)) { l.Over = true; return l; }
            bool on = t < D;
            switch (s.Kind)
            {
                case FieldKind.Gravity:
                {
                    // A bubble of bent space (no black hole: only a speck of a core) at full size that shrinks onto its
                    // centre while it pulls, getting stronger, with air rippling inward; then a white flash and a pulse:
                    // a ring of bent air and a pale shell racing outward with the blast.
                    double p = Clamp01(t / D);
                    l.LensRadius = R * (1 - 0.9 * p * p);
                    l.LensStrength = on ? GravityLens * Smooth(0, 0.25, t) * (0.35 + 0.65 * p) : 0;
                    l.CoreShare = GravityCore;
                    if (on) { Ring(ref l, R, t - 0.05, 0.6); InwardRings(ref l, R, t, 0.7, 0.5); }
                    l.Pulling = on;
                    if (!on)
                    {
                        double a = t - D, x = Clamp01(a / PulseTime);
                        l.Flash = 3 * Math.Exp(-a / 0.12); l.FlashRadius = 0.5 * R;
                        l.RingRadius = R * (0.05 + 1.6 * (1 - (1 - x) * (1 - x)));
                        l.RingAmp = 1.2 * Math.Pow(1 - x, 1.2);
                        l.SphereRadius = l.RingRadius * 0.9;
                        l.SphereAlpha = 0.6 * Math.Pow(1 - x, 1.5);
                    }
                    break;
                }
                case FieldKind.Warp:
                {
                    // A blinding flash, then a black hole: the lens forms in a third of a second, holds, and collapses over
                    // the last 0.6 s with a second flash and a ripple.
                    double k = Smooth(0, 0.35, t) * Math.Pow(1 - Smooth(D - 0.6, D, t), 1.5);
                    l.LensRadius = R; l.LensStrength = k; l.CoreShare = 0.13;
                    if (t < D) Ring(ref l, R, t - 0.1, 1.0); else Ring(ref l, R, t - D, 1.2);
                    if (t > 1 && on) InwardRings(ref l, 1.3 * R, t - 1, 1.2, 0.6);
                    l.Flash = 3 * Math.Exp(-t / 0.25) + (t >= D ? 2 * Math.Exp(-(t - D) / 0.2) : 0);
                    l.FlashRadius = t >= D ? 0.6 * R : 0.4 * R;
                    l.Pulling = on; l.Destroying = on;
                    break;
                }
                case FieldKind.Tachyon:
                {
                    // A green flash, a sphere that swells to the full radius in half a second, holds, and falls back in on
                    // itself over the last 0.6 s.
                    double grow = Smooth(0, 0.5, t), shrink = t >= D ? Smooth(D, D + FlashTail, t) : 0;
                    l.SphereRadius = R * grow * (1 - shrink);
                    l.SphereAlpha = (0.55 + 0.45 * Math.Exp(-t / 0.3)) * (1 - shrink);
                    double closed = t - D - FlashTail;
                    l.Flash = 2.5 * Math.Exp(-t / 0.2) + (closed >= 0 ? 1.2 * Math.Exp(-closed / 0.12) : 0);
                    l.FlashRadius = 0.3 * R;
                    l.Freezing = on;
                    break;
                }
            }
            return l;
        }

        /// <summary>
        /// Rings of bent air sweeping inward from beyond the radius to the centre, one every <paramref name="period"/> s:
        /// a shockwave played backwards, the air being drawn in.
        /// </summary>
        static void InwardRings(ref FieldLook l, double from, double t, double period, double amp)
        {
            if (t < 0) return;
            double ph = t / period - Math.Floor(t / period);
            l.InRingRadius = from * (1 - ph * ph);                  // speeds up as it nears the centre
            l.InRingAmp = amp * Math.Sin(Math.PI * Math.Min(1, ph * 1.15)) * Smooth(0, 0.4, t);
        }

        static void Ring(ref FieldLook l, double R, double since, double amp)
        {
            const double life = 1.4;
            if (since < 0 || since > life) return;
            l.RingRadius = R * (0.3 + 2.2 * since / life);
            l.RingAmp = amp * Math.Pow(1 - since / life, 1.5);
        }

        /// <summary>
        /// The pull on a point dx,dy,dz (m) from the centre: AccelG towards it inside the radius, nothing outside, and
        /// nothing in the last metre (no singularity). Returns m/s² along each axis.
        /// </summary>
        public static void Pull(FieldSpec s, double dx, double dy, double dz, out double ax, out double ay, out double az)
        {
            ax = ay = az = 0;
            double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (d <= 1 || d > s.Radius || s.AccelG <= 0) return;
            double a = s.AccelG * G0 / d;
            ax = -dx * a; ay = -dy * a; az = -dz * a;
        }

        public static bool Inside(double radius, double dx, double dy, double dz) => dx * dx + dy * dy + dz * dz <= radius * radius;
    }
}
