namespace RP.Spectre.Combat
{
    /// <summary>
    /// The weapon tuning table (build brief S18) as factory methods. These values would live in
    /// <c>Spectre/Data</c> JSON for balancing without a recompile; they are gathered here for now, each as a
    /// fresh <see cref="Weapon"/> instance (so cooldowns are independent). The two <c>Prototype…</c> entries
    /// are the player's edge: higher damage, but markedly more heat and capacitor draw (S10.4).
    /// </summary>
    public static class WeaponCatalog
    {
        public static Weapon PulseLaser() => new Weapon
        {
            Family = WeaponFamily.PulseLaser, Damage = 18, FireRate = 5, ProjectileSpeed = double.PositiveInfinity,
            DamageType = DamageType.Energy, VsShield = 1.4, VsHull = 0.8, HeatPerShot = 4, CapacitorCost = 5,
        };

        public static Weapon Beam() => new Weapon
        {
            Family = WeaponFamily.Beam, Damage = 8, FireRate = 5, ProjectileSpeed = double.PositiveInfinity,
            DamageType = DamageType.Energy, VsShield = 1.5, VsHull = 0.7, HeatPerShot = 9, CapacitorCost = 7,
        };

        public static Weapon Railgun() => new Weapon
        {
            Family = WeaponFamily.Railgun, Damage = 120, FireRate = 0.8, ProjectileSpeed = 1500,
            DamageType = DamageType.Kinetic, VsShield = 0.5, VsHull = 1.6, HeatPerShot = 8, CapacitorCost = 10,
            IsBallistic = true, RecoilImpulse = 6000,
        };

        public static Weapon Autocannon() => new Weapon
        {
            Family = WeaponFamily.Autocannon, Damage = 14, FireRate = 10, ProjectileSpeed = 900,
            DamageType = DamageType.Kinetic, VsShield = 0.5, VsHull = 1.4, HeatPerShot = 3, CapacitorCost = 2,
            IsBallistic = true, RecoilImpulse = 700,
        };

        public static Weapon Missile() => new Weapon
        {
            Family = WeaponFamily.Missile, Damage = 250, FireRate = 0.3, ProjectileSpeed = 400,
            DamageType = DamageType.Missile, VsShield = 0, VsHull = 1.5, HeatPerShot = 0, CapacitorCost = 0,
        };

        public static Weapon Torpedo() => new Weapon
        {
            Family = WeaponFamily.Torpedo, Damage = 1200, FireRate = 0.1, ProjectileSpeed = 250,
            DamageType = DamageType.Missile, VsShield = 0, VsHull = 2.0, HeatPerShot = 0, CapacitorCost = 0,
        };

        public static Weapon PointDefence() => new Weapon
        {
            Family = WeaponFamily.PointDefence, Damage = 30, FireRate = 12, ProjectileSpeed = 2000,
            DamageType = DamageType.Kinetic, VsShield = 0.5, VsHull = 0.5, HeatPerShot = 1, CapacitorCost = 1,
        };

        // --- The player's prototype weapons (S10.4/S18): out-damage their class, but run hot and draw hard. ---

        public static Weapon PrototypePulse() => new Weapon
        {
            Family = WeaponFamily.PrototypePulse, Damage = 48, FireRate = 5, ProjectileSpeed = double.PositiveInfinity,
            DamageType = DamageType.Energy, VsShield = 1.6, VsHull = 1.2, HeatPerShot = 9, CapacitorCost = 14,
            IsPrototype = true,
        };

        public static Weapon PrototypeLance() => new Weapon
        {
            Family = WeaponFamily.PrototypeLance, Damage = 220, FireRate = 1.0, ProjectileSpeed = 2000,
            DamageType = DamageType.Kinetic, VsShield = 0.7, VsHull = 2.2, HeatPerShot = 30, CapacitorCost = 40,
            IsBallistic = true, IsPrototype = true, RecoilImpulse = 9000,
        };
    }
}
