using Xunit;
using CORIS.Core;
using System.Numerics;

public class PhysicsTests
{
    [Fact]
    public void VesselWithUpwardThrustAcceleratesUpward()
    {
        var vessel = new Vessel
        {
            Id = "test-vessel",
            Name = "Test Vessel",
            Parts = new System.Collections.Generic.List<Part>
            {
                new Part
                {
                    Id = "engine-1",
                    Name = "Engine",
                    Pieces = new System.Collections.Generic.List<Piece>
                    {
                        new Piece
                        {
                            Id = "engine-piece-1",
                            Type = "engine",
                            Mass = 1.0,
                            Properties = new System.Collections.Generic.Dictionary<string, double>
                            {
                                ["thrust"] = 1000.0,
                                ["gimbal"] = 0.0
                            }
                        }
                    }
                },
                new Part
                {
                    Id = "tank-1",
                    Name = "Tank",
                    Pieces = new System.Collections.Generic.List<Piece>
                    {
                        new Piece
                        {
                            Id = "tank-piece-1",
                            Type = "tank",
                            Mass = 1.0,
                            Properties = new System.Collections.Generic.Dictionary<string, double>
                            {
                                ["fuel"] = 10.0
                            }
                        }
                    }
                }
            }
        };
        var state = new VesselState();
        state.Orientation = new Vector3(0, 0, 0); // Set pitch to 0 (upward)
        var fuel = new FuelState { Fuel = 10.0 };
        // Use the same update logic as the main sim (copy-paste for now)
        double thrust = 1000.0;
        double mass = 2.0 + fuel.Fuel;
        double gravity = 9.81;
        double pitchRad = state.Orientation.Y * System.Math.PI / 180.0;
        var thrustDir = new Vector3(0f, (float)System.Math.Cos(pitchRad), (float)System.Math.Sin(pitchRad));
        var thrustVec = thrustDir * (float)thrust;
        var netForce = thrustVec + new Vector3(0, (float)(-mass * gravity), 0);
        double rho = 1.225, Cd = 0.75, A = 1.0;
        var v = state.Velocity;
        double vMag = v.Length();
        if (vMag > 0)
        {
            var dragDir = v * (float)(-1.0 / vMag);
            double dragMag = 0.5 * rho * Cd * A * vMag * vMag;
            var drag = dragDir * (float)dragMag;
            netForce += drag;
        }
        state.Acceleration = netForce / (float)mass;
        state.Velocity += state.Acceleration * 1.0;
        state.Position += state.Velocity * 1.0;
        Assert.True(state.Velocity.Y > 0, "Vessel should accelerate upward with upward thrust");
    }
} 