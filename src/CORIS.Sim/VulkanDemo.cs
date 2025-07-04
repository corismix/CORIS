using System;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace CORIS.Sim
{
    public static class VulkanDemo
    {
        private static Vk _vk = null!;
        private static IWindow _window = null!;
        private static Instance _instance;
        private static SurfaceKHR _surface;
        private static PhysicalDevice _physicalDevice;
        private static Device _device;
        private static Queue _graphicsQueue;
        private static Queue _presentQueue;
        private static SwapchainKHR _swapchain;
        private static uint _graphicsQueueFamilyIndex;
        private static uint _presentQueueFamilyIndex;
        private static bool _enableValidation = false;
        private static bool _enableMoltenVKTrace = false;

        public static void Run(bool enableValidation = false, bool enableMoltenVKTrace = false)
        {
            _enableValidation = enableValidation;
            _enableMoltenVKTrace = enableMoltenVKTrace;

            Console.WriteLine("=== CORIS High-Performance Vulkan Graphics Engine ===");
            Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
            Console.WriteLine($"Architecture: {RuntimeInformation.OSArchitecture}");
            
            // Set MoltenVK environment variables for optimal performance
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Console.WriteLine("macOS detected - configuring MoltenVK optimizations");
                Environment.SetEnvironmentVariable("MVK_CONFIG_USE_METAL_ARGUMENT_BUFFERS", "1");
                Environment.SetEnvironmentVariable("MVK_CONFIG_SHADER_SOURCE_COMPRESSION", "lz4");
                if (_enableMoltenVKTrace)
                {
                    Environment.SetEnvironmentVariable("MVK_CONFIG_TRACE_VULKAN_CALLS", "1");
                }
            }

            try
            {
                InitializeWindow();
                InitializeVulkan();
                MainLoop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                Cleanup();
            }
        }

        private static void InitializeWindow()
        {
            var options = WindowOptions.DefaultVulkan;
            options.Title = "CORIS - High-Performance Rocketry Simulation";
            options.Size = new Silk.NET.Maths.Vector2D<int>(1920, 1080);
            options.VSync = true;
            
            _window = Window.Create(options);
            _window.Load += OnWindowLoad;
            _window.Render += OnWindowRender;
            _window.Closing += OnWindowClosing;
            _window.FramebufferResize += OnFramebufferResize;
        }

        private static void InitializeVulkan()
        {
            _vk = Vk.GetApi();
            CreateInstance();
            CreateSurface();
            PickPhysicalDevice();
            CreateLogicalDevice();
            CreateSwapchain();
            
            Console.WriteLine("Vulkan initialization complete - ready for high-performance rendering");
        }

        private static void CreateInstance()
        {
            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)SilkMarshal.StringToPtr("CORIS Rocketry Simulation"),
                ApplicationVersion = new Version32(1, 0, 0),
                PEngineName = (byte*)SilkMarshal.StringToPtr("CORIS Engine"),
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version12
            };

            // Get required extensions from GLFW/windowing
            var glfwExtensions = _window.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
            var extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);

            // Add platform-specific extensions
            var extensionList = new List<string>(extensions);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // MoltenVK portability extensions
                extensionList.Add("VK_KHR_portability_enumeration");
                extensionList.Add("VK_KHR_get_physical_device_properties2");
                Console.WriteLine("Added MoltenVK portability extensions");
            }

            if (_enableValidation)
            {
                extensionList.Add("VK_EXT_debug_utils");
            }

            var enabledExtensions = extensionList.ToArray();
            
            Console.WriteLine($"Enabled instance extensions: {string.Join(", ", enabledExtensions)}");

            var layers = new List<string>();
            if (_enableValidation)
            {
                layers.Add("VK_LAYER_KHRONOS_validation");
                Console.WriteLine("Validation layers enabled");
            }

            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)enabledExtensions.Length,
                PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(enabledExtensions),
                EnabledLayerCount = (uint)layers.Count,
                PpEnabledLayerNames = layers.Count > 0 ? (byte**)SilkMarshal.StringArrayToPtr(layers.ToArray()) : null
            };

            // Add portability flag for MoltenVK
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                createInfo.Flags |= InstanceCreateFlags.EnumeratePortabilityBitKhr;
            }

            var result = _vk.CreateInstance(in createInfo, null, out _instance);
            if (result != Result.Success)
            {
                throw new Exception($"Failed to create Vulkan instance: {result}");
            }

            Console.WriteLine("Vulkan instance created successfully");

            // Clean up
            SilkMarshal.Free((nint)appInfo.PApplicationName);
            SilkMarshal.Free((nint)appInfo.PEngineName);
            SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
            if (createInfo.PpEnabledLayerNames != null)
                SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
        }

        private static void CreateSurface()
        {
            _surface = _window.VkSurface!.Create<AllocationCallbacks>(_instance.ToHandle(), null).ToSurface();
            Console.WriteLine("Vulkan surface created");
        }

        private static void PickPhysicalDevice()
        {
            uint deviceCount = 0;
            _vk.EnumeratePhysicalDevices(_instance, &deviceCount, null);
            
            if (deviceCount == 0)
            {
                throw new Exception("Failed to find GPUs with Vulkan support");
            }

            var devices = new PhysicalDevice[deviceCount];
            _vk.EnumeratePhysicalDevices(_instance, &deviceCount, devices);

            PhysicalDevice? selectedDevice = null;
            int bestScore = -1;

            foreach (var device in devices)
            {
                var score = RateDeviceSuitability(device);
                if (score > bestScore)
                {
                    bestScore = score;
                    selectedDevice = device;
                }
            }

            if (selectedDevice == null)
            {
                throw new Exception("Failed to find a suitable GPU");
            }

            _physicalDevice = selectedDevice.Value;
            
            // Print device info
            _vk.GetPhysicalDeviceProperties(_physicalDevice, out var properties);
            string deviceName = SilkMarshal.PtrToString((nint)properties.DeviceName);
            Console.WriteLine($"Selected GPU: {deviceName}");
            Console.WriteLine($"Vulkan API Version: {properties.ApiVersion >> 22}.{(properties.ApiVersion >> 12) & 0x3ff}.{properties.ApiVersion & 0xfff}");
            Console.WriteLine($"Driver Version: {properties.DriverVersion}");
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Console.WriteLine("Running on MoltenVK - Vulkan to Metal translation layer");
            }
        }

        private static int RateDeviceSuitability(PhysicalDevice device)
        {
            _vk.GetPhysicalDeviceProperties(device, out var properties);
            _vk.GetPhysicalDeviceFeatures(device, out var features);

            int score = 0;

            // Prefer discrete GPUs
            if (properties.DeviceType == PhysicalDeviceType.DiscreteGpu)
                score += 1000;

            // Maximum possible size of textures affects graphics quality
            score += (int)properties.Limits.MaxImageDimension2D;

            // Must support geometry shaders (if not on MoltenVK)
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !features.GeometryShader)
                return 0;

            // Must have required queue families
            var indices = FindQueueFamilies(device);
            if (!indices.IsComplete())
                return 0;

            // Check extension support
            if (!CheckDeviceExtensionSupport(device))
                return 0;

            return score;
        }

        private static QueueFamilyIndices FindQueueFamilies(PhysicalDevice device)
        {
            var indices = new QueueFamilyIndices();

            uint queueFamilyCount = 0;
            _vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);
            
            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            _vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, queueFamilies);

            uint i = 0;
            foreach (var queueFamily in queueFamilies)
            {
                if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                {
                    indices.GraphicsFamily = i;
                }

                // Check for present support
                _vk.TryGetInstanceExtension(_instance, out KhrSurface khrSurface);
                khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, _surface, out var presentSupport);
                if (presentSupport)
                {
                    indices.PresentFamily = i;
                }

                if (indices.IsComplete())
                    break;

                i++;
            }

            return indices;
        }

        private static bool CheckDeviceExtensionSupport(PhysicalDevice device)
        {
            uint extensionCount = 0;
            _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, &extensionCount, null);
            
            var availableExtensions = new ExtensionProperties[extensionCount];
            _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, &extensionCount, availableExtensions);

            var requiredExtensions = new HashSet<string> { "VK_KHR_swapchain" };
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                requiredExtensions.Add("VK_KHR_portability_subset");
            }

            foreach (var extension in availableExtensions)
            {
                string name = SilkMarshal.PtrToString((nint)extension.ExtensionName);
                requiredExtensions.Remove(name);
            }

            return requiredExtensions.Count == 0;
        }

        private static void CreateLogicalDevice()
        {
            var indices = FindQueueFamilies(_physicalDevice);
            _graphicsQueueFamilyIndex = indices.GraphicsFamily!.Value;
            _presentQueueFamilyIndex = indices.PresentFamily!.Value;

            var uniqueQueueFamilies = new HashSet<uint> { indices.GraphicsFamily!.Value, indices.PresentFamily!.Value };
            var queueCreateInfos = new List<DeviceQueueCreateInfo>();

            float queuePriority = 1.0f;
            foreach (var queueFamily in uniqueQueueFamilies)
            {
                var queueCreateInfo = new DeviceQueueCreateInfo
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = queueFamily,
                    QueueCount = 1,
                    PQueuePriorities = &queuePriority
                };
                queueCreateInfos.Add(queueCreateInfo);
            }

            var deviceFeatures = new PhysicalDeviceFeatures();

            var extensions = new List<string> { "VK_KHR_swapchain" };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                extensions.Add("VK_KHR_portability_subset");
            }

            var enabledExtensions = extensions.ToArray();

            DeviceCreateInfo createInfo;
            fixed (DeviceQueueCreateInfo* queueCreateInfosPtr = queueCreateInfos.ToArray())
            {
                createInfo = new DeviceCreateInfo
                {
                    SType = StructureType.DeviceCreateInfo,
                    QueueCreateInfoCount = (uint)queueCreateInfos.Count,
                    PQueueCreateInfos = queueCreateInfosPtr,
                    PEnabledFeatures = &deviceFeatures,
                    EnabledExtensionCount = (uint)enabledExtensions.Length,
                    PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(enabledExtensions)
                };

                var result = _vk.CreateDevice(_physicalDevice, in createInfo, null, out _device);
                if (result != Result.Success)
                {
                    throw new Exception($"Failed to create logical device: {result}");
                }
            }

            _vk.GetDeviceQueue(_device, _graphicsQueueFamilyIndex, 0, out _graphicsQueue);
            _vk.GetDeviceQueue(_device, _presentQueueFamilyIndex, 0, out _presentQueue);

            Console.WriteLine("Vulkan logical device created");
            Console.WriteLine($"Graphics queue family: {_graphicsQueueFamilyIndex}");
            Console.WriteLine($"Present queue family: {_presentQueueFamilyIndex}");

            SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
        }

        private static void CreateSwapchain()
        {
            _vk.TryGetInstanceExtension(_instance, out KhrSurface khrSurface);
            _vk.TryGetDeviceExtension(_device, out KhrSwapchain khrSwapchain);

            // Query swapchain support
            khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out var capabilities);

            uint formatCount = 0;
            khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, null);
            var formats = new SurfaceFormatKHR[formatCount];
            khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, &formatCount, formats);

            uint presentModeCount = 0;
            khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModeCount, null);
            var presentModes = new PresentModeKHR[presentModeCount];
            khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, &presentModeCount, presentModes);

            // Choose optimal settings
            var surfaceFormat = ChooseSwapSurfaceFormat(formats);
            var presentMode = ChooseSwapPresentMode(presentModes);
            var extent = ChooseSwapExtent(capabilities);

            uint imageCount = capabilities.MinImageCount + 1;
            if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
            {
                imageCount = capabilities.MaxImageCount;
            }

            var createInfo = new SwapchainCreateInfoKHR
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = _surface,
                MinImageCount = imageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = extent,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ColorAttachmentBit
            };

            if (_graphicsQueueFamilyIndex != _presentQueueFamilyIndex)
            {
                var queueFamilyIndices = new uint[] { _graphicsQueueFamilyIndex, _presentQueueFamilyIndex };
                fixed (uint* queueFamilyIndicesPtr = queueFamilyIndices)
                {
                    createInfo.ImageSharingMode = SharingMode.Concurrent;
                    createInfo.QueueFamilyIndexCount = 2;
                    createInfo.PQueueFamilyIndices = queueFamilyIndicesPtr;
                }
            }
            else
            {
                createInfo.ImageSharingMode = SharingMode.Exclusive;
            }

            createInfo.PreTransform = capabilities.CurrentTransform;
            createInfo.CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr;
            createInfo.PresentMode = presentMode;
            createInfo.Clipped = true;
            createInfo.OldSwapchain = default;

            var result = khrSwapchain.CreateSwapchain(_device, createInfo, null, out _swapchain);
            if (result != Result.Success)
            {
                throw new Exception($"Failed to create swapchain: {result}");
            }

            Console.WriteLine($"Swapchain created: {extent.Width}x{extent.Height}, {surfaceFormat.Format}, {presentMode}");
        }

        private static SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
        {
            foreach (var format in availableFormats)
            {
                if (format.Format == Format.B8G8R8A8Srgb && format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                {
                    return format;
                }
            }
            return availableFormats[0];
        }

        private static PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
        {
            // Prefer FIFO on macOS for MoltenVK compatibility
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return PresentModeKHR.FifoKhr;
            }

            foreach (var mode in availablePresentModes)
            {
                if (mode == PresentModeKHR.MailboxKhr)
                {
                    return mode;
                }
            }
            return PresentModeKHR.FifoKhr;
        }

        private static Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.CurrentExtent.Width != uint.MaxValue)
            {
                return capabilities.CurrentExtent;
            }

            var size = _window.FramebufferSize;
            return new Extent2D
            {
                Width = Math.Clamp((uint)size.X, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
                Height = Math.Clamp((uint)size.Y, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height)
            };
        }

        private static void MainLoop()
        {
            _window.Run();
        }

        private static void OnWindowLoad()
        {
            Console.WriteLine("Vulkan window loaded - entering main render loop");
        }

        private static void OnWindowRender(double deltaTime)
        {
            // High-performance render loop stub
            // This will be expanded with PBR pipeline, command buffer recording, etc.
        }

        private static void OnFramebufferResize(Silk.NET.Maths.Vector2D<int> newSize)
        {
            Console.WriteLine($"Framebuffer resized to {newSize.X}x{newSize.Y}");
            // TODO: Recreate swapchain
        }

        private static void OnWindowClosing()
        {
            Console.WriteLine("Vulkan window closing");
        }

        private static void Cleanup()
        {
            if (_device.Handle != 0)
            {
                _vk.DeviceWaitIdle(_device);
                
                if (_swapchain.Handle != 0)
                {
                    _vk.TryGetDeviceExtension(_device, out KhrSwapchain khrSwapchain);
                    khrSwapchain.DestroySwapchain(_device, _swapchain, null);
                }
                
                _vk.DestroyDevice(_device, null);
            }

            if (_surface.Handle != 0)
            {
                _vk.TryGetInstanceExtension(_instance, out KhrSurface khrSurface);
                khrSurface.DestroySurface(_instance, _surface, null);
            }

            if (_instance.Handle != 0)
            {
                _vk.DestroyInstance(_instance, null);
            }

            _window?.Dispose();
            Console.WriteLine("Vulkan cleanup complete");
        }
    }

    public struct QueueFamilyIndices
    {
        public uint? GraphicsFamily;
        public uint? PresentFamily;

        public bool IsComplete() => GraphicsFamily.HasValue && PresentFamily.HasValue;
    }
} 