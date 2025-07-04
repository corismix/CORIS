# CORIS: High-Performance Rocketry Simulation Engine

## Overview
CORIS is a cross-platform, high-fidelity rocketry simulation engine inspired by the BRUTAL framework. It is designed for extreme performance, deterministic simulation, and deep moddability, leveraging a hybrid C#/.NET and C++ interop architecture. The project targets advanced simulation, custom rendering (Vulkan), and robust physics (Jolt), with a focus on extensibility and scientific accuracy.

## Project Structure
- `src/CORIS.Core`: Core engine logic, data model, and interop layers.
- `src/CORIS.Sim`: Application entry point (console app for now).
- `assets/`: Game and simulation assets.
- `interop/`: Native bindings or C++ interop code (if needed).
- `tests/`: Unit and integration tests.
- `docs/`: Technical documentation and blueprints.

## Prerequisites
- .NET 8 SDK or newer (macOS compatible)
- Vulkan SDK (for graphics development)
- C++ build tools (if developing interop/native code)

## Getting Started (macOS)
1. **Clone the repository:**
   ```sh
git clone <your-repo-url>
cd CORIS
```
2. **Restore dependencies:**
   ```sh
dotnet restore
```
3. **Build the solution:**
   ```sh
dotnet build
```
4. **Run the simulation app:**
   ```sh
dotnet run --project src/CORIS.Sim
```

5. **Run the Vulkan demo (with MoltenVK on macOS):**
   ```sh
dotnet run --project src/CORIS.Sim -- --vulkan
```

## MoltenVK Integration for macOS

CORIS uses MoltenVK to run Vulkan code on macOS, where Metal is the native graphics API. The integration works as follows:

1. **Automatic MoltenVK Loading**: When running on macOS, the `Silk.NET.MoltenVK.Native` package automatically provides the MoltenVK library, which translates Vulkan API calls to Metal.

2. **Platform-Specific Extensions**: The engine detects macOS at runtime and enables the required Vulkan extensions:
   - `VK_KHR_portability_enumeration` - Required for MoltenVK instance creation
   - `VK_KHR_portability_subset` - For device compatibility with Metal's capabilities

3. **Surface Creation**: On macOS, a Metal-compatible surface is created through the `VK_EXT_metal_surface` extension, which GLFW/Silk.NET handles automatically.

4. **No Code Changes Needed**: Game code uses the same Vulkan API across all platforms - the translation happens transparently.

5. **Performance Notes**: While MoltenVK provides good performance, some Vulkan features may be emulated or limited on macOS. The engine handles these differences automatically where possible.

To verify MoltenVK is working correctly, run the Vulkan demo with the `--vulkan` flag and look for "macOS detected: Using MoltenVK" in the console output.

## Notes
- For Vulkan development, install the [LunarG Vulkan SDK](https://vulkan.lunarg.com/sdk/home).
- If you encounter issues with native libraries (e.g., Jolt), ensure your environment variables (e.g., `DYLD_LIBRARY_PATH`) are set correctly.
- This scaffold is cross-platform, but some dependencies may require additional setup on macOS.

## License
MIT (or specify your license here) 