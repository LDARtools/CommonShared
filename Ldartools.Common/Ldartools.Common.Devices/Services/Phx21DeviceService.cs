using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Ldartools.Common.IO;
using Ldartools.Common.Services;
using Ldartools.Common.Util;

#if  COMPACT_FRAMEWORK
using System.Net.Sockets;
#endif

namespace Ldartools.Common.Devices.Services
{
    public class Phx21DeviceService : IPhxDeviceService
    {
        private readonly IBluetoothService bluetoothService;
        private readonly IDevice device;
        private readonly IFileManager fileManager;
        private bool isRunning;

        private Phx21 _phx21;
        private ReadingUpdatedEventArgs ruea = new ReadingUpdatedEventArgs(0.0, "nothing");
        public string SerialNumber { get; private set; }
        public event ConnectedHandler Connected;
        public event DisconnectedHandler Disconnected;
#pragma warning disable 67
        public event ReconnectingHandler Reconnecting;
        public event ReconnectSuccessHandler ReconnectSuccess;
#pragma warning restore 67

        public event EventHandler StatusChanged;
        public event EventHandler<DataPolledEventArgs> PhxDataPolled;
        public event EventHandler<WriteFlashProgressEventArgs> WriteFlashProgress;
        public event EventHandler<ErrorEventArgs> PhxError;

        public ObservableCollection<Exception> PhxExceptions { get; protected set; }

        public ITimeMongerService TimeMongerService { get; set; }

        public string Application
        {
            get { return _phx21?.Application ?? string.Empty; }
            set { if (_phx21 != null) _phx21.Application = value; }
        }

        public string User
        {
            get { return _phx21?.User ?? string.Empty; }
            set { if (_phx21 != null) _phx21.User = value; }
        }

        public string Site
        {
            get { return _phx21?.Site ?? string.Empty; }
            set { if (_phx21 != null) _phx21.Site = value; }
        }

        public int SignalStrength
        {
            get => device.SignalStrength;
            set
            {
                if (device == null) return;
                device.SignalStrength = value;
                OnPropertyChanged();
            }
        }

        protected virtual void OnWriteFlashProgress(WriteFlashProgressEventArgs e)
        {
            WriteFlashProgress?.Invoke(this, e);
        }

        public event EventHandler<ReadFlashProgressEventArgs> ReadFlashProgress;

        protected virtual void OnReadFlashProgress(ReadFlashProgressEventArgs e)
        {
            ReadFlashProgress?.Invoke(this, e);
        }

        public bool IsPollingData { get; set; }

        public Phx21 Phx21 => _phx21;

        public string Status
        {
            get { return status; }
            protected set
            {
                status = value;

                OnStatusChanged();
            }
        }

        public Dictionary<string, string> PhxProperties { get; protected set; }

        public int UseAvgPerc
        {
            get { return _phx21.UseAvgPerc; }
            set { _phx21.UseAvgPerc = value; }
        }

        public int LongAverageCount
        {
            get { return _phx21.LongAverageCount; }
            set { _phx21.LongAverageCount = value; }
        }

        public int ShortAverageCount
        {
            get { return _phx21.ShortAverageCount; }
            set { _phx21.ShortAverageCount = value; }
        }

        public bool IsConnected
        {
            get { return isConnected; }
            private set { isConnected = value; }
        }

        public DeviceType DeviceType => DeviceType.Phx21;

        public event ReadingUpdatedHandler ReadingUpdated;

        protected virtual void InvokeReadingUpdated(ReadingUpdatedEventArgs ruea2)
        {
            ReadingUpdated?.Invoke(ruea2);
        }

        public bool IsRunning
        {
            get { return isRunning;}
            set
            {
                if (Equals(isRunning, value))
                    return;

                isRunning = value;

                OnPropertyChanged("IsRunning");
            }
        }

        public int PollingInterval => _phx21.PollingInterval;

        public void ConfigureLogging()
        {
            if (Phx21 == null)
                throw new Exception("phx21 is not connected.  Connect to the device first.");

            Phx21.ConfigureLogging();
        }

        public void ConfigureLogging(string loggingDirectory)
        {
            if (Phx21 == null)
                throw new Exception("phx21 is not connected.  Connect to the device first.");

            Phx21.ConfigureLogging(loggingDirectory);
        }

        public void UseScheduler(RepeatedTaskScheduler scheduler)
        {
            Phx21.UseScheduler(scheduler);
        }

        public void StartPollingData(int interval = 333)
        {
            if (Phx21 == null)
                throw new Exception("phx21 is not connected.  Connect to the device first.");

            Phx21.DataPolled -= Phx21OnDataPolled;
            Phx21.DataPolled += Phx21OnDataPolled;

            IsPollingData = true;

            Phx21.StartPollingData(interval);
        }

        public void StopPollingData()
        {
            if (Phx21 == null)
                throw new Exception("phx21 is not connected.  Connect to the device first.");

            Phx21.DataPolled -= Phx21OnDataPolled;

            IsPollingData = false;

            Phx21.StopPollingData();
        }

        public double? LastBackgroundReading { get; set; }

        private bool isConnected;
        private string status;

        public Phx21DeviceService(IBluetoothService bluetoothService, IDevice device, IFileManager fileManager)
        {
            this.bluetoothService = bluetoothService;
            this.device = device;
            this.fileManager = fileManager;

            SerialNumber = device.Name;

            PhxExceptions = new ObservableCollection<Exception>();
        }

        public void Connect(string application = "", string user = "", string site = "")
        {
            if (IsConnected)
                return;

            var streams = bluetoothService.Connect(device);
            
            _phx21 = new Phx21(streams.InputStream,
                streams.OutputStream,
                fileManager,
                device.Name, TimeMongerService, application, user, site);

            _phx21.Error += Phx21OnError;
            _phx21.WriteFlashProgress += Phx21OnWriteFlashProgress;
            _phx21.ReadFlashProgress += Phx21OnReadFlashProgress;
            
            IsConnected = true;
            Status = "Connected";       
            OnConnected(new DeviceEventArgs(this));
        }

        private void Phx21OnReadFlashProgress(object sender, ReadFlashProgressEventArgs readFlashProgressEventArgs)
        {
            OnReadFlashProgress(readFlashProgressEventArgs);
        }

        private void Phx21OnWriteFlashProgress(object sender, WriteFlashProgressEventArgs writeFlashProgressEventArgs)
        {
            OnWriteFlashProgress(writeFlashProgressEventArgs);
        }

        private bool reconnecting = false;

        private void Phx21OnError(object sender, ErrorEventArgs errorEventArgs)
        {
            if (reconnecting) return;
            PhxExceptions.Add(errorEventArgs.Exception);
            PhxError?.Invoke(sender, errorEventArgs);
            
            if (errorEventArgs.Exception.GetType().FullName.StartsWith("Java.IO.IOException") || errorEventArgs.Exception is Reconnect21NeededException)
            {
                try
                {
                    Disconnect();
                }
                catch (Exception)
                {
                    //ignore
                }

                //Not reconnecting for now
                //try
                //{
                //    reconnecting = true;
                //    Reconnecting?.Invoke(new DeviceEventArgs(this));
                //    phx21.Error -= Phx21OnError;

                //    try
                //    {
                //        phx21?.WriteToPhxLog("Attempting to reconnect");
                //        Disconnect();
                //        Connect();
                //        phx21?.WriteToPhxLog("Reconnect success");
                //        ReconnectSuccess?.Invoke(new DeviceEventArgs(this));
                //    }
                //    catch (Exception ex)
                //    {
                //        PhxExceptions.Add(ex);
                //        PhxError?.Invoke(this, new ErrorEventArgs(ex));
                //        OnDisconnected(new DeviceEventArgs(this));
                //    }
                //}
                //finally
                //{
                //    reconnecting = false;
                //}
            }
        }

        private void Phx21OnDataPolled(object sender, DataPolledEventArgs dataPolledEventArgs)
        {
            IsRunning = bool.Parse(dataPolledEventArgs.PhxProperties["IsIgnited"]);

            foreach (var kv in dataPolledEventArgs.PhxProperties.ToArray())
            {
                if (!PropertySelector.Phx42Translation.ContainsKey(kv.Key))
                    continue;

                dataPolledEventArgs.PhxProperties[PropertySelector.Phx42Translation[kv.Key]] = kv.Value;
            }

            try
            {
                OnPhxDataPolled(dataPolledEventArgs);
            }
            catch (Exception ex)
            {
                Phx21?.WriteExceptionToPhxLog(ex);
            }

            
        }

        public override string ToString()
        {
            return SerialNumber;
        }
        
        public void SetPpmCalibration(int indexNumber, int ppmTenths)
        {
            _phx21.SetPpmCalibration(indexNumber, ppmTenths);
        }

        public void SetPpmCalibration(int indexNumber, int ppmTenths, int picoampsTenths, ushort H2Pressure, bool overwrite)
        {
            _phx21.SetPpmCalibration(indexNumber, ppmTenths, picoampsTenths, H2Pressure, overwrite);
        }

        public void GenerateCalibration(float ppm)
        {
            _phx21.GeneratePpmCalibration((int)(ppm * 10));
        }

        Dictionary<string, string> battSettings = new Dictionary<string, string>();

        public void WriteToBatteryFuelGaugeChip(int address, string hexData)
        {
            battSettings[address.ToString()] = hexData;
        }

        public string ReadFromBatteryFuelGaugeChip(int address, int count)
        {
            return battSettings[address.ToString()];
        }

        public float GetPpm()
        {
            Phx21Status status = null;

            TryUtils.Retry(() => status = _phx21.ReadDataExtended(), 5, 100);

            if (status == null) throw new Exception("Could not get phx21 status");

            return (float)status.Ppm;
        }

        public void TurnOnPump()
        {
            _phx21.TurnOnPumpToTargetPressure(1.75);
        }

        public void TurnOffPump()
        {
            _phx21.TurnOffPump();
        }

        public DataPolledEventArgs GetStatus()
        {
            Phx21Status status = null;
            
            TryUtils.Retry(() => status = _phx21.ReadDataExtended(), 5, 100);

            if (status == null) throw new Exception("Could not get phx21 status");

            IsRunning = status.IsIgnited;

            var polledArgs = new DataPolledEventArgs(StatusToDictionary(status), (float)status.Ppm);

            foreach (var kv in polledArgs.PhxProperties.ToArray())
            {
                if (!PropertySelector.Phx42Translation.ContainsKey(kv.Key))
                    continue;

                polledArgs.PhxProperties[PropertySelector.Phx42Translation[kv.Key]] = kv.Value;
            }

            return polledArgs;
        }

        public Dictionary<string, string> GetData()
        {
            return GetStatus().PhxProperties;
        }

        public bool IsReconnecting => false;
        public string NoPpmLabel => "N/A";
        public void DeleteCalEntryRange(float ppmMin, float ppmMax, bool factoryCal)
        {
            List<PpmCalibrationInfo> keepCalibrations = new List<PpmCalibrationInfo>();

            PpmCalibrationInfo[] calibrations = GetAllCalibrations();

            foreach (var ppmCalibrationInfo in calibrations)
            {
                if (ppmCalibrationInfo.IsValid && (ppmCalibrationInfo.Ppm < ppmMin || ppmCalibrationInfo.Ppm > ppmMax))
                {
                    keepCalibrations.Add(ppmCalibrationInfo);
                }
            }

            DeleteCalibrations();

            keepCalibrations = keepCalibrations.OrderBy(c => c.Ppm).ToList();

            for (int i = 0; i < keepCalibrations.Count; i++)
            {
                var ppmCalibrationInfo = keepCalibrations[0];
                _phx21.SetPpmCalibration(i, (int)(ppmCalibrationInfo.Ppm*10), ppmCalibrationInfo.FidCurrent*10, (ushort)(ppmCalibrationInfo.H2Pressure*10), true);
            }

        }

        public void SetCalibrationAtIndex(int ppm, decimal pa, int index, string date, bool factoryCal)
        {
            _phx21.SetPpmCalibration(index, ppm*10);
        }

        public PpmCalibrationInfo[] GetAllCalibrations()
        {
            List<PpmCalibrationInfo> calibrations = new List<PpmCalibrationInfo>();

            for (int i = 0; i < 6; i++)
            {
                calibrations.Add(_phx21.GetPpmCalibration(i));
            }

            return calibrations.Where(c => c.IsValid).OrderBy(c => c.Ppm).ToArray();
        }

        public void DeleteCalibrations()
        {
            for (int i = 5; i >= 0; i--)
            {
                _phx21.SetPpmCalibration(i, 0, 0, 0, false);
                Task.Delay(1000).Wait();
            }
        }

        private static PropertyInfo[] _phx21StatusProperties = null;

        private Dictionary<string, string> StatusToDictionary(Phx21Status status)
        {
            if (_phx21StatusProperties == null)
            {
                _phx21StatusProperties = typeof(Phx21Status).GetRuntimeProperties().ToArray();
            }

            Dictionary<string, string> properties = new Dictionary<string, string>();

            foreach (var phx21StatusProperty in _phx21StatusProperties)
            {
                properties[phx21StatusProperty.Name] = phx21StatusProperty.GetValue(status).ToString();
            }

            return properties;
        }

        public void FlashBulkErase()
        {
            throw new NotImplementedException();
        }

        public void UpdateFirmware(byte[] firmwareFileBytes)
        {
            throw new NotImplementedException();
        }

        public void WriteToPhxLog(string text)
        {
            _phx21.WriteToPhxLog(text);
        }

        public void WriteMemoryWithLength(byte[] bytes)
        {
            _phx21.WriteDataWithLength(0, bytes);
        }

        public void WriteMemory(byte[] bytes)
        {
            _phx21.WriteData(bytes);
        }

        public byte[] ReadMemory()
        {
            return _phx21.ReadDataFromStoredLength(0);
        }

        public PpmCalibrationInfo GetCalibration(int index)
        {
            return _phx21.GetPpmCalibration(index);
        }

        public void SetSolenoidBOff()
        {
            _phx21.SetSolenoidBOff();
        }

        public void SetSolenoidBOn()
        {
            _phx21.SetSolenoidBOn();
        }

        public void SetSolenoidAOff()
        {
            _phx21.SetSolenoidAOff();
        }

        public void SetSolenoidAOn()
        {
            _phx21.SetSolenoidAOn();
        }

        public void IgniteGlowPlug1(int i)
        {
            throw new NotImplementedException();
        }

        public void IgniteGlowPlug2(int i)
        {
            throw new NotImplementedException();
        }

        public void WriteMemoryWithLengthAsync(byte[] bytes, Action callback, Action<Exception> errorCallback)
        {
            Task task = new Task(() =>
            {
                try
                {
                    WriteMemoryWithLength(bytes);
                    callback();
                }
                catch (Exception ex)
                {
                    Phx21?.WriteExceptionToPhxLog(ex);
                    errorCallback(ex);
                }
            });

            task.Start();
        }

        public void ReadMemoryAsync(Action<byte[]> callback)
        {
            Task task = new Task(() =>
            {
                byte[] bytes = ReadMemory();
                callback(bytes);
            });

            task.Start();
        }

        private bool useGlowB = false;

        public void Start()
        {
            Start(useGlowB);
            useGlowB = !useGlowB;
        }

        public void Start(bool useGlowPlugB)
        {
            _phx21.IgniteOn(useGlowPlugB);
        }

        public string GetFirmwareVersion()
        {
            return _phx21.GetFirmwareVersion();
        }

        public void WriteExceptionToPhxLog(Exception exception)
        {
            _phx21.WriteExceptionToPhxLog(exception);
        }

        public void Stop()
        {
            _phx21.IgniteOff();
            //continueReading = false;
            Status = "Connected";
        }

        public void Disconnect()
        {
            List<Exception> exceptions = new List<Exception>();

            try
            {
                _phx21.StopPollingData();
                _phx21.SendGoodbye();
                Task.Delay(1000).Wait(1000);
                _phx21.ShutdownNow = true;

                //this is just to send and receive a message through to shutdown the message threads
                _phx21.GetFirmwareVersion();

                _phx21.Error -= Phx21OnError;
                _phx21.WriteFlashProgress -= Phx21OnWriteFlashProgress;
                _phx21.ReadFlashProgress -= Phx21OnReadFlashProgress;
                //_phx21.StopPollingData();
            }
            catch (Exception)
            {
                //don't care....
                //exceptions.Add(ex);
                //Phx21?.WriteExceptionToPhxLog(ex);
            }

            try
            {
                bluetoothService.Disconnect(device);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
                Phx21?.WriteExceptionToPhxLog(ex);
            }

            _phx21 = null;

            IsConnected = false;
            Status = "Disconnected";

            OnDisconnected(new DeviceEventArgs(this));

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }


        protected virtual void OnConnected(DeviceEventArgs args)
        {
            Connected?.Invoke(args);
        }

        protected void OnDisconnected(DeviceEventArgs args)
        {
            Disconnected?.Invoke(args);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName]string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnStatusChanged()
        {
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnPhxDataPolled(DataPolledEventArgs e)
        {
            PhxDataPolled?.Invoke(this, e);

            ruea.ClearAndLoadVals(e.Ppm, e.Ppm.ToString());           
            InvokeReadingUpdated(ruea);
        }

        public void LogExceptionToPhxLog(Exception exception)
        {
            _phx21.WriteExceptionToPhxLog(exception);
        }
    }
}