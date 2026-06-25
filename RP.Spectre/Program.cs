namespace RP.Spectre
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using RP.Game.Audio;
    using RP.Game.Core;
    using RP.Game.Core.Logging;
    using RP.Game.Graphics.Vulkan;
    using RP.Game.Physics;
    using RP.Game.Platform;
    using RP.Game.Rendering;
    using RP.Game.Scene;
    using RP.Math;
    using RP.Spectre.Combat;
    using RP.Spectre.Ships;
    using RP.Spectre.State;
    using RP.Spectre.World;
    using Silk.NET.Input;
    using Silk.NET.Maths;
    using Silk.NET.Windowing;

    /// <summary>
    /// The Spectre executable's entry point: open a window, bring up the Vulkan renderer, and run the
    /// fixed-timestep loop — now drawing a <b>live fleet battle</b>. A headless <see cref="BattleSimulation"/>
    /// fights two fleets built from the ship roster; each frame their hulls (and the debris of the dead) are
    /// handed to the renderer as instanced cubes, sized per ship class, while the player flies the cockpit
    /// camera freely through it (build brief S7/S20 — "a battle of 50+ ships reads as a real engagement").
    /// </summary>
    /// <remarks>
    /// Pass <c>--frames N</c> (or set <c>SPECTRE_FRAMES=N</c>) to auto-close after N rendered frames — a
    /// bounded run the build can execute unattended and then assert that no Vulkan validation error was
    /// logged. The full ship/projectile/HUD <i>art</i> is still cubes; the systems beneath are the tested
    /// ones from the build log. This is the visible smoke pass for the whole stack.
    /// </remarks>
    public static class Program
    {
        public static int Main(string[] args)
        {
            int maxFrames = ParseMaxFrames(args);

            var validationWatch = new CollectingLogSink();
            var log = new Logger(new ConsoleLogSink(), validationWatch) { MinimumLevel = LogLevel.Info };
            log.Info("Spectre", "Live fleet battle. Fly the cockpit: WASD thrust, Space/Ctrl up/down, mouse steer, " +
                                 "Q/E roll, Shift boost, T flight-assist, F5 quicksave." +
                                 (maxFrames > 0 ? $" Auto-closing after {maxFrames} frames." : string.Empty));

            IWindow window = VulkanWindow.Create("SPECTRE", 1280, 720);
            window.Initialize();

            using var renderer = new VulkanRenderer(window, log, enableValidation: true);
            window.FramebufferResize += (Vector2D<int> _) => renderer.NotifyResize();

            // The player: a free-flying cockpit, held back and looking down the +Z axis at the engagement,
            // which is centred on the world origin. Default orientation faces -Z, so the fight is dead ahead.
            var ship = new RigidBody { Position = new Vector3d(0, 600, 5200) };
            var shipController = new ShipController(ship);
            var floatingOrigin = new FloatingOrigin(rebaseThreshold: 4096);

            // Two fleets drawn up from the roster, fought headless by the battle sim.
            var battle = BuildBattle(seed: 20260625);
            var debris = new DebrisField(prewarm: 256);
            var particles = new ParticleSystem(prewarm: 1024);
            var wasAlive = new bool[battle.Combatants.Count];
            for (int i = 0; i < wasAlive.Length; i++) wasAlive[i] = battle.Combatants[i].Alive;

            // The player's guns: a ballistic autocannon (so the shots are visible tracers), with its own
            // capacitor/heat so the real fire-discipline applies, and the projectile system they feed.
            Weapon playerGun = WeaponCatalog.Autocannon();
            var playerCapacitor = new Capacitor(capacity: 600, rechargeRate: 160);
            var playerHeat = new HeatSink(maximum: 500, dissipationRate: 150);
            var projectiles = new ProjectileSystem(prewarm: 256) { MaxRange = 6000 };

            // Reusable per-frame instance buffers (positions/colours/scales) for the renderer.
            var positions = new List<Vector3d>(VulkanInstanceBudget);
            var colors = new List<Vector3>(VulkanInstanceBudget);
            var scales = new List<float>(VulkanInstanceBudget);
            var hudLines = new List<HudVertex>(512);

            SpectreSettings settings = SaveSystem.LoadSettings();
            shipController.FlightAssist = settings.FlightAssistDefault;
            renderer.Camera.FieldOfView = new Angle(settings.FieldOfViewDegrees, AngleUnits.DEG);
            renderer.Camera.NearPlane = 1.0;      // cockpit scale
            renderer.Camera.FarPlane = 80_000.0;  // see ships and capitals kilometres out — the void is vast
            renderer.ClearColor = (0.01f, 0.01f, 0.02f, 1f); // deep space
            renderer.ModelTransform = Matrix.Identity;        // hulls don't spin; the battle moves them
            bool interactive = maxFrames <= 0;

            int worldSeed = 1;
            if (interactive && SaveSystem.TryLoad(out SpectreSaveData? existingSave) && existingSave is not null)
            {
                SaveSystem.ApplyTo(existingSave, ship);
                shipController.FlightAssist = existingSave.FlightAssist;
                worldSeed = existingSave.WorldSeed;
                log.Info("Save", $"Continued from save (ship at {existingSave.ShipPosition.ToVector():0}).");
            }
            bool previousQuicksaveKey = false;

            IInputContext input = window.CreateInput();
            IKeyboard? keyboard = input.Keyboards.Count > 0 ? input.Keyboards[0] : null;
            IMouse? mouse = input.Mice.Count > 0 ? input.Mice[0] : null;

            // Audio bring-up, guarded so a machine with no device still runs.
            AudioEngine? audio = null;
            try
            {
                audio = new AudioEngine(log);
            }
            catch (Exception ex)
            {
                log.Warning("Audio", $"Audio unavailable, continuing without it: {ex.Message}");
            }

            var accumulator = new FixedTimestepAccumulator(fixedDeltaSeconds: 1.0 / 60.0);
            var time = new GameTime(accumulator.FixedDeltaSeconds, 0.0, 0);
            var clock = Stopwatch.StartNew();
            double lastSeconds = 0.0;
            long renderedFrames = 0;

            while (!window.IsClosing)
            {
                window.DoEvents();

                double now = clock.Elapsed.TotalSeconds;
                double frameSeconds = now - lastSeconds;
                lastSeconds = now;

                if (interactive && keyboard is not null && mouse is not null)
                {
                    shipController.ReadControls(keyboard, mouse);

                    bool quicksaveKey = keyboard.IsKeyPressed(Key.F5);
                    if (quicksaveKey && !previousQuicksaveKey)
                    {
                        SaveSystem.Save(SaveSystem.Capture(ship, shipController.FlightAssist, worldSeed, missionProgress: 0));
                        log.Info("Save", "Quicksaved.");
                    }
                    previousQuicksaveKey = quicksaveKey;
                }

                int steps = accumulator.Advance(frameSeconds);
                for (int i = 0; i < steps; i++)
                {
                    time = time.Advanced();
                    double dt = accumulator.FixedDeltaSeconds;

                    if (interactive) shipController.FixedStep(dt);
                    else ship.Integrate(dt); // bounded mode: hold the scripted vantage

                    battle.Step(dt);

                    // Player guns: left mouse fires (auto-fires in the bounded smoke run). The autocannon is
                    // triple-gated (cooldown + heat + charge), so holding the trigger overheats it just like AI.
                    playerCapacitor.Update(dt);
                    playerHeat.Update(dt);
                    playerGun.Update(dt);
                    bool firing = !interactive || (mouse?.IsButtonPressed(MouseButton.Left) ?? false);
                    if (firing && playerGun.TryFire(playerCapacitor, playerHeat))
                    {
                        projectiles.Fire(ship.Position + ship.Forward * 6, ship.Forward, playerGun, Faction.Coalition);
                    }
                    projectiles.Step(dt, battle.Combatants); // player shots damage the Severance hulls

                    debris.Step(dt);
                    particles.Step(dt);
                    HandleNewKills(battle, debris, particles, wasAlive);
                }

                // Floating origin follows the player so render coordinates stay small and precise.
                if (floatingOrigin.MaybeRebase(ship.Position))
                {
                    log.Info("World", $"Floating-origin rebased at ~{ship.Position.Magnitude:0} m out.");
                }
                renderer.RenderOrigin = floatingOrigin.Origin;

                renderer.Camera.Position = floatingOrigin.ToRenderSpace(ship.Position);
                renderer.Camera.Target = renderer.Camera.Position + ship.Forward;
                renderer.Camera.Up = ship.Up;

                if (audio is not null)
                {
                    Vector3d forward = (renderer.Camera.Target - renderer.Camera.Position).NormalizeOrDefault();
                    audio.SetListener((Vector3)renderer.Camera.Position, Vector3.Zero, (Vector3)forward, Vector3.UnitY);
                }

                BuildInstances(battle, debris, projectiles, particles, positions, colors, scales);
                renderer.SetInstances(
                    CollectionsMarshal.AsSpan(positions),
                    CollectionsMarshal.AsSpan(colors),
                    CollectionsMarshal.AsSpan(scales));

                float aspect = renderer.Camera.AspectRatio <= 0 ? 16f / 9f : (float)renderer.Camera.AspectRatio;
                BuildHud(renderer.Camera.ViewProjection, floatingOrigin.Origin, ship, playerHeat, battle, aspect, hudLines);
                renderer.SetHudLines(CollectionsMarshal.AsSpan(hudLines));

                renderer.DrawFrame(accumulator.Alpha);
                renderedFrames++;

                if (maxFrames > 0 && renderedFrames == maxFrames / 2)
                {
                    log.Info("Spectre", "Scripted resize → 960x540 to exercise swapchain recreation.");
                    window.Size = new Vector2D<int>(960, 540);
                }

                if (maxFrames > 0 && renderedFrames >= maxFrames)
                {
                    log.Info("Spectre", $"Reached {maxFrames}-frame limit; closing. " +
                                         $"Survivors — Coalition {battle.AliveCount(Faction.Coalition)}, " +
                                         $"Severance {battle.AliveCount(Faction.Severance)}.");
                    window.Close();
                }
            }

            renderer.WaitIdle();
            // Dispose the input context BEFORE the window: GLFW unhooks its callbacks from the live window, so
            // tearing the window down first leaves it marshalling callbacks onto a dead handle (a hard crash).
            input.Dispose();
            audio?.Dispose();
            renderer.Dispose();
            window.Dispose();

            bool hadErrors = validationWatch.HasAtLeast(LogLevel.Error);
            log.Info("Spectre", hadErrors
                ? "FINISHED WITH VALIDATION ERRORS — see log above."
                : $"Clean run: {renderedFrames} frames, {time.StepCount} sim steps, no validation errors.");

            return hadErrors ? 1 : 0;
        }

        private const int VulkanInstanceBudget = 4096;

        // Faction tints for the instanced hulls.
        private static readonly Vector3 CoalitionColor = new(0.30f, 0.55f, 1.00f); // cool blue
        private static readonly Vector3 SeveranceColor = new(1.00f, 0.35f, 0.28f); // hostile red
        private static readonly Vector3 DebrisColor = new(0.45f, 0.45f, 0.48f);    // dead grey
        private static readonly Vector3 TracerColor = new(1.00f, 0.92f, 0.40f);    // muzzle yellow

        /// <summary>Draws up two fleets from the roster: fighters, a couple of corvettes, and a cruiser flagship
        /// per side, in two formations facing off across the origin.</summary>
        private static BattleSimulation BuildBattle(int seed)
        {
            var rng = new Random(seed);
            var ships = new List<Combatant>();

            void Wing(Faction faction, double centreX)
            {
                bool coalition = faction == Faction.Coalition;
                double Spread() => (rng.NextDouble() - 0.5) * 2600;
                Vector3d At() => new Vector3d(centreX + Spread() * 0.25, Spread(), Spread());

                // Light fighters use the Wasp hull, re-flagged to this wing's faction.
                for (int i = 0; i < 22; i++)
                    ships.Add(ShipFactory.Build(ShipCatalog.SeveranceWasp(), At(), faction));
                for (int i = 0; i < 6; i++)
                    ships.Add(ShipFactory.Build(coalition ? ShipCatalog.CoalitionCorvette() : ShipCatalog.SeveranceHornet(), At()));
                for (int i = 0; i < 2; i++)
                    ships.Add(ShipFactory.Build(coalition ? ShipCatalog.CoalitionCruiser() : ShipCatalog.SeveranceLocust(),
                        new Vector3d(centreX, (i - 0.5) * 400, (i - 0.5) * 600)));
            }

            // Two fleets within weapon reach so the brawl starts at once, on fantastical drives.
            Wing(Faction.Coalition, -1300);
            Wing(Faction.Severance, 1300);
            return new BattleSimulation(ships)
            {
                WeaponRange = 2200,
                MaxSpeed = 360,        // brisk dogfighting pace (the player's own drive is the fast one)
                MaxThrust = 1_500_000,
            };
        }

        // A ship's death: scatter debris and flash an explosion (hot white-orange cooling to red embers).
        private static void HandleNewKills(BattleSimulation battle, DebrisField debris, ParticleSystem particles, bool[] wasAlive)
        {
            IReadOnlyList<Combatant> ships = battle.Combatants;
            for (int i = 0; i < ships.Count; i++)
            {
                if (wasAlive[i] && !ships[i].Alive)
                {
                    Combatant dead = ships[i];
                    int chunks = Math.Clamp((int)dead.Radius, 6, 40);
                    debris.Spawn(dead.Body.Position, dead.Body.Velocity, dead.Body.Mass, chunks,
                        scatterSpeed: 12 + dead.Radius * 0.5, rng: SharedFxRng);

                    int sparks = Math.Clamp((int)(dead.Radius * 3), 24, 220);
                    particles.Burst(dead.Body.Position, dead.Body.Velocity * 0.4, sparks,
                        speed: 40 + dead.Radius * 1.5, life: 1.4f, size: (float)(dead.Radius * 0.7),
                        hot: new Vector3(6f, 3.2f, 1.2f), cool: new Vector3(1.6f, 0.25f, 0.05f), rng: SharedFxRng);

                    wasAlive[i] = false;
                }
            }
        }

        private static readonly Random SharedFxRng = new(1);

        // Bright engine-glow tint (>1 so the HDR bloom makes it flare behind each ship).
        private static readonly Vector3 EngineGlow = new(1.4f, 2.0f, 3.2f);

        private static void BuildInstances(
            BattleSimulation battle, DebrisField debris, ProjectileSystem projectiles, ParticleSystem particles,
            List<Vector3d> positions, List<Vector3> colors, List<float> scales)
        {
            positions.Clear();
            colors.Clear();
            scales.Clear();

            foreach (Combatant c in battle.Combatants)
            {
                if (!c.Alive) continue;
                positions.Add(c.Body.Position);
                colors.Add(c.Faction == Faction.Coalition ? CoalitionColor : SeveranceColor);
                scales.Add((float)(c.Radius * 2.0)); // unit hull -> ship-diameter hull

                // A glowing engine bloom trailing the direction of travel.
                double speed = c.Body.Velocity.Magnitude;
                if (speed > 1.0 && positions.Count < VulkanInstanceBudget)
                {
                    Vector3d back = c.Body.Velocity / speed;
                    positions.Add(c.Body.Position - back * (c.Radius * 1.3));
                    colors.Add(EngineGlow);
                    scales.Add((float)(c.Radius * 0.9));
                }
            }

            foreach (Projectile p in projectiles.Active)
            {
                if (positions.Count >= VulkanInstanceBudget) break;
                positions.Add(p.Position);
                colors.Add(TracerColor);
                scales.Add(10f);
            }

            foreach (Particle p in particles.Active)
            {
                if (positions.Count >= VulkanInstanceBudget) break;
                positions.Add(p.Position);
                colors.Add(ParticleSystem.ColorOf(p));
                scales.Add(ParticleSystem.SizeOf(p));
            }

            foreach (RigidBody chunk in debris.Active)
            {
                if (positions.Count >= VulkanInstanceBudget) break;
                positions.Add(chunk.Position);
                colors.Add(DebrisColor);
                scales.Add(6f);
            }
        }

        // --- HUD (build brief S14): a peripheral, geometric overlay built from the player + target state and
        // projected to screen space. Lines only (no font yet); colours kept bright so they read over bloom. ---
        private static void BuildHud(
            Matrix viewProj, Vector3d renderOrigin, RigidBody ship, Combat.HeatSink playerHeat,
            BattleSimulation battle, float aspect, List<HudVertex> lines)
        {
            lines.Clear();
            var hud = new Vector3(0.25f, 0.9f, 1.1f);
            var prograde = new Vector3(0.4f, 1.1f, 0.55f);
            var targetCol = new Vector3(1.2f, 0.45f, 0.35f);
            float ax = aspect <= 0 ? 1f : 1f / aspect; // scale x so shapes stay square

            void Line(Vector2 a, Vector2 b, Vector3 c)
            {
                lines.Add(new HudVertex(a, c));
                lines.Add(new HudVertex(b, c));
            }

            bool Project(Vector3d world, out Vector2 ndc)
            {
                Vector3d r = world - renderOrigin;
                double x = r.X, y = r.Y, z = r.Z;
                double w = viewProj[3, 0] * x + viewProj[3, 1] * y + viewProj[3, 2] * z + viewProj[3, 3];
                if (w <= 1e-4)
                {
                    ndc = default;
                    return false;
                }
                double cx = viewProj[0, 0] * x + viewProj[0, 1] * y + viewProj[0, 2] * z + viewProj[0, 3];
                double cy = viewProj[1, 0] * x + viewProj[1, 1] * y + viewProj[1, 2] * z + viewProj[1, 3];
                ndc = new Vector2((float)(cx / w), (float)(cy / w));
                return true;
            }

            // Boresight: a gapped cross at screen centre.
            Line(new Vector2(-0.06f * ax, 0), new Vector2(-0.02f * ax, 0), hud);
            Line(new Vector2(0.02f * ax, 0), new Vector2(0.06f * ax, 0), hud);
            Line(new Vector2(0, -0.06f), new Vector2(0, -0.02f), hud);
            Line(new Vector2(0, 0.06f), new Vector2(0, 0.02f), hud);

            // Prograde marker: a small diamond where the velocity vector points.
            double speed = ship.Velocity.Magnitude;
            if (speed > 1.0 && Project(ship.Position + ship.Velocity / speed * 3000.0, out Vector2 pg))
            {
                float r = 0.022f;
                var up = pg + new Vector2(0, r);
                var dn = pg + new Vector2(0, -r);
                var rt = pg + new Vector2(r * ax, 0);
                var lf = pg + new Vector2(-r * ax, 0);
                Line(up, rt, prograde); Line(rt, dn, prograde); Line(dn, lf, prograde); Line(lf, up, prograde);
            }

            // Nearest hostile: corner brackets + a hull bar.
            Combatant? target = NearestHostile(battle, ship.Position);
            if (target is not null && Project(target.Body.Position, out Vector2 t))
            {
                float s = 0.05f, e = 0.025f; // bracket half-size and arm length
                float sx = s * ax, ex = e * ax;
                // Four corners, each an L.
                Line(new Vector2(t.X - sx, t.Y + s), new Vector2(t.X - sx + ex, t.Y + s), targetCol);
                Line(new Vector2(t.X - sx, t.Y + s), new Vector2(t.X - sx, t.Y + s - e), targetCol);
                Line(new Vector2(t.X + sx, t.Y + s), new Vector2(t.X + sx - ex, t.Y + s), targetCol);
                Line(new Vector2(t.X + sx, t.Y + s), new Vector2(t.X + sx, t.Y + s - e), targetCol);
                Line(new Vector2(t.X - sx, t.Y - s), new Vector2(t.X - sx + ex, t.Y - s), targetCol);
                Line(new Vector2(t.X - sx, t.Y - s), new Vector2(t.X - sx, t.Y - s + e), targetCol);
                Line(new Vector2(t.X + sx, t.Y - s), new Vector2(t.X + sx - ex, t.Y - s), targetCol);
                Line(new Vector2(t.X + sx, t.Y - s), new Vector2(t.X + sx, t.Y - s + e), targetCol);

                float hull = (float)(target.Hull.Hp / target.Hull.MaxHp);
                float barY = t.Y - s - 0.02f;
                Line(new Vector2(t.X - sx, barY), new Vector2(t.X - sx + 2 * sx * hull, barY), targetCol);
            }

            // Bottom-left gauges: speed and weapon heat.
            float speedFrac = (float)Math.Min(speed / 300.0, 1.0);
            Line(new Vector2(-0.9f, -0.84f), new Vector2(-0.9f + 0.28f * speedFrac, -0.84f), hud);
            float heatFrac = (float)playerHeat.Fraction;
            var heatCol = heatFrac > 0.8f ? new Vector3(1.3f, 0.3f, 0.2f) : new Vector3(1.0f, 0.7f, 0.2f);
            Line(new Vector2(-0.9f, -0.89f), new Vector2(-0.9f + 0.28f * heatFrac, -0.89f), heatCol);
        }

        private static Combatant? NearestHostile(BattleSimulation battle, Vector3d from)
        {
            Combatant? best = null;
            double bestSq = double.PositiveInfinity;
            foreach (Combatant c in battle.Combatants)
            {
                if (!c.Alive || c.Faction != Faction.Severance) continue;
                double d = (c.Body.Position - from).MagnitudeSquared;
                if (d < bestSq) { bestSq = d; best = c; }
            }
            return best;
        }

        private static int ParseMaxFrames(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--frames" && int.TryParse(args[i + 1], out int n) && n > 0) return n;
            }

            string? env = Environment.GetEnvironmentVariable("SPECTRE_FRAMES");
            if (int.TryParse(env, out int envN) && envN > 0) return envN;

            return 0; // 0 = run until the window is closed
        }
    }
}
