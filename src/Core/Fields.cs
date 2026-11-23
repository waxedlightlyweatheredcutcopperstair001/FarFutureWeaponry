using System;

namespace FarFutureWeaponry.Core
{
    public enum FieldKind { None, Gravity, Warp, Tachyon, Rinse, Seismic }

    /// <summary>The settings of a bomb's field (ModuleFFWBomb fields).</summary>
    public sealed class FieldSpec
    {
        public FieldKind Kind;
        public double Radius = 300;        // m: the pull's reach, the lens, the tachyon sphere
        public double InnerRadius;         // m: the warp bomb's event horizon (everything inside is destroyed)
        public double AccelG = 6;          // pull towards the centre, in g (9.81 m/s²), the same at any distance inside
        public double Duration = 3;        // s the field lasts
        public double BlastTntKg;          // kg TNT set off at the centre when the field ends (gravity bomb)
        public double RingSpeed = 400;     // m/s the seismic ring spreads at

        public static FieldKind Parse(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "gravity": return FieldKind.Gravity;
                case "warp": return FieldKind.Warp;
                case "tachyon": return FieldKind.Tachyon;
                case "rinse": return FieldKind.Rinse;
                case "seismic": return FieldKind.Seismic;
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
        public bool Pulling, Destroying, Freezing;     // physics running at this moment (Freezing: new craft are caught)
        public double Speed;                           // tachyon field: how fast time runs inside (1 normal, 0 stopped)
        public bool Slowed;                            // tachyon field: craft caught are still held (Speed < 1)
        public double Tau;                             // tachyon field: the bomb's own clock (its flash and pieces): forward, held, back
        public double RingRadius2, RingAlpha2;         // the seismic ring: radius (m) and brightness 0-1
        public bool Killing;                           // the Blue Rinse sphere still spreading (it kills the crews it reaches)
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
            s.Kind == FieldKind.Tachyon ? s.Duration + SpeedUp + 0.2 :
            s.Kind == FieldKind.Rinse ? RinseSpread + RinseFade :
            s.Kind == FieldKind.Seismic ? s.Radius / Math.Max(1, s.RingSpeed) + 0.1 :
            s.Duration + (s.Kind == FieldKind.Gravity ? PulseTime + 0.2 : FlashTail);

        // ---- The tachyon field's time ----
        // Time inside slows to a stop over SlowDown seconds, holds, and comes back over SpeedUp seconds after the field's
        // duration (both smooth). The bomb's own clock runs at that speed, so its flash and pieces slow to a standstill,
        // hang there, and play back to the start as time returns.
        public const double SlowDown = 1.2, SpeedUp = 2.0;

        // The Blue Rinse: the sphere spreads over RinseSpread seconds (fast at first, easing out), then fades.
        public const double RinseSpread = 2.5, RinseFade = 1.0;

        /// <summary>How fast time runs inside the field at t (1 normal, 0 stopped).</summary>
        public static double TimeSpeed(double t, double duration)
        {
            if (t <= 0) return 1;
            if (t < SlowDown) return 1 - Smooth(0, SlowDown, t);
            if (t < duration) return 0;
            return Smooth(duration, duration + SpeedUp, t);
        }

        // Time elapsed inside over the first u seconds of the slow-down (the integral of the speed).
        static double Inside(double u)
        {
            if (u <= 0) return 0;
            if (u >= SlowDown) return SlowDown / 2;
            double x = u / SlowDown;
            return u - SlowDown * (x * x * x - x * x * x * x / 2);
        }

        /// <summary>The bomb's own clock: runs forward as time slows, holds, then plays back to 0 while time returns.</summary>
        public static double BombClock(double t, double duration)
        {
            if (t <= 0) return 0;
            if (t < duration) return Inside(t);
            if (t < duration + SpeedUp) return Inside(SlowDown * (1 - (t - duration) / SpeedUp));
            return 0;
        }   // (the tachyon flash comes as the sphere closes)

        public static FieldLook At(FieldSpec s, double t)
        {
            var l = new FieldLook { Speed = 1 };
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
                case FieldKind.Rinse:
                {
                    // An electric-blue sphere spreading from the centre out to the full radius, a flash as it goes off, then
                    // fading; every Kerbal it reaches dies.
                    double x = Clamp01(t / RinseSpread);
                    l.SphereRadius = R * (1 - (1 - x) * (1 - x) * (1 - x));
                    l.SphereAlpha = t < RinseSpread ? 0.9 : 0.9 * (1 - Clamp01((t - RinseSpread) / RinseFade));
                    l.Killing = t <= RinseSpread;
                    l.Flash = 2.5 * Math.Exp(-t / 0.2); l.FlashRadius = 0.1 * R;
                    break;
                }
                case FieldKind.Seismic:
                {
                    // A flat ring of electric-blue energy racing out to the full radius, fading over the last fifth.
                    double life = R / Math.Max(1, s.RingSpeed);
                    l.RingRadius2 = Math.Min(R, s.RingSpeed * t);
                    l.RingAlpha2 = t <= life ? 1 - Smooth(0.8 * life, life, t) : 0;
                    l.Flash = 2.8 * Math.Exp(-t / 0.15); l.FlashRadius = 40;
                    break;
                }
                case FieldKind.Tachyon:
                {
                    // A green flash that slows to a standstill as time does and hangs in the middle, round the bomb's
                    // frozen pieces; a sphere that swells to the full radius in half a second and holds. At the end both
                    // play backwards while time returns, and the sphere shrinks away.
                    double end = D + SpeedUp;
                    l.Speed = TimeSpeed(t, D);
                    l.Slowed = t < end;
                    l.Freezing = on;
                    l.Tau = BombClock(t, D);
                    double shrink = t >= D ? Smooth(D, end, t) : 0;
                    l.SphereRadius = R * Smooth(0, 0.5, t) * (1 - shrink);
                    l.SphereAlpha = (0.55 + 0.45 * Math.Exp(-t / 0.3)) * (1 - shrink);
                    if (t < end)
                    {
                        l.Flash = 2.2 * Math.Exp(-l.Tau / 0.45) * Smooth(0, 0.03, l.Tau + (t < D ? 0.03 : 0));
                        l.FlashRadius = 0.25 * R * (0.3 + 0.7 * Math.Min(1, l.Tau / 0.25));
                    }
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
