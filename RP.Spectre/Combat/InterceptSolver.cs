namespace RP.Spectre.Combat
{
    using RP.Math;

    /// <summary>
    /// Computes the <b>lead point</b> for a ballistic weapon: where to aim so a projectile of finite speed
    /// meets a target that keeps moving while the shot is in flight (build brief S8.3, "lead your target").
    /// This is the maths behind the HUD lead indicator.
    /// </summary>
    /// <remarks>
    /// <para>The shot hits at some future time <c>t</c> when the projectile (travelling <c>s·t</c> from the
    /// shooter) reaches the target (at <c>P + V·t</c>). Squaring "distance travelled = distance to target"
    /// gives a quadratic in <c>t</c>: <c>(V·V − s²)t² + 2(D·V)t + D·D = 0</c>, where <c>D</c> is the shooter→
    /// target offset. We solve it with <see cref="PolynomialRoots.SolveQuadratic"/> (the maths lives in
    /// RP.Math, not here) and take the soonest positive hit time. No positive root means the projectile
    /// cannot catch the target — there is no lead, and the HUD shows none.</para>
    /// </remarks>
    public static class InterceptSolver
    {
        /// <summary>
        /// The world point to aim at, or null if the target cannot be hit. A hitscan weapon
        /// (<see cref="double.PositiveInfinity"/> speed) aims straight at the target.
        /// </summary>
        public static Vector3d? LeadPoint(Vector3d shooter, Vector3d targetPosition, Vector3d targetVelocity, double projectileSpeed)
        {
            if (double.IsInfinity(projectileSpeed)) return targetPosition; // instantaneous — no lead needed

            Vector3d offset = targetPosition - shooter;
            double a = targetVelocity.DotProduct(targetVelocity) - projectileSpeed * projectileSpeed;
            double b = 2.0 * offset.DotProduct(targetVelocity);
            double c = offset.DotProduct(offset);

            double[] roots = PolynomialRoots.SolveQuadratic(a, b, c);

            double soonest = double.PositiveInfinity;
            foreach (double t in roots)
            {
                if (t > 1e-9 && t < soonest) soonest = t;
            }

            if (double.IsPositiveInfinity(soonest)) return null; // unreachable

            return targetPosition + targetVelocity * soonest;
        }
    }
}
