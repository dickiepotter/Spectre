namespace RP.Spectre.Combat
{
    /// <summary>
    /// The kind of damage a hit delivers. Each weapon family leans on one, and shields/hull respond
    /// differently to each (build brief S8.1) — the per-weapon "vs shield" / "vs hull" multipliers in the
    /// tuning tables (S18) encode that interaction, so every weapon has a job.
    /// </summary>
    public enum DamageType
    {
        /// <summary>Lasers and beams. Strong against shields, moderate against hull.</summary>
        Energy,

        /// <summary>Railguns and autocannon. Weak against shields, strong against hull.</summary>
        Kinetic,

        /// <summary>Guided missiles/torpedoes. Bypass shields to the hull if not intercepted by point-defence.</summary>
        Missile,
    }
}
