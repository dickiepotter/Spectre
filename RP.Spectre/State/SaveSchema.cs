namespace RP.Spectre.State
{
    using System;
    using RP.Math;

    /// <summary>A JSON-friendly 3-vector (settable properties) used in the save schema; converts to/from
    /// the double <see cref="Vector3d"/> at the boundary so the schema never depends on RP.Math's own
    /// serialization behaviour.</summary>
    public struct SavedVector3
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public static SavedVector3 From(Vector3d v) => new SavedVector3 { X = v.X, Y = v.Y, Z = v.Z };
        public readonly Vector3d ToVector() => new Vector3d(X, Y, Z);
    }

    /// <summary>A JSON-friendly quaternion for the save schema.</summary>
    public struct SavedQuaternion
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double W { get; set; }

        public static SavedQuaternion From(Quaternion q) => new SavedQuaternion { X = q.X, Y = q.Y, Z = q.Z, W = q.W };
        public readonly Quaternion ToQuaternion() => new Quaternion(X, Y, Z, W);
    }

    /// <summary>
    /// Everything Spectre persists to resume a run (build brief S21.2): the ship's full physical state, the
    /// flight-assist mode, and enough world state (seed, mission progress) to rebuild the rest. The
    /// <see cref="SchemaVersion"/> lets a later build detect and migrate or reject an old save rather than
    /// crash on it.
    /// </summary>
    public sealed class SpectreSaveData
    {
        /// <summary>Bumped whenever the schema changes incompatibly.</summary>
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public const int CurrentSchemaVersion = 1;

        public SavedVector3 ShipPosition { get; set; }
        public SavedVector3 ShipVelocity { get; set; }
        public SavedQuaternion ShipOrientation { get; set; } = SavedQuaternion.From(Quaternion.Identity);
        public SavedVector3 ShipAngularVelocity { get; set; }

        public bool FlightAssist { get; set; } = true;

        /// <summary>The world seed — debris and layout regenerate from this rather than being stored piecewise.</summary>
        public int WorldSeed { get; set; }

        /// <summary>How far through the mission spine the player is.</summary>
        public int MissionProgress { get; set; }
    }

    /// <summary>
    /// Player settings, persisted independently of saves (build brief S21.3). Missing or out-of-range
    /// values fall back to the documented defaults via <see cref="ClampToValid"/>.
    /// </summary>
    public sealed class SpectreSettings
    {
        // Video
        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 720;
        public bool Fullscreen { get; set; }
        public bool VSync { get; set; } = true;
        public int FrameRateCap { get; set; }       // 0 = uncapped
        public float FieldOfViewDegrees { get; set; } = 60f;

        // Audio (per-bus, build brief S15.2)
        public float MasterVolume { get; set; } = 1f;
        public float MusicVolume { get; set; } = 0.8f;
        public float SfxVolume { get; set; } = 1f;
        public float UiVolume { get; set; } = 1f;
        public float VoiceVolume { get; set; } = 1f;
        public bool Muted { get; set; }

        // Controls
        public float MouseSensitivity { get; set; } = 1f;
        public bool InvertY { get; set; }
        public bool FlightAssistDefault { get; set; } = true;

        // Difficulty (build brief S22)
        public string Difficulty { get; set; } = "Standard";

        /// <summary>Pushes every value into its valid range, so a hand-edited or corrupt config can never
        /// produce a broken game (e.g. a zero resolution or a negative volume).</summary>
        public void ClampToValid()
        {
            Width = Math.Clamp(Width, 640, 7680);
            Height = Math.Clamp(Height, 480, 4320);
            FrameRateCap = Math.Clamp(FrameRateCap, 0, 1000);
            FieldOfViewDegrees = Math.Clamp(FieldOfViewDegrees, 50f, 110f);

            MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
            MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
            SfxVolume = Math.Clamp(SfxVolume, 0f, 1f);
            UiVolume = Math.Clamp(UiVolume, 0f, 1f);
            VoiceVolume = Math.Clamp(VoiceVolume, 0f, 1f);

            MouseSensitivity = Math.Clamp(MouseSensitivity, 0.05f, 10f);

            if (Difficulty is not ("Story" or "Standard" or "Hard" or "Custom"))
            {
                Difficulty = "Standard";
            }
        }
    }
}
