namespace RP.Spectre.Tests.Audio
{
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Spectre.Audio;

    /// <summary>
    /// The adaptive-audio director (build brief S15.4): tension rises fast with threat and decays slowly so
    /// dread lingers, the audio cue tracks tension through a hysteresis band (and fire forces Combat), and the
    /// music gain scales with both tension and the player's volume setting.
    /// </summary>
    [TestClass]
    public sealed class TensionDirectorTests
    {
        private static ThreatContext Calm => new() { NearestThreatRange = double.PositiveInfinity };

        private static void Hold(TensionDirector d, ThreatContext ctx, double seconds)
        {
            const double dt = 1.0 / 60.0;
            for (double t = 0; t < seconds; t += dt) d.Update(ctx, dt);
        }

        [TestMethod]
        public void StartsCalmAndSilentish()
        {
            var d = new TensionDirector();
            d.Tension.Should().Be(0);
            d.Cue.Should().Be(AudioCue.Calm);
        }

        [TestMethod]
        public void TensionRisesUnderFire_AndCueGoesToCombat()
        {
            var d = new TensionDirector();
            Hold(d, new ThreatContext { UnderFire = true, NearbyHostiles = 2, NearestThreatRange = 200 }, 2.0);

            d.Tension.Should().BeGreaterThan(0.8);
            d.Cue.Should().Be(AudioCue.Combat);
        }

        [TestMethod]
        public void DreadLingers_TensionDecaysSlowerThanItRose()
        {
            var d = new TensionDirector();
            var hot = new ThreatContext { UnderFire = true, NearbyHostiles = 3, NearestThreatRange = 150 };

            // Rise to a high tension quickly...
            Hold(d, hot, 1.0);
            double afterRise = d.Tension;
            afterRise.Should().BeGreaterThan(0.6);

            // ...then go fully calm for the same duration; it should still be elevated (slow decay).
            Hold(d, Calm, 1.0);
            d.Tension.Should().BeGreaterThan(0.3);   // hasn't dropped to nothing
            d.Tension.Should().BeLessThan(afterRise); // but is falling
        }

        [TestMethod]
        public void InsideWreck_RaisesABaselineUnease_EvenWithNoEnemies()
        {
            var d = new TensionDirector();
            Hold(d, new ThreatContext { InsideWreck = true, NearestThreatRange = double.PositiveInfinity }, 2.0);

            d.Tension.Should().BeGreaterThan(0);
            d.Cue.Should().Be(AudioCue.Unease);
        }

        [TestMethod]
        public void ACloseStalker_NotYetFiring_ReachesStalkNotCombat()
        {
            var d = new TensionDirector();
            Hold(d, new ThreatContext { InsideWreck = true, NearbyHostiles = 1, NearestThreatRange = 250, UnderFire = false }, 3.0);

            d.Cue.Should().Be(AudioCue.Stalk);
        }

        [TestMethod]
        public void MusicGain_ScalesWithTensionAndVolumeSetting()
        {
            var d = new TensionDirector();

            float quiet = d.MusicGain(1f); // tension 0 -> the bed level
            Hold(d, new ThreatContext { UnderFire = true, NearbyHostiles = 3, NearestThreatRange = 100 }, 2.0);
            float loud = d.MusicGain(1f);
            loud.Should().BeGreaterThan(quiet);

            // The player's music-volume setting scales the whole thing.
            d.MusicGain(0.5f).Should().BeApproximately(loud * 0.5f, 1e-4f);
        }
    }
}
