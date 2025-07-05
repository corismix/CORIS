# Baseline Inventory Report

Generated on $(date)

## Projects & Target Frameworks

| Project | Target Framework | Output Type |
|---------|------------------|-------------|
| `src/CORIS.Core` | net8.0 | Class Library |
| `src/CORIS.Sim`  | net8.0 | Console Application |
| `tests/CORIS.Tests` | net8.0 | Test Project |
| `EnumChecker` | net8.0 | Console Application |

## Public APIs (Top‐Level)

### CORIS.Core

* VectorMath (static utility class)
* Piece, Part, Vessel data classes
* Vector3Legacy (struct) – to be migrated
* Vector3d (struct)
* VesselState, FuelState models
* ModLoader (static)
* OrbitalMechanics (partial implementation)

### CORIS.Sim

* Program (entry point, CLI argument parser)
* VulkanDemo (graphics sample)
* VulkanTest (headless validation helper)

### EnumChecker

* Program (enum reflection helper)

## Inter‐Project References

* `CORIS.Sim` → `CORIS.Core`
* `CORIS.Tests` → `CORIS.Core`

## NuGet Dependencies (lock files committed)

* Silk.NET (Windowing, Vulkan, MoltenVK Native) v2.22.0
* JoltPhysicsSharp v2.17.4
* xUnit + Microsoft.NET.Test.Sdk + coverlet.collector

## TODO Comments

| File | Line | Note |
|------|------|------|
| `src/CORIS.Core/PiecePartVessel.cs` | 109 | Implement XML loading |

## Summary

The baseline build and test suite pass on .NET 8.0. Dependency lock files (`packages.lock.json`) have been generated for every project, and a GitHub Actions workflow (`baseline-audit`) has been added to ensure cross-platform builds on Windows, Linux, and macOS.