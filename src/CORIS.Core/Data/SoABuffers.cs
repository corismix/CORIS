using System;
using System.Runtime.InteropServices;

namespace CORIS.Core.Data
{
    /// <summary>
    /// Generic grow-able Structure-of-Arrays buffer owning three parallel managed arrays for Piece/Part/Vessel.
    /// By design, elements are never re-ordered after insertion; indices remain stable.
    /// Removal marks slots as free and they are reused by subsequent Add().
    /// No locks – caller must guarantee single-thread write or add a job system.
    /// </summary>
    public sealed class SoABuffers
    {
        public PieceState[]  PieceStates  { get; private set; } = new PieceState[1024];
        public PartState[]   PartStates   { get; private set; } = new PartState[256];
        public VesselState[] VesselStates { get; private set; } = new VesselState[64];

        private BitArray _pieceFree = new(1024);
        private BitArray _partFree  = new(256);
        private BitArray _vesselFree = new(64);

        public int AddPiece(in PieceState value)
        {
            int idx = FindFree(_pieceFree, PieceStates.Length, ref PieceStates);
            PieceStates[idx] = value;
            return idx;
        }
        public int AddPart(in PartState value)
        {
            int idx = FindFree(_partFree, PartStates.Length, ref PartStates);
            PartStates[idx] = value;
            return idx;
        }
        public int AddVessel(in VesselState value)
        {
            int idx = FindFree(_vesselFree, VesselStates.Length, ref VesselStates);
            VesselStates[idx] = value;
            return idx;
        }
        public ref PieceState GetPiece(int index)  => ref PieceStates[index];
        public ref PartState  GetPart(int index)   => ref PartStates[index];
        public ref VesselState GetVessel(int index)=> ref VesselStates[index];

        private static int FindFree(BitArray free, int length, ref PieceState[] grow)
        {
            for (int i = 0; i < length; i++)
                if (!free.Get(i)) { free.Set(i, true); return i; }
            // grow
            int oldLen = length;
            Array.Resize(ref grow, length * 2);
            free.Length = grow.Length;
            free.Set(oldLen, true);
            return oldLen;
        }
        private static int FindFree(BitArray free, int length, ref PartState[] grow)
        {
            for (int i = 0; i < length; i++)
                if (!free.Get(i)) { free.Set(i, true); return i; }
            int oldLen = length;
            Array.Resize(ref grow, length * 2);
            free.Length = grow.Length;
            free.Set(oldLen, true);
            return oldLen;
        }
        private static int FindFree(BitArray free, int length, ref VesselState[] grow)
        {
            for (int i = 0; i < length; i++)
                if (!free.Get(i)) { free.Set(i, true); return i; }
            int oldLen = length;
            Array.Resize(ref grow, length * 2);
            free.Length = grow.Length;
            free.Set(oldLen, true);
            return oldLen;
        }
    }
}