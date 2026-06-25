namespace RP.Spectre.World
{
    using System;
    using RP.Game.Scene;
    using RP.Math;

    /// <summary>
    /// The dreadnought <i>Tantalus</i> as a place you fly into: a 3.5 km hulk that drifts and slowly tumbles
    /// through space, with an interior authored in its own local frame and streamed in as the player descends
    /// (build brief S13/S14, "the descent"). It is a thin Spectre-specific wrapper that binds the engine's
    /// <see cref="ReferenceFrame"/> (the tumble) to a <see cref="ChunkStreamer"/> (interior residency) and
    /// knows the ship's actual dimensions.
    /// </summary>
    /// <remarks>
    /// The horror beat the brief asks for is that the wreck is never still: it tumbles, so "down" keeps
    /// changing and a point on a bulkhead is genuinely moving. <see cref="HullVelocityAt"/> exposes that
    /// motion so the player must <i>match</i> the hulk to settle against it — drifting in naively means
    /// scraping along a wall that is rotating past you.
    /// </remarks>
    public sealed class Wreck
    {
        /// <summary>Half-extents of the hull bounding box in local space (metres): X width, Y height, Z length.</summary>
        public Vector3d HalfExtents { get; }

        /// <summary>The wreck's world placement and tumble.</summary>
        public ReferenceFrame Frame { get; }

        /// <summary>Interior chunk residency, addressed in the wreck's local frame.</summary>
        public ChunkStreamer Interior { get; }

        public Wreck(Vector3d halfExtents, ReferenceFrame frame, ChunkStreamer interior)
        {
            HalfExtents = halfExtents;
            Frame = frame;
            Interior = interior;
        }

        /// <summary>
        /// The canonical Tantalus: ~3.5 km bow-to-stern (local +Z), drifting slowly and tumbling about a
        /// tilted axis at a fraction of a degree per second, with its interior streamed in 200 m chunks. The
        /// tumble is deliberately gentle — enough to disorient, not enough to make docking impossible.
        /// </summary>
        public static Wreck Tantalus(Vector3d position = default)
        {
            // 3500 m long, 700 m abeam, 520 m deep — a true capital hulk.
            var halfExtents = new Vector3d(350, 260, 1750);

            var frame = new ReferenceFrame
            {
                Position = position,
                Velocity = new Vector3d(2.0, 0, -1.0), // a slow galactic drift
                // ~0.6°/s about a tilted axis: a long, queasy tumble.
                AngularVelocity = new Vector3d(0.004, 0.009, 0.002),
            };

            // Load interior within ~600 m of the player, drop past ~1000 m: a comfortable hysteresis band.
            var interior = new ChunkStreamer(chunkSize: 200, loadRadius: 600, unloadRadius: 1000);

            return new Wreck(halfExtents, frame, interior);
        }

        /// <summary>True if a world-space point lies within the hull's local bounding box (i.e. "inside the wreck").</summary>
        public bool Contains(Vector3d worldPoint)
        {
            Vector3d local = Frame.ToLocal(worldPoint);
            return Math.Abs(local.X) <= HalfExtents.X
                && Math.Abs(local.Y) <= HalfExtents.Y
                && Math.Abs(local.Z) <= HalfExtents.Z;
        }

        /// <summary>
        /// The world velocity of the hull at the world point nearest a given world position — what a ship must
        /// match to hold station against the tumbling hulk. The mismatch with the ship's own velocity is the
        /// relative speed that grinds it along a moving bulkhead.
        /// </summary>
        public Vector3d HullVelocityAt(Vector3d worldPoint) => Frame.PointVelocity(Frame.ToLocal(worldPoint));

        /// <summary>
        /// Advances the wreck by <paramref name="dt"/> (it keeps tumbling and drifting) and restreams the
        /// interior around the player. The streamer works in local space, so the player's world position is
        /// converted into the wreck's frame first — that is what keeps the resident set centred on the player
        /// even as the hull rotates beneath them. Returns the residency change for the asset loader to act on.
        /// </summary>
        public ChunkResidencyChange Update(Vector3d playerWorldPosition, double dt)
        {
            Frame.Advance(dt);
            Vector3d playerLocal = Frame.ToLocal(playerWorldPosition);
            return Interior.Update(playerLocal);
        }
    }
}
