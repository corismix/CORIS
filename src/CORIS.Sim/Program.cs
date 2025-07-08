using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CORIS.Core;
using CORIS.Core.SoA;
using System.Numerics;
using Silk.NET.Windowing;

namespace CORIS.Sim
{
    class Program
    {
        private static SimulationState _simulationState = new();
        private static PhysicsEngine _physicsEngine = new(_simulationState);

        // Main entry point, handles argument dispatching
        public static void Main(string[] args)
        {
            if (args.Contains("--help") || args.Contains("-h"))
            {
                PrintHelp();
            }
            else if (args.Contains("--vulkan-test"))
            {
                Console.WriteLine("Running Vulkan test without window creation");
                VulkanDemo.RunHeadless();
            }
            else if (args.Contains("--vulkan") || args.Contains("--vk") || args.Contains("--metal"))
            {
                VulkanDemo.Run();
            }
            else if (args.Contains("--rocket-builder"))
            {
                RunRocketBuilder();
            }
            else if (args.Contains("--sim"))
            {
                RunSimulation();
            }
            else
            {
                // Default action is the "Hello Orbit" demo
                RunHelloOrbit();
            }
        }

        private static void RunRocketBuilder()
        {
            var builder = new RocketBuilder("assets/parts.json");
            builder.Run();
        }

        public static void RunHelloOrbit()
        {
            Console.WriteLine("Running 'Hello Orbit' demo...");
            Console.WriteLine("============================");

            // Simulation parameters
            const double G = 6.67430e-11; // Gravitational constant
            const float dt = 0.1f;        // Time step (s)
            const int simulationSteps = 10000;
            const int reportInterval = 500; // Print status every N steps

            // Central body (e.g., a planet)
            var centralBodyMass = 5.972e24f; // kg (mass of Earth)
            var centralBodyPosition = Vector3.Zero;

            // Satellite
            var satellite = new VesselState
            {
                Mass = 1000f, // kg
                Position = new Vector3(6.771e6f, 0, 0), // 400 km altitude above Earth radius of 6371km
            };

            // Calculate initial velocity for a stable circular orbit
            float r = satellite.Position.Length();
            float orbitalSpeed = (float)Math.Sqrt(G * centralBodyMass / r);
            satellite.Velocity = new Vector3(0, orbitalSpeed, 0);

            Console.WriteLine($"Initial State:");
            Console.WriteLine($"  Position: {satellite.Position} m");
            Console.WriteLine($"  Velocity: {satellite.Velocity} m/s (Magnitude: {satellite.Velocity.Length():F2} m/s)");
            Console.WriteLine($"  Orbital Speed for stable orbit: {orbitalSpeed:F2} m/s");
            Console.WriteLine("\nStarting simulation...\n");

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < simulationSteps; i++)
            {
                // Calculate gravitational force
                var toSatellite = satellite.Position - centralBodyPosition;
                var distSq = toSatellite.LengthSquared();
                var forceDir = Vector3.Normalize(toSatellite) * -1.0f; // Force points towards the central body
                var forceMag = (float)(G * centralBodyMass * satellite.Mass / distSq);
                var force = forceDir * forceMag;

                // Update physics state (using simple Euler integration)
                satellite.Acceleration = force / satellite.Mass;
                satellite.Velocity += satellite.Acceleration * dt;
                satellite.Position += satellite.Velocity * dt;

                if (i % reportInterval == 0)
                {
                    Console.WriteLine($"Step {i}:");
                    Console.WriteLine($"  Position: {satellite.Position} (Altitude: {(satellite.Position.Length() - 6.371e6f) / 1000:F2} km)");
                    Console.WriteLine($"  Velocity: {satellite.Velocity.Length():F2} m/s");
                }
            }

            stopwatch.Stop();
            Console.WriteLine($"\nSimulation finished in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("CORIS Simulator");
            Console.WriteLine("Usage: dotnet run --project src/CORIS.Sim -- [command]");
            Console.WriteLine("Commands:");
            Console.WriteLine("  --help, -h          Show this help message");
            Console.WriteLine("  --sim               Run the new SoA physics simulation");
            Console.WriteLine("  --rocket-builder    Run the ASCII rocket builder (WIP)");
            Console.WriteLine("  --vulkan            Run the Vulkan graphics demo");
            Console.WriteLine("  --vulkan-test       Run a headless Vulkan test");
            Console.WriteLine("  (no command)        Run the 'Hello Orbit' demo");
        }

        #region New Simulation Loop
        private static void RunSimulation()
        {
            Console.WriteLine("=== Running new SoA Physics Simulation ===");

            // 1. Create a simple rocket vessel
            var cockpit = new Piece { Id = "1", Name = "Cockpit", Mass = 500, Type = "cockpit" };
            var fuelTank = new Piece { Id = "2", Name = "Fuel Tank", Mass = 1000, Type = "tank", Properties = new() { { "fuel", 5000.0 } } };
            var enginePiece = new Piece { Id = "3", Name = "Engine", Mass = 750, Type = "engine", Properties = new() { { "thrust", 150000.0 }, { "isp", 300.0 } } };

            // Add pieces to the simulation state
            var cockpitGuid = _simulationState.AddEntity(cockpit, new Vector3(0, 2, 0), Quaternion.Identity);
            var tankGuid = _simulationState.AddEntity(fuelTank, new Vector3(0, 0, 0), Quaternion.Identity);
            var engineGuid = _simulationState.AddEntity(enginePiece, new Vector3(0, -2, 0), Quaternion.Identity);

            Console.WriteLine($"Created {cockpit.Name} with GUID: {cockpitGuid}");
            Console.WriteLine($"Created {fuelTank.Name} with GUID: {tankGuid}");
            Console.WriteLine($"Created {enginePiece.Name} with GUID: {engineGuid}");
            Console.WriteLine($"Total entities: {_simulationState.EntityCount}");

            // Create the engine struct for physics calculations
            var partEngine = new PartEngine
            {
                Thrust = (double)enginePiece.Properties["thrust"],
                Isp = (double)enginePiece.Properties["isp"],
                DryMass = enginePiece.Mass,
                Gimbal = 0
            };

            // 2. Run the simulation loop
            const int simulationSteps = 500;
            const int reportInterval = 50;
            const double dt = 1.0 / 60.0; // 60Hz update rate
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < simulationSteps; i++)
            {
                // 1. Clear forces from the previous frame
                _physicsEngine.ClearForces();

                // 2. Apply controlled forces (e.g., thrust)
                var engineIndex = _simulationState.GetEntityIndex(engineGuid);
                if (engineIndex != -1)
                {
                    // Apply 100% throttle
                    _physicsEngine.ApplyThrustForce(engineIndex, partEngine, 1.0, dt);
                }

                // 3. Update the physics engine (applies environmental forces and integrates motion)
                _physicsEngine.Update(dt);

                // 4. Report state periodically
                if (i % reportInterval == 0)
                {
                    Console.WriteLine($"\n--- Step {i} ---");
                    PrintSimulationState();
                }

                Thread.Sleep(16); // Simulate a 60Hz game loop
            }

            stopwatch.Stop();
            Console.WriteLine($"\nSimulation finished in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
        }

        private static void PrintSimulationState()
        {
            for (int i = 0; i < _simulationState.EntityCount; i++)
            {
                var guid = _simulationState.GetGuidFromIndex(i);
                var type = _simulationState.PieceTypes[i];
                var pos = _simulationState.Positions[i];
                var vel = _simulationState.Velocities[i];
                var mass = _simulationState.Masses[i];
                var fuel = _simulationState.Fuels[i];

                Console.WriteLine($"  [{i}] {type} ({guid.ToString().Substring(0, 8)})");
                Console.WriteLine($"      Pos: {pos.X:F2}, {pos.Y:F2}, {pos.Z:F2} m");
                Console.WriteLine($"      Vel: {vel.X:F2}, {vel.Y:F2}, {vel.Z:F2} m/s (Speed: {vel.Length():F2} m/s)");
                Console.WriteLine($"      Mass: {mass:F2} kg | Fuel: {fuel:F2} kg");
            }
        }
        #endregion
    }
}
