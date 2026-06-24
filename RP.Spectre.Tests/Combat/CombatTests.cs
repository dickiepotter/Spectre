namespace RP.Spectre.Tests.Combat
{
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Spectre.Combat;

    /// <summary>
    /// Shield/hull behaviour and the lethality check (build brief S18/S20): shields deplete and regen with
    /// the correct delay, a downed shield exposes the hull, and an unshielded Spectre dies to focused
    /// fighter fire in the 2–4 second target window.
    /// </summary>
    [TestClass]
    public sealed class CombatTests
    {
        // Spectre tuning (S18).
        private const double SpectreHull = 180;
        private const double SpectreShield = 300;
        private const double SpectreRegen = 30;
        private const double SpectreRegenDelay = 4;

        // Pulse laser (S18): damage 18 @ 5/s, vs-shield 1.4, vs-hull 0.8.
        private const double PulseDamage = 18;
        private const double PulseRate = 5;
        private const double PulseVsShield = 1.4;
        private const double PulseVsHull = 0.8;

        [TestMethod]
        public void Shield_AbsorbsHitWhileUp_NoHullDamage()
        {
            var shield = new Shield(SpectreShield, SpectreRegen, SpectreRegenDelay);
            var hull = new Hull(SpectreHull);

            DamageResult r = DamageRouter.Apply(shield, hull, PulseDamage, PulseVsShield, PulseVsHull, DamageType.Energy);

            r.HullDamage.Should().Be(0);
            hull.Hp.Should().Be(SpectreHull);
            shield.Current.Should().BeApproximately(SpectreShield - PulseDamage * PulseVsShield, 1e-9);
        }

        [TestMethod]
        public void Shield_DoesNotRegenBeforeDelay_ThenDoes()
        {
            var shield = new Shield(SpectreShield, SpectreRegen, SpectreRegenDelay) { Current = 100 };
            shield.NotifyHit();

            // Just under the delay: no regen yet.
            for (int i = 0; i < 60 * 3; i++) shield.Update(1.0 / 60.0); // 3 s < 4 s delay
            shield.Current.Should().BeApproximately(100, 1e-6);

            // Past the delay: regen resumes.
            for (int i = 0; i < 60 * 2; i++) shield.Update(1.0 / 60.0); // now ~5 s total
            shield.Current.Should().BeGreaterThan(100);
        }

        [TestMethod]
        public void DownedShield_ExposesHull_AndHitGoesStraightThrough()
        {
            var shield = new Shield(SpectreShield, SpectreRegen, SpectreRegenDelay) { Current = 0 };
            var hull = new Hull(SpectreHull);
            shield.IsDown.Should().BeTrue();

            DamageResult r = DamageRouter.Apply(shield, hull, PulseDamage, PulseVsShield, PulseVsHull, DamageType.Energy);

            r.HullDamage.Should().BeApproximately(PulseDamage * PulseVsHull, 1e-9);
            hull.Hp.Should().BeApproximately(SpectreHull - PulseDamage * PulseVsHull, 1e-9);
        }

        [TestMethod]
        public void ShieldJustFell_IsReportedOnTheHitThatDropsIt()
        {
            var shield = new Shield(20, SpectreRegen, SpectreRegenDelay); // tiny shield
            var hull = new Hull(SpectreHull);

            // 18 * 1.4 = 25.2 shield damage > 20 -> shield falls this hit, remainder bleeds to hull.
            DamageResult r = DamageRouter.Apply(shield, hull, PulseDamage, PulseVsShield, PulseVsHull, DamageType.Energy);

            r.ShieldJustFell.Should().BeTrue();
            shield.IsDown.Should().BeTrue();
            r.HullDamage.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public void Missile_BypassesShieldToHull()
        {
            var shield = new Shield(SpectreShield, SpectreRegen, SpectreRegenDelay);
            var hull = new Hull(SpectreHull);

            DamageResult r = DamageRouter.Apply(shield, hull, 250, vsShield: 0, vsHull: 1.5, DamageType.Missile);

            shield.Current.Should().Be(SpectreShield); // untouched
            r.HullDamage.Should().BeApproximately(250 * 1.5, 1e-9);
        }

        [TestMethod]
        public void LethalityCheck_UnshieldedSpectre_DiesToPulseFireInTwoToFourSeconds()
        {
            var shield = new Shield(SpectreShield, SpectreRegen, SpectreRegenDelay) { Current = 0 }; // shields down
            var hull = new Hull(SpectreHull);

            double dt = 1.0 / 60.0;
            double fireInterval = 1.0 / PulseRate;
            double sinceShot = fireInterval; // fire immediately on the first eligible frame
            double t = 0;

            while (!hull.IsDestroyed && t < 10.0)
            {
                t += dt;
                sinceShot += dt;
                if (sinceShot >= fireInterval)
                {
                    sinceShot -= fireInterval;
                    DamageRouter.Apply(shield, hull, PulseDamage, PulseVsShield, PulseVsHull, DamageType.Energy);
                }
            }

            hull.IsDestroyed.Should().BeTrue();
            t.Should().BeInRange(2.0, 4.0); // the brief's lethal-window target
        }

        [TestMethod]
        public void FullShield_SurvivesNotablyLongerThanNoShield()
        {
            double TimeToDie(double startShield)
            {
                var shield = new Shield(SpectreShield, SpectreRegen, SpectreRegenDelay) { Current = startShield };
                var hull = new Hull(SpectreHull);
                double dt = 1.0 / 60.0, fireInterval = 1.0 / PulseRate, sinceShot = fireInterval, t = 0;
                while (!hull.IsDestroyed && t < 20.0)
                {
                    t += dt;
                    sinceShot += dt;
                    shield.Update(dt);
                    if (sinceShot >= fireInterval)
                    {
                        sinceShot -= fireInterval;
                        DamageRouter.Apply(shield, hull, PulseDamage, PulseVsShield, PulseVsHull, DamageType.Energy);
                    }
                }

                return t;
            }

            TimeToDie(SpectreShield).Should().BeGreaterThan(TimeToDie(0) + 1.0);
        }
    }
}
