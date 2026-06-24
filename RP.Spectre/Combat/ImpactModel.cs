namespace RP.Spectre.Combat
{
    using RP.Game.Physics;

    /// <summary>
    /// Spectre's crash-damage rule: it turns the energy of a collision into hull damage. Physics, not just
    /// weapons, kills ships here (build brief S7) — flying full-boost into a tumbling hull plate should be
    /// lethal, while drifting gently into debris should only nudge you.
    /// </summary>
    /// <remarks>
    /// The curve has a <b>soft threshold</b>: below it (gentle bumps, slow docking) there is no damage at
    /// all, just the physical nudge the impulse already applied. Above it, damage rises linearly with the
    /// excess energy. The constants are this title's tuning (they would live in <c>Spectre/Data</c> JSON;
    /// constants here for now) and are set so a deliberate high-speed ram between fighters is mutually
    /// destructive, matching the brief's "hull death is fast".
    /// </remarks>
    public static class ImpactModel
    {
        /// <summary>Impacts below this energy (joules) do no damage — only a nudge.</summary>
        public const double SoftThresholdJoules = 500_000.0;

        /// <summary>Hull damage per joule of energy above the threshold.</summary>
        public const double DamagePerJoule = 3.0e-5;

        /// <summary>Hull damage from a given impact energy (joules). Zero below the soft threshold.</summary>
        public static double HullDamage(double impactEnergyJoules)
        {
            if (impactEnergyJoules <= SoftThresholdJoules) return 0.0;
            return (impactEnergyJoules - SoftThresholdJoules) * DamagePerJoule;
        }

        /// <summary>Convenience: the hull damage two colliding bodies would inflict, from their relative
        /// motion and masses (uses the engine's reduced-mass impact energy).</summary>
        public static double HullDamage(RigidBody a, RigidBody b) =>
            HullDamage(CollisionResolver.ImpactEnergy(a, b));
    }
}
