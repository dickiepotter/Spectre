namespace RP.Spectre.Tests.Missions
{
    using System.Collections.Generic;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Game.Physics;
    using RP.Math;
    using RP.Spectre.Combat;
    using RP.Spectre.Missions;
    using RP.Spectre.Ships;

    /// <summary>
    /// The mission/objective layer (build brief S12): objectives resolve against a live battle, and a mission
    /// wins only when every objective is complete, failing if an objective is lost or a ward is destroyed.
    /// Most are driven directly (killing ships by hand) so each rule is checked in isolation; the final test
    /// resolves a mission over a real headless battle to prove the pieces fit together end to end.
    /// </summary>
    [TestClass]
    public sealed class MissionTests
    {
        private static Combatant BuildShip(Faction faction) => BuildShip(faction, Vector3d.Zero);

        private static Combatant BuildShip(Faction faction, Vector3d position)
        {
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

        private static void Kill(Combatant ship) => ship.Hull.TakeDamage(ship.Hull.MaxHp);

        [TestMethod]
        public void EliminateObjective_CompletesOnlyWhenTheFactionIsGone()
        {
            var player = BuildShip(Faction.Coalition);
            var enemyA = BuildShip(Faction.Severance);
            var enemyB = BuildShip(Faction.Severance);
            var battle = new BattleSimulation(new[] { player, enemyA, enemyB });
            var objective = new EliminateFactionObjective(Faction.Severance);

            objective.Update(battle, 0.1);
            objective.Status.Should().Be(ObjectiveStatus.Active); // enemies remain

            Kill(enemyA);
            objective.Update(battle, 0.1);
            objective.Status.Should().Be(ObjectiveStatus.Active); // one still alive

            Kill(enemyB);
            objective.Update(battle, 0.1);
            objective.Status.Should().Be(ObjectiveStatus.Complete);
        }

        [TestMethod]
        public void KeepAliveObjective_CompletesAfterDuration_AndFailsIfSubjectDies()
        {
            var ally = BuildShip(Faction.Coalition);
            var battle = new BattleSimulation(new[] { ally });

            var survive = new KeepAliveObjective(ally, duration: 1.0, "Hold for 1s");
            survive.Update(battle, 0.5);
            survive.Status.Should().Be(ObjectiveStatus.Active);
            survive.Update(battle, 0.6); // crosses the 1.0s mark
            survive.Status.Should().Be(ObjectiveStatus.Complete);

            var protect = new KeepAliveObjective(ally, duration: 1.0, "Hold for 1s");
            Kill(ally);
            protect.Update(battle, 0.1);
            protect.Status.Should().Be(ObjectiveStatus.Failed);
        }

        [TestMethod]
        public void Mission_Succeeds_WhenAllObjectivesComplete()
        {
            var player = BuildShip(Faction.Coalition);
            var enemy = BuildShip(Faction.Severance);
            var battle = new BattleSimulation(new[] { player, enemy });
            var mission = new Mission(
                "Clear the field",
                new IObjective[] { new EliminateFactionObjective(Faction.Severance) },
                wards: new[] { player });

            mission.Update(battle, 0.1);
            mission.State.Should().Be(MissionState.InProgress);

            Kill(enemy);
            mission.Update(battle, 0.1);
            mission.State.Should().Be(MissionState.Succeeded);
        }

        [TestMethod]
        public void Mission_Fails_WhenThePlayerWardDies()
        {
            var player = BuildShip(Faction.Coalition);
            var enemy = BuildShip(Faction.Severance);
            var battle = new BattleSimulation(new[] { player, enemy });
            var mission = new Mission(
                "Clear the field",
                new IObjective[] { new EliminateFactionObjective(Faction.Severance) },
                wards: new[] { player });

            Kill(player);
            mission.Update(battle, 0.1);
            mission.State.Should().Be(MissionState.Failed);
        }

        [TestMethod]
        public void Mission_Fails_WhenAnEscortWardDies_EvenIfEnemiesAreCleared()
        {
            var player = BuildShip(Faction.Coalition);
            var freighter = BuildShip(Faction.Coalition);
            var enemy = BuildShip(Faction.Severance);
            var battle = new BattleSimulation(new[] { player, freighter, enemy });
            var mission = new Mission(
                "Escort the freighter",
                new IObjective[] { new EliminateFactionObjective(Faction.Severance) },
                wards: new[] { player, freighter });

            // The escort is lost in the same instant the last enemy dies — a ward loss outranks the win.
            Kill(enemy);
            Kill(freighter);
            mission.Update(battle, 0.1);
            mission.State.Should().Be(MissionState.Failed);
        }

        [TestMethod]
        public void Mission_StaysResolved_OnceDecided()
        {
            var player = BuildShip(Faction.Coalition);
            var enemy = BuildShip(Faction.Severance);
            var battle = new BattleSimulation(new[] { player, enemy });
            var mission = new Mission(
                "Clear the field",
                new IObjective[] { new EliminateFactionObjective(Faction.Severance) },
                wards: new[] { player });

            Kill(enemy);
            mission.Update(battle, 0.1);
            mission.State.Should().Be(MissionState.Succeeded);

            // A later ward death must not flip a finished mission.
            Kill(player);
            mission.Update(battle, 0.1);
            mission.State.Should().Be(MissionState.Succeeded);
        }

        [TestMethod]
        public void Encounter_ScalesEnemyCountWithDifficulty_AndNeverBelowOne()
        {
            var story = DifficultyScalars.For(DifficultyPreset.Story);
            var standard = DifficultyScalars.For(DifficultyPreset.Standard);
            var hard = DifficultyScalars.For(DifficultyPreset.Hard);

            Encounter.ScaledEnemyCount(10, standard).Should().Be(10);
            Encounter.ScaledEnemyCount(10, story).Should().Be(6);   // 10 * 0.6
            Encounter.ScaledEnemyCount(10, hard).Should().Be(14);   // 10 * 1.4

            Encounter.ScaledEnemyCount(1, story).Should().Be(1);    // 1 * 0.6 -> rounds to 1, floored at 1
        }

        [TestMethod]
        public void Mission_ResolvesOverARealHeadlessBattle()
        {
            // A lopsided clear-the-field: a Coalition wing overwhelms a lone Severance fighter. The player
            // ward sits well back from the engagement, so the enemy focuses a nearer escort and the player
            // survives — the mission should land on Succeeded with no hand-killing of ships.
            var player = BuildShip(Faction.Coalition, new Vector3d(-4000, 0, 0));
            var wingmen = new[]
            {
                BuildShip(Faction.Coalition, new Vector3d(-400, 0, 0)),
                BuildShip(Faction.Coalition, new Vector3d(-400, 200, 0)),
                BuildShip(Faction.Coalition, new Vector3d(-400, -200, 0)),
                BuildShip(Faction.Coalition, new Vector3d(-600, 0, 200)),
            };
            var enemy = BuildShip(Faction.Severance, new Vector3d(400, 0, 0));

            var roster = new List<Combatant> { player, enemy };
            roster.AddRange(wingmen);
            var battle = new BattleSimulation(roster);
            var mission = new Mission(
                "Clear the field",
                new IObjective[] { new EliminateFactionObjective(Faction.Severance) },
                wards: new[] { player });

            const double dt = 1.0 / 60.0;
            for (int step = 0; step < 60 * 30 && mission.State == MissionState.InProgress; step++)
            {
                battle.Step(dt);
                mission.Update(battle, dt);
            }

            mission.State.Should().Be(MissionState.Succeeded);
            player.Alive.Should().BeTrue();
            battle.AliveCount(Faction.Severance).Should().Be(0);
        }
    }
}
