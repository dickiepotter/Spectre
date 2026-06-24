namespace RP.Spectre.Tests.Combat
{
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Game.Physics;
    using RP.Math;
    using RP.Spectre.Combat;

    /// <summary>
    /// Crash-damage tuning checks (build brief S7/S18): gentle bumps do nothing; a high-speed fighter ram
    /// is lethal; damage rises with energy.
    /// </summary>
    [TestClass]
    public sealed class ImpactModelTests
    {
        private const double SpectreHullHp = 180.0; // S18 tuning table

        [TestMethod]
        public void GentleBump_DoesNoDamage()
        {
            // Two fighters drifting together at ~5 m/s.
            var a = new RigidBody { Mass = 11_000, Velocity = new Vector3d(5, 0, 0) };
            var b = new RigidBody { Mass = 11_000, Velocity = Vector3d.Zero };

            ImpactModel.HullDamage(a, b).Should().Be(0);
        }

        [TestMethod]
        public void HighSpeedFighterRam_IsLethal()
        {
            // Head-on at a combined ~100 m/s closing speed.
            var a = new RigidBody { Mass = 11_000, Velocity = new Vector3d(50, 0, 0) };
            var b = new RigidBody { Mass = 11_000, Velocity = new Vector3d(-50, 0, 0) };

            ImpactModel.HullDamage(a, b).Should().BeGreaterThan(SpectreHullHp);
        }

        [TestMethod]
        public void Damage_RisesWithEnergy()
        {
            double low = ImpactModel.HullDamage(1_000_000);
            double high = ImpactModel.HullDamage(5_000_000);
            high.Should().BeGreaterThan(low);
        }

        [TestMethod]
        public void BelowThreshold_IsExactlyZero_AtThreshold_StillZero()
        {
            ImpactModel.HullDamage(ImpactModel.SoftThresholdJoules - 1).Should().Be(0);
            ImpactModel.HullDamage(ImpactModel.SoftThresholdJoules).Should().Be(0);
            ImpactModel.HullDamage(ImpactModel.SoftThresholdJoules + 1_000_000).Should().BeGreaterThan(0);
        }
    }
}
