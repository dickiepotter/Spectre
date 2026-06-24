namespace RP.Spectre.Combat
{
    using System;

    /// <summary>
    /// A shared energy <b>capacitor</b> that weapons draw from (build brief S8.3). Firing spends charge;
    /// the capacitor trickles back up over time. Overdraw it and your guns stall until it recovers —
    /// forcing fire discipline and the "do I shoot or do I dodge" tension. The player's prototype guns draw
    /// hard on this, which is what keeps their over-class damage honest (S10.4).
    /// </summary>
    public sealed class Capacitor
    {
        public double Capacity { get; }
        public double Current { get; private set; }

        /// <summary>Charge regained per second.</summary>
        public double RechargeRate { get; }

        public Capacitor(double capacity, double rechargeRate)
        {
            Capacity = capacity;
            Current = capacity;
            RechargeRate = rechargeRate;
        }

        /// <summary>The fraction of full charge currently available, 0…1.</summary>
        public double Fraction => Capacity <= 0 ? 0 : Current / Capacity;

        /// <summary>Spends <paramref name="cost"/> if it is available; returns false (drawing nothing) if not.</summary>
        public bool TryDraw(double cost)
        {
            if (cost > Current) return false;
            Current -= cost;
            return true;
        }

        /// <summary>Recharges toward the cap.</summary>
        public void Update(double dt)
        {
            if (Current < Capacity)
            {
                Current = Math.Min(Capacity, Current + RechargeRate * dt);
            }
        }
    }
}
