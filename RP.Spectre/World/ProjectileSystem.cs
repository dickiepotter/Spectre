namespace RP.Spectre.World
{
    using System;
    using System.Collections.Generic;
    using RP.Game.Scene;
    using RP.Math;
    using RP.Spectre.Combat;
    using RP.Spectre.Ships;

    /// <summary>One ballistic round in flight: a point carrying a weapon's damage payload until it hits or
    /// runs out of range. A class (not a struct) so it can live in an <see cref="ObjectPool{T}"/>.</summary>
    public sealed class Projectile
    {
        public Vector3d Position;
        public Vector3d Velocity;
        public double Damage;
        public double VsShield;
        public double VsHull;
        public DamageType DamageType;
        public Faction Owner;
        public double RemainingRange;
        public bool Alive;
    }

    /// <summary>
    /// In-world ballistic weapons fire (build brief S6/S8): finite-speed rounds that travel, can be led or
    /// dodged, and apply <see cref="DamageRouter"/> damage on impact. This is the entity layer the combat
    /// <i>rules</i> were always meant to drive (deferred from Phase 5/6). Rounds are pooled so a sustained
    /// firefight never churns the GC, and hit-tests are <b>swept</b> over each step so a fast shell can't tunnel
    /// through a thin target between frames.
    /// </summary>
    /// <remarks>
    /// Hitscan weapons (infinite projectile speed — pulse, beam) are not projectiles; the battle applies their
    /// damage instantly. This system is for the railgun/autocannon/missile families.
    /// </remarks>
    public sealed class ProjectileSystem
    {
        private readonly ObjectPool<Projectile> _pool;
        private readonly List<Projectile> _active = new();

        public ProjectileSystem(int prewarm = 64)
        {
            _pool = new ObjectPool<Projectile>(() => new Projectile(), prewarm, onReturn: p => p.Alive = false);
        }

        /// <summary>Maximum distance a round travels before it expires (metres).</summary>
        public double MaxRange { get; set; } = 4000;

        /// <summary>Rounds currently in flight.</summary>
        public IReadOnlyList<Projectile> Active => _active;

        /// <summary>Fires one round of <paramref name="weapon"/> from <paramref name="position"/> along
        /// <paramref name="direction"/>, owned by <paramref name="owner"/> (so it won't hit its own side).</summary>
        public Projectile Fire(Vector3d position, Vector3d direction, Weapon weapon, Faction owner)
        {
            if (double.IsInfinity(weapon.ProjectileSpeed))
                throw new ArgumentException("Hitscan weapons are not projectiles; apply their damage directly.", nameof(weapon));

            Vector3d dir = direction.NormalizeOrDefault();

            Projectile p = _pool.Get();
            p.Position = position;
            p.Velocity = dir * weapon.ProjectileSpeed;
            p.Damage = weapon.Damage;
            p.VsShield = weapon.VsShield;
            p.VsHull = weapon.VsHull;
            p.DamageType = weapon.DamageType;
            p.Owner = owner;
            p.RemainingRange = MaxRange;
            p.Alive = true;
            _active.Add(p);
            return p;
        }

        /// <summary>
        /// Advances every round by <paramref name="dt"/>, applying damage to the first enemy hull each one
        /// crosses and expiring rounds that hit or exceed their range. Returns the number of hits this step.
        /// </summary>
        public int Step(double dt, IReadOnlyList<Combatant> targets)
        {
            int hits = 0;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Projectile p = _active[i];
                Vector3d start = p.Position;
                Vector3d end = p.Position + p.Velocity * dt;

                Combatant? victim = NearestHitAlong(start, end, p.Owner, targets);
                if (victim is not null)
                {
                    DamageRouter.Apply(victim.Shield, victim.Hull, p.Damage, p.VsShield, p.VsHull, p.DamageType);
                    hits++;
                    Expire(i);
                    continue;
                }

                p.Position = end;
                p.RemainingRange -= (end - start).Magnitude;
                if (p.RemainingRange <= 0) Expire(i);
            }

            return hits;
        }

        private void Expire(int index)
        {
            Projectile p = _active[index];
            _active.RemoveAt(index);
            _pool.Return(p); // resets Alive = false
        }

        private static Combatant? NearestHitAlong(Vector3d start, Vector3d end, Faction owner, IReadOnlyList<Combatant> targets)
        {
            Combatant? nearest = null;
            double nearestT = double.PositiveInfinity;

            foreach (Combatant c in targets)
            {
                if (!c.Alive || c.Faction == owner) continue;
                if (SegmentHitsSphere(start, end, c.Body.Position, c.Radius, out double t) && t < nearestT)
                {
                    nearestT = t;
                    nearest = c;
                }
            }

            return nearest;
        }

        // Closest-point-on-segment test: does segment [a,b] pass within radius of centre? t is the hit
        // parameter in [0,1] along the segment (used to pick the nearest of several overlapping targets).
        private static bool SegmentHitsSphere(Vector3d a, Vector3d b, Vector3d centre, double radius, out double t)
        {
            Vector3d d = b - a;
            double len2 = d.MagnitudeSquared;
            if (len2 < 1e-18)
            {
                t = 0;
                return (a - centre).Magnitude <= radius;
            }

            t = (centre - a).DotProduct(d) / len2;
            t = Math.Clamp(t, 0, 1);
            Vector3d closest = a + d * t;
            return (closest - centre).Magnitude <= radius;
        }
    }
}
