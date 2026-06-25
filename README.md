# SPECTRE

> A single-player, from-scratch C# space game: fast, lethal fleet combat through a debris graveyard on
> the outside, and a dread-soaked descent into the corpse of a 3.5 km dreadnought on the inside. Built on
> a hand-written Vulkan engine, not an off-the-shelf one.

This repository is also a **teaching artefact**: the code and docs are written so a curious reader can
learn how a game engine and a game are built from base principles (build brief S4.5). If you are here to
learn, start with the architecture tour below, then read the source in the order the phases were built.

> **Status:** systems-complete. Every game system — Vulkan rendering foundation, Newtonian flight + floating
> origin, combat (shields/hull/damage/weapons/point-defence), sensors/EW, ships + battle AI, the tumbling
> wreck + interior streaming, missions/difficulty/campaign, the HUD and menu models, adaptive audio, and
> pooled in-world projectiles/debris — has a tested headless core (**988 tests** across `RP.Math`,
> `RP.Game`, `RP.Spectre`). What remains is the **GPU/art layer**: drawing the battles, projectiles, debris
> and wreck interior, and the text-rendering pass for the HUD/menus — the parts that need a human watching the
> screen. The per-phase story lives in [`BUILD_LOG.md`](BUILD_LOG.md); the design spec is the build brief.

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
dotnet run --project RP.Spectre              # opens the window and flies (WASD/mouse; T toggles assist)
dotnet run --project RP.Spectre -- --frames 200   # bounded run: auto-closes, asserts zero validation errors
```

## Architecture tour (grows with the build)

1. **The simulation loop** — `RP.Game.Core.FixedTimestepAccumulator`. Why a fixed timestep, how the
   accumulator drains real time into constant-size steps, the spiral-of-death clamp, and the `Alpha`
   that interpolates rendering between steps. This is the first thing to read.
2. **The Vulkan rendering path** — `RP.Game.Graphics.Vulkan` — device/swapchain/pipelines, dynamic rendering,
   GPU instancing, frustum culling, frames-in-flight. (The instanced *draw* of battles/debris/interior is the
   remaining GPU work.)
3. **Newtonian flight & the floating origin** — `RP.Game.Physics.RigidBody` + `RP.Game.Scene.FloatingOrigin`,
   flown by `RP.Spectre.World.ShipController`: 6-DoF physics in double precision, rebased to float near the
   camera so a kilometre-scale world stays jitter-free.
4. **Combat** — `RP.Spectre.Combat`: shields as a clock, fragile hull, per-weapon damage routing, heat/
   capacitor fire discipline, facet shields, ballistic lead, point-defence — and crash damage from real
   impact energy. Driven at scale by `RP.Spectre.Ships.BattleSimulation`, fed by the data-driven roster
   (`ShipCatalog`/`ShipFactory`) and fired in-world through `RP.Spectre.World.ProjectileSystem`.
5. **The descent** — `RP.Game.Scene.ReferenceFrame` + `ChunkStreamer`, wrapped by `RP.Spectre.World.Wreck`:
   the *Tantalus* tumbles slowly while its interior streams in around the player, in the hull's own local
   frame, even as it rotates beneath them.
6. **The game around the fight** — missions/difficulty/campaign (`RP.Spectre.Missions`), the HUD and menu
   models (`RP.Spectre.Hud`, `RP.Game.Mechanics.Menu` + `RP.Spectre.Shell`), save/resume
   (`RP.Spectre.State`), and the adaptive-audio dread director (`RP.Spectre.Audio`).

## Repository layout

```
Spectre.sln                  integrated solution (Math + Game + Spectre)
RP.Spectre/                  the game executable (this title's content + entry point)
RP.Spectre.Tests/            this title's tests
docs/MATH_INVENTORY.md       RP.Math public surface + pinned conventions
BUILD_LOG.md                 per-phase narrative, decisions, deviations, blockers
CREDITS.md                   (added with the first sourced asset) third-party assets + licences
```
