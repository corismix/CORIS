# CORIS Engine: Inventory Report

*Generated: 2025-07-05*

---

## 1. Public APIs

### CORIS.Core
- **Classes**: `Piece`, `Part`, `Vessel`, `VesselState`, `FuelState`, `ModLoader`, `PhysicsEngine`
- **Structs**: `Vector3Legacy` (obsolete), `Vector3d`
- **Functionality**: Defines core data structures for vessel construction, physics state, and mod loading capabilities.

### CORIS.Sim
- **Classes**: `VulkanTest`
- **Methods**: `VulkanTest.RunHeadlessTest()`
- **Functionality**: Contains the main simulation entry point and graphics/physics test runners.

### EnumChecker
- A standalone utility project. Further inspection is needed to detail its public API.

---

## 2. Project References

- `CORIS.Sim` -> `CORIS.Core`
- `CORIS.Tests` -> `CORIS.Core`

---

## 3. TODO Comments

- **File**: `src/CORIS.Core/PiecePartVessel.cs`
- **Method**: `ModLoader.LoadPartsFromXml`
- **Comment**: `// TODO: Implement XML loading`

---

## 4. NuGet Dependencies

Dependency lock files (`packages.lock.json`) have been generated for all projects to ensure repeatable builds.

- **CORIS.Core**: `JoltPhysicsSharp`, `System.Numerics.Vectors`
- **CORIS.Sim**: `Silk.NET.Windowing`, `Silk.NET.MoltenVK.Native`, `Silk.NET.Vulkan`, `Silk.NET.Vulkan.Extensions.KHR`
- **CORIS.Tests**: `coverlet.collector`, `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`
- **EnumChecker**: `Silk.NET.Vulkan`, `Silk.NET.Vulkan.Extensions.KHR`
