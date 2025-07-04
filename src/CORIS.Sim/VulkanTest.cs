using System;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace CORIS.Sim
{
    public static class VulkanTest
    {
        public static void RunHeadlessTest()
        {
            Console.WriteLine("=== CORIS Headless Vulkan Test ===");
            Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
            Console.WriteLine($"Architecture: {RuntimeInformation.OSArchitecture}");

            try
            {
                // Test basic Vulkan functionality without creating a window
                TestVulkanInstance();
                TestPhysicalDeviceEnumeration();
                TestMoltenVKSpecificFeatures();

                Console.WriteLine("✅ All Vulkan tests passed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Vulkan test failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        private static void TestVulkanInstance()
        {
            Console.WriteLine("\n--- Testing Vulkan Instance Creation ---");

            var vk = Vk.GetApi();

            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)SilkMarshal.StringToPtr("CORIS Headless Test"),
                ApplicationVersion = new Version32(1, 0, 0),
                PEngineName = (byte*)SilkMarshal.StringToPtr("CORIS Engine"),
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version12
            };

            var extensions = new List<string>();
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                extensions.Add("VK_KHR_portability_enumeration");
                extensions.Add("VK_KHR_get_physical_device_properties2");
                Console.WriteLine("Added MoltenVK portability extensions");
            }

            var enabledExtensions = extensions.ToArray();

            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)enabledExtensions.Length,
                PpEnabledExtensionNames = enabledExtensions.Length > 0 ? (byte**)SilkMarshal.StringArrayToPtr(enabledExtensions) : null
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                createInfo.Flags |= InstanceCreateFlags.EnumeratePortabilityBitKhr;
            }

            var result = vk.CreateInstance(in createInfo, null, out var instance);
            if (result != Result.Success)
            {
                throw new Exception($"Failed to create Vulkan instance: {result}");
            }

            Console.WriteLine("✅ Vulkan instance created successfully");

            // Cleanup
            vk.DestroyInstance(instance, null);
            SilkMarshal.Free((nint)appInfo.PApplicationName);
            SilkMarshal.Free((nint)appInfo.PEngineName);
            if (createInfo.PpEnabledExtensionNames != null)
                SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
        }

        private static void TestPhysicalDeviceEnumeration()
        {
            Console.WriteLine("\n--- Testing Physical Device Enumeration ---");

            var vk = Vk.GetApi();

            // Create minimal instance for testing
            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)SilkMarshal.StringToPtr("CORIS Test"),
                ApiVersion = Vk.Version12
            };

            var extensions = new List<string>();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                extensions.Add("VK_KHR_portability_enumeration");
            }

            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)extensions.Count,
                PpEnabledExtensionNames = extensions.Count > 0 ? (byte**)SilkMarshal.StringArrayToPtr(extensions.ToArray()) : null
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                createInfo.Flags |= InstanceCreateFlags.EnumeratePortabilityBitKhr;
            }

            vk.CreateInstance(in createInfo, null, out var instance);

            // Enumerate physical devices
            uint deviceCount = 0;
            vk.EnumeratePhysicalDevices(instance, &deviceCount, null);

            if (deviceCount == 0)
            {
                throw new Exception("No Vulkan-capable devices found");
            }

            var devices = new PhysicalDevice[deviceCount];
            vk.EnumeratePhysicalDevices(instance, &deviceCount, devices);

            Console.WriteLine($"Found {deviceCount} Vulkan-capable device(s):");

            for (int i = 0; i < deviceCount; i++)
            {
                vk.GetPhysicalDeviceProperties(devices[i], out var properties);
                string deviceName = SilkMarshal.PtrToString((nint)properties.DeviceName);
                
                Console.WriteLine($"  Device {i}: {deviceName}");
                Console.WriteLine($"    Type: {properties.DeviceType}");
                Console.WriteLine($"    API Version: {properties.ApiVersion >> 22}.{(properties.ApiVersion >> 12) & 0x3ff}.{properties.ApiVersion & 0xfff}");
                Console.WriteLine($"    Driver Version: {properties.DriverVersion}");
                Console.WriteLine($"    Vendor ID: 0x{properties.VendorId:X}");
                Console.WriteLine($"    Device ID: 0x{properties.DeviceId:X}");

                // Test extension enumeration
                uint extensionCount = 0;
                vk.EnumerateDeviceExtensionProperties(devices[i], (byte*)null, &extensionCount, null);
                Console.WriteLine($"    Available Extensions: {extensionCount}");

                // Check for required extensions
                var availableExtensions = new ExtensionProperties[extensionCount];
                vk.EnumerateDeviceExtensionProperties(devices[i], (byte*)null, &extensionCount, availableExtensions);

                bool hasSwapchain = false;
                bool hasPortability = false;

                foreach (var ext in availableExtensions)
                {
                    string extName = SilkMarshal.PtrToString((nint)ext.ExtensionName);
                    if (extName == "VK_KHR_swapchain") hasSwapchain = true;
                    if (extName == "VK_KHR_portability_subset") hasPortability = true;
                }

                Console.WriteLine($"    Swapchain Support: {(hasSwapchain ? "✅" : "❌")}");
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Console.WriteLine($"    Portability Subset: {(hasPortability ? "✅" : "❌")}");
                }
            }

            Console.WriteLine("✅ Physical device enumeration successful");

            // Cleanup
            vk.DestroyInstance(instance, null);
            SilkMarshal.Free((nint)appInfo.PApplicationName);
            if (createInfo.PpEnabledExtensionNames != null)
                SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
        }

        private static void TestMoltenVKSpecificFeatures()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Console.WriteLine("\n--- Skipping MoltenVK tests (not on macOS) ---");
                return;
            }

            Console.WriteLine("\n--- Testing MoltenVK Specific Features ---");

            var vk = Vk.GetApi();

            // Test with MoltenVK optimizations enabled
            Environment.SetEnvironmentVariable("MVK_CONFIG_USE_METAL_ARGUMENT_BUFFERS", "1");
            Environment.SetEnvironmentVariable("MVK_CONFIG_SHADER_SOURCE_COMPRESSION", "lz4");

            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)SilkMarshal.StringToPtr("CORIS MoltenVK Test"),
                ApiVersion = Vk.Version12
            };

            var extensions = new string[]
            {
                "VK_KHR_portability_enumeration",
                "VK_KHR_get_physical_device_properties2"
            };

            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)extensions.Length,
                PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions),
                Flags = InstanceCreateFlags.EnumeratePortabilityBitKhr
            };

            vk.CreateInstance(in createInfo, null, out var instance);

            // Test MoltenVK device features
            uint deviceCount = 0;
            vk.EnumeratePhysicalDevices(instance, &deviceCount, null);
            var devices = new PhysicalDevice[deviceCount];
            vk.EnumeratePhysicalDevices(instance, &deviceCount, devices);

            if (deviceCount > 0)
            {
                var device = devices[0];
                vk.GetPhysicalDeviceProperties(device, out var properties);
                string deviceName = SilkMarshal.PtrToString((nint)properties.DeviceName);

                Console.WriteLine($"MoltenVK Device: {deviceName}");

                // Check if this is actually MoltenVK
                if (deviceName.Contains("Apple") || deviceName.Contains("M1") || deviceName.Contains("M2") || deviceName.Contains("M3") || deviceName.Contains("M4"))
                {
                    Console.WriteLine("✅ Detected Apple Silicon GPU with MoltenVK");
                }

                // Test timeline semaphore support (important for performance)
                vk.GetPhysicalDeviceFeatures(device, out var features);
                
                uint extensionCount = 0;
                vk.EnumerateDeviceExtensionProperties(device, (byte*)null, &extensionCount, null);
                var availableExtensions = new ExtensionProperties[extensionCount];
                vk.EnumerateDeviceExtensionProperties(device, (byte*)null, &extensionCount, availableExtensions);

                bool hasTimelineSemaphore = false;
                bool hasPortabilitySubset = false;

                foreach (var ext in availableExtensions)
                {
                    string extName = SilkMarshal.PtrToString((nint)ext.ExtensionName);
                    if (extName == "VK_KHR_timeline_semaphore") hasTimelineSemaphore = true;
                    if (extName == "VK_KHR_portability_subset") hasPortabilitySubset = true;
                }

                Console.WriteLine($"Timeline Semaphore Support: {(hasTimelineSemaphore ? "✅" : "❌")}");
                Console.WriteLine($"Portability Subset: {(hasPortabilitySubset ? "✅" : "❌")}");

                // Check memory properties
                vk.GetPhysicalDeviceMemoryProperties(device, out var memProperties);
                Console.WriteLine($"Memory Heaps: {memProperties.MemoryHeapCount}");
                Console.WriteLine($"Memory Types: {memProperties.MemoryTypeCount}");

                for (uint i = 0; i < memProperties.MemoryHeapCount; i++)
                {
                    var heap = memProperties.MemoryHeaps[(int)i];
                    Console.WriteLine($"  Heap {i}: {heap.Size / (1024 * 1024)} MB, Flags: {heap.Flags}");
                }
            }

            Console.WriteLine("✅ MoltenVK feature testing complete");

            // Cleanup
            vk.DestroyInstance(instance, null);
            SilkMarshal.Free((nint)appInfo.PApplicationName);
            SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
        }
    }
}