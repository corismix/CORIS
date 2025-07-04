using Silk.NET.Windowing;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Core.Native;
using Silk.NET.Core;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;

namespace CORIS.Sim
{
    public static class VulkanDemo
    {
        // Vulkan objects
        private static Vk _vk = default!;
        private static Instance _instance;
        private static PhysicalDevice _physicalDevice;
        private static Device _device;
        private static SurfaceKHR _surface;
        private static KhrSurface _khrSurface = default!;
        private static KhrSwapchain _khrSwapchain = default!;
        private static SwapchainKHR _swapchain;
        private static PipelineCache _pipelineCache;
        private static bool _hasTimelineSemaphore = false;
        
        // MoltenVK compatibility flags
        private static readonly bool _isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static void Run()
        {
            // Ensure MoltenVK runtime tweaks (must be set before Vk.GetApi)
            if (_isMacOS)
            {
                Environment.SetEnvironmentVariable("MVK_CONFIG_USE_METAL_ARGUMENT_BUFFERS", "1");
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MVK_CONFIG_SHADER_SOURCE_COMPRESSION")))
                    Environment.SetEnvironmentVariable("MVK_CONFIG_SHADER_SOURCE_COMPRESSION", "lz4");
            }

            Console.WriteLine("Starting Vulkan Demo");
            Console.WriteLine("===================");
            
            try
            {
                // On macOS, we need to use Metal directly instead of trying to create a Vulkan window
                if (_isMacOS)
                {
                    Console.WriteLine("Running on macOS with MoltenVK");
                    RunHeadless();
                }
                else
                {
                    // Create window with Vulkan support on Windows/Linux
                    RunWithWindow();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Vulkan demo: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        private static void RunHeadless()
        {
            // This is a simplified version that doesn't create a window
            // but still initializes Vulkan and demonstrates MoltenVK functionality
            
            Console.WriteLine("Running in headless mode (no window) for macOS");
            
            // Initialize Vulkan
            InitializeVulkanHeadless();
            
            Console.WriteLine("Vulkan initialized successfully in headless mode");
            
            // Perform some basic Vulkan operations
            // For now, we'll just enumerate supported extensions
            EnumerateExtensions();
            
            // Clean up
            CleanupVulkan();
        }
        
        private static void RunWithWindow()
        {
            // Create window with Vulkan support
            var options = WindowOptions.DefaultVulkan;
            options.Title = "CORIS Vulkan Demo";
            options.Size = new Silk.NET.Maths.Vector2D<int>(800, 600);
            options.API = GraphicsAPI.DefaultVulkan;
            using var window = Window.Create(options);
            
            window.Load += () => 
            {
                InitializeVulkan(window);
                Console.WriteLine("Vulkan window loaded.");
            };
            
            window.Render += delta => 
            {
                // Rendering code will be added in future updates
            };
            
            window.Closing += () => 
            {
                CleanupVulkan();
                Console.WriteLine("Vulkan window closing.");
            };
            
            window.Run();
        }

        private static unsafe void InitializeVulkanHeadless()
        {
            // Get Vulkan API instance
            _vk = Vk.GetApi();

            // Create Vulkan instance with platform-specific extensions
            CreateInstanceHeadless();

            // Select physical device
            SelectPhysicalDevice();

            // Create logical device
            CreateLogicalDevice();
        }
        
        private static unsafe void InitializeVulkan(IWindow window)
        {
            // Get Vulkan API instance
            _vk = Vk.GetApi();

            // Create Vulkan instance with platform-specific extensions
            CreateInstance(window);

            // Create surface (platform-specific via GLFW/Silk.NET)
            CreateSurface(window);

            // Select physical device
            SelectPhysicalDevice();

            // Create logical device
            CreateLogicalDevice();
            
            // Create swapchain
            CreateSwapchain(window.Size.X, window.Size.Y);
        }
        
        private static unsafe void CreateInstanceHeadless()
        {
            var available = GetAvailableInstanceExtensions();
            // Application info
            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("CORIS Vulkan Demo"),
                ApplicationVersion = Vk.MakeVersion(1, 0, 0),
                PEngineName = (byte*)Marshal.StringToHGlobalAnsi("CORIS Engine"),
                EngineVersion = Vk.MakeVersion(1, 0, 0),
                ApiVersion = Vk.Version12
            };
            var extensions = new List<string>();
            if (_isMacOS)
            {
                extensions.Add("VK_KHR_get_physical_device_properties2");
                if (available.Contains("VK_KHR_portability_enumeration"))
                    extensions.Add("VK_KHR_portability_enumeration");
            }
            InstanceCreateFlags flags = 0;
            if (extensions.Contains("VK_KHR_portability_enumeration"))
                flags |= InstanceCreateFlags.EnumeratePortabilityBitKhr;
            var extPtr = SilkMarshal.StringArrayToPtr(extensions.ToArray());
            try
            {
                var ci = new InstanceCreateInfo
                {
                    SType = StructureType.InstanceCreateInfo,
                    PApplicationInfo = &appInfo,
                    EnabledExtensionCount = (uint)extensions.Count,
                    PpEnabledExtensionNames = (byte**)extPtr,
                    Flags = flags
                };
                _vk.CreateInstance(in ci, null, out _instance).ThrowOnError();
            }
            finally { SilkMarshal.Free(extPtr); SilkMarshal.Free((nint)appInfo.PApplicationName); SilkMarshal.Free((nint)appInfo.PEngineName); }
            _vk.CurrentInstance = _instance;
        }

        private static unsafe void CreateInstance(IWindow window)
        {
            var available = GetAvailableInstanceExtensions();
            // Application info
            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("CORIS Vulkan Demo"),
                ApplicationVersion = Vk.MakeVersion(1, 0, 0),
                PEngineName = (byte*)Marshal.StringToHGlobalAnsi("CORIS Engine"),
                EngineVersion = Vk.MakeVersion(1, 0, 0),
                ApiVersion = Vk.Version12
            };

            // Get extensions required by the window system
            // GLFW/Silk.NET will handle platform differences (Win32, XCB, Metal)
            // On macOS, this will include VK_KHR_surface and VK_EXT_metal_surface
            var glfwExtensions = window.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
            
            // Convert the extensions to a string array
            var extensions = new string[(int)glfwExtensionCount];
            for (int i = 0; i < (int)glfwExtensionCount; i++)
            {
                unsafe
                {
                    var ptr = ((byte**)glfwExtensions)[i];
                    extensions[i] = Marshal.PtrToStringAnsi((IntPtr)ptr)!;
                }
            }

            // Set up debug messenger if needed (not implemented for brevity)

            // Check for macOS platform and ensure MoltenVK compatibility
            if (_isMacOS)
            {
                Console.WriteLine("Running on macOS: Using MoltenVK for Vulkan support");
                
                // MoltenVK requires these extensions for proper operation on macOS
                var macOSExtensions = new List<string>(extensions);
                
                // VK_KHR_get_physical_device_properties2 might be needed for some features
                macOSExtensions.Add("VK_KHR_get_physical_device_properties2");
                
                Console.WriteLine("Adding macOS-specific extensions for MoltenVK:");
                foreach (var ext in macOSExtensions)
                {
                    Console.WriteLine($"  - {ext}");
                }
                
                extensions = macOSExtensions.ToArray();
            }
            
            // Add timeline semaphore extension if supported
            if (available.Contains("VK_KHR_timeline_semaphore") && !extensions.Contains("VK_KHR_timeline_semaphore"))
                extensions = extensions.Append("VK_KHR_timeline_semaphore").ToArray();
            
            // Create instance
            var extPtr = SilkMarshal.StringArrayToPtr(extensions);
            try
            {
                var icFlags = InstanceCreateFlags.None;
                if (extensions.Contains("VK_KHR_portability_enumeration")) icFlags |= InstanceCreateFlags.EnumeratePortabilityBitKhr;
                var ci = new InstanceCreateInfo
                {
                    SType = StructureType.InstanceCreateInfo,
                    PApplicationInfo = &appInfo,
                    EnabledExtensionCount = (uint)extensions.Length,
                    PpEnabledExtensionNames = (byte**)extPtr,
                    Flags = icFlags
                };
                
                _vk.CreateInstance(in ci, null, out _instance).ThrowOnError();
                Console.WriteLine("Successfully created Vulkan instance");
            }
            finally 
            {
                SilkMarshal.Free(extPtr);
            }
            
            // Cleanup marshalled strings
            SilkMarshal.Free((nint)appInfo.PApplicationName);
            SilkMarshal.Free((nint)appInfo.PEngineName);
            
            // Load Vulkan instance-level functions
            _vk.CurrentInstance = _instance;
        }

        private static unsafe void CreateSurface(IWindow window)
        {
            // Use Silk.NET's built-in surface creation which handles platform differences
            // For macOS, this internally creates a CAMetalLayer and uses VK_EXT_metal_surface
            try
            {
                _surface = window.VkSurface!.Create<AllocationCallbacks>(_instance.ToHandle(), null).ToSurface();
                
                if (_isMacOS)
                {
                    Console.WriteLine("Created Vulkan surface using CAMetalLayer for MoltenVK compatibility");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create Vulkan surface: {ex.Message}");
            }
            
            // Get the KHR surface extension 
            if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface))
            {
                throw new Exception("KHR_surface extension not found");
            }
        }

        private static unsafe void SelectPhysicalDevice()
        {
            // Get all physical devices
            uint deviceCount = 0;
            _vk.EnumeratePhysicalDevices(_instance, &deviceCount, null);
            if (deviceCount == 0)
            {
                throw new Exception("Failed to find GPUs with Vulkan support");
            }

            var devices = stackalloc PhysicalDevice[(int)deviceCount];
            _vk.EnumeratePhysicalDevices(_instance, &deviceCount, devices);

            // Select the first suitable device
            // In a real application, you'd want to score and select the best device
            _physicalDevice = devices[0];
            
            // Get device properties for logging
            _vk.GetPhysicalDeviceProperties(_physicalDevice, out var deviceProperties);
            var deviceName = Marshal.PtrToStringAnsi((IntPtr)deviceProperties.DeviceName);
            Console.WriteLine($"Selected GPU: {deviceName}");
            
            if (_isMacOS)
            {
                // On macOS, check for portability subset extension which indicates MoltenVK features
                bool hasPortabilitySubset = HasExtension(_physicalDevice, "VK_KHR_portability_subset");
                if (hasPortabilitySubset)
                {
                    Console.WriteLine("Device supports VK_KHR_portability_subset (MoltenVK compatibility)");
                }
                else
                {
                    Console.WriteLine("Warning: VK_KHR_portability_subset not supported, some MoltenVK features may be unavailable");
                }
            }
        }

        private static unsafe void CreateLogicalDevice()
        {
            // Find queue family that supports graphics
            // In a real application, you'd also check for present support and select unique families
            uint queueFamilyCount = 0;
            _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueFamilyCount, null);
            var queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
            _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &queueFamilyCount, queueFamilies);

            uint graphicsFamily = uint.MaxValue;
            for (uint i = 0; i < queueFamilyCount; i++)
            {
                if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
                {
                    // For headless mode, we don't need to check present support
                    if (_surface.Handle == 0)
                    {
                        graphicsFamily = i;
                        break;
                    }
                    else
                    {
                        // Check present support
                        Bool32 presentSupport = false;
                        _khrSurface.GetPhysicalDeviceSurfaceSupport(_physicalDevice, i, _surface, &presentSupport);
                        
                        if (presentSupport)
                        {
                            graphicsFamily = i;
                            break;
                        }
                    }
                }
            }

            if (graphicsFamily == uint.MaxValue)
            {
                throw new Exception("Failed to find a queue family that supports graphics");
            }

            // Create device with a single queue
            var queuePriority = 1.0f;
            var queueCreateInfo = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = graphicsFamily,
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };

            // Enable device features as needed
            var deviceFeatures = new PhysicalDeviceFeatures();
            
            // Create device extensions list
            var deviceExtensionList = new List<string>();
            
            // Add swapchain extension if we have a surface
            if (_surface.Handle != 0)
            {
                deviceExtensionList.Add("VK_KHR_swapchain");
            }
            
            // Check for macOS compatibility
            bool hasPortabilitySubset = _isMacOS && HasExtension(_physicalDevice, "VK_KHR_portability_subset");
            if (hasPortabilitySubset)
            {
                deviceExtensionList.Add("VK_KHR_portability_subset");
                Console.WriteLine("Enabling VK_KHR_portability_subset for MoltenVK compatibility");
            }

            // Timeline semaphore extension support
            bool timelineAvailable = HasExtension(_physicalDevice, "VK_KHR_timeline_semaphore");
            if (timelineAvailable)
            {
                deviceExtensionList.Add("VK_KHR_timeline_semaphore");
                _hasTimelineSemaphore = true;
            }

            // Convert to native pointers
            var deviceExtensionPtr = SilkMarshal.StringArrayToPtr(deviceExtensionList.ToArray());
            try
            {
                // Create the logical device
                var deviceCreateInfo = new DeviceCreateInfo
                {
                    SType = StructureType.DeviceCreateInfo,
                    QueueCreateInfoCount = 1,
                    PQueueCreateInfos = &queueCreateInfo,
                    PEnabledFeatures = &deviceFeatures,
                    EnabledExtensionCount = (uint)deviceExtensionList.Count,
                    PpEnabledExtensionNames = (byte**)deviceExtensionPtr
                };

                _vk.CreateDevice(_physicalDevice, in deviceCreateInfo, null, out _device).ThrowOnError();
                Console.WriteLine("Logical device created successfully");
            }
            finally
            {
                // Free marshalled memory
                SilkMarshal.Free(deviceExtensionPtr);
            }
            
            // Load device-level functions
            _vk.CurrentDevice = _device;
            
            // Get the swapchain extension if we have a surface
            if (_surface.Handle != 0)
            {
                if (!_vk.TryGetDeviceExtension(_instance, _device, out _khrSwapchain))
                {
                    throw new Exception("KHR_swapchain extension not found");
                }
            }

            // Create pipeline cache
            CreatePipelineCache();
        }

        private static unsafe bool HasExtension(PhysicalDevice physicalDevice, string extensionName)
        {
            uint count = 0;
            _vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &count, null);
            var extensions = stackalloc ExtensionProperties[(int)count];
            _vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &count, extensions);

            for (int i = 0; i < count; i++)
            {
                var currentExtName = Marshal.PtrToStringAnsi((IntPtr)extensions[i].ExtensionName);
                if (currentExtName == extensionName)
                    return true;
            }
            
            return false;
        }

        private static unsafe void CreateSwapchain(int width, int height)
        {
            // Skip swapchain creation in headless mode
            if (_surface.Handle == 0)
            {
                return;
            }
            
            // Query surface capabilities
            _khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out var surfaceCapabilities);

            // Query surface formats
            uint formatCount = 0;
            _khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, null);
            var surfaceFormats = stackalloc SurfaceFormatKHR[(int)formatCount];
            _khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, surfaceFormats);

            // Pick a surface format (prefer B8G8R8A8_UNORM with SRGB colorspace)
            var surfaceFormat = surfaceFormats[0]; // Default to first format
            for (int i = 0; i < formatCount; i++)
            {
                if (surfaceFormats[i].Format == Format.B8G8R8A8Unorm && 
                    surfaceFormats[i].ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                {
                    surfaceFormat = surfaceFormats[i];
                    break;
                }
            }

            // Query present modes
            uint presentModeCount = 0;
            _khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModeCount, null);
            var presentModes = stackalloc PresentModeKHR[(int)presentModeCount];
            _khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModeCount, presentModes);

            // Pick a present mode (prefer mailbox, but FIFO is always available and works best on macOS)
            var presentMode = PresentModeKHR.FifoKhr; // Default to FIFO (vsync)
            
            // On macOS, stick with FIFO as recommended in the MoltenVK research
            if (!_isMacOS)
            {
                for (int i = 0; i < presentModeCount; i++)
                {
                    if (presentModes[i] == PresentModeKHR.MailboxKhr)
                    {
                        presentMode = PresentModeKHR.MailboxKhr;
                        break;
                    }
                }
            }
            else
            {
                Console.WriteLine("Using FIFO present mode for optimal MoltenVK compatibility");
            }
            
            Console.WriteLine($"Using present mode: {presentMode}");

            // Choose swapchain extent
            var extent = new Extent2D
            {
                Width = Math.Clamp((uint)width, surfaceCapabilities.MinImageExtent.Width, surfaceCapabilities.MaxImageExtent.Width),
                Height = Math.Clamp((uint)height, surfaceCapabilities.MinImageExtent.Height, surfaceCapabilities.MaxImageExtent.Height)
            };

            // Decide how many images in the swapchain
            uint imageCount = surfaceCapabilities.MinImageCount + 1;
            if (surfaceCapabilities.MaxImageCount > 0 && imageCount > surfaceCapabilities.MaxImageCount)
            {
                imageCount = surfaceCapabilities.MaxImageCount;
            }

            // Create the swapchain
            var createInfo = new SwapchainCreateInfoKHR
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = _surface,
                MinImageCount = imageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = extent,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ColorAttachmentBit,
                ImageSharingMode = SharingMode.Exclusive,
                PreTransform = surfaceCapabilities.CurrentTransform,
                CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
                PresentMode = presentMode,
                Clipped = true,
                OldSwapchain = default
            };

            _khrSwapchain.CreateSwapchain(_device, in createInfo, null, out _swapchain).ThrowOnError();
            Console.WriteLine($"Created swapchain with extent {extent.Width}x{extent.Height}");
        }
        
        private static unsafe void EnumerateExtensions()
        {
            // Get instance extensions
            uint extensionCount = 0;
            _vk.EnumerateInstanceExtensionProperties((byte*)null, &extensionCount, null);
            
            if (extensionCount > 0)
            {
                var extensions = stackalloc ExtensionProperties[(int)extensionCount];
                _vk.EnumerateInstanceExtensionProperties((byte*)null, &extensionCount, extensions);
                
                Console.WriteLine($"Available Vulkan instance extensions ({extensionCount}):");
                for (int i = 0; i < Math.Min(10, extensionCount); i++) // Show only the first 10
                {
                    var extName = Marshal.PtrToStringAnsi((IntPtr)extensions[i].ExtensionName);
                    Console.WriteLine($"  - {extName} (version {extensions[i].SpecVersion})");
                }
                
                if (extensionCount > 10)
                {
                    Console.WriteLine($"  ... and {extensionCount - 10} more");
                }
            }
            else
            {
                Console.WriteLine("No Vulkan instance extensions available");
            }
            
            // Get device extensions
            uint deviceExtCount = 0;
            _vk.EnumerateDeviceExtensionProperties(_physicalDevice, (byte*)null, &deviceExtCount, null);
            
            if (deviceExtCount > 0)
            {
                var deviceExts = stackalloc ExtensionProperties[(int)deviceExtCount];
                _vk.EnumerateDeviceExtensionProperties(_physicalDevice, (byte*)null, &deviceExtCount, deviceExts);
                
                Console.WriteLine($"Available device extensions ({deviceExtCount}):");
                for (int i = 0; i < Math.Min(10, deviceExtCount); i++) // Show only the first 10
                {
                    var extName = Marshal.PtrToStringAnsi((IntPtr)deviceExts[i].ExtensionName);
                    Console.WriteLine($"  - {extName} (version {deviceExts[i].SpecVersion})");
                }
                
                if (deviceExtCount > 10)
                {
                    Console.WriteLine($"  ... and {deviceExtCount - 10} more");
                }
            }
            else
            {
                Console.WriteLine("No device extensions available");
            }
        }

        private static unsafe void CreatePipelineCache()
        {
            string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CORIS");
            Directory.CreateDirectory(cacheDir);
            string cacheFile = Path.Combine(cacheDir, "vkcache.bin");
            byte[] initial = File.Exists(cacheFile) ? File.ReadAllBytes(cacheFile) : null;
            fixed(byte* pInit = initial)
            {
                var pci = new PipelineCacheCreateInfo
                {
                    SType = StructureType.PipelineCacheCreateInfo,
                    InitialDataSize = (nuint)(initial?.Length ?? 0),
                    PInitialData = initial != null ? pInit : null
                };
                _vk.CreatePipelineCache(_device, &pci, null, out _pipelineCache).ThrowOnError();
            }
        }

        private static unsafe void CleanupVulkan()
        {
            // Wait for the device to finish operations before cleanup
            if (_device.Handle != 0)
            {
                _vk.DeviceWaitIdle(_device);
            }
            
            // Destroy swapchain
            if (_swapchain.Handle != 0 && _device.Handle != 0)
            {
                _khrSwapchain.DestroySwapchain(_device, _swapchain, null);
            }
            
            // Destroy device
            if (_device.Handle != 0)
            {
                _vk.DestroyDevice(_device, null);
            }
            
            // Destroy surface
            if (_surface.Handle != 0 && _instance.Handle != 0)
            {
                _khrSurface.DestroySurface(_instance, _surface, null);
            }
            
            // Destroy instance
            if (_instance.Handle != 0)
            {
                _vk.DestroyInstance(_instance, null);
            }

            // Save pipeline cache
            if (_pipelineCache.Handle != 0 && _device.Handle != 0)
            {
                nuint size = 0;
                _vk.GetPipelineCacheData(_device, _pipelineCache, &size, null);
                if (size > 0)
                {
                    byte[] data = new byte[(int)size];
                    fixed(byte* pData = data)
                    {
                        _vk.GetPipelineCacheData(_device, _pipelineCache, &size, pData);
                    }
                    string cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CORIS");
                    Directory.CreateDirectory(cacheDir);
                    File.WriteAllBytes(Path.Combine(cacheDir, "vkcache.bin"), data);
                }
                _vk.DestroyPipelineCache(_device, _pipelineCache, null);
            }
        }

        private static unsafe HashSet<string> GetAvailableInstanceExtensions()
        {
            uint count = 0;
            _vk.EnumerateInstanceExtensionProperties((byte*)null, &count, null);
            var props = stackalloc ExtensionProperties[(int)count];
            _vk.EnumerateInstanceExtensionProperties((byte*)null, &count, props);
            var set = new HashSet<string>();
            for (int i = 0; i < count; i++)
            {
                var name = Marshal.PtrToStringAnsi((IntPtr)props[i].ExtensionName);
                if (!string.IsNullOrEmpty(name)) set.Add(name);
            }
            return set;
        }
    }

    // Extension method to throw an exception when a Vulkan function returns an error
    public static class VulkanExtensions
    {
        public static Result ThrowOnError(this Result result)
        {
            if (result != Result.Success)
            {
                throw new Exception($"Vulkan error: {result}");
            }
            return result;
        }
    }
} 