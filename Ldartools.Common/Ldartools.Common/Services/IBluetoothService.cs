using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ldartools.Common.IO;

namespace Ldartools.Common.Services
{
    public delegate void DeviceDiscoveredHandler(IEnumerable<IDevice> devices);
    public delegate void DeviceRemovedHandler(IEnumerable<IDevice> devices);

    public delegate void SignalStrengthUpdatedHandler(string name, int strength);

    public class StreamContainer
    {
        public IInputStream InputStream { get; set; }
        public IOutputStream OutputStream { get; set; }
        public object Socket { get; set; }
    }

    public interface IBluetoothService
    {
        string Address { get; }
        event DeviceDiscoveredHandler DeviceDiscovered;
        event EventHandler DeviceDiscoveryComplete;
        event DeviceRemovedHandler DeviceRemoved;
        event SignalStrengthUpdatedHandler SignalStrengthUpdated;

        void Enable();
        void Disable();

        void StartDiscovery();
        StreamContainer Connect(IDevice device);
        void StopDiscovery();
        void Disconnect(IDevice device);
        StreamContainer Reconnect(IDevice device);
        bool IsSearching { get; }
        Task<string> Unpair(IDevice device);
        Task<string> Pair(IDevice device);
        Dictionary<string, IDevice> DiscoveredDevices { get; }
    }
}