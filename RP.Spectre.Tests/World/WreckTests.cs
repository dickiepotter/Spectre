namespace RP.Spectre.Tests.World
{
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Game.Scene;
    using RP.Math;
    using RP.Spectre.World;

    /// <summary>
    /// The Tantalus wreck (build brief S13/S14): a capital-scale hulk that tumbles slowly, knows what is
    /// inside it, exposes the hull's motion so a ship can match it, and streams its interior around the player
    /// as they descend — even as the hull rotates beneath them.
    /// </summary>
    [TestClass]
    public sealed class WreckTests
    {
        [TestMethod]
        public void Tantalus_IsCapitalScale_AndTumblesSlowlyButReally()
        {
            var wreck = Wreck.Tantalus();

            // ~3.5 km bow-to-stern along local +Z.
            (wreck.HalfExtents.Z * 2).Should().BeApproximately(3500, 1);

            double degPerSec = wreck.Frame.AngularVelocity.Magnitude * 180.0 / System.Math.PI;
            degPerSec.Should().BeGreaterThan(0);     // it really is tumbling
            degPerSec.Should().BeLessThan(5);        // but slowly — disorienting, not unflyable
        }

        [TestMethod]
        public void Contains_IsTrueInsideTheHull_FalseWellOutside()
        {
            var wreck = Wreck.Tantalus(new Vector3d(10000, 0, 0));

            wreck.Contains(new Vector3d(10000, 0, 0)).Should().BeTrue();           // dead centre
            wreck.Contains(new Vector3d(10000, 0, 0) + new Vector3d(0, 0, 5000)).Should().BeFalse(); // past the bow
        }

        [TestMethod]
        public void HullVelocityAt_ReflectsDriftPlusTumble()
        {
            var wreck = Wreck.Tantalus();

            // At the very centre there is no tumble arm, so the hull velocity is just the drift.
            Vector3d centre = wreck.HullVelocityAt(wreck.Frame.Position);
            centre.Distance(wreck.Frame.Velocity).Should().BeLessThan(1e-9);

            // Far out toward the bow the tumble adds a tangential component, so it differs from pure drift.
            Vector3d bow = wreck.Frame.ToWorld(new Vector3d(0, 0, 1700));
            wreck.HullVelocityAt(bow).Distance(wreck.Frame.Velocity).Should().BeGreaterThan(1.0);
        }

        [TestMethod]
        public void Update_TumblesTheHull_OverTime()
        {
            var wreck = Wreck.Tantalus();
            Quaternion start = wreck.Frame.Orientation;

            const double dt = 1.0 / 60.0;
            for (int i = 0; i < 60 * 10; i++) wreck.Update(wreck.Frame.Position, dt); // 10 s

            // After ten seconds of tumble the orientation has clearly moved.
            wreck.Frame.Orientation.IsIdentity(1e-3).Should().BeFalse();
            (wreck.Frame.Orientation != start).Should().BeTrue();
        }

        [TestMethod]
        public void Update_StreamsInteriorAroundThePlayer_EvenAsTheHullRotates()
        {
            var wreck = Wreck.Tantalus();

            // Enter near the stern.
            Vector3d sternLocal = new Vector3d(0, 0, -1600);
            Vector3d sternWorld = wreck.Frame.ToWorld(sternLocal);
            ChunkResidencyChange entry = wreck.Update(sternWorld, 1.0 / 60.0);
            entry.Loaded.Should().NotBeEmpty();
            ChunkId sternChunk = wreck.Interior.CellOf(sternLocal);
            wreck.Interior.Resident.Should().Contain(sternChunk);

            // Push deep toward the bow over a few seconds; the resident set should follow into new chunks and
            // eventually drop the stern entrance.
            const double dt = 1.0 / 60.0;
            for (int i = 0; i < 60 * 8; i++)
            {
                double z = -1600 + i * (3000.0 / (60 * 8)); // sweep stern -> bow in local Z
                Vector3d world = wreck.Frame.ToWorld(new Vector3d(0, 0, z));
                wreck.Update(world, dt);
            }

            Vector3d bowLocal = new Vector3d(0, 0, 1400);
            wreck.Interior.Resident.Should().Contain(wreck.Interior.CellOf(bowLocal)); // now near the bow
            wreck.Interior.Resident.Should().NotContain(sternChunk);                   // stern left behind
        }
    }
}
