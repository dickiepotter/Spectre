namespace RP.Spectre.Tests.Ships
{
    using System;
    using System.Collections.Generic;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Game.Physics;
    using RP.Math;
    using RP.Spectre.Combat;
    using RP.Spectre.Ships;

    /// <summary>
    /// Headless scenario test (build brief S20): run a 50-ship battle for a stretch of time with no window
    /// and assert it stays sane — no exceptions, no NaN transforms, and a plausible outcome (attrition
    /// actually happens). This is how "a battle of 50+ ships reads as a real engagement" is verified
    /// without a human watching.
    /// </summary>
    [TestClass]
    public sealed class BattleScenarioTests
    {
        private static Combatant BuildFighter(Faction faction, Vector3d position)
        {
            // Interceptor-class stats (S18): light, fragile, fast.
            var body = new RigidBody { Position = position, Mass = 8000, InertiaScalar = 4000 };
            return new Combatant(
                faction, body,
                shields: new FacetShields(capacityPerFacet: 70, regenRate: 25, regenDelay: 4),
                hull: new Hull(120),
                weapon: WeaponCatalog.PulseLaser(),
                capacitor: new Capacitor(200, rechargeRate: 40),
                heat: new HeatSink(maximum: 120, dissipationRate: 30),
                radius: 8);
        }

        private static BattleSimulation BuildBattle(int perSide, int seed)
        {
            var rng = new Random(seed); // seeded for a reproducible scenario
            var ships = new List<Combatant>();

            for (int i = 0; i < perSide; i++)
            {
                double Spread() => (rng.NextDouble() - 0.5) * 1200;
                ships.Add(BuildFighter(Faction.Coalition, new Vector3d(-1000 + Spread(), Spread(), Spread())));
                ships.Add(BuildFighter(Faction.Severance, new Vector3d(1000 + Spread(), Spread(), Spread())));
            }

            return new BattleSimulation(ships);
        }

        [TestMethod]
        public void FiftyShipBattle_RunsHeadlessWithoutNaNsAndProducesAttrition()
        {
            BattleSimulation battle = BuildBattle(perSide: 25, seed: 1234); // 50 ships
            battle.Combatants.Count.Should().Be(50);
            int initialAlive = battle.AliveCount();

            // 40 seconds at 60 Hz.
            const double dt = 1.0 / 60.0;
            for (int step = 0; step < 60 * 40; step++)
            {
                battle.Step(dt);
            }

            // No NaN/Infinity crept into any transform.
            foreach (Combatant ship in battle.Combatants)
            {
                ship.Body.Position.IsNaN().Should().BeFalse();
                double.IsNaN(ship.Body.Velocity.Magnitude).Should().BeFalse();
            }

            // A plausible outcome: ships actually died.
            int finalAlive = battle.AliveCount();
            finalAlive.Should().BeLessThan(initialAlive);
        }

        [TestMethod]
        public void Battle_IsDeterministicForAGivenSeed()
        {
            int RunAndCountSurvivors()
            {
                BattleSimulation battle = BuildBattle(perSide: 25, seed: 99);
                for (int step = 0; step < 60 * 20; step++) battle.Step(1.0 / 60.0);
                return battle.AliveCount();
            }

            RunAndCountSurvivors().Should().Be(RunAndCountSurvivors()); // same seed -> same result
        }
    }
}
