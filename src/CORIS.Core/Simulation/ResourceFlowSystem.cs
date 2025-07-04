using System.Collections.Generic;
using CORIS.Core.Data;

namespace CORIS.Core.Simulation
{
    public sealed class ResourceFlowSystem
    {
        private readonly Dictionary<int/*partIdx*/, List<ResourceTank>> _tanksByPart = new();

        public void RegisterTank(int partIndex, ResourceTank tank)
        {
            if (!_tanksByPart.TryGetValue(partIndex, out var list))
            {
                list = new List<ResourceTank>();
                _tanksByPart[partIndex] = list;
            }
            list.Add(tank);
        }

        public bool Consume(int partIndex, ResourceType type, float amount)
        {
            if (!_tanksByPart.TryGetValue(partIndex, out var list)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                ref var tank = ref list[i];
                if (tank.Type != type) continue;
                if (tank.Amount >= amount)
                {
                    tank.Amount -= amount;
                    list[i] = tank;
                    return true;
                }
            }
            return false;
        }
    }
}