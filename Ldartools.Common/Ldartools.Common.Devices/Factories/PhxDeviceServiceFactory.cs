using System;
using Ldartools.Common.Devices.Services;
using Ldartools.Common.IO;
using Ldartools.Common.Services;

namespace Ldartools.Common.Devices.Factories
{
    public class PhxDeviceServiceFactory : IDeviceServiceFactory
    {
        private readonly Func<IBluetoothService, IDevice, IFileManager, IPhxDeviceService> phxDeviceServiceFunc;

        public PhxDeviceServiceFactory(Func<IBluetoothService, IDevice, IFileManager, IPhxDeviceService> phxDeviceServiceFunc)
        {
            this.phxDeviceServiceFunc = phxDeviceServiceFunc;
        }

        public bool CanCreateServiceFrom(string deviceName)
        {
            return deviceName.ToLower().Contains("phx");
        }

        public IDeviceService Create(IBluetoothService bluetoothService, IDevice device, IFileManager fileManager)
        {
            return phxDeviceServiceFunc(bluetoothService, device, fileManager);
        }
    }
}