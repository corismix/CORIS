using System;
using System.Collections.Generic;
using System.Numerics;
using CORIS.Core.Data;
using CORIS.Core.Physics;

namespace CORIS.Core.Simulation
{
    public sealed class ForceIntegrator
    {
        private const float G0 = 9.80665f;

        private readonly SoABuffers _buffers;
        private readonly PhysicsWorld _physics;

        // basic engine/drag config per piece
        private readonly Dictionary<int, EngineParams> _engines = new();
        private readonly Dictionary<int, DragParams> _dragShapes = new();

        public ForceIntegrator(SoABuffers buffers, PhysicsWorld physics)
        {
            _buffers = buffers;
            _physics = physics;
        }

        public void RegisterEngine(int pieceIndex, float thrustN, float isp, float dryMassKg)
        {
            _engines[pieceIndex] = new EngineParams(thrustN, isp, dryMassKg);
        }
        public void RegisterDragShape(int pieceIndex, float areaM2, float cd)
        {
            _dragShapes[pieceIndex] = new DragParams(areaM2, cd);
        }

        public void Update(float dt)
        {
            // Thrust integration
            foreach (var kv in _engines)
            {
                int idx = kv.Key;
                EngineParams ep = kv.Value;
                ref var ps = ref _buffers.PieceStates[idx];
                if (ps.Mass <= 0) continue;

                float mDot = ep.Thrust / (ep.Isp * G0);
                float massPrev = ps.Mass;
                ps.Mass = Math.Max(ps.Mass - mDot * dt, ep.DryMass);

                float dv = ep.Isp * G0 * MathF.Log(massPrev / ps.Mass);
                Vector3 deltav = ps.Forward * dv;
                ps.Velocity += deltav;

                // translate to force: F = m * dv/dt (approx current mass)
                Vector3 thrustForce = ps.Forward * (ep.Thrust);
                _physics.AddForce(idx, thrustForce);

                ps.MassPrev = massPrev;
            }

            // Drag for all pieces with velocity
            foreach (var kv in _dragShapes)
            {
                int idx = kv.Key;
                var drag = kv.Value;
                ref var ps = ref _buffers.PieceStates[idx];
                if (ps.Velocity.LengthSquared() < 1e-4f) continue;

                float rho = SampleAtmosphereDensity(idx); // placeholder
                if (rho <= 0) continue;

                Vector3 v = ps.Velocity;
                float speed = v.Length();
                Vector3 vDir = v / speed;
                float dragMag = 0.5f * rho * speed * speed * drag.Cd * drag.Area;
                Vector3 dragForce = -vDir * dragMag;
                _physics.AddForce(idx, dragForce);
            }
        }

        // Very naive exponential atmosphere based on vessel altitude (not implemented)
        private float SampleAtmosphereDensity(int pieceIndex)
        {
            return 0f; // TODO: integrate with altitude from VesselState
        }

        private readonly record struct EngineParams(float Thrust, float Isp, float DryMass);
        private readonly record struct DragParams(float Area, float Cd);
    }
}