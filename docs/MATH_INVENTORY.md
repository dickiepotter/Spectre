# RP.Math — API inventory and pinned conventions (Phase 0)

> **Why this document exists.** The build brief (S0, S4.4, S19-Phase 0) requires that before a single
> line of new maths is written, the full public surface of the existing `RP.Math` library and its
> conventions are read and recorded — so the engine inherits them without surprises. Conventions
> mismatches (handedness, matrix order, angle units) are the most expensive bugs in a from-scratch 3D
> engine, so they are pinned here once and held to everywhere in `RP.Game` and `RP.Spectre`.
>
> Source read: `D:\Source\personal-github\Math\RP.Math` (assembly + root namespace **`RP.Math`**;
> targets `net8.0;netstandard2.0`). The library's own `README.md` is an excellent ~2,000-line tutorial;
> this file is the engine-facing distillation, not a replacement for it.

---

## 1. The conventions the engine inherits (PINNED — do not deviate)

| Concern | RP.Math's position | What the engine does |
|---|---|---|
| **Precision** | Everything is **`double`**. There is a single 3-component `Vector` (double x,y,z); there is **no** float type and **no** `Vector2`/`Vector4`. | Simulate in `double` (the "truth"). Convert to `float` only at the Vulkan upload boundary. The brief's float `Vector3` need is met by **adding a float vector family to Math** (see §4). |
| **Angles** | **Radians** internally, always, wrapped in the `Angle` value type (degrees/radians/gradians on the way in/out). A bare `double` implicitly means radians. | Radians everywhere. Store/compute angles as `Angle` at API edges where unit confusion is possible. |
| **Mutability** | Immutable value objects. Every operation returns a **new** value; nothing mutates in place. Static and instance forms of each op agree (instance calls static). | Engine state structs (positions, velocities) follow the same immutable-value style; the simulation replaces values rather than mutating maths types. |
| **Handedness** | **Convention-agnostic.** `OrthogonalAxes` names the choice and *derives* handedness from the axis labels. Presets: `OpenGL`/`Maya`/`Godot`/`MathsYUp` (right, up, **near**); `DirectX`/`Direct3D`/`Unity` (right, up, **far**); `Blender`/`Max3ds`/`MathsZUp`; `Unreal`. | **Engine decision (recorded in BUILD_LOG):** world space is **right-handed, Y-up, Z-toward-viewer** = `OrthogonalAxes.OpenGL`/`MathsYUp`. The **Vulkan backend** applies the clip-space Y-flip and `[0,1]` depth at the projection step — world/sim code never sees Vulkan's quirks. |
| **Matrix storage** | 4×4 of `double`, **row-indexed** `m[row, col]` (backed by `double[,]`). | Same. Upload to GPU as the layout the shader expects (transpose at the boundary if needed; decided in Phase 1). |
| **Matrix–vector** | **Column-vector** convention: `v' = M · v` (`operator *(Matrix, Vector)` treats `v` as a column). | Same: `v' = M · v`. |
| **Composition order** | `(A · B) · v` applies **B first, then A** (right-to-left). E.g. `(T · S) · v` scales then translates. | Same. Build transforms as `Projection · View · Model`. |
| **Perspective divide** | `Matrix * Vector` **throws** `ArithmeticException` if the resulting homogeneous `w ≠ 1`. It does **not** perform the perspective divide. | The renderer must do its own `w`-divide for projected points; do not push clip-space points through `Matrix * Vector`. |
| **Degenerate inputs** | Deliberate, documented: strict `Normalize()` **throws** on zero length; `NormalizeOrDefault()` returns zero. Many comparisons take a **tolerance**; `…OrDefault` safe variants exist. Special-cased for ∞/NaN components. | Prefer the `…OrDefault` forms in hot paths that can see degenerate data (zero velocity, coincident points). |

---

## 2. Public surface, by area

Folders under `RP.Math/` map to sub-namespaces (all under root `RP.Math`).

- **Core** — `Vector` (double 3D; the foundation: arithmetic, dot, cross, mixed/triple product,
  magnitude & magnitude², normalize/`NormalizeOrDefault`, distance, angle-between via stable
  `atan2(|a×b|, a·b)`, lerp/`Slerp`, `MoveTowards`, `ClampMagnitude`, `Project`/`Reject`,
  `Reflect`/`Reflection`, component min/max/clamp, rotate-about-axis, tuple/deconstruct, constants).
  `Matrix` (4×4: +,−,scalar·, M·M, M·v, transpose, inverse, determinant, `TranslationMatrix`,
  `RotationMatrixAboutX/Y/Z`, `LookAt`, `PerspectiveFieldOfView`, `PerspectiveOffCenter`,
  `Orthographic`, `OrthographicOffCenter`). `Angle` (units, classification, trig, constants).
- **Orientation** — `Quaternion` (slerp/nlerp, axis-angle, Euler, matrix, look-rotation, conjugate/inverse),
  `AxisAngle`, `Rotation` (Euler X/Y/Z), `Attitude` (yaw/pitch/roll, convention-aware via `OrthogonalAxes`),
  `Pose` (position + orientation), `OrthogonalAxes` + `Handedness`. The four rotation encodings
  (`Quaternion`/`AxisAngle`/`Rotation`/`Matrix`) interconvert losslessly via explicit casts.
- **Geometry** — `Line`, `Ray`, `LineSegment`, `Chord`, `Plane` (closest-point, signed distance,
  line/plane intersection, reflection).
- **Bounds** — `Box` (AABB), `BoundingSphere` — quick-rejection volumes (relevant to frustum culling).
- **Shapes** — conceptual solids/areas (`Sphere`, `Cuboid`, `Cylinder`, `Cone`, `Capsule`, `Ellipsoid`,
  `Torus`, `Circle`, `Triangle`, `Rectangle`, `Ellipse`, `Annulus`, `Sector`) **and** their `Placed…`
  partners (placed by a `Pose`; add containment, closest-point, line/ray hits). Plus `PlacedPolygon`,
  `PlacedTetrahedron`.
- **Curves** — `Bezier`, `Hermite`, `CatmullRom`.
- **Numerics** — `DoubleExtension` (tolerant compare, ULP), `ExpandedDouble`, `PolynomialRoots`
  (real root solvers — useful for ballistic lead/intercept), `Tolerance`, `UlongExtension`.

---

## 3. What the engine will lean on most

- **Transforms & cameras:** `Matrix.LookAt` / `PerspectiveFieldOfView` (Phase 1 camera controller).
- **Orientation:** `Quaternion` for all ship/camera rotation (no gimbal lock); `Pose` for placement.
- **Collision maths:** `Ray`/`Plane`/`BoundingSphere`/`Box`/`Placed…` intersection (Phase 4 collision).
- **Intercept maths:** `PolynomialRoots` for ballistic lead indicators (Phase 6).
- **Stable angle/Slerp:** already hardened against NaN at the (anti)parallel poles.

---

## 4. Gaps vs. the brief, and the planned RP.Math extensions

The brief (S4.1, S16) assumes some maths that `RP.Math` does **not** yet have. Per the completionist
principle (S4.3) these are **added to Math**, finished with edge cases + tests + tutorial docs, not
reimplemented locally. Confirmed gaps:

1. **Float vector family (DECIDED — full family).** Add `Vector2/3/4` (float) and `Vector2/3/4` double
   counterparts with the complete operation set, edge cases, and tests. Needed for: GPU vertex/instance
   data (float) and the floating-origin "true position" (double). *Naming to confirm at check-in* — the
   existing double 3D type is `Vector`; proposed: keep `Vector` as the canonical double-3D and introduce
   `Vector2`/`Vector4` (double) + `Vector2F`/`Vector3F`/`Vector4F` (float), **or** the brief's literal
   `Vector3`(float)/`Vector3d`(double) scheme. This renames nothing existing either way.
2. **Numerical integrators (GAP).** The brief (S4.1) implies Math holds "numerical integration methods",
   but there are **no** ODE integrators (explicit/semi-implicit Euler, RK4, Verlet). These are added to
   Math in Phase 3; Game.Physics holds only the *driver* that calls them.
3. **Frustum type (minor).** `Box`/`BoundingSphere` exist; an explicit 6-plane `Frustum` + AABB/sphere
   tests for culling are added (Math holds the maths; Game holds the culling system) in Phase 2.
4. **Double-precision rebasing helpers (minor).** Floating-origin needs double position ± float offset
   conversions; these come with the double vector family in (1).

> Everything in §4 is a **forward plan**, not done yet. Phase 0 only inventories and pins conventions.
> The float vector family is the first Math extension and will be delivered one type at a time, pattern
> first, for review (it is the user's library).
