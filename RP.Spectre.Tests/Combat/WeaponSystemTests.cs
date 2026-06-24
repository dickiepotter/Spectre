namespace RP.Spectre.Tests.Combat
{
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Spectre.Combat;

    [TestClass]
    public sealed class WeaponSystemTests
    {
        // --- Capacitor ---

        [TestMethod]
        public void Capacitor_DrawsWhenAvailable_FailsWhenNot()
        {
            var cap = new Capacitor(capacity: 100, rechargeRate: 10);
            cap.TryDraw(60).Should().BeTrue();
            cap.Current.Should().Be(40);
            cap.TryDraw(60).Should().BeFalse(); // not enough
            cap.Current.Should().Be(40);        // unchanged
        }

        [TestMethod]
        public void Capacitor_RechargesUpToCap()
        {
            var cap = new Capacitor(100, 50);
            cap.TryDraw(100);
            cap.Update(1.0);
            cap.Current.Should().BeApproximately(50, 1e-9);
            cap.Update(10.0);
            cap.Current.Should().Be(100); // clamped
        }

        // --- Heat ---

        [TestMethod]
        public void HeatSink_OverheatsThenVentsWithHysteresis()
        {
            var heat = new HeatSink(maximum: 100, dissipationRate: 50, resetFraction: 0.5);
            heat.Add(100);
            heat.IsOverheated.Should().BeTrue();
            heat.CanFire.Should().BeFalse();

            heat.Update(0.5); // -25 -> 75, still above 50% reset
            heat.IsOverheated.Should().BeTrue();
            heat.Update(0.6); // -30 -> 45, below 50% -> clears
            heat.IsOverheated.Should().BeFalse();
        }

        // --- Weapon firing gate ---

        [TestMethod]
        public void Weapon_RespectsFireRateCooldown()
        {
            var cap = new Capacitor(1000, 0);
            var heat = new HeatSink(1000, 0);
            var pulse = WeaponCatalog.PulseLaser(); // 5/s -> 0.2 s cooldown

            pulse.TryFire(cap, heat).Should().BeTrue();
            pulse.TryFire(cap, heat).Should().BeFalse(); // still cooling
            pulse.Update(0.2);
            pulse.TryFire(cap, heat).Should().BeTrue();
        }

        [TestMethod]
        public void Weapon_StallsWhenCapacitorEmpty()
        {
            var cap = new Capacitor(capacity: 10, rechargeRate: 0); // only ~2 pulse shots (5 each)
            var heat = new HeatSink(1000, 0);
            var pulse = WeaponCatalog.PulseLaser();

            pulse.TryFire(cap, heat).Should().BeTrue();
            pulse.Update(1.0);
            pulse.TryFire(cap, heat).Should().BeTrue();
            pulse.Update(1.0);
            pulse.TryFire(cap, heat).Should().BeFalse(); // capacitor drained -> stall
        }

        [TestMethod]
        public void Weapon_StallsWhenOverheated()
        {
            var cap = new Capacitor(1000, 0);
            var heat = new HeatSink(maximum: 10, dissipationRate: 0); // overheats after a few shots
            var lance = WeaponCatalog.PrototypeLance(); // 30 heat per shot

            lance.TryFire(cap, heat).Should().BeTrue();
            heat.IsOverheated.Should().BeTrue();
            lance.Update(10.0);
            lance.TryFire(cap, heat).Should().BeFalse(); // still venting
        }

        // --- Prototype edge (S10.4) ---

        [TestMethod]
        public void PrototypePulse_OutDamagesButRunsHotterThanStandardPulse()
        {
            var standard = WeaponCatalog.PulseLaser();
            var prototype = WeaponCatalog.PrototypePulse();

            prototype.IsPrototype.Should().BeTrue();
            prototype.Damage.Should().BeGreaterThan(standard.Damage);          // the edge
            prototype.HeatPerShot.Should().BeGreaterThan(standard.HeatPerShot); // the cost
            prototype.CapacitorCost.Should().BeGreaterThan(standard.CapacitorCost);
        }

        [TestMethod]
        public void BallisticWeapons_HaveTravelTimeAndRecoil_HitscanDoesNot()
        {
            WeaponCatalog.Railgun().IsBallistic.Should().BeTrue();
            WeaponCatalog.Railgun().RecoilImpulse.Should().BeGreaterThan(0);
            double.IsPositiveInfinity(WeaponCatalog.PulseLaser().ProjectileSpeed).Should().BeTrue(); // hitscan
            WeaponCatalog.PulseLaser().RecoilImpulse.Should().Be(0);
        }
    }
}
