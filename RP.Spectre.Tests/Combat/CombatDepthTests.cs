namespace RP.Spectre.Tests.Combat
{
    using System.Collections.Generic;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Math;
    using RP.Spectre.Combat;

    [TestClass]
    public sealed class CombatDepthTests
    {
        // --- Facet shields ---

        [TestMethod]
        public void FacetForLocalDirection_MapsEachAxisToItsFacing()
        {
            FacetShields.FacetForLocalDirection(new Vector3(0, 0, -1)).Should().Be(Facet.Fore);
            FacetShields.FacetForLocalDirection(new Vector3(0, 0, 1)).Should().Be(Facet.Aft);
            FacetShields.FacetForLocalDirection(new Vector3(1, 0, 0)).Should().Be(Facet.Starboard);
            FacetShields.FacetForLocalDirection(new Vector3(-1, 0, 0)).Should().Be(Facet.Port);
            FacetShields.FacetForLocalDirection(new Vector3(0, 1, 0)).Should().Be(Facet.Dorsal);
            FacetShields.FacetForLocalDirection(new Vector3(0, -1, 0)).Should().Be(Facet.Ventral);
        }

        [TestMethod]
        public void Flanking_DropsOneFacet_WhileOthersStayUp()
        {
            // Cruiser facets (S18): 1500 each. Hammer the fore facet only.
            var facets = new FacetShields(capacityPerFacet: 1500, regenRate: 60, regenDelay: 7);
            var hull = new Hull(9000);

            for (int i = 0; i < 200; i++)
            {
                Shield fore = facets.ForLocalHit(new Vector3(0, 0, -1));
                DamageRouter.Apply(fore, hull, baseDamage: 220, vsShield: 0.7, vsHull: 2.2, DamageType.Kinetic);
            }

            facets[Facet.Fore].IsDown.Should().BeTrue();      // flanked facet collapses
            facets[Facet.Aft].IsDown.Should().BeFalse();      // others untouched
            facets[Facet.Port].Current.Should().Be(1500);
        }

        // --- Ballistic lead ---

        [TestMethod]
        public void LeadPoint_StationaryTarget_IsTheTargetItself()
        {
            Vector3d? lead = InterceptSolver.LeadPoint(
                Vector3d.Origin, new Vector3d(500, 0, 0), Vector3d.Zero, projectileSpeed: 1500);
            lead.Should().NotBeNull();
            lead!.Value.Distance(new Vector3d(500, 0, 0)).Should().BeLessThan(1e-6);
        }

        [TestMethod]
        public void LeadPoint_HitscanWeapon_AimsStraightAtTarget()
        {
            Vector3d? lead = InterceptSolver.LeadPoint(
                Vector3d.Origin, new Vector3d(100, 20, 0), new Vector3d(0, 50, 0), double.PositiveInfinity);
            lead!.Value.Should().Be(new Vector3d(100, 20, 0));
        }

        [TestMethod]
        public void LeadPoint_MovingTarget_LeadsAhead_AndIsTimeConsistent()
        {
            var shooter = Vector3d.Origin;
            var target = new Vector3d(100, 0, 0);
            var velocity = new Vector3d(0, 50, 0);
            const double speed = 100;

            Vector3d lead = InterceptSolver.LeadPoint(shooter, target, velocity, speed)!.Value;

            lead.Y.Should().BeGreaterThan(0); // ahead of the target's current position
            // The projectile must reach the lead point exactly when the target does.
            double projectileTime = (lead - shooter).Magnitude / speed;
            double targetTime = lead.Y / velocity.Y;
            projectileTime.Should().BeApproximately(targetTime, 1e-6);
        }

        [TestMethod]
        public void LeadPoint_TargetOutrunsProjectile_HasNoSolution()
        {
            Vector3d? lead = InterceptSolver.LeadPoint(
                Vector3d.Origin, new Vector3d(10, 0, 0), new Vector3d(200, 0, 0), projectileSpeed: 100);
            lead.Should().BeNull();
        }

        // --- Point-defence ---

        [TestMethod]
        public void PointDefence_InterceptsAWeakMissileBeforeImpact()
        {
            var pd = new PointDefenceSystem(range: 600, damagePerSecond: 360); // 30 dmg @ 12/s
            var missiles = new List<GuidedMissile>
            {
                new GuidedMissile { Health = 50, DistanceToTarget = 800, ClosingSpeed = 400 },
            };

            int intercepted = 0, leaked = 0;
            for (int i = 0; i < 600 && missiles.Count > 0; i++)
            {
                var r = pd.Update(missiles, 1.0 / 60.0);
                intercepted += r.Intercepted;
                leaked += r.LeakedThrough;
            }

            intercepted.Should().Be(1);
            leaked.Should().Be(0);
        }

        [TestMethod]
        public void PointDefence_TankyMissileLeaksThrough()
        {
            var pd = new PointDefenceSystem(range: 600, damagePerSecond: 360);
            var missiles = new List<GuidedMissile>
            {
                new GuidedMissile { Health = 100000, DistanceToTarget = 200, ClosingSpeed = 400 },
            };

            int leaked = 0;
            for (int i = 0; i < 600 && missiles.Count > 0; i++)
            {
                leaked += pd.Update(missiles, 1.0 / 60.0).LeakedThrough;
            }

            leaked.Should().Be(1);
        }
    }
}
