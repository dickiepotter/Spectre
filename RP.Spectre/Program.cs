namespace RP.Spectre
{
    using System;
    using RP.Game.Core;

    /// <summary>
    /// The Spectre executable's entry point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At Phase 0 there is no window yet: opening one needs the Vulkan device bring-up, which in turn
    /// needs the Vulkan SDK installed on the build machine (see <c>BUILD_LOG.md</c>). Rather than block,
    /// this entry point exercises the part of the engine that is already real and headlessly verifiable —
    /// the <see cref="FixedTimestepAccumulator"/> — by driving a short, simulated run and printing a
    /// summary. It is the smallest honest proof that "the loop runs on a stable fixed timestep" before a
    /// single pixel exists.
    /// </para>
    /// <para>
    /// When the renderer lands, this is replaced by: create window → create Vulkan device/swapchain →
    /// enter the real loop (poll input, drain fixed steps, render with interpolation). The loop shape
    /// below is exactly that real loop with the I/O ends stubbed by a synthetic clock.
    /// </para>
    /// </remarks>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("SPECTRE — Phase 0 headless loop check");
            Console.WriteLine("(no window yet; the Vulkan renderer arrives once the SDK is installed)\n");

            // 60 Hz simulation. We pretend the renderer is producing frames at a wobbly ~144 fps and
            // simulate two seconds of wall-clock time, to show steady stepping under uneven frame times.
            var accumulator = new FixedTimestepAccumulator(fixedDeltaSeconds: 1.0 / 60.0);
            var time = new GameTime(accumulator.FixedDeltaSeconds, totalSeconds: 0.0, stepCount: 0);

            const double simulatedWallClock = 2.0; // seconds
            double elapsed = 0.0;
            int frames = 0;

            // A deterministic, jittery frame time around 1/144 s — no real clock needed for a smoke run.
            var jitter = new Random(12345);

            while (elapsed < simulatedWallClock)
            {
                double frameSeconds = (1.0 / 144.0) * (0.5 + jitter.NextDouble()); // ~3.5–10.4 ms
                elapsed += frameSeconds;
                frames++;

                int steps = accumulator.Advance(frameSeconds);
                for (int i = 0; i < steps; i++)
                {
                    // This is where Update(time) would run physics, AI, combat... For Phase 0 we only
                    // advance the clock so the step count and total sim time can be checked.
                    time = time.Advanced();
                }

                // Here a real build would render: Lerp(previous, current, accumulator.Alpha).
            }

            Console.WriteLine($"Rendered {frames} frames over {elapsed:0.000}s of wall-clock time.");
            Console.WriteLine($"Simulation ran {time.StepCount} fixed steps ({time}).");
            Console.WriteLine($"Final interpolation alpha: {accumulator.Alpha:0.000}");
            Console.WriteLine($"\nExpected ~{simulatedWallClock * 60.0:0} steps at 60 Hz — frame-rate independent. OK.");
            return 0;
        }
    }
}
