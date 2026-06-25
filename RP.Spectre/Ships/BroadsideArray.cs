namespace RP.Spectre.Ships
{
    using System.Collections.Generic;
    using RP.Math;

    /// <summary>A capital ship's weapon facings, each a battery mounted on one side of the hull.</summary>
    public enum Broadside
    {
        Fore,
        Aft,
        Port,
        Starboard,
        Dorsal,
        Ventral,
    }

    /// <summary>
    /// The firing-arc logic for capital broadsides (build brief S12 capital combat). Unlike a fighter, whose
    /// single gun points wherever the nose points, a capital fights by <b>presenting a side</b>: a target off
    /// the port beam is engaged by the port battery while the starboard guns stay dark. This models which
    /// batteries can bear on a target, given the target's direction in the ship's own local frame, so the
    /// capital AI manoeuvres to bring the most guns to bear instead of nosing in like a dart.
    /// </summary>
    /// <remarks>
    /// Each facing has an outward normal in local space (forward is −Z, the engine convention): a battery
    /// bears when the target direction falls within <see cref="ArcHalfWidth"/> of its normal. Overlapping arcs
    /// mean a target on a quarter (say off the port bow) can be engaged by two batteries at once — the sweet
    /// spot a good captain holds.
    /// </remarks>
    public sealed class BroadsideArray
    {
        private static readonly (Broadside Facing, Vector3d Normal)[] Facings =
        {
            (Broadside.Fore, new Vector3d(0, 0, -1)),
            (Broadside.Aft, new Vector3d(0, 0, 1)),
            (Broadside.Port, new Vector3d(-1, 0, 0)),
            (Broadside.Starboard, new Vector3d(1, 0, 0)),
            (Broadside.Dorsal, new Vector3d(0, 1, 0)),
            (Broadside.Ventral, new Vector3d(0, -1, 0)),
        };

        private readonly double _arcCos;

        /// <param name="arcHalfWidth">Half-angle of each battery's firing arc (e.g. 60° gives a 120° arc).</param>
        public BroadsideArray(Angle arcHalfWidth)
        {
            ArcHalfWidth = arcHalfWidth;
            _arcCos = Angle.Cos(arcHalfWidth);
        }

        /// <summary>Half-angle of every battery's firing arc.</summary>
        public Angle ArcHalfWidth { get; }

        /// <summary>True if the given battery can bear on a target lying in <paramref name="localTargetDirection"/>.</summary>
        public bool Bears(Broadside facing, Vector3d localTargetDirection)
        {
            Vector3d dir = localTargetDirection.NormalizeOrDefault();
            if (dir.IsZero()) return false;

            foreach ((Broadside f, Vector3d normal) in Facings)
            {
                if (f == facing) return normal.DotProduct(dir) >= _arcCos;
            }

            return false;
        }

        /// <summary>Every battery that can bear on a target in <paramref name="localTargetDirection"/>.</summary>
        public IReadOnlyList<Broadside> BearingFacings(Vector3d localTargetDirection)
        {
            var bearing = new List<Broadside>();
            Vector3d dir = localTargetDirection.NormalizeOrDefault();
            if (dir.IsZero()) return bearing;

            foreach ((Broadside facing, Vector3d normal) in Facings)
            {
                if (normal.DotProduct(dir) >= _arcCos) bearing.Add(facing);
            }

            return bearing;
        }
    }
}
