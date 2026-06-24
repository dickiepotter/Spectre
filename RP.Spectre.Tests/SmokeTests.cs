namespace RP.Spectre.Tests
{
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Game.Core;

    /// <summary>
    /// A first end-to-end check that the layers wire together: Spectre's test project can reach into
    /// RP.Game (and, transitively, RP.Math). Real Spectre gameplay tests replace this as systems land.
    /// </summary>
    [TestClass]
    public sealed class SmokeTests
    {
        [TestMethod]
        public void SpectreCanDriveTheEngineLoop_ProvingTheLayersAreReferenced()
        {
            var acc = new FixedTimestepAccumulator(1.0 / 60.0);

            // Two seconds of sim time, fed as 120 frames of ~1/60 s, should be ~120 steps.
            int steps = 0;
            for (int i = 0; i < 120; i++) steps += acc.Advance(1.0 / 60.0);

            steps.Should().BeInRange(119, 121);
        }
    }
}
