namespace RP.Spectre.Tests.World
{
    using System;
    using System.Collections.Generic;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Math;
    using RP.Spectre.Combat;
    using RP.Spectre.Ships;
    using RP.Spectre.World;

    /// <summary>
    /// In-world ballistic projectiles (build brief S6/S8, the Phase 5/6 deferral): rounds travel, hit the
    /// right side, apply <see cref="DamageRouter"/> damage, can't tunnel through a target in one step, expire
    /// at range, and are pooled for reuse.
    /// </summary>
    [TestClass]
    public sealed class ProjectileSystemTests
    {
        private static Combatant TargetAt(Vector3d position, Faction faction = Faction.Severance)
            => ShipFactory.Build(ShipCatalog.SeveranceWasp(), position, faction);

        [TestMethod]
        public void Fire_LaunchesAlongTheAimDirectionAtWeaponSpeed()
        {
            var sys = new ProjectileSystem();
            Weapon rail = WeaponCatalog.Railgun();
            Projectile p = sys.Fire(Vector3d.Zero, new Vector3d(1, 0, 0), rail, Faction.Coalition);

            sys.Active.Should().HaveCount(1);
            p.Velocity.Distance(new Vector3d(rail.ProjectileSpeed, 0, 0)).Should().BeLessThan(1e-6);
        }

        [TestMethod]
        public void Fire_RejectsHitscanWeapons()
        {
            var sys = new ProjectileSystem();
            Action fireBeam = () => sys.Fire(Vector3d.Zero, new Vector3d(1, 0, 0), WeaponCatalog.PulseLaser(), Faction.Coalition);
            fireBeam.Should().Throw<ArgumentException>(); // pulse is hitscan (infinite speed)
        }

        [TestMethod]
        public void ARound_HitsAnEnemyAhead_DealsDamage_AndExpires()
        {
            var sys = new ProjectileSystem();
            var enemy = TargetAt(new Vector3d(1000, 0, 0));
            double shieldBefore = enemy.Shield.Current;

            sys.Fire(Vector3d.Zero, new Vector3d(1, 0, 0), WeaponCatalog.Railgun(), Faction.Coalition);
            int hits = sys.Step(1.0, new[] { enemy }); // 1500 m/s * 1 s sweeps past x=1000

            hits.Should().Be(1);
            enemy.Shield.Current.Should().BeLessThan(shieldBefore); // damage routed in
            sys.Active.Should().BeEmpty();                          // round consumed on hit
        }

        [TestMethod]
        public void ARound_DoesNotHitItsOwnSide()
        {
            var sys = new ProjectileSystem();
            var friend = TargetAt(new Vector3d(1000, 0, 0), Faction.Coalition);

            sys.Fire(Vector3d.Zero, new Vector3d(1, 0, 0), WeaponCatalog.Railgun(), Faction.Coalition);
            int hits = sys.Step(1.0, new[] { friend });

            hits.Should().Be(0);
            friend.Shield.Current.Should().Be(friend.Shield.Capacity); // untouched
        }

        [TestMethod]
        public void AFastRound_CannotTunnelThroughAThinTargetInOneStep()
        {
            var sys = new ProjectileSystem { MaxRange = 100_000 };
            var enemy = TargetAt(new Vector3d(1000, 0, 0)); // radius ~8

            sys.Fire(Vector3d.Zero, new Vector3d(1, 0, 0), WeaponCatalog.Railgun(), Faction.Coalition);
            int hits = sys.Step(10.0, new[] { enemy }); // end x = 15000, far past the target

            hits.Should().Be(1); // the swept test still catches it
        }

        [TestMethod]
        public void AMiss_ExpiresAtMaxRange()
        {
            var sys = new ProjectileSystem { MaxRange = 100 };
            sys.Fire(Vector3d.Zero, new Vector3d(1, 0, 0), WeaponCatalog.Railgun(), Faction.Coalition);

            sys.Step(1.0, new List<Combatant>()); // travels 1500 m, exceeds the 100 m range
            sys.Active.Should().BeEmpty();
        }

        [TestMethod]
        public void Rounds_AreReused_FromThePool()
        {
            var sys = new ProjectileSystem(prewarm: 1) { MaxRange = 100 };
            Projectile first = sys.Fire(Vector3d.Zero, new Vector3d(1, 0, 0), WeaponCatalog.Railgun(), Faction.Coalition);
            sys.Step(1.0, new List<Combatant>()); // expires it
            sys.Active.Should().BeEmpty();

            Projectile second = sys.Fire(Vector3d.Zero, new Vector3d(1, 0, 0), WeaponCatalog.Railgun(), Faction.Coalition);
            second.Should().BeSameAs(first); // same instance recycled
        }
    }
}
