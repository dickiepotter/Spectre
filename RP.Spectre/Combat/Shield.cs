namespace RP.Spectre.Combat
{
    using System;

    /// <summary>
    /// A shield — "a clock, not a wall" (build brief S2/S8.1). It has a capacity, a regen rate, and,
    /// crucially, a <b>regen delay</b>: regeneration only resumes a set time after the last hit, so
    /// sustained fire keeps it suppressed. This is the key tuning lever that makes shields buy you seconds
    /// rather than make you invulnerable. Fighters carry one omnidirectional shield; capital facets
    /// (Phase 6) are several of these.
    /// </summary>
    public sealed class Shield
    {
        /// <summary>Maximum shield strength.</summary>
        public double Capacity { get; }

        /// <summary>Current shield strength (0…<see cref="Capacity"/>).</summary>
        public double Current { get; set; }

        /// <summary>Strength regained per second, once regen resumes.</summary>
        public double RegenRate { get; }

        /// <summary>Seconds after the last hit before regeneration resumes.</summary>
        public double RegenDelay { get; }

        // Time since the last hit. Starts "long ago" so an untouched shield can regen immediately.
        private double _timeSinceHit = double.PositiveInfinity;

        public Shield(double capacity, double regenRate, double regenDelay)
        {
            Capacity = capacity;
            Current = capacity;
            RegenRate = regenRate;
            RegenDelay = regenDelay;
        }

        /// <summary>True when the shield is down — the hull beneath is now exposed.</summary>
        public bool IsDown => Current <= 0;

        /// <summary>Records that the shield was just hit, restarting the regen-delay clock.</summary>
        public void NotifyHit() => _timeSinceHit = 0;

        /// <summary>
        /// Advances regeneration by <paramref name="dt"/> seconds. Nothing happens until
        /// <see cref="RegenDelay"/> has elapsed since the last hit — then strength refills at
        /// <see cref="RegenRate"/> up to the cap.
        /// </summary>
        public void Update(double dt)
        {
            _timeSinceHit += dt;
            if (_timeSinceHit >= RegenDelay && Current < Capacity)
            {
                Current = Math.Min(Capacity, Current + RegenRate * dt);
            }
        }
    }
}
