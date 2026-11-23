using System;

namespace FarFutureWeaponry.Core
{
    /// <summary>
    /// A capacitor-bank charge-up sound made in code (no audio files), exactly as long as the weapon's charge:
    /// a whine that sweeps up in pitch and loudness, a mains-style hum underneath, and electric crackles that get
    /// denser towards the shot. It fades out in the last few milliseconds so the firing sound takes over cleanly.
    /// </summary>
    public static class ChargeSound
    {
        public const float StartHz = 160, EndHz = 2200;

        /// <summary>Mono samples in [-1, 1].</summary>
        public static float[] Generate(int sampleRate, float seconds, int seed = 1)
        {
            int n = Math.Max(1, (int)(sampleRate * Math.Max(0.05f, seconds)));
            var s = new float[n];
            var rng = new Random(seed);
            double phase = 0, phase2 = 0;
            double crackle = 0;   // current crackle burst level (decays)
            double buzzPhase = 0;
            double ln = Math.Log(EndHz / StartHz);
            for (int i = 0; i < n; i++)
            {
                double p = (double)i / n;                      // 0..1 through the charge
                double f = StartHz * Math.Exp(ln * p * p);      // slow start, fast rise at the end
                phase += 2 * Math.PI * f / sampleRate;
                phase2 += 2 * Math.PI * f * 2.01 / sampleRate;  // slightly detuned octave: a beating, electric edge
                double whine = Math.Sin(phase) * 0.55 + Math.Sin(phase2) * 0.2;
                whine *= 0.25 + 0.75 * p;

                buzzPhase += 2 * Math.PI * 100 / sampleRate;    // hum: a soft square wave at 100 Hz
                double buzz = Math.Tanh(Math.Sin(buzzPhase) * 4) * 0.12 * (0.6 + 0.4 * Math.Sin(2 * Math.PI * 7 * p));

                // Crackles: random bursts of noise, 8/s at the start, 70/s at the end.
                double rate = 8 + 62 * p * p;
                if (rng.NextDouble() < rate / sampleRate) crackle = 0.5 + 0.5 * rng.NextDouble();
                double noise = (rng.NextDouble() * 2 - 1) * crackle;
                crackle *= Math.Exp(-1.0 / (0.003 * sampleRate));   // each burst lasts a few ms

                double v = whine + buzz + noise * 0.6;
                double edge = Math.Min(1, Math.Min(i / (0.01 * sampleRate), (n - 1 - i) / (0.02 * sampleRate)));
                s[i] = (float)Math.Max(-1, Math.Min(1, v * edge * 0.8));
            }
            return s;
        }
    }
}
