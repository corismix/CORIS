using System.Numerics;
using System.Runtime.InteropServices;

namespace CORIS.Core.Data
{
    // Architecture-driven, cache-friendly structs (SoA managed elsewhere)
    // Only stateful, frequently-modified values live here – config lives in separate static tables.

    /// <summary>
    /// Per-piece dynamic state used every physics tick. Designed for pin-blittable marshalling to Jolt.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PieceState
    {
        // Position is handled in the parent local-physics context; therefore we store only velocity & mass here.
        public Vector3 Velocity;          // m/s, world-space of the local physics island
        public Vector3 AngularVelocity;   // rad/s
        public float   Mass;              // current mass (kg)
        public float   MassPrev;          // mass at previous tick (kg) – needed for Δv integration
        public Vector3 Forward;           // normalised forward vector (unit)
    }

    /// <summary>
    /// Runtime state for a Part (collection of pieces sharing resources, e.g. fuel tank).
    /// </summary>
    public struct PartState
    {
        public int     FirstPieceIndex;   // index into global Piece SoA
        public ushort  PieceCount;
        public ushort  ResourceTableIndex; // pointer into shared resource database (fuel, power, etc.)
    }

    /// <summary>
    /// High-level vessel aggregate; owns a local physics origin and list of parts.
    /// </summary>
    public struct VesselState
    {
        public long   VesselId;
        public int    FirstPartIndex;
        public int    PartCount;
        public Vector3d LocalOrigin;      // double-precision origin for close-physics context (m)
        public Quaternion Orientation;    // world orientation relative to universe root
    }
}