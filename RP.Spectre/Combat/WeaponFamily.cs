namespace RP.Spectre.Combat
{
    /// <summary>
    /// The weapon families (build brief S8.3). Each has a distinct, readable role so every weapon is worth
    /// carrying — energy strips shields, kinetic punches hull, missiles bypass to hull unless intercepted,
    /// point-defence shoots down missiles and small debris, torpedoes overwhelm capital facets, and the
    /// player's two <c>Prototype…</c> guns out-damage their class (S10.4) at the cost of heat and capacitor.
    /// </summary>
    public enum WeaponFamily
    {
        PulseLaser,
        Beam,
        Railgun,
        Autocannon,
        Missile,
        PointDefence,
        Torpedo,
        PrototypePulse,
        PrototypeLance,
    }
}
