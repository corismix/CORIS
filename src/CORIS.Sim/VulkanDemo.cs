using Silk.NET.Windowing;
using System;

namespace CORIS.Sim
{
    public static class VulkanDemo
    {
        public static void Run()
        {
            var options = WindowOptions.DefaultVulkan;
            options.Title = "CORIS Vulkan Demo";
            options.Size = new Silk.NET.Maths.Vector2D<int>(800, 600);
            using var window = Window.Create(options);
            window.Load += () => Console.WriteLine("Vulkan window loaded.");
            window.Render += delta => { /* Clear screen, stub for now */ };
            window.Closing += () => Console.WriteLine("Vulkan window closing.");
            window.Run();
        }
    }
} 