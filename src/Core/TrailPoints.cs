using System;
using System.Collections.Generic;

namespace FarFutureWeaponry.Core
{
    /// <summary>
    /// The points of a fading trail behind a projectile: positions (in any fixed frame; the plugin uses the main
    /// body's frame so floating-origin shifts don't move the trail) and the time each was laid down.
    /// </summary>
    public sealed class TrailPoints
    {
        public struct Point
        {
            public double X, Y, Z;
            public float Time;
        }

        readonly List<Point> points = new List<Point>();
        public IReadOnlyList<Point> Points => points;
        public int Count => points.Count;

        public double MinSpacing = 2;   // m between stored points
        public int MaxPoints = 512;

        /// <summary>
        /// Add the projectile's position at <paramref name="time"/>. A point closer than MinSpacing to the last one
        /// replaces it (the head of the trail stays on the projectile without piling up points).
        /// </summary>
        public void Add(double x, double y, double z, float time)
        {
            var p = new Point { X = x, Y = y, Z = z, Time = time };
            int n = points.Count;
            if (n >= 2)
            {
                Point prev = points[n - 2];
                double dx = x - prev.X, dy = y - prev.Y, dz = z - prev.Z;
                if (dx * dx + dy * dy + dz * dz < MinSpacing * MinSpacing)
                {
                    points[n - 1] = p;
                    return;
                }
            }
            points.Add(p);
            if (points.Count > MaxPoints) points.RemoveAt(0);
        }

        /// <summary>Drop points older than <paramref name="fade"/> at <paramref name="now"/>, keeping one so the last segment fades smoothly.</summary>
        public void Prune(float now, float fade)
        {
            int drop = 0;
            while (drop + 1 < points.Count && now - points[drop + 1].Time >= fade) drop++;
            if (drop > 0) points.RemoveRange(0, drop);
            if (points.Count == 1 && now - points[0].Time >= fade) points.Clear();
        }

        /// <summary>True once every point has faded.</summary>
        public bool Faded(float now, float fade) => points.Count == 0 || now - points[points.Count - 1].Time >= fade;

        public void Clear() => points.Clear();
    }
}
