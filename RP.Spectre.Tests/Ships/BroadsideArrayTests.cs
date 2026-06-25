namespace RP.Spectre.Tests.Ships
{
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Math;
    using RP.Spectre.Ships;

    /// <summary>
    /// Capital broadside arcs (build brief S12 capital combat): batteries bear only on targets within their
    /// firing arc, so a capital fights by presenting a side. Directions are in the ship's own local frame
    /// (forward is −Z).
    /// </summary>
    [TestClass]
    public sealed class BroadsideArrayTests
    {
        [TestMethod]
        public void TargetOffThePortBeam_IsEngagedByPort_NotStarboard()
        {
            var array = new BroadsideArray(new Angle(System.Math.PI / 3)); // 60° half-arc

            array.Bears(Broadside.Port, new Vector3d(-1, 0, 0)).Should().BeTrue();
            array.Bears(Broadside.Starboard, new Vector3d(-1, 0, 0)).Should().BeFalse();
        }

        [TestMethod]
        public void TargetDeadAhead_IsEngagedByTheForeBattery()
        {
            var array = new BroadsideArray(new Angle(System.Math.PI / 4)); // 45° half-arc
            array.Bears(Broadside.Fore, new Vector3d(0, 0, -1)).Should().BeTrue(); // −Z is forward
            array.Bears(Broadside.Aft, new Vector3d(0, 0, -1)).Should().BeFalse();
        }

        [TestMethod]
        public void TargetOnAQuarter_IsEngagedByTwoBatteries()
        {
            var array = new BroadsideArray(new Angle(System.Math.PI / 3)); // 60° half-arc

            // Off the port bow: 45° between forward (−Z) and port (−X), inside a 60° arc of each.
            var bearing = array.BearingFacings(new Vector3d(-1, 0, -1));
            bearing.Should().Contain(Broadside.Port);
            bearing.Should().Contain(Broadside.Fore);
            bearing.Should().HaveCount(2);
        }

        [TestMethod]
        public void NarrowArc_LeavesAQuarterTargetInANGap()
        {
            var array = new BroadsideArray(new Angle(System.Math.PI / 8)); // 22.5° half-arc

            // The same 45°-off-bow target is now outside both the 22.5° fore and port arcs.
            array.BearingFacings(new Vector3d(-1, 0, -1)).Should().BeEmpty();
        }

        [TestMethod]
        public void AZeroDirection_BearsNoBatteries()
        {
            var array = new BroadsideArray(new Angle(System.Math.PI / 3));
            array.BearingFacings(Vector3d.Zero).Should().BeEmpty();
            array.Bears(Broadside.Fore, Vector3d.Zero).Should().BeFalse();
        }
    }
}
