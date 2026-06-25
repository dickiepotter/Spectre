namespace RP.Spectre.Tests.World
{
    using System;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using RP.Math;
    using RP.Spectre.World;

    /// <summary>
    /// The burst-particle FX system: a burst spawns the requested sparks, they age and are culled when their
    /// life runs out, and their colour/size interpolate hot→cool over life (the look the HDR bloom turns into
    /// glowing explosions).
    /// </summary>
    [TestClass]
    public sealed class ParticleSystemTests
    {
        [TestMethod]
        public void Burst_SpawnsTheRequestedParticles()
        {
            var fx = new ParticleSystem();
            fx.Burst(Vector3d.Zero, Vector3d.Zero, count: 30, speed: 50, life: 1.0f, size: 4f,
                hot: new Vector3(6, 3, 1), cool: new Vector3(1, 0.2f, 0.05f), rng: new Random(1));

            fx.Active.Should().HaveCount(30);
        }

        [TestMethod]
        public void Particles_AgeAndAreCulledWhenTheirLifeEnds()
        {
            var fx = new ParticleSystem();
            fx.Burst(Vector3d.Zero, Vector3d.Zero, 20, 10, life: 0.5f, size: 2f,
                hot: Vector3.One, cool: Vector3.Zero, rng: new Random(2));

            for (int i = 0; i < 120; i++) fx.Step(1.0 / 60.0); // 2 s — well past the longest life
            fx.Active.Should().BeEmpty();
        }

        [TestMethod]
        public void ColorAndSize_InterpolateHotToCoolOverLife()
        {
            var fx = new ParticleSystem();
            fx.Burst(Vector3d.Zero, Vector3d.Zero, 1, 0, life: 1.0f, size: 10f,
                hot: new Vector3(8, 4, 1), cool: new Vector3(1, 0, 0), rng: new Random(3));
            Particle p = fx.Active[0];

            // Young: near the hot colour, near full size.
            ParticleSystem.ColorOf(p).X.Should().BeApproximately(8f, 0.5f);
            ParticleSystem.SizeOf(p).Should().BeApproximately(10f, 0.5f);

            for (int i = 0; i < 57; i++) fx.Step(1.0 / 60.0); // ~0.95 s, near end of life
            ParticleSystem.ColorOf(p).X.Should().BeLessThan(2f); // cooled toward the cool colour
            ParticleSystem.SizeOf(p).Should().BeLessThan(10f);   // shrunk
        }
    }
}
