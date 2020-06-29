using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ldartools.Common.Devices.Services;
using Ldartools.Common.IO;
using Ldartools.Common.Services;

namespace Ldartools.Common.Devices
{
    public class TcpDeviceManagerService : IDeviceManagerService
    {
        private readonly IFileManager _fileManager;
        private CancellationTokenSource _discoveryCancellationTokenSource;
        private const int TcpStreamPort = 2142;
        private const int UdpDiscoveryPort = 4221;
        private const string MulticastAddress = "238.217.217.21";

        public TcpDeviceManagerService(IFileManager fileManager)
        {
            _fileManager = fileManager;
        }

        #region Implementation of IDeviceManagerService
#pragma warning disable 67
        public event DevicesFoundHandler DevicesFound;
        public event DevicesRemovedHandler DevicesRemoved;
        public event DeviceConnectedHandler DeviceConnected;
        public event DeviceDisconnectedHandler DeviceDisconnected;
        public event EventHandler DevicesFoundComplete;
        public event SignalStrengthUpdateHandler SignalStrengthUpdate;
#pragma warning restore 67
        public bool IsSearching { get; private set; }
        public IDeviceService[] ConnectedDevices
        {
            get
            {
                lock (_connectedDevices)
                {
                    return _connectedDevices.ToArray();
                }
            }
        }

        public IDeviceService[] Devices => _devices.ToArray();
        public List<string> NonPhxDevices { get; } = new List<string>();

        private readonly List<IDeviceService> _devices = new List<IDeviceService>();

        private readonly List<IDeviceService> _connectedDevices = new List<IDeviceService>();

        public void FindDevices()
        {
            IsSearching = true;
            _devices.Clear();
            _discoveryCancellationTokenSource = new CancellationTokenSource();
            var token = _discoveryCancellationTokenSource.Token;

            var startTime = DateTime.UtcNow;

            Task.Run(() =>
            {
                //find devices in folder with ipaddresses
                var files = _fileManager.GetFiles(Path.Combine(_fileManager.DataDirectory, "LDARtools", "emulators"));

                foreach (var file in files)
                {
                    var ipText = _fileManager.ReadAllText(file);
                    FileInfo f = new FileInfo(file);
                    var fileName  = f.Name.Replace(f.Extension, "");

                    if (IPAddress.TryParse(ipText, out var ip))
                    {
                        var deviceService = new PhxTcpDeviceService(ipText, fileName);
                        deviceService.Connected += DeviceOnConnected;
                        deviceService.Disconnected += DeviceOnDisconnected;
                        _devices.Add(deviceService);
                        try
                        {
                            DevicesFound?.Invoke(new DevicesFoundEventArgs(Devices));
                        }
#pragma warning disable 168
                        catch (Exception e)
#pragma warning restore 168
                        {
                            Debugger.Break();
                        }
                    }
                }

                //find using udp
                try
                {
                    using (var udpClient = new UdpClient())
                    {
                        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, UdpDiscoveryPort));
                        udpClient.JoinMulticastGroup(IPAddress.Parse(MulticastAddress));
                        var messageSource = new IPEndPoint(0, 0);
                        while (true)
                        {
                            if (token.IsCancellationRequested) break;
                            var encodedMessage = udpClient.Receive(ref messageSource);
                            var message = Encoding.UTF8.GetString(encodedMessage);
                            if (message.StartsWith("phx@"))
                            {
                                var computerName = message.Substring(4, message.Length - 4);
                                var deviceName = $"phxSim@{computerName}({messageSource.Address})";
                                if (_devices.All(d => d.SerialNumber != deviceName))
                                {
                                    var deviceService =
                                        new PhxTcpDeviceService(messageSource.Address.ToString(), deviceName);
                                    deviceService.Connected += DeviceOnConnected;
                                    deviceService.Disconnected += DeviceOnDisconnected;
                                    _devices.Add(deviceService);
                                    try
                                    {
                                        DevicesFound?.Invoke(new DevicesFoundEventArgs(Devices));
                                    }
#pragma warning disable CS0168 // Variable is declared but never used
                                    catch (Exception e)
#pragma warning restore CS0168 // Variable is declared but never used
                                    {
                                        Debugger.Break();
                                    }
                                }
                            }

                            if (DateTime.UtcNow - startTime > TimeSpan.FromSeconds(30)) break;
                        }

                        DevicesFoundComplete?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch
                {
                    //do nothing
                }
            }, token);
        }

        public void StopFindingDevices()
        {
            _discoveryCancellationTokenSource?.Cancel();
            _discoveryCancellationTokenSource = null;
            IsSearching = false;
        }

        public void Enable()
        {
            //do nothing
        }

        public void Disable()
        {
            //do nothing
        }

        public void DisconnectAll()
        {
            foreach (var device in _devices)
            {
                device.Disconnect();
            }
        }

        #endregion

        private void DeviceOnConnected(DeviceEventArgs args)
        {
            lock (_connectedDevices)
            {
                _connectedDevices.Add(args.Device);
            }

            DeviceConnected?.Invoke(args.Device);
        }

        private void DeviceOnDisconnected(DeviceEventArgs args)
        {
            lock (_connectedDevices)
            {
                _connectedDevices.Remove(args.Device);
            }
            DeviceDisconnected?.Invoke(args.Device);
        }
    }
}
