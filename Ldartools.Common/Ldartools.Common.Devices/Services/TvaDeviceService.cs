using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ldartools.Common.IO;
using Ldartools.Common.Services;

namespace Ldartools.Common.Devices.Services
{
    public class TvaDeviceService : ITvaDeviceService
    {
        private readonly IBluetoothService _bluetoothService;
        private readonly IDevice _device;
        private readonly IFileManager _fileManager;
        private string _status;
        private ReadingUpdatedEventArgs _ruea = new ReadingUpdatedEventArgs(0.0, "nothing");

        
        public Tva Tva { get; protected set; }
#pragma warning disable 67
        public event PropertyChangedEventHandler PropertyChanged;
        public event DisconnectedHandler Disconnected;
        public event ReconnectingHandler Reconnecting;
        public event ReconnectSuccessHandler ReconnectSuccess;
        public event EventHandler<WriteFlashProgressEventArgs> WriteFlashProgress;
        public event EventHandler<ReadFlashProgressEventArgs> ReadFlashProgress;
#pragma warning restore 67
        public string SerialNumber { get; set; }
        public event ConnectedHandler Connected;
        
        public event EventHandler StatusChanged;
        public event EventHandler<TvaErrorEventArgs> TvaError;

        public string Application
        {
            get { return Tva?.Application ?? string.Empty; }
            set { if (Tva != null) Tva.Application = value; }
        }

        public string User
        {
            get { return Tva?.User ?? string.Empty; }
            set { if (Tva != null) Tva.User = value; }
        }

        public string Site
        {
            get { return Tva?.Site ?? string.Empty; }
            set { if (Tva != null) Tva.Site = value; }
        }

        public string Status
        {
            get => _status;
            protected set
            {
                _status = value;
                OnStatusChanged();
            }
        }

        public bool IsConnected { get; protected set; }
        public DeviceType DeviceType => DeviceType.Tva;

        public TvaDeviceService(IBluetoothService bluetoothService, IDevice device, IFileManager fileManager)
        {
            _bluetoothService = bluetoothService;
            _device = device;
            _fileManager = fileManager;
            SerialNumber = _device.Name;
        }

        public void Connect(string application = "", string user = "", string site = "")
        {
            if (IsConnected)
                return;

            var streams = _bluetoothService.Connect(_device);

            Tva = new Tva(streams,
                _fileManager,
                _device.Name, TimeMongerService, application, user, site);

            Tva.Error += Tva_OnError;
            Tva.DataPolled += Tva_DataPolled;
            Tva.TvaError += Tva_TvaError;

            IsConnected = true;
            Status = "Connected";
            OnConnected(new DeviceEventArgs(this));
        }

        private void Tva_TvaError(object sender, TvaErrorEventArgs e)
        {
            TvaError?.Invoke(sender, e);
        }

        private void Tva_DataPolled(object sender, DataPolledEventArgs e)
        {
            try
            {
                OnPhxDataPolled(e);
            }
            catch (Exception ex)
            {
                Tva?.WriteExceptionToLog(ex);
            }
        }

        private void Tva_OnError(object sender, ErrorEventArgs e)
        {
            //PhxError?.Invoke(sender, errorEventArgs);
        }

        public void Disconnect()
        {
            List<Exception> exceptions = new List<Exception>();

            try
            {
                Tva.ShutdownNow = true;

                Tva.Error -= Tva_OnError;
                Tva.DataPolled -= Tva_DataPolled;
                Tva.TvaError -= Tva_TvaError;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                Tva?.WriteExceptionToLog(ex);
            }

            try
            {
                _bluetoothService.Disconnect(_device);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                Tva?.WriteExceptionToLog(ex);
            }

            Tva = null;

            IsConnected = false;
            Status = "Disconnected";

            OnDisconnected(new DeviceEventArgs(this));

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }

        public int SignalStrength { get; set; }
        public double? LastBackgroundReading { get; set; }
        public event ReadingUpdatedHandler ReadingUpdated;
        public bool IsRunning { get; set; }

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
        }

        public void StartPollingData(int interval = 333)
        {
        }

        public void StopPollingData()
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public void Start(bool useGlowPlugB)
        {
            throw new NotImplementedException();
        }

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

        public bool IsPollingData => true;
        public void WriteToPhxLog(string text)
        {
            Tva.WriteToLog(text);
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
            throw new NotImplementedException();
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
            Tva.WriteExceptionToLog(exception);
        }

        public ITimeMongerService TimeMongerService { get; set; }
        public Dictionary<string, string> GetData()
        {
            throw new NotImplementedException();
        }

        public bool IsReconnecting => false;
        public string NoPpmLabel => "N/A";
        public void DeleteCalEntryRange(float ppmMin, float ppmMax, bool factoryCal)
        {
            
        }

        public void SetCalibrationAtIndex(int ppm, decimal pa, int index, string date, bool factoryCal)
        {
            
        }

        public PpmCalibrationInfo[] GetAllCalibrations()
        {
            return null;
        }

        public void DeleteCalibrations()
        {
            
        }

        protected virtual void OnConnected(DeviceEventArgs args)
        {
            Connected?.Invoke(args);
        }

        protected virtual void OnStatusChanged()
        {
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnPhxDataPolled(DataPolledEventArgs e)
        {
            if (e.Ppm < 0)
            {
                IsRunning = false;
            }
            else
            {
                IsRunning = true;
            }

            PhxDataPolled?.Invoke(this, e);

            _ruea.ClearAndLoadVals(e.Ppm, e.Ppm.ToString());
            InvokeReadingUpdated(_ruea);
        }

        protected virtual void InvokeReadingUpdated(ReadingUpdatedEventArgs ruea2)
        {
            ReadingUpdated?.Invoke(ruea2);
        }

        protected void OnDisconnected(DeviceEventArgs args)
        {
            Disconnected?.Invoke(args);
        }

        public void PressSelectButton()
        {
            Tva.PressSelectButton();
        }

        public void PressNextButton()
        {
            Tva.PressNextButton();
        }
    }
}
