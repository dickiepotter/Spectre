namespace RP.Spectre.Missions
{
    /// <summary>The difficulty presets (build brief S22). Standard is the tuned "marginally challenging"
    /// target the lethality check is balanced against.</summary>
    public enum DifficultyPreset
    {
        Story,
        Standard,
        Hard,
        Custom,
    }

    /// <summary>
    /// The difficulty scalars (build brief S22). The player's advantage — the agile, over-gunned Spectre —
    /// is a <b>constant</b>; difficulty never touches it. What these dials scale is everything the advantage
    /// does <i>not</i> solve: how many enemies there are, how hard and accurately they hit, how aggressively
    /// they coordinate, and how much damage the player takes. Each is a simple multiplier so balancing is a
    /// data change, and they feed the lethality/scenario tests so a preset can never silently break balance.
    /// </summary>
    public readonly struct DifficultyScalars
    {
        /// <summary>Enemy count / reinforcement rate multiplier.</summary>
        public double EnemyCount { get; }

        /// <summary>Enemy weapon damage multiplier.</summary>
        public double EnemyDamage { get; }

        /// <summary>Enemy accuracy / reaction multiplier.</summary>
        public double EnemyAccuracy { get; }

        /// <summary>Enemy aggression / coordination multiplier.</summary>
        public double EnemyAggression { get; }

        /// <summary>Multiplier on damage the <i>player</i> takes.</summary>
        public double IncomingDamage { get; }

        public DifficultyScalars(double enemyCount, double enemyDamage, double enemyAccuracy, double enemyAggression, double incomingDamage)
        {
            EnemyCount = enemyCount;
            EnemyDamage = enemyDamage;
            EnemyAccuracy = enemyAccuracy;
            EnemyAggression = enemyAggression;
            IncomingDamage = incomingDamage;
        }

        /// <summary>The scalars for a preset (the S18/S22 table). Custom returns Standard as its starting point.</summary>
        public static DifficultyScalars For(DifficultyPreset preset) => preset switch
        {
            DifficultyPreset.Story => new DifficultyScalars(0.6, 0.6, 0.7, 0.7, 0.7),
            DifficultyPreset.Hard => new DifficultyScalars(1.4, 1.3, 1.25, 1.3, 1.2),
            _ => new DifficultyScalars(1.0, 1.0, 1.0, 1.0, 1.0), // Standard / Custom baseline
        };
    }
}
