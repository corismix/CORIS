# MoltenVK Implementation in CORIS

This document details the implementation of MoltenVK in the CORIS rocketry simulation engine, allowing Vulkan code to run on macOS by translating Vulkan API calls to Metal.

## Implementation Overview

The MoltenVK implementation in CORIS follows these key principles:

1. **Transparent to application code**: The same Vulkan code runs on all platforms without conditional compilation
2. **Runtime detection**: macOS is detected at runtime to enable MoltenVK-specific code paths
3. **Proper extension handling**: Required extensions are enabled for MoltenVK compatibility
4. **Fallback mechanisms**: Headless mode is used when window creation isn't supported

## Key Components

### 1. MoltenVK Package Integration

The `Silk.NET.MoltenVK.Native` NuGet package is included in the CORIS.Sim project, providing the MoltenVK library that translates Vulkan calls to Metal:

```xml
<ItemGroup>
  <PackageReference Include="Silk.NET.MoltenVK.Native" Version="2.0.1" />
</ItemGroup>
```

This package automatically loads the MoltenVK library when running on macOS, making it available to the Vulkan loader.

### 2. Platform Detection

The code detects macOS at runtime using `RuntimeInformation.IsOSPlatform(OSPlatform.OSX)` and enables MoltenVK-specific code paths when needed:

```csharp
private static readonly bool _isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

// Later in the code...
if (_isMacOS) {
    // MoltenVK-specific code
}
```

### 3. Required Extensions

For MoltenVK to work correctly, the following extensions are enabled:

- **Instance Extensions**:
  - `VK_KHR_get_physical_device_properties2` - Required for querying device capabilities
  - `VK_KHR_surface` - For surface creation

- **Device Extensions**:
  - `VK_KHR_portability_subset` - Required for MoltenVK compatibility
  - `VK_KHR_swapchain` - For swapchain creation (when using a window)

### 4. Headless Mode

Since GLFW doesn't support Vulkan on all macOS configurations, we implemented a headless mode that initializes Vulkan without creating a window. This allows the application to use Vulkan for compute operations or offscreen rendering:

```csharp
// On macOS, we need to use Metal directly instead of trying to create a Vulkan window
if (_isMacOS) {
    Console.WriteLine("Running on macOS with MoltenVK");
    RunHeadless();
} else {
    // Create window with Vulkan support on Windows/Linux
    RunWithWindow();
}
```

### 5. Surface Creation

When window creation is supported, we use GLFW/Silk.NET's built-in surface creation which handles the platform-specific details:

```csharp
_surface = window.VkSurface!.Create<AllocationCallbacks>(_instance.ToHandle(), null).ToSurface();
```

On macOS, this internally creates a `CAMetalLayer` and uses the `VK_EXT_metal_surface` extension.

### 6. Present Mode Selection

To ensure optimal compatibility with MoltenVK, we use the FIFO present mode on macOS:

```csharp
// On macOS, stick with FIFO as recommended in the MoltenVK research
if (!_isMacOS) {
    // Try to use mailbox mode on Windows/Linux
    // ...
} else {
    Console.WriteLine("Using FIFO present mode for optimal MoltenVK compatibility");
}
```

## Challenges and Solutions

### 1. Enum Compatibility

We encountered issues with Silk.NET's Vulkan bindings where enum values had different names than expected. This required fixes for:

- `ColorSpaceKHR.SpaceSrgbNonlinearKhr` (instead of `SrgbNonlinearKhr`)
- `PresentModeKHR.FifoKhr` (instead of `PresentModeFifoKhr`)
- `CompositeAlphaFlagsKHR.OpaqueBitKhr` (instead of `OpaqueKhr`)

### 2. Window Creation

GLFW doesn't support Vulkan on all macOS configurations, resulting in this error:
```
Attempted to initialize a Vulkan window using GLFW, which doesn't support Vulkan on this computer.
```

Solution: Implemented a headless mode that initializes Vulkan without creating a window, allowing the application to continue functioning.

### 3. Extension Handling

MoltenVK requires specific extensions, but not all are available on all systems. We implemented a robust extension detection system that only enables extensions that are actually supported.

## Testing and Verification

To verify MoltenVK is working correctly, we created a `VulkanTest` class that:

1. Initializes Vulkan without creating a window
2. Enumerates available physical devices
3. Checks for MoltenVK-specific extensions
4. Reports detailed information about the Vulkan environment

This can be run with:
```sh
dotnet run --project src/CORIS.Sim -- --vulkan-test
```

## Conclusion

The MoltenVK integration in CORIS enables the engine to use the same Vulkan code across all platforms while transparently handling the translation to Metal on macOS. This approach provides good performance and compatibility without requiring platform-specific rendering code.

Future improvements could include:
- Better handling of Metal-specific features and limitations
- Performance optimizations specific to the Metal backend
- Support for Metal-specific extensions via the `VK_EXT_metal_objects` extension 