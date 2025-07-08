using System;
using System.Collections.Generic;
using System.Numerics;

namespace CORIS.Core.SoA
{
    /// <summary>
    /// Manages the state of all simulated entities using a Structure-of-Arrays (SoA) layout.
    /// This approach improves cache performance by keeping related data for a single attribute (e.g., position)
    /// contiguous in memory.
    /// </summary>
    public class SimulationState
    {
        // GUID-to-index mapping for fast lookups.
        private readonly Dictionary<Guid, int> _entityIndexMap = new();
        // Index-to-GUID mapping for reverse lookups.
        private readonly List<Guid> _indexEntityMap = new();

        // --- Data Arrays (The "Structures of Arrays") ---
        public List<Vector3> Positions { get; private set; } = new();
        public List<Vector3> Velocities { get; private set; } = new();
        public List<Vector3> Accelerations { get; private set; } = new();
        public List<Quaternion> Orientations { get; private set; } = new();
        public List<Vector3> AngularVelocities { get; private set; } = new();
        public List<float> Masses { get; private set; } = new();
        public List<Vector3> Forces { get; private set; } = new();
        public List<Vector3> Torques { get; private set; } = new();
        public List<double> Fuels { get; private set; } = new();
        public List<string> PieceTypes { get; private set; } = new(); // e.g., "engine", "tank"
        public List<float> InverseMasses { get; private set; } = new();
        public List<float> DragCoefficients { get; private set; } = new();
        public List<float> CrossSectionalAreas { get; private set; } = new();


        public int EntityCount => _indexEntityMap.Count;

        /// <summary>
        /// Adds a new entity to the simulation state and returns its GUID.
        /// </summary>
        public Guid AddEntity(Piece piece, Vector3 position, Quaternion orientation)
        {
            var guid = Guid.NewGuid();
            var index = EntityCount;

            _entityIndexMap[guid] = index;
            _indexEntityMap.Add(guid);

            // Add data to arrays
            Positions.Add(position);
            Velocities.Add(Vector3.Zero); // Initial velocity is zero
            Accelerations.Add(Vector3.Zero);
            Orientations.Add(orientation);
            AngularVelocities.Add(Vector3.Zero);
            Masses.Add((float)piece.Mass);
            Forces.Add(Vector3.Zero);
            Torques.Add(Vector3.Zero);
            Fuels.Add(GetInitialFuel(piece));
            PieceTypes.Add(piece.Type);
            InverseMasses.Add(piece.Mass > 0 ? 1.0f / (float)piece.Mass : 0.0f);
            DragCoefficients.Add(GetDragCoefficient(piece));
            CrossSectionalAreas.Add(GetCrossSectionalArea(piece));

            return guid;
        }


        /// <summary>
        /// Removes an entity from the simulation state.
        /// This implementation uses a swap-and-pop to avoid shifting all subsequent elements.
        /// </summary>
        public void RemoveEntity(Guid guid)
        {
            if (!_entityIndexMap.TryGetValue(guid, out var indexToRemove))
            {
                return; // Entity not found
            }

            var lastIndex = EntityCount - 1;
            var lastGuid = _indexEntityMap[lastIndex];

            // Move the last element's data to the slot of the element being removed.
            Positions[indexToRemove] = Positions[lastIndex];
            Velocities[indexToRemove] = Velocities[lastIndex];
            Accelerations[indexToRemove] = Accelerations[lastIndex];
            Orientations[indexToRemove] = Orientations[lastIndex];
            AngularVelocities[indexToRemove] = AngularVelocities[lastIndex];
            Masses[indexToRemove] = Masses[lastIndex];
            Forces[indexToRemove] = Forces[lastIndex];
            Torques[indexToRemove] = Torques[lastIndex];
            Fuels[indexToRemove] = Fuels[lastIndex];
            PieceTypes[indexToRemove] = PieceTypes[lastIndex];
            InverseMasses[indexToRemove] = InverseMasses[lastIndex];
            DragCoefficients[indexToRemove] = DragCoefficients[lastIndex];
            CrossSectionalAreas[indexToRemove] = CrossSectionalAreas[lastIndex];

            // Update the mapping for the moved entity.
            _entityIndexMap[lastGuid] = indexToRemove;
            _indexEntityMap[indexToRemove] = lastGuid;

            // Remove the last element.
            _entityIndexMap.Remove(guid);
            _indexEntityMap.RemoveAt(lastIndex);
            Positions.RemoveAt(lastIndex);
            Velocities.RemoveAt(lastIndex);
            Accelerations.RemoveAt(lastIndex);
            Orientations.RemoveAt(lastIndex);
            AngularVelocities.RemoveAt(lastIndex);
            Masses.RemoveAt(lastIndex);
            Forces.RemoveAt(lastIndex);
            Torques.RemoveAt(lastIndex);
            Fuels.RemoveAt(lastIndex);
            PieceTypes.RemoveAt(lastIndex);
            InverseMasses.RemoveAt(lastIndex);
            DragCoefficients.RemoveAt(lastIndex);
            CrossSectionalAreas.RemoveAt(lastIndex);
        }

        /// <summary>
        /// Gets the index for a given entity GUID. Returns -1 if not found.
        /// </summary>
        public int GetEntityIndex(Guid guid)
        {
            return _entityIndexMap.TryGetValue(guid, out var index) ? index : -1;
        }

        /// <summary>
        /// Gets the GUID for a given entity index. Returns Guid.Empty if not found.
        /// </summary>
        public Guid GetGuidFromIndex(int index)
        {
            if (index >= 0 && index < _indexEntityMap.Count)
            {
                return _indexEntityMap[index];
            }
            return Guid.Empty;
        }

        // Helper to get initial fuel from a piece's properties
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

        private double GetInitialFuel(Piece piece)
        {
            if (piece.Type == "tank" && piece.Properties?.TryGetValue("fuel", out var fuel) == true)
            {
                return fuel;
            }
            return 0.0;
        }
    }
}
