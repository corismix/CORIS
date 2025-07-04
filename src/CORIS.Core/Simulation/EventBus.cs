using System;
using System.Collections.Generic;

namespace CORIS.Core.Simulation
{
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public void Subscribe<T>(Action<T> handler)
        {
            var t = typeof(T);
            if (!_handlers.TryGetValue(t, out var list))
            {
                list = new List<Delegate>();
                _handlers[t] = list;
            }
            list.Add(handler);
        }

        public void Publish<T>(T evt)
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
            {
                foreach (var d in list)
                    ((Action<T>)d)?.Invoke(evt);
            }
        }
    }
}