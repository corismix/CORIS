namespace CORIS.Core.Data
{
    public enum ResourceType : byte
    {
        Fuel,
        Oxidizer,
        MonoProp,
        ElectricCharge
    }

    public struct ResourceTank
    {
        public ResourceType Type;
        public float Capacity;
        public float Amount;
    }
}