using System;

namespace FarFutureWeaponry.Core
{
    /// <summary>
    /// Breaking a model into fragments (the tachyon bomb's shatter and reassembly), as pure maths: which fragment each
    /// triangle goes to, where each fragment flies, and how far out it is at a given moment.
    /// </summary>
    public static class Shatter
    {
        /// <summary>
        /// Group triangles by their centroids (x,y,z triples) round <paramref name="count"/> random seed points inside the
        /// centroids' bounding box (a Voronoi split, so each fragment is one chunk). Returns the group of each triangle;
        /// every group 0..count-1 that gets no triangle is simply empty.
        /// </summary>
        public static int[] Assign(float[] centroids, int count, int seed)
        {
            int n = centroids.Length / 3;
            var group = new int[n];
            if (n == 0 || count <= 1) return group;
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float x = centroids[i * 3], y = centroids[i * 3 + 1], z = centroids[i * 3 + 2];
                if (x < minX) minX = x; if (y < minY) minY = y; if (z < minZ) minZ = z;
                if (x > maxX) maxX = x; if (y > maxY) maxY = y; if (z > maxZ) maxZ = z;
            }
            var rng = new Random(seed);
            // Seeds on actual triangles (a random box point could fall in empty space and make a sliver).
            var sx = new float[count]; var sy = new float[count]; var sz = new float[count];
            for (int k = 0; k < count; k++)
            {
                int i = rng.Next(n);
                sx[k] = centroids[i * 3]; sy[k] = centroids[i * 3 + 1]; sz[k] = centroids[i * 3 + 2];
            }
            for (int i = 0; i < n; i++)
            {
                float x = centroids[i * 3], y = centroids[i * 3 + 1], z = centroids[i * 3 + 2];
                int best = 0; float bestD = float.MaxValue;
                for (int k = 0; k < count; k++)
                {
                    float dx = x - sx[k], dy = y - sy[k], dz = z - sz[k], d = dx * dx + dy * dy + dz * dz;
                    if (d < bestD) { bestD = d; best = k; }
                }
                group[i] = best;
            }
            return group;
        }

        public const double BurstTime = 0.35, ReturnTime = 0.6;

        /// <summary>
        /// How far out a fragment is (0 = in place, 1 = fully flung) at time t after the detonation, for a field that
        /// collapses at <paramref name="collapseAt"/>: flung out fast, hangs still (frozen by its own field), then pulled
        /// back together as the field collapses.
        /// </summary>
        public static double Spread(double t, double collapseAt)
        {
            if (t <= 0) return 0;
            if (t < collapseAt)
            {
                double x = Math.Min(1, t / BurstTime);
                return 1 - (1 - x) * (1 - x) * (1 - x);              // ease out
            }
            double r = Math.Min(1, (t - collapseAt) / ReturnTime);
            return 1 - r * r * r;                                    // ease in: slow start, snaps together
        }

        /// <summary>True once the fragments are back together.</summary>
        public static bool Reassembled(double t, double collapseAt) => t >= collapseAt + ReturnTime;
    }
}
