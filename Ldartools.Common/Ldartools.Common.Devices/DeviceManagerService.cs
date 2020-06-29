using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ldartools.Common.Devices.Factories;
using Ldartools.Common.Devices.Services;
using Ldartools.Common.IO;
using Ldartools.Common.Services;

namespace Ldartools.Common.Devices
{
    public class DeviceManagerService : IDeviceManagerService
    {
        private readonly IBluetoothService _bluetoothService;
        private readonly IFileManager fileManager;
        public event DevicesFoundHandler DevicesFound;
        public event DevicesRemovedHandler DevicesRemoved;
        public event DeviceConnectedHandler DeviceConnected;
        public event DeviceDisconnectedHandler DeviceDisconnected;
        public event EventHandler DevicesFoundComplete;
        public event SignalStrengthUpdateHandler SignalStrengthUpdate;

        public bool IsSearching { get; protected set; }

        public IDeviceService[] ConnectedDevices => _connectedDevices.ToArray();
        public List<IDeviceService> _connectedDevices = new List<IDeviceService>();
        private List<IDeviceService> _devices = new List<IDeviceService>();

        public IDeviceService[] Devices => _devices.ToArray();

        public List<string> NonPhxDevices { get; } = new List<string>();

        public DeviceManagerService(IBluetoothService bluetoothService, IFileManager fileManager)
        {
            _bluetoothService = bluetoothService;

            this.fileManager = fileManager;
            bluetoothService.DeviceDiscovered += BluetoothServiceOnDeviceDiscovered;
            bluetoothService.DeviceDiscoveryComplete += BluetoothServiceDeviceDiscoveryComplete;
            bluetoothService.DeviceRemoved += BluetoothService_DeviceRemoved;
            bluetoothService.SignalStrengthUpdated += BluetoothService_SignalStrengthUpdated;
        }

        private void BluetoothService_SignalStrengthUpdated(string name, int strength)
        {
            try
            {
                var deviceService = _devices.FirstOrDefault(d => d.SerialNumber == name);

                if (deviceService != null)
                {
                    deviceService.SignalStrength = strength;
                }

                SignalStrengthUpdate?.Invoke(name, strength);
            }
            catch (Exception)
            {
            }
        }

        private void BluetoothService_DeviceRemoved(IEnumerable<IDevice> devices)
        {
            foreach (var device in devices)
            {
                var deviceService = _devices.FirstOrDefault(d => d.SerialNumber == device.Name);

                if (deviceService != null)
                {
                    _devices.Remove(deviceService);
                    DevicesRemoved?.Invoke(new DevicesFoundEventArgs(new []{deviceService}));
                }
            }
        }

        private void BluetoothServiceDeviceDiscoveryComplete(object sender, EventArgs e)
        {
            OnDevicesFoundComplete();
            IsSearching = false;
        }

        private void BluetoothServiceOnDeviceDiscovered(IEnumerable<IDevice> devices)
        {
            try
            {
                bool anychange = false;

                foreach (var device in devices)
                {
                    var existing = _devices.FirstOrDefault(a => a.SerialNumber == device.Name);

                    if (existing != null)
                    {
                        _devices.Remove(existing);
                        DevicesRemoved?.Invoke(new DevicesFoundEventArgs(new []{existing}));
                    }

                    var name = device.Name.ToLower();

                    IDeviceService phxDeviceService;

                    if (name.StartsWith("phx42"))
                    {
                        phxDeviceService = new Phx42DeviceService(_bluetoothService, device, fileManager);
                    }
                    else if (name.StartsWith("phx21"))
                    {
                        phxDeviceService = new Phx21DeviceService(_bluetoothService, device, fileManager);
                    }
                    else if (name.StartsWith("fec"))
                    {
                        phxDeviceService = new TvaDeviceService(_bluetoothService, device, fileManager);
                    }
                    else
                    {
                        if (NonPhxDevices.All(n => n != name)) NonPhxDevices.Add(name);
                        continue;
                    }

                    _devices.Add(phxDeviceService);
                    anychange = true;

                    foreach (IDeviceService deviceService in Devices)
                    {
                        deviceService.Connected -= DeviceOnConnected;
                        deviceService.Connected += DeviceOnConnected;

                        deviceService.Disconnected -= DeviceOnDisconnected;
                        deviceService.Disconnected += DeviceOnDisconnected;
                    }
                }

                if (anychange)
                {
                    OnDevicesFound(new DevicesFoundEventArgs(Devices));
                }
            }
            catch (Exception)
            {
                //ignore
            }
        }

        private void RefreshDevices()
        {
            _devices.Clear();
            NonPhxDevices.Clear();

            BluetoothServiceOnDeviceDiscovered(_bluetoothService.DiscoveredDevices?.Values ?? Enumerable.Empty<IDevice>());
        }

        private void DeviceOnDisconnected(DeviceEventArgs args)
        {
            lock (_connectedDevices)
            {
                _connectedDevices.Remove(args.Device);
            }

            OnDeviceDisconnected(args.Device);
        }

        public void FindDevices()
        {
            IsSearching = true;

            RefreshDevices();

            if (_bluetoothService.IsSearching)
            {
                return;
            }

            _devices.Clear();
            NonPhxDevices.Clear();
            Task.Run(() => _bluetoothService.StartDiscovery());
        }

        public void StopFindingDevices()
        {
            _bluetoothService.StopDiscovery();
            IsSearching = false;
        }

        public void Disable()
        {
            _bluetoothService.Disable();
        }

        public void Enable()
        {
            _bluetoothService.Enable();
        }

        private void DeviceOnConnected(DeviceEventArgs args)
        {
            lock (_connectedDevices)
            {
                _connectedDevices.Add(args.Device);
            }

            OnDeviceConnected(args.Device);
        }

        protected virtual void OnDevicesFound(DevicesFoundEventArgs args)
        {
            DevicesFound?.Invoke(args);
        }

        

        protected virtual void OnDeviceConnected(IDeviceService device)
        {
            DeviceConnected?.Invoke(device);
        }

        protected virtual void OnDeviceDisconnected(IDeviceService device)
        {
            DeviceDisconnected?.Invoke(device);
        }

        protected virtual void OnDevicesFoundComplete()
        {
            DevicesFoundComplete?.Invoke(this, EventArgs.Empty);
        }

        public void DisconnectAll()
        {
            List<Task> disconnectTasks = new List<Task>();

            foreach (var connectedDevice in ConnectedDevices.ToArray())
            {
                var t = Task.Run(() => connectedDevice.Disconnect());

                disconnectTasks.Add(t);
            }

            Task.WaitAll(disconnectTasks.ToArray());
        }
    }
}