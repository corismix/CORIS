# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Development Commands

### Building the Project
```bash
# Restore dependencies
dotnet restore

# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/CORIS.Tests/

# Run specific test by name filter
dotnet test --filter "TestMethodName"

# Run the main simulation application
dotnet run --project src/CORIS.Sim

# Run specific demos
dotnet run --project src/CORIS.Sim -- --vulkan    # Vulkan/MoltenVK demo
dotnet run --project src/CORIS.Sim -- --sim       # SoA physics simulation
dotnet run --project src/CORIS.Sim -- --help      # Show all available options
```

### Project Structure
- `src/CORIS.Core/`: Core simulation engine with physics, orbital mechanics, and data models
- `src/CORIS.Sim/`: Main application entry point and demo implementations
- `tests/CORIS.Tests/`: Unit tests using xUnit framework
- `assets/`: Game assets and configuration files
- `docs/`: Technical documentation and development summaries

## Core Architecture

### Physics Engine (`PhysicsEngine.cs`)
- Uses **Structure-of-Arrays (SoA)** data layout for optimal cache performance
- Implements **Piece-Part-Vessel** hierarchy for modular rocket construction
- High-frequency physics with sub-stepping (4 substeps per frame, 120Hz max timestep)
- Force-based thrust model with realistic fuel consumption using Tsiolkovsky rocket equation
- Environmental forces: gravity, atmospheric drag with altitude-based density calculation
- Verlet integration for stability at high speeds

### Orbital Mechanics (`OrbitalMechanics.cs`)
- High-precision orbital propagation using Runge-Kutta 4th order integration
- Converts between orbital state vectors and Keplerian elements
- Implements Hohmann and bi-elliptic transfer calculations
- Patched conic solver for multi-body trajectory planning
- Supports Earth, Moon, and solar system gravitational parameters

### Data Model (`DataModel.SoA.cs`)
- **SimulationState**: Core SoA container managing all entity data
- Entity management with GUID-to-index mapping for fast lookups
- Efficient add/remove operations using swap-and-pop pattern
- Stores positions, velocities, masses, forces, fuel, drag coefficients, etc.

### Vector Math (`VectorMath.cs`)
- Extends System.Numerics.Vector3 with physics-specific operations
- Cross product, safe normalization, spherical coordinate conversion
- Smooth damping and interpolation functions for camera systems

## Key Implementation Details

### Platform Support
- **Cross-platform**: .NET 9.0 targeting macOS, Windows, Linux
- **Graphics**: Vulkan via MoltenVK on macOS, native Vulkan on other platforms
- **Physics**: JoltPhysicsSharp for native performance
- **Window Management**: Silk.NET windowing system

### Performance Considerations
- Uses `AllowUnsafeBlocks=true` for performance-critical code paths
- Structure-of-Arrays layout minimizes cache misses during physics updates
- Double precision for orbital mechanics, single precision for local physics
- Sub-stepping physics loop maintains stability at variable frame rates

### Data Flow
1. **Force Clearing**: `PhysicsEngine.ClearForces()` at start of each frame
2. **Controlled Forces**: Apply thrust, user input forces
3. **Environmental Forces**: Apply gravity, drag during `PhysicsEngine.Update()`
4. **Integration**: Verlet integration updates positions/velocities

### Testing
- Uses xUnit framework for unit testing
- Tests focus on physics calculations and orbital mechanics accuracy
- Run tests with `dotnet test` command

## Development Notes

### Adding New Parts
- Create `Piece` objects with proper `Type` classification
- Add pieces to `SimulationState` via `AddEntity()` method
- Set appropriate drag coefficients and cross-sectional areas based on part type

### Extending Physics
- New forces should be applied in the simulation loop before calling `PhysicsEngine.Update()`
- Environmental forces are handled automatically by the physics engine
- Use `PartEngine` struct for thrust calculations with proper Isp and dry mass values

### Graphics Development
- Vulkan demo in `VulkanDemo.cs` shows basic rendering setup
- MoltenVK automatically handles Vulkan-to-Metal translation on macOS
- Window creation and input handling via Silk.NET APIs