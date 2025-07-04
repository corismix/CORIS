using System;
using System.Collections.Concurrent;

namespace CORIS.Core.Simulation
{
    public interface ICommand { void Execute(); }

    /// <summary>
    /// Thread-safe command buffer processed on main sim thread each tick.
    /// </summary>
    public sealed class CommandBuffer
    {
        private readonly ConcurrentQueue<ICommand> _queue = new();

        public void Enqueue(ICommand cmd) => _queue.Enqueue(cmd);

        public void Drain()
        {
            while (_queue.TryDequeue(out var cmd))
            {
                try { cmd.Execute(); }
                catch (Exception ex) { Console.WriteLine($"Command error: {ex.Message}"); }
            }
        }
    }
}