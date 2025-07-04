using System;
using System.IO;
using System.Linq;
using CORIS.Core;
using Silk.NET.Windowing;

namespace CORIS.Sim
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("--vulkan"))
            {
                VulkanDemo.Run();
                return;
            }
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
            double maxVelocity = vesselState.Velocity.Magnitude();
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
                if (vesselState.Velocity.Magnitude() > maxVelocity) maxVelocity = vesselState.Velocity.Magnitude();
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
            var thrustVec = new CORIS.Core.Vector3(0, 0, 0);
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
                        var dir = new CORIS.Core.Vector3(0, Math.Cos(pitchRad), Math.Sin(pitchRad));
                        thrustVec += dir * t;
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
            var netForce = thrustVec + new CORIS.Core.Vector3(0, -mass * gravity, 0);

            // Drag (air resistance)
            double rho = 1.225; // air density at sea level (kg/m^3)
            double Cd = 0.75;   // drag coefficient (typical for rockets)
            double A = 1.0;     // cross-sectional area (m^2)
            var v = state.Velocity;
            double vMag = v.Magnitude();
            if (vMag > 0)
            {
                var dragDir = v * (-1.0 / vMag); // opposite to velocity
                double dragMag = 0.5 * rho * Cd * A * vMag * vMag;
                var drag = dragDir * dragMag;
                netForce += drag;
            }

            state.Acceleration = netForce / mass;
            state.Velocity += state.Acceleration * 1.0; // dt = 1s
            state.Position += state.Velocity * 1.0; // dt = 1s

            // Tsiolkovsky: update fuel and mass
            fuel.Fuel -= fuelUsed;
            if (fuel.Fuel < 0) fuel.Fuel = 0;

            // Stub: update orientation and angular velocity (simulate gimbal/RCS)
            if (fuel.Fuel > 0)
            {
                double angularAccel = 1.0; // deg/s^2, simple constant
                var angVel = state.AngularVelocity;
                angVel.Y += angularAccel * 1.0; // pitch axis
                state.AngularVelocity = angVel;
            }
            state.Orientation += state.AngularVelocity * 1.0; // deg/s * dt
        }

        static void UpdateSubstep(Vessel vessel, VesselState state, FuelState fuel, double dt)
        {
            // 3D physics: thrust in orientation, gravity in -Y
            var thrustVec = new CORIS.Core.Vector3(0, 0, 0);
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
                        var dir = new CORIS.Core.Vector3(0, Math.Cos(pitchRad), Math.Sin(pitchRad));
                        thrustVec += dir * t;
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
            var netForce = thrustVec + new CORIS.Core.Vector3(0, -mass * gravity, 0);

            // Drag (air resistance)
            double rho = 1.225; // air density at sea level (kg/m^3)
            double Cd = 0.75;   // drag coefficient (typical for rockets)
            double A = 1.0;     // cross-sectional area (m^2)
            var v = state.Velocity;
            double vMag = v.Magnitude();
            if (vMag > 0)
            {
                var dragDir = v * (-1.0 / vMag); // opposite to velocity
                double dragMag = 0.5 * rho * Cd * A * vMag * vMag;
                var drag = dragDir * dragMag;
                netForce += drag;
            }

            state.Acceleration = netForce / mass;
            state.Velocity += state.Acceleration * dt;
            state.Position += state.Velocity * dt;

            // Tsiolkovsky: update fuel and mass
            fuel.Fuel -= fuelUsed;
            if (fuel.Fuel < 0) fuel.Fuel = 0;

            // Stub: update orientation and angular velocity (simulate gimbal/RCS)
            if (fuel.Fuel > 0)
            {
                double angularAccel = 1.0; // deg/s^2, simple constant
                var angVel = state.AngularVelocity;
                angVel.Y += angularAccel * dt; // pitch axis
                state.AngularVelocity = angVel;
            }
            state.Orientation += state.AngularVelocity * dt; // deg/s * dt
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

