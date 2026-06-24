namespace RP.Spectre.Combat
{
    using System;

    /// <summary>
    /// A weapon <b>heat</b> model (build brief S8.3). Each shot adds heat; the sink dissipates it over
    /// time. Cross the limit and the weapon <b>overheats</b> — it must vent and cannot fire until it has
    /// cooled back down to a safe level. Together with the <see cref="Capacitor"/> this rewards measured
    /// fire over spraying, and is the cost that balances the prototype guns' high damage.
    /// </summary>
    public sealed class HeatSink
    {
        /// <summary>Heat at which the weapon overheats and must vent.</summary>
        public double Maximum { get; }

        /// <summary>Current heat.</summary>
        public double Current { get; private set; }

        /// <summary>Heat shed per second.</summary>
        public double DissipationRate { get; }

        /// <summary>Once overheated, the fraction of <see cref="Maximum"/> it must cool back to before
        /// firing is allowed again (hysteresis, so it does not stutter on the edge).</summary>
        public double ResetFraction { get; }

        /// <summary>True while venting after an overheat — the weapon cannot fire.</summary>
        public bool IsOverheated { get; private set; }

        public HeatSink(double maximum, double dissipationRate, double resetFraction = 0.5)
        {
            Maximum = maximum;
            DissipationRate = dissipationRate;
            ResetFraction = resetFraction;
            Current = 0;
        }

        /// <summary>The fraction of the heat limit currently used, 0…1+ (drives a HUD heat bar).</summary>
        public double Fraction => Maximum <= 0 ? 0 : Current / Maximum;

        /// <summary>True if the weapon is cool enough to fire.</summary>
        public bool CanFire => !IsOverheated;

        /// <summary>Adds heat from a shot, tripping the overheat state at the limit.</summary>
        public void Add(double heat)
        {
            Current += heat;
            if (Current >= Maximum) IsOverheated = true;
        }

        /// <summary>Dissipates heat; clears the overheat state once cooled below the reset level.</summary>
        public void Update(double dt)
        {
            Current = Math.Max(0, Current - DissipationRate * dt);
            if (IsOverheated && Current <= Maximum * ResetFraction)
            {
                IsOverheated = false;
            }
        }
    }
}
