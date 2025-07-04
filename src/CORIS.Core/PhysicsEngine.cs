using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace CORIS.Core
{
    /// <summary>
    /// High-performance physics engine implementing data-oriented design
    /// Demonstrates the "Piece-Part-Vessel" hierarchy from the architectural blueprint
    /// Note: This is a simplified implementation for demonstration purposes
    /// </summary>
    public class PhysicsEngine : IDisposable
    {
        // Data-oriented storage for physics entities - Structure of Arrays (SoA) pattern
        private readonly List<PiecePhysicsState> _pieceStates;
        private readonly Dictionary<int, int> _pieceIdToIndex;
        
        // Performance tracking and configuration
        private double _lastUpdateTime;
        private int _currentStep = 0;
        private readonly Vector3 _gravity = new Vector3(0, -9.81f, 0);
        
        // High-performance physics settings optimized for rocketry simulation
        private const int SubStepsPerFrame = 4; // For stable high-speed physics
        private const double TimeStepLimit = 1.0 / 120.0; // 120Hz max timestep
        
        public PhysicsEngine()
        {
            Console.WriteLine("=== Initializing CORIS High-Performance Physics Engine ===");
            
            _pieceStates = new List<PiecePhysicsState>();
            _pieceIdToIndex = new Dictionary<int, int>();
            
            Console.WriteLine("Physics engine initialized successfully");
            Console.WriteLine($"Max timestep: {TimeStepLimit * 1000:F1}ms");
            Console.WriteLine($"Sub-steps per frame: {SubStepsPerFrame}");
        }

        /// <summary>
        /// Integrates thrust forces following the Tsiolkovsky rocket equation
        /// This is the core performance-critical function from the architectural blueprint
        /// Implements: mDot = Thrust / (Isp * g0), dv = Isp * g0 * ln(m0/m1)
        /// </summary>
        public void IntegrateThrust(in PartEngine engine, ref PiecePhysicsState state, double dt)
        {
            const double g0 = 9.80665; // Standard gravity (m/s²)

            if (state.Fuel <= 0 || engine.Thrust <= 0) return;

            // Calculate mass flow rate: mDot = Thrust / (Isp * g0)
            double mDot = engine.Thrust / (engine.Isp * g0);
            
            // Update mass (ensuring we don't go below dry mass)
            double prevMass = state.Mass;
            state.Mass = Math.Max(state.Mass - mDot * dt, engine.DryMass);
            
            // Calculate delta-v for this timestep using Tsiolkovsky equation
            // This is numerical integration as recommended in the blueprint
            if (prevMass > state.Mass)
            {
                double dv = engine.Isp * g0 * Math.Log(prevMass / state.Mass);
                
                // Apply thrust in the forward direction of the piece
                Vector3 thrustDirection = VectorMath.Transform(VectorMath.UnitZ, state.Orientation);
                Vector3 deltaVelocity = thrustDirection * (float)dv;
                
                // Apply velocity change directly to the physics state
                state.LinearVelocity += deltaVelocity;
                
                // Update fuel consumption
                state.Fuel -= mDot * dt;
                if (state.Fuel < 0) state.Fuel = 0;
            }
        }

        /// <summary>
        /// Creates a physics body for a vessel piece with optimized settings
        /// </summary>
        public int CreatePieceBody(Piece piece, Vector3 position, Quaternion orientation)
        {
            // Create physics state for this piece using data-oriented design
            var physicsState = new PiecePhysicsState
            {
                PieceID = piece.Id,
                Mass = piece.Mass,
                Fuel = GetInitialFuel(piece),
                Position = position,
                Orientation = orientation,
                LinearVelocity = VectorMath.Zero,
                AngularVelocity = VectorMath.Zero,
                Force = VectorMath.Zero,
                Torque = VectorMath.Zero,
                InverseMass = 1.0f / (float)piece.Mass,
                DragCoefficient = GetDragCoefficient(piece),
                CrossSectionalArea = GetCrossSectionalArea(piece)
            };

            // Add to data-oriented storage using Structure of Arrays pattern
            int bodyId = _pieceStates.Count;
            _pieceStates.Add(physicsState);
            _pieceIdToIndex[piece.Id.GetHashCode()] = bodyId;

            Console.WriteLine($"Created physics body for {piece.Type} piece: mass={piece.Mass:F1}kg");
            return bodyId;
        }

        /// <summary>
        /// High-frequency physics update with sub-stepping for stability
        /// Implements 120Hz sub-stepping for atmospheric flight as recommended
        /// </summary>
        public void Update(double deltaTime)
        {
            _currentStep++;
            _lastUpdateTime = deltaTime;

            // Clamp timestep to prevent instability
            deltaTime = Math.Min(deltaTime, TimeStepLimit);

            // Sub-stepping for high-frequency updates during atmospheric flight
            double subDeltaTime = deltaTime / SubStepsPerFrame;

            for (int step = 0; step < SubStepsPerFrame; step++)
            {
                // Clear forces for this substep
                ClearForces();
                
                // Apply environmental forces
                ApplyGravity();
                ApplyAtmosphericDrag(subDeltaTime);
                
                // Integrate physics using Verlet integration for stability
                IntegrateMotion(subDeltaTime);
            }
        }

        /// <summary>
        /// Clear all accumulated forces - called each substep
        /// </summary>
        private void ClearForces()
        {
            // Data-oriented loop for maximum cache efficiency
            for (int i = 0; i < _pieceStates.Count; i++)
            {
                var state = _pieceStates[i];
                state.Force = VectorMath.Zero;
                state.Torque = VectorMath.Zero;
                _pieceStates[i] = state;
            }
        }

        /// <summary>
        /// Apply gravitational forces to all bodies
        /// </summary>
        private void ApplyGravity()
        {
            for (int i = 0; i < _pieceStates.Count; i++)
            {
                var state = _pieceStates[i];
                state.Force += _gravity * (float)state.Mass;
                _pieceStates[i] = state;
            }
        }

        /// <summary>
        /// Apply atmospheric drag forces based on altitude and velocity
        /// Implements: FD = 0.5 * ρ * CD * A * v²
        /// </summary>
        private void ApplyAtmosphericDrag(double deltaTime)
        {
            for (int i = 0; i < _pieceStates.Count; i++)
            {
                var state = _pieceStates[i];
                
                if (state.Position.Y > 70000) continue; // Above atmosphere
                
                // Calculate atmospheric density based on altitude
                double altitude = state.Position.Y;
                double rho = CalculateAtmosphericDensity(altitude);
                
                if (rho <= 0) continue;

                Vector3 velocity = state.LinearVelocity;
                float velocityMagnitude = velocity.Length();
                
                if (velocityMagnitude > 0.1f)
                {
                    Vector3 dragDirection = velocity / -velocityMagnitude;
                    float dragMagnitude = (float)(0.5 * rho * state.DragCoefficient * state.CrossSectionalArea * velocityMagnitude * velocityMagnitude);
                    Vector3 dragForce = dragDirection * dragMagnitude;
                    
                    state.Force += dragForce;
                    _pieceStates[i] = state;
                }
            }
        }

        /// <summary>
        /// Integrate motion using Verlet integration for stability
        /// </summary>
        private void IntegrateMotion(double dt)
        {
            float fdt = (float)dt;
            
            // Data-oriented integration loop for maximum performance
            for (int i = 0; i < _pieceStates.Count; i++)
            {
                var state = _pieceStates[i];
                
                // Linear motion integration
                Vector3 acceleration = state.Force * state.InverseMass;
                state.LinearVelocity += acceleration * fdt;
                state.Position += state.LinearVelocity * fdt;
                
                // Angular motion integration (simplified)
                // In a full implementation, this would use proper rotational dynamics
                Vector3 angularAcceleration = state.Torque * state.InverseMass; // Simplified
                state.AngularVelocity += angularAcceleration * fdt;
                
                // Apply angular velocity to orientation
                if (state.AngularVelocity.Length() > 0.001f)
                {
                    Vector3 axis = VectorMath.SafeNormalize(state.AngularVelocity);
                    float angle = state.AngularVelocity.Length() * fdt;
                    Quaternion deltaRotation = Quaternion.CreateFromAxisAngle(axis, angle);
                    state.Orientation = Quaternion.Normalize(state.Orientation * deltaRotation);
                }
                
                _pieceStates[i] = state;
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
        /// Get initial fuel amount from piece properties
        /// </summary>
        private double GetInitialFuel(Piece piece)
        {
            if (piece.Type == "tank" && piece.Properties?.TryGetValue("fuel", out var fuel) == true)
            {
                return fuel;
            }
            return 0.0;
        }

        /// <summary>
        /// Get drag coefficient based on piece type
        /// </summary>
        private float GetDragCoefficient(Piece piece)
        {
            return piece.Type switch
            {
                "engine" => 0.8f,
                "tank" => 0.6f,
                "cockpit" => 0.4f,
                "wing" => 0.1f,
                _ => 0.7f
            };
        }

        /// <summary>
        /// Get cross-sectional area based on piece type
        /// </summary>
        private float GetCrossSectionalArea(Piece piece)
        {
            return piece.Type switch
            {
                "engine" => 0.8f,
                "tank" => 1.2f,
                "cockpit" => 1.0f,
                "wing" => 2.0f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Get physics state for a piece by ID
        /// </summary>
        public PiecePhysicsState? GetPieceState(int pieceId)
        {
            if (_pieceIdToIndex.TryGetValue(pieceId, out int index) && index < _pieceStates.Count)
            {
                return _pieceStates[index];
            }
            return null;
        }

        /// <summary>
        /// Get all physics states for debugging and visualization
        /// </summary>
        public IReadOnlyList<PiecePhysicsState> GetAllStates() => _pieceStates.AsReadOnly();

        /// <summary>
        /// Performance metrics
        /// </summary>
        public PhysicsMetrics GetMetrics()
        {
            return new PhysicsMetrics
            {
                ActiveBodies = _pieceStates.Count,
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
    /// Data-oriented physics state for individual pieces
    /// Optimized for cache-friendly access patterns using Structure of Arrays
    /// </summary>
    public struct PiecePhysicsState
    {
        public string PieceID;
        public double Mass;
        public double Fuel;
        public Vector3 Position;
        public Quaternion Orientation;
        public Vector3 LinearVelocity;
        public Vector3 AngularVelocity;
        public Vector3 Force;
        public Vector3 Torque;
        public float InverseMass;
        public float DragCoefficient;
        public float CrossSectionalArea;
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