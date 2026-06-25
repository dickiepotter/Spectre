namespace RP.Spectre.World
{
    using System;
    using System.Collections.Generic;
    using RP.Game.Physics;
    using RP.Game.Scene;
    using RP.Math;

    /// <summary>
    /// Spawns and carries the wreckage a destroyed ship leaves behind (build brief S8 note: debris are just
    /// <see cref="RigidBody"/>s with conserved momentum). Chunks are pooled like projectiles so a battle full
    /// of kills never thrashes the GC, and each kill's fragments are given <b>zero-mean scatter</b> over the
    /// parent's velocity, with their masses summing to the parent's — so the cloud's total momentum exactly
    /// equals the ship's at the instant it died. Newtonian: no drag, so the cloud drifts forever.
    /// </summary>
    public sealed class DebrisField
    {
        private readonly ObjectPool<RigidBody> _pool;
        private readonly List<RigidBody> _active = new();

        public DebrisField(int prewarm = 64)
        {
            _pool = new ObjectPool<RigidBody>(() => new RigidBody(), prewarm);
        }

        /// <summary>The live debris chunks.</summary>
        public IReadOnlyList<RigidBody> Active => _active;

        /// <summary>
        /// Bursts <paramref name="count"/> debris chunks from a kill at <paramref name="position"/>, conserving
        /// the parent's momentum: chunk masses sum to <paramref name="parentMass"/> and their scatter about
        /// <paramref name="parentVelocity"/> sums to zero, so Σ(mᵢ·vᵢ) = parentMass · parentVelocity.
        /// </summary>
        public void Spawn(Vector3d position, Vector3d parentVelocity, double parentMass, int count, double scatterSpeed, Random rng)
        {
            if (count <= 0) return;

            // Draw raw random scatters, then subtract their mean so the set has zero net momentum.
            var scatters = new Vector3d[count];
            Vector3d sum = Vector3d.Zero;
            for (int i = 0; i < count; i++)
            {
                var s = new Vector3d(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1) * scatterSpeed;
                scatters[i] = s;
                sum += s;
            }

            Vector3d mean = sum / count;
            double chunkMass = parentMass / count;

            for (int i = 0; i < count; i++)
            {
                RigidBody chunk = _pool.Get();
                chunk.Position = position;
                chunk.Velocity = parentVelocity + (scatters[i] - mean);
                chunk.Mass = chunkMass;
                chunk.Orientation = Quaternion.Identity;
                // A little tumble each, for visual life — angular momentum isn't tracked across the cloud.
                chunk.AngularVelocity = new Vector3d(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5);
                _active.Add(chunk);
            }
        }

        /// <summary>Drifts every chunk forward (no forces — momentum persists).</summary>
        public void Step(double dt)
        {
            foreach (RigidBody chunk in _active) chunk.Integrate(dt);
        }

        /// <summary>Returns all chunks to the pool (e.g. when a debris field is culled far from the player).</summary>
        public void Clear()
        {
            foreach (RigidBody chunk in _active) _pool.Return(chunk);
            _active.Clear();
        }

        /// <summary>Total momentum of the live cloud (Σ mᵢ·vᵢ) — the conserved quantity.</summary>
        public Vector3d TotalMomentum()
        {
            Vector3d p = Vector3d.Zero;
            foreach (RigidBody chunk in _active) p += chunk.Velocity * chunk.Mass;
            return p;
        }
    }
}
