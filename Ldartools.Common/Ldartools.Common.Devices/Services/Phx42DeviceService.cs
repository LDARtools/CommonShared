using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Ldartools.Common.IO;
using Ldartools.Common.Services;

namespace Ldartools.Common.Devices.Services
{
    public class Phx42DeviceService : IPhx42DeviceService
    {
        private readonly IBluetoothService _bluetoothService;
        private IDevice _device;
        private readonly IFileManager _fileManager;
        private bool _isRunning;
        private bool _reconnecting = false;

        public bool Unpaired { get; set; }

        public Phx42 phx42
        {
            get
            {
                CheckReconnect();
                if (!IsConnected) throw new Exception($"{SerialNumber} is not connected");
                return _phx42;
            }
            set { _phx42 = value; }
        }

        private string _lastPhx42Error = null;
        private string _ignitePhx42Error = null;

        public ITimeMongerService TimeMongerService { get; set; }

        public string SerialNumber { get; private set; }
        public event ConnectedHandler Connected;
        public event DisconnectedHandler Disconnected;
        public event ReconnectingHandler Reconnecting;
        public event ReconnectSuccessHandler ReconnectSuccess;

        public event EventHandler StatusChanged;
        public event EventHandler<DataPolledEventArgs> PhxDataPolled;
        public event EventHandler<WriteFlashProgressEventArgs> WriteFlashProgress;
        public event EventHandler<UpdateFirmwareEventArgs> UpdateFirmwareProgress;
        public event EventHandler<GetLogsProgressEventArgs> GetLogsProgress;

        public bool IsReconnecting => _reconnecting;

        public float Ppm { get; protected set; }
        public string NoPpmLabel => "-100 PPM";

        public int PeriodicReportingRate => phx42.PeriodicInterval;
        public int PollingInterval => phx42.PeriodicInterval;
        public bool BatteryReportingEnabled => phx42.BatteryReportingEnabled;
        public bool ReadingsReportingEnabled => phx42.ReadingsReportingEnabled;
        public bool DriveLevelsReportingEnabled => phx42.DriveLevelsReportingEnabled;
        public bool FidReadingsReportingEnabled => phx42.FidReadingsReportingEnabled;

        public string Application
        {
            get { return _phx42?.Application ?? string.Empty; }
            set { if (_phx42 != null) _phx42.Application = value; }
        }

        public string User
        {
            get { return _phx42?.User ?? string.Empty; }
            set { if (_phx42 != null) _phx42.User = value; }
        }

        public string Site
        {
            get { return _phx42?.Site ?? string.Empty; }
            set { if (_phx42 != null) _phx42.Site = value; }
        }

        public string GetLogs(int maxLog = 500, int delayBetweenMessages = 300)
        {
            return phx42.GetLogs(maxLog, delayBetweenMessages);
        }

        public void StopPollingSpecificData(string dataType)
        {
            phx42.StopPollingSpecificData(dataType);
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

        public bool IsPollingData => phx42.IsPolling;

        public string Status
        {
            get { return _status; }
            protected set
            {
                _status = value;

                OnStatusChanged();
            }
        }

        public Dictionary<string, string> PhxProperties { get; protected set; }

        public int UseAvgPerc
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public int LongAverageCount
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public int ShortAverageCount
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public bool IsConnected
        {
            get { return _isConnected; }
            private set { _isConnected = value; }
        }

        public DeviceType DeviceType => DeviceType.Phx42;

        public void Unignite()
        {
            phx42.AbortIgnite();
        }

        public void TurnOff()
        {
            phx42.TurnOff();
        }

        public event EventHandler<CommandErrorEventArgs> CommandError;
        public string MacAddress { get; }
        public string LogFilepath { get; protected set; }
        public string DebugLogFilepath { get; protected set; }

        public event ReadingUpdatedHandler ReadingUpdated;

        protected virtual void InvokeReadingUpdated(ReadingUpdatedEventArgs args)
        {
            ReadingUpdatedHandler updated = ReadingUpdated;
            if (updated != null) updated(args);
        }

        public bool IsRunning
        {
            get { return _isRunning; }
            set
            {
                if (Equals(_isRunning, value))
                    return;

                _isRunning = value;

                OnPropertyChanged();
            }
        }

        public int SignalStrength
        {
            get { return _device.SignalStrength; }
            set
            {
                if (_device == null) return;

                _device.SignalStrength = value;
                OnPropertyChanged();
            }
        }

        public void ConfigureLogging()
        {
            if (phx42 == null)
                throw new Exception("phx42 is not connected.  Connect to the device first.");

            phx42.ConfigureLogging();
            LogFilepath = phx42.LogFilePath;
            DebugLogFilepath = phx42.DebugLogFilePath;
        }

        public void ConfigureLogging(string loggingDirectory)
        {
            if (phx42 == null)
                throw new Exception("phx42 is not connected.  Connect to the device first.");

            phx42.ConfigureLogging(loggingDirectory);
            LogFilepath = phx42.LogFilePath;
            DebugLogFilepath = phx42.DebugLogFilePath;
        }

        public void UseScheduler(RepeatedTaskScheduler scheduler)
        {
            phx42.UseScheduler(scheduler);
        }

        public void StartPollingData(int interval = 333)
        {
            if (phx42 == null)
                throw new Exception("phx42 is not connected.  Connect to the device first.");

            phx42.StartPollingData(interval);
        }

        public void StartPollingData(int interval = 333, bool pollBattery = true, bool pollReadings = true, bool pollDriveLevels = true, bool pollFIDReadings = true)
        {
            if (phx42 == null)
                throw new Exception("phx42 is not connected.  Connect to the device first.");

            phx42.StartPollingData(interval, pollBattery, pollReadings, pollDriveLevels, pollFIDReadings);
        }

        public void StopPollingData()
        {
            if (phx42 == null)
                throw new Exception("phx42 is not connected.  Connect to the device first.");

            phx42.StopPollingData();
        }

        public void SetParamVersion(DateTime versionDate)
        {
            phx42.WriteParamsVersion(versionDate);
        }

        public void SetShutdownThresholds(decimal? p2InLow, decimal? p2InHigh, decimal? chamber, decimal? fDelta, decimal? fOff, bool? enableCode24)
        {
            phx42.SetPumpShutdownThresholds(p2InLow, p2InHigh, chamber, fDelta, fOff, enableCode24);
        }

        public void SetShutdownThresholds(decimal? p2InLow, decimal? p2InHigh, decimal? chamber, decimal? fDelta, decimal? fOff, bool? enableCode24, decimal? lowVoltageShutdown)
        {
            phx42.SetPumpShutdownThresholds(p2InLow, p2InHigh, chamber, fDelta, fOff, enableCode24, lowVoltageShutdown);
        }

        public void SetShutdownThresholds(decimal? p2InLow, decimal? p2InHigh, decimal? p1InHMin, decimal? p1InHMax, decimal? chamber, decimal? fDelta, decimal? fOff, bool? enableCode24, decimal? lowVoltageShutdown)
        {
            phx42.SetPumpShutdownThresholds(p2InLow, p2InHigh, p1InHMin, p1InHMax, chamber, fDelta, fOff, enableCode24, lowVoltageShutdown);
        }

        public void SetShutdownThresholds(decimal p2InLow, decimal p2InHigh, decimal chamberOverTempLimit)
        {
            phx42.SetPumpShutdownThresholds(p2InLow, p2InHigh, chamberOverTempLimit);
        }

        public void SetShutdownThresholds(decimal p2InLow, decimal chamberOverTempLimit)
        {
            phx42.SetPumpShutdownThresholds(p2InLow, chamberOverTempLimit);
        }

        public ShutdownThresholds GetShutdownThresholds()
        {
            return phx42.GetPumpShutdownThresholds();
        }

        public void SetH2Clc(bool? enable = true, decimal? low = null, decimal? high = null, int? distance = null, int? speed = null, double? tolerance = null)
        {
            phx42.SetH2ClosedLoopControl(enable, (double?)low, (double?)high, distance, speed, tolerance);
        }

        public void AbortAutoIgnition()
        {
            phx42.AbortIgnite();
        }

        public void SetTime(DateTime currentDateTime)
        {
            phx42.SetTime(currentDateTime);
        }

        public DateTime GetTime()
        {
            return phx42.GetTime();
        }

        public void LogAllReadingsNow()
        {
            phx42.LogAllReadingsNow();
        }

        public DateTime ReadParamVersion()
        {
            return phx42.ReadParamsVersion();
        }

        public void DeleteCalEntryRange(float minPpm, float maxPpm, bool includePers)
        {
            phx42.DeleteCalEntryRange(minPpm, maxPpm, includePers);
        }

        public double? LastBackgroundReading { get; set; }

        private bool _isConnected;
        private string _status;

        public Phx42DeviceService(IBluetoothService bluetoothService, IDevice device, IFileManager fileManager)
        {
            _bluetoothService = bluetoothService;
            _device = device;
            _fileManager = fileManager;

            PhxProperties = new Dictionary<string, string>();

            SerialNumber = device.Name;
            MacAddress = device.Address;
        }

        public async Task<string> Unpair()
        {
            var result = await _bluetoothService.Unpair(_device);
            Unpaired = true;

            return result;
        }

        public async Task<string> Pair()
        {
            return await _bluetoothService.Pair(_device);
        }

        public void Connect(string application = "", string user = "", string site = "")
        {
            if (IsConnected)
                return;

            var streams = _bluetoothService.Connect(_device);

            Unpaired = false;

            phx42 = new Phx42(streams.InputStream,
                streams.OutputStream,
                _fileManager,
                _device.Name, TimeMongerService, application, user, site);

            IsConnected = true;

            phx42.Error += Phx42OnError;
            phx42.CommandError += Phx42OnCommandError;
            phx42.WriteFlashProgress += Phx42OnWriteFlashProgress;
            phx42.ReadFlashProgress += Phx42OnReadFlashProgress;
            phx42.UpdateFirmwareProgress += Phx42OnUpdateFirmwareProgress;
            phx42.GetLogsProgress += Phx42OnGetLogsProgress;
            phx42.DataPolled += Phx42OnDataPolled;
            Status = "Connected";
            OnConnected(new DeviceEventArgs(this));
        }

        private void Phx42OnGetLogsProgress(object sender, GetLogsProgressEventArgs e)
        {
            OnGetLogsProgress(e);
        }

        private void Phx42OnUpdateFirmwareProgress(object sender, UpdateFirmwareEventArgs updateFirmwareEventArgs)
        {
            OnUpdateFirmwareProgress(updateFirmwareEventArgs);
        }

        private void Phx42OnCommandError(object sender, CommandErrorEventArgs commandErrorEventArgs)
        {
            _lastPhx42Error = commandErrorEventArgs.Error;

            if (commandErrorEventArgs.ErrorType == CommandErrorType.AutoIgnitionSequence || commandErrorEventArgs.ErrorType == CommandErrorType.Shutdown)
            {
                _ignitePhx42Error = commandErrorEventArgs.Error;
            }

            if (commandErrorEventArgs.ErrorType == CommandErrorType.Shutdown && IsRunning)
            {
                IsRunning = false;
            }

            OnCommandError(commandErrorEventArgs.ErrorType, commandErrorEventArgs.Error);
        }

        private void Phx42OnReadFlashProgress(object sender, ReadFlashProgressEventArgs readFlashProgressEventArgs)
        {
            OnReadFlashProgress(readFlashProgressEventArgs);
        }

        private void Phx42OnWriteFlashProgress(object sender, WriteFlashProgressEventArgs writeFlashProgressEventArgs)
        {
            OnWriteFlashProgress(writeFlashProgressEventArgs);
        }

        private bool disconnecting = false;

        private void Phx42OnError(object sender, ErrorEventArgs errorEventArgs)
        {
            if (_reconnecting || disconnecting)
                return;

            if (errorEventArgs.Exception.GetType().FullName.StartsWith("Java.IO.IOException") || errorEventArgs.Exception is ReconnectNeededException)
            {
                    disconnecting = true;
                //try
                //{
                    Disconnect();
                //}
                //catch (Exception ex)
                //{
                //    //ignore
                //}
                //try
                //{
                //    phx42?.WriteToPhxLog("Attempting to reconnect");
                //    Reconnect();
                //    phx42?.WriteToPhxLog("Reconnect success");
                //}
                //catch (Exception ex)
                //{
                //    IsConnected = false;
                //    OnDisconnected(new DeviceEventArgs(this));
                //}
            }
            else
            {
                //do something with this maybe? It's already in the device log
            }
        }

        public int PpmAverageCount { get; set; } = 100;
        public void SetSamplePumpCLCOff()
        {
            phx42.SetSamplePumpClosedLoopControl(false);
            phx42.SetSamplePumpLevel(0);
        }

        public void SetCombustionPumpOff()
        {
            phx42.SetCombustionPumpClosedLoopControl(false);
            phx42.SetCombustionPumpLevel(0);
        }

        private Phx42 _phx42;

        private void Phx42OnDataPolled(object sender, DataPolledEventArgs dataPolledEventArgs)
        {
            try
            {
                IsRunning = bool.Parse(dataPolledEventArgs.PhxProperties[Phx42PropNames.IsIgnited]);
                
                PhxProperties = dataPolledEventArgs.PhxProperties;

                try
                {
                    var lastPpmsCount = phx42.LastPpms.Count;
                    var skip = Math.Max(lastPpmsCount - PpmAverageCount, 0);
                    var take = Math.Min(PpmAverageCount, lastPpmsCount);

                    dataPolledEventArgs.PhxProperties["PPMAverage"] =
                        Math.Round(phx42.LastPpms.Skip(skip).Take(take).Average(), 1).ToString();
                }
                catch (Exception)
                {
                    dataPolledEventArgs.PhxProperties["PPMAverage"] = "-100";
                    //ignore
                }

                Ppm = dataPolledEventArgs.Ppm;
                OnPhxDataPolled(dataPolledEventArgs);
            }
            catch (Exception ex)
            {
                phx42.WriteToPhxLog("Couldn't parse polled data:");
                phx42.WriteExceptionToPhxLog(ex);
            }


        }

        public override string ToString()
        {
            return SerialNumber;
        }

        public void DeleteCalibration(int index)
        {
            phx42.DeleteCalEntry(index);
        }

        public void SetPpmCalibration(int indexNumber, int ppmTenths)
        {
            throw new NotImplementedException();
        }

        public void SetPpmCalibration(int indexNumber, int ppmTenths, int picoampsTenths, ushort H2Pressure, bool overwrite)
        {
            if (ppmTenths == 0 && picoampsTenths == 0 && H2Pressure == 0 && !overwrite)
            {
                phx42.DeleteCalEntry(indexNumber);
            }
            else
            {
                throw new Exception("This function is provided for backward compatability for deleting cals, please use SetCalibrationAtIndex for setting calibrations");
            }
        }

        public void WriteToBatteryFuelGaugeChip(int address, string hexData)
        {
            phx42.WriteToBatteryFuelGaugeChip(address, hexData);
        }

        public DriftInfo GetDriftInfo()
        {
            return phx42.GetDriftInfo();
        }

        public void SetDriftInfo(bool adjz, bool adja, double? timeNeg = null, double? paAdj = null)
        {
            phx42.SetDriftInfo(adjz, adja, timeNeg, paAdj);
        }

        public string ReadFromBatteryFuelGaugeChip(int address, int count)
        {
            return phx42.ReadFromBatteryFuelGaugeChip(address, count);
        }

        public void GenerateCalibration(float ppm)
        {
            var timestamp = DateTime.Now;
            phx42.GenerateCalTableEntry(ppm, timestamp);
        }

        public void GenerateCalibration(float ppm, DateTime timeStamp)
        {
            phx42.GenerateCalTableEntry(ppm, timeStamp);
        }

        public void SetCaseThermConstants(int rp, int r25, int b)
        {
            phx42.SetCaseThermConstants(rp, r25, b);
        }

        public void SetChamberThermConstants(int rp, int r25, int b)
        {
            phx42.SetChamberThermConstants(rp, r25, b);
        }

        public ClosedLoopControlParams SetSamplePumpCLCParams(double? target, int p, int i, int d, double offset, int ff)
        {
            return phx42.SetSamplePumpClosedLoopControl(null, target, p, i, d, offset, ff);
        }

        public void SetSamplePumpLevel(double level)
        {
            phx42.SetSamplePumpLevel(level);
        }

        public void SetCombustionPumpLevel(double level)
        {
            phx42.SetCombustionPumpLevel(level);
        }

        public void SetSamplePumpCLCOn()
        {
            phx42.SetSamplePumpClosedLoopControl(true);
        }

        public void SetLPH2On()
        {
            phx42.SetH2ClosedLoopControl(true);
        }

        public ClosedLoopControlParams SetCombustionPumpCLCParams(double? target, int p, int i, int d, double offset, int ff)
        {
            return phx42.SetCombustionPumpClosedLoopControl(null, target, p, i, d, offset, ff);
        }

        public void SetCombustionPumpOn()
        {
            phx42.SetCombustionPumpClosedLoopControl(true);
        }

        public bool Ignite()
        {
            bool isIgnited = false;
            //int count = 0;
            
            //while (!isIgnited && count < tries)
            //{
                try
                {
                    _ignitePhx42Error = null;
                    phx42.WriteToPhxLog("Attempting to ignite");
                    phx42.Ignite();

                    var stopWatch = Stopwatch.StartNew();

                    //Should error out before 5 minutes if not successful
                    while (stopWatch.ElapsedMilliseconds < 300000)
                    {
                        var data = phx42.GetData();
                        isIgnited = data.ContainsKey(Phx42PropNames.IsIgnited) && bool.Parse(data[Phx42PropNames.IsIgnited]);
                        
                        if (isIgnited)
                            break;

                        if (!string.IsNullOrWhiteSpace(_ignitePhx42Error))
                        {
                            break;
                        }

                        Task.Delay(250).Wait(250);
                    }
                }
                catch (Exception ex)
                {
                    phx42.WriteToPhxLog("Problem igniting");
                    phx42.WriteExceptionToPhxLog(ex);
                }

                //count++;
            //}

            IsRunning = isIgnited;

            phx42.WriteToPhxLog($"Finished trying to ignite, ignited = {isIgnited}");

            if (!IsRunning && !string.IsNullOrWhiteSpace(_ignitePhx42Error))
                throw new Exception($"phx42 Ignite error: {_ignitePhx42Error}");

            return isIgnited;
        }

        public void SetLPH2Off()
        {
            phx42.SetH2ClosedLoopControl(false);
        }

        public void TurnOnPump()
        {
            throw new NotImplementedException();
            //Phx42.TurnOnPumpToTargetPressure(1.75);
        }

        public void TurnOffPump()
        {
            throw new NotImplementedException();
            //Phx42.TurnOffPump();
        }

        public void WriteToPhxLog(string text)
        {
            try
            {
                phx42.WriteToPhxLog(text);
            }
            catch (Exception)
            {
                //ignore
            }
        }

        public void WriteMemoryWithLength(byte[] bytes)
        {
            var length = bytes.Length;

            var lengthBytes = BitConverter.GetBytes(length);

            List<byte> lengthAndData = new List<byte>();
            lengthAndData.AddRange(lengthBytes);
            lengthAndData.AddRange(bytes);

            phx42.WriteData(lengthAndData.ToArray());
        }

        public void WriteMemory(byte[] bytes)
        {
            phx42.WriteData(bytes);
        }

        public byte[] ReadMemory()
        {
            var lengthBytes = phx42.ReadData(4);

            var length = BitConverter.ToInt32(lengthBytes, 0);

            if (length < 0 || length > 100000) throw new Exception($"Bad message size: {length}");

            var readData = phx42.ReadData(4, length);

            return readData;
        }

        public PpmCalibrationInfo GetCalibration(int index)
        {
            return phx42.GetCalibrationTable()[index];
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

        public float GetPpm()
        {
            return phx42.GetPpm();
        }

        public DataPolledEventArgs GetStatus()
        {
            var ppm = phx42.GetPpm();
            var status = phx42.CurrentStatus;

            status["PPM"] = ppm.ToString();

            IsRunning = ppm >= 0;
            
            return new DataPolledEventArgs(status, ppm);
        }

        public void FlashBulkErase()
        {
            phx42.FlashBulkErase();
        }

        public void UpdateFirmware(byte[] firmwareFileBytes)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (phx42.UpdateFirmware(firmwareFileBytes))
                    break;
            }
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

        public void Start()
        {
            Ignite();
            //phx42.Ignite();

            /*Task task = new Task(
                () =>
                {
                    //Set Pump A
                    phx42.SetPumpDrive(1, 32);

                    //Set Pump B
                    phx42.SetPumpDrive(2, 58);

                    //Record FID Temp
                    var initReadings = phx42.GetReadings();
                    var initFIDTemp = decimal.Parse(initReadings["ChamberOuterTemp"]);

                    //Turn on Solenoid
                    phx42.SetSolenoid(true);

                    //LPH2 Control loop on
                    phx42.SetNeedleValveBacklash(10);
                    phx42.SetH2ClosedLoopControl(true, 0.93, 0.95, -30, 600);

                    //Wait for LPH2 to get in range
                    bool lph2InRange = false;

                    while (!lph2InRange)
                    {
                        var readings = phx42.GetReadings();
                        var lph2 = double.Parse(readings["AirPressure"]);
                        lph2InRange = lph2 >= 0.93 && lph2 <= 0.95;
                        Task.Delay(10).Wait();
                    }

                    //Glow plug on
                    phx42.PulseGlowPlug(5);

                    //Pump B off
                    phx42.SetPumpDrive(2, 0);

                    //When FID temp increases 5 degrees turn off glow plug
                    bool tempIncreased = false;

                    while (!tempIncreased)
                    {
                        var readings = phx42.GetReadings();
                        tempIncreased = decimal.Parse(readings["ChamberOuterTemp"]) - initFIDTemp >= 5;
                        Task.Delay(10).Wait();
                    }

                    //Set Pump B
                    phx42.SetPumpDrive(2, 58);

                    //100 ms delay
                    Task.Delay(100).Wait();

                    //Turn off glow plug
                    phx42.PulseGlowPlug(0);
                    
                    //Turn on Deadhead detection (future)
                });

            task.Start();*/
        }

        public void Start(bool useGlowPlugB)
        {
            Start();
        }

        public string GetFirmwareVersion()
        {
            return phx42.GetFirmwareVersion();
        }

        public void Stop()
        {
            phx42.SetSolenoid(false);
            phx42.SetCombustionPumpLevel(0);
            phx42.SetSamplePumpLevel(0);
        }

        public void Reconnect()
        {
            try
            {
                Reconnecting?.Invoke(new DeviceEventArgs(this));
                _reconnecting = true;
                if (phx42 != null)
                {
                    try
                    {

                        phx42.Error -= Phx42OnError;
                        phx42.CommandError -= Phx42OnCommandError;
                        phx42.WriteFlashProgress -= Phx42OnWriteFlashProgress;
                        phx42.ReadFlashProgress -= Phx42OnReadFlashProgress;
                        phx42.UpdateFirmwareProgress -= Phx42OnUpdateFirmwareProgress;

                        phx42.ShutdownNow = true;
                    }
                    catch (Exception ex)
                    {
                        phx42?.WriteToPhxLog("Problem disconnecting");
                        phx42?.WriteExceptionToPhxLog(ex);
                    }

                    IsConnected = false;
                    Status = "Disconnected";
                }

                try
                {
                    var streams = _bluetoothService.Reconnect(_device);

                    phx42 = new Phx42(streams.InputStream,
                        streams.OutputStream,
                        _fileManager,
                        _device.Name, TimeMongerService);

                    phx42.Error += Phx42OnError;
                    phx42.CommandError += Phx42OnCommandError;
                    phx42.WriteFlashProgress += Phx42OnWriteFlashProgress;
                    phx42.ReadFlashProgress += Phx42OnReadFlashProgress;
                    phx42.UpdateFirmwareProgress += Phx42OnUpdateFirmwareProgress;

                    if (IsPollingData) phx42.StartPollingData();

                    IsConnected = true;
                    Status = "Connected";
                    ReconnectSuccess?.Invoke(new DeviceEventArgs(this));
                }
                catch (Exception ex)
                {
                    phx42?.WriteToPhxLog("Problem reconnecting");
                    phx42?.WriteExceptionToPhxLog(ex);
                    OnDisconnected(new DeviceEventArgs(this));
                    return;
                }
            }
            finally
            {
                _reconnecting = false;
            }
        }

        public void Disconnect()
        {
            if (!IsConnected) return;

            try
            {
                phx42.DataPolled -= Phx42OnDataPolled;
                phx42.Error -= Phx42OnError;
                phx42.CommandError -= Phx42OnCommandError;
                phx42.WriteFlashProgress -= Phx42OnWriteFlashProgress;
                phx42.ReadFlashProgress -= Phx42OnReadFlashProgress;
                phx42.UpdateFirmwareProgress -= Phx42OnUpdateFirmwareProgress;
                phx42.StopPollingData();

                Task.Delay(500).Wait(500);

                phx42.ShutdownNow = true;

                Task.Delay(500).Wait(500);
            }
            catch (Exception ex)
            {
                phx42?.WriteToPhxLog("Problem stopping polling");
                phx42?.WriteExceptionToPhxLog(ex);
            }

            try
            {
                phx42.ShutdownNow = true;

                _bluetoothService.Disconnect(_device);
            }
            catch (Exception ex)
            {
                phx42?.WriteToPhxLog("Problem disconnecting");
                phx42?.WriteExceptionToPhxLog(ex);
            }

            IsConnected = false;
            Status = "Disconnected";

            OnDisconnected(new DeviceEventArgs(this));
        }


        protected virtual void OnConnected(DeviceEventArgs args)
        {
            Connected?.Invoke(args);
        }

        protected void OnDisconnected(DeviceEventArgs args)
        {
            Disconnected?.Invoke(args);
        }

        protected virtual void OnStatusChanged()
        {
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnPhxDataPolled(DataPolledEventArgs e)
        {
            PhxDataPolled?.Invoke(this, e);

            InvokeReadingUpdated(new ReadingUpdatedEventArgs(e.Ppm,  e.Ppm.ToString()));
        }

        public AutoIgnitionParameters GetAutoIgnitionParameters()
        {
            return phx42.GetAutoIgnitionParameters();
        }

        public void SetCalibrationAtIndex(int ppm, decimal pa, int index, string timestamp, bool persist = false)
        {
            phx42.SetCalibrationAtIndex(ppm, pa, index, timestamp, persist);
        }

        public void SetAutoIgniteParameters(double aset, double bset, double hset, double prtime,
            double atol, double btol, double htol, double gppwr, double bpause, double gpdur,
            double igtime, double minrise, double bslope)
        {
            phx42.SetAutoIgniteParameters(aset, bset, hset, prtime, atol, btol, htol, 
                gppwr, bpause, gpdur, igtime, minrise, bslope);
        }

        public void SetAutoIgniteParameters(double sampleSetpoint, double combustionSetpoint, double lph2Setpoint)
        {
            phx42.SetAutoIgniteParameters(sampleSetpoint, combustionSetpoint, lph2Setpoint);
        }
        public string GetBatterySN()
        {
            return phx42.GetBatterySerial();
        }

        public PpmCalibrationInfo[] GetAllCalibrations()
        {
            return phx42.GetCalibrationTable();
        }

        public void DeleteCalibrations()
        {
            for (int i = 9; i >=0; i--)
            {
                phx42.DeleteCalEntry(i);
            }
        }

        public void SetH2TargetComp(bool isCase, double refNum, double pos, double neg)
        {
            phx42.SetH2TargetComp(isCase, refNum, pos, neg);
        }

        public void SetP1TargetComp(bool isCase, double refNum, double pos, double neg)
        {
            phx42.SetP1TargetComp(isCase, refNum, pos, neg);
        }

        public void SetP2TargetComp(bool isCase, double refNum, double pos, double neg)
        {
            phx42.SetP2TargetComp(isCase, refNum, pos, neg);
        }

        public BatteryInfo GetBatteryInfo()
        {
            return phx42.GetBatteryStatus();
        }

        public void PulseGlowPlug(decimal durationSeconds, decimal powerPct=0.75m)
        {
            phx42.PulseGlowPlug(durationSeconds, powerPct);
        }

        public Dictionary<string, string> GetData()
        {
            return phx42.GetData();
        }

        public void SetHeaterDrive(double percent)
        {
            phx42.SetHeaterDrive(percent);
        }

        public void SetSolenoid(bool enabled)
        {
            phx42.SetSolenoid(enabled);
        }

        public void SetH2LPClosedLoopControl(bool enable, double lowPsi, double highPsi, int distance, int speed, double? tolerance)
        {
            phx42.SetH2ClosedLoopControl(enable, lowPsi, highPsi, distance, speed, tolerance);
        }

        public void SetNeedleValveBacklash(int backlash)
        {
            phx42.SetNeedleValveBacklash(backlash);
        }

        public void MoveNeedleValve(int step, int rate)
        {
            phx42.MoveNeedleValve(step, rate);
        }

        public DDC114Params SetDDC114Params(int? range, int? integusec, bool? subtraction, bool? testPin)
        {
            return phx42.SetDDC114Params(range, integusec, subtraction, testPin);
        }

        public FIDFilteringParams SetFIDOutputFilteringParams(double iir, int avg, int riseCount = -1, int riseDelta = -1)
        {
            return phx42.SetFIDOutputFilterParams(iir, avg, riseCount, riseDelta);
        }

        public void SetH2Clc(int speed, int distance)
        {
            phx42.SetH2ClosedLoopControl(distance, speed);
        }
        
         public void SetPeriodicReportingInterval(int interval)
        {
            phx42.SetPeriodicReportingInterval(interval);
        }


        public void StopPeriodicReporting(bool battery = false, bool readings = false, bool driveLevels = false, bool fidReadings = false)
        {
            phx42.StopPeriodicReporting(battery, readings, driveLevels, fidReadings);
        }

        public void StartPeriodicReporting(bool battery = false, bool readings = false, bool driveLevels = false, bool fidReadings = false)
        {
            phx42.StartPeriodicReporting(battery, readings, driveLevels, fidReadings);
        }

        public Dictionary<string, string> GetDriveLevels()
        {
            return phx42.GetDriveLevels();
        }

        public void LogExceptionToPhxLog(Exception exception)
        {
            phx42.WriteExceptionToPhxLog(exception);
        }

        public void SendRawCommand(string command)
        {
            phx42.SendRawCommand(command);
        }

        public void RegisterForAllMessages()
        {
            phx42.MessageReceived += Phx42_MessageReceived;
        }

        public void DeregisterForAllMessages()
        {
            phx42.MessageReceived -= Phx42_MessageReceived;
        }

        public event EventHandler<string> MessageReceived;

        private void Phx42_MessageReceived(object sender, string e)
        {
            MessageReceived?.Invoke(this, e);
        }

        public void SetLowRangeMode(bool enable)
        {
            phx42.SetLowRangeMode(enable);
        }

        public int GetLogCount()
        {
            return phx42.GetLogCount();
        }

        public void SetBluetoothName(string name)
        {
            phx42.SetBluetoothName(name);
        }

        public bool GetLowRangeMode()
        {
            return phx42.GetLowRangeMode();
        }

        public void SetWarmupTime(int seconds)
        {
            phx42.SetWarmupTime(seconds);
        }

        public (string, bool) GetLogsFor(TimeSpan time, bool append, int maxLog = 500, int delayBetweenMessages = 300)
        {
            return phx42.GetLogsFor(time, append, maxLog, delayBetweenMessages);
        }

        protected virtual void OnCommandError(CommandErrorType errorType, string error)
        {
            CommandError?.Invoke(this, new CommandErrorEventArgs(errorType, error));
        }

        protected virtual void OnUpdateFirmwareProgress(UpdateFirmwareEventArgs e)
        {
            UpdateFirmwareProgress?.Invoke(this, e);
        }

        protected virtual void CheckReconnect()
        {
            DateTime start = DateTime.Now;

            while (_reconnecting && (DateTime.Now - start).TotalSeconds < 30) Task.Delay(100).Wait(100);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnGetLogsProgress(GetLogsProgressEventArgs e)
        {
            GetLogsProgress?.Invoke(this, e);
        }
    }
    
}