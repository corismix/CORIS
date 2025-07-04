using System;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Silk.NET.Core.Native;

namespace CORIS.Sim
{
    public static class VulkanTest
    {
        public static unsafe void Run()
        {
            Console.WriteLine("Starting Vulkan Test");
            Console.WriteLine("===================");
            
            // Check if running on macOS
            bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            if (isMacOS)
            {
                Console.WriteLine("Running on macOS: MoltenVK should be used");
            }
            else
            {
                Console.WriteLine("Running on non-macOS platform: Native Vulkan should be used");
            }
            
            try
            {
                // Initialize Vulkan
                var vk = Vk.GetApi();
                Console.WriteLine("Successfully loaded Vulkan API");
                
                // Create application info
                var appInfo = new ApplicationInfo
                {
                    SType = StructureType.ApplicationInfo,
                    PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Vulkan Test"),
                    ApplicationVersion = Vk.MakeVersion(1, 0, 0),
                    PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
                    EngineVersion = Vk.MakeVersion(1, 0, 0),
                    ApiVersion = Vk.Version12
                };
                
                // Setup instance extensions
                var extensions = new string[] { };
                
                if (isMacOS)
                {
                    // Add required extensions for MoltenVK
                    // Based on the error message, VK_KHR_portability_enumeration is not supported
                    // So we'll only use the extensions that are actually supported
                    extensions = new string[]
                    {
                        "VK_KHR_get_physical_device_properties2",
                        "VK_KHR_surface"
                    };
                    
                    Console.WriteLine("Adding macOS-specific extensions for MoltenVK:");
                    foreach (var ext in extensions)
                    {
                        Console.WriteLine($"  - {ext}");
                    }
                }
                
                // Create instance
                var extensionPtrs = SilkMarshal.StringArrayToPtr(extensions);
                try
                {
                    var instanceCreateInfo = new InstanceCreateInfo
                    {
                        SType = StructureType.InstanceCreateInfo,
                        PApplicationInfo = &appInfo,
                        EnabledExtensionCount = (uint)extensions.Length,
                        PpEnabledExtensionNames = (byte**)extensionPtrs
                    };
                    
                    // Create instance
                    Instance instance;
                    var result = vk.CreateInstance(in instanceCreateInfo, null, out instance);
                    if (result != Result.Success)
                    {
                        Console.WriteLine($"Failed to create Vulkan instance: {result}");
                        return;
                    }
                    
                    Console.WriteLine("Successfully created Vulkan instance");
                    
                    // Enumerate physical devices
                    uint deviceCount = 0;
                    vk.EnumeratePhysicalDevices(instance, &deviceCount, null);
                    
                    if (deviceCount == 0)
                    {
                        Console.WriteLine("No Vulkan-compatible devices found");
                        vk.DestroyInstance(instance, null);
                        return;
                    }
                    
                    Console.WriteLine($"Found {deviceCount} Vulkan-compatible device(s)");
                    
                    var devices = stackalloc PhysicalDevice[(int)deviceCount];
                    vk.EnumeratePhysicalDevices(instance, &deviceCount, devices);
                    
                    // Get device properties
                    for (int i = 0; i < deviceCount; i++)
                    {
                        vk.GetPhysicalDeviceProperties(devices[i], out var properties);
                        var deviceName = Marshal.PtrToStringAnsi((IntPtr)properties.DeviceName);
                        var driverVersion = properties.DriverVersion;
                        var apiVersion = properties.ApiVersion;
                        
                        uint major = (apiVersion >> 22) & 0x3FF;
                        uint minor = (apiVersion >> 12) & 0x3FF;
                        uint patch = apiVersion & 0xFFF;
                        
                        Console.WriteLine($"Device {i}: {deviceName}");
                        Console.WriteLine($"  Driver Version: {driverVersion}");
                        Console.WriteLine($"  API Version: {major}.{minor}.{patch}");
                        
                        // Check if this is a MoltenVK device on macOS
                        if (isMacOS)
                        {
                            // Enumerate device extensions to check for portability subset
                            uint extensionCount = 0;
                            vk.EnumerateDeviceExtensionProperties(devices[i], (byte*)null, &extensionCount, null);
                            
                            if (extensionCount > 0)
                            {
                                var deviceExtensions = stackalloc ExtensionProperties[(int)extensionCount];
                                vk.EnumerateDeviceExtensionProperties(devices[i], (byte*)null, &extensionCount, deviceExtensions);
                                
                                bool hasPortabilitySubset = false;
                                Console.WriteLine("  Device extensions:");
                                for (int j = 0; j < extensionCount; j++)
                                {
                                    var extName = Marshal.PtrToStringAnsi((IntPtr)deviceExtensions[j].ExtensionName);
                                    if (extName == "VK_KHR_portability_subset")
                                    {
                                        hasPortabilitySubset = true;
                                    }
                                    
                                    // Print only a few key extensions to avoid flooding the console
                                    if (j < 5 || extName.Contains("portability") || extName.Contains("metal"))
                                    {
                                        Console.WriteLine($"    - {extName}");
                                    }
                                }
                                
                                if (hasPortabilitySubset)
                                {
                                    Console.WriteLine("  This device supports VK_KHR_portability_subset (MoltenVK compatibility)");
                                }
                                else
                                {
                                    Console.WriteLine("  This device does not support VK_KHR_portability_subset");
                                }
                            }
                        }
                    }
                    
                    // Clean up
                    vk.DestroyInstance(instance, null);
                }
                finally
                {
                    // Free marshalled memory
                    SilkMarshal.Free(extensionPtrs);
                    Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
                    Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
                }
                
                Console.WriteLine("Vulkan test completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Vulkan test: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
} 