namespace RP.Spectre.Tests.Missions
{
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Spectre.Combat;
    using RP.Spectre.Missions;

    /// <summary>
    /// Difficulty scalars and their effect on the lethality check (build brief S22/S20): Standard is the
    /// neutral, tuned target; Story is gentler and Hard harsher on every dial; and the incoming-damage
    /// multiplier shifts time-to-die in the expected direction without breaking the Standard balance.
    /// </summary>
    [TestClass]
    public sealed class DifficultyTests
    {
        [TestMethod]
        public void Standard_IsNeutral()
        {
            var s = DifficultyScalars.For(DifficultyPreset.Standard);
            s.EnemyCount.Should().Be(1.0);
            s.EnemyDamage.Should().Be(1.0);
            s.EnemyAccuracy.Should().Be(1.0);
            s.EnemyAggression.Should().Be(1.0);
            s.IncomingDamage.Should().Be(1.0);
        }

        [TestMethod]
        public void Story_IsGentler_AndHard_IsHarsher_OnEveryDial()
        {
            var story = DifficultyScalars.For(DifficultyPreset.Story);
            var standard = DifficultyScalars.For(DifficultyPreset.Standard);
            var hard = DifficultyScalars.For(DifficultyPreset.Hard);

            story.EnemyDamage.Should().BeLessThan(standard.EnemyDamage);
            story.IncomingDamage.Should().BeLessThan(standard.IncomingDamage);
            hard.EnemyDamage.Should().BeGreaterThan(standard.EnemyDamage);
            hard.IncomingDamage.Should().BeGreaterThan(standard.IncomingDamage);
        }

        // Time for an unshielded Spectre to die under pulse fire, scaled by the incoming-damage dial.
        private static double TimeToDie(double incomingDamageMultiplier)
        {
            var shield = new Shield(300, 30, 4) { Current = 0 };
            var hull = new Hull(180);
            const double dt = 1.0 / 60.0, fireInterval = 1.0 / 5.0;
            double sinceShot = fireInterval, t = 0;

            while (!hull.IsDestroyed && t < 30.0)
            {
                t += dt;
                sinceShot += dt;
                if (sinceShot >= fireInterval)
                {
                    sinceShot -= fireInterval;
                    DamageRouter.Apply(shield, hull, 18 * incomingDamageMultiplier, 1.4, 0.8, DamageType.Energy);
                }
            }

            return t;
        }

        [TestMethod]
        public void LethalityCheck_HoldsAtStandard_AndShiftsWithDifficulty()
        {
            double standard = TimeToDie(DifficultyScalars.For(DifficultyPreset.Standard).IncomingDamage);
            double story = TimeToDie(DifficultyScalars.For(DifficultyPreset.Story).IncomingDamage);
            double hard = TimeToDie(DifficultyScalars.For(DifficultyPreset.Hard).IncomingDamage);

            standard.Should().BeInRange(2.0, 4.0); // the tuned target still holds
            hard.Should().BeLessThan(standard);    // harder = die sooner
            story.Should().BeGreaterThan(standard); // gentler = survive longer
        }
    }
}
