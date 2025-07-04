using System;
using CORIS.Core.Data;
using CORIS.Core.Physics;

namespace CORIS.Core.Simulation
{
    public sealed class SimulationEngine
    {
        private readonly SoABuffers _buffers = new();
        private readonly PhysicsWorld _physics;
        private readonly ResourceFlowSystem _resources;
        private readonly ForceIntegrator _forces;
        private readonly OrbitalIntegrator _orbit;
        private readonly CommandBuffer _cmdBuf = new();
        public readonly EventBus Events = new();

        private bool _running;
        private const float FixedDt = 1f / 60f; // 60 Hz physics

        public SimulationEngine()
        {
            _physics   = new PhysicsWorld(_buffers);
            _resources = new ResourceFlowSystem();
            _forces    = new ForceIntegrator(_buffers, _physics, _resources);
            _orbit     = new OrbitalIntegrator(_buffers);
        }

        public void Tick()
        {
            // 1. handle external commands
            _cmdBuf.Drain();

            // 2. staged systems not yet implemented
            
            // 3. integrate forces (updates PieceStates + enqueues to physics)
            _forces.Update(FixedDt);

            // 4. physics step (Jolt)
            _physics.Step(FixedDt);

            // 5. orbital integration (double precision path)
            _orbit.Step(FixedDt);

            // 6. publish tick complete
            Events.Publish(new TickEvent());
        }

        public void RunForSeconds(float seconds)
        {
            _running = true;
            int steps = (int)(seconds / FixedDt);
            for (int i = 0; i < steps && _running; i++)
                Tick();
        }

        public CommandBuffer Commands => _cmdBuf;

        public void Stop() => _running = false;
    }

    public readonly struct TickEvent { }
}