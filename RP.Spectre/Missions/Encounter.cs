namespace RP.Spectre.Missions
{
    using System;

    /// <summary>
    /// How difficulty shapes the <i>scope</i> of an encounter (build brief S22). The player's advantage is a
    /// constant, so difficulty does not buff the Spectre or nerf the enemy ship-for-ship — it changes how
    /// many enemies turn up. This is the one place the <see cref="DifficultyScalars.EnemyCount"/> dial is
    /// turned into an actual spawn count, kept separate so mission authoring stays difficulty-agnostic.
    /// </summary>
    public static class Encounter
    {
        /// <summary>
        /// Scales a mission's baseline enemy count by the difficulty dial, rounded to a whole ship and never
        /// below one — an encounter always has at least one enemy to fight.
        /// </summary>
        public static int ScaledEnemyCount(int baseCount, DifficultyScalars scalars)
        {
            int scaled = (int)Math.Round(baseCount * scalars.EnemyCount, MidpointRounding.AwayFromZero);
            return Math.Max(1, scaled);
        }
    }
}
