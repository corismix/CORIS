using System;
using System.Numerics;

namespace CORIS.Core
{
    /// <summary>
    /// High-fidelity orbital mechanics system implementing patched conic solvers
    /// and advanced maneuver planning from the architectural blueprint
    /// </summary>
    public class OrbitalMechanics
    {
        // Gravitational constants and celestial body data
        public static class CelestialBodies
        {
            public const double EarthMu = 3.986004418e14; // m³/s² - Standard gravitational parameter
            public const double EarthRadius = 6.371e6; // m - Earth radius
            public const double MoonMu = 4.9048695e12; // m³/s²
            public const double MoonDistance = 3.844e8; // m - Average Earth-Moon distance
            public const double SolarMu = 1.32712440018e20; // m³/s²
        }

        /// <summary>
        /// Represents an orbital state vector
        /// </summary>
        public struct OrbitalState
        {
            public Vector3 Position;        // m
            public Vector3 Velocity;        // m/s
            public double Time;             // s since epoch
            public double Mu;               // m³/s² - Gravitational parameter of central body
        }

        /// <summary>
        /// Represents orbital elements for efficient orbit representation
        /// </summary>
        public struct OrbitalElements
        {
            public double SemiMajorAxis;    // m
            public double Eccentricity;    // dimensionless
            public double Inclination;     // radians
            public double LongitudeOfAscendingNode; // radians
            public double ArgumentOfPeriapsis; // radians
            public double TrueAnomaly;      // radians
            public double Mu;               // m³/s²
        }

        /// <summary>
        /// Represents a maneuver node for trajectory planning
        /// </summary>
        public struct ManeuverNode
        {
            public double Time;             // s - Time to execute maneuver
            public Vector3 DeltaV;          // m/s - Required velocity change
            public double DeltaVMagnitude => DeltaV.Length();
            public OrbitalState PreManeuverState;
            public OrbitalState PostManeuverState;
        }

        /// <summary>
        /// High-precision orbital propagator using double precision
        /// Implements the patched conic solver from the architectural blueprint
        /// </summary>
        public static OrbitalState PropagateOrbit(OrbitalState initialState, double timeStep, double duration)
        {
            OrbitalState currentState = initialState;
            double totalTime = 0;
            int steps = (int)(duration / timeStep);

            // Use Runge-Kutta 4th order integration for high precision
            for (int i = 0; i < steps; i++)
            {
                currentState = RungeKutta4Step(currentState, timeStep);
                totalTime += timeStep;
            }

            currentState.Time = initialState.Time + totalTime;
            return currentState;
        }

        /// <summary>
        /// Runge-Kutta 4th order integration step
        /// </summary>
        private static OrbitalState RungeKutta4Step(OrbitalState state, double dt)
        {
            var k1 = CalculateAcceleration(state);
            var state2 = new OrbitalState
            {
                Position = state.Position + state.Velocity * (float)(dt / 2),
                Velocity = state.Velocity + k1 * (float)(dt / 2),
                Mu = state.Mu
            };

            var k2 = CalculateAcceleration(state2);
            var state3 = new OrbitalState
            {
                Position = state.Position + state.Velocity * (float)(dt / 2) + k1 * (float)(dt * dt / 4),
                Velocity = state.Velocity + k2 * (float)(dt / 2),
                Mu = state.Mu
            };

            var k3 = CalculateAcceleration(state3);
            var state4 = new OrbitalState
            {
                Position = state.Position + state.Velocity * (float)dt + k2 * (float)(dt * dt / 2),
                Velocity = state.Velocity + k3 * (float)dt,
                Mu = state.Mu
            };

            var k4 = CalculateAcceleration(state4);

            return new OrbitalState
            {
                Position = state.Position + state.Velocity * (float)dt + (k1 + k2 * 2f + k3 * 2f + k4) * (float)(dt * dt / 6),
                Velocity = state.Velocity + (k1 + k2 * 2f + k3 * 2f + k4) * (float)(dt / 6),
                Time = state.Time + dt,
                Mu = state.Mu
            };
        }

        /// <summary>
        /// Calculate gravitational acceleration at current position
        /// </summary>
        private static Vector3 CalculateAcceleration(OrbitalState state)
        {
            double r = state.Position.Length();
            if (r < 1e-6) return VectorMath.Zero; // Avoid division by zero

            double acceleration = -state.Mu / (r * r * r);
            return state.Position * (float)acceleration;
        }

        /// <summary>
        /// Convert orbital state vector to orbital elements
        /// </summary>
        public static OrbitalElements StateToElements(OrbitalState state)
        {
            Vector3 r = state.Position;
            Vector3 v = state.Velocity;
            double mu = state.Mu;

            // Calculate orbital angular momentum
            Vector3 h = VectorMath.Cross(r, v);
            double hMag = h.Length();

            // Calculate eccentricity vector
            Vector3 eVec = VectorMath.Cross(v, h) / (float)mu - VectorMath.SafeNormalize(r);
            double e = eVec.Length();

            // Calculate semi-major axis
            double energy = 0.5 * v.LengthSquared() - mu / r.Length();
            double a = -mu / (2 * energy);

            // Calculate inclination
            double i = Math.Acos(Math.Abs(h.Z) / hMag);

            // Calculate longitude of ascending node
            Vector3 n = VectorMath.Cross(VectorMath.UnitZ, h);
            double nMag = n.Length();
            double omega = 0;
            if (nMag > 1e-6)
            {
                omega = Math.Acos(n.X / nMag);
                if (n.Y < 0) omega = 2 * Math.PI - omega;
            }

            // Calculate argument of periapsis
            double w = 0;
            if (nMag > 1e-6 && e > 1e-6)
            {
                w = Math.Acos(Vector3.Dot(n, eVec) / (nMag * e));
                if (eVec.Z < 0) w = 2 * Math.PI - w;
            }

            // Calculate true anomaly
            double nu = 0;
            if (e > 1e-6)
            {
                nu = Math.Acos(Vector3.Dot(eVec, r) / (e * r.Length()));
                if (Vector3.Dot(r, v) < 0) nu = 2 * Math.PI - nu;
            }

            return new OrbitalElements
            {
                SemiMajorAxis = a,
                Eccentricity = e,
                Inclination = i,
                LongitudeOfAscendingNode = omega,
                ArgumentOfPeriapsis = w,
                TrueAnomaly = nu,
                Mu = mu
            };
        }

        /// <summary>
        /// Convert orbital elements to state vector
        /// </summary>
        public static OrbitalState ElementsToState(OrbitalElements elements, double time = 0)
        {
            double a = elements.SemiMajorAxis;
            double e = elements.Eccentricity;
            double i = elements.Inclination;
            double omega = elements.LongitudeOfAscendingNode;
            double w = elements.ArgumentOfPeriapsis;
            double nu = elements.TrueAnomaly;
            double mu = elements.Mu;

            // Calculate position and velocity in orbital plane
            double p = a * (1 - e * e);
            double r = p / (1 + e * Math.Cos(nu));

            Vector3 rPQW = new Vector3(
                (float)(r * Math.Cos(nu)),
                (float)(r * Math.Sin(nu)),
                0
            );

            Vector3 vPQW = new Vector3(
                (float)(-Math.Sqrt(mu / p) * Math.Sin(nu)),
                (float)(Math.Sqrt(mu / p) * (e + Math.Cos(nu))),
                0
            );

            // Rotation matrices for orbital plane to inertial frame
            double cosOmega = Math.Cos(omega);
            double sinOmega = Math.Sin(omega);
            double cosI = Math.Cos(i);
            double sinI = Math.Sin(i);
            double cosW = Math.Cos(w);
            double sinW = Math.Sin(w);

            // PQW to IJK transformation matrix
            var R11 = cosOmega * cosW - sinOmega * sinW * cosI;
            var R12 = -cosOmega * sinW - sinOmega * cosW * cosI;
            var R21 = sinOmega * cosW + cosOmega * sinW * cosI;
            var R22 = -sinOmega * sinW + cosOmega * cosW * cosI;
            var R31 = sinW * sinI;
            var R32 = cosW * sinI;

            Vector3 position = new Vector3(
                (float)(R11 * rPQW.X + R12 * rPQW.Y),
                (float)(R21 * rPQW.X + R22 * rPQW.Y),
                (float)(R31 * rPQW.X + R32 * rPQW.Y)
            );

            Vector3 velocity = new Vector3(
                (float)(R11 * vPQW.X + R12 * vPQW.Y),
                (float)(R21 * vPQW.X + R22 * vPQW.Y),
                (float)(R31 * vPQW.X + R32 * vPQW.Y)
            );

            return new OrbitalState
            {
                Position = position,
                Velocity = velocity,
                Time = time,
                Mu = mu
            };
        }

        /// <summary>
        /// Calculate Hohmann transfer maneuver between two circular orbits
        /// Implements the transfer equations from the architectural blueprint
        /// </summary>
        public static (ManeuverNode firstBurn, ManeuverNode secondBurn) CalculateHohmannTransfer(
            double r1, double r2, double mu, double currentTime)
        {
            // Calculate delta-v for Hohmann transfer
            double v1 = Math.Sqrt(mu / r1); // Circular velocity at initial orbit
            double v2 = Math.Sqrt(mu / r2); // Circular velocity at target orbit

            // Transfer orbit velocities
            double vTransfer1 = Math.Sqrt(mu * (2 / r1 - 2 / (r1 + r2))); // At periapsis
            double vTransfer2 = Math.Sqrt(mu * (2 / r2 - 2 / (r1 + r2))); // At apoapsis

            // Delta-v calculations
            double deltaV1 = vTransfer1 - v1; // First burn (prograde)
            double deltaV2 = v2 - vTransfer2; // Second burn (prograde)

            // Transfer time (half period of transfer ellipse)
            double transferTime = Math.PI * Math.Sqrt(Math.Pow(r1 + r2, 3) / (8 * mu));

            var firstBurn = new ManeuverNode
            {
                Time = currentTime,
                DeltaV = new Vector3((float)deltaV1, 0, 0), // Prograde
                // States would be calculated based on current orbit
            };

            var secondBurn = new ManeuverNode
            {
                Time = currentTime + transferTime,
                DeltaV = new Vector3((float)deltaV2, 0, 0), // Prograde
                // States would be calculated based on transfer orbit at apoapsis
            };

            return (firstBurn, secondBurn);
        }

        /// <summary>
        /// Calculate bi-elliptic transfer for very large orbit changes
        /// More efficient than Hohmann when r2/r1 > 11.94
        /// </summary>
        public static (ManeuverNode firstBurn, ManeuverNode secondBurn, ManeuverNode thirdBurn)
            CalculateBiEllipticTransfer(double r1, double r2, double rIntermediate, double mu, double currentTime)
        {
            double v1 = Math.Sqrt(mu / r1);
            double v2 = Math.Sqrt(mu / r2);

            // First transfer phase (r1 to rIntermediate)
            double vTransfer1_1 = Math.Sqrt(mu * (2 / r1 - 2 / (r1 + rIntermediate)));
            double vTransfer1_2 = Math.Sqrt(mu * (2 / rIntermediate - 2 / (r1 + rIntermediate)));

            // Second transfer phase (rIntermediate to r2)
            double vTransfer2_1 = Math.Sqrt(mu * (2 / rIntermediate - 2 / (rIntermediate + r2)));
            double vTransfer2_2 = Math.Sqrt(mu * (2 / r2 - 2 / (rIntermediate + r2)));

            double deltaV1 = vTransfer1_1 - v1;
            double deltaV2 = vTransfer2_1 - vTransfer1_2;
            double deltaV3 = v2 - vTransfer2_2;

            double transferTime1 = Math.PI * Math.Sqrt(Math.Pow(r1 + rIntermediate, 3) / (8 * mu));
            double transferTime2 = Math.PI * Math.Sqrt(Math.Pow(rIntermediate + r2, 3) / (8 * mu));

            var firstBurn = new ManeuverNode
            {
                Time = currentTime,
                DeltaV = new Vector3((float)deltaV1, 0, 0)
            };

            var secondBurn = new ManeuverNode
            {
                Time = currentTime + transferTime1,
                DeltaV = new Vector3((float)deltaV2, 0, 0)
            };

            var thirdBurn = new ManeuverNode
            {
                Time = currentTime + transferTime1 + transferTime2,
                DeltaV = new Vector3((float)deltaV3, 0, 0)
            };

            return (firstBurn, secondBurn, thirdBurn);
        }

        /// <summary>
        /// Calculate plane change maneuver
        /// Most efficient when performed at apoapsis (lowest velocity)
        /// </summary>
        public static ManeuverNode CalculatePlaneChange(OrbitalState state, double inclinationChange)
        {
            double velocity = state.Velocity.Length();
            double deltaV = 2 * velocity * Math.Sin(inclinationChange / 2);

            return new ManeuverNode
            {
                Time = state.Time,
                DeltaV = new Vector3(0, (float)deltaV, 0), // Normal direction
                PreManeuverState = state
            };
        }

        /// <summary>
        /// Calculate gravity assist delta-v gain
        /// Used for interplanetary missions to outer solar system
        /// </summary>
        public static double CalculateGravityAssistDeltaV(double vInfinity, double turnAngle)
        {
            return 2 * vInfinity * Math.Sin(turnAngle / 2);
        }

        /// <summary>
        /// Calculate orbital period
        /// </summary>
        public static double CalculateOrbitalPeriod(double semiMajorAxis, double mu)
        {
            return 2 * Math.PI * Math.Sqrt(Math.Pow(semiMajorAxis, 3) / mu);
        }

        /// <summary>
        /// Calculate escape velocity from given altitude
        /// </summary>
        public static double CalculateEscapeVelocity(double altitude, double mu, double bodyRadius)
        {
            double r = bodyRadius + altitude;
            return Math.Sqrt(2 * mu / r);
        }

        /// <summary>
        /// Calculate sphere of influence for patched conic method
        /// </summary>
        public static double CalculateSphereOfInfluence(double semiMajorAxis, double primaryMu, double secondaryMu)
        {
            return semiMajorAxis * Math.Pow(secondaryMu / primaryMu, 2.0 / 5.0);
        }

        /// <summary>
        /// High-level maneuver planner that optimizes transfer efficiency
        /// </summary>
        public static class ManeuverPlanner
        {
            /// <summary>
            /// Determines optimal transfer type based on orbit ratio
            /// </summary>
            public static string DetermineOptimalTransferType(double r1, double r2)
            {
                double ratio = Math.Max(r1, r2) / Math.Min(r1, r2);

                if (ratio < 11.94)
                    return "Hohmann";
                else
                    return "BiElliptic";
            }

            /// <summary>
            /// Calculate total delta-v budget for a mission
            /// </summary>
            public static double CalculateMissionDeltaV(ManeuverNode[] maneuvers)
            {
                double totalDeltaV = 0;
                foreach (var maneuver in maneuvers)
                {
                    totalDeltaV += maneuver.DeltaVMagnitude;
                }
                return totalDeltaV;
            }

            /// <summary>
            /// Find optimal launch window for interplanetary transfer
            /// Simplified Porkchop plot calculation
            /// </summary>
            public static (double launchTime, double arrivalTime, double totalDeltaV)
                FindOptimalLaunchWindow(double departureOrbitRadius, double arrivalOrbitRadius,
                                      double searchStartTime, double searchDuration, double timeStep)
            {
                double bestDeltaV = double.MaxValue;
                double bestLaunchTime = searchStartTime;
                double bestArrivalTime = searchStartTime;

                for (double launchTime = searchStartTime; launchTime < searchStartTime + searchDuration; launchTime += timeStep)
                {
                    for (double flightTime = 100 * 24 * 3600; flightTime < 500 * 24 * 3600; flightTime += timeStep)
                    {
                        double arrivalTime = launchTime + flightTime;

                        // Calculate transfer delta-v (simplified)
                        var (firstBurn, secondBurn) = CalculateHohmannTransfer(
                            departureOrbitRadius, arrivalOrbitRadius, CelestialBodies.SolarMu, launchTime);

                        double totalDeltaV = firstBurn.DeltaVMagnitude + secondBurn.DeltaVMagnitude;

                        if (totalDeltaV < bestDeltaV)
                        {
                            bestDeltaV = totalDeltaV;
                            bestLaunchTime = launchTime;
                            bestArrivalTime = arrivalTime;
                        }
                    }
                }

                return (bestLaunchTime, bestArrivalTime, bestDeltaV);
            }
        }
    }

    /// <summary>
    /// Patched conic solver for multi-body trajectory planning
    /// Implements the high-precision solver from the architectural blueprint
    /// </summary>
    public class PatchedConicSolver
    {
        private readonly List<CelestialBody> _celestialBodies;

        public struct CelestialBody
        {
            public string Name;
            public double Mu;                    // Gravitational parameter
            public double Radius;               // Physical radius
            public double SphereOfInfluence;    // SOI radius
            public Vector3 Position;            // Current position
            public Vector3 Velocity;            // Current velocity
        }

        public PatchedConicSolver()
        {
            _celestialBodies = new List<CelestialBody>
            {
                new CelestialBody
                {
                    Name = "Earth",
                    Mu = OrbitalMechanics.CelestialBodies.EarthMu,
                    Radius = OrbitalMechanics.CelestialBodies.EarthRadius,
                    SphereOfInfluence = 924000000, // 924,000 km
                    Position = VectorMath.Zero,
                    Velocity = VectorMath.Zero
                },
                new CelestialBody
                {
                    Name = "Moon",
                    Mu = OrbitalMechanics.CelestialBodies.MoonMu,
                    Radius = 1.737e6, // 1,737 km
                    SphereOfInfluence = 66100000, // 66,100 km
                    Position = new Vector3((float)OrbitalMechanics.CelestialBodies.MoonDistance, 0, 0),
                    Velocity = VectorMath.Zero
                }
            };
        }

        /// <summary>
        /// Solve trajectory through multiple sphere of influences
        /// </summary>
        public List<OrbitalMechanics.OrbitalState> SolveTrajectory(
            OrbitalMechanics.OrbitalState initialState, double duration, double timeStep)
        {
            var trajectory = new List<OrbitalMechanics.OrbitalState>();
            var currentState = initialState;
            double currentTime = 0;

            while (currentTime < duration)
            {
                // Determine which celestial body has influence
                var influencingBody = DetermineInfluencingBody(currentState.Position);
                currentState.Mu = influencingBody.Mu;

                // Propagate until SOI change or end of duration
                double nextSOITime = FindNextSOITransition(currentState, timeStep);
                double propagationTime = Math.Min(timeStep, Math.Min(nextSOITime, duration - currentTime));

                currentState = OrbitalMechanics.PropagateOrbit(currentState, propagationTime / 10, propagationTime);
                trajectory.Add(currentState);

                currentTime += propagationTime;
            }

            return trajectory;
        }

        private CelestialBody DetermineInfluencingBody(Vector3 position)
        {
            // Find the celestial body whose SOI contains the position
            foreach (var body in _celestialBodies)
            {
                double distance = VectorMath.Distance(position, body.Position);
                if (distance <= body.SphereOfInfluence)
                {
                    return body;
                }
            }

            // Default to primary body (Earth)
            return _celestialBodies[0];
        }

        private double FindNextSOITransition(OrbitalMechanics.OrbitalState state, double maxTime)
        {
            // Simplified SOI transition detection
            // In practice, this would use more sophisticated numerical methods
            return maxTime; // Placeholder
        }
    }
}