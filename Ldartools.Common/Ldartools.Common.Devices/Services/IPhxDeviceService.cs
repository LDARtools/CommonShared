using System;
using System.Collections.Generic;
using System.ComponentModel;
using Ldartools.Common.Services;

namespace Ldartools.Common.Devices.Services
{
    public interface IPhxDeviceService : IAnalyzerDeviceService
    {
        event EventHandler<DataPolledEventArgs> PhxDataPolled;
        Dictionary<string, string> PhxProperties { get; } 
        int UseAvgPerc { get; set; }
        int LongAverageCount { get; set; }
        int ShortAverageCount { get; set; }
        int PollingInterval { get; }

        void SetPpmCalibration(int indexNumber, int ppmTenths);
        void SetPpmCalibration(int indexNumber, int ppmTenths, int picoampsTenths, ushort H2Pressure, bool overwrite);
        void GenerateCalibration(float ppm);

        void WriteToBatteryFuelGaugeChip(int address, string hexData);
        string ReadFromBatteryFuelGaugeChip(int address, int count);

        float GetPpm();

        void TurnOnPump();
        void TurnOffPump();
        void WriteMemory(byte[] bytes);
        byte[] ReadMemory();
        void Start(bool useGlowPlugB);
        event EventHandler<WriteFlashProgressEventArgs> WriteFlashProgress;
        event EventHandler<ReadFlashProgressEventArgs> ReadFlashProgress;
        void WriteMemoryWithLengthAsync(byte[] bytes, Action callback, Action<Exception> errorCallback);
        void ReadMemoryAsync(Action<byte[]> callback);
        void WriteMemoryWithLength(byte[] bytes);
        bool IsPollingData { get; }
        void WriteToPhxLog(string text);
        string GetFirmwareVersion();
        PpmCalibrationInfo GetCalibration(int index);
        void SetSolenoidBOff();
        void SetSolenoidBOn();
        void SetSolenoidAOff();
        void SetSolenoidAOn();
        void IgniteGlowPlug1(int i);
        void IgniteGlowPlug2(int i);
        DataPolledEventArgs GetStatus();
        void FlashBulkErase();
        void UpdateFirmware(byte[] firmwareFileBytes);
        void LogExceptionToPhxLog(Exception exception);
        ITimeMongerService TimeMongerService { get; set; }
        Dictionary<string, string> GetData();
        bool IsReconnecting { get; }
        string NoPpmLabel { get; }
        void DeleteCalEntryRange(float ppmMin, float ppmMax, bool factoryCal);
        void SetCalibrationAtIndex(int ppm, decimal pa, int index, string date, bool factoryCal);
        PpmCalibrationInfo[] GetAllCalibrations();
        void DeleteCalibrations();
    }
}