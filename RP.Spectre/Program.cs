namespace RP.Spectre
{
    using System;
    using System.Diagnostics;
    using RP.Game.Audio;
    using RP.Game.Core;
    using RP.Game.Core.Logging;
    using RP.Game.Graphics.Vulkan;
    using RP.Game.Physics;
    using RP.Game.Platform;
    using RP.Game.Scene;
    using RP.Math;
    using RP.Spectre.World;
    using Silk.NET.Input;
    using Silk.NET.Maths;
    using Silk.NET.Windowing;

    /// <summary>
    /// The Spectre executable's entry point: open a window, bring up the Vulkan renderer, and run the
    /// fixed-timestep loop, drawing a lit, spinning 3D cube each frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the Phase 0/1 milestone made real (build brief S19): a window rendering a lit, moving 3D
    /// scene through our from-scratch Vulkan renderer, with all transforms from RP.Math, validation layers
    /// on and routed to the engine logger, surviving resize/minimise, and tearing down cleanly.
    /// </para>
    /// <para>
    /// Pass <c>--frames N</c> (or set <c>SPECTRE_FRAMES=N</c>) to auto-close after N rendered frames. That
    /// turns the otherwise-interactive window into a bounded run the build can execute unattended and then
    /// assert that no Vulkan validation error was logged — a headless-ish check of the "smoke pass" that
    /// would normally need a human watching the screen.
    /// </para>
    /// </remarks>
    public static class Program
    {
        public static int Main(string[] args)
        {
            int maxFrames = ParseMaxFrames(args);

            // A collecting sink lets us scan after the run for any validation error; the console sink shows
            // lifecycle and validation output live.
            var validationWatch = new CollectingLogSink();
            var log = new Logger(new ConsoleLogSink(), validationWatch) { MinimumLevel = LogLevel.Info };

            log.Info("Spectre", "Phase 3 — Newtonian flight (WASD thrust, mouse steer, Q/E roll, T assist)." +
                                 (maxFrames > 0 ? $" Auto-closing after {maxFrames} frames." : string.Empty));

            IWindow window = VulkanWindow.Create("SPECTRE — Phase 1", 1280, 720);
            window.Initialize();

            using var renderer = new VulkanRenderer(window, log, enableValidation: true);

            // The window owns the truth about its size; tell the renderer to rebuild the swapchain on change.
            window.FramebufferResize += (Vector2D<int> _) => renderer.NotifyResize();

            // The player ship: a Newtonian rigid body starting just outside the cube grid, looking in.
            var ship = new RigidBody { Position = new Vector3d(0, 4, 25) };
            var shipController = new ShipController(ship);
            var floatingOrigin = new FloatingOrigin(rebaseThreshold: 4096);

            // In bounded mode there is no pilot, so script a fast burn to prove the ship can fly tens of km
            // and the floating origin rebases without precision loss or validation errors.
            bool scriptedFlight = maxFrames > 0;
            if (scriptedFlight) ship.Velocity = new Vector3d(0, 0, -8000); // 8 km/s along the hull

            // Controls: WASD thrust/strafe, Space/Ctrl up/down, mouse steer, Q/E roll, Shift boost,
            // T toggles flight-assist.
            using IInputContext input = window.CreateInput();
            IKeyboard? keyboard = input.Keyboards.Count > 0 ? input.Keyboards[0] : null;
            IMouse? mouse = input.Mice.Count > 0 ? input.Mice[0] : null;

            // Audio bring-up: open OpenAL and play a positional tone off to one side so it pans as you fly.
            // Guarded — a machine with no audio device must not stop the game.
            AudioEngine? audio = null;
            try
            {
                audio = new AudioEngine(log);
                audio.PlayAt(AudioEngine.GenerateSineTone(440f, 2.0f), 44100, new Vector3(12, 0, 0), gain: 1f);
            }
            catch (Exception ex)
            {
                log.Warning("Audio", $"Audio unavailable, continuing without it: {ex.Message}");
            }

            // 60 Hz simulation, decoupled from however fast the GPU presents.
            var accumulator = new FixedTimestepAccumulator(fixedDeltaSeconds: 1.0 / 60.0);
            var time = new GameTime(accumulator.FixedDeltaSeconds, 0.0, 0);

            var clock = Stopwatch.StartNew();
            double lastSeconds = 0.0;
            long renderedFrames = 0;

            while (!window.IsClosing)
            {
                window.DoEvents(); // pump OS messages (resize, close, input)

                double now = clock.Elapsed.TotalSeconds;
                double frameSeconds = now - lastSeconds;
                lastSeconds = now;

                // Sample input once per frame into a thrust/torque intent for this frame's fixed steps.
                if (keyboard is not null && mouse is not null)
                {
                    shipController.ReadControls(keyboard, mouse);
                }

                int steps = accumulator.Advance(frameSeconds);
                for (int i = 0; i < steps; i++)
                {
                    time = time.Advanced();
                    if (scriptedFlight)
                    {
                        ship.Integrate(accumulator.FixedDeltaSeconds); // coast the scripted burn
                    }
                    else
                    {
                        shipController.FixedStep(accumulator.FixedDeltaSeconds); // 6-DoF physics on a fixed step
                    }
                }

                // Floating origin follows the ship, so rendering stays jitter-free however far you fly.
                if (floatingOrigin.MaybeRebase(ship.Position))
                {
                    log.Info("World", $"Floating-origin rebased at ~{ship.Position.Magnitude:0} m out — render space reset, no jitter.");
                }
                renderer.RenderOrigin = floatingOrigin.Origin;

                // Cockpit camera rides the ship: render-space position, looking along the hull.
                renderer.Camera.Position = floatingOrigin.ToRenderSpace(ship.Position);
                renderer.Camera.Target = renderer.Camera.Position + ship.Forward;
                renderer.Camera.Up = ship.Up;

                // Keep the audio listener on the camera so the positional tone pans as you move.
                if (audio is not null)
                {
                    Vector3d forward = (renderer.Camera.Target - renderer.Camera.Position).NormalizeOrDefault();
                    audio.SetListener(
                        (Vector3)renderer.Camera.Position, Vector3.Zero, (Vector3)forward, Vector3.UnitY);
                }

                // Spin the cube from the simulation clock: a full turn about Y every ~6 s, plus a slower
                // tumble about X so all faces come into view.
                double spin = time.TotalSeconds;
                renderer.ModelTransform =
                    Matrix.RotationMatrixAboutYAxis(spin) * Matrix.RotationMatrixAboutXAxis(spin * 0.5);

                renderer.DrawFrame(accumulator.Alpha);
                renderedFrames++;

                // In bounded mode, force one resize halfway through to exercise swapchain recreation —
                // the Phase 0 acceptance "resizing does not crash or leak", made automatic.
                if (maxFrames > 0 && renderedFrames == maxFrames / 2)
                {
                    log.Info("Spectre", "Scripted resize → 960x540 to exercise swapchain recreation.");
                    window.Size = new Vector2D<int>(960, 540);
                }

                if (maxFrames > 0 && renderedFrames >= maxFrames)
                {
                    log.Info("Spectre", $"Reached {maxFrames}-frame limit; closing.");
                    window.Close();
                }
            }

            renderer.WaitIdle();
            audio?.Dispose();
            // `using` disposes the renderer (clean Vulkan teardown) before the window is destroyed below.
            renderer.Dispose();
            window.Dispose();

            bool hadErrors = validationWatch.HasAtLeast(LogLevel.Error);
            log.Info("Spectre", hadErrors
                ? "FINISHED WITH VALIDATION ERRORS — see log above."
                : $"Clean run: {renderedFrames} frames, {time.StepCount} sim steps, no validation errors.");

            return hadErrors ? 1 : 0;
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
