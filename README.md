# SPECTRE

> A single-player, from-scratch C# space game: fast, lethal fleet combat through a debris graveyard on
> the outside, and a dread-soaked descent into the corpse of a 3.5 km dreadnought on the inside. Built on
> a hand-written Vulkan engine, not an off-the-shelf one.

This repository is also a **teaching artefact**: the code and docs are written so a curious reader can
learn how a game engine and a game are built from base principles (build brief S4.5). If you are here to
learn, start with the architecture tour below, then read the source in the order the phases were built.

> **Status:** early build. **Phase 0** (engine/solution foundation + Math inventory + fixed-timestep
> loop) is in progress; the Vulkan renderer is pending the Vulkan SDK install (see `BUILD_LOG.md`). The
> per-phase story lives in [`BUILD_LOG.md`](BUILD_LOG.md); the design spec is the build brief.

## The three layers

```
RP.Math      pure mathematics (existing library; double-precision, immutable, tutorial-grade)
   ▲
RP.Game      the engine: rendering, physics, audio, scene, mechanics — valid for ANY game
   ▲
RP.Spectre   this game: ships, weapons, the Tantalus wreck, missions, HUD, tuning data
```

Dependencies point strictly upward. `RP.Math` and `RP.Game` live in sibling repositories
(`../Math`, `../Game`); `Spectre.sln` references all three.

- Engine library overview: [`../Game/README.md`](../Game/README.md)
- Maths library inventory + the conventions the whole engine inherits: [`docs/MATH_INVENTORY.md`](docs/MATH_INVENTORY.md)

## Build, run, and test

Prerequisites: **.NET SDK 8+** (10 is fine). For the rendering phases you will also need the
**Vulkan SDK** installed (`glslc`/validation layers) — see `BUILD_LOG.md` for the current status.

```sh
# from this folder (D:\Source\personal-github\Spectre)
dotnet build Spectre.sln           # builds Math + Game + Spectre
dotnet test  Spectre.sln           # runs the full headless test suite
dotnet run --project RP.Spectre    # runs the game (currently a headless Phase-0 loop check)
```

## Architecture tour (grows with the build)

1. **The simulation loop** — `RP.Game.Core.FixedTimestepAccumulator`. Why a fixed timestep, how the
   accumulator drains real time into constant-size steps, the spiral-of-death clamp, and the `Alpha`
   that interpolates rendering between steps. This is the first thing to read.
2. *(planned)* **The Vulkan rendering path** — device/swapchain/pipelines, dynamic rendering, descriptor
   indexing, frames-in-flight.
3. *(planned)* **Newtonian flight & the floating origin** — 6-DoF physics in double precision, rebased to
   float near the camera so a kilometre-scale world stays jitter-free.
4. *(planned)* **Combat** — shields as a clock, fragile hull, crash damage from real impact energy.
5. *(planned)* **The descent** — streaming the wreck interior in its own slowly-tumbling local frame.

## Repository layout

```
Spectre.sln                  integrated solution (Math + Game + Spectre)
RP.Spectre/                  the game executable (this title's content + entry point)
RP.Spectre.Tests/            this title's tests
docs/MATH_INVENTORY.md       RP.Math public surface + pinned conventions
BUILD_LOG.md                 per-phase narrative, decisions, deviations, blockers
CREDITS.md                   (added with the first sourced asset) third-party assets + licences
```
