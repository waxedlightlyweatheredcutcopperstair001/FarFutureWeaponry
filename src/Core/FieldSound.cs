using System;

namespace FarFutureWeaponry.Core
{
    /// <summary>The fields' sounds, made in code like the charge sound (no audio files). Mono samples in [-1, 1].</summary>
    public static class FieldSound
    {
        /// <summary>
        /// A deep rumble for a gravity field: brown noise and a low throb whose pitch sinks, swelling in and, if
        /// <paramref name="riseAtEnd"/>, climbing in pitch and loudness at the end (the collapse).
        /// </summary>
        public static float[] Rumble(int sampleRate, float seconds, bool riseAtEnd, int seed = 1)
        {
            int n = Math.Max(1, (int)(sampleRate * Math.Max(0.1f, seconds)));
            var s = new float[n];
            var rng = new Random(seed);
            double brown = 0, phase = 0, phase2 = 0;
            for (int i = 0; i < n; i++)
            {
                double p = (double)i / n;
                brown = (brown + (rng.NextDouble() * 2 - 1) * 0.04) * 0.996;
                double f = 42 - 14 * p + (riseAtEnd ? 70 * Math.Pow(p, 6) : 0);
                phase += 2 * Math.PI * f / sampleRate;
                phase2 += 2 * Math.PI * f * 1.5 / sampleRate;
                double throb = 0.6 + 0.4 * Math.Sin(2 * Math.PI * 1.7 * i / sampleRate);
                double v = brown * 4 + (Math.Sin(phase) * 0.5 + Math.Sin(phase2) * 0.15) * throb;
                double env = Math.Min(1, i / (0.15 * sampleRate)) * Math.Min(1, (n - 1 - i) / (0.08 * sampleRate));
                if (riseAtEnd) env *= 0.7 + 0.5 * Math.Pow(p, 4);
                s[i] = (float)Math.Max(-1, Math.Min(1, v * env * 0.7));
            }
            return s;
        }

        /// <summary>
        /// The seismic charge: a beat of silence, then a deep, ringing "bwaaang": a low tone and metallic overtones that
        /// bend down and ring out over a few seconds.
        /// </summary>
        public static float[] Seismic(int sampleRate, float seconds, int seed = 1)
        {
            int n = Math.Max(1, (int)(sampleRate * Math.Max(0.5f, seconds)));
            var s = new float[n];
            var rng = new Random(seed);
            double[] ratio = { 1, 2.01, 2.76, 3.98, 5.4 }, amp = { 0.55, 0.3, 0.22, 0.12, 0.08 }, decay = { 2.2, 1.6, 1.1, 0.8, 0.5 };
            var phase = new double[ratio.Length];
            int start = (int)(0.12 * sampleRate);
            for (int i = start; i < n; i++)
            {
                double t = (double)(i - start) / sampleRate;
                double f0 = 70 * (1 + 0.5 * Math.Exp(-t / 0.15));   // the strike bends down into the ring
                double v = 0;
                for (int k = 0; k < ratio.Length; k++)
                {
                    phase[k] += 2 * Math.PI * f0 * ratio[k] / sampleRate;
                    v += Math.Sin(phase[k]) * amp[k] * Math.Exp(-t / decay[k]);
                }
                v += (rng.NextDouble() * 2 - 1) * 0.35 * Math.Exp(-t / 0.05);   // the crack at the start
                double env = Math.Min(1, (i - start) / (0.004 * sampleRate)) * Math.Min(1, (n - 1 - i) / (0.05 * sampleRate));
                s[i] = (float)Math.Max(-1, Math.Min(1, v * env * 0.85));
            }
            return s;
        }

        /// <summary>
        /// A swept tone with a shimmer of noise: falling for the tachyon field's start (time stopping), rising for its
        /// collapse (time catching up).
        /// </summary>
        public static float[] Sweep(int sampleRate, float seconds, double fromHz, double toHz, int seed = 1)
        {
            int n = Math.Max(1, (int)(sampleRate * Math.Max(0.05f, seconds)));
            var s = new float[n];
            var rng = new Random(seed);
            double phase = 0, phase2 = 0, ln = Math.Log(Math.Max(1, toHz) / Math.Max(1, fromHz));
            for (int i = 0; i < n; i++)
            {
                double p = (double)i / n;
                double f = fromHz * Math.Exp(ln * p);
                phase += 2 * Math.PI * f / sampleRate;
                phase2 += 2 * Math.PI * f * 1.503 / sampleRate;
                double shimmer = (rng.NextDouble() * 2 - 1) * 0.12 * (1 - p);
                double v = Math.Sin(phase) * 0.6 + Math.Sin(phase2) * 0.25 + shimmer;
                double env = Math.Min(1, i / (0.01 * sampleRate)) * Math.Pow(1 - p, 0.7);
                s[i] = (float)Math.Max(-1, Math.Min(1, v * env * 0.8));
            }
            return s;
        }
    }
}
