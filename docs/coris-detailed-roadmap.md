# CORIS Engine – AI-First Development Roadmap

> Purpose: Provide an explicit, step-by-step feature backlog for an autonomous coding agent tasked with evolving the current CORIS codebase into the single-player, high-fidelity rocketry simulator envisioned in `rocket-sim-architecture.md` and `moltenvk-research.md`.  Each phase lists **actionable deliverables** the agent must implement, the order of execution, and clear exit criteria.

---

## Phase 0  » Baseline Audit  (Week 0)

1. Verify the **clean build & test** status on Windows, Linux and macOS.
2. Produce an **inventory report** of public APIs, project references and TODO comments.
3. Generate **dependency lock files** to pin current NuGet versions.

*Exit Criteria*: CI workflow named `baseline-audit` finishes green; report artifact uploaded.

---

## Phase 1  » Core Stabilisation & De-Risking  (Months 0-3)

Actionable Features for the Agent:

1. **Vector3 Migration**  – Replace every `Vector3Legacy` reference with `System.Numerics.Vector3`; delete legacy struct.
2. **Unsafe Code Audit**  – Isolate unsafe blocks to interop helpers; wrap with safe abstractions; enable `<AllowUnsafeBlocks>true>` only where required.
3. **Silk.NET Upgrade**   – Move all graphics projects to the latest Silk.NET LTS version; update API calls; rerun Vulkan smoke tests.
4. **Double-Precision Orbital Integrator**  – Introduce `DoubleVector3` + RK4‐DP propagation; write unit tests targeting < 1 mm drift over a six-hour orbit.
5. **MoltenVK Validation** – On macOS, run Vulkan validation layers with MoltenVK; zero errors permitted.
6. **CI Matrix Expansion** – Add GitHub Actions jobs for `win-latest`, `ubuntu-latest`, `macos-latest` (arm64 & x64) running build, unit, and headless Vulkan tests.

*Exit Criteria*: All unit tests pass; deterministic "Hello Orbit" demo renders at ≥120 fps; CI `stabilisation` workflow green.

---

## Phase 2  » Minimum-Viable Simulation  (Months 4-8)

Actionable Features:

1. **Piece-Part-Vessel SoA**  – Refactor runtime data into cache-aligned Structure-of-Arrays; ensure GUID ↔ index maps.
2. **Rocket Builder (ASCII Terminal UI)** – Implement an interactive, stepwise terminal UI for rocket assembly. User selects parts (starting with capsule, then tanks, then engines, etc.) one by one, top-to-bottom. Each step displays a side-on ASCII rendering and part specs. Navigation supports forward, back, undo, and reorder. Shows running mass/thrust/Δv. Blueprint can be reviewed, saved, loaded, and edited before confirmation. Supports additional part types (e.g., decouplers, fairings) and extensibility for future features. Fully covered by automated tests (unit and integration). Exit criterion: user can build, review, save, load, and launch a multi-stage vehicle using only the ASCII UI, with all part specs and stack diagrams correct.
3. **ASCII Map View (v1)** – Terminal UI rendering of orbits, maneuver nodes, and planet outlines; moved from Phase 3 for earlier feedback.
4. **Planet Prototype (Scaled Sphere)** – Render a textured sphere representing Earth-like body; expose radius, mass, gravitational parameter; implement simple exponential atmosphere density curve used by drag model.
5. **Orbital Frame & Local Origin System** – Introduce coordinate manager that keeps physics local origin ≤2 km to prevent FP precision loss; update camera & physics transforms accordingly.
6. **Basic Flight Camera & Input** – 3-axis free camera that can track vessel; WASD/EQ rotation, scroll zoom; binds vessel throttle & staging keys for manual flight tests.
7. **Jolt Physics Bridge**   – Add native Jolt binaries per platform; write C# P/Invoke layer; expose `PhysicsWorldService` with step/addBody/removeBody/raycast.
8. **Internal Stress Model (Stub)**  – Implement placeholder joint-stiffness system to eliminate wobbly stacks; expose tuning parameters.
9. **Atmospheric Ascent Autopilot (Sample)**  – Script that launches a stock rocket to 100 km; validates thrust, drag and fuel consumption.
10. **Save / Load System**   – Serialize complete simulation state (including builder blueprints) to binary; load restores bit-identical physics state; add regression test.
11. **Command-Line Switches** – `--launch-to-leo`, `--benchmark-physics`, `--headless` flags.

*Exit Criteria*: User can build, review, save, load, and launch a multi-stage vehicle using only the ASCII UI, with all part specs and stack diagrams correct. Planet prototype renders at correct scale; manual or scripted launch achieves 100 km circular orbit within ±2 % Δv prediction; save-load diff == 0; physics runs 120 Hz on reference PC.

---

## Phase 3  » Gameplay Loop, Planetary Systems & Orbital Planning  (Months 9-12)

Actionable Features:

1. **3D Rocket Builder (Workspace & Mesh Swap)** – Start implementation of in-engine workspace for mesh-based assembly, hot-swapping real glTF meshes once available. Implement part hierarchy UI, collider generation, and support for drag-and-drop mesh placement. This phase begins after the ASCII builder is complete and validated.
2. **Planet Generation Framework** – Create `PlanetService` that loads celestial config JSON (radius, mass, atmosphere profile, rotation); supports multiple bodies; exposes query APIs (altitude, gravity, atmospheric density).
3. **Procedural Planet LOD (Stage 0)** – Implement geodesic sphere subdivision + height-map sampling to generate coarse LOD mesh; CPU-side for now; cull & render planet up to 500 km view.
4. **Maneuver Node Data Model**  – Class with Δv vector, start epoch, burn duration, and serialization.
5. **Patched Conic Solver**  – Implement Hohmann, bi-elliptic, plane-change calculations; expose pork-chop plotting API.
6. **Tech-Tree JSON Spec**  – Define nodes, prerequisites, unlock costs; implement evaluator.
7. **Part Authoring Pipeline**  – Parse `assets/parts.json` into SoA at startup; add file-watcher for hot reload.
8. **Telemetry Output**  – Stream key flight metrics to stdout and CSV file for analysis.
8. **Part Authoring Pipeline**  – Parse `assets/parts.json` into SoA at startup; add file-watcher for hot reload.
9. **Telemetry Output**  – Stream key flight metrics to stdout and CSV file for analysis.

*Exit Criteria*: Two-body system (Kerbin-style planet + Moon) loads from JSON; orbits display in ASCII map; player can build, launch and perform lunar free-return mission unlocking next tech tier.

---

## Phase 4  » Content, Modding & Graphics Foundation  (Months 13-16)

Actionable Features:

1. **Mod SDK**  – Create NuGet template exposing engine extension points; include versioned ABI checks.
2. **DLL Hot-Reload Loader**  – File system watcher reloads C# mod DLLs in < 2 s; gracefully handles reflection exceptions.
3. **glTF Importer**  – Convert mesh + PBR material to GPU buffers; build asset-processor CLI that bakes textures to KTX2.
4. **Renderer Pass 0**  – Forward renderer with PBR lighting, instanced meshes, skybox; verify MoltenVK path.
5. **Content Samples**  – Ship example part pack and planet mesh showcasing importer.

*Exit Criteria*: Example mod DLL adds a new engine part visible in renderer; hot reload inserts part without restart; frame-time ≤ 16 ms at 1080p.

---

## Phase 5  » Polishing, Optimisation & Early-Access Release  (Months 17-20)

Actionable Features:

1. **Renderer Pass 1**  – Deferred + forward-plus transparency, GPU instancing for ≥100 k pieces, volumetric clouds, geo-clipmap planets.
2. **Atmospheric Scattering**  – Precompute LUTs; integrate into sky and ground shaders.
3. **UI Framework**  – Dockable panels, layered-data toggles (novice vs expert), pop-out MFD support on multi-monitor.
4. **Asset Pipeline Hardening**  – CI importer tests, asset-bundle versioning, compression.
5. **Installer & Packaging**  – Cross-platform scripts that bundle MoltenVK, sign macOS app, create Steam-ready builds.
6. **Documentation Pass**  – Update README, modding wiki, in-code XML docs; generate API reference site.

*Exit Criteria*: Playable vertical slice with high-fidelity graphics, tech-tree progression, zero crash defects during 2-hour soak test; Steam build uploaded and passes review.

---

## Risk Register & Mitigations

| Risk | Phase | Mitigation |
|------|-------|-----------|
| Jolt C# wrapper drifts from upstream | 2–5 | Maintain minimal fork; contribute bindings upstream. |
| MoltenVK performance regression | 1–5 | Pin MoltenVK version per release; maintain benchmarking suite. |
| Scope creep (solo dev) | All | Enforce phase gate reviews; drop non-critical features early. |

---

## Tracking & Success Metrics

* **Continuous Integration**: Workflows `baseline-audit`, `stabilisation`, `simulation`, `graphics`, `release` must stay green.
* **Physics Throughput**: ≥120 simulation steps/s for baseline 100-part vessel on Ryzen 7 5800X.
* **Graphics Budget**: Frame ≤ 8 ms at 1080p on RTX 3060 & Apple M1 Pro.
* **Unit-Test Coverage**: ≥70 % for math, physics, and data-model assemblies.
* **Crash-Free Rate**: ≥99.5 % over 24 h soak in Phase 5.

---

**End of Roadmap – Ready for Autonomous Execution** 