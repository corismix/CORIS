using Xunit;
using CORIS.Core;
using System.Numerics;

public class OrbitalIntegratorTests
{
    /// <summary>
    /// Verifies that the RK4 propagator maintains a circular orbit within 1 mm over 6 hours.
    /// </summary>
    [Fact]
    public void CircularOrbit_DriftLessThanOneMillimetre()
    {
        const double altitude = 400_000; // 400 km LEO
        const double r0 = OrbitalMechanics.CelestialBodies.EarthRadius + altitude;
        const double mu = OrbitalMechanics.CelestialBodies.EarthMu;
        // Circular orbital speed v = sqrt(mu / r)
        double v0 = System.Math.Sqrt(mu / r0);

        var initialState = new OrbitalMechanics.OrbitalState
        {
            Position = new Vector3((float)r0, 0, 0),
            Velocity = new Vector3(0, (float)v0, 0),
            Time = 0,
            Mu = mu
        };

        double duration = 6 * 3600; // 6 hours
        double dt = 10;             // 10-second step

        var finalState = OrbitalMechanics.PropagateOrbit(initialState, dt, duration);

        double rFinal = finalState.Position.Length();
        double drift = System.Math.Abs(rFinal - r0);

        Assert.True(drift < 0.001, $"Radial drift {drift} m exceeds 1 mm");
    }
}