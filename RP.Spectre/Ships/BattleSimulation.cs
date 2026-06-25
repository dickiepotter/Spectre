namespace RP.Spectre.Ships
{
    using System.Collections.Generic;
    using System.Linq;
    using RP.Game.Ai;
    using RP.Spectre.Combat;
    using RP.Math;

    /// <summary>
    /// A headless battle: it steps a set of <see cref="Combatant"/>s through target selection, steering and
    /// firing, with no rendering. This is both the heart of a live engagement and the thing the scenario
    /// tests exercise — run 50 ships for a few seconds and check nothing blew up (NaNs, exceptions) and the
    /// outcome is plausible (build brief S7, S12, S20).
    /// </summary>
    /// <remarks>
    /// Each living ship picks the nearest enemy, <see cref="Steering.Pursue"/>s it (so it leads a moving
    /// target), and — once inside weapon range — fires, routing damage through the target's shield and
    /// hull. Speed is capped so the fixed-step integration stays stable. It is deliberately simple AI; the
    /// point of this phase is that it runs at scale and reads as a fight, not that it is a grandmaster.
    /// </remarks>
    public sealed class BattleSimulation
    {
        // The hull mesh's frame: +X right, +Y up, forward is -Z (so +Z is NEAR the viewer). Ships are turned
        // to face their heading in this convention so the renderer's per-instance rotation reads as flight.
        private static readonly OrthogonalAxes ShipAxes =
            new OrthogonalAxes(AxisAlignment.RIGHT, AxisAlignment.UP, AxisAlignment.NEAR);

        private readonly List<Combatant> _combatants;
        private readonly List<Vector3d> _positionScratch = new List<Vector3d>();

        /// <summary>Distance within which a ship will open fire (metres).</summary>
        public double WeaponRange { get; set; } = 1500;

        /// <summary>Speed cap (m/s) so fixed-step integration stays well-behaved at scale.</summary>
        public double MaxSpeed { get; set; } = 220;

        /// <summary>Maximum steering force (N) a ship applies to manoeuvre.</summary>
        public double MaxThrust { get; set; } = 600_000;

        /// <summary>Neighbour distance (m) inside which ships push apart, so packs spread into a brawl instead
        /// of collapsing to a point — the difference between a furball and a static clump.</summary>
        public double SeparationRadius { get; set; } = 220;

        public BattleSimulation(IEnumerable<Combatant> combatants)
        {
            _combatants = combatants.ToList();
        }

        public IReadOnlyList<Combatant> Combatants => _combatants;

        public int AliveCount(Faction faction) => _combatants.Count(c => c.Faction == faction && c.Alive);

        public int AliveCount() => _combatants.Count(c => c.Alive);

        /// <summary>Advances the whole battle by <paramref name="dt"/> seconds.</summary>
        public void Step(double dt)
        {
            // Snapshot live positions once for the separation rule (cheap O(n) gather, O(n^2) test).
            _positionScratch.Clear();
            foreach (Combatant c in _combatants)
            {
                if (c.Alive) _positionScratch.Add(c.Body.Position);
            }

            foreach (Combatant ship in _combatants)
            {
                if (!ship.Alive) continue;

                Combatant? target = NearestEnemy(ship);
                if (target is not null)
                {
                    Vector3d toTarget = target.Body.Position - ship.Body.Position;
                    double distance = toTarget.Magnitude;

                    // Manoeuvre toward a firing position (lead the target)... The steering behaviours return a
                    // velocity-error that reads as an *acceleration* intent (magnitude ~MaxSpeed), so it must be
                    // multiplied by mass to become a force — otherwise a 60-tonne hull barely twitches and the
                    // fleets never close. The resulting force is clamped to the ship's MaxThrust, which is what
                    // makes light interceptors whip around and capitals turn like fortresses.
                    Vector3d accel = Steering.Pursue(
                        ship.Body.Position, ship.Body.Velocity,
                        target.Body.Position, target.Body.Velocity, MaxSpeed, double.MaxValue);

                    // ...and shove away from whoever is too close, so the fight spreads out and swirls.
                    accel += Steering.Separation(ship.Body.Position, _positionScratch, SeparationRadius, MaxSpeed);
                    ship.Body.ApplyForce((accel * ship.Body.Mass).ClampMagnitude(MaxThrust));

                    // Fire when in range and the gun is ready (heat/capacitor permitting).
                    if (distance <= WeaponRange && ship.Weapon.TryFire(ship.Capacitor, ship.Heat))
                    {
                        // The hit lands on the facet of the target facing the shooter.
                        DamageRouter.Apply(target.ShieldForHitFrom(ship.Body.Position), target.Hull,
                            ship.Weapon.Damage, ship.Weapon.VsShield, ship.Weapon.VsHull, ship.Weapon.DamageType);
                    }
                }

                // Advance per-ship systems and physics.
                ship.Capacitor.Update(dt);
                ship.Heat.Update(dt);
                ship.Weapon.Update(dt);
                ship.Shields.Update(dt);
                ship.Body.Velocity = ship.Body.Velocity.ClampMagnitude(MaxSpeed);
                ship.Body.Integrate(dt);

                // Turn the hull to face where it's going (or, when nearly stationary, toward its prey), so the
                // renderer draws a banking dogfight instead of a frozen formation.
                FaceHeading(ship, target);
            }
        }

        // Orient the body to look along its heading. Velocity wins while moving; a near-stationary ship aims at
        // its target instead, so noses never snap to a default pose.
        private static void FaceHeading(Combatant ship, Combatant? target)
        {
            Vector3d forward = ship.Body.Velocity;
            if (forward.MagnitudeSquared < 25.0) // < 5 m/s: heading is ill-defined, aim at the enemy
            {
                forward = target is not null ? target.Body.Position - ship.Body.Position : ship.Body.Forward;
            }

            if (forward.MagnitudeSquared > 1e-6)
            {
                ship.Body.Orientation = Quaternion.LookRotation(forward, ShipAxes);
            }
        }

        private Combatant? NearestEnemy(Combatant ship)
        {
            Combatant? nearest = null;
            double nearestSq = double.PositiveInfinity;
            foreach (Combatant other in _combatants)
            {
                if (!other.Alive || other.Faction == ship.Faction) continue;
                double dSq = (other.Body.Position - ship.Body.Position).MagnitudeSquared;
                if (dSq < nearestSq)
                {
                    nearestSq = dSq;
                    nearest = other;
                }
            }

            return nearest;
        }
    }
}
