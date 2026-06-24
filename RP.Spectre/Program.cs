namespace RP.Spectre
{
    using System;
    using System.Diagnostics;
    using RP.Game.Core;
    using RP.Game.Core.Logging;
    using RP.Game.Graphics;
    using RP.Game.Graphics.Vulkan;
    using RP.Game.Platform;
    using Silk.NET.Maths;
    using Silk.NET.Windowing;

    /// <summary>
    /// The Spectre executable's entry point: open a window, bring up the Vulkan renderer, and run the
    /// fixed-timestep loop, clearing the screen to a slowly-cycling colour each frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the Phase 0 milestone made real (build brief S19): a window that clears every frame on a
    /// stable fixed-timestep loop, with validation layers on and routed to the engine logger, surviving
    /// resize/minimise, and tearing down cleanly. There is no gameplay yet — the colour cycle simply
    /// proves the loop is running and the GPU is presenting.
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

            log.Info("Spectre", "Phase 0 — window + Vulkan clear loop." +
                                 (maxFrames > 0 ? $" Auto-closing after {maxFrames} frames." : string.Empty));

            IWindow window = VulkanWindow.Create("SPECTRE — Phase 0", 1280, 720);
            window.Initialize();

            using IRenderer renderer = new VulkanRenderer(window, log, enableValidation: true);

            // The window owns the truth about its size; tell the renderer to rebuild the swapchain on change.
            window.FramebufferResize += (Vector2D<int> _) => renderer.NotifyResize();

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

                int steps = accumulator.Advance(frameSeconds);
                for (int i = 0; i < steps; i++)
                {
                    time = time.Advanced();
                    // (Update(time) — physics/AI/combat — goes here in later phases.)
                }

                // Cycle the clear colour from the simulation clock so the screen visibly changes each frame.
                renderer.ClearColor = HueToRgb(time.TotalSeconds * 0.1);
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

        /// <summary>
        /// A tiny HSV→RGB for a fully-saturated hue sweep, so the clear colour cycles through the spectrum.
        /// Kept local and trivial; real colour work later lives in the engine, built on RP.Math.
        /// </summary>
        private static (float R, float G, float B, float A) HueToRgb(double hueTurns)
        {
            double h = (hueTurns % 1.0 + 1.0) % 1.0 * 6.0; // 0..6
            double x = 1.0 - Math.Abs(h % 2.0 - 1.0);
            (double r, double g, double b) = (int)h switch
            {
                0 => (1.0, x, 0.0),
                1 => (x, 1.0, 0.0),
                2 => (0.0, 1.0, x),
                3 => (0.0, x, 1.0),
                4 => (x, 0.0, 1.0),
                _ => (1.0, 0.0, x),
            };

            return ((float)r, (float)g, (float)b, 1f);
        }
    }
}
