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
        /// <summary>Builds a combatant of the given class at <paramref name="position"/>, optionally re-flagged
        /// to a different faction (e.g. to field a captured hull).</summary>
        public static Combatant Build(ShipClass spec, Vector3d position, Faction? faction = null)
        {
            var body = new RigidBody { Position = position, Mass = spec.Mass, InertiaScalar = spec.Inertia };
            return new Combatant(
                faction ?? spec.Faction,
                body,
                shield: new Shield(spec.ShieldCapacity, spec.ShieldRegen, spec.ShieldRegenDelay),
                hull: new Hull(spec.HullHp),
                weapon: spec.PrimaryWeapon(),
                capacitor: new Capacitor(spec.CapacitorCapacity, spec.CapacitorRecharge),
                heat: new HeatSink(spec.HeatMax, spec.HeatDissipation),
                radius: spec.Radius);
        }
    }
}
