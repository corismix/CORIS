using System;

namespace CORIS.Core.Simulation
{
    /// <summary>
    /// Basic planet atmosphere model (Earth-like for now). Provides density as function of altitude.
    /// Can be replaced with table-based or continuous ISA later.
    /// </summary>
    public static class AtmosphereModel
    {
        private const float SeaLevelDensity = 1.225f; // kg/m^3 at 0 m
        private const float ScaleHeight    = 8500f;   // m

        /// <summary>
        /// Returns atmosphere density (kg/m^3) at given altitude in metres.
        /// </summary>
        public static float Density(float altitudeMeters)
        {
            if (altitudeMeters < 0) altitudeMeters = 0;
            if (altitudeMeters > 150_000) return 0f; // space
            return SeaLevelDensity * MathF.Exp(-altitudeMeters / ScaleHeight);
        }
    }
}