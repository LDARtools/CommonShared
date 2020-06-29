using System;
using System.ComponentModel;

namespace Ldartools.Common.Services
{
    public delegate void ReadingUpdatedHandler(ReadingUpdatedEventArgs args);

    public delegate void ConnectedHandler(DeviceEventArgs args);

    public delegate void DisconnectedHandler(DeviceEventArgs args);

    public delegate void ReconnectingHandler(DeviceEventArgs args);

    public delegate void ReconnectSuccessHandler(DeviceEventArgs args);

    public enum DeviceType
    {
        Unknown,
        Phx21,
        Phx42,
        Tva,
        Tcp
    }

    public interface IDeviceService : INotifyPropertyChanged
    {
        string SerialNumber { get; }
        event ConnectedHandler Connected;
        event DisconnectedHandler Disconnected;
        event ReconnectingHandler Reconnecting;
        event ReconnectSuccessHandler ReconnectSuccess;
        event EventHandler StatusChanged;
        string Status { get; }
        bool IsConnected { get; }
        DeviceType DeviceType { get; }
        void Connect(string application = "", string user = "", string site = "");
        void Disconnect();
        int SignalStrength { get; set; }
        string Application { get; set; }
        string User { get; set; }
        string Site { get; set; }


    }

    public interface IAnalyzerDeviceService : IDeviceService
    {
        double? LastBackgroundReading { get; set; }
        event ReadingUpdatedHandler ReadingUpdated;
        bool IsRunning { get; }
        void ConfigureLogging();
        void ConfigureLogging(string loggingDirectory);
        void UseScheduler(RepeatedTaskScheduler scheduler);
        void StartPollingData(int interval = 333);
        void StopPollingData();
        void Start();
        void Stop();
    }

    public class DeviceEventArgs
    {
        public IDeviceService Device { get; set; }

        public DeviceEventArgs(IDeviceService device)
        {
            Device = device;
        }
    }

    public class ReadingUpdatedEventArgs
    {
        public double Reading { get; set; }
        public string ReadingString { get; set; }

        public ReadingUpdatedEventArgs(double reading, string readingString)
        {
            Reading = reading;
            ReadingString = readingString;
        }

        public void ClearAndLoadVals(double reading, string readingString)
        {
            Reading = 0.0;
            ReadingString = "0";

            Reading = reading;
            ReadingString = readingString;
        }
    }
}