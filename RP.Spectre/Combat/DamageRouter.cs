namespace RP.Spectre.Combat
{
    /// <summary>
    /// The outcome of one hit, returned by <see cref="DamageRouter.Apply"/>.
    /// </summary>
    public readonly struct DamageResult
    {
        /// <summary>Hull damage actually dealt this hit.</summary>
        public double HullDamage { get; }

        /// <summary>True if this hit was the one that dropped the shield (fire the shield-down event).</summary>
        public bool ShieldJustFell { get; }

        /// <summary>True if this hit destroyed the ship.</summary>
        public bool Destroyed { get; }

        public DamageResult(double hullDamage, bool shieldJustFell, bool destroyed)
        {
            HullDamage = hullDamage;
            ShieldJustFell = shieldJustFell;
            Destroyed = destroyed;
        }
    }

    /// <summary>
    /// Routes a weapon hit through a ship's shield and into its hull, applying the per-weapon "vs shield" /
    /// "vs hull" multipliers (build brief S8/S18). This is where the design pillars meet the numbers:
    /// shields are a clock that energy weapons strip quickly, kinetic weapons punch through to hull, and
    /// missiles bypass the shield entirely (point-defence is their counter, Phase 6).
    /// </summary>
    public static class DamageRouter
    {
        /// <summary>
        /// Applies a hit. Energy/kinetic hits are absorbed by the shield until it falls, with any portion
        /// of the hit the shield could not stop bleeding through to the hull; missiles (and any hit while
        /// the shield is already down) go straight to hull. Damage to each layer is scaled by the matching
        /// multiplier.
        /// </summary>
        /// <param name="baseDamage">The weapon's raw damage for this hit.</param>
        /// <param name="vsShield">Multiplier of base damage applied to the shield.</param>
        /// <param name="vsHull">Multiplier of base damage applied to the hull.</param>
        public static DamageResult Apply(
            Shield shield, Hull hull, double baseDamage, double vsShield, double vsHull, DamageType type)
        {
            bool shieldWasUp = !shield.IsDown;
            double hullDamage;

            if (type == DamageType.Missile || shield.IsDown)
            {
                hullDamage = baseDamage * vsHull;
            }
            else
            {
                shield.NotifyHit();
                double shieldDamage = baseDamage * vsShield;
                if (shieldDamage <= shield.Current)
                {
                    shield.Current -= shieldDamage;
                    hullDamage = 0;
                }
                else
                {
                    // The shield stops what it can; the unstopped fraction of the hit reaches the hull.
                    double fractionThrough = (shieldDamage - shield.Current) / shieldDamage;
                    shield.Current = 0;
                    hullDamage = baseDamage * vsHull * fractionThrough;
                }
            }

            hull.TakeDamage(hullDamage);

            bool shieldJustFell = shieldWasUp && shield.IsDown;
            return new DamageResult(hullDamage, shieldJustFell, hull.IsDestroyed);
        }
    }
}
