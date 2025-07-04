using System;
using CORIS.Core.Data;
using System.Numerics;

namespace CORIS.Core.Simulation
{
    /// <summary>
    /// Integrates orbits in inertial frame using simple Newtonian gravity to central body.
    /// For now single central body mu constant (Earth). Future: patched conics.
    /// </summary>
    public sealed class OrbitalIntegrator
    {
        private const double MuEarth = 3.986004418e14; // m^3/s^2

        private readonly SoABuffers _buffers;
        public OrbitalIntegrator(SoABuffers buffers) => _buffers = buffers;

        public void Step(double dt)
        {
            for (int i = 0; i < _buffers.VesselStates.Length; i++)
            {
                ref var vs = ref _buffers.VesselStates[i];
                if (vs.VesselId == 0) continue; // empty slot

                Vector3d r = vs.LocalOrigin; // assume origin relative to planet centre
                double rMag = r.Length();
                if (rMag < 1.0) continue;
                Vector3d acc = -MuEarth / (rMag * rMag * rMag) * r;
                vs.Velocity += acc * dt;
                vs.LocalOrigin += vs.Velocity * dt;
            }
        }
    }

    // simple double precision vector struct
    public struct Vector3d
    {
        public double X, Y, Z;
        public Vector3d(double x, double y, double z){X=x;Y=y;Z=z;}
        public static Vector3d operator +(Vector3d a, Vector3d b)=> new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static Vector3d operator -(Vector3d a, Vector3d b)=> new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static Vector3d operator *(Vector3d a, double s)=> new(a.X*s,a.Y*s,a.Z*s);
        public static Vector3d operator *(double s, Vector3d a)=> new(a.X*s,a.Y*s,a.Z*s);
        public double Length()=>Math.Sqrt(X*X+Y*Y+Z*Z);
    }
}