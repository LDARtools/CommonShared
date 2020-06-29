using System;
using System.Collections.Generic;
using Ldartools.Common.Services;

namespace Ldartools.Common.Devices.Services
{
    public class TargetCompInfo
    {
        public bool IsCase { get; set; }
        public double RefNum { get; set; }
        public double Pos { get; set; }
        public double Neg { get; set; }
    }

    public interface IPhx42DeviceService : IPhxDeviceService
    {
        event EventHandler<CommandErrorEventArgs> CommandError;

        event EventHandler<UpdateFirmwareEventArgs> UpdateFirmwareProgress;
        event EventHandler<GetLogsProgressEventArgs> GetLogsProgress;
        string MacAddress { get; }
        string LogFilepath { get; }
        string DebugLogFilepath { get; }
        AutoIgnitionParameters GetAutoIgnitionParameters();

        void SetAutoIgniteParameters(double aset, double bset, double hset, double prtime,
            double atol, double btol, double htol, double gppwr, double bpause, double gpdur,
            double igtime, double minrise, double bslope);

        void SetAutoIgniteParameters(double sampleSetpoint, double combustionSetpoint, double lph2Setpoint);
        string GetBatterySN();

        void GenerateCalibration(float ppm, DateTime timeStamp);

        void SetH2TargetComp(bool isCase, double refNum, double pos, double neg);
        void SetP1TargetComp(bool isCase, double refNum, double pos, double neg);
        void SetP2TargetComp(bool isCase, double refNum, double pos, double neg);

        BatteryInfo GetBatteryInfo();
        void PulseGlowPlug(decimal durationSeconds, decimal powerPct);
        void SetHeaterDrive(double percent);
        void SetSolenoid(bool enabled);
        void SetSamplePumpCLCOn();
        void SetLPH2On();
        void SetCombustionPumpOn();
        bool Ignite();
        void SetLPH2Off();
        int PpmAverageCount { get; set; }
        void SetSamplePumpCLCOff();
        void SetCombustionPumpOff();
        void SetParamVersion(DateTime versionDate);

        void SetShutdownThresholds(decimal? p2InLow, decimal? p2InHigh, decimal? chamber, decimal? fDelta, decimal? fOff, bool? enableCode24);

        void SetShutdownThresholds(decimal? p2InLow, decimal? p2InHigh, decimal? chamber, decimal? fDelta,
            decimal? fOff, bool? enableCode24, decimal? lowVoltageShutdown);

        void SetShutdownThresholds(decimal? p2InLow, decimal? p2InHigh, decimal? p1InHMin, decimal? p1InHMax,
            decimal? chamber, decimal? fDelta, decimal? fOff, bool? enableCode24, decimal? lowVoltageShutdown);

        void SetShutdownThresholds(decimal p2InLow, decimal p2InHigh, decimal chamberOverTempLimit);
        void SetShutdownThresholds(decimal p2InLow, decimal chamberOverTempLimit);
        ShutdownThresholds GetShutdownThresholds();
        void SetH2Clc(bool? enable = true, decimal? low = null, decimal? high = null, int? distance = null, int? speed = null, double? tolerance = null);
        void AbortAutoIgnition();
        void SetTime(DateTime currentDateTime);
        DateTime GetTime();
        DateTime ReadParamVersion();

        void LogAllReadingsNow();

        DriftInfo GetDriftInfo();
        void SetDriftInfo(bool adjz, bool adja, double? timeNeg = null, double? paAdj = null);

        ClosedLoopControlParams SetSamplePumpCLCParams(double? target, int p, int i, int d, double offset, int ff);
        void SetSamplePumpLevel(double level);

        ClosedLoopControlParams SetCombustionPumpCLCParams(double? target, int p, int i, int d, double offset, int ff);
        void SetCaseThermConstants(int rp, int r25, int b);
        void SetChamberThermConstants(int rp, int r25, int b);
        void SetNeedleValveBacklash(int backlash);
        void MoveNeedleValve(int step, int rate);
        DDC114Params SetDDC114Params(int? range, int? integusec, bool? subtraction, bool? testPin);
        FIDFilteringParams SetFIDOutputFilteringParams(double iir, int avg, int riseCount = 0, int riseDelta = 0);
        void SetH2Clc(int unitSettingsH2Speed, int unitSettingsH2Steps);
        void StartPollingData(int interval = 333, bool pollBattery = true, bool pollReadings = true, bool pollDriveLevels = true, bool pollFIDReadings = true);
        Dictionary<string, string> GetDriveLevels();

        void SetPeriodicReportingInterval(int interval);
        void StopPeriodicReporting(bool battery = false, bool readings = false, bool driveLevels = false, bool fidReadings = false);
        void StartPeriodicReporting(bool battery = false, bool readings = false, bool driveLevels = false, bool fidReadings = false);

        int PeriodicReportingRate { get; }
        bool BatteryReportingEnabled { get; }
        bool ReadingsReportingEnabled { get; }
        bool DriveLevelsReportingEnabled { get; }
        bool FidReadingsReportingEnabled { get; }
        
        string GetLogs(int maxLog = 500, int delayBetweenMessages = 300);
        void StopPollingSpecificData(string dataType);
        void SetLowRangeMode(bool enable);

        int GetLogCount();
        void SetBluetoothName(string name);
        bool GetLowRangeMode();
        void SetCombustionPumpLevel(double level);
        void SetWarmupTime(int seconds);
        (string, bool) GetLogsFor(TimeSpan time, bool append, int maxLog = 500, int delayBetweenMessages = 300);
        void Unignite();
        void TurnOff();
    }

    public class CommandErrorEventArgs : EventArgs
    {
        public CommandErrorType ErrorType { get; }
        public string Error { get; }

        public CommandErrorEventArgs(CommandErrorType errorType, string error)
        {
            ErrorType = errorType;
            Error = error;
        }
    }

    public enum CommandErrorType
    {
        Shutdown,
        AutoIgnitionSequence,
        Message
    }
}