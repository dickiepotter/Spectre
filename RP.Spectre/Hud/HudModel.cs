namespace RP.Spectre.Hud
{
    using RP.Game.Physics;
    using RP.Math;
    using RP.Spectre.Combat;

    /// <summary>
    /// A target's readout on the HUD: how far, how fast it is closing, how hurt it is, and where to put the
    /// crosshair to hit it (build brief S8.3/S14). All in world space; the renderer projects it to the screen.
    /// </summary>
    public readonly struct TargetReadout
    {
        public double Range { get; init; }

        /// <summary>Unit direction from the player to the target (world).</summary>
        public Vector3d Direction { get; init; }

        /// <summary>Range rate as a closing speed: positive means the gap is shrinking (m/s).</summary>
        public double ClosingSpeed { get; init; }

        public double ShieldFraction { get; init; }
        public double HullFraction { get; init; }

        /// <summary>True if a ballistic lead solution exists (the target can be hit).</summary>
        public bool HasLeadSolution { get; init; }

        /// <summary>World point to aim at for an intercept — the lead pip. Only meaningful if <see cref="HasLeadSolution"/>.</summary>
        public Vector3d AimPoint { get; init; }
    }

    /// <summary>
    /// One frame's worth of flight + combat HUD data, computed from the ship's live state (build brief
    /// S14/S3 deferral: the prograde marker and the readouts). It is a pure snapshot — no rendering — so it is
    /// fully testable and the Vulkan/text layer just draws what it says.
    /// </summary>
    public readonly struct HudSnapshot
    {
        public double Speed { get; init; }

        /// <summary>The velocity vector (world). The prograde marker points along this.</summary>
        public Vector3d Velocity { get; init; }

        /// <summary>Unit velocity direction, or zero when at rest (no prograde marker).</summary>
        public Vector3d Prograde { get; init; }

        /// <summary>Where the nose points (world) — the boresight/gun line.</summary>
        public Vector3d Boresight { get; init; }

        public double ShieldFraction { get; init; }
        public double HullFraction { get; init; }
        public double HeatFraction { get; init; }
        public double CapacitorFraction { get; init; }
        public bool Overheated { get; init; }
        public bool WeaponReady { get; init; }

        /// <summary>The selected target's readout, if one is selected.</summary>
        public TargetReadout? Target { get; init; }
    }

    /// <summary>
    /// Builds the <see cref="HudSnapshot"/> from the ship's systems each frame. Kept separate from both the
    /// flight model and the renderer: flight produces the state, this interprets it for the pilot, and the UI
    /// layer draws it.
    /// </summary>
    public static class HudModel
    {
        private static double Fraction(double current, double max) => max <= 0 ? 0 : current / max;

        /// <summary>Builds the flight/combat HUD for the player ship, with no target selected.</summary>
        public static HudSnapshot Build(RigidBody self, Shield shield, Hull hull, HeatSink heat, Capacitor capacitor, bool weaponReady)
            => Build(self, shield, hull, heat, capacitor, weaponReady, target: null);

        /// <summary>
        /// Builds the HUD for the player ship. If <paramref name="target"/> is supplied, also fills the
        /// <see cref="HudSnapshot.Target"/> readout — range, closing speed, the target's condition and the lead
        /// pip for <paramref name="weaponProjectileSpeed"/>.
        /// </summary>
        public static HudSnapshot Build(
            RigidBody self, Shield shield, Hull hull, HeatSink heat, Capacitor capacitor, bool weaponReady,
            TargetContact? target, double weaponProjectileSpeed = double.PositiveInfinity)
        {
            double speed = self.Velocity.Magnitude;

            return new HudSnapshot
            {
                Speed = speed,
                Velocity = self.Velocity,
                Prograde = speed > 1e-9 ? self.Velocity / speed : Vector3d.Zero,
                Boresight = self.Forward,
                ShieldFraction = Fraction(shield.Current, shield.Capacity),
                HullFraction = Fraction(hull.Hp, hull.MaxHp),
                HeatFraction = heat.Fraction,
                CapacitorFraction = capacitor.Fraction,
                Overheated = heat.IsOverheated,
                WeaponReady = weaponReady,
                Target = target is { } t ? Track(self, t, weaponProjectileSpeed) : null,
            };
        }

        private static TargetReadout Track(RigidBody self, TargetContact target, double weaponProjectileSpeed)
        {
            Vector3d offset = target.Position - self.Position;
            double range = offset.Magnitude;
            Vector3d dir = range > 1e-9 ? offset / range : Vector3d.Zero;

            // Range rate from the relative velocity; positive closing speed = the gap is shrinking.
            Vector3d relative = self.Velocity - target.Velocity;
            double closing = relative.DotProduct(dir);

            // Lead the shot using the target's motion relative to us (so our own velocity is accounted for).
            Vector3d? lead = InterceptSolver.LeadPoint(self.Position, target.Position, target.Velocity - self.Velocity, weaponProjectileSpeed);

            return new TargetReadout
            {
                Range = range,
                Direction = dir,
                ClosingSpeed = closing,
                ShieldFraction = target.ShieldFraction,
                HullFraction = target.HullFraction,
                HasLeadSolution = lead is not null,
                AimPoint = lead ?? target.Position,
            };
        }
    }

    /// <summary>The minimal target state the HUD needs — decoupled from <c>Combatant</c> so the HUD can mark
    /// any contact (ship, debris, waypoint).</summary>
    public readonly struct TargetContact
    {
        public Vector3d Position { get; init; }
        public Vector3d Velocity { get; init; }
        public double ShieldFraction { get; init; }
        public double HullFraction { get; init; }
    }
}
