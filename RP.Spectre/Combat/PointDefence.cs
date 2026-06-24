namespace RP.Spectre.Combat
{
    using System.Collections.Generic;

    /// <summary>
    /// A guided missile in flight toward the ship, reduced to what point-defence needs to know: how much
    /// punishment it can take, how far out it is, and how fast it is closing.
    /// </summary>
    public sealed class GuidedMissile
    {
        /// <summary>Remaining health; point-defence fire whittles this down.</summary>
        public double Health { get; set; }

        /// <summary>Distance to the defended ship (metres).</summary>
        public double DistanceToTarget { get; set; }

        /// <summary>Closing speed (metres/second).</summary>
        public double ClosingSpeed { get; set; }

        public bool Destroyed => Health <= 0;
        public bool Reached => DistanceToTarget <= 0;
    }

    /// <summary>
    /// A point-defence battery (build brief S8.3/S8.4): an auto-targeting, short-range weapon that shoots
    /// down incoming missiles (and small debris) before they reach the ship. It is the counter that makes
    /// missiles a tactical choice rather than an auto-win — fire enough and some leak through; a screen of
    /// point-defence stops them.
    /// </summary>
    public sealed class PointDefenceSystem
    {
        /// <summary>Engagement range (metres) — missiles outside this are not yet targeted.</summary>
        public double Range { get; }

        /// <summary>Sustained damage to the targeted missile (damage/sec); a PD weapon's rate × damage.</summary>
        public double DamagePerSecond { get; }

        public PointDefenceSystem(double range, double damagePerSecond)
        {
            Range = range;
            DamagePerSecond = damagePerSecond;
        }

        /// <summary>The outcome of one defended frame.</summary>
        public readonly struct TickResult
        {
            public int Intercepted { get; }
            public int LeakedThrough { get; }
            public TickResult(int intercepted, int leakedThrough)
            {
                Intercepted = intercepted;
                LeakedThrough = leakedThrough;
            }
        }

        /// <summary>
        /// Advances all tracked missiles by <paramref name="dt"/>: concentrates fire on the closest missile
        /// in range, moves them inward, and removes those destroyed (intercepted) or that reached the ship
        /// (leaked through). Returns how many did each.
        /// </summary>
        public TickResult Update(IList<GuidedMissile> missiles, double dt)
        {
            // Target the closest in-range, still-alive missile and pour fire into it.
            GuidedMissile? target = null;
            foreach (GuidedMissile m in missiles)
            {
                if (m.Destroyed || m.DistanceToTarget > Range) continue;
                if (target is null || m.DistanceToTarget < target.DistanceToTarget) target = m;
            }

            if (target is not null) target.Health -= DamagePerSecond * dt;

            // Move missiles inward.
            foreach (GuidedMissile m in missiles) m.DistanceToTarget -= m.ClosingSpeed * dt;

            // Resolve interceptions and leaks.
            int intercepted = 0, leaked = 0;
            for (int i = missiles.Count - 1; i >= 0; i--)
            {
                GuidedMissile m = missiles[i];
                if (m.Destroyed) { intercepted++; missiles.RemoveAt(i); }
                else if (m.Reached) { leaked++; missiles.RemoveAt(i); }
            }

            return new TickResult(intercepted, leaked);
        }
    }
}
