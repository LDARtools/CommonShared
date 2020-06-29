using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Ldartools.Common.Extensions.String;
using Ldartools.Common.Services;
using Newtonsoft.Json;

namespace Ldartools.Common.Devices.Services
{
    public class PhxTcpPayload
    {
        public bool Ignited { get; set; }
        public int Ppm { get; set; }
        public int Battery { get; set; }
        public int Hydrogen { get; set; }
    }

    public class PhxTcpDeviceService : IPhxDeviceService, IDisposable
    {
        private TcpClient _client;
        private StreamReader _streamReader;
        private StreamWriter _streamWriter;
        private readonly string _ipAddress;
        private Timer _pollingTimer;
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>();

        private readonly ReadingUpdatedEventArgs _readingUpdatedEventArgs = new ReadingUpdatedEventArgs(0.0, "nothing");

        public string Application { get; set; }

        public string User { get; set; }

        public string Site { get; set; }

        public PhxTcpDeviceService(string ipAddress, string serial)
        {
            _ipAddress = ipAddress;
            SerialNumber = serial;
        }

        #region Implementation of INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName]string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Implementation of IDeviceService

        public string SerialNumber { get; }
        public event ConnectedHandler Connected;
        public event DisconnectedHandler Disconnected;
#pragma warning disable 67
        public event ReconnectingHandler Reconnecting;
        public event ReconnectSuccessHandler ReconnectSuccess;
        public event EventHandler StatusChanged;
#pragma warning restore 67

        public string Status { get; private set; }

        public bool IsConnected => _client?.Connected ?? false;
        public DeviceType DeviceType => DeviceType.Tcp;

        public void Connect(string application = "", string user = "", string site = "")
        {
            if(IsConnected)
                return;
            _client = new TcpClient();
            _client.Connect(_ipAddress, 2142);
            var stream = _client.GetStream();
            _streamReader = new StreamReader(stream);
            _streamWriter = new StreamWriter(stream);
            Status = "Connected";
            Connected?.Invoke(new DeviceEventArgs(this));
        }

        public void Disconnect()
        {
            _pollingTimer?.Dispose();
            _client.Dispose();
            _client = null;
            Disconnected?.Invoke(new DeviceEventArgs(this));
        }

        public int SignalStrength { get; set; }

        #endregion

        #region Implementation of IDisposable

        public void Dispose()
        {
            _pollingTimer?.Dispose();
            _client?.Dispose();
        }

        #endregion

        #region Implementation of IAnalyzerDeviceService

        public double? LastBackgroundReading { get; set; }
        public event ReadingUpdatedHandler ReadingUpdated;

        private bool _isRunning;

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                OnPropertyChanged();
            }
        }

        public void ConfigureLogging()
        {
            throw new NotImplementedException();
        }

        public void ConfigureLogging(string loggingDirectory)
        {
            throw new NotImplementedException();
        }

        public void UseScheduler(RepeatedTaskScheduler scheduler)
        {
            //do nothing
        }

        public void StartPollingData(int interval = 333)
        {
            _pollingTimer = new Timer(Poll, null, interval, interval);
        }

        private int _lastPolledPpm;

        private void Poll(object _)
        {
            try
            {
                _streamWriter.WriteLine("poll");
                _streamWriter.Flush();
                var payloadJson = _streamReader.ReadLine();
                var result = JsonConvert.DeserializeObject<PhxTcpPayload>(payloadJson);
                _properties["IsIgnited"] = result.Ignited ? bool.TrueString : bool.FalseString;
                if (IsRunning != result.Ignited)
                {
                    IsRunning = result.Ignited;
                }

                _lastPolledPpm = result.Ppm;
                _properties[Phx42PropNames.BatteryCharge] = result.Battery.ToString();
                _properties["HPH2"] = result.Hydrogen.ToString();
                PhxDataPolled?.Invoke(this, new DataPolledEventArgs(_properties, result.Ppm));
                _readingUpdatedEventArgs.ClearAndLoadVals(result.Ppm, result.Ppm.ToString());
                ReadingUpdated?.Invoke(_readingUpdatedEventArgs);
            }
            catch(Exception)
            {
                //TODO handle disconnect
                throw;
            }
        }

        public void StopPollingData()
        {
            _pollingTimer?.Dispose();
        }

        public void Start()
        {
            try
            {
                _properties["IsIgnited"] = bool.TrueString;
                _streamWriter.WriteLine("ignite");
                _streamWriter.Flush();
                var result = _streamReader.ReadLine();
                if (result == "ignited")
                {
                    IsRunning = true;
                }
            }
            catch (Exception)
            {
                //TODO handle disconnect
                throw;
            }
        }

        public void Stop()
        {
            try
            {
                _properties["IsIgnited"] = bool.FalseString;
                _streamWriter.WriteLine("unignite");
                _streamWriter.Flush();
                var result = _streamReader.ReadLine();
                if (result == "flameout")
                {
                    IsRunning = false;
                }
            }
            catch (Exception)
            {
                //TODO handle disconnect
                throw;
            }
        }

        #endregion

        #region Implementation of IPhxDeviceService

        public event EventHandler<DataPolledEventArgs> PhxDataPolled;
        public Dictionary<string, string> PhxProperties { get; }
        public int UseAvgPerc { get; set; }
        public int LongAverageCount { get; set; }
        public int ShortAverageCount { get; set; }
        public int PollingInterval { get; }
        public void SetPpmCalibration(int indexNumber, int ppmTenths)
        {
            throw new NotImplementedException();
        }

        public void SetPpmCalibration(int indexNumber, int ppmTenths, int picoampsTenths, ushort H2Pressure, bool overwrite)
        {
            throw new NotImplementedException();
        }

        public void GenerateCalibration(float ppm)
        {
            throw new NotImplementedException();
        }

        public void WriteToBatteryFuelGaugeChip(int address, string hexData)
        {
            throw new NotImplementedException();
        }

        public string ReadFromBatteryFuelGaugeChip(int address, int count)
        {
            throw new NotImplementedException();
        }

        public float GetPpm()
        {
            return _lastPolledPpm;
        }

        public void TurnOnPump()
        {
            throw new NotImplementedException();
        }

        public void TurnOffPump()
        {
            throw new NotImplementedException();
        }

        public void WriteMemory(byte[] bytes)
        {
            throw new NotImplementedException();
        }

        public byte[] ReadMemory()
        {
            return new byte[0];
        }

        public void Start(bool useGlowPlugB)
        {
            Start();
        }

#pragma warning disable 67
        public event EventHandler<WriteFlashProgressEventArgs> WriteFlashProgress;
        public event EventHandler<ReadFlashProgressEventArgs> ReadFlashProgress;
#pragma warning restore 67

        public void WriteMemoryWithLengthAsync(byte[] bytes, Action callback, Action<Exception> errorCallback)
        {
            throw new NotImplementedException();
        }

        public void ReadMemoryAsync(Action<byte[]> callback)
        {
            throw new NotImplementedException();
        }

        public void WriteMemoryWithLength(byte[] bytes)
        {
            throw new NotImplementedException();
        }

        public bool IsPollingData { get; }
        public void WriteToPhxLog(string text)
        {
            throw new NotImplementedException();
        }

        public string GetFirmwareVersion()
        {
            throw new NotImplementedException();
        }

        public PpmCalibrationInfo GetCalibration(int index)
        {
            throw new NotImplementedException();
        }

        public void SetSolenoidBOff()
        {
            throw new NotImplementedException();
        }

        public void SetSolenoidBOn()
        {
            throw new NotImplementedException();
        }

        public void SetSolenoidAOff()
        {
            throw new NotImplementedException();
        }

        public void SetSolenoidAOn()
        {
            throw new NotImplementedException();
        }

        public void IgniteGlowPlug1(int i)
        {
            throw new NotImplementedException();
        }

        public void IgniteGlowPlug2(int i)
        {
            throw new NotImplementedException();
        }

        public DataPolledEventArgs GetStatus()
        {
            return new DataPolledEventArgs(PhxProperties, _lastPolledPpm);
        }

        public void FlashBulkErase()
        {
            throw new NotImplementedException();
        }

        public void UpdateFirmware(byte[] firmwareFileBytes)
        {
            throw new NotImplementedException();
        }

        public void LogExceptionToPhxLog(Exception exception)
        {
            throw new NotImplementedException();
        }

        public ITimeMongerService TimeMongerService { get; set; }
        public Dictionary<string, string> GetData()
        {
            throw new NotImplementedException();
        }

        public bool IsReconnecting { get; }
        public string NoPpmLabel => "-100 PPM";
        public void DeleteCalEntryRange(float ppmMin, float ppmMax, bool factoryCal)
        {
            throw new NotImplementedException();
        }

        public void SetCalibrationAtIndex(int ppm, decimal pa, int index, string date, bool factoryCal)
        {
            throw new NotImplementedException();
        }

        public PpmCalibrationInfo[] GetAllCalibrations()
        {
            throw new NotImplementedException();
        }

        public void DeleteCalibrations()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
