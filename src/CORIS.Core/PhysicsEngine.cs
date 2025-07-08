using System;
using System.Numerics;
using System.Runtime.InteropServices;
using CORIS.Core.SoA;

namespace CORIS.Core
{
    /// <summary>
    /// High-performance physics engine implementing data-oriented design
    /// Demonstrates the "Piece-Part-Vessel" hierarchy from the architectural blueprint
    /// Note: This is a simplified implementation for demonstration purposes
    /// </summary>
    public class PhysicsEngine : IDisposable
    {
        // The simulation state, managed externally, using a Structure-of-Arrays (SoA) layout.
        private readonly SimulationState _simulationState;

        // Performance tracking and configuration
        private double _lastUpdateTime;
        private int _currentStep = 0;
        private readonly Vector3 _gravity = new Vector3(0, -9.81f, 0);

        // High-performance physics settings optimized for rocketry simulation
        private const int SubStepsPerFrame = 4; // For stable high-speed physics
        private const double TimeStepLimit = 1.0 / 120.0; // 120Hz max timestep

        public PhysicsEngine(SimulationState simulationState)
        {
            Console.WriteLine("=== Initializing CORIS High-Performance Physics Engine ===");

            _simulationState = simulationState;

            Console.WriteLine("Physics engine initialized successfully");
            Console.WriteLine($"Max timestep: {TimeStepLimit * 1000:F1}ms");
            Console.WriteLine($"Sub-steps per frame: {SubStepsPerFrame}");
        }

        /// <summary>
        /// Applies thrust as a force to a given entity and updates its mass and fuel.
        /// This is now a force-based model, consistent with gravity and drag.
        /// </summary>
        public void ApplyThrustForce(int entityIndex, in PartEngine engine, double throttle, double dt)
        {
            // Ensure throttle is within [0, 1]
            throttle = Math.Clamp(throttle, 0.0, 1.0);

            if (_simulationState.Fuels[entityIndex] <= 0 || engine.Thrust <= 0 || throttle <= 0) return;

            // Apply thrust force
            Vector3 thrustDirection = Vector3.Transform(Vector3.UnitZ, _simulationState.Orientations[entityIndex]);
            Vector3 thrustForce = thrustDirection * (float)engine.Thrust * (float)throttle;
            _simulationState.Forces[entityIndex] += thrustForce;

            // --- Fuel Consumption and Mass Reduction ---
            const double g0 = 9.80665; // Standard gravity (m/s²)

            // Calculate mass flow rate: mDot = Thrust / (Isp * g0)
            double mDot = (engine.Thrust / (engine.Isp * g0)) * throttle;

            // Update mass based on fuel consumption for this timestep
            double massChange = mDot * dt;
            _simulationState.Masses[entityIndex] = Math.Max((float)(_simulationState.Masses[entityIndex] - massChange), (float)engine.DryMass);

            // Update fuel
            _simulationState.Fuels[entityIndex] = Math.Max(0, _simulationState.Fuels[entityIndex] - massChange);
        }

        /// <summary>
        /// Creates a physics body for a vessel piece and adds it to the simulation state.
        /// </summary>
        public Guid CreatePieceBody(Piece piece, Vector3 position, Quaternion orientation)
        {
            return _simulationState.AddEntity(piece, position, orientation);
        }

        /// <summary>
        /// High-frequency physics update with sub-stepping for stability.
        /// Applies environmental forces and integrates motion. Caller is responsible for clearing forces
        /// and applying controlled forces (like thrust) before calling Update.
        /// </summary>
        public void Update(double deltaTime)
        {
            _lastUpdateTime = deltaTime;
            double dt = Math.Min(deltaTime, TimeStepLimit);

            double subDt = dt / SubStepsPerFrame;
            for (int i = 0; i < SubStepsPerFrame; i++)
            {
                // Forces are now cleared by the caller before applying new frame-specific forces.
                ApplyGravity();
                ApplyAtmosphericDrag();
                IntegrateMotion(subDt);
                _currentStep++;
            }
        }

        /// <summary>
        /// Clear all accumulated forces. Should be called by the simulation loop at the start of each frame.
        /// </summary>
        public void ClearForces()
        {
            for (int i = 0; i < _simulationState.EntityCount; i++)
            {
                _simulationState.Forces[i] = Vector3.Zero;
                _simulationState.Torques[i] = Vector3.Zero;
            }
        }

        /// <summary>
        /// Apply gravitational forces to all bodies
        /// </summary>
        private void ApplyGravity()
        {
            for (int i = 0; i < _simulationState.EntityCount; i++)
            {
                _simulationState.Forces[i] += _gravity * _simulationState.Masses[i];
            }
        }

        /// <summary>
        /// Apply atmospheric drag forces based on altitude and velocity
        /// Implements: FD = 0.5 * ρ * CD * A * v²
        /// </summary>
        private void ApplyAtmosphericDrag()
        {
            for (int i = 0; i < _simulationState.EntityCount; i++)
            {
                // Simplified altitude calculation (Y-component)
                double altitude = _simulationState.Positions[i].Y;
                if (altitude < 0) altitude = 0;

                // Skip drag if outside sensible atmosphere
                if (altitude > 80000) continue;

                double density = CalculateAtmosphericDensity(altitude);
                float speed = _simulationState.Velocities[i].Length();

                if (speed > 0.01f)
                {
                    Vector3 dragDirection = -Vector3.Normalize(_simulationState.Velocities[i]);
                    float dragMagnitude = 0.5f * (float)density * speed * speed * _simulationState.DragCoefficients[i] * _simulationState.CrossSectionalAreas[i];

                    _simulationState.Forces[i] += dragDirection * dragMagnitude;
                }
            }
        }

        /// <summary>
        /// Integrate motion using Verlet integration for stability
        /// </summary>
        private void IntegrateMotion(double dt)
        {
            float fdt = (float)dt;

            for (int i = 0; i < _simulationState.EntityCount; i++)
            {
                // Verlet integration for position
                Vector3 acceleration = _simulationState.Forces[i] * _simulationState.InverseMasses[i];
                _simulationState.Positions[i] += _simulationState.Velocities[i] * fdt + 0.5f * acceleration * fdt * fdt;

                // Update velocity
                _simulationState.Velocities[i] += acceleration * fdt;

                // Simplified angular motion
                Vector3 angular_acceleration = _simulationState.Torques[i] * _simulationState.InverseMasses[i]; // Simplified
                _simulationState.AngularVelocities[i] += angular_acceleration * fdt;

                // Update orientation from angular velocity
                if (_simulationState.AngularVelocities[i].LengthSquared() > 0.001f)
                {
                    var delta_rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(_simulationState.AngularVelocities[i]), _simulationState.AngularVelocities[i].Length() * fdt);
                    _simulationState.Orientations[i] = Quaternion.Normalize(_simulationState.Orientations[i] * delta_rotation);
                }
            }
        }

        /// <summary>
        /// Calculate atmospheric density using exponential model
        /// </summary>
        private double CalculateAtmosphericDensity(double altitude)
        {
            const double seaLevelDensity = 1.225; // kg/m³
            const double scaleHeight = 8400; // m

            return seaLevelDensity * Math.Exp(-altitude / scaleHeight);
        }











        /// <summary>
        /// Performance metrics
        /// </summary>
        public PhysicsMetrics GetMetrics()
        {
            return new PhysicsMetrics
            {
                ActiveBodies = _simulationState.EntityCount,
                CurrentStep = _currentStep,
                LastUpdateTime = _lastUpdateTime,
                SubStepsPerFrame = SubStepsPerFrame
            };
        }

        public void Dispose()
        {
            Console.WriteLine($"Physics engine disposed - processed {_currentStep} steps");
        }
    }



    /// <summary>
    /// Engine data for thrust calculations following Tsiolkovsky equation
    /// </summary>
    public struct PartEngine
    {
        public double Thrust;      // Newtons
        public double Isp;         // Specific impulse (seconds)
        public double DryMass;     // Engine dry mass (kg)
        public double Gimbal;      // Gimbal range (degrees)
    }

    /// <summary>
    /// Performance metrics for monitoring physics engine
    /// </summary>
    public struct PhysicsMetrics
    {
        public int ActiveBodies;
        public int CurrentStep;
        public double LastUpdateTime;
        public int SubStepsPerFrame;
    }
}