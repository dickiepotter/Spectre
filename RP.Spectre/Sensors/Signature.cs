namespace RP.Spectre.Sensors
{
    using System;

    /// <summary>
    /// A ship's <b>sensor signature</b> — how easy it is to detect and lock (build brief S8.4). Boosting,
    /// firing, and being large all raise it; going dark (engines cold, drifting, guns silent) drops it to a
    /// fraction. This is what enables stealth approaches, which pairs with sneaking up on the wreck.
    /// </summary>
    public static class Signature
    {
        /// <summary>The lowest fraction of base signature a ship shows with engines fully cold.</summary>
        public const double DarkFloor = 0.3;

        /// <summary>
        /// Computes the current signature from the ship's base (size-driven) signature and its activity.
        /// </summary>
        /// <param name="baseSignature">Size-driven baseline (bigger ship → larger).</param>
        /// <param name="throttle">Engine output 0…1; at 0 the ship is "dark".</param>
        /// <param name="firing">Whether weapons are firing (a bright, brief spike).</param>
        /// <param name="boosting">Whether the boost is lit (a large multiplier).</param>
        public static double Compute(double baseSignature, double throttle, bool firing, bool boosting)
        {
            double t = Math.Clamp(throttle, 0.0, 1.0);

            // Engines drive most of it: from DarkFloor (cold) up to full at max throttle.
            double signature = baseSignature * (DarkFloor + (1.0 - DarkFloor) * t);

            if (boosting) signature *= 2.5; // a hard burn lights you up
            if (firing) signature += baseSignature * 0.5;

            return signature;
        }
    }
}
