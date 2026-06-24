namespace RP.Spectre.Sensors
{
    using System;

    /// <summary>
    /// Tracks a missile <b>lock-on</b> (build brief S8.4): a lock takes time to build and needs a steady
    /// signal. Strong contacts lock fast; weak ones (a dark target, or one behind a gas cloud) lock slowly
    /// or not at all, and an established lock <i>breaks</i> if the target slips out of sensor — which is the
    /// player's (and the AI's) escape: break line of sight or go dark and the lock decays.
    /// </summary>
    public sealed class LockOnTracker
    {
        /// <summary>Seconds of solid signal needed to achieve a lock.</summary>
        public double RequiredSeconds { get; }

        /// <summary>Signal strength below which the lock makes no progress and an existing one decays.</summary>
        public double MinimumSignal { get; }

        /// <summary>How fast a lock decays (progress/second) when the signal is lost.</summary>
        public double DecayRate { get; }

        /// <summary>Current lock progress in seconds (0…<see cref="RequiredSeconds"/>).</summary>
        public double Progress { get; private set; }

        public LockOnTracker(double requiredSeconds = 2.0, double minimumSignal = 0.15, double decayRate = 1.5)
        {
            RequiredSeconds = requiredSeconds;
            MinimumSignal = minimumSignal;
            DecayRate = decayRate;
        }

        /// <summary>True once a full lock is achieved.</summary>
        public bool IsLocked => Progress >= RequiredSeconds;

        /// <summary>Lock progress as a 0…1 fraction (drives the HUD lock reticle).</summary>
        public double Fraction => RequiredSeconds <= 0 ? 1 : Progress / RequiredSeconds;

        /// <summary>
        /// Advances the lock by one tick. A signal above <see cref="MinimumSignal"/> builds the lock faster
        /// the stronger it is; a signal below it decays any progress, eventually breaking the lock.
        /// </summary>
        public void Update(double dt, double signalStrength)
        {
            if (signalStrength <= MinimumSignal)
            {
                Progress = Math.Max(0, Progress - DecayRate * dt);
            }
            else
            {
                Progress = Math.Min(RequiredSeconds, Progress + signalStrength * dt);
            }
        }

        /// <summary>Drops the lock (e.g. when switching targets).</summary>
        public void Reset() => Progress = 0;
    }
}
