using System;

namespace FarFutureWeaponry.Core
{
    public enum ContainmentStatus { Empty, Contained, Failing, Annihilating }

    /// <summary>
    /// A magnetic antimatter trap, like Far Future Technologies' tanks: holding any antimatter costs a flat amount of
    /// ElectricCharge per second. Without it the trap holds for a short grace period, then the antimatter starts to
    /// annihilate against the walls at a fixed rate, and that energy heats the part (which overheats and explodes
    /// unless power comes back first).
    /// </summary>
    public sealed class Containment
    {
        public double EcPerSecond = 1;
        public double GracePeriod = 2;          // s without power before annihilation starts
        public double AnnihilationRate = 0.001;  // units/s once it does

        public ContainmentStatus Status { get; private set; } = ContainmentStatus.Empty;
        public double UnpoweredFor { get; private set; }   // s

        /// <summary>EC to ask for this step (0 if empty).</summary>
        public double EcRequest(double amount, double dt) => amount > 0 && dt > 0 ? EcPerSecond * dt : 0;

        /// <summary>
        /// Advance one step. <paramref name="ecGot"/> is what the request returned. Returns the units annihilated this
        /// step (never more than <paramref name="amount"/>).
        /// </summary>
        public double Step(double amount, double ecGot, double dt)
        {
            if (amount <= 0)
            {
                Status = ContainmentStatus.Empty;
                UnpoweredFor = 0;
                return 0;
            }
            double needed = EcRequest(amount, dt);
            if (needed <= 0 || ecGot >= needed * 0.999)
            {
                Status = ContainmentStatus.Contained;
                UnpoweredFor = 0;
                return 0;
            }
            // Partly powered: the shortfall counts as unpowered time.
            UnpoweredFor += dt * (1 - Math.Max(0, ecGot) / needed);
            if (UnpoweredFor < GracePeriod)
            {
                Status = ContainmentStatus.Failing;
                return 0;
            }
            Status = ContainmentStatus.Annihilating;
            return Math.Min(amount, AnnihilationRate * dt);
        }

        public void Reset()
        {
            Status = ContainmentStatus.Empty;
            UnpoweredFor = 0;
        }
    }
}
