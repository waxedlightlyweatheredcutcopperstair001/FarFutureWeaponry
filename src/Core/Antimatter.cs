using System;

namespace FarFutureWeaponry.Core
{
    /// <summary>
    /// Antimatter energy, in Far Future Technologies' units, so FFT tanks and FFW parts agree:
    /// one unit releases 36 GJ when it annihilates (FFT's DetonationKJPerUnit = 36,000,000 kJ).
    /// </summary>
    public static class Antimatter
    {
        public const string ResourceName = "Antimatter";
        public const double KJPerUnit = 36e6;
        public const double KJPerKgTnt = 4184;          // 4.184 MJ per kg of TNT

        /// <summary>TNT equivalent of one unit: about 8.6 t.</summary>
        public static double TntKgPerUnit => KJPerUnit / KJPerKgTnt;

        public static double TntKg(double units) => units * TntKgPerUnit;

        public static double UnitsForTntKg(double kg) => kg / TntKgPerUnit;

        /// <summary>Heat (kW) released by annihilating <paramref name="units"/> over <paramref name="dt"/> seconds.</summary>
        public static double HeatKW(double units, double dt) => dt > 0 ? units * KJPerUnit / dt : 0;

        /// <summary>
        /// Whole shots left in <paramref name="amount"/> at <paramref name="perShot"/> each. BDArmory fires when at least
        /// 99.5% of a shot is left (rounding across several tanks), so this does too.
        /// </summary>
        public static int ShotsLeft(double amount, double perShot)
        {
            if (perShot <= 0 || amount <= 0) return 0;
            return (int)Math.Floor(amount / perShot + 0.005);
        }
    }
}
