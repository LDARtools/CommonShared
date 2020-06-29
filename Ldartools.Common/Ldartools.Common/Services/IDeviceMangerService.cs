using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ldartools.Common.Services
{
    public delegate void DevicesFoundHandler(DevicesFoundEventArgs args);

    public delegate void DevicesRemovedHandler(DevicesFoundEventArgs args);

    public delegate void DeviceConnectedHandler(IDeviceService analyzer);

    public delegate void DeviceDisconnectedHandler(IDeviceService analyzer);

    public delegate void SignalStrengthUpdateHandler(string name, int strength);

    public interface IDeviceManagerService
    {
        event DevicesFoundHandler DevicesFound;
        event DevicesRemovedHandler DevicesRemoved;
        event DeviceConnectedHandler DeviceConnected;
        event DeviceDisconnectedHandler DeviceDisconnected;
        event EventHandler DevicesFoundComplete;
        event SignalStrengthUpdateHandler SignalStrengthUpdate;

        bool IsSearching { get; }

        IDeviceService[] ConnectedDevices { get;  }

        IDeviceService[] Devices { get; }

        List<string> NonPhxDevices { get; }
        
        void FindDevices();
        void StopFindingDevices();
        void Enable();
        void Disable();

        void DisconnectAll();
    }

    public class DevicesFoundEventArgs
    {
        public IDeviceService[] Devices { get; set; }

        public DevicesFoundEventArgs(IDeviceService[] devices)
        {
            Devices = devices;
        }
    }
}