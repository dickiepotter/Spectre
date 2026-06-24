namespace RP.Spectre.Tests.Sensors
{
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Spectre.Sensors;

    [TestClass]
    public sealed class SensorTests
    {
        // --- Signature ---

        [TestMethod]
        public void Signature_GoingDarkIsLowest_BoostingAndFiringRaiseIt()
        {
            double dark = Signature.Compute(100, throttle: 0, firing: false, boosting: false);
            double cruise = Signature.Compute(100, throttle: 0.5, firing: false, boosting: false);
            double boosting = Signature.Compute(100, throttle: 1, firing: false, boosting: true);
            double firing = Signature.Compute(100, throttle: 0.5, firing: true, boosting: false);

            dark.Should().BeApproximately(30, 1e-9);     // DarkFloor * base
            cruise.Should().BeGreaterThan(dark);
            boosting.Should().BeGreaterThan(cruise);
            firing.Should().BeGreaterThan(cruise);
        }

        // --- Sensor signal ---

        [TestMethod]
        public void SignalStrength_FallsWithDistanceAndIsZeroBeyondRange()
        {
            double near = SensorModel.SignalStrength(100, distance: 500, sensorRange: 5000, occlusion: 0);
            double far = SensorModel.SignalStrength(100, distance: 4000, sensorRange: 5000, occlusion: 0);

            near.Should().BeGreaterThan(far);
            SensorModel.SignalStrength(100, distance: 6000, sensorRange: 5000, occlusion: 0).Should().Be(0);
        }

        [TestMethod]
        public void SignalStrength_GasAndDebrisOcclusionCutTheSignal()
        {
            double clear = SensorModel.SignalStrength(100, 1000, 5000, occlusion: 0);
            double murky = SensorModel.SignalStrength(100, 1000, 5000, occlusion: 0.7);

            murky.Should().BeLessThan(clear);
            SensorModel.SignalStrength(100, 1000, 5000, occlusion: 1).Should().Be(0); // fully blocked
        }

        // --- Lock-on ---

        [TestMethod]
        public void Lock_AcquiresWithSteadyStrongSignal()
        {
            var lockOn = new LockOnTracker(requiredSeconds: 2.0, minimumSignal: 0.15);
            for (int i = 0; i < 60 * 3 && !lockOn.IsLocked; i++) lockOn.Update(1.0 / 60.0, signalStrength: 1.0);
            lockOn.IsLocked.Should().BeTrue();
        }

        [TestMethod]
        public void Lock_NeverProgressesOnTooWeakASignal()
        {
            var lockOn = new LockOnTracker(requiredSeconds: 2.0, minimumSignal: 0.15);
            for (int i = 0; i < 60 * 10; i++) lockOn.Update(1.0 / 60.0, signalStrength: 0.1); // below minimum
            lockOn.Progress.Should().Be(0);
            lockOn.IsLocked.Should().BeFalse();
        }

        [TestMethod]
        public void Lock_BreaksWhenTheTargetGoesDarkOrBehindCover()
        {
            var lockOn = new LockOnTracker(requiredSeconds: 2.0, minimumSignal: 0.15, decayRate: 1.5);
            for (int i = 0; i < 60 * 3; i++) lockOn.Update(1.0 / 60.0, 1.0); // establish lock
            lockOn.IsLocked.Should().BeTrue();

            for (int i = 0; i < 60 * 3; i++) lockOn.Update(1.0 / 60.0, 0.0); // signal lost
            lockOn.IsLocked.Should().BeFalse();
            lockOn.Progress.Should().Be(0);
        }
    }
}
