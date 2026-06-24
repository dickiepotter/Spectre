# SPECTRE — Build Log

A per-phase narrative of *what* was built, *why*, and every *deviation* from the build brief and every
*decision* made on an ambiguous point (brief S0 rule 4, S24). Read top-down as the story of the build.

---

## Phase 1 — Rendering foundation  *(in progress)*

### Done so far

- **SPIR-V build pipeline.** Shader sources (GLSL) live in `RP.Game/Rendering/Shaders`; an MSBuild step
  (`CompileShaders` + `EmbedShaders`) runs `glslc --target-env=vulkan1.3` and **embeds** the resulting
  SPIR-V into `RP.Game.dll`, so bytecode travels with the assembly and consumers need no loose files.
  glslc is located via `VULKAN_SDK` with a fallback to the known install path (recorded; generalise if
  the SDK moves).
- **Graphics pipeline + first triangle.** `VulkanRenderer.Pipeline.cs` (partial class): loads shader
  modules from the embedded SPIR-V, builds a pipeline using **dynamic rendering** (colour format via
  `PipelineRenderingCreateInfo`, no render-pass object) and **dynamic viewport/scissor** (so a resize
  needs no pipeline rebuild), and draws a 3-vertex triangle whose colours interpolate across the face.
  *Verified:* 150-frame run draws the triangle, survives the scripted resize, tears down clean, **zero
  validation errors**, exit 0.

### Next in Phase 1

Vertex/index buffers via a memory allocator → a `Mesh` type → depth buffering → a camera controller
producing view/projection from **RP.Math** → a basic lit shader → render a cube, then a grid of cubes.
This is where the **float vector family** (Vector3/Vector3d) gets added to RP.Math for vertex data.

---

## Phase 0 — Engine bring-up + Math inventory  *(COMPLETE — all acceptance criteria met)*

### Vulkan device bring-up (the rest of Phase 0)

Built the full Vulkan 1.3 spine in `RP.Game` (`Graphics/IRenderer`, `Graphics/Vulkan/VulkanRenderer`,
`Platform/VulkanWindow`), via Silk.NET 2.23. One file holds the whole spine while it is small enough to
read end-to-end, commented as a lesson: instance + validation messenger → `Logger`; surface; physical
device selection (graphics+present+swapchain, prefers discrete); logical device with **dynamic
rendering** + **synchronization2** (Vulkan 1.3); swapchain (sRGB format, mailbox/FIFO, clamped extent)
with image views; command pool/buffers; **frames-in-flight** (2) with semaphores + fences;
clear-to-colour each frame via dynamic rendering; swapchain **recreation on resize/minimise**; reverse-
order teardown.

**Acceptance — verified by a bounded run (`RP.Spectre --frames 200`, with a scripted mid-run resize):**

- ✅ Window opens; Vulkan 1.3 device + swapchain come up (selected *AMD Radeon Graphics*, 2 images @ 1280×720).
- ✅ Swapchain clears every frame (hue-cycling colour) on the stable 60 Hz fixed-timestep loop.
- ✅ Validation layers **ON** and routed to `Logger`; **zero validation errors** from our code (the only
  warnings are an external OBS overlay layer). 
- ✅ Scripted resize → 960×540 triggers swapchain recreation with no crash/leak/validation error.
- ✅ Clean teardown ("Renderer torn down cleanly"), process exits 0.
- ✅ Math inventory exists (`docs/MATH_INVENTORY.md`); `RP.Math ← RP.Game ← RP.Spectre` compiles clean.

`AllowUnsafeBlocks` enabled for `RP.Game` (Vulkan is a pointer-heavy C API); unsafe code is confined to
`Graphics/Vulkan`. The SPIR-V offline-compile step is verified working (`glslc`) and is wired into the
build in Phase 1 when the first shaders arrive.

### (foundation, earlier this phase)

### What landed this session

- **Three-layer solution wired and compiling** (`Math ← Game ← Spectre`), zero warnings:
  - `RP.Game` (new shared engine library) and `RP.Game.Tests` in the **Game** repo.
  - `RP.Spectre` (game executable) and `RP.Spectre.Tests` in the **Spectre** repo.
  - Two classic-format solutions: `Game.sln` (library standalone) and `Spectre.sln` (integrated, all
    five projects), referencing the existing `RP.Math` project across repos.
- **Math API inventory + pinned conventions** → `docs/MATH_INVENTORY.md` (brief S0/S4.4 deliverable).
- **Fixed-timestep loop** (`RP.Game.Core.FixedTimestepAccumulator` + `GameTime`) with tutorial-grade
  comments and **24 passing tests** covering the edge cases (spiral-of-death clamp, frame-rate
  independence, NaN/negative rejection, state-unpoisoned-after-bad-input). The Spectre exe runs it
  headlessly (297 jittery frames → exactly 120 fixed steps over 2 s).
- **Logging** (`RP.Game.Core.Logging`): `LogLevel`, `ILogSink`, `Logger` (level filter + multi-sink
  fan-out, thread-safe), `ConsoleLogSink`, and `CollectingLogSink` (for tests + headless "did anything
  go wrong?" checks). Built now because the Vulkan debug messenger must route validation output here
  (brief S4.2) — so it is ready to wire the instant the SDK lands. **9 passing tests.**
- **Test total: 34 passing, 0 failing.**

### Decisions recorded (brief S24 + ambiguities)

| # | Decision | Rationale |
|---|---|---|
| D0.1 | **Namespace/assembly: `RP.Game`, `RP.Spectre`** (root namespace = assembly name, area sub-namespaces beneath). | Mirrors `RP.Math` exactly so the libraries are siblings (brief S4.1, S24.2). |
| D0.2 | **Target `net8.0`** for Game/Spectre. | Matches Math's primary TFM; Silk.NET/Vulkan fully supported; .NET 10 SDK present can still build it. |
| D0.3 | **Test stack: MSTest + FluentAssertions, classic VSTest runner** — *deviation* from the brief's "xUnit or NUnit" (S20). | Consistency with the existing `RP.Math.Tests` wins; `dotnet test` works without the .NET 10 Testing-Platform opt-in. |
| D0.4 | **Classic `.sln` format** (forced via `--format sln`). | .NET 10 defaults to `.slnx`; Math uses `.sln` and the brief names `Spectre.sln`. Sibling consistency. |
| D0.5 | **World convention: right-handed, Y-up, Z-toward-viewer** (`OrthogonalAxes.OpenGL`/`MathsYUp`). The Vulkan backend applies the clip-space Y-flip and `[0,1]` depth at projection. | Math is convention-agnostic, so the engine must pick one. Y-up RH is the most common teaching convention and matches Math's default illustrations; isolating Vulkan's clip quirks to the backend keeps higher layers clean (brief S4.2, S4.4). |
| D0.6 | **Math stays double-precision as the truth; add a *full float vector family* to Math.** Naming (user-chosen, brief's scheme): `Vector2/3/4` = float, `Vector2d/3d/4d` = double, and the existing `Vector` becomes an **alias for `Vector3d`**. Delivered pattern-first for review. | User-selected. Sim/physics/true-position in double; float only at the GPU boundary (brief S4.1, S5, S16; completionist S4.3). **Implication:** "Vector = alias for Vector3d" means renaming the existing `Vector` struct → `Vector3d` and providing `Vector` as a compatibility alias — this touches existing Math code/tests, so it is done as a dedicated, fully-tested change, not rushed. |
| D0.7 | **Spectre is a single exe project** (game logic + entry point) for now; split into a logic library if it grows. | Avoid premature project splitting (brief S17). Test project references the exe directly. |

### Deviations from the brief

- **D0.3** test framework (MSTest vs xUnit/NUnit) — see above.
- **Phase 0 Vulkan acceptance not yet met** — see blocker below. The non-Vulkan Phase 0 deliverables
  (solution, Math inventory, fixed-timestep loop + tests) are complete.

### Blockers / needs-user-action

- **Vulkan SDK — RESOLVED.** Now installed at `C:\VulkanSDK\1.4.350.0`. Verified working this session:
  `glslc v2026.2` compiles GLSL → SPIR-V (the brief's required offline compile step), and the
  `VK_LAYER_KHRONOS_validation` layer is present. `VULKAN_SDK` was not yet exported in the current shell
  (a fresh shell, or `setx VULKAN_SDK C:\VulkanSDK\1.4.350.0`, picks it up); the loader also finds the
  layer via the registry. **The Vulkan device bring-up is no longer blocked.**
  - History: the initial `winget install KhronosGroup.VulkanSDK` downloaded + hash-verified (309 MB) but
    its elevated (UAC) step failed in the non-interactive session; the SDK was subsequently installed.

### Confirmed Math conventions (full detail in `docs/MATH_INVENTORY.md`)

Double precision; radians; immutable values; matrix is row-indexed `m[row,col]`, **column-vector**
`v' = M·v`, right-to-left composition; `Matrix * Vector` throws if `w ≠ 1` (renderer does its own
perspective divide); handedness is chosen via `OrthogonalAxes` (engine picks Y-up right-handed).

### Next (Phase 0 completion)

1. *(needs SDK — user installing)* Vulkan 1.3 device bring-up: instance + validation→`Logger`,
   device/queues, swapchain with resize/minimise recreation, command pools, frames-in-flight sync,
   memory allocator, clear-to-colour. The `Logger` (above) is ready to receive validation output.
2. The **float vector family** in Math (`Vector3` float first, as the reviewable pattern) — needed from
   Phase 1 (vertex data) / Phase 3 (physics), not for the Vulkan device bring-up, so it follows (1).
