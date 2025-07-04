using System;
using System.Collections.Generic;
using System.Numerics;
using JoltPhysicsSharp;
using CORIS.Core.Data;

namespace CORIS.Core.Physics
{
    public sealed class PhysicsWorld : IDisposable
    {
        private readonly SoABuffers _buffers;

        private readonly TempAllocator _tempAllocator;
        private readonly JobSystem _jobSystem;
        private readonly PhysicsSystem _physics;
        private readonly BodyInterface _bodyIf;

        private readonly Dictionary<int/*pieceIdx*/, Body> _bodyByPiece = new();
        private readonly Dictionary<BodyID, int> _pieceByBody = new();

        // Pending per-piece forces (world space) to apply next tick
        private readonly Dictionary<int, Vector3> _forceAccum = new();

        public PhysicsWorld(SoABuffers buffers)
        {
            _buffers = buffers;

            // Jolt global init guarded by runtime once
            Jolt.Initialize();

            _tempAllocator = new TempAllocator(8 * 1024 * 1024);
            _jobSystem     = new JobSystem(JobSystem.MaxJobs, JobSystem.MaxBarriers);

            // very simple broad-phase filters for now
            var bpl = new BroadPhaseLayerInterfaceImp();
            var objVsBpl = new ObjectVsBroadPhaseLayerFilterImp();
            var objPair  = new ObjectLayerPairFilterImp();

            _physics = new PhysicsSystem();
            _physics.Init(maxBodies: 262_144, 0, 1024, 1024, bpl, objVsBpl, objPair);
            _bodyIf = _physics.GetBodyInterface();
        }

        #region Body creation
        public void SyncPiecesToBodies()
        {
            // iterate all PieceStates, ensure body exists.
            for (int i = 0; i < _buffers.PieceStates.Length; ++i)
            {
                if (!_bodyByPiece.ContainsKey(i))
                {
                    ref readonly var ps = ref _buffers.PieceStates[i];
                    if (ps.Mass <= 0) continue; // empty slot

                    CreateBodyForPiece(i, ps);
                }
            }
        }
        private void CreateBodyForPiece(int idx, in PieceState ps)
        {
            // very temporary: sphere 0.5m radius
            var shape = new SphereShapeSettings(0.5f).Create().Get();
            var bodySettings = new BodyCreationSettings(
                shape,
                new RVec3(0,0,0), // position handled elsewhere
                Quaternion.Identity,
                MotionType.Dynamic,
                (ObjectLayer)1);
            bodySettings.MassPropertiesOverride.Mass = ps.Mass;
            bodySettings.OverrideMassProperties = EOverrideMassProperties.CalculateInertia;

            Body body = _bodyIf.CreateBody(bodySettings);
            _bodyIf.AddBody(body.ID, Activation.Activate);

            _bodyByPiece[idx] = body;
            _pieceByBody[body.ID] = idx;
        }
        #endregion

        public void AddForce(int pieceIndex, Vector3 force)
        {
            if (_forceAccum.TryGetValue(pieceIndex, out var f))
                _forceAccum[pieceIndex] = f + force;
            else
                _forceAccum[pieceIndex] = force;
        }

        public void Step(float dt)
        {
            // Apply queued forces
            foreach (var kv in _forceAccum)
            {
                if (_bodyByPiece.TryGetValue(kv.Key, out var body))
                {
                    _bodyIf.AddForce(body.ID, new Vec3(kv.Value.X, kv.Value.Y, kv.Value.Z));
                }
            }
            _forceAccum.Clear();

            // run simulation
            _physics.Update(dt, 
                collisionSteps: 1, integrationSubSteps: 1, 
                tempAllocator: _tempAllocator, jobSystem: _jobSystem);

            // sync velocities back
            foreach (var kv in _bodyByPiece)
            {
                int idx = kv.Key;
                Body body = kv.Value;
                ref var ps = ref _buffers.PieceStates[idx];
                Vec3 v = _bodyIf.GetLinearVelocity(body.ID);
                ps.Velocity = new Vector3(v.X, v.Y, v.Z);
            }
        }

        public void Dispose()
        {
            foreach (var body in _bodyByPiece.Values)
                _bodyIf.RemoveBody(body.ID);
            _bodyByPiece.Clear();
            _pieceByBody.Clear();
            _physics?.Dispose();
            _jobSystem?.Dispose();
            _tempAllocator?.Dispose();
        }

        // Minimal filter stubs
        private class BroadPhaseLayerInterfaceImp : BroadPhaseLayerInterface
        {
            public override int GetNumBroadPhaseLayers() => 1;
            public override BroadPhaseLayer GetBroadPhaseLayer(ObjectLayer layer) => new(0);
            public override ObjectLayer GetObjectLayer(BroadPhaseLayer layer) => (ObjectLayer)1;
        }
        private class ObjectVsBroadPhaseLayerFilterImp : ObjectVsBroadPhaseLayerFilter
        {
            public override bool ShouldCollide(ObjectLayer objLayer, BroadPhaseLayer broadPhaseLayer) => true;
        }
        private class ObjectLayerPairFilterImp : ObjectLayerPairFilter
        {
            public override bool ShouldCollide(ObjectLayer layer1, ObjectLayer layer2) => true;
        }
    }
}