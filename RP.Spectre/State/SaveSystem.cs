namespace RP.Spectre.State
{
    using System.IO;
    using RP.Game.Mechanics;
    using RP.Game.Physics;

    /// <summary>
    /// Spectre's save/settings system: where the files live, and how to capture the live game into a
    /// <see cref="SpectreSaveData"/> and restore it again. Built on the engine's generic
    /// <see cref="JsonStore"/> (atomic, corruption-safe); this layer only knows the Spectre specifics.
    /// </summary>
    public static class SaveSystem
    {
        public const string AppName = "Spectre";

        /// <summary>Per-user folder for saves and settings (e.g. <c>%AppData%\Spectre</c>).</summary>
        public static string Directory => JsonStore.UserDataDirectory(AppName);

        /// <summary>The single resumable save slot.</summary>
        public static string SavePath => Path.Combine(Directory, "save.json");

        /// <summary>The settings file (persisted independently of saves).</summary>
        public static string SettingsPath => Path.Combine(Directory, "settings.json");

        /// <summary>True if a resumable save exists (drives the menu's "Continue" being enabled).</summary>
        public static bool HasSave => File.Exists(SavePath);

        /// <summary>Snapshots the live ship + run state into a serialisable save object.</summary>
        public static SpectreSaveData Capture(RigidBody ship, bool flightAssist, int worldSeed, int missionProgress) =>
            new SpectreSaveData
            {
                ShipPosition = SavedVector3.From(ship.Position),
                ShipVelocity = SavedVector3.From(ship.Velocity),
                ShipOrientation = SavedQuaternion.From(ship.Orientation),
                ShipAngularVelocity = SavedVector3.From(ship.AngularVelocity),
                FlightAssist = flightAssist,
                WorldSeed = worldSeed,
                MissionProgress = missionProgress,
            };

        /// <summary>Restores a saved ship state onto a live <see cref="RigidBody"/>.</summary>
        public static void ApplyTo(SpectreSaveData data, RigidBody ship)
        {
            ship.Position = data.ShipPosition.ToVector();
            ship.Velocity = data.ShipVelocity.ToVector();
            ship.Orientation = data.ShipOrientation.ToQuaternion();
            ship.AngularVelocity = data.ShipAngularVelocity.ToVector();
        }

        /// <summary>Writes the save to the user-data slot (atomic).</summary>
        public static void Save(SpectreSaveData data) => JsonStore.Save(SavePath, data);

        /// <summary>Loads the save slot; false if none exists or it is unreadable.</summary>
        public static bool TryLoad(out SpectreSaveData? data) => JsonStore.TryLoad(SavePath, out data);

        /// <summary>Writes settings to the user-data folder.</summary>
        public static void SaveSettings(SpectreSettings settings) => JsonStore.Save(SettingsPath, settings);

        /// <summary>Loads settings, clamping to valid ranges; returns defaults if none/corrupt.</summary>
        public static SpectreSettings LoadSettings()
        {
            if (JsonStore.TryLoad(SettingsPath, out SpectreSettings? settings) && settings is not null)
            {
                settings.ClampToValid();
                return settings;
            }

            return new SpectreSettings();
        }
    }
}
