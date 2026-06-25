namespace RP.Spectre.Tests.Hud
{
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Game.Physics;
    using RP.Math;
    using RP.Spectre.Combat;
    using RP.Spectre.Hud;

    /// <summary>
    /// The flight/combat HUD snapshot (build brief S14, and the S3 prograde-marker deferral): a pure read of
    /// the ship's state — speed and prograde, gauges, and a target readout with range, closing speed and the
    /// ballistic lead pip. No rendering, so the maths is checked directly.
    /// </summary>
    [TestClass]
    public sealed class HudModelTests
    {
        private static (RigidBody body, Shield shield, Hull hull, HeatSink heat, Capacitor cap) Player()
            => (new RigidBody(), new Shield(300, 30, 4), new Hull(180), new HeatSink(200, 50), new Capacitor(320, 70));

        [TestMethod]
        public void Prograde_PointsAlongVelocity_AndSpeedIsItsMagnitude()
        {
            var (body, shield, hull, heat, cap) = Player();
            body.Velocity = new Vector3d(0, 0, -120);

            HudSnapshot hud = HudModel.Build(body, shield, hull, heat, cap, weaponReady: true);

            hud.Speed.Should().BeApproximately(120, 1e-9);
            hud.Prograde.Distance(new Vector3d(0, 0, -1)).Should().BeLessThan(1e-9);
            hud.Boresight.Distance(new Vector3d(0, 0, -1)).Should().BeLessThan(1e-9); // identity orientation faces −Z
        }

        [TestMethod]
        public void AtRest_HasNoProgradeDirection()
        {
            var (body, shield, hull, heat, cap) = Player();
            HudSnapshot hud = HudModel.Build(body, shield, hull, heat, cap, weaponReady: false);

            hud.Speed.Should().Be(0);
            hud.Prograde.Should().Be(Vector3d.Zero);
            hud.WeaponReady.Should().BeFalse();
        }

        [TestMethod]
        public void Gauges_ReflectTheSystems()
        {
            var (body, shield, hull, heat, cap) = Player();
            shield.Current = 150; // half of 300

            HudSnapshot hud = HudModel.Build(body, shield, hull, heat, cap, weaponReady: true);

            hud.ShieldFraction.Should().BeApproximately(0.5, 1e-9);
            hud.HullFraction.Should().BeApproximately(1.0, 1e-9);
            hud.CapacitorFraction.Should().Be(cap.Fraction);
            hud.HeatFraction.Should().Be(heat.Fraction);
            hud.Target.Should().BeNull(); // no target supplied
        }

        [TestMethod]
        public void Target_ReportsRange_ClosingSpeed_AndCondition()
        {
            var (body, shield, hull, heat, cap) = Player();
            body.Velocity = new Vector3d(0, 0, -100); // flying toward a target dead ahead (−Z)

            var contact = new TargetContact
            {
                Position = new Vector3d(0, 0, -1500),
                Velocity = Vector3d.Zero,
                ShieldFraction = 0.25,
                HullFraction = 0.6,
            };

            HudSnapshot hud = HudModel.Build(body, shield, hull, heat, cap, true, contact, weaponProjectileSpeed: double.PositiveInfinity);
            TargetReadout t = hud.Target!.Value;

            t.Range.Should().BeApproximately(1500, 1e-6);
            t.Direction.Distance(new Vector3d(0, 0, -1)).Should().BeLessThan(1e-9);
            t.ClosingSpeed.Should().BeApproximately(100, 1e-9); // closing in at 100 m/s
            t.ShieldFraction.Should().Be(0.25);
            t.HullFraction.Should().Be(0.6);
            t.HasLeadSolution.Should().BeTrue();
            t.AimPoint.Should().Be(contact.Position); // hitscan aims straight at it
        }

        [TestMethod]
        public void ClosingSpeed_IsNegativeWhenOpeningTheRange()
        {
            var (body, shield, hull, heat, cap) = Player();
            body.Velocity = new Vector3d(0, 0, 100); // running away from a target ahead

            var contact = new TargetContact { Position = new Vector3d(0, 0, -1000), Velocity = Vector3d.Zero };
            HudSnapshot hud = HudModel.Build(body, shield, hull, heat, cap, true, contact);

            hud.Target!.Value.ClosingSpeed.Should().BeLessThan(0);
        }

        [TestMethod]
        public void LeadPip_LeadsACrossingTargetForABallisticWeapon()
        {
            var (body, shield, hull, heat, cap) = Player();

            var contact = new TargetContact
            {
                Position = new Vector3d(1000, 0, 0),
                Velocity = new Vector3d(0, 100, 0), // crossing in +Y
            };

            HudSnapshot hud = HudModel.Build(body, shield, hull, heat, cap, true, contact, weaponProjectileSpeed: 1000);
            TargetReadout t = hud.Target!.Value;

            t.HasLeadSolution.Should().BeTrue();
            t.AimPoint.Y.Should().BeGreaterThan(0); // aim ahead of where the target is now
        }
    }
}
