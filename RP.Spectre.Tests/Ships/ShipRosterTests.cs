namespace RP.Spectre.Tests.Ships
{
    using System;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Math;
    using RP.Spectre.Ships;

    /// <summary>
    /// The ship roster as data (build brief S18): a class's stat block builds a faithful <see cref="RP.Spectre.Ships.Combatant"/>,
    /// stats scale sensibly across the size tiers, and the two faction lines plus the player Spectre are present
    /// and distinct.
    /// </summary>
    [TestClass]
    public sealed class ShipRosterTests
    {
        [TestMethod]
        public void Factory_BuildsACombatantMatchingTheStatBlock()
        {
            ShipClass spec = ShipCatalog.CoalitionFrigate();
            var ship = ShipFactory.Build(spec, new Vector3d(100, 0, 0));

            ship.Faction.Should().Be(spec.Faction);
            ship.Body.Position.Should().Be(new Vector3d(100, 0, 0));
            ship.Body.Mass.Should().Be(spec.Mass);
            ship.Hull.MaxHp.Should().Be(spec.HullHp);
            ship.Shield.Capacity.Should().Be(spec.ShieldCapacity);
            ship.Radius.Should().Be(spec.Radius);
            ship.Alive.Should().BeTrue();
        }

        [TestMethod]
        public void Factory_CanReflagAHullToAnotherFaction()
        {
            var captured = ShipFactory.Build(ShipCatalog.SeveranceWasp(), Vector3d.Zero, Faction.Coalition);
            captured.Faction.Should().Be(Faction.Coalition);
        }

        [TestMethod]
        public void Stats_ScaleMonotonicallyWithHullClass()
        {
            ShipClass[] line =
            {
                ShipCatalog.CoalitionCorvette(),
                ShipCatalog.CoalitionFrigate(),
                ShipCatalog.CoalitionCruiser(),
                ShipCatalog.CoalitionCarrier(),
            };

            for (int i = 1; i < line.Length; i++)
            {
                line[i].HullHp.Should().BeGreaterThan(line[i - 1].HullHp);            // bigger = tankier
                line[i].Mass.Should().BeGreaterThan(line[i - 1].Mass);
                line[i].MaxSpeed.Should().BeLessThan(line[i - 1].MaxSpeed);           // bigger = slower
                line[i].Hardpoints.Should().BeGreaterThanOrEqualTo(line[i - 1].Hardpoints);
            }
        }

        [TestMethod]
        public void Wasp_MatchesTheCanonicalInterceptorStats()
        {
            // The Severance Wasp is the interceptor the earlier hand-built battle used; the data must agree.
            ShipClass wasp = ShipCatalog.SeveranceWasp();
            wasp.Faction.Should().Be(Faction.Severance);
            wasp.Class.Should().Be(HullClass.Interceptor);
            wasp.HullHp.Should().Be(120);
            wasp.ShieldCapacity.Should().Be(200);
        }

        [TestMethod]
        public void Spectre_IsTheFastPrototype()
        {
            ShipClass spectre = ShipCatalog.Spectre();
            ShipClass wasp = ShipCatalog.SeveranceWasp();

            spectre.MaxSpeed.Should().BeGreaterThan(wasp.MaxSpeed);                   // the agile edge
            spectre.PrimaryWeapon().IsPrototype.Should().BeTrue();                    // over-class guns
        }
    }
}
