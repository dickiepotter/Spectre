namespace RP.Spectre.Ships
{
    using RP.Spectre.Combat;

    /// <summary>
    /// The ship roster as data (build brief S10/S18): the Earth Coalition's line (corvette up to a fleet
    /// carrier), the Severance breakaway's insect-named hulls (the <i>Wasp</i> up to the <i>Hive</i>), and the
    /// player's prototype <i>Spectre</i>. Stats scale monotonically with <see cref="HullClass"/> — bigger
    /// hulls are tankier and slower — so a fight reads as a clear pecking order. Balancing is a data edit here.
    /// </summary>
    public static class ShipCatalog
    {
        // ---- Earth Coalition (friendly) ----

        public static ShipClass CoalitionCorvette() => new()
        {
            Name = "Lance corvette", Faction = Faction.Coalition, Class = HullClass.Corvette,
            HullHp = 260, ShieldCapacity = 350, ShieldRegen = 30, Mass = 60_000, Inertia = 40_000,
            Radius = 18, MaxSpeed = 160, CapacitorCapacity = 300, CapacitorRecharge = 50,
            HeatMax = 160, HeatDissipation = 40, PrimaryWeapon = WeaponCatalog.Autocannon, Hardpoints = 2,
        };

        public static ShipClass CoalitionFrigate() => new()
        {
            Name = "Warden frigate", Faction = Faction.Coalition, Class = HullClass.Frigate,
            HullHp = 900, ShieldCapacity = 1200, ShieldRegen = 60, Mass = 400_000, Inertia = 320_000,
            Radius = 45, MaxSpeed = 110, CapacitorCapacity = 800, CapacitorRecharge = 90,
            HeatMax = 320, HeatDissipation = 70, PrimaryWeapon = WeaponCatalog.Railgun, Hardpoints = 4,
        };

        public static ShipClass CoalitionCruiser() => new()
        {
            Name = "Sentinel cruiser", Faction = Faction.Coalition, Class = HullClass.Cruiser,
            HullHp = 3200, ShieldCapacity = 4000, ShieldRegen = 120, Mass = 2_000_000, Inertia = 1_600_000,
            Radius = 90, MaxSpeed = 80, CapacitorCapacity = 2000, CapacitorRecharge = 180,
            HeatMax = 700, HeatDissipation = 140, PrimaryWeapon = WeaponCatalog.Railgun, Hardpoints = 8,
        };

        public static ShipClass CoalitionCarrier() => new()
        {
            Name = "Bastion carrier", Faction = Faction.Coalition, Class = HullClass.Carrier,
            HullHp = 9000, ShieldCapacity = 9000, ShieldRegen = 220, Mass = 9_000_000, Inertia = 7_000_000,
            Radius = 180, MaxSpeed = 55, CapacitorCapacity = 5000, CapacitorRecharge = 400,
            HeatMax = 1400, HeatDissipation = 280, PrimaryWeapon = WeaponCatalog.Torpedo, Hardpoints = 12,
        };

        // ---- Severance breakaway (hostile) ----

        public static ShipClass SeveranceWasp() => new()
        {
            Name = "Wasp interceptor", Faction = Faction.Severance, Class = HullClass.Interceptor,
            HullHp = 120, ShieldCapacity = 200, ShieldRegen = 25, Mass = 8_000, Inertia = 4_000,
            Radius = 8, MaxSpeed = 220, CapacitorCapacity = 200, CapacitorRecharge = 40,
            HeatMax = 120, HeatDissipation = 30, PrimaryWeapon = WeaponCatalog.PulseLaser, Hardpoints = 1,
        };

        public static ShipClass SeveranceHornet() => new()
        {
            Name = "Hornet gunship", Faction = Faction.Severance, Class = HullClass.Corvette,
            HullHp = 300, ShieldCapacity = 320, ShieldRegen = 28, Mass = 70_000, Inertia = 45_000,
            Radius = 20, MaxSpeed = 150, CapacitorCapacity = 320, CapacitorRecharge = 48,
            HeatMax = 170, HeatDissipation = 42, PrimaryWeapon = WeaponCatalog.Autocannon, Hardpoints = 2,
        };

        public static ShipClass SeveranceLocust() => new()
        {
            Name = "Locust cruiser", Faction = Faction.Severance, Class = HullClass.Cruiser,
            HullHp = 3000, ShieldCapacity = 3600, ShieldRegen = 110, Mass = 1_900_000, Inertia = 1_500_000,
            Radius = 88, MaxSpeed = 82, CapacitorCapacity = 1900, CapacitorRecharge = 170,
            HeatMax = 680, HeatDissipation = 135, PrimaryWeapon = WeaponCatalog.Railgun, Hardpoints = 8,
        };

        public static ShipClass SeveranceHive() => new()
        {
            Name = "Hive carrier", Faction = Faction.Severance, Class = HullClass.Carrier,
            HullHp = 9500, ShieldCapacity = 8500, ShieldRegen = 210, Mass = 9_500_000, Inertia = 7_400_000,
            Radius = 190, MaxSpeed = 52, CapacitorCapacity = 5200, CapacitorRecharge = 420,
            HeatMax = 1500, HeatDissipation = 300, PrimaryWeapon = WeaponCatalog.Torpedo, Hardpoints = 14,
        };

        // ---- The player ----

        /// <summary>
        /// The <i>Spectre</i>: a light, strongly over-thrust prototype carrying over-class weapons (S10.4). Its
        /// edge is a constant the difficulty dials never touch — see <c>Missions.DifficultyScalars</c>.
        /// </summary>
        public static ShipClass Spectre() => new()
        {
            Name = "Spectre prototype", Faction = Faction.Coalition, Class = HullClass.Interceptor,
            HullHp = 180, ShieldCapacity = 300, ShieldRegen = 30, ShieldRegenDelay = 3.5,
            Mass = 11_000, Inertia = 5_000, Radius = 9, MaxSpeed = 260,
            CapacitorCapacity = 320, CapacitorRecharge = 70, HeatMax = 200, HeatDissipation = 55,
            PrimaryWeapon = WeaponCatalog.PrototypePulse, Hardpoints = 2,
        };
    }
}
