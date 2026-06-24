namespace RP.Spectre.Combat
{
    /// <summary>
    /// One weapon: its immutable stats plus the per-instance fire-rate cooldown. Firing is gated on three
    /// things together — the cooldown (rate of fire), the <see cref="HeatSink"/> (not overheated) and the
    /// <see cref="Capacitor"/> (enough charge). That triple gate is what turns raw damage numbers into the
    /// fire-discipline gameplay of build brief S8.3.
    /// </summary>
    public sealed class Weapon
    {
        public WeaponFamily Family { get; init; }

        /// <summary>Damage per shot (or per tick for continuous weapons).</summary>
        public double Damage { get; init; }

        /// <summary>Shots per second.</summary>
        public double FireRate { get; init; } = 1;

        /// <summary>Projectile speed in m/s; <see cref="double.PositiveInfinity"/> for hitscan.</summary>
        public double ProjectileSpeed { get; init; } = double.PositiveInfinity;

        public double HeatPerShot { get; init; }
        public double CapacitorCost { get; init; }

        /// <summary>Ballistic weapons have travel time and apply <see cref="RecoilImpulse"/> when fired.</summary>
        public bool IsBallistic { get; init; }

        /// <summary>The player's deliberate over-class edge (build brief S10.4): explicit, auditable, tunable.</summary>
        public bool IsPrototype { get; init; }

        public DamageType DamageType { get; init; } = DamageType.Energy;

        /// <summary>Damage multiplier vs shields (S18).</summary>
        public double VsShield { get; init; } = 1;

        /// <summary>Damage multiplier vs hull (S18).</summary>
        public double VsHull { get; init; } = 1;

        /// <summary>Recoil impulse (N·s) applied opposite the firing direction when a ballistic weapon fires.</summary>
        public double RecoilImpulse { get; init; }

        private double _cooldown;

        /// <summary>True once the fire-rate cooldown has elapsed.</summary>
        public bool ReadyToCycle => _cooldown <= 0;

        /// <summary>Can this weapon fire right now (rate, heat, and charge all permitting)?</summary>
        public bool CanFire(Capacitor capacitor, HeatSink heat) =>
            _cooldown <= 0 && heat.CanFire && capacitor.Current >= CapacitorCost;

        /// <summary>
        /// Attempts to fire: if allowed, spends capacitor charge, adds heat, starts the cooldown, and
        /// returns true. Otherwise does nothing and returns false (the gun "stalls").
        /// </summary>
        public bool TryFire(Capacitor capacitor, HeatSink heat)
        {
            if (!CanFire(capacitor, heat)) return false;

            capacitor.TryDraw(CapacitorCost);
            heat.Add(HeatPerShot);
            _cooldown = FireRate > 0 ? 1.0 / FireRate : 0;
            return true;
        }

        /// <summary>Advances the fire-rate cooldown.</summary>
        public void Update(double dt)
        {
            if (_cooldown > 0) _cooldown -= dt;
        }
    }
}
