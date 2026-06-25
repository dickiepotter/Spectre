namespace RP.Spectre.Tests.Missions
{
    using System.Linq;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Spectre.Missions;
    using RP.Spectre.State;

    /// <summary>
    /// The campaign spine (build brief S12/S21.2): an ordered run of beats with a cursor that advances only on
    /// a win, finishes after the last beat, and round-trips through the save schema's progress field.
    /// </summary>
    [TestClass]
    public sealed class CampaignTests
    {
        private static Campaign TwoBeat() => new(new[]
        {
            new MissionBrief { Name = "One" },
            new MissionBrief { Name = "Two" },
        });

        [TestMethod]
        public void NewCampaign_StartsOnTheFirstBeat()
        {
            var c = TwoBeat();
            c.Progress.Should().Be(0);
            c.IsComplete.Should().BeFalse();
            c.Current!.Name.Should().Be("One");
        }

        [TestMethod]
        public void Winning_AdvancesToTheNextBeat()
        {
            var c = TwoBeat();
            c.Record(MissionState.Succeeded);
            c.Current!.Name.Should().Be("Two");
        }

        [TestMethod]
        public void LosingOrNotFinishing_ReplaysTheSameBeat()
        {
            var c = TwoBeat();
            c.Record(MissionState.Failed);
            c.Current!.Name.Should().Be("One");
            c.Record(MissionState.InProgress);
            c.Current!.Name.Should().Be("One");
        }

        [TestMethod]
        public void WinningTheLastBeat_CompletesTheCampaign()
        {
            var c = TwoBeat();
            c.Record(MissionState.Succeeded);
            c.Record(MissionState.Succeeded);

            c.IsComplete.Should().BeTrue();
            c.Current.Should().BeNull();
            c.Record(MissionState.Succeeded); // past the end is a no-op
            c.Progress.Should().Be(2);
        }

        [TestMethod]
        public void Progress_RoundTripsThroughTheSaveSchema()
        {
            var save = new SpectreSaveData();
            var c = SpectreCampaign.Build();
            c.Record(MissionState.Succeeded);
            c.Record(MissionState.Succeeded); // now on beat index 2

            save.MissionProgress = c.Progress;
            var resumed = SpectreCampaign.Build(save.MissionProgress);

            resumed.Progress.Should().Be(2);
            resumed.Current!.Name.Should().Be("Severance Ambush");
        }

        [TestMethod]
        public void TheStorySpine_RunsFleetCombatIntoTheDescent()
        {
            var c = SpectreCampaign.Build();

            c.Missions.Should().HaveCountGreaterThan(3);
            c.Missions.First().IsDescent.Should().BeFalse();  // opens in open space
            c.Missions.Last().IsDescent.Should().BeTrue();    // ends inside the wreck
            c.Missions.Any(m => m.IsDescent).Should().BeTrue();
        }

        [TestMethod]
        public void ResumeCursor_IsClampedToRange()
        {
            new Campaign(new[] { new MissionBrief { Name = "Only" } }, progress: 99).IsComplete.Should().BeTrue();
            new Campaign(new[] { new MissionBrief { Name = "Only" } }, progress: -5).Progress.Should().Be(0);
        }
    }
}
