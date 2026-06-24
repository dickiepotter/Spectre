namespace RP.Spectre.Sensors
{
    using System;

    /// <summary>
    /// Turns a target's signature, range, and the stuff in the way into a single <b>signal strength</b>
    /// (0…1) — how clear a sensor contact is. Gas clouds and debris between you and the target cut the
    /// signal, which is why the battlefield's leftovers are tactically meaningful and not just scenery
    /// (build brief S8.4, S9).
    /// </summary>
    public static class SensorModel
    {
        /// <summary>
        /// Signal strength (0…1) of a contact. Zero beyond <paramref name="sensorRange"/>; falls off with
        /// distance and with <paramref name="occlusion"/> (0 = clear line, 1 = fully blocked by gas/debris).
        /// </summary>
        /// <param name="signature">Target signature (see <see cref="Signature"/>).</param>
        /// <param name="distance">Distance to the target (metres).</param>
        /// <param name="sensorRange">The sensor's nominal range for a reference signature.</param>
        /// <param name="occlusion">Fraction of the line of sight blocked by gas/debris, 0…1.</param>
        /// <param name="referenceSignature">Signature that fills the range at full clarity.</param>
        public static double SignalStrength(
            double signature, double distance, double sensorRange, double occlusion, double referenceSignature = 100)
        {
            if (distance >= sensorRange || sensorRange <= 0) return 0;

            double rangeFactor = 1.0 - distance / sensorRange;            // linear falloff to the edge
            double clarity = Math.Clamp(1.0 - occlusion, 0.0, 1.0);       // gas/debris cut the signal
            double brightness = referenceSignature <= 0 ? 1.0 : signature / referenceSignature;

            return Math.Clamp(brightness * rangeFactor * clarity, 0.0, 1.0);
        }

        /// <summary>Whether a contact is detectable at all (any non-trivial signal).</summary>
        public static bool IsDetectable(double signalStrength, double threshold = 0.05) => signalStrength > threshold;
    }
}
