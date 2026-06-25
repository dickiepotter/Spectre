namespace RP.Spectre.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Math;
    using RP.Spectre.Audio;
    using RP.Spectre.Combat;
    using RP.Spectre.Hud;
    using RP.Spectre.Missions;
    using RP.Spectre.Ships;

    /// <summary>
    /// Capstone integration (build brief S20): play one campaign beat end to end, headless, exercising the
    /// whole stack at once — the data-driven roster (<see cref="ShipFactory"/>), difficulty-scaled spawns
    /// (<see cref="Encounter"/>), the battle sim, the mission rules, the HUD snapshot and the tension director —
    /// and confirm the beat resolves, the campaign advances, and nothing went NaN. This is the proof the parts
    /// fit, not just that each part works alone.
    /// </summary>
    [TestClass]
    public sealed class PlaythroughTests
    {
        [TestMethod]
        public void OneCampaignBeat_PlaysThroughAllSystemsAndAdvances()
        {
            var scalars = DifficultyScalars.For(DifficultyPreset.Standard);
            var campaign = SpectreCampaign.Build();
            campaign.Current!.Name.Should().Be("Shakedown");

            // Spawn the beat: the player Spectre held back, a Coalition wing forward, and a difficulty-scaled
            // Severance picket. Lopsided so the run is deterministic and the player ward survives.
            var player = ShipFactory.Build(ShipCatalog.Spectre(), new Vector3d(-4000, 0, 0));
            var roster = new List<Combatant> { player };
            for (int i = 0; i < 4; i++)
                roster.Add(ShipFactory.Build(ShipCatalog.CoalitionCorvette(), new Vector3d(-400, (i - 2) * 150, 0)));

            int enemyCount = Encounter.ScaledEnemyCount(2, scalars);
            var enemies = new List<Combatant>();
            for (int i = 0; i < enemyCount; i++)
            {
                var e = ShipFactory.Build(ShipCatalog.SeveranceWasp(), new Vector3d(500, (i - 0.5) * 200, 0));
                enemies.Add(e);
                roster.Add(e);
            }

            var battle = new BattleSimulation(roster);
            var mission = new Mission("Shakedown",
                new IObjective[] { new EliminateFactionObjective(Faction.Severance) },
                wards: new[] { player });
            var tension = new TensionDirector();

            const double dt = 1.0 / 60.0;
            bool sawTension = false;

            for (int step = 0; step < 60 * 90 && mission.State == MissionState.InProgress; step++)
            {
                battle.Step(dt);
                mission.Update(battle, dt);

                // Drive the HUD off the player and the nearest living enemy.
                Combatant? nearest = enemies.Where(e => e.Alive)
                    .OrderBy(e => (e.Body.Position - player.Body.Position).MagnitudeSquared)
                    .FirstOrDefault();
                TargetContact? contact = nearest is null ? null : new TargetContact
                {
                    Position = nearest.Body.Position,
                    Velocity = nearest.Body.Velocity,
                    ShieldFraction = nearest.ShieldFraction,
                    HullFraction = nearest.Hull.Hp / nearest.Hull.MaxHp,
                };

                HudSnapshot hud = HudModel.Build(player.Body, player.Shields[RP.Spectre.Combat.Facet.Fore], player.Hull, player.Heat,
                    player.Capacitor, weaponReady: !player.Heat.IsOverheated, contact);
                double.IsNaN(hud.Speed).Should().BeFalse();

                double range = nearest is null ? double.PositiveInfinity : (nearest.Body.Position - player.Body.Position).Magnitude;
                tension.Update(new ThreatContext
                {
                    NearbyHostiles = battle.AliveCount(Faction.Severance),
                    NearestThreatRange = range,
                    UnderFire = range < 1500,
                }, dt);
                if (tension.Tension > 0) sawTension = true;
            }

            mission.State.Should().Be(MissionState.Succeeded);
            player.Alive.Should().BeTrue();
            battle.AliveCount(Faction.Severance).Should().Be(0);
            sawTension.Should().BeTrue(); // the fight registered on the audio director

            // No transform went non-finite over the whole fight.
            foreach (Combatant c in battle.Combatants) c.Body.Position.IsNaN().Should().BeFalse();

            // The win advances the campaign to the next beat.
            campaign.Record(mission.State);
            campaign.Current!.Name.Should().Be("The Graveyard");
        }
    }
}
