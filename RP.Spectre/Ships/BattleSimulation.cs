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
        private readonly List<Combatant> _combatants;

        /// <summary>Distance within which a ship will open fire (metres).</summary>
        public double WeaponRange { get; set; } = 1500;

        /// <summary>Speed cap (m/s) so fixed-step integration stays well-behaved at scale.</summary>
        public double MaxSpeed { get; set; } = 220;

        /// <summary>Maximum steering force (N) a ship applies to manoeuvre.</summary>
        public double MaxThrust { get; set; } = 600_000;

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
            foreach (Combatant ship in _combatants)
            {
                if (!ship.Alive) continue;

                Combatant? target = NearestEnemy(ship);
                if (target is not null)
                {
                    Vector3d toTarget = target.Body.Position - ship.Body.Position;
                    double distance = toTarget.Magnitude;

                    // Manoeuvre toward a firing position (lead the target).
                    Vector3d steer = Steering.Pursue(
                        ship.Body.Position, ship.Body.Velocity,
                        target.Body.Position, target.Body.Velocity, MaxSpeed, MaxThrust);
                    ship.Body.ApplyForce(steer);

                    // Fire when in range and the gun is ready (heat/capacitor permitting).
                    if (distance <= WeaponRange && ship.Weapon.TryFire(ship.Capacitor, ship.Heat))
                    {
                        DamageRouter.Apply(target.Shield, target.Hull,
                            ship.Weapon.Damage, ship.Weapon.VsShield, ship.Weapon.VsHull, ship.Weapon.DamageType);
                    }
                }

                // Advance per-ship systems and physics.
                ship.Capacitor.Update(dt);
                ship.Heat.Update(dt);
                ship.Weapon.Update(dt);
                ship.Shield.Update(dt);
                ship.Body.Velocity = ship.Body.Velocity.ClampMagnitude(MaxSpeed);
                ship.Body.Integrate(dt);
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
