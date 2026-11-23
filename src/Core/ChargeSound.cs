using System;

namespace FarFutureWeaponry.Core
{
    /// <summary>
    /// A capacitor-bank charge-up sound made in code (no audio files), exactly as long as the weapon's charge: a whine
    /// rising steadily in pitch (the same rate all the way, a little over an octave and a half) and loud from the first
    /// moment, a hum underneath, and electric crackling over the top all through it, getting denser towards the shot.
    /// It fades out in the last few milliseconds so the firing sound takes over cleanly.
    /// </summary>
    public static class ChargeSound
    {
        public const float StartHz = 260, EndHz = 820;

        /// <summary>Mono samples in [-1, 1].</summary>
        public static float[] Generate(int sampleRate, float seconds, int seed = 1)
        {
            int n = Math.Max(1, (int)(sampleRate * Math.Max(0.05f, seconds)));
            var s = new float[n];
            var rng = new Random(seed);
            double phase = 0, phase2 = 0, buzzPhase = 0;
            double crackle = 0, crackleSign = 1;
            double ln = Math.Log(EndHz / StartHz);
            double hp = 0, prevNoise = 0;
            for (int i = 0; i < n; i++)
            {
                double p = (double)i / n;                       // 0..1 through the charge
                double f = StartHz * Math.Exp(ln * p);           // a steady rise (log-linear)
                phase += 2 * Math.PI * f / sampleRate;
                phase2 += 2 * Math.PI * f * 2.01 / sampleRate;   // a slightly detuned octave: a beating, electric edge
                double whine = (Math.Sin(phase) * 0.55 + Math.Sin(phase2) * 0.18) * (0.55 + 0.35 * p);

                buzzPhase += 2 * Math.PI * 100 / sampleRate;     // hum: a soft square wave at 100 Hz
                double buzz = Math.Tanh(Math.Sin(buzzPhase) * 4) * 0.1;

                // Crackling: sharp bursts of high-passed noise, 25 a second at the start, 60 at the end.
                double rate = 25 + 35 * p;
                if (rng.NextDouble() < rate / sampleRate) { crackle = 0.6 + 0.4 * rng.NextDouble(); crackleSign = rng.NextDouble() < 0.5 ? -1 : 1; }
                double noise = rng.NextDouble() * 2 - 1;
                hp = 0.85 * (hp + noise - prevNoise); prevNoise = noise;   // (high-passed: a snap, not a thud)
                double crack = (hp * 0.8 + crackleSign * 0.4) * crackle;
                crackle *= Math.Exp(-1.0 / (0.004 * sampleRate));

                double v = whine + buzz + crack * 0.7;
                double edge = Math.Min(1, Math.Min(i / (0.01 * sampleRate), (n - 1 - i) / (0.02 * sampleRate)));
                s[i] = (float)Math.Max(-1, Math.Min(1, v * edge * 0.75));
            }
            return s;
        }
    }
}
