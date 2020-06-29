namespace Ldartools.Common.Services
{
    public interface IDevice
    {
        string Address { get; }
        string Name { get; }
        bool IsConnected { get; }
        int SignalStrength { get; set; }
    }
}