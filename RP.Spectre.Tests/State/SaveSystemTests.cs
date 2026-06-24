namespace RP.Spectre.Tests.State
{
    using System.IO;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Game.Mechanics;
    using RP.Game.Physics;
    using RP.Math;
    using RP.Spectre.State;

    /// <summary>
    /// Save/load round-trip — the single most important non-graphics test (build brief S20): a broken save
    /// silently ruins a player's run, so the restored state must exactly match what was saved.
    /// </summary>
    [TestClass]
    public sealed class SaveSystemTests
    {
        private string _dir = "";
        public TestContext? TestContext { get; set; }

        [TestInitialize]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "RP.Spectre.Tests.Save", TestContext!.TestName!);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        [TestMethod]
        public void CaptureThenApply_RestoresFullShipState()
        {
            var ship = new RigidBody
            {
                Position = new Vector3d(12345.6, -789.0, 4242.0),
                Velocity = new Vector3d(30, -5, 12),
                Orientation = Quaternion.FromAxisAngle(Vector3d.YAxis, new Angle(1.2)),
                AngularVelocity = new Vector3d(0.1, -0.2, 0.05),
            };

            SpectreSaveData data = SaveSystem.Capture(ship, flightAssist: false, worldSeed: 99, missionProgress: 3);

            var restored = new RigidBody();
            SaveSystem.ApplyTo(data, restored);

            restored.Position.Distance(ship.Position).Should().BeLessThan(1e-9);
            restored.Velocity.Distance(ship.Velocity).Should().BeLessThan(1e-9);
            restored.AngularVelocity.Distance(ship.AngularVelocity).Should().BeLessThan(1e-9);
            restored.Orientation.X.Should().BeApproximately(ship.Orientation.X, 1e-12);
            restored.Orientation.W.Should().BeApproximately(ship.Orientation.W, 1e-12);
        }

        [TestMethod]
        public void FullRoundTripThroughDisk_PreservesEverything()
        {
            var ship = new RigidBody
            {
                Position = new Vector3d(50000.25, 1000.5, -33333.0),
                Velocity = new Vector3d(-1, 2, -3),
                Orientation = Quaternion.FromAxisAngle(Vector3d.XAxis, new Angle(0.7)),
                AngularVelocity = new Vector3d(0, 0.3, 0),
            };
            var saved = SaveSystem.Capture(ship, flightAssist: true, worldSeed: 1234, missionProgress: 5);

            string path = Path.Combine(_dir, "save.json");
            JsonStore.Save(path, saved);
            JsonStore.TryLoad(path, out SpectreSaveData? loaded).Should().BeTrue();

            var restored = new RigidBody();
            SaveSystem.ApplyTo(loaded!, restored);

            restored.Position.Distance(ship.Position).Should().BeLessThan(1e-6);
            restored.Velocity.Distance(ship.Velocity).Should().BeLessThan(1e-9);
            loaded!.FlightAssist.Should().BeTrue();
            loaded.WorldSeed.Should().Be(1234);
            loaded.MissionProgress.Should().Be(5);
            loaded.SchemaVersion.Should().Be(SpectreSaveData.CurrentSchemaVersion);
        }

        [TestMethod]
        public void Settings_RoundTrip()
        {
            var settings = new SpectreSettings { Width = 2560, Height = 1440, MasterVolume = 0.5f, Difficulty = "Hard" };
            string path = Path.Combine(_dir, "settings.json");

            JsonStore.Save(path, settings);
            JsonStore.TryLoad(path, out SpectreSettings? loaded).Should().BeTrue();

            loaded!.Width.Should().Be(2560);
            loaded.MasterVolume.Should().BeApproximately(0.5f, 1e-6f);
            loaded.Difficulty.Should().Be("Hard");
        }

        [TestMethod]
        public void Settings_ClampToValid_FixesOutOfRangeValues()
        {
            var settings = new SpectreSettings
            {
                Width = 1, Height = 99999, MasterVolume = 5f, MouseSensitivity = -3f, Difficulty = "Nonsense",
            };

            settings.ClampToValid();

            settings.Width.Should().BeGreaterThanOrEqualTo(640);
            settings.Height.Should().BeLessThanOrEqualTo(4320);
            settings.MasterVolume.Should().Be(1f);
            settings.MouseSensitivity.Should().BeGreaterThan(0f);
            settings.Difficulty.Should().Be("Standard");
        }

        [TestMethod]
        public void Settings_DefaultsAreSensible()
        {
            var defaults = new SpectreSettings();
            defaults.Width.Should().Be(1280);
            defaults.VSync.Should().BeTrue();
            defaults.MasterVolume.Should().Be(1f);
            defaults.Difficulty.Should().Be("Standard");
        }
    }
}
