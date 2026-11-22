using System;
using System.Globalization;

namespace FarFutureWeaponry.Core
{
    /// <summary>Plain colour (0-1 channels), so the effect maths can be tested without Unity.</summary>
    public struct Rgba
    {
        public float R, G, B, A;
        public Rgba(float r, float g, float b, float a) { R = r; G = g; B = b; A = a; }

        /// <summary>
        /// Parse "r, g, b[, a]" in 0-255 (BDArmory's format), or "#rrggbb[aa]". Returns <paramref name="fallback"/> if
        /// the text can't be read.
        /// </summary>
        public static Rgba Parse(string text, Rgba fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            text = text.Trim();
            try
            {
                if (text.StartsWith("#"))
                {
                    string h = text.Substring(1);
                    if (h.Length != 6 && h.Length != 8) return fallback;
                    float C(int i) => int.Parse(h.Substring(i, 2), NumberStyles.HexNumber) / 255f;
                    return new Rgba(C(0), C(2), C(4), h.Length == 8 ? C(6) : 1);
                }
                string[] p = text.Split(',');
                if (p.Length < 3 || p.Length > 4) return fallback;
                float V(int i) => Math.Max(0, Math.Min(255, float.Parse(p[i].Trim(), CultureInfo.InvariantCulture))) / 255f;
                return new Rgba(V(0), V(1), V(2), p.Length == 4 ? V(3) : 1);
            }
            catch (FormatException) { return fallback; }
            catch (OverflowException) { return fallback; }
        }
    }

    /// <summary>Time curves for the procedural effects.</summary>
    public static class FxCurves
    {
        /// <summary>
        /// Impact flash brightness, 0-1: a fast rise over the first 8% of <paramref name="duration"/>, then an
        /// exponential fade that reaches 0 exactly at the end.
        /// </summary>
        public static float Flash(float t, float duration)
        {
            if (duration <= 0 || t < 0 || t >= duration) return 0;
            float rise = 0.08f * duration;
            if (t < rise) return t / rise;
            float x = (t - rise) / (duration - rise);           // 0..1
            const float k = 4f;
            float e = (float)Math.Exp(-k * x), end = (float)Math.Exp(-k);
            return (e - end) / (1 - end);
        }

        /// <summary>Flash size factor: grows quickly to full size, then keeps swelling a little as it fades.</summary>
        public static float FlashSize(float t, float duration)
        {
            if (duration <= 0 || t <= 0) return 0.3f;
            float x = Math.Min(1, t / duration);
            return 0.3f + 0.7f * (float)Math.Sqrt(Math.Min(1, x / 0.15f)) + 0.3f * x;
        }

        /// <summary>Trail opacity for a point <paramref name="age"/> seconds old: holds, then fades out over the rest of <paramref name="fade"/>.</summary>
        public static float TrailAlpha(float age, float fade, float hold = 0.15f)
        {
            if (age < 0) return 1;
            if (fade <= 0 || age >= fade) return 0;
            float h = Math.Min(hold, fade * 0.5f);
            if (age <= h) return 1;
            float x = (age - h) / (fade - h);
            return (1 - x) * (1 - x);
        }

        /// <summary>
        /// Spark emission while a weapon charges, as a fraction of the full rate: a trickle at first, a storm just
        /// before the shot. <paramref name="chargeTime"/> &lt;= 0 counts as fully charged.
        /// </summary>
        public static float ChargeSparks(float elapsed, float chargeTime)
        {
            float p = chargeTime > 0 ? Math.Max(0, Math.Min(1, elapsed / chargeTime)) : 1;
            return 0.15f + 0.85f * p * p;
        }
    }
}
