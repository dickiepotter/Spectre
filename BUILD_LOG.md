# SPECTRE — Build Log

A per-phase narrative of *what* was built, *why*, and every *deviation* from the build brief and every
*decision* made on an ambiguous point (brief S0 rule 4, S24). Read top-down as the story of the build.

---

## Phase 21 — First gameplay on screen: the battle, drawn

- **Renderer instancing is now fed from the game, not a hardcoded grid.** `VulkanRenderer.SetInstances(world
  positions, colours, scales)` replaces the internal cube grid: the game hands the renderer the live scene
  each frame (true double positions, rebased through the floating origin, frustum-culled). Per-instance
  **scale** was added end to end — `InstanceData` gains a `Scale`, the pipeline a 6th vertex attribute
  (`location = 5`, `R32_SFLOAT`), and `mesh.vert` scales the unit cube before placing it — so one cube can be
  a fighter or a capital.
- **`Program.cs` now draws a live fleet battle.** Two fleets drawn from the roster (`ShipFactory` +
  `ShipCatalog`) are fought headless by `BattleSimulation`; each frame the living hulls (coloured by faction,
  sized by class) and the **debris of the dead** (`DebrisField`, bursting on each kill) are pushed to the
  renderer, while the player flies the cockpit camera freely through it. This is the visible smoke pass for
  Phases 7/14/18 at once.
- *Fixed:* a hard crash on exit — the Silk.NET input context was being disposed *after* the window, leaving
  GLFW marshalling callbacks onto a dead handle; it is now disposed first.
- *Verified (human + bounded run):* `--frames 150` renders the battle, survives the scripted resize, tears
  down cleanly and reports **no validation errors** (exit 0); both fleets take attrition (9 v 9 survivors at
  the cap). 208 solution tests still green.
- *Still cubes, deliberately:* ships/debris are coloured scaled cubes, not meshes; HUD/menu **text** and real
  art remain the outstanding GPU/art work. But the game now *shows* its tested systems instead of a demo cube.

---

## Phase 20 — Systems complete: capstone integration + state of the build

- **Capstone playthrough** (`RP.Spectre.Tests.PlaythroughTests`): one campaign beat played **end to end,
  headless**, exercising the whole stack at once — the data-driven roster (`ShipFactory`), difficulty-scaled
  spawns (`Encounter`), `BattleSimulation`, the `Mission` rules, the `HudModel` snapshot and the
  `TensionDirector` — and asserting the beat resolves to **Succeeded**, the player ward survives, no transform
  goes NaN, the tension director registered the fight, and **the win advances the campaign** to the next beat.
  This is the proof the parts *fit*, not just that each works alone. 1 test.
- **State of the build.** Every game system now has a tested headless core:
  - *Engine (`RP.Game`)* — Vulkan 1.3 bring-up, instancing, frustum culling, depth; fixed-timestep loop;
    logging; `RigidBody` 6-DoF physics + collision; `FloatingOrigin`; `ReferenceFrame` + `ChunkStreamer`;
    `SimTierManager`; steering AI; `ObjectPool`; app-state machine + `Menu`; audio math; save store.
  - *Game (`RP.Spectre`)* — flight (`ShipController`); combat (shields/hull/damage/weapons/heat/capacitor/
    facets/lead/point-defence/impact); sensors/EW; ships roster + battle + broadsides; the *Tantalus* wreck;
    projectiles + debris; missions/difficulty/campaign; HUD model; menu trees; adaptive audio; save schema.
- **What remains — the GPU/art layer (human-verified "smoke pass", per the brief).** The instanced *draw* of
  battles, projectiles, debris and the wreck interior; the **text-rendering pass** that turns the `HudModel`
  and the `Menu`/`SpectreMenus` models into pixels; interior meshes/materials; and audio-stem playback +
  crossfade driven by the `TensionDirector`. Each consumes a system whose *logic* is finished and tested here;
  none can be verified without a human at the screen, which is why they are the remaining work rather than
  more headless phases.
- Tests: 780 RP.Math + 104 RP.Game + 104 Spectre = **988 total**, all green.

---

## Phase 19 — The campaign spine  *(the Phase-12 deferral; progression + save tie met)*

- **`RP.Spectre.Missions.Campaign`** + `MissionBrief`: an ordered list of story beats (name + briefing + a
  descent flag) and a cursor that **advances only on a win** (a loss replays the same beat). `Progress` is
  exactly the integer the save schema persists, so resuming drops the player back on the right mission; pure
  progression bookkeeping, no battle state.
- **`RP.Spectre.Missions.SpectreCampaign`**: the actual spine as data — *Shakedown → The Graveyard →
  Severance Ambush → Into the Tantalus → The Core* — pivoting at the descent from open fleet combat to the
  claustrophobic interior (the last two beats are inside the wreck).
- *Verified:* starts on beat 0; a win advances, a loss/unfinished replays; winning the last beat completes
  the campaign (Current → null, past-the-end is a no-op); progress **round-trips through `SpectreSaveData`**
  (resume lands on the right beat); the spine opens in open space and ends inside the wreck; the resume cursor
  is clamped to range. 7 tests.
- *Deferred:* binding each beat to a concrete spawned `BattleSimulation`/`Wreck` setup (the encounter
  authoring) and the briefing/cutscene presentation — the *progression* and its save tie are complete.
- Tests: 780 RP.Math + 104 RP.Game + 103 Spectre = **987 total**.

---

## Phase 18 — In-world projectiles + debris (pooled)  *(the Phase 4/5/6/8 deferral; acceptance met)*

- **`RP.Game.Scene.ObjectPool<T>`** (engine): a reuse pool for short-lived objects so a firefight never
  churns the GC (S2/S5). `Get`/`Return` with an optional reset hook; `CreatedCount` (never falls) is how the
  tests prove reuse — churning many objects allocates only the peak-live count. 4 tests.
- **`RP.Spectre.World.ProjectileSystem`** + `Projectile`: finite-speed ballistic rounds that travel, can be
  led/dodged, and apply `DamageRouter` damage on impact — the entity layer the combat *rules* were always
  meant to drive. Rounds are **pooled**, hit-tests are **swept** over each step (a fast shell can't tunnel
  through a thin target between frames), and a round never hits its own side. Hitscan weapons are rejected
  (the battle applies their damage instantly). 7 tests.
- **`RP.Spectre.World.DebrisField`**: a kill bursts pooled `RigidBody` chunks with **zero-mean scatter** over
  the parent's velocity and masses summing to the parent's, so the cloud's **total momentum exactly equals
  the ship's** at the instant it died; then they drift forever (no drag). 4 tests.
- *Verified:* rounds launch at weapon speed/aim, hit the right faction, deal damage and expire; a fast round
  still hits (swept); misses expire at range; rounds recycle from the pool; debris conserves momentum through
  spawn and stepping, splits the mass, and returns to the pool on clear.
- *Deferred:* wiring projectiles into `BattleSimulation` (it resolves hitscan inline today) and spawning a
  `DebrisField` on the destruction event, plus the GPU **instanced draw** of rounds/debris — the entity
  *behaviour* is complete and tested.
- Tests: 780 RP.Math + 104 RP.Game + 96 Spectre = **980 total**.

---

## Phase 17 — Adaptive audio: the dread director  *(S15.4 tension/cue model met; mixer playback deferred)*

- **`RP.Spectre.Audio.TensionDirector`** (+ `ThreatContext`, `AudioCue`): turns the tactical situation
  (inside the wreck, nearby hostiles, nearest-threat range, taking fire) into a single smoothed `Tension`
  [0,1] and an `AudioCue` (Calm → Unease → Stalk → Combat). Tension **rises fast and decays slow** so the
  *dread lingers* after a threat passes — the feel of the descent — and the cue is chosen through a
  **hysteresis band** so layers don't flutter on a threshold (active fire forces Combat outright). `MusicGain`
  scales a tension-driven intensity by the player's music-volume setting via the engine's
  `AudioMath.ComposeGain`.
- *Verified:* starts calm/quiet; fire drives tension high and the cue to Combat; after going calm, tension is
  still elevated but falling (slow decay > fast rise asymmetry); inside the wreck raises a baseline Unease
  with no enemies; a close-but-not-firing stalker reaches Stalk (not Combat); and music gain rises with
  tension and scales with the volume setting. 6 tests.
- *Deferred:* the actual stem/ambience **playback + crossfade** on the engine `AudioEngine` consumes this
  director's `Cue`/`MusicGain`; the *pacing logic* — what the descent should feel like — is complete and
  tested.
- Tests: 780 RP.Math + 100 RP.Game + 85 Spectre = **965 total**.

---

## Phase 16 — Menus + navigation  *(the 3.5 menu-UI deferral; model + game tree met, draw deferred)*

- **`RP.Game.Mechanics.Menu` / `MenuItem` / `MenuController`** (engine): a generic menu model — a moving
  selection that **wraps and skips disabled rows** (so the cursor never lands where it can't act), and a
  controller holding a **stack** of menus so a submenu pushes and `Back` pops (the universal Settings → … →
  back flow), with `Activate` running an action or descending, and `Reset` collapsing to the root. Pure model:
  the UI maps keys onto `MoveUp`/`MoveDown`/`Activate`/`Back` and draws `Items` with `SelectedIndex`. 7 tests.
- **`RP.Spectre.Shell.SpectreMenus`** (game): the actual trees built on that model and wired to the
  `AppStateMachine` — **Main** (New Game / Continue / Settings / Quit, with *Continue* disabled when there is
  no save), **Pause** (Resume / Settings / Abandon), and a **Settings** submenu that toggles/cycles the live
  `SpectreSettings`. Rows drive **legal** state transitions (the machine rejects illegal ones). 6 tests.
- *Verified:* navigation wraps and skips disabled rows; submenus push/pop; New Game → Playing (and runs its
  hook); Quit → Exiting; Resume (from Paused) → Playing; cycling difficulty edits the live settings;
  Continue's enabled state tracks the presence of a save.
- *Deferred:* the on-screen **draw** (text + layout via the Vulkan text path) renders this model — together
  with the HUD snapshot, the whole UI's *logic* is now complete and tested; only the text-rendering layer
  remains as the human-verified GPU piece.
- Tests: 780 RP.Math + 100 RP.Game + 79 Spectre = **959 total**.

---

## Phase 15 — The HUD model  *(the S3 prograde-marker deferral; computed core met, text/draw deferred)*

- **`RP.Spectre.Hud.HudModel`** + `HudSnapshot` / `TargetReadout` / `TargetContact`: one frame's flight +
  combat HUD as a **pure computed snapshot** — speed and the **prograde marker** (unit velocity, zero at
  rest), the boresight (gun line), and shield/hull/heat/capacitor gauges + weapon-ready/overheat flags. With
  a selected `TargetContact` it also gives **range, closing speed** (range rate; positive = closing), the
  target's condition, and the **lead pip** via `InterceptSolver` (target motion taken *relative to the
  player* so the ship's own velocity is accounted for). `TargetContact` is decoupled from `Combatant` so the
  HUD can mark any contact (ship, debris, waypoint).
- *Verified:* prograde tracks velocity and vanishes at rest; gauges mirror the systems; with a target, range/
  closing-speed/condition are right (closing flips sign when you run); a hitscan weapon aims straight while a
  ballistic weapon **leads a crossing target** ahead of its current position. 6 tests.
- *Deferred:* the actual on-screen **text/UI draw** (needs the Vulkan text path) consumes this snapshot — the
  HUD's *what to show* is complete and tested; only the *drawing* remains, with the menus (next).
- Tests: 780 RP.Math + 93 RP.Game + 73 Spectre = **946 total**.

---

## Phase 14 — Ship roster as data + capital broadsides  *(the Phase-7 deferral; acceptance met)*

- **`RP.Spectre.Ships.ShipClass`** + **`ShipCatalog`**: the roster as data (S18) — the Coalition line
  (Lance corvette → Warden frigate → Sentinel cruiser → Bastion carrier), the Severance insect hulls
  (**Wasp** interceptor → Hornet → Locust → **Hive** carrier), and the player **Spectre** (prototype-armed,
  fastest). Stats scale monotonically with `HullClass` (bigger = tankier, slower, more hardpoints), so a
  fight reads as a clear pecking order; init-only props so a class reads as a flat table entry like
  `WeaponCatalog`. Balancing is a data edit.
- **`RP.Spectre.Ships.ShipFactory`**: the one place a stat block becomes a live `Combatant` (with an optional
  faction re-flag for captured hulls), so spawning is "pick classes, place them".
- **`RP.Spectre.Ships.BroadsideArray`**: capital firing-arc logic — a target's local direction picks which
  batteries (Fore/Aft/Port/Starboard/Dorsal/Ventral) **bear**, so a capital fights by *presenting a side*
  rather than nosing in; overlapping arcs reward holding a target on the quarter (two batteries at once).
- *Verified:* the factory builds a combatant faithful to its spec (and re-flags faction); stats scale across
  the size tiers; the Wasp matches the canonical interceptor the earlier hand-built battle used; the Spectre
  is the fast prototype; and broadside arcs engage the correct side, double up on a quarter, and leave a gap
  when narrowed. 5 roster + 5 broadside = 10 tests.
- *Deferred:* multi-weapon firing in `BattleSimulation` (it drives one weapon per ship today) and a
  capital-vs-capital sim that drives the broadside batteries build on this data + arc core.
- Tests: 780 RP.Math + 93 RP.Game + 67 Spectre = **940 total**.

---

## Phase 13 — The descent: tumbling wreck + interior streaming  *(local-frame core met; rendering deferred)*

- **`RP.Game.Scene.ReferenceFrame`** (engine): a moving, rotating local coordinate frame — a world authored
  in clean local space and dropped into the galaxy at a drifting/tumbling pose. Local↔world point and
  direction transforms (round-trip exact), `PointVelocity` = `v + ω × r` (the hull's true motion at a point,
  what a ship must match to ride it), and `Advance` (drift + axis-angle tumble, same scheme as `RigidBody`).
  Composes with `FloatingOrigin` — local→world here, world→render there. 6 tests.
- **`RP.Game.Scene.ChunkStreamer`** (engine): generic 3D chunk residency around a moving focus, with a
  **load/unload hysteresis band** (same anti-thrash idea as `SimTierManager`) so loitering on a boundary
  doesn't page a chunk in and out. Pure bookkeeping: returns the load/unload change set for an asset loader
  to act on; knows nothing about what a chunk *contains*. 6 tests.
- **`RP.Spectre.World.Wreck`** (the *Tantalus*): binds the two — a ~3.5 km hulk (700×520 m abeam/deep)
  drifting and tumbling at ~0.6°/s about a tilted axis, interior streamed in 200 m chunks (600 m load /
  1000 m unload). `Contains` (inside the hull box), `HullVelocityAt` (match-the-tumble velocity), and
  `Update` (advance the tumble, then restream **in local space** so the resident set stays centred on the
  player *even as the hull rotates beneath them*). 5 tests.
- *Verified (S13/S14):* transforms round-trip; the tumble carries fixed points with the right tangential
  velocity; chunks page in/out only past their respective radii (hysteresis holds on a boundary nudge); and
  a full descent sweep stern→bow follows the player into new chunks and drops the entrance — all headless.
- *Deferred:* the interior **art/mesh** per chunk, the dread-pacing audio, and rendering the tumble (cockpit
  "down" swinging) ride on the existing instancing + this frame/streamer core; the local-frame *maths* and
  residency *rules* are complete and tested.
- Tests: 780 RP.Math + 93 RP.Game + 57 Spectre = **930 total**.

---

## Phase 12 — Missions + difficulty  *(objective/mission layer + difficulty dials; acceptance met)*

- **`RP.Spectre.Missions.Difficulty`** (committed at phase start): `DifficultyPreset` (Story/Standard/Hard/
  Custom) + `DifficultyScalars` (enemy count / damage / accuracy / aggression + incoming-damage, the S22
  table). The player's advantage is a **constant**; the dials scale only what it doesn't solve.
- **`RP.Spectre.Missions.Objective`**: `IObjective` (watches the live battle, resolves to Complete/Failed)
  with `EliminateFactionObjective` (clear a faction) and `KeepAliveObjective` (hold a ship alive for *N* s —
  covers both "survive the ambush" and "keep the freighter intact"). New mission *types* are new objectives,
  not new machinery.
- **`RP.Spectre.Missions.Mission`**: a thin rule-layer over a `BattleSimulation` — a bag of objectives that
  must **all** complete to win, plus **wards** (ships whose loss fails the mission: the player, optionally an
  escort). Splitting "must-complete" from "must-not-die" lets one model cover clear-the-field, timed-survival
  and **escort** (= an eliminate objective with the freighter added as a ward) with no per-type code. A
  resolved mission stays resolved.
- **`RP.Spectre.Missions.Encounter`**: the one place `DifficultyScalars.EnemyCount` becomes an actual spawn
  count (rounded, floored at 1) — so difficulty shapes encounter *scope*, not ship-for-ship stats, and
  mission authoring stays difficulty-agnostic.
- *Verified (S22/S20/S12):* Standard is neutral, Story gentler / Hard harsher on every dial, and the
  **lethality check holds at 2–4 s on Standard** while Hard kills sooner / Story later (a preset can't
  silently break balance); objectives resolve only on the real condition; a mission **succeeds** when all
  objectives complete, **fails** on player **or** escort-ward loss (ward loss outranks a simultaneous win),
  and a finished mission won't flip; encounter scaling tracks difficulty; and a mission **resolves to
  Succeeded over a real headless lopsided battle** (no hand-killing). 3 difficulty + 8 mission = 11 tests.
- *Deferred:* mission *scripting/sequence* (briefings, triggers, the campaign graph) and in-world objective
  markers/HUD build on this `Mission`/`IObjective` core; the full ship roster the encounter scaler spawns is
  the Phase-7 deferral.
- Tests: 780 RP.Math + 81 RP.Game + 52 Spectre = **913 total**.

---

## Phase 8 — Sensors / EW  *(sensor model met; debris drift reuses physics)*

- **`RP.Spectre.Sensors`**: `Signature` (boost/fire/size raise it, going dark drops it to a floor),
  `SensorModel` (signal strength falling with distance and **gas/debris occlusion**), `LockOnTracker`
  (locks build with steady signal, **decay/break when the target goes dark or behind cover**).
- *Verified:* going dark is the lowest signature; occlusion cuts the signal to zero when fully blocked;
  a lock acquires on a strong steady contact, never builds on a too-weak one, and **breaks when the
  signal is lost** — i.e. you can shake a pursuer in a cloud or behind a hulk (S8.4). 6 tests.
- *Note:* debris fields are `RigidBody` bodies with conserved momentum (already covered by Phase 3
  physics + Phase 2 instancing/pooling); gas clouds feed the `occlusion` term. The in-world debris
  generator/pool builds on those.
- Tests: 780 RP.Math + 81 RP.Game + 41 Spectre = **902 total**.

---

## Phase 7 — Ships + AI + a real battle  *(headless battle acceptance met)*

- **`RP.Game.Ai.Steering`**: Reynolds behaviours — seek, flee, arrive (eased stop), pursue (leads a moving
  target), separation (flocking anti-collision) — generic, composable, 6 tests.
- **`RP.Game.Scene.SimTier` + `SimTierManager`**: Near/Mid/Far LOD by distance with a hysteresis band so
  entities don't thrash tiers on a boundary (the brief's affordability trick, S5) — 4 tests.
- **`RP.Spectre.Ships`**: `Combatant` (body + shield + hull + weapon + heat/capacitor + faction) and
  `BattleSimulation` (nearest-enemy targeting → pursue → fire → route damage; speed-capped fixed-step).
- *Verified (S20 scenario test):* a **50-ship battle runs 40 s headless with no exceptions, no NaN
  transforms, and real attrition**, and is **deterministic for a given seed**.
- *Deferred:* the full ship roster as data (corvette→carrier, Severance Wasp→Hive) and capital broadside/
  facet AI build on this `Combatant`/`BattleSimulation` core; in-world rendering of the battle reuses the
  Phase 2 instancing.
- Tests: 780 RP.Math + 81 RP.Game + 35 Spectre = **896 total**.

---

## Phase 6 — Combat depth  *(acceptance met)*

- **Weapon families + fire discipline**: `WeaponFamily`, `Capacitor` (shared draw/recharge), `HeatSink`
  (overheat + vent hysteresis), `Weapon` (triple-gated firing: cooldown + heat + charge; ballistic
  recoil), `WeaponCatalog` (the S18 table incl. the over-class `Prototype` guns — higher damage, more
  heat, heavier draw).
- **Facet shields**: `FacetShields` — six independent directional facets; a hit's local direction picks
  the facing, so flanking drops one facet while the others hold (tested on a cruiser).
- **Ballistic lead**: `InterceptSolver` solves the intercept quadratic via **`RP.Math.PolynomialRoots`**
  (maths in Math, not reimplemented) — stationary/hitscan/leading/unreachable cases tested for time
  consistency.
- **Point-defence**: `PointDefenceSystem` + `GuidedMissile` — concentrates fire on the closest in-range
  missile; weak missiles are intercepted, tanky ones leak through.
- *Verified:* each family has a distinct role; missiles can be intercepted; the prototype edge is real but
  costed. 8 + 2 + 5 + 2 = 17 new tests.
- Tests: 780 RP.Math + 71 RP.Game + 33 Spectre = **884 total**.

---

## Phase 5 — Shields + hull + damage routing  *(combat core + lethality check met)*

- **`RP.Spectre.Combat`**: `Shield` (capacity/regen/**regen delay** — the key lever; suppressed under
  sustained fire), `Hull` (fragile HP pool), `DamageType` (energy/kinetic/missile), and `DamageRouter`
  (applies per-weapon vs-shield / vs-hull multipliers; energy strips shields, kinetic punches hull,
  missiles bypass; reports the shield-down event and destruction).
- *Verified (S18/S20):* shield absorbs while up, regen only after the delay, downed shield exposes hull,
  missile bypass, shield-down event — and the **lethality check**: an unshielded Spectre dies to pulse
  fire (18 dmg @ 5/s, vs-hull 0.8) in **~2.5 s**, inside the 2–4 s target; a full shield survives notably
  longer (7 combat tests).
- *Sequencing:* in-world weapon firing (projectiles) and destruction → debris need the entity/projectile
  model; they continue in Phase 6/7. The combat *rules* are complete and tuned.
- Tests: 780 RP.Math + 71 RP.Game + 17 Spectre = **868 total**.

---

## Phase 4 — Collision + crash damage  *(physics + damage curve done; in-world wiring folds into Phase 5)*

- **`RP.Game.Physics.CollisionResolver`**: reduced mass, sphere-overlap detection (contact normal +
  penetration), impact energy (½·reduced-mass·closing-speed²), and impulse resolution — equal-and-opposite,
  momentum-conserving, restitution-controlled. Tested: reduced mass, momentum conservation, elastic
  velocity swap, separating-bodies no-op, light-vs-heavy asymmetry, energy ∝ speed² (7 tests).
- **`RP.Spectre.Combat.ImpactModel`**: the crash-damage curve — soft threshold (gentle bumps → no damage)
  then linear in excess energy. Tuned so a head-on fighter ram exceeds the Spectre's 180 hull HP (lethal)
  while a 5 m/s drift does nothing (4 tests).
- *Sequencing:* "ramming destroys the ship" needs a Hull HP bar to subtract from — that lands with Phase 5
  (shields + hull + destruction → debris), where the resolver + curve get wired to in-world obstacles.
- Tests: 780 RP.Math + 71 RP.Game + 10 Spectre.

---

## Phase 3.5 — Game shell: state machine, save/resume, settings  *(testable core met; menu UI deferred)*

- **`RP.Game.Mechanics.AppStateMachine`** (+ `AppState`): Boot → MainMenu → Playing ⇄ Paused → Exiting,
  with a legal-transition table and a change event (5 tests).
- **`RP.Game.Mechanics.JsonStore`**: generic persistence — atomic write (temp + replace), corruption-safe
  load (missing/garbage → false, never throws), per-user data directory (4 tests).
- **`RP.Spectre.State`**: `SpectreSaveData` (versioned: ship transform/velocity/orientation/ang-vel,
  flight-assist, world seed, mission progress) + `SpectreSettings` (video/audio/controls/difficulty with
  defaults + `ClampToValid`) + `SaveSystem` (capture/apply to the `RigidBody`).
- *Verified:* **save round-trip** — the brief's single most important non-graphics test (S20) — through
  disk, plus capture/apply, settings round-trip, defaults, and clamping (6 Spectre tests).
- Wired into the game: settings load on boot, a prior save **auto-continues**, **F5 quicksaves**.
- *Deferred:* the on-screen Main Menu / Pause / Settings UI needs text/UI rendering (a Phase-12 polish
  item); the state machine + persistence beneath it are complete and tested.
- Tests: 780 RP.Math + 64 RP.Game + 6 Spectre.

---

## Phase 3 — Newtonian flight + floating origin  *(acceptance met)*

- **Integrators in RP.Math** (`Numerics/Integrators`, the Phase 0 gap): explicit/semi-implicit Euler + RK4,
  tested against closed-form cases (7 tests).
- **`RP.Game.Physics.RigidBody`**: double-precision 6-DoF body (position/velocity, quaternion orientation,
  angular velocity), world/local force & torque, `Integrate` = semi-implicit Euler + axis-angle quaternion
  integration. No drag — momentum persists. Tested (conserved momentum, constant thrust, frame-rate
  independence, local→world, spin-up).
- **`RP.Game.Scene.FloatingOrigin`**: camera-relative rebasing (`ToRenderSpace`/`FromRenderSpace`,
  `MaybeRebase` past a threshold). Renderer rebases all instances by `RenderOrigin` each frame, so drawing
  happens in small, precise coordinates centred on the player.
- **`RP.Spectre.World.ShipController`**: the Spectre's flight — WASD thrust/strafe, Space/Ctrl up/down,
  mouse steer, Q/E roll, Shift boost, **T toggles flight-assist** (counter-thrust bleeds lateral drift,
  counter-torque stops spin, brakes off-throttle). Cockpit camera rides the ship; floating origin follows.
- *Verified:* a scripted 8 km/s burn flew **~24.7 km, rebasing 6× (every ~4096 m), zero validation
  errors** — precision holds at range. Tests: 780 RP.Math + 55 RP.Game + 1 Spectre.
- *Deferred:* the velocity-vector (prograde) marker → HUD phase; assist on/off "feel" is a human smoke test.

---

## Phase 2 — Renderer at scale + input/audio  *(core acceptance met)*

- **GPU instancing**: 16³ = 4096 cubes in one `CmdDrawIndexed` via a per-instance vertex binding
  (offset + colour); push constant carries view-projection + a shared spin.
- **Frustum culling**: `RP.Math.Frustum` (Gribb–Hartmann, 9 tests) + `Game.Scene.FrustumCuller` (the
  system); culled survivors streamed into per-frame host-visible instance buffers. From inside the grid,
  ~2434/4096 visible — the rest rejected.
- **Input**: `Game.Scene.FreeFlyCamera` (Silk.NET.Input) — WASD/QE move, right-mouse look, Shift boost,
  frame-rate-independent.
- **Audio**: `Game.Audio.AudioMath` (attenuation/Doppler/dB/bus gain — 7 headless tests, brief S15.3) +
  `Game.Audio.AudioEngine` (OpenAL via Silk.NET; positional one-shot playback; bundled OpenAL Soft).
- *Verified:* one run does instancing + culling + input + a positional tone together, zero validation
  errors, clean teardown. Tests: 771 RP.Math + 44 RP.Game + 1 Spectre.
- *Deferred (quality, not acceptance):* texture/material loading + bindless descriptors, and replacing the
  one-allocation-per-buffer scheme with a block/VMA sub-allocator — revisited when the asset pipeline and
  real meshes land.

---

## Phase 1 — Rendering foundation  *(complete)*

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

### Done (cont.)

- **RP.Math upgraded** (separate repo, committed): renamed the double `Vector` → `Vector3d` with an
  assembly-wide `Vector` alias (all 744 existing tests pass unchanged), and added the **float vector
  family** `Vector2`/`Vector3`/`Vector4` (completionist ops, edge-case tests, `Vector3d` widen/narrow
  + `System.Numerics` GPU interop). 762 RP.Math tests pass. *Deferred:* double `Vector2d`/`Vector4d`
  (not yet needed; `Vector3d` covers true-position).
- **Vertex buffers**: `Rendering/Vertex` (position+colour, blittable) and `VulkanRenderer.Buffers.cs`
  (FindMemoryType, CreateBuffer, **staging upload** to a DEVICE_LOCAL buffer, one-time CopyBuffer). The
  test triangle now draws from a real device-local vertex buffer; pipeline has vertex input
  binding/attributes. *Verified:* clean 120-frame run, resize OK, zero validation errors.
  - *Allocator note:* one `DeviceMemory` per buffer for now — to be replaced by a block sub-allocator /
    VMA in Phase 2 (scale), as the brief requires. Flagged in code.

### Done (cont.) — lit 3D cube (Phase 1 core acceptance met)

- **Camera** (`Rendering/Camera`): view/projection from **RP.Math** (`LookAt` + `PerspectiveFieldOfView`)
  plus a `VulkanClipCorrection` matrix (Y-flip + depth [-1,1]→[0,1]) — the one place Vulkan's clip-space
  quirks live. `ToColumnMajorFloats` flattens RP.Math's row-indexed matrices to GLSL column-major. Unit-
  tested (transpose, clip correction, finiteness).
- **Index buffers + cube mesh**: `Vertex` gains a normal; a 24-vertex/36-index unit cube (per-face
  normals + colours) in device-local buffers; `CmdDrawIndexed`.
- **Depth buffer** (`VulkanRenderer.Depth.cs`): D32 depth image/view sized to the swapchain (rebuilt on
  resize), cleared each frame, depth-test+write in the pipeline, fed to dynamic rendering as a depth
  attachment.
- **Lit mesh shaders** (`mesh.vert`/`mesh.frag`): a 128-byte push constant (MVP + model) drives a Lambert
  diffuse + ambient term from the world-space normal.
- The game positions the camera and spins the cube from the sim clock. *Verified:* clean 150-frame run,
  survives resize, **zero validation errors**, exit 0. (Visual correctness is the human smoke pass.)
- Tests: 762 RP.Math + 37 RP.Game + 1 Spectre, all green.

### Remaining Phase 1 polish / next

A "grid of cubes" (multiple model matrices) folds naturally into **Phase 2** (GPU instancing + a real
block/VMA allocator, texture/material loading, frustum culling, input, OpenAL audio bring-up), so it is
taken up there rather than as a one-off here.

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
