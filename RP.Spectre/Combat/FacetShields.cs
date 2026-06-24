namespace RP.Spectre.Combat
{
    using System.Collections.Generic;
    using RP.Math;

    /// <summary>
    /// A capital ship's six directional shield facets (build brief S8.1). Each facet is an independent
    /// <see cref="Shield"/> with its own capacity and regeneration, so damage to one side does not protect
    /// the others — which is exactly what makes flanking pay off: keep hitting the same weakened facet and
    /// it falls, exposing the hull there while the rest of the shield is still up.
    /// </summary>
    public sealed class FacetShields
    {
        private static readonly Facet[] Facings =
        {
            Facet.Fore, Facet.Aft, Facet.Port, Facet.Starboard, Facet.Dorsal, Facet.Ventral,
        };

        private readonly Dictionary<Facet, Shield> _shields = new Dictionary<Facet, Shield>();

        public FacetShields(double capacityPerFacet, double regenRate, double regenDelay)
        {
            foreach (Facet facing in Facings)
            {
                _shields[facing] = new Shield(capacityPerFacet, regenRate, regenDelay);
            }
        }

        /// <summary>The shield for a given facet.</summary>
        public Shield this[Facet facet] => _shields[facet];

        /// <summary>
        /// Picks the facet a hit lands on from its direction in the <i>ship's local frame</i> (the
        /// direction from the ship centre out toward the impact). The dominant axis chooses the facing:
        /// −Z is fore (the ship looks down −Z), +X starboard, +Y dorsal, and their opposites.
        /// </summary>
        public static Facet FacetForLocalDirection(Vector3 localDirection)
        {
            float ax = System.MathF.Abs(localDirection.X);
            float ay = System.MathF.Abs(localDirection.Y);
            float az = System.MathF.Abs(localDirection.Z);

            if (az >= ax && az >= ay)
            {
                return localDirection.Z < 0 ? Facet.Fore : Facet.Aft;
            }

            if (ax >= ay)
            {
                return localDirection.X >= 0 ? Facet.Starboard : Facet.Port;
            }

            return localDirection.Y >= 0 ? Facet.Dorsal : Facet.Ventral;
        }

        /// <summary>The shield facing a hit coming from <paramref name="localDirection"/> in ship space.</summary>
        public Shield ForLocalHit(Vector3 localDirection) => _shields[FacetForLocalDirection(localDirection)];

        /// <summary>Regenerates every facet (each respects its own regen delay).</summary>
        public void Update(double dt)
        {
            foreach (Shield shield in _shields.Values)
            {
                shield.Update(dt);
            }
        }
    }
}
