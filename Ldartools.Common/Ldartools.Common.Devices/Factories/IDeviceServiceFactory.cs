using Ldartools.Common.IO;
using Ldartools.Common.Services;

namespace Ldartools.Common.Devices.Factories
{
    public interface IDeviceServiceFactory
    {
        bool CanCreateServiceFrom(string deviceName);
        IDeviceService Create(IBluetoothService bluetoothService, IDevice device, IFileManager fileManager);
    }
}