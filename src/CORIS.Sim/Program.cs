using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CORIS.Core;
using Silk.NET.Windowing;
using System.Numerics;

namespace CORIS.Sim
{
    class Program
    {
        static void Main(string[] args)
        {
            // Check for help flag
            if (args.Contains("--help") || args.Contains("-h"))
            {
                PrintHelp();
                return;
            }
            
            // Check for Vulkan test flag
            if (args.Contains("--vulkan-test"))
            {
                Console.WriteLine("Running Vulkan test without window creation");
                VulkanTest.RunHeadlessTest();
                return;
            }
            
            // Check for Vulkan demo flag
            if (args.Contains("--vulkan") || args.Contains("--vk") || args.Contains("--metal"))
            {
                bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
                
                Console.WriteLine("Starting CORIS Vulkan Demo");
                Console.WriteLine("=========================");
                
                // Detect platform and inform about MoltenVK on macOS
                if (isMacOS)
                {
                    Console.WriteLine("macOS detected: Using MoltenVK to translate Vulkan to Metal");
                    Console.WriteLine("MoltenVK is loaded automatically via Silk.NET.MoltenVK.Native");
                    Console.WriteLine("MoltenVK translates Vulkan API calls to Metal under the hood");
                    Console.WriteLine("This allows the same Vulkan code to run on macOS without changes");
                    
                    if (args.Contains("--metal"))
                    {
                        Console.WriteLine("Note: --metal flag detected - this is the same as --vulkan on macOS");
                    }
                }
                else if (args.Contains("--metal"))
                {
                    Console.WriteLine("Warning: --metal flag is only relevant on macOS");
                    Console.WriteLine("On this platform, native Vulkan is used directly");
                }
                
                // Parse extra Vulkan dev flags
                bool enableVkTrace = args.Contains("--vk-trace");
                bool enableVkValidation = args.Contains("--vk-validate");

                if (enableVkTrace)
                {
                    Environment.SetEnvironmentVariable("MVK_CONFIG_TRACE_VULKAN_CALLS", "1");
                }
                if (enableVkValidation)
                {
                    Environment.SetEnvironmentVariable("VK_LAYER_PATH", string.Empty); // let loader locate default validation layers
                    Environment.SetEnvironmentVariable("VK_INSTANCE_LAYERS", "VK_LAYER_KHRONOS_validation");
                }
                
                try
                {
                    VulkanDemo.Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
                return;
            }
            
            // Regular simulation mode
            RunSimulation();
        }
        
        static void PrintHelp()
        {
            Console.WriteLine("CORIS Rocketry Simulation Engine");
            Console.WriteLine("===============================");
            Console.WriteLine("Command line options:");
            Console.WriteLine("  --help, -h        : Show this help message");
            Console.WriteLine("  --vulkan, --vk    : Run the Vulkan rendering demo");
            Console.WriteLine("  --vulkan-test     : Run a basic Vulkan test without window creation");
            Console.WriteLine("  --metal           : Run with Metal rendering on macOS (same as --vulkan)");
            Console.WriteLine("  --vk-trace        : Enable MoltenVK call tracing (macOS only)");
            Console.WriteLine("  --vk-validate     : Enable Vulkan validation layers if available");
            Console.WriteLine();
            Console.WriteLine("On macOS, the Vulkan API calls are automatically translated to Metal");
            Console.WriteLine("using MoltenVK. This is transparent to the application code.");
        }
        
        static void RunSimulation()
        {
            // Load parts from JSON
            string partsPath = Path.Combine("assets", "parts.json");
            var parts = ModLoader.LoadPartsFromJson(partsPath);
            Console.WriteLine($"Loaded {parts.Count} parts from {partsPath}");

            // List all loaded parts and their pieces/properties
            foreach (var part in parts)
            {
                Console.WriteLine($"Part: {part.Name} (Id: {part.Id})");
                foreach (var piece in part.Pieces)
                {
                    Console.WriteLine($"  Piece: {piece.Type} (Id: {piece.Id}), Mass: {piece.Mass}");
                    if (piece.Properties != null && piece.Properties.Count > 0)
                    {
                        Console.WriteLine("    Properties:");
                        foreach (var kv in piece.Properties)
                        {
                            Console.WriteLine($"      {kv.Key}: {kv.Value}");
                        }
                    }
                }
            }

            // Build a vessel from all loaded parts
            var vessel = new Vessel { Id = "vessel-1", Name = "Modded Rocket", Parts = parts };
            Console.WriteLine($"Vessel: {vessel.Name}");
            Console.WriteLine($"Total Mass: {vessel.Mass} units");

            // Create a vessel state for physics
            var vesselState = new VesselState();

            // Calculate initial fuel (sum all tank pieces)
            double initialFuel = 0;
            foreach (var part in vessel.Parts)
            {
                foreach (var piece in part.Pieces)
                {
                    if (piece.Type == "tank" && piece.Properties != null && piece.Properties.TryGetValue("fuel", out var f))
                        initialFuel += f;
                }
            }
            var fuelState = new FuelState { Fuel = initialFuel };

            // Collect altitude history for ASCII graph (now Y position)
            var altitudeHistory = new System.Collections.Generic.List<double>();
            double maxAltitude = vesselState.Position.Y;
            double maxVelocity = vesselState.Velocity.Length();
            string stopReason = "";
            int step = 0;
            bool staged = false;
            var pitchHistory = new System.Collections.Generic.List<double>();
            while (true)
            {
                // Sub-stepping: 10 sub-steps per main step
                for (int sub = 0; sub < 10; sub++)
                {
                    UpdateSubstep(vessel, vesselState, fuelState, 0.1);
                }
                altitudeHistory.Add(vesselState.Position.Y);
                pitchHistory.Add(vesselState.Orientation.Y);
                if (vesselState.Position.Y > maxAltitude) maxAltitude = vesselState.Position.Y;
                if (vesselState.Velocity.Length() > maxVelocity) maxVelocity = vesselState.Velocity.Length();
                step++;
                if (step % 10 == 0 || fuelState.Fuel <= 0 || vesselState.Position.Y < 0)
                {
                    Render(vessel, vesselState, fuelState, step);
                }
                if (fuelState.Fuel <= 0 && !staged)
                {
                    // Jettison first tank part
                    var tank = vessel.Parts.FirstOrDefault(p => p.Pieces.Any(pc => pc.Type == "tank"));
                    if (tank != null)
                    {
                        vessel.Parts.Remove(tank);
                        Console.WriteLine($"[Staging] Jettisoned part: {tank.Name}");
                        staged = true;
                        continue;
                    }
                }
                if (fuelState.Fuel <= 0)
                {
                    stopReason = "Out of fuel";
                    break;
                }
                if (vesselState.Position.Y < 0)
                {
                    stopReason = $"Crashed at step {step}, position {vesselState.Position.Y:F2} m";
                    break;
                }
            }
            // Flight summary
            Console.WriteLine("\n--- Flight Summary ---");
            Console.WriteLine($"Max altitude: {maxAltitude:F2} m");
            Console.WriteLine($"Max velocity: {maxVelocity:F2} m/s");
            Console.WriteLine($"Total flight time: {step} s");
            Console.WriteLine($"Reason for stop: {stopReason}");

            // ASCII graph of altitude (Y)
            PrintAsciiAltitudeGraph(altitudeHistory, maxAltitude);
            PrintAsciiPitchGraph(pitchHistory);

            // End of simulation
            Console.WriteLine("\nSimulation complete. Press Enter to exit.");
            Console.ReadLine();
        }

        static void Update(Vessel vessel, VesselState state, FuelState fuel)
        {
            // 3D physics: thrust in orientation, gravity in -Y
            var thrustVec = new Vector3(0, 0, 0);
            double fuelUsed = 0.0;
            double g0 = 9.80665; // standard gravity
            double dryMass = 0;
            double totalIsp = 0;
            double totalThrust = 0;
            foreach (var part in vessel.Parts)
            {
                foreach (var piece in part.Pieces)
                {
                    if (piece.Type == "engine" && piece.Properties != null && piece.Properties.TryGetValue("thrust", out var t) && piece.Properties.TryGetValue("isp", out var isp))
                    {
                        double gimbal = 0.0;
                        if (piece.Properties.TryGetValue("gimbal", out var g))
                            gimbal = g;
                        double pitch = state.Orientation.Y + gimbal;
                        double pitchRad = pitch * Math.PI / 180.0;
                        var dir = new Vector3(0f, (float)Math.Cos(pitchRad), (float)Math.Sin(pitchRad));
                        thrustVec += dir * (float)t;
                        totalThrust += t;
                        totalIsp += isp;
                        if (fuel.Fuel > 0)
                        {
                            // Tsiolkovsky: mDot = Thrust / (Isp * g0)
                            double mDot = t / (isp * g0); // fuel per second
                            fuelUsed += mDot;
                        }
                    }
                }
            }
            // Reduce vessel mass as fuel burns
            foreach (var part in vessel.Parts)
            {
                foreach (var piece in part.Pieces)
                {
                    if (piece.Type == "tank" && piece.Properties != null && piece.Properties.TryGetValue("fuel", out var f))
                        dryMass += piece.Mass; // tank mass only
                    else
                        dryMass += piece.Mass;
                }
            }
            double mass = dryMass + fuel.Fuel;
            double gravity = 9.81; // m/s^2, Earth gravity
            // Net force: sum of all engine thrusts, gravity in -Y
            var netForce = thrustVec + new Vector3(0, (float)(-mass * gravity), 0);

            // Drag (air resistance)
            double rho = 1.225; // air density at sea level (kg/m^3)
            double Cd = 0.75;   // drag coefficient (typical for rockets)
            double A = 1.0;     // cross-sectional area (m^2)
            var v = state.Velocity;
            double vMag = v.Length();
            if (vMag > 0)
            {
                var dragDir = v * (float)(-1.0 / vMag); // opposite to velocity
                double dragMag = 0.5 * rho * Cd * A * vMag * vMag;
                var drag = dragDir * (float)dragMag;
                netForce += drag;
            }

            state.Acceleration = netForce / (float)mass;
            state.Velocity += state.Acceleration * 1.0f; // dt = 1s
            state.Position += state.Velocity * 1.0f; // dt = 1s

            // Tsiolkovsky: update fuel and mass
            fuel.Fuel -= fuelUsed;
            if (fuel.Fuel < 0) fuel.Fuel = 0;

            // Stub: update orientation and angular velocity (simulate gimbal/RCS)
            if (fuel.Fuel > 0)
            {
                double angularAccel = 1.0; // deg/s^2, simple constant
                var angVel = state.AngularVelocity;
                angVel.Y += (float)angularAccel * 1.0f; // pitch axis
                state.AngularVelocity = angVel;
            }
            state.Orientation += state.AngularVelocity * 1.0f; // deg/s * dt
        }

        static void UpdateSubstep(Vessel vessel, VesselState state, FuelState fuel, double dt)
        {
            // 3D physics: thrust in orientation, gravity in -Y
            var thrustVec = new Vector3(0, 0, 0);
            double fuelUsed = 0.0;
            double g0 = 9.80665; // standard gravity
            double dryMass = 0;
            double totalIsp = 0;
            double totalThrust = 0;
            foreach (var part in vessel.Parts)
            {
                foreach (var piece in part.Pieces)
                {
                    if (piece.Type == "engine" && piece.Properties != null && piece.Properties.TryGetValue("thrust", out var t) && piece.Properties.TryGetValue("isp", out var isp))
                    {
                        double gimbal = 0.0;
                        if (piece.Properties.TryGetValue("gimbal", out var g))
                            gimbal = g;
                        double pitch = state.Orientation.Y + gimbal;
                        double pitchRad = pitch * Math.PI / 180.0;
                        var dir = new Vector3(0f, (float)Math.Cos(pitchRad), (float)Math.Sin(pitchRad));
                        thrustVec += dir * (float)t;
                        totalThrust += t;
                        totalIsp += isp;
                        if (fuel.Fuel > 0)
                        {
                            // Tsiolkovsky: mDot = Thrust / (Isp * g0)
                            double mDot = t / (isp * g0); // fuel per second
                            fuelUsed += mDot * dt;
                        }
                    }
                }
            }
            // Reduce vessel mass as fuel burns
            foreach (var part in vessel.Parts)
            {
                foreach (var piece in part.Pieces)
                {
                    if (piece.Type == "tank" && piece.Properties != null && piece.Properties.TryGetValue("fuel", out var f))
                        dryMass += piece.Mass; // tank mass only
                    else
                        dryMass += piece.Mass;
                }
            }
            double mass = dryMass + fuel.Fuel;
            double gravity = 9.81; // m/s^2, Earth gravity
            // Net force: sum of all engine thrusts, gravity in -Y
            var netForce = thrustVec + new Vector3(0, (float)(-mass * gravity), 0);

            // Drag (air resistance)
            double rho = 1.225; // air density at sea level (kg/m^3)
            double Cd = 0.75;   // drag coefficient (typical for rockets)
            double A = 1.0;     // cross-sectional area (m^2)
            var v = state.Velocity;
            double vMag = v.Length();
            if (vMag > 0)
            {
                var dragDir = v * (float)(-1.0 / vMag); // opposite to velocity
                double dragMag = 0.5 * rho * Cd * A * vMag * vMag;
                var drag = dragDir * (float)dragMag;
                netForce += drag;
            }

            state.Acceleration = netForce / (float)mass;
            state.Velocity += state.Acceleration * (float)dt;
            state.Position += state.Velocity * (float)dt;

            // Tsiolkovsky: update fuel and mass
            fuel.Fuel -= fuelUsed;
            if (fuel.Fuel < 0) fuel.Fuel = 0;

            // Stub: update orientation and angular velocity (simulate gimbal/RCS)
            if (fuel.Fuel > 0)
            {
                double angularAccel = 1.0; // deg/s^2, simple constant
                var angVel = state.AngularVelocity;
                angVel.Y += (float)angularAccel * (float)dt; // pitch axis
                state.AngularVelocity = angVel;
            }
            state.Orientation += state.AngularVelocity * (float)dt; // deg/s * dt
        }

        static void Render(Vessel vessel, VesselState state, FuelState fuel, int step)
        {
            Console.WriteLine($"[Step {step}] Vessel mass: {vessel.Mass}, Fuel: {fuel.Fuel:F2}");
            Console.WriteLine($"[Physics] Pos: {state.Position}, Vel: {state.Velocity}, Accel: {state.Acceleration}");
            Console.WriteLine($"[Attitude] Orientation (Yaw, Pitch, Roll): {state.Orientation}, Angular Vel: {state.AngularVelocity}");
            Console.WriteLine($"[Render] Drawing vessel: {vessel.Name}");
        }

        static void PrintAsciiAltitudeGraph(System.Collections.Generic.List<double> history, double maxAlt)
        {
            const int rows = 40;
            int cols = history.Count;
            double minAlt = 0;
            double range = maxAlt - minAlt;
            if (range < 1) range = 1;
            char[,] grid = new char[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    grid[r, c] = ' ';
            for (int c = 0; c < cols; c++)
            {
                int row = rows - 1 - (int)(((history[c] - minAlt) / range) * (rows - 1));
                if (row < 0) row = 0;
                if (row >= rows) row = rows - 1;
                grid[row, c] = '*';
            }
            Console.WriteLine("\n--- Altitude (ASCII Graph) ---");
            for (int r = 0; r < rows; r++)
            {
                double alt = minAlt + (range * (rows - 1 - r)) / (rows - 1);
                Console.Write($"{alt,8:F0}m |");
                for (int c = 0; c < cols; c++)
                    Console.Write(grid[r, c]);
                Console.WriteLine();
            }
        }

        static void PrintAsciiPitchGraph(System.Collections.Generic.List<double> history)
        {
            const int rows = 20;
            int cols = history.Count;
            double minPitch = history.Min();
            double maxPitch = history.Max();
            double range = maxPitch - minPitch;
            if (range < 1) range = 1;
            char[,] grid = new char[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    grid[r, c] = ' ';
            for (int c = 0; c < cols; c++)
            {
                int row = rows - 1 - (int)(((history[c] - minPitch) / range) * (rows - 1));
                if (row < 0) row = 0;
                if (row >= rows) row = rows - 1;
                grid[row, c] = '*';
            }
            Console.WriteLine("\n--- Pitch (ASCII Graph) ---");
            for (int r = 0; r < rows; r++)
            {
                double pitch = minPitch + (range * (rows - 1 - r)) / (rows - 1);
                Console.Write($"{pitch,8:F0}°|");
                for (int c = 0; c < cols; c++)
                    Console.Write(grid[r, c]);
                Console.WriteLine();
            }
        }
    }
}

