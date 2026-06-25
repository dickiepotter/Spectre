namespace RP.Spectre.Audio
{
    using System;
    using RP.Game.Audio;

    /// <summary>Which musical/ambient layer the moment calls for, calm to frantic (build brief S15.4).</summary>
    public enum AudioCue
    {
        /// <summary>Open space, nothing near — sparse ambience.</summary>
        Calm,

        /// <summary>Something is off: inside the wreck, or a contact at the edge of sensors.</summary>
        Unease,

        /// <summary>Being hunted — a hostile is close but not yet shooting.</summary>
        Stalk,

        /// <summary>Active engagement — taking or trading fire.</summary>
        Combat,
    }

    /// <summary>The situation the director scores against, sampled each update.</summary>
    public readonly struct ThreatContext
    {
        /// <summary>Inside the tumbling wreck — a baseline dread even with nothing near.</summary>
        public bool InsideWreck { get; init; }

        /// <summary>How many hostiles are in play nearby.</summary>
        public int NearbyHostiles { get; init; }

        /// <summary>Range to the nearest threat (metres); large/infinite when there is none.</summary>
        public double NearestThreatRange { get; init; }

        /// <summary>The player is currently taking fire.</summary>
        public bool UnderFire { get; init; }
    }

    /// <summary>
    /// The adaptive-audio director (build brief S15.4): it turns the tactical situation into a single smoothed
    /// <see cref="Tension"/> value and an <see cref="AudioCue"/>, which the mixer uses to crossfade music and
    /// ambience. Tension <b>rises fast and decays slow</b> — the dread lingers after the threat passes, which
    /// is the whole feel of the descent. The cue is chosen with a hysteresis band so layers don't flutter on
    /// and off at a threshold. Pure logic; the actual sound playback is the engine's job.
    /// </summary>
    public sealed class TensionDirector
    {
        /// <summary>How fast tension climbs toward a higher target (units/second).</summary>
        public double RiseRate { get; set; } = 2.5;

        /// <summary>How fast it falls toward a lower target — deliberately slow, so dread lingers (units/second).</summary>
        public double DecayRate { get; set; } = 0.2;

        /// <summary>Beyond this range a threat adds nothing to tension (metres).</summary>
        public double ThreatRange { get; set; } = 1200;

        /// <summary>Current smoothed tension in [0, 1].</summary>
        public double Tension { get; private set; }

        /// <summary>The current audio layer (chosen from <see cref="Tension"/> with hysteresis).</summary>
        public AudioCue Cue { get; private set; } = AudioCue.Calm;

        /// <summary>Advances the director by <paramref name="dt"/> seconds against the current situation.</summary>
        public void Update(ThreatContext context, double dt)
        {
            double target = TargetTension(context);
            double rate = (target > Tension ? RiseRate : DecayRate) * dt;
            Tension = MoveToward(Tension, target, rate);
            Cue = SelectCue(Tension, context.UnderFire, Cue);
        }

        /// <summary>The instantaneous tension the situation warrants, before smoothing.</summary>
        private double TargetTension(ThreatContext c)
        {
            double t = 0;
            if (c.InsideWreck) t += 0.25;                                  // baseline dread inside the hulk
            t += Math.Min(c.NearbyHostiles * 0.2, 0.5);                    // a swarm is worse than a straggler
            t += 0.35 * ProximityFactor(c.NearestThreatRange);            // closer = tenser
            if (c.UnderFire) t += 0.6;                                     // bullets trump everything
            return Math.Clamp(t, 0, 1);
        }

        // 1 when the threat is on top of you, falling linearly to 0 at ThreatRange (and 0 when there is none).
        private double ProximityFactor(double range)
        {
            if (double.IsInfinity(range) || range >= ThreatRange) return 0;
            if (range <= 0) return 1;
            return 1.0 - range / ThreatRange;
        }

        // Banded selection: each threshold is sticky in the direction of the current cue, so a value hovering
        // on a boundary keeps its layer rather than thrashing. Active fire forces Combat regardless.
        private static AudioCue SelectCue(double tension, bool underFire, AudioCue current)
        {
            if (underFire) return AudioCue.Combat;

            const double band = 0.07;
            double combatIn = current == AudioCue.Combat ? 0.75 - band : 0.75 + band;
            if (tension >= combatIn) return AudioCue.Combat;

            bool atLeastStalk = current is AudioCue.Combat or AudioCue.Stalk;
            double stalkIn = atLeastStalk ? 0.45 - band : 0.45 + band;
            if (tension >= stalkIn) return AudioCue.Stalk;

            bool atLeastUnease = current != AudioCue.Calm;
            double uneaseIn = atLeastUnease ? 0.15 - band : 0.15 + band;
            if (tension >= uneaseIn) return AudioCue.Unease;

            return AudioCue.Calm;
        }

        /// <summary>
        /// The music bus gain to apply right now: a tension-driven intensity (quiet bed at rest, full at peak)
        /// composed with the player's music-volume setting via the engine's <see cref="AudioMath.ComposeGain"/>.
        /// </summary>
        public float MusicGain(float musicVolumeSetting)
        {
            float intensity = 0.25f + 0.75f * (float)Tension;
            return AudioMath.ComposeGain(musicVolumeSetting, intensity);
        }

        private static double MoveToward(double from, double to, double maxDelta)
        {
            double diff = to - from;
            if (Math.Abs(diff) <= maxDelta) return to;
            return from + Math.Sign(diff) * maxDelta;
        }
    }
}
