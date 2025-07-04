using System.Collections.Generic;
using CORIS.Core.Data;

namespace CORIS.Core.Simulation
{
    public sealed class StagingSystem
    {
        private readonly SoABuffers _buffers;
        private readonly PhysicsWorld _physics;

        // maps vessel -> list of stages (each stage = list of part indices)
        private readonly Dictionary<long, Queue<List<int>>> _stages = new();

        public StagingSystem(SoABuffers buffers, PhysicsWorld physics)
        {
            _buffers = buffers;
            _physics = physics;
        }

        public void RegisterStage(long vesselId, List<int> partIndices)
        {
            if (!_stages.TryGetValue(vesselId, out var queue))
            {
                queue = new Queue<List<int>>();
                _stages[vesselId] = queue;
            }
            queue.Enqueue(partIndices);
        }

        public void ActivateNextStage(long vesselId)
        {
            if (!_stages.TryGetValue(vesselId, out var queue) || queue.Count == 0) return;
            var parts = queue.Dequeue();
            // For simplicity, detach parts from vessel physics (future: create debris vessel)
            foreach (var partIdx in parts)
            {
                ref var part = ref _buffers.GetPart(partIdx);
                // mark piece count 0 => disabled for now
                part.PieceCount = 0;
            }
        }
    }
}