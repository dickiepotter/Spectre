namespace RP.Spectre.Ships
{
    using RP.Game.Physics;
    using RP.Math;
    using RP.Spectre.Combat;

    /// <summary>
    /// Turns a <see cref="ShipClass"/> stat block into a live <see cref="Combatant"/> at a position (build
    /// brief S18). This is the one place data becomes a ship, so spawning a battle is "pick classes, place
    /// them" rather than hand-assembling shields/hulls/weapons each time.
    /// </summary>
    public static class ShipFactory
    {
        /// <summary>Each of the six shield facets holds this fraction of the class's rated shield capacity.</summary>
        public const double FacetCapacityFactor = 0.55;

        /// <summary>Per-facet regen as a fraction of the class's rated regen.</summary>
        public const double FacetRegenFactor = 0.7;

        /// <summary>Builds a combatant of the given class at <paramref name="position"/>, optionally re-flagged
        /// to a different faction (e.g. to field a captured hull).</summary>
        public static Combatant Build(ShipClass spec, Vector3d position, Faction? faction = null)
        {
            var body = new RigidBody { Position = position, Mass = spec.Mass, InertiaScalar = spec.Inertia };
            // Split the shield into six directional facets. Each facet holds a little over half the class's
            // rated strength, so a side fired on from one angle falls quickly (focus-fire), while damage spread
            // around the hull in a swirling furball is absorbed and regenerated facet-by-facet.
            return new Combatant(
                faction ?? spec.Faction,
                body,
                shields: new FacetShields(spec.ShieldCapacity * FacetCapacityFactor, spec.ShieldRegen * FacetRegenFactor, spec.ShieldRegenDelay),
                hull: new Hull(spec.HullHp),
                weapon: spec.PrimaryWeapon(),
                capacitor: new Capacitor(spec.CapacitorCapacity, spec.CapacitorRecharge),
                heat: new HeatSink(spec.HeatMax, spec.HeatDissipation),
                radius: spec.Radius,
                name: spec.Name);
        }
    }
}
