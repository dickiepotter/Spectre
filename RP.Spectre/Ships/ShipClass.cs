namespace RP.Spectre.Ships
{
    using System;
    using RP.Spectre.Combat;

    /// <summary>Hull size tiers, smallest to largest (build brief S10/S18). Drives feel: darts to fortresses.</summary>
    public enum HullClass
    {
        Interceptor,
        Corvette,
        Frigate,
        Cruiser,
        Carrier,
    }

    /// <summary>
    /// One ship class as <b>data</b> (build brief S18): the stat block that, run through <see cref="ShipFactory"/>,
    /// becomes a live <see cref="Combatant"/>. These would live in <c>Spectre/Data</c> JSON for balancing
    /// without a recompile; <see cref="ShipCatalog"/> gathers the roster here for now. Init-only properties so
    /// a class reads as a flat table entry, like <see cref="WeaponCatalog"/>.
    /// </summary>
    public sealed class ShipClass
    {
        public string Name { get; init; } = "";
        public Faction Faction { get; init; }
        public HullClass Class { get; init; }

        public double HullHp { get; init; }
        public double ShieldCapacity { get; init; }
        public double ShieldRegen { get; init; }
        public double ShieldRegenDelay { get; init; } = 4;

        public double Mass { get; init; }
        public double Inertia { get; init; }
        public double Radius { get; init; }

        /// <summary>Top speed the AI will drive this class to (m/s) — fighters fast, capitals ponderous.</summary>
        public double MaxSpeed { get; init; }

        public double CapacitorCapacity { get; init; }
        public double CapacitorRecharge { get; init; }
        public double HeatMax { get; init; }
        public double HeatDissipation { get; init; }

        /// <summary>Factory for this class's primary weapon (fresh instance per ship for independent cooldowns).</summary>
        public Func<Weapon> PrimaryWeapon { get; init; } = WeaponCatalog.PulseLaser;

        /// <summary>Number of weapon batteries. Fighters mount one; capitals mount many (their broadsides).</summary>
        public int Hardpoints { get; init; } = 1;
    }
}
