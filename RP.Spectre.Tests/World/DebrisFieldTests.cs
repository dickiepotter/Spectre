namespace RP.Spectre.Tests.World
{
    using System;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Math;
    using RP.Spectre.World;

    /// <summary>
    /// Destruction debris (build brief S8 note): a kill bursts pooled <c>RigidBody</c> chunks that conserve the
    /// ship's momentum and then drift forever (no drag). Conservation is the headline check — the cloud's total
    /// momentum equals the ship's at the instant it died.
    /// </summary>
    [TestClass]
    public sealed class DebrisFieldTests
    {
        [TestMethod]
        public void Spawn_CreatesTheRequestedChunks_EachAShareOfTheMass()
        {
            var field = new DebrisField();
            field.Spawn(Vector3d.Zero, new Vector3d(10, 0, 0), parentMass: 8000, count: 10, scatterSpeed: 40, rng: new Random(1));

            field.Active.Should().HaveCount(10);
            foreach (var chunk in field.Active) chunk.Mass.Should().BeApproximately(800, 1e-9); // 8000 / 10
        }

        [TestMethod]
        public void TotalMomentum_EqualsTheParentShipsMomentum()
        {
            var field = new DebrisField();
            var parentVel = new Vector3d(120, -30, 15);
            const double parentMass = 8000;
            field.Spawn(Vector3d.Zero, parentVel, parentMass, count: 16, scatterSpeed: 80, rng: new Random(7));

            Vector3d expected = parentVel * parentMass;
            field.TotalMomentum().Distance(expected).Should().BeLessThan(1e-6); // zero-mean scatter conserves it
        }

        [TestMethod]
        public void Step_DriftsTheChunks_ButConservesMomentum()
        {
            var field = new DebrisField();
            var parentVel = new Vector3d(50, 0, 0);
            field.Spawn(Vector3d.Zero, parentVel, 8000, count: 8, scatterSpeed: 30, rng: new Random(3));

            Vector3d before = field.TotalMomentum();
            var sample = field.Active[0];
            Vector3d posBefore = sample.Position;

            for (int i = 0; i < 60; i++) field.Step(1.0 / 60.0); // 1 s

            sample.Position.Distance(posBefore).Should().BeGreaterThan(0); // it moved (no drag)
            field.TotalMomentum().Distance(before).Should().BeLessThan(1e-6); // momentum unchanged
        }

        [TestMethod]
        public void Clear_EmptiesTheFieldAndReturnsChunksToThePool()
        {
            var field = new DebrisField();
            field.Spawn(Vector3d.Zero, Vector3d.Zero, 8000, count: 5, scatterSpeed: 20, rng: new Random(2));
            field.Active.Should().NotBeEmpty();

            field.Clear();
            field.Active.Should().BeEmpty();
        }
    }
}
