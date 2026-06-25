namespace RP.Spectre.World
{
    using System;
    using System.Collections.Generic;
    using RP.Game.Scene;
    using RP.Math;

    /// <summary>One short-lived glowing particle: a spark of an explosion or a puff of engine wash. Its colour
    /// is interpolated from hot (young) to cool (old) and it shrinks as it dies, so a burst reads as a flash
    /// that cools to embers. Colours are kept &gt; 1 on purpose so the HDR bloom makes them glow.</summary>
    public sealed class Particle
    {
        public Vector3d Position;
        public Vector3d Velocity;
        public float Age;
        public float Life;
        public float Size;
        public Vector3 ColorHot;
        public Vector3 ColorCool;
        public bool Alive;

        /// <summary>0 at birth, 1 at death.</summary>
        public float T => Life <= 0 ? 1f : Age / Life;
    }

    /// <summary>
    /// A pooled burst-particle system for explosions and engine wash (build brief: spectacle). It owns no
    /// rendering — the caller reads <see cref="Active"/> each frame and emits instances — so the same glowing
    /// particles flow through the existing instanced draw and the bloom chain. Newtonian: particles coast (a
    /// touch of drag so bursts settle) and fade out, then return to the pool.
    /// </summary>
    public sealed class ParticleSystem
    {
        private readonly ObjectPool<Particle> _pool;
        private readonly List<Particle> _active = new();

        public ParticleSystem(int prewarm = 512)
        {
            _pool = new ObjectPool<Particle>(() => new Particle(), prewarm, onReturn: p => p.Alive = false);
        }

        public IReadOnlyList<Particle> Active => _active;

        /// <summary>Throws <paramref name="count"/> particles out from a point, inheriting a base velocity and
        /// scattering at up to <paramref name="speed"/> m/s, fading over <paramref name="life"/> seconds.</summary>
        public void Burst(Vector3d position, Vector3d baseVelocity, int count, double speed, float life, float size,
            Vector3 hot, Vector3 cool, Random rng)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = new Vector3d(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1);
                if (dir.IsZero()) dir = new Vector3d(0, 1, 0);
                dir = dir.Normalize();

                Particle p = _pool.Get();
                p.Position = position;
                p.Velocity = baseVelocity + dir * (speed * (0.3 + 0.7 * rng.NextDouble()));
                p.Age = 0;
                p.Life = life * (float)(0.6 + 0.8 * rng.NextDouble());
                p.Size = size;
                p.ColorHot = hot;
                p.ColorCool = cool;
                p.Alive = true;
                _active.Add(p);
            }
        }

        /// <summary>Advances and ages every particle; dead ones go back to the pool.</summary>
        public void Step(double dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Particle p = _active[i];
                p.Age += (float)dt;
                if (p.Age >= p.Life)
                {
                    _active.RemoveAt(i);
                    _pool.Return(p);
                    continue;
                }

                p.Position += p.Velocity * dt;
                p.Velocity *= 1.0 - 1.5 * dt; // gentle drag so a burst blooms then settles
            }
        }

        /// <summary>Current colour of a particle (hot → cool over its life).</summary>
        public static Vector3 ColorOf(Particle p)
        {
            float t = p.T;
            return p.ColorHot * (1 - t) + p.ColorCool * t;
        }

        /// <summary>Current draw size (shrinks toward death).</summary>
        public static float SizeOf(Particle p) => p.Size * (1f - 0.7f * p.T);
    }
}
