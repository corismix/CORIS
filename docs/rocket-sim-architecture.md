# **A Technical Blueprint for High-Performance Rocketry Simulation: Architecture, Physics, and Implementation**

## **Architectural Blueprint: The High-Performance Simulation Engine**

This section deconstructs the architectural philosophy and technical components of a high-performance simulation engine, modeled after the "BRUTAL" framework. It presents a blueprint for developers seeking to build a game where simulation fidelity is paramount, analyzing the strategic decisions that enable performance far exceeding that of conventional, general-purpose game engines.

### **1.1. Core Philosophy: The Strategic Trade-off for Performance, Determinism, and Moddability**

The foundational philosophy of this architectural blueprint is an uncompromising pursuit of raw performance, extreme scalability, and deterministic control. It represents a deliberate and calculated departure from the design of mainstream game engines like Unity and Unreal. Unlike general-purpose engines that prioritize rapid prototyping and ease of use for a broad audience, this architecture is engineered for a specific, demanding niche: large-scale, physics-intensive simulations.

The framework's design intentionally "trades ease-of-use for deterministic control," a choice made to robustly support three critical pillars essential to the rocketry simulation genre: deep, system-level moddability, the ability to simulate vehicles with very high part counts, and a deterministic multiplayer environment capable of handling complex physics interactions at high time-warp factors.1 This philosophy positions the architecture not as a direct competitor to mainstream engines, but as a specialized instrument for a domain that consistently pushes against the performance limits of existing tools.

The name "BRUTAL," associated with the reference framework, reflects this design ethos—a ruthless and pragmatic focus on performance efficiency, achieved by stripping away layers of abstraction that obscure or hinder direct hardware access.1 This represents a fundamental stance against the "black box" nature of many commercial engines, where developers are often at the mercy of the engine's internal behaviors and performance characteristics. Every subsequent architectural decision, from the choice of graphics API to the data model, flows directly from this core principle of prioritizing direct control and measurable performance above all else.

### **1.2. System Architecture: The Hybrid C\#/.NET 8 and C++ Interop Model**

The framework is built upon a sophisticated hybrid architecture that leverages the distinct strengths of both managed and native code to achieve its performance goals. The primary language is C\# 11 running on the modern.NET 8 runtime. All high-level engine logic, gameplay systems, and, critically, user-created mods are compiled to standard managed Intermediate Language (IL).1 This managed core interfaces with the most performance-critical subsystems—graphics and physics—through what are described as "thin C++ interop" layers.1

This model seeks to capture the best of both worlds: the high-level productivity, type safety, powerful tooling, and rich ecosystem of C\# for game logic, while simultaneously harnessing the bare-metal performance of C++ for the two most computationally expensive tasks in a rocketry simulation. The description "thin C++ interop" is of particular significance. It implies that the C++ components are not monolithic engine modules in their own right. Rather, they are minimal, non-allocating wrappers meticulously designed to expose the native APIs of the underlying libraries—Vulkan for graphics and Jolt for physics—to the C\# environment with as little overhead as possible.1 This approach avoids the performance penalties associated with complex data marshalling between managed and native memory and maintains a clear, well-defined boundary.5

In this model, the C\# code acts as the "driver," orchestrating the native libraries which execute the heavy lifting. This is a complex architectural pattern that demands significant engineering expertise to implement correctly but grants an extraordinary degree of control and performance.1 For a developer seeking to emulate this, it is not necessary to build these interop layers from scratch. Mature, open-source libraries like Silk.NET for Vulkan and JoltPhysicsSharp for Jolt provide the necessary low-level bindings, allowing the developer to focus on the simulation logic itself rather than the intricacies of native interoperability.1

### **1.3. The Graphics Subsystem: A Practical Guide to Direct-to-Vulkan Rendering**

The rendering pipeline is architected to extract maximum performance from modern graphics hardware by using the Vulkan API directly. The engine leverages Vulkan 1.x and provides "direct command-buffer access from C\# via interop layer".1 This is a profound architectural choice that sets it apart from nearly all high-level engines, which abstract the graphics API behind their own rendering interfaces.

The decision to use Vulkan is a direct consequence of the framework's core philosophy. Vulkan's explicit nature and low driver overhead are cited as providing a tangible performance benefit, cutting CPU-side render time by approximately 30% when compared to an identically shaded scene in DirectX 11\.1 This quantifiable advantage is deemed worth the notoriously steep learning curve and development complexity associated with the API. Shaders are authored in standard languages like HLSL or GLSL and are compiled offline into the SPIR-V intermediate representation, which is then consumed directly by the Vulkan driver. The lighting model employs a modern Physically-Based Rendering (PBR) workflow, using a deferred shading pass for opaque geometry and a forward+ pass for transparencies and complex atmospheric effects.1

The pipeline is not merely using Vulkan; it is architected around Vulkan's unique capabilities. The optimization checklist explicitly mentions advanced, Vulkan-specific techniques such as "batch instanced-mesh by material," "reuse secondary command buffers across frames," and "parallelise command buffer building".1 These are not trivial tasks; they require a deep, expert-level understanding of Vulkan's explicit, multi-threaded command submission model. This level of integration allows the C\# simulation logic to construct rendering commands with minimal overhead, avoiding the performance bottlenecks that would arise from frequent crossing of the managed/native boundary.

Key visual features underscore this high-performance approach. The renderer supports massive-scale vessel rendering, on the order of 105 sub-parts, through GPU-instanced meshes. It also features a high-fidelity volumetric cloud and atmosphere system, which was ported from the well-regarded "Scatterer/blackrack" technology, demonstrating a commitment to visual quality that rivals established titles.1 Terrain is handled via procedural generation using geo-clipmaps and a spherical quadtree, enabling seamless orbit-to-surface transitions, a hallmark feature of advanced space simulations.1

### **1.4. The Physics Subsystem: Harnessing the Jolt SDK for Deterministic, Multithreaded Simulation**

The heart of the simulation's capability is its physics subsystem, built upon the Jolt Physics SDK. Jolt was selected for its state-of-the-art performance, particularly its exceptional multithreading capabilities, which are a cornerstone of the engine's performance claims.1

Benchmarks show that Jolt's broad-phase algorithm, which uses Morton-coded spatial hashing, parallelizes cleanly and delivers "near-linear scaling to 16 threads".1 This results in a staggering performance advantage in physics-bound scenarios. This raw performance is complemented by a rigorous focus on determinism. The simulation is designed to be "bit-exact across CPU brands if compilation flags \[are\] set," a critical and non-negotiable feature for enabling the framework's multiplayer architecture.1

The integration goes beyond a simple library drop-in. To solve the classic "wobbly rocket" problem endemic to physics-based construction games, a custom internal-stress model was developed by Felipe "HarvesteR" Falanghe, the original creator of *Kerbal Space Program*, demonstrating a deep, domain-specific customization of the physics engine.1

To handle the vast scales of space simulation and avoid the infamous "Kraken" floating-point precision errors, the architecture employs a sophisticated, multi-layered physics model. A double-precision orbital integrator is used for high-level trajectory calculations, while the real-time, interactive physics operates using single-precision floats. This is managed through a system of segmented "close-physics" zones. Each vessel or point of interest has its own local physics context with an origin that is continuously re-based to stay near the world origin (0,0,0), preventing precision loss as it travels vast distances.1 Distant craft, which do not require high-frequency collision checks, are propagated using a computationally cheaper patched-conics solver that runs on separate threads, ensuring they do not consume resources from the primary simulation loop.1 This hybrid approach provides both the precision needed for orbital mechanics and the performance needed for real-time collisions.

### **1.5. Data-Oriented Design: Implementing the "Piece-Part-Vessel" Hierarchy**

The core data model eschews generic, off-the-shelf Entity Component System (ECS) frameworks in favor of a purpose-built, data-oriented architecture. This model is described as a "Piece-Part-Vessel" hierarchy and is noted as being "akin to ECS but purpose-built".1

By creating a custom data structure tailored to the specific problem domain, the developers can optimize memory layout and data access patterns far more effectively than a generic ECS framework would allow. A generic ECS must be flexible enough to handle arbitrary combinations of components, which can introduce overhead in memory layout and system iteration. A rocketry simulation, however, has a very well-defined and stable data hierarchy: Vessels are composed of Parts, which can be broken down into functional Pieces. Architecting a data-oriented system around this known structure eliminates the need for the flexibility of a generic ECS, thereby shedding its associated overhead.1

The provided C\# code snippet for thrust integration offers a clear window into this design. The IntegrateThrust method operates on a ref PieceState state struct. The use of ref indicates that the method is directly modifying the state data in-place, avoiding costly copies. This PieceState struct is almost certainly part of a larger Structure of Arrays (SoA), a memory layout pattern that packs homogeneous data together. This layout is ideal for Single Instruction, Multiple Data (SIMD) processing, allowing the CPU to perform the same operation on multiple pieces of data simultaneously, a cornerstone of high-performance computing.1 The entire data schema is also designed to be "mod-first," with part definitions loaded from external XML or JSON files, reinforcing moddability as a core architectural tenet.1

### **1.6. Networking Architecture: The "Shared Timeline" Model for Multiplayer Time Warp**

The multiplayer architecture is designed to solve the unique and challenging problem of time-warp in a physics-based simulation. The framework employs a client-server model built on RocketNet (a custom fork of the RakNet networking library) that implements a "shared timeline" for warp handling.1 This model is described as being "Paradox-style," where any player can request a change in the simulation's time scale (warp factor), which is then synchronized across all connected peers.1

The viability of this entire model rests on the foundation of the deterministic physics engine. This connection is not merely incidental; it is a fundamental architectural dependency. Because the Jolt-based simulation is guaranteed to be bit-exact, clients can independently re-simulate the periods between authoritative state snapshots from the server with perfect accuracy, provided they have the same inputs. This allows the system to be "lock-step free," meaning clients do not need to pause and wait for network packets from other players on every single physics tick.1

When the server broadcasts a new state—for example, that the shared timeline has warped forward to T+5 minutes—each client receives the state snapshot and the new time scale. They can then run their local, deterministic Jolt simulation at high speed to "catch up" to the correct point in the shared timeline. This approach drastically reduces the required network bandwidth, as the server only needs to send periodic state snapshots and input changes, rather than a constant stream of position and rotation updates for every object. This makes a smooth multiplayer experience at high warp factors feasible, a feat that is exceptionally difficult to achieve with non-deterministic physics engines.1

## **Comparative Analysis: Justifying the Custom Engine Approach**

While the architectural deep dive explains how a high-performance engine works, this section provides the crucial context of *why* such a framework is necessary. By comparing its performance and developer experience against established industry leaders, a clear picture emerges of this architecture as a specialized tool designed to solve problems that are intractable with mainstream technology.

### **2.1. Quantitative Performance Analysis: A Deep Dive into the Benchmarks**

The most compelling argument for a custom, performance-first architecture lies in its objective, quantifiable performance metrics, particularly within its target domain of many-body physics simulation. The following table, with data derived from a Jolt physics white-paper and cross-engine benchmark ports, starkly illustrates this advantage.1

| Engine | CPU Scene (3,680 bodies) | GPU Scene (484 convex-mesh) | Notes |
| :---- | :---- | :---- | :---- |
| **BRUTAL \+ Jolt** | 770 steps/s (discrete); 510 steps/s (CCD) | 144 fps (Vulkan) | Near-linear scaling to 16 threads. 1 |
| **Unity 2022 (PhysX)** | 280 steps/s (discrete); 120 steps/s (CCD) | 110 fps (DX11) | Scales poorly past 6 cores. 1 |
| **Unreal Engine 5 (Chaos)** | 310 steps/s | 118 fps (DX12) | Higher CPU overhead in high-body scenes. 1 |
| **Godot 4 (Bullet)** | 190 steps/s | 92 fps (Vulkan) | Limited multithreaded broad-phase. 1 |

The CPU scene benchmark is the most revealing. In a high-stress test involving thousands of interacting rigid bodies, the custom framework's Jolt-based simulation is 2.75 times faster than Unity's PhysX implementation and over 4 times faster than Godot's Bullet-based physics.1 This is not a marginal gain; it is a generational leap in capability. On the GPU side, the use of Vulkan with its low driver overhead allows the custom engine to maintain the highest frame rate, delivering a \~30% advantage over a comparable DirectX 11 scene in Unity.1

However, the most strategically important metric is not the raw performance but the scalability. The notes highlight that Jolt provides "near-linear scaling to 16 threads," with a measured 4.1x speed-up on an 8-core CPU.1 In contrast, mainstream engines like Unity with PhysX are noted to "scale poorly" beyond a small number of cores.1 This implies that as consumer CPUs continue to add more cores—a persistent trend in hardware development—the performance advantage of a Jolt-based architecture over its competitors will not just persist but actively grow. This makes it a strategically "future-proof" choice for developers betting on the continuation of this hardware trend. For a project whose central gameplay promise depends on simulating hundreds of interconnected parts, this performance differential is the deciding factor.

### **2.2. Qualitative Developer Trade-offs: The True Cost of Unparalleled Performance**

The immense power of a custom, low-level framework comes at a significant cost in terms of developer effort and ecosystem support. The decision to pursue such an architecture is a conscious trade-off, sacrificing the convenience of mainstream engines for unparalleled control and performance. The following matrix quantifies this trade-off across several key criteria for a solo developer or small team.1

| Criterion | BRUTAL (Emulated) | Unity | Unreal | Godot |
| :---- | :---- | :---- | :---- | :---- |
| Learning Curve | Medium-High (Vulkan, Data-Oriented) | Low | Medium | Low |
| Docs & Samples | Growing, but limited | Extensive | Extensive | Good |
| Community | Small, specialized (\~4k Discord) | Massive | Large | Large |
| Build Iteration Time | Excellent (\<2s hot-swap) | Slow (3-10s) | Very Slow (5-15s+) | Fast (1-3s) |
| Licensing/Royalty | Permissive (MIT-style) | Royalty-free | 5% Revenue Share | Permissive (MIT) |

A custom architecture presents a "Medium-High" learning curve, a direct consequence of its low-level nature. A developer must be comfortable with the complexities of the Vulkan API and data-oriented programming paradigms to be effective. Its documentation, while growing, and its community, likely centered on a specialized Discord server, are dwarfed by the massive, mature ecosystems surrounding Unity and Unreal.1

However, two factors in this matrix stand out as critical counter-arguments. First, the "Build iteration" time is a hidden but powerful productivity multiplier. The ability to hot-swap C\# logic DLLs in under two seconds is dramatically faster than Unity's domain reloads (which can take up to 10 seconds) and especially Unreal's full C++ compilation cycle (often 15 seconds or more).1 For a solo developer or small team, whose most valuable resource is time, this rapid feedback loop between writing code and seeing the result can lead to a significant increase in overall development velocity. This helps to offset the initial investment in learning the more complex systems.

Second, the licensing model is extremely favorable. The framework code itself would be proprietary, but its core dependencies, Vulkan and Jolt, are open and royalty-free.1 This stands in stark contrast to Unreal Engine's 5% revenue share on gross revenue above a certain threshold, a significant financial consideration for a successful independent project.1

### **2.3. The Solo Developer Verdict: A Strategic Decision Framework**

Ultimately, the choice of whether to adopt a custom, high-performance architecture is not about finding the "best" engine, but about correctly matching the tool to the project's most fundamental and non-negotiable requirement. For a vast majority of game development projects—from platformers to narrative adventures—choosing this path would be an act of profound inefficiency. The minimal physics needs of such games would not justify the immense effort required to reinvent rendering, UI, and asset import pipelines from scratch.1

However, for a developer whose vision is a next-generation rocketry sandbox, the calculus is inverted. The primary technical risk for such a project is that the physics simulation will fail to deliver on its core promise: that large, complex, player-built rockets will feel stable, perform realistically, and not cripple the CPU. The benchmark data clearly indicates that this is a genuine risk with mainstream physics packages.1

In this specific context, a custom architecture becomes a highly rational, if not essential, choice. The performance of Jolt and the direct control of Vulkan are not merely "nice-to-have" features; they are enabling technologies. They are the tools that make the core vision of the game possible. A developer choosing this path is consciously accepting the role of a systems engineer in addition to that of a game designer. They are trading the pre-built comforts of a mainstream engine for the power to create a simulation that would otherwise be impossible.1

## **Foundational Physics for Rocketry Simulation**

A high-performance engine is only as good as the physical models it simulates. The architectural design is predicated on the correct and stable implementation of fundamental rocketry and orbital mechanics principles. This section translates the core physics equations into a practical guide for implementation within the simulation.

### **3.1. Newton's Laws in Code: From First Principles to Simulation Ticks**

The simulation's physical fidelity is grounded in a direct application of Newton's Laws of Motion. Each law can be mapped to a specific software system, providing a clear and logical connection from high-level physics to concrete implementation.1

* **Newton's First Law (Inertia):** An object in motion stays in motion. This is the foundation of the PatchedConicSolver, which calculates an unpowered vessel's trajectory (its orbit) as it coasts through space under the influence of gravity alone.1  
* **Newton's Second Law (F=ma):** The acceleration of an object is directly proportional to the net force applied. This law governs the real-time application of forces from thrust and atmospheric drag, which are integrated each physics step to determine the vessel's acceleration (a=Fnet/m).1  
* **Newton's Third Law (Action-Reaction):** For every action, there is an equal and opposite reaction. The expulsion of reaction mass from a rocket nozzle generates an equal and opposite thrust force. This is the foundational principle behind the Tsiolkovsky rocket equation.1

### **3.2. The Tsiolkovsky Rocket Equation: From Mission Planning to Real-Time Numerical Integration**

The heart of all mission planning in rocketry is the Tsiolkovsky rocket equation, which defines the maximum change in velocity (Δv or "delta-v") a vehicle can achieve. The equation is given as 1:

Δv=veln(mfm0)

where Δv is the change in velocity, ve is the effective exhaust velocity of the engine, m0 is the initial total mass (wet mass), and mf is the final total mass (dry mass). The exhaust velocity is directly related to the engine's specific impulse (Isp), a standard measure of efficiency, by the formula ve=Isp⋅g0, where g0 is standard gravity (9.80665m/s2).  
While this formula is excellent for mission planning, its direct application in a real-time simulation can be inaccurate. A more sophisticated, numerical approach is required for high fidelity. Rather than calculating the total Δv for an entire burn at once, the simulation should calculate an instantaneous dv over a very small time step, dt. The key line of code from a sample implementation, double dv \= engine.Isp \* g0 \* Math.Log(state.MassPrev / state.Mass);, reveals this process.1 It calculates the delta-v achieved during a single physics tick based on the ratio of the vessel's mass at the beginning of the tick (

state.MassPrev) to its mass at the end of the tick (state.Mass). This is effectively a numerical integration of the rocket equation's differential form. This method is more computationally intensive but provides far greater physical accuracy, correctly accounting for continuous mass loss during a burn and allowing for variable throttle settings.

### **3.3. Atmospheric Flight Dynamics: Modeling and Integrating Drag and Thrust Forces**

During ascent and re-entry, the two most significant forces acting on a rocket are thrust and atmospheric drag. The drag force is modeled using the standard drag equation 1:

FD=21ρCDAv2

Here, FD is the drag force, ρ (rho) is the atmospheric density at the current altitude, CD is the drag coefficient of the vessel (a value of 0.6-0.8 is suggested for typical rocket shapes at zero angle of attack), A is the cross-sectional area, and v is the vessel's velocity.1  
The combined equation for acceleration within an atmosphere, incorporating thrust, gravity, and drag, is given as 1:

a=mT−gr^−2m1ρCDAvv

where T is the thrust vector, m is the vessel's current mass, and gr^ is the gravitational acceleration vector.  
A critical piece of practical implementation advice is the recommendation to run this integration at a 120 Hz sub-step during atmospheric flight.1 The drag force is proportional to the square of velocity (

v2), meaning it changes non-linearly and can become extremely large at high speeds. If the physics simulation's time step (dt) is too large, the calculated drag force can "overshoot," leading to an incorrect velocity on the next frame. This error accumulates rapidly, causing numerical instability that can make the simulation "blow up." By sub-stepping—running the physics integration multiple times with a smaller dt for each single rendered frame—the simulation can handle these rapidly changing forces with much greater stability and accuracy. This is a common and necessary technique in high-fidelity flight simulators.1

## **Implementing Advanced Orbital Mechanics**

Beyond basic launch and orbit, a compelling rocketry simulation must allow for complex orbital maneuvers. The architectural blueprint calls for a key design pattern: create a PatchedConicSolver system that operates in its own simulation context. This system is responsible for the low-frequency, high-precision calculations of future trajectories, separate from the high-frequency, real-time rigid-body physics handled by Jolt.

### **4.1. Foundational Transfers: Hohmann, Bi-Elliptic, and the Patched Conic Solver**

The most fundamental maneuver for moving between two circular, co-planar orbits is the Hohmann transfer. It is the most fuel-efficient two-burn maneuver for this purpose. The required delta-v for the two burns are 1:

* First burn (to enter the transfer ellipse): Δv1=r1μ(r1+r22r2−1)  
* Second burn (to circularize at the new orbit): Δv2=r2μ(1−r1+r22r1)

Here, μ (mu) is the standard gravitational parameter of the central body, r1 is the radius of the initial orbit, and r2 is the radius of the target orbit.

For very large changes in orbital altitude, the Bi-elliptic transfer can be more efficient. This involves three burns: one to boost into a very high intermediate apoapsis, a second to raise the periapsis at that high altitude, and a third to lower the apoapsis to the final target orbit. This maneuver becomes more efficient than a Hohmann transfer when the ratio of the final to initial orbital radii (r2/r1) is greater than approximately 11.94.1 The

PatchedConicSolver system would be responsible for calculating these burn values and timings, presenting them to the player as a "maneuver node" on their orbital map.1 Open-source libraries like ALGLIB (C\#) or poliastro (Python) can serve as valuable references for building such a solver, and detailed script-based implementations like the one demonstrated in FreeFlyer provide a clear path for development.14

### **4.2. Advanced Maneuvers: Plane Changes and Gravity Assists**

To reach orbits that are not in the same plane as the current one (e.g., a polar orbit from an equatorial one), an inclination change maneuver is required. For a vessel in a circular orbit, the required delta-v is given by 1:

Δv=2vsin(2Δi)

where v is the orbital velocity and Δi is the desired change in inclination. This formula reveals a key strategic insight: the maneuver is most fuel-efficient when orbital velocity v is at its lowest. For an elliptical orbit, this occurs at apoapsis (the highest point). A well-designed maneuver planner would automatically place inclination change nodes at these optimal points.1  
For interplanetary travel, gravity assists (or "slingshot maneuvers") are a critical technique for gaining velocity without expending fuel. The velocity gain from a flyby is given by 1:

Δv=2v∞sin(2δ)

where v∞ is the hyperbolic excess velocity of the spacecraft relative to the planet, and δ is the turn angle of the trajectory. This shows that the benefit is directly proportional to the spacecraft's incoming speed, which is why missions to the outer solar system frequently use a gravity assist from the massive planet Jupiter to gain the necessary velocity.1

### **4.3. Close-Proximity Operations: Rendezvous and Docking with the Clohessy-Wiltshire Equations**

The final, delicate phase of many missions involves rendezvous and docking with another object in orbit. The blueprint recommends using the Clohessy-Wiltshire (CW) equations for this phase.1 The full equations of orbital motion are non-linear and complex to solve. However, for close-proximity operations (within a few kilometers), the CW equations provide a highly accurate linearized model of the relative motion between two objects. This simplified model is computationally much cheaper and is ideal for driving UI elements like a docking port alignment indicator, which can guide the player's thrust inputs to null out relative velocity and drift.1

The blueprint also provides a crucial safety and gameplay parameter: velocities should be matched to less than 0.1m/s prior to physical contact to prevent damage in the Jolt physics simulation.1 This hard-coded rule translates a complex physical reality into a clear, actionable gameplay mechanic.

## **Game Design and User Experience Architecture**

A high-fidelity simulation is technically impressive, but it must be translated into an engaging and accessible game through thoughtful design. This section explores the game design and UX principles required to build a compelling experience on top of the powerful technical foundation.

### **5.1. The Core Gameplay Loop: A Cycle of Design, Execution, and Evolution**

The player's journey is structured around a clear and compelling core gameplay loop, which consists of five distinct stages that are tightly coupled to the engine's core technical systems 1:

1. **Design:** Players use a modular editor to assemble multi-stage rockets from a library of parts. This interface provides critical live feedback, such as updating Δv charts based on the current design. This stage directly leverages the Piece-Part-Vessel data system.  
2. **Launch:** The player pilots their creation in real-time from the launchpad through the atmosphere and into orbit. This phase is powered by the high-fidelity Jolt physics simulation, including the atmospheric drag and gravity models.  
3. **Plan:** Once in a stable orbit, the player transitions to a strategic map view. Here, they use a maneuver node editor, powered by the PatchedConicSolver, to plan future burns for orbital changes, lunar transfers, or interplanetary voyages.  
4. **Execute:** The player returns to real-time control to execute the planned maneuvers, carefully controlling thrust and vessel orientation. This phase utilizes the multiplayer-ready "shared timeline" for time warp, allowing the player to fast-forward through long coasts.  
5. **Evolve:** Success in missions earns rewards (e.g., science points, contract payments) that are used to unlock new technologies in a tech-tree. This grants access to more exotic engines, larger fuel tanks, and more advanced components, enabling more ambitious missions and completing the loop.

This structure demonstrates a logical and efficient coupling between the game's design and the engine's architecture, with each stage of the loop mapping to a specific, purpose-built technical component.

### **5.2. Designing Robust Progression Systems: Tech-Trees, Budgets, and Reputation**

To provide long-term motivation and strategic depth, the gameplay is guided by three interconnected progression systems 1:

* **Tech-Tree:** A classic progression mechanic where players spend "science" earned from experiments (e.g., using a thermocouple or gravimeter) to unlock access to more advanced parts. This gates progress behind specific mission-based achievements, giving players clear goals and rewarding exploration.  
* **Budget:** A financial system that introduces economic constraints. Parts have costs, and missions have payouts. A particularly effective mechanic is "cost-per-ton" to orbit, which naturally encourages players to design more efficient and reusable launch vehicles—a core challenge in real-world rocketry that enhances the simulation's depth and provides an emergent design challenge.  
* **Reputation:** This system acts as a modifier on contract payouts and availability. Successful missions increase reputation, leading to more lucrative and challenging offers. Conversely, spectacular failures can cause "investor pull-back," adding a layer of risk and consequence to each launch and discouraging reckless gameplay.

### **5.3. UI/UX Strategy for High-Density Information: Layered Data and Multi-Window MFDs**

One of the greatest challenges in a high-fidelity simulation is managing information density without overwhelming the player. The proposed UX strategy addresses this through two key principles, aiming to make complex data both accessible and powerful.1

* **Layered Data:** The user interface is designed to be adaptable to the player's skill level. A "novice mode" might hide complex orbital vectors like normal/anti-normal and radial-in/radial-out, presenting a simplified, more intuitive view focused on prograde/retrograde markers. An "expert mode," however, would expose the full state vectors and orbital parameters, giving experienced players the detailed information they require for precision maneuvers.1 This allows the game to have a gentle learning curve without sacrificing depth.  
* **Multi-Window MFDs:** A standout feature is the ability to leverage the custom engine's unique multi-camera, multi-window output capability to create pop-out Multi-Function Displays (MFDs).1 This allows a player with a multi-monitor setup to create a highly immersive, realistic "cockpit" environment, drawing inspiration from real-world flight simulators and community desires for more advanced UIs.29 For example, the main monitor could show the primary 3D view, a second monitor could display a persistent orbital map, and a third could show detailed vessel subsystem status (e.g., power, fuel, thermal). This is a powerful feature for the simulation genre that is difficult or impossible to achieve in many mainstream engines, serving as a key differentiator enabled directly by the low-level architectural design.

## **Technical Implementation Guide**

This section provides practical guidance and illustrative code to bridge the gap between the theoretical architecture and a concrete implementation. The focus is on demonstrating the foundational steps a developer would take to emulate this framework using modern C\# and publicly available libraries.

### **6.1. Environment Setup: The C\#/.NET, Vulkan, and Jolt Toolchain**

A developer seeking to replicate the high-performance architecture would need to assemble a specific toolchain. This includes the.NET 8 SDK, the official LunarG Vulkan SDK for graphics drivers and validation layers, and the source code for the Jolt Physics library.1 The most critical component is the C\# interoperability layer. Rather than building this complex layer from scratch, a developer can leverage existing, high-quality open-source binding libraries, which significantly de-risks and accelerates the project:

* **For Vulkan:** **Silk.NET** is the recommended choice. It is a.NET Foundation project that provides comprehensive, up-to-date, and low-level C\# bindings for Vulkan and its many extensions. It handles the complexities of function pointer loading and struct marshalling, allowing the developer to work with the Vulkan API from C\# in a way that closely mirrors the native C++ API.1  
* **For Jolt Physics:** **JoltPhysicsSharp** is a dedicated library that provides cross-platform C\# bindings for the Jolt Physics engine. It wraps the native Jolt C++ API, exposing its functionality to the managed C\# environment.1

Using these libraries allows a developer to focus on building the simulation logic and rendering techniques, rather than the monumental task of creating and maintaining the native interop bindings themselves.

### **6.2. Code Implementation: Foundational C\# Examples for Vulkan and Jolt**

The following illustrative code snippets demonstrate the fundamental sequence of operations required to initialize Vulkan and Jolt using C\# and the recommended binding libraries. These represent the conceptual first steps a developer would take.

#### **Illustrative Vulkan Initialization in C\#**

This snippet shows the basic steps to initialize Vulkan and create a window surface using Silk.NET, demystifying the initial setup process.1

```csharp
// Illustrative C# code using Silk.NET for Vulkan initialization.  
// This demonstrates the conceptual steps required by the graphics layer.  
// Note: This is not a complete, runnable example. Error handling and resource cleanup are omitted for brevity.  
using Silk.NET.Vulkan;  
using Silk.NET.Windowing;  
using System.Runtime.InteropServices;

public class VulkanFoundation  
{  
    private Vk \_vk;  
    private IWindow \_window;  
    private Instance \_instance;  
    private SurfaceKHR \_surface;  
    private PhysicalDevice \_physicalDevice;  
    private Device \_device;

    public void Initialize()  
    {  
        // 1\. Initialize the API and create a window with Vulkan support  
        \_vk \= Vk.GetApi();  
        var options \= WindowOptions.DefaultVulkan;  
        options.Title \= "High-Performance Rocketry Simulation";  
        options.Size \= new(1920, 1080);  
        \_window \= Window.Create(options);  
        \_window.Initialize(); // Ensure window is created before using its handle
    
        // 2\. Create a Vulkan Instance  
        var appInfo \= new ApplicationInfo(pApplicationName: (byte\*)Marshal.StringToHGlobalAnsi("RocketrySim"), apiVersion: Vk.Version12);  
        var requiredExtensions \= \_window.GetRequiredVulkanExtensions(out var extCount);  
        var createInfo \= new InstanceCreateInfo(  
            sType: StructureType.InstanceCreateInfo,  
            pApplicationInfo: \&appInfo,  
            enabledExtensionCount: extCount,  
            ppEnabledExtensionNames: requiredExtensions  
        );  
        \_vk.CreateInstance(in createInfo, null, out \_instance).ThrowOnError();
    
        // 3\. Create a Window Surface (WSI Integration)  
        \_surface \= \_window.CreateVulkanSurface(\_instance, null).ToSurface();
    
        // 4\. Pick a Physical Device (GPU)  
        uint deviceCount \= 0;  
        \_vk.EnumeratePhysicalDevices(\_instance, \&deviceCount, null);  
        var physicalDevices \= new PhysicalDevice\[deviceCount\];  
        \_vk.EnumeratePhysicalDevices(\_instance, \&deviceCount, physicalDevices);  
        \_physicalDevice \= physicalDevices; // Simplified: just pick the first one
    
        // 5\. Create a Logical Device  
        // (Queue family selection logic omitted for brevity)  
        float queuePriority \= 1.0f;  
        var queueCreateInfo \= new DeviceQueueCreateInfo(  
            sType: StructureType.DeviceQueueCreateInfo,  
            queueFamilyIndex: 0, // Assume index 0 supports graphics/present  
            queueCount: 1,  
            pQueuePriorities: \&queuePriority  
        );  
        var deviceCreateInfo \= new DeviceCreateInfo(  
            sType: StructureType.DeviceCreateInfo,  
            pQueueCreateInfos: \&queueCreateInfo,  
            queueCreateInfoCount: 1  
        );  
        \_vk.CreateDevice(\_physicalDevice, in deviceCreateInfo, null, out \_device).ThrowOnError();  
    }  
}
```

#### **Illustrative Jolt Physics World Setup in C\#**

This snippet illustrates the basic setup of a Jolt physics world using a wrapper like JoltPhysicsSharp, showcasing its data-driven API.1

```csharp
// Illustrative C# code using JoltPhysicsSharp for a basic physics scene.  
// This demonstrates the conceptual steps required by the physics layer.  
// Note: This is not a complete, runnable example. Interface implementations and the main loop are omitted.  
using JoltPhysicsSharp;  
using System.Numerics;

public class JoltFoundation  
{  
    // These interfaces must be implemented by the user to define collision filtering.  
    private readonly BroadPhaseLayerInterface \_broadPhaseLayerInterface;  
    private readonly ObjectVsBroadPhaseLayerFilter \_objectVsBroadPhaseLayerFilter;  
    private readonly ObjectLayerPairFilter \_objectLayerPairFilter;

    private PhysicsSystem \_physicsSystem;  
    private BodyInterface \_bodyInterface;
    
    public void Initialize()  
    {  
        // 1\. Initialize Jolt native library and its global systems  
        Jolt.Initialize();  
        using var tempAllocator \= new TempAllocator(10 \* 1024 \* 1024); // 10MB  
        using var jobSystem \= new JobSystem(JobSystem.MaxJobs, JobSystem.MaxBarriers);
    
        // 2\. Create the Physics System  
        \_physicsSystem \= new PhysicsSystem();  
        \_physicsSystem.Init(maxBodies: 65535, 0, 0, 0, \_broadPhaseLayerInterface, \_objectVsBroadPhaseLayerFilter, \_objectLayerPairFilter);
    
        // 3\. Get the Body Interface  
        \_bodyInterface \= \_physicsSystem.GetBodyInterface();
    
        // 4\. Create a collision shape  
        var sphereSettings \= new SphereShapeSettings(radius: 0.5f);  
        Shape sphereShape \= sphereSettings.Create().Get();
    
        // 5\. Define the creation settings for a new rigid body  
        var bodySettings \= new BodyCreationSettings(  
            sphereShape,  
            new RVec3(0, 100, 0), // Initial position (double-precision)  
            Quaternion.Identity,  
            MotionType.Dynamic,  
            (ObjectLayer)1 // User-defined collision layer  
        );  
        bodySettings.OverrideMassProperties \= EOverrideMassProperties.CalculateInertia;  
        bodySettings.MassPropertiesOverride.Mass \= 10.0f; // 10 kg
    
        // 6\. Create the body and add it to the physics system  
        Body body \= \_bodyInterface.CreateBody(bodySettings);  
        \_bodyInterface.AddBody(body.ID, Activation.Activate);  
    }  
}
```

#### **The IntegrateThrust Function: A Case Study in High-Performance C\#**

The IntegrateThrust function is a microcosm of the overall architectural philosophy, perfectly illustrating how high-performance C\# logic is used to apply physically accurate forces that drive the underlying Jolt simulation.1

```csharp
// Part of a VesselSystem in the simulation  
public void IntegrateThrust(in PartEngine engine, ref PieceState state, double dt)  
{  
    const double g0 \= 9.80665; // m/s^2

    // Update mass flow from thrust and specific impulse  
    double mDot \= engine.Thrust / (engine.Isp \* g0);  
    state.Mass \= Math.Max(state.Mass \- mDot \* dt, engine.DryMass);
    
    // Calculate delta-v for this timestep using the Tsiolkovsky rocket equation  
    // This is a numerical integration step.  
    double dv \= engine.Isp \* g0 \* Math.Log(state.MassPrev / state.Mass);
    
    // Apply the change in velocity in the vessel's forward direction  
    Vector3d deltav \= state.Forward \* dv;  
    state.Velocity \+= deltav;
    
    // Update the previous mass for the next iteration  s
    state.MassPrev \= state.Mass;  
}
```

A line-by-line analysis highlights its design:

* public void IntegrateThrust(in PartEngine engine, ref PieceState state, double dt): The method signature is a masterclass in performance-oriented C\#. in PartEngine passes the engine's static data as a read-only reference, avoiding a struct copy. ref PieceState passes a mutable reference to the dynamic state of the part, allowing the method to modify its mass and velocity directly in memory with maximum efficiency.  
* mDot \= engine.Thrust / (engine.Isp \* g0): This line calculates the mass flow rate (m˙) from the fundamental relationship linking thrust, specific impulse, and exhaust velocity.  
* state.Mass \=...: The vessel's total mass is reduced by the amount of propellant consumed in this discrete time step (m˙⋅dt). The Math.Max call ensures the mass never drops below the engine's inert dry mass.  
* dv \=... Math.Log(state.MassPrev / state.Mass): This is the numerical application of the Tsiolkovsky equation. It calculates the change in velocity (dv) achieved during this single time step based on the ratio of the mass before the step to the mass after the step.  
* state.Velocity \+= deltav: The calculated delta-v vector is added to the part's current velocity. This updated state is what will be consumed by the Jolt physics solver on its next update, which will then integrate this new velocity to calculate the vessel's new position.

This single function encapsulates the entire architectural pattern: it is (1) written in high-performance, modern C\#, (2) implements correct, textbook physics, (3) operates on a data-oriented struct layout, and (4) is designed to be called within a high-frequency simulation loop to apply forces that the native physics engine then resolves.

### **6.3. 3D Asset Workflow: From Blender to Engine via glTF and PBR Materials**

A professional and efficient 3D asset workflow is crucial for maintaining visual quality and performance. The recommended pipeline leverages industry-standard tools and formats.1

1. **Modeling and Baking:** High-poly source models are created in a 3D application like Blender. From these, low-poly game-ready models are derived. Details from the high-poly mesh are baked into normal maps and ambient occlusion (AO) maps to be applied to the low-poly version, preserving visual fidelity at a lower computational cost.56  
2. **PBR Texturing:** Assets are textured using a Physically-Based Rendering (PBR) workflow, typically creating maps for Albedo (base color), Metallic, Roughness, and the baked AO map. This ensures materials react realistically to the engine's lighting model.  
3. **Export Format (glTF 2.0):** The glTF 2.0 format is the ideal intermediary for exporting assets from Blender to the custom engine. It is a modern, efficient, open standard designed for the transmission of 3D scenes and models with PBR materials. Its specification is well-documented, making it relatively straightforward to write a custom importer.56  
4. **Engine Import:** A custom importer must be written for the engine. This tool will parse the glTF file and convert its data—meshes, materials, textures—into the engine's own optimized, GPU-friendly runtime formats, such as bindless vertex and index buffers.1

### **6.4. Procedural Generation: Techniques for Planets, Terrain, and Celestial Bodies**

To create vast and varied game worlds without an enormous art team, procedural generation is essential. The engine should incorporate several techniques for generating celestial bodies and their surfaces.1

* **Planet Terrain:** The surface of planets can be generated using fractal noise algorithms. Simplex noise is generally preferred over classic Perlin noise, as it exhibits fewer directional artifacts, resulting in more natural-looking terrain.59 Multiple octaves of noise are layered to create detail at different scales, from large continents to small hills. This noise is typically mapped onto a sphere to form the planet's base geometry.  
* **Procedural Meshes:** The engine must be capable of generating mesh geometry at runtime. There are numerous well-documented techniques for creating procedural meshes in C\#, such as generating vertices, triangles, and UV coordinates for grids, spheres, and other primitives.65  
* **Level-of-Detail (LOD):** A robust Level-of-Detail (LOD) system is non-negotiable for rendering planet-scale objects. As the player approaches a planet from orbit, the geometry must become progressively more detailed. The reference architecture uses a combination of geo-clipmaps and a spherical quadtree to manage terrain patches at different resolutions. For very distant views, pre-generated "spherical billboards"—impostors that represent the entire planet—can be used to drastically reduce the rendering cost.1

### **6.5. Advanced Optimization: A Practical Checklist for High-Performance Code**

The reference documentation provides a checklist of expert-level optimization techniques that are highly specific to its low-level architecture. This goes beyond generic advice and offers actionable strategies for extracting maximum performance from the custom engine.1

| Area | BRUTAL-Specific Recommendation | Source |
| :---- | :---- | :---- |
| Draw Calls | Batch instanced-mesh by material; reuse secondary command buffers across frames. | 1 |
| CPU Threads | Parallelise command buffer building & Jolt solving; avoid small submits (vkQueueSubmit). | 1 |
| Physics Islands | Limit solver iterations (4 vel \+ 1 pos) unless under heavy stress. | 1 |
| Floating-Point | Keep physics contexts local origin ≤ 2km; re-center renderer per frame. | 1 |
| Vulkan Pitfalls | Avoid pipeline-barrier thrashing; prefer timeline semaphores. | 1 |

Each recommendation reveals a deep understanding of the underlying systems. For example, "Avoid pipeline-barrier thrashing; prefer timeline semaphores" is advanced advice for Vulkan developers. Pipeline barriers are a powerful but complex synchronization primitive that can easily introduce GPU stalls ("bubbles") if not used with perfect precision. Timeline semaphores are a more modern and often more efficient alternative for managing dependencies between GPU operations. Similarly, "Keep physics contexts local origin \<= 2 km" is the practical rule for implementing the segmented physics zones needed to combat floating-point error in large worlds. This checklist serves as a condensed guide to avoiding common and costly performance traps inherent in this type of low-level development.

## **A Strategic Development Roadmap for the Solo Developer**

Achieving a project of this magnitude requires a disciplined, phased approach. The following 24-month roadmap is not just a schedule but a strategic plan for progressively tackling technical risk and building complexity in a manageable way. It draws inspiration from the iterative development cycles of successful complex simulation games like *Factorio* and *RimWorld* and the hard-won lessons of solo developers.1

### **7.1. Phase 1: Foundation and Technical De-risking (Months 0-3)**

* **Milestone:** "Hello Orbit" demo at 120 fps.1  
* **Strategic Focus:** This initial phase is entirely dedicated to technical de-risking. The goal is not to make a game, but to prove that the foundational technology stack can be assembled and made functional. This involves successfully compiling and linking the.NET runtime, a Vulkan renderer, and the Jolt physics engine. The "Hello Orbit" demo serves as a critical validation point: it confirms that the developer can render a scene via Vulkan and implement the double-precision orbital propagator, tackling the two most difficult technical unknowns from the outset.

### **7.2. Phase 2: Minimum-Viable Simulation (Months 4-8)**

* **Milestone:** Launch to Low Earth Orbit (LEO), save/load functionality.1  
* **Strategic Focus:** This phase is about implementing the core data structures (Piece-Part-Vessel) and integrating the rendering and physics systems. A successful launch to a stable orbit validates that the integration of thrust, gravity, and atmospheric drag forces with the Jolt solver is working correctly. Introducing save/load functionality early is a crucial quality-of-life feature for any simulation game, preventing tedious repetition during testing.

### **7.3. Phase 3: Core Gameplay and Orbital Planning (Months 9-12)**

* **Milestone:** A functional lunar free-return mission.1  
* **Strategic Focus:** Building the simulation's "brain." This involves developing the PatchedConicSolver and the associated maneuver node UI. A lunar free-return trajectory is a classic, non-trivial orbital mechanics problem. Successfully simulating it proves that the maneuver planning and trajectory prediction systems are robust and accurate. Achieving this milestone within the first year demonstrates that the project's core simulation engine is viable.

### **7.4. Phase 4: Content, Modding, and Extensibility (Months 13-16)**

* **Milestone:** Community-created part packs can be compiled and loaded.1  
* **Strategic Focus:** Shifting from core engine technology to content and extensibility. Implementing the C\# DLL hot-loading interface for mods is a pivotal step.84 This allows the developer to begin building out the library of rocket parts while simultaneously creating the infrastructure for the community to contribute, effectively parallelizing and scaling content creation.

### **7.5. Phase 5: Multiplayer and Stress Testing (Months 17-20)**

* **Milestone:** A successful 4-player stress test.1  
* **Strategic Focus:** Tackling the next major technical hurdle: networking. This phase involves implementing the RocketNet "shared timeline" model. A successful stress test validates that the deterministic physics simulation and networking architecture work in concert to provide a stable multiplayer experience, even at high time-warp.

### **7.6. Phase 6: Polishing, Optimization, and Release (Months 21-24)**

* **Milestone:** Version 1.0 is ready for Early Access release.1  
* **Strategic Focus:** This final phase is dedicated to bringing the project to a commercially viable standard. This includes final art passes, deep performance optimization, bug fixing, and creating marketing assets. The roadmap pragmatically notes that AI-assisted coding can help offset boilerplate work by \~25%, a modern acknowledgment of how a solo developer can realistically tackle a project of this ambitious scope.1

## **Conclusion and Strategic Recommendations**

The architectural blueprint detailed in this report is a testament to the power of specialized, performance-first engineering. It is not a general-purpose game engine but a finely-honed instrument designed for a single, demanding purpose: to enable the creation of next-generation, physics-heavy rocketry simulations. Its architecture, which marries the productivity of C\# with direct, low-level control over Vulkan and the Jolt Physics engine, provides a quantifiable and significant performance advantage over mainstream tools in its target domain.

This performance, however, is not without cost. The framework demands a high level of technical expertise, forcing the developer to engage with complex, systems-level concepts in graphics and physics that are typically abstracted away by commercial engines. The ecosystem is smaller, and the developer must be prepared to build more tooling from scratch.

Therefore, the decision to emulate this architecture is a strategic one that must be made with a clear-eyed assessment of a project's core requirements.

**Recommendations:**

1. **Assess the Core Constraint:** A developer must identify the single greatest technical risk to their project's vision. If the vision is fundamentally limited by the number of physical objects that can be simulated, the stability of joints between parts, or the performance ceiling of mainstream physics engines, then this high-performance architecture is a highly compelling solution. If these are not the primary constraints, the overhead of its adoption is likely not justified.  
2. **Commit to Systems Engineering:** A developer choosing this path must be willing to become a systems engineer as well as a game designer. They must embrace the complexity of the Vulkan API and the nuances of data-oriented design. The provided roadmap offers a viable path, but it is a challenging one that requires discipline and a deep technical skill set.  
3. **Leverage the Ecosystem:** The resources provided by projects like Kitten Space Agency—from open-source assets to custom physics code—are a critical accelerator. A developer should plan to integrate these resources from day one to mitigate the "cold start" problem and benefit from the work already done by others in the community.

In conclusion, this architectural blueprint represents a calculated bet on the idea that for certain classes of problems, raw performance and deterministic control are more valuable than generalized convenience. For the right project and the right developer, it is not just a viable choice, but potentially the only choice to realize a vision that lies beyond the horizon of current off-the-shelf technology.

#### **Works cited**

1. Blueprint for a High-Performance 3-D Rocketry Simu.pdf  
2. Video watch page: KSA | The KSP Replacement from RocketWerkz ..., accessed on July 4, 2025, [https://www.reddit.com/r/KerbalSpaceProgram/comments/1gg5106/ksa\_the\_ksp\_replacement\_from\_rocketwerkz\_seamless/](https://www.reddit.com/r/KerbalSpaceProgram/comments/1gg5106/ksa_the_ksp_replacement_from_rocketwerkz_seamless/)  
3. C++ / Objective-C++ Interop \- Compiler \- Swift Forums, accessed on July 4, 2025, [https://forums.swift.org/t/c-objective-c-interop/9989](https://forums.swift.org/t/c-objective-c-interop/9989)  
4. Interoperability Overview \- C\# | Microsoft Learn, accessed on July 4, 2025, [https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/interop/](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/interop/)  
5. C++ Language Interoperability Layer \- Compiler Research, accessed on July 4, 2025, [https://compiler-research.org/libinterop/](https://compiler-research.org/libinterop/)  
6. Vulkan essentials, accessed on July 4, 2025, [https://docs.vulkan.org/samples/latest/samples/vulkan\_basics.html](https://docs.vulkan.org/samples/latest/samples/vulkan_basics.html)  
7. Reducing Vulkan® API call overhead \- AMD GPUOpen, accessed on July 4, 2025, [https://gpuopen.com/learn/reducing-vulkan-api-call-overhead/](https://gpuopen.com/learn/reducing-vulkan-api-call-overhead/)  
8. Vulkan in Game Programming: A Comprehensive Guide \- Number Analytics, accessed on July 4, 2025, [https://www.numberanalytics.com/blog/vulkan-in-game-programming-comprehensive-guide](https://www.numberanalytics.com/blog/vulkan-in-game-programming-comprehensive-guide)  
9. jrouwe/JoltPhysics: A multi core friendly rigid body physics and collision detection library. Written in C++. Suitable for games and VR applications. Used by Horizon Forbidden West. \- GitHub, accessed on July 4, 2025, [https://github.com/jrouwe/JoltPhysics](https://github.com/jrouwe/JoltPhysics)  
10. r/kittenspaceagency Wiki: A Beginner's Guide \- Reddit, accessed on July 4, 2025, [https://www.reddit.com/r/kittenspaceagency/wiki/index/](https://www.reddit.com/r/kittenspaceagency/wiki/index/)  
11. HOHMANN TRANSFER ALGORITHM \- Colorado Pressbooks Network, accessed on July 4, 2025, [https://colorado.pressbooks.pub/app/uploads/sites/14/2024/01/George\_Hohmann-Transfer-Algorithm.pdf](https://colorado.pressbooks.pub/app/uploads/sites/14/2024/01/George_Hohmann-Transfer-Algorithm.pdf)  
12. Hohmann Transfer with the Spacecraft Dynamics Block ... \- MathWorks, accessed on July 4, 2025, [https://www.mathworks.com/help/aeroblks/hohmann-transfer-with-the-spacecraft-dynamics-block.html](https://www.mathworks.com/help/aeroblks/hohmann-transfer-with-the-spacecraft-dynamics-block.html)  
13. Hohmann Transfer Example \- Optimizing a Spacecraft Manuever ..., accessed on July 4, 2025, [https://openmdao.org/newdocs/versions/latest/examples/hohmann\_transfer/hohmann\_transfer.html](https://openmdao.org/newdocs/versions/latest/examples/hohmann_transfer/hohmann_transfer.html)  
14. COPT-Public/COPT-Release: COPT (Cardinal Optimizer) release notes and links \- GitHub, accessed on July 4, 2025, [https://github.com/COPT-Public/COPT-Release](https://github.com/COPT-Public/COPT-Release)  
15. COPT-Release/README-60.md at main \- GitHub, accessed on July 4, 2025, [https://github.com/COPT-Public/COPT-Release/blob/main/README-60.md](https://github.com/COPT-Public/COPT-Release/blob/main/README-60.md)  
16. Conic solver (SOCP and beyond) \- C++, C\#, Java library \- ALGLIB, accessed on July 4, 2025, [https://www.alglib.net/conic-programming/](https://www.alglib.net/conic-programming/)  
17. Convex/non-convex QP and QCQP solver \- C++, C\#, Java \- ALGLIB, accessed on July 4, 2025, [https://www.alglib.net/quadratic-programming/](https://www.alglib.net/quadratic-programming/)  
18. Patched Conics Transfer \- a.i. solutions, accessed on July 4, 2025, [https://ai-solutions.com/\_freeflyeruniversityguide/patched\_conics\_transfer.htm](https://ai-solutions.com/_freeflyeruniversityguide/patched_conics_transfer.htm)  
19. orbital-mechanics · GitHub Topics, accessed on July 4, 2025, [https://github.com/topics/orbital-mechanics?l=c%23](https://github.com/topics/orbital-mechanics?l=c%23)  
20. About Orekit, accessed on July 4, 2025, [https://www.orekit.org/](https://www.orekit.org/)  
21. c\# \- Orbital Mechanics \- Stack Overflow, accessed on July 4, 2025, [https://stackoverflow.com/questions/655843/orbital-mechanics](https://stackoverflow.com/questions/655843/orbital-mechanics)  
22. poliastro \- Open Collective, accessed on July 4, 2025, [https://opencollective.com/poliastro](https://opencollective.com/poliastro)  
23. Clohessy–Wiltshire equations \- Wikipedia, accessed on July 4, 2025, [https://en.wikipedia.org/wiki/Clohessy%E2%80%93Wiltshire\_equations](https://en.wikipedia.org/wiki/Clohessy%E2%80%93Wiltshire_equations)  
24. Mastering Clohessy-Wiltshire Equations \- Number Analytics, accessed on July 4, 2025, [https://www.numberanalytics.com/blog/ultimate-guide-clohessy-wiltshire-equations](https://www.numberanalytics.com/blog/ultimate-guide-clohessy-wiltshire-equations)  
25. Game Progression and Progression Systems \- Game Design Skills, accessed on July 4, 2025, [https://gamedesignskills.com/game-design/game-progression/](https://gamedesignskills.com/game-design/game-progression/)  
26. Tools Summit: A Series of Microtalks About Spreadsheets | Schedule 2025, accessed on July 4, 2025, [https://schedule.gdconf.com/session/tools-summit-a-series-of-microtalks-about-spreadsheets/908160](https://schedule.gdconf.com/session/tools-summit-a-series-of-microtalks-about-spreadsheets/908160)  
27. Data Visualization for UI-UX Design: Telling Stories with Data, accessed on July 4, 2025, [https://www.kaarwan.com/blog/ui-ux-design/data-visualization-for-ui-ux-design?id=635](https://www.kaarwan.com/blog/ui-ux-design/data-visualization-for-ui-ux-design?id=635)  
28. UI/UX for Complex Data: How to Simplify Analytics for Non-Technical Users \- Medium, accessed on July 4, 2025, [https://medium.com/@toritsejumoju/ui-ux-for-complex-data-how-to-simplify-analytics-for-non-technical-users-b427181423bc](https://medium.com/@toritsejumoju/ui-ux-for-complex-data-how-to-simplify-analytics-for-non-technical-users-b427181423bc)  
29. Kitten Space Agency News Thread \- The Lounge \- Kerbal Space Program Forums, accessed on July 4, 2025, [https://forum.kerbalspaceprogram.com/topic/227590-kitten-space-agency-news-thread/](https://forum.kerbalspaceprogram.com/topic/227590-kitten-space-agency-news-thread/)  
30. The new UI for KSP2 \- improvements and regressions from previous concepts?, accessed on July 4, 2025, [https://forum.kerbalspaceprogram.com/topic/210240-the-new-ui-for-ksp2-improvements-and-regressions-from-previous-concepts/](https://forum.kerbalspaceprogram.com/topic/210240-the-new-ui-for-ksp2-improvements-and-regressions-from-previous-concepts/)  
31. Kerbal Space Program 2 on Steam, accessed on July 4, 2025, [https://store.steampowered.com/app/954850/Kerbal\_Space\_Program\_2/](https://store.steampowered.com/app/954850/Kerbal_Space_Program_2/)  
32. Top UI/UX Design Tips \- How to Design a Great Bottom Mobile Navigation Bar \- YouTube, accessed on July 4, 2025, [https://www.youtube.com/watch?v=wLJ40GV2XEc](https://www.youtube.com/watch?v=wLJ40GV2XEc)  
33. Who said you can't mod the UI? Should definitely be possible to backport the new NavBall\! : r/KerbalSpaceProgram \- Reddit, accessed on July 4, 2025, [https://www.reddit.com/r/KerbalSpaceProgram/comments/11wd6ms/who\_said\_you\_cant\_mod\_the\_ui\_should\_definitely\_be/](https://www.reddit.com/r/KerbalSpaceProgram/comments/11wd6ms/who_said_you_cant_mod_the_ui_should_definitely_be/)  
34. OpenRocket Simulator, accessed on July 4, 2025, [https://openrocket.info/](https://openrocket.info/)  
35. 2+ Thousand Game Rocket Ui Royalty-Free Images, Stock Photos & Pictures | Shutterstock, accessed on July 4, 2025, [https://www.shutterstock.com/search/game-rocket-ui](https://www.shutterstock.com/search/game-rocket-ui)  
36. Launch Visualizer, accessed on July 4, 2025, [https://www.rocksim.com/](https://www.rocksim.com/)  
37. 10+ Thousand Rocket Ui Royalty-Free Images, Stock Photos & Pictures | Shutterstock, accessed on July 4, 2025, [https://www.shutterstock.com/search/rocket-ui](https://www.shutterstock.com/search/rocket-ui)  
38. Browse thousands of Cockpit images for design inspiration \- Dribbble, accessed on July 4, 2025, [https://dribbble.com/search/cockpit](https://dribbble.com/search/cockpit)  
39. Spaceship Cockpit Vector Art, Icons, and Graphics for Free Download \- Vecteezy, accessed on July 4, 2025, [https://www.vecteezy.com/free-vector/spaceship-cockpit](https://www.vecteezy.com/free-vector/spaceship-cockpit)  
40. Cockpit Design \- Pinterest, accessed on July 4, 2025, [https://www.pinterest.com/ideas/cockpit-design/922582955401/](https://www.pinterest.com/ideas/cockpit-design/922582955401/)  
41. 3D Cockpits \- Pioneer dev forum, accessed on July 4, 2025, [https://forum.pioneerspacesim.net/viewtopic.php?t=113](https://forum.pioneerspacesim.net/viewtopic.php?t=113)  
42. Hello Window | Silk.NET, accessed on July 4, 2025, [https://dotnet.github.io/Silk.NET/docs/opengl/c1/1-hello-window/](https://dotnet.github.io/Silk.NET/docs/opengl/c1/1-hello-window/)  
43. Welcome | Silk.NET, accessed on July 4, 2025, [https://dotnet.github.io/Silk.NET/docs/](https://dotnet.github.io/Silk.NET/docs/)  
44. JoltPhysics.js demos, accessed on July 4, 2025, [https://jrouwe.github.io/JoltPhysics.js/](https://jrouwe.github.io/JoltPhysics.js/)  
45. Jolt Physics: Jolt Physics Samples \- GitHub Pages, accessed on July 4, 2025, [https://jrouwe.github.io/JoltPhysicsDocs/5.0.0/md\_\_docs\_\_samples.html](https://jrouwe.github.io/JoltPhysicsDocs/5.0.0/md__docs__samples.html)  
46. Forum: Jolt Physics Wrapper \- OpenEuphoria, accessed on July 4, 2025, [https://openeuphoria.org/forum/137927.wc](https://openeuphoria.org/forum/137927.wc)  
47. Building and Using Jolt Physics \- GitHub Pages, accessed on July 4, 2025, [https://jrouwe.github.io/JoltPhysics/md\_\_build\_2\_r\_e\_a\_d\_m\_e.html](https://jrouwe.github.io/JoltPhysics/md__build_2_r_e_a_d_m_e.html)  
48. How to install and use JOLT PHYSICS in GODOT \- YouTube, accessed on July 4, 2025, [https://www.youtube.com/watch?v=SaW1MQbsB4U](https://www.youtube.com/watch?v=SaW1MQbsB4U)  
49. MacSpain/JoltPhysicsSharpUnity: JoltPhysics C\# port to Unity Engine \- GitHub, accessed on July 4, 2025, [https://github.com/MacSpain/JoltPhysicsSharpUnity](https://github.com/MacSpain/JoltPhysicsSharpUnity)  
50. C\# and Game Physics: Implementing Realistic Simulations \- Datatas, accessed on July 4, 2025, [https://datatas.com/c-and-game-physics-implementing-realistic-simulations/](https://datatas.com/c-and-game-physics-implementing-realistic-simulations/)  
51. Jolt physics C\# scripting: How do I reference Jolt Joints? : r/godot \- Reddit, accessed on July 4, 2025, [https://www.reddit.com/r/godot/comments/1ed9y26/jolt\_physics\_c\_scripting\_how\_do\_i\_reference\_jolt/](https://www.reddit.com/r/godot/comments/1ed9y26/jolt_physics_c_scripting_how_do_i_reference_jolt/)  
52. Raylib-cs 7.0.1 \- NuGet, accessed on July 4, 2025, [https://www.nuget.org/packages/Raylib-cs/](https://www.nuget.org/packages/Raylib-cs/)  
53. C\# Simple 2D Game Physics (Gravity, Jumping, Movement & Block Collision) \- CodeProject, accessed on July 4, 2025, [https://www.codeproject.com/Tips/881397/Csharp-Simple-D-Game-Physics-Gravity-Jumping-Movem](https://www.codeproject.com/Tips/881397/Csharp-Simple-D-Game-Physics-Gravity-Jumping-Movem)  
54. JoltPhysicsSharp 2.17.3 \- NuGet, accessed on July 4, 2025, [https://www.nuget.org/packages/JoltPhysicsSharp](https://www.nuget.org/packages/JoltPhysicsSharp)  
55. JoltPhysicsSharp:JoltPhysics C\# bindings \- GitCode, accessed on July 4, 2025, [https://gitcode.com/gh\_mirrors/jo/JoltPhysicsSharp/overview](https://gitcode.com/gh_mirrors/jo/JoltPhysicsSharp/overview)  
56. glTF 2.0 \- Blender 4.4 Manual \- Blender Documentation, accessed on July 4, 2025, [https://docs.blender.org/manual/en/latest/addons/import\_export/scene\_gltf2.html](https://docs.blender.org/manual/en/latest/addons/import_export/scene_gltf2.html)  
57. glTF 2.0 — Blender Manual, accessed on July 4, 2025, [https://docs.blender.org/manual/en/2.80/addons/io\_scene\_gltf2.html](https://docs.blender.org/manual/en/2.80/addons/io_scene_gltf2.html)  
58. Part 3: Creating a glTF-compatible Blender material from a set of PBR textures \- YouTube, accessed on July 4, 2025, [https://www.youtube.com/watch?v=70LRKp54zIc](https://www.youtube.com/watch?v=70LRKp54zIc)  
59. c\# \- Procedural Island Terrain Generation \- Stack Overflow, accessed on July 4, 2025, [https://stackoverflow.com/questions/30110703/procedural-island-terrain-generation](https://stackoverflow.com/questions/30110703/procedural-island-terrain-generation)  
60. \[Unity\] Procedural Planets (E01 the sphere) \- YouTube, accessed on July 4, 2025, [https://www.youtube.com/watch?v=QN39W020LqU](https://www.youtube.com/watch?v=QN39W020LqU)  
61. Procedural Generation Basic Tutorials For Beginners In Unity \- YouTube, accessed on July 4, 2025, [https://www.youtube.com/playlist?list=PLuldlT8dkudoNONqbt8GDmMkoFbXfsv9m](https://www.youtube.com/playlist?list=PLuldlT8dkudoNONqbt8GDmMkoFbXfsv9m)  
62. WardBenjamin/SimplexNoise: C\# Simplex Noise (1D, 2D, 3D). Supports arbitrary sizes and scales. \- GitHub, accessed on July 4, 2025, [https://github.com/WardBenjamin/SimplexNoise](https://github.com/WardBenjamin/SimplexNoise)  
63. C\# Simplex noise library offering 1D, 2D, and 3D forms : r/csharp \- Reddit, accessed on July 4, 2025, [https://www.reddit.com/r/csharp/comments/tusaso/c\_simplex\_noise\_library\_offering\_1d\_2d\_and\_3d/](https://www.reddit.com/r/csharp/comments/tusaso/c_simplex_noise_library_offering_1d_2d_and_3d/)  
64. Simplex Noise in C\# | Mattias Fagerlund's Coding Blog, accessed on July 4, 2025, [https://lotsacode.wordpress.com/2013/04/10/simplex-noise-in-c/](https://lotsacode.wordpress.com/2013/04/10/simplex-noise-in-c/)  
65. Procedural Mesh Geometry \- Unity \- Manual, accessed on July 4, 2025, [https://docs.unity3d.com/2020.1/Documentation/Manual/GeneratingMeshGeometryProcedurally.html](https://docs.unity3d.com/2020.1/Documentation/Manual/GeneratingMeshGeometryProcedurally.html)  
66. Unity Procedural Meshes Tutorials \- Catlike Coding, accessed on July 4, 2025, [https://catlikecoding.com/unity/tutorials/procedural-meshes/](https://catlikecoding.com/unity/tutorials/procedural-meshes/)  
67. Creating a Mesh \- Catlike Coding, accessed on July 4, 2025, [https://catlikecoding.com/unity/tutorials/procedural-meshes/creating-a-mesh/](https://catlikecoding.com/unity/tutorials/procedural-meshes/creating-a-mesh/)  
68. Managed Threading Best Practices \- .NET | Microsoft Learn, accessed on July 4, 2025, [https://learn.microsoft.com/en-us/dotnet/standard/threading/managed-threading-best-practices](https://learn.microsoft.com/en-us/dotnet/standard/threading/managed-threading-best-practices)  
69. C\# Threading and Multithreading: A Guide With Examples \- Stackify, accessed on July 4, 2025, [https://stackify.com/c-threading-and-multithreading-a-guide-with-examples/](https://stackify.com/c-threading-and-multithreading-a-guide-with-examples/)  
70. Vulkanised 2025: Practical Global Optimization & Analysis of Render Graphs \- YouTube, accessed on July 4, 2025, [https://www.youtube.com/watch?v=v9LaTFLhP38](https://www.youtube.com/watch?v=v9LaTFLhP38)  
71. Solo Game Dev: From Dreamy Expectations to Harsh Realities | by ..., accessed on July 4, 2025, [https://medium.com/@romain.mouillard.fr/solo-game-dev-lessons-learned-the-hard-way-0720138000bd](https://medium.com/@romain.mouillard.fr/solo-game-dev-lessons-learned-the-hard-way-0720138000bd)  
72. Factorio Post-Apocalypse \#01 PLANETFALL \- YouTube, accessed on July 4, 2025, [https://www.youtube.com/watch?v=VHXBXLkAMCs](https://www.youtube.com/watch?v=VHXBXLkAMCs)  
73. A kind of post-mortem \- Broadcasts From Space Age \- Factorio Let's Play/Tutorial \- YouTube, accessed on July 4, 2025, [https://www.youtube.com/watch?v=jyScNAfNlpo](https://www.youtube.com/watch?v=jyScNAfNlpo)  
74. Postmortem on my 1K spm megabase. : r/factorio \- Reddit, accessed on July 4, 2025, [https://www.reddit.com/r/factorio/comments/c8re6t/postmortem\_on\_my\_1k\_spm\_megabase/](https://www.reddit.com/r/factorio/comments/c8re6t/postmortem_on_my_1k_spm_megabase/)  
75. Harvest Organs Post Mortem :: Comments \- Steam Community, accessed on July 4, 2025, [https://steamcommunity.com/sharedfiles/filedetails/comments/1204502413](https://steamcommunity.com/sharedfiles/filedetails/comments/1204502413)  
76. Factorio \- Wikipedia, accessed on July 4, 2025, [https://en.wikipedia.org/wiki/Factorio](https://en.wikipedia.org/wiki/Factorio)  
77. Roadmap/History \- Official Factorio Wiki, accessed on July 4, 2025, [https://wiki.factorio.com/Roadmap/History](https://wiki.factorio.com/Roadmap/History)  
78. Factorio:About, accessed on July 4, 2025, [https://wiki.factorio.com/Factorio:About](https://wiki.factorio.com/Factorio:About)  
79. Friday Facts \#184 \- Five years of Factorio, accessed on July 4, 2025, [https://factorio.com/blog/post/fff-184](https://factorio.com/blog/post/fff-184)  
80. RimWorld \- Wikipedia, accessed on July 4, 2025, [https://en.wikipedia.org/wiki/RimWorld](https://en.wikipedia.org/wiki/RimWorld)  
81. RimWorld Wiki, accessed on July 4, 2025, [https://rimworldwiki.com/](https://rimworldwiki.com/)  
82. Lore \- RimWorld Wiki, accessed on July 4, 2025, [https://rimworldwiki.com/wiki/Lore](https://rimworldwiki.com/wiki/Lore)  
83. Timeline of Rimworld Lore : r/RimWorld \- Reddit, accessed on July 4, 2025, [https://www.reddit.com/r/RimWorld/comments/12op6sm/timeline\_of\_rimworld\_lore/](https://www.reddit.com/r/RimWorld/comments/12op6sm/timeline_of_rimworld_lore/)  
84. Code Modding Documentation | Paradox Interactive Forums, accessed on July 4, 2025, [https://forum.paradoxplaza.com/forum/threads/code-modding-documentation.1631780/](https://forum.paradoxplaza.com/forum/threads/code-modding-documentation.1631780/)  
85. Modding API \- Cities: Skylines Wiki, accessed on July 4, 2025, [https://skylines.paradoxwikis.com/Modding\_API](https://skylines.paradoxwikis.com/Modding_API)  
86. Home \- BoI: Lua API Docs, accessed on July 4, 2025, [https://wofsauge.github.io/IsaacDocs/rep/](https://wofsauge.github.io/IsaacDocs/rep/)  
87. Modding API Documentation? : r/X4Foundations \- Reddit, accessed on July 4, 2025, [https://www.reddit.com/r/X4Foundations/comments/k3m5ew/modding\_api\_documentation/](https://www.reddit.com/r/X4Foundations/comments/k3m5ew/modding_api_documentation/)