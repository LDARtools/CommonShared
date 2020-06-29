
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ldartools.Common.DataStructures;
using Ldartools.Common.Devices.Services;
using Ldartools.Common.IO;
using Ldartools.Common.Services;

namespace Ldartools.Common.Devices
{
    public static class Phx42PropNames
    {
        public const string Solenoid = "Solenoid";
        public const string GlowPlug = "GlowPlug";
        public const string Current = "Current";
        public const string InternalTemp = "InternalTemp";
        public const string ExternalTemp = "ExternalTemp";
        public const string HPH2 = "HPH2";
        public const string LPH2 = "LPH2";
        public const string SamplePressure = "SamplePressure";
        public const string SamplePpl = "SamplePPL";
        public const string CombustionPpl = "CombustionPPL";
        public const string CombustionPressure = "CombustionPressure";
        public const string PicoAamps = "PicoAmps";
        public const string IsIgnited = "IsIgnited";
        public const string PPM = "PPM";
        public const string Timestamp = "Timestamp";
        public const string BatteryCharge = "BatteryCharge";
        public const string BatteryStatus = "BatteryStatus";
        public const string PPMAverage = "PPMAverage";
        public const string CaseTemp = "CaseTemp";
        public const string Vacuum = "Vacuum";
        public const string NeedleValve = "NeedleValve";
        public const string Heater = "Heater";
        public const string PaOffset = "PaOffset";
        public const string P1Target = "P1Target";
        public const string P2Target = "P2Target";
        public const string Altimeter = "Altimeter";
        public const string H2Target = "H2Target";
        public const string Volts = "Volts";
    }

    public class ReconnectNeededException : Exception
    {

    }


    public sealed class Phx42
    {
        private Timer LoggingTimer;
        private Timer logWriteTimer;
        private bool inWriteLog = false;

        private const string hostToUnit = "ZUzu";
        private const string unitToHost = "YTyt";
        private const string endOfMessage = "\x0D\x0A";

        private MaxSizeList<CommMessage> receivedMessages = new MaxSizeList<CommMessage>(20);
        private ConcurrentQueue<CommMessage> _receivedLogMessages = new ConcurrentQueue<CommMessage>();
        private ConcurrentDictionary<string, KeyValuePair<DateTime, int>> errors = new ConcurrentDictionary<string, KeyValuePair<DateTime, int>>();
        private ConcurrentDictionary<string, DateTime> sentMessages = new ConcurrentDictionary<string, DateTime>();

        #region Define Constants

        private const int longTimeout = 5000;
        private const string msgCommCheck = "CHEK";
        private const string msgEnablePeriodicReports = "PRPT";
        private const string msgPeriodicReportinInterval = "TRPT";
        private const string msgRequestSingleReport = "SRPT";
        private const string msgReadings = "RDNG";
        private const string msgDriveLevels = "DRVL";
        private const string msgCalibrationTable = "CALT";
        private const string msgGenerateCalTableEntry = "GCTE";
        private const string msgRealTimeClock = "TIME";
        private const string msgHsPressureComp = "H2PC";
        private const string msgFIDReadings = "FIDR";
        private const string msgSetHeaterDrive = "HEAT";
        private const string msgGlowPlugPulse = "GPPU";
        private const string msgMoveNeedleValve = "MNDL";
        private const string msgSetValveBacklash = "SVBL";
        private const string msgDebug = "DEBG";
        private const string msgErrorReport = "EROR"; // these are sent in response to a specific command
        private const string msgSpontaneousErrorReport = "SERR"; // these are for errors that are not in response to a specific command
        private const string msgPowerControl = "POWR";
        private const string msgPowerPushButton = "PBTN";
        private const string msgSetPumpDrive = "PUMP";
        private const string msgBluetoothName = "BTNM";
        private const string msgSolenoid = "SOLN";
        private const string msgFwVersion = "VERS";
        private const string msgDeleteCalTableEntry = "DCTE";
        private const string msgDeleteCalTableRange = "DCTR";
        private const string msgFlashDownload = "FLDL";
        private const string msgExtFlashRead = "EFRD";
        private const string msgExtFlashWrite = "EFWR";
        private const string msgExtFlashBulkErase = "EFBE";
        private const string msgExtFlashSectorErase = "EFSE";
        private const string msgSetH2LpCloseLoopCtrl = "H2CL";
        private const string msgCalAllLpsAtZero = "LPZR";
        private const string msgCalH2HpAtZero = "HPZR";
        private const string msgLpCalPoint = "LPCA";
        private const string msgHpCalPoint = "HPCA";
        private const string msgSetLpCalDefault = "LPFD";
        private const string msgSetHpCalDefault = "HPFD";
        private const string msgSetPump1ClosedLoopCtrl = "P1CL";
        private const string msgSetPump2ClosedLoopCtrl = "P2CL";
        private const string msgSetNtcConstantsCase = "NTC0";
        private const string msgSetNTCConstantsChamber = "NTC1";
        private const string msgSystemShutdownReport = "SHUT";
        private const string msgSystemShutdownThresholds = "SSTH";
        private const string msgAutoIgnitionParameters = "AIGP";
        private const string msgStartAutoIgnitionSequence = "AIGS";
        private const string msgStatusOfAutoIgnitionSequence = "AIGS";
        private const string msgWriteToBatteryFuelGaugeChip = "BATW";
        private const string msgReadFromBatteryFuelGaugeChip = "BATR";
        private const string msgBatteryStatus = "BATS";
        private const string msgWriteParameterVerNum = "WPVN";
        private const string msgReadsParameterVerNum = "RPVN";
        private const string msgSetShutdownThresholds = "SSTH";
        private const string msgSetFidOutputFiltering = "FILT";
        private const string msgSetDDC114Params = "DDCS";
        private const string msgSetH2TargetComp = "H2TC";
        private const string msgSetP1TargetComp = "P1TC";
        private const string msgSetP2TargetComp = "P2TC";
        private const string msgDriftInfo = "DRFT";
        private const string msgSetBluetoothName = "BTNM";
        private const string msgSetLowRangeMode = "LRNG";
        private const string msgWarmupTime = "WUTM";

        private const string msgReadLogs = "RLOG";
        private const string msgLogAllReadings = "LALR";
        private const string msgLogCount = "LCNT";

        private const int MAX_PARAMETERS_PER_MESSAGE = 32;
        private const int MAX_PARAMETER_ID_TEXT_LENGTH = 16;
        private const int MAX_PARAMETER_VAL_TEXT_LENGTH = 32;

        // For the following value, we expect a max of 256 3-byte sequences (" XX"):
        private const int MAX_UNPARSED_STRING_LENGTH = (256 * 3);

        private const int VALUE_TYPE_TEXT = 0;
        private const int VALUE_TYPE_INTEGER = 1;
        private const int VALUE_TYPE_FLOAT = 2;

        private const int FLASH_BLOCK_READ_TIMEOUT = 1000;
        private const byte MAX_CMD_LENGTH_BYTES = 255;
        private const int CMD_START_TIMEOUT_MS = 300;

        #endregion Define Constants

        public event EventHandler<CommandErrorEventArgs> CommandError;
        public event EventHandler<ErrorEventArgs> Error;
        public event EventHandler<UpdateFirmwareEventArgs> UpdateFirmwareProgress;
        public event EventHandler<GetLogsProgressEventArgs> GetLogsProgress;
        public event EventHandler WriteFlashComplete;

        public event EventHandler<string> MessageReceived;

        private bool _firmwareUpdateInProgress = false;

        public string User { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string Application { get; set; } = string.Empty;

        private void OnWriteFlashComplete()
        {
            var handler = WriteFlashComplete;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public event EventHandler ReadFlashComplete;

        private void OnReadFlashComplete()
        {
            var handler = ReadFlashComplete;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public event EventHandler<WriteFlashProgressEventArgs> WriteFlashProgress;

        private void OnWriteFlashProgress(int progress)
        {
            WriteFlashProgress?.Invoke(this, new WriteFlashProgressEventArgs(progress));
        }

        public event EventHandler<ReadFlashProgressEventArgs> ReadFlashProgress;

        private void OnReadFlashProgress(int progress)
        {
            var handler = ReadFlashProgress;
            if (handler != null) handler(this, new ReadFlashProgressEventArgs(progress));
        }

        private void OnError(ErrorEventArgs e)
        {
            Error?.Invoke(this, e);
        }

        public event EventHandler<DataPolledEventArgs> DataPolled;

        private readonly IInputStream _inputStream;
        private readonly IOutputStream _outputStream;
        private readonly IFileManager _fileManager;
        
        private bool _isLoggingConfigured;

        public int PeriodicInterval { get; private set; } = 100;
        private int _loggingInterval = 500;

        private string _batterySerial = "";
        
        public string LogFilePath { get; private set; }
        public string DebugLogFilePath { get; private set; }
        private ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();

        public Dictionary<string, string> CurrentStatus
        {
            get { return lockedStatus.ToDictionary(entry => entry.Key, entry => entry.Value); }
        }

        private ConcurrentDictionary<string, string> lockedStatus { get; } = new ConcurrentDictionary<string, string>();

        public string Name { get; set; }
        public bool IsRunning { get; set; }
        public string Status { get; set; }
        public int UseAvgPerc { get; set; }
        public int LongAverageCount { get; set; }
        public int ShortAverageCount { get; set; }
        public int AverageCutoffPpm { get; set; }

        public bool BatteryReportingEnabled { get; private set; }
        public bool ReadingsReportingEnabled { get; private set; }
        public bool DriveLevelsReportingEnabled { get; private set; }
        public bool FidReadingsReportingEnabled { get; private set; }

        public bool IsPolling => BatteryReportingEnabled | ReadingsReportingEnabled | DriveLevelsReportingEnabled | FidReadingsReportingEnabled;

        private Action loggingAction;

        private ITimeMongerService _timeMongerService;

        private DateTime Now => _timeMongerService?.Now ?? DateTime.Now;

        private readonly List<string> _messageTypes = new List<string>();

        public Phx42(IInputStream inputStream, IOutputStream outputStream, IFileManager fileManager, string name,
            ITimeMongerService monger = null, string application = "", string user = "", string site = "")
        {
            Name = name;
            _inputStream = inputStream;

            _outputStream = outputStream;
            _fileManager = fileManager;
            _timeMongerService = monger;
            Application = application;
            User = user;
            Site = site;

            ConfigureLogging();

            UseAvgPerc = 10;
            LongAverageCount = 25;
            ShortAverageCount = 5;
            AverageCutoffPpm = 40;

            var msgFields = typeof(Phx42).GetRuntimeFields()
                .Where(f => f.IsStatic && f.IsPrivate && f.Name.StartsWith("msg"));

            foreach (var fieldInfo in msgFields)
            {
                _messageTypes.Add(fieldInfo.GetValue(null).ToString());
            }

            lockedStatus.TryAdd(Phx42PropNames.BatteryCharge, "0");
            lockedStatus.TryAdd(Phx42PropNames.BatteryStatus, "Not Working");
            lockedStatus.TryAdd(Phx42PropNames.PPM, "0");

            InitPollingAndLoggingActions();
            StartMessageHandler();

            if (_timeMongerService != null)
            {
                SetTime(_timeMongerService.Now);
            }

            try
            {
                CommMessage message = new CommMessage();
                message.MsgType = msgBatteryStatus;

                message.Parameters["PCT"] = 1;
                message.Parameters["VOLTS"] = 1;
                message.Parameters["AMPS"] = 1;
                message.Parameters["SER"] = 1;

                SendOutgoingMessage(message);
            }
            catch (Exception ex)
            {
                WriteExceptionToPhxLog(ex);
            }

            EnablePeriodicReadings(msgReadings, false);
            EnablePeriodicReadings(msgDriveLevels, false);
            EnablePeriodicReadings(msgFIDReadings, false);
            EnablePeriodicReadings(msgBatteryStatus, false);

            LoggingTimer = new Timer(LoggingTimerCallback, null, 0, 1000);
        }

        public bool ShutdownNow { get; set; }

        private Task messageThread = null;

        private DateTime _lastMessageReceived = DateTime.Now;

        private int messageThreadLastLine = 0;

        private string messageThreadLastMethod = "";

        private int? messageThreadTaskId = null;

        private void PrintTrace(string message = null, [CallerMemberName] string callingMethod = "")
        {
            WriteToLog(string.IsNullOrEmpty(message)
                ? $"Entered {callingMethod}"
                : $"Entered {callingMethod} - {message}");
        }

        private void Trace([CallerMemberName] string callingMethod = "",
            [CallerLineNumber] int callingFileLineNumber = 0)
        {
            messageThreadLastLine = callingFileLineNumber;
            messageThreadLastMethod = callingMethod;
            messageThreadTaskId = Task.CurrentId;
        }

        private void StartMessageHandler()
        {
            if (messageThread != null && messageThread.Status == TaskStatus.Running) return;

            _lastMessageReceived = Now;

            messageThread = new Task(() =>
            {
                int errorcount = 0;

                _lastMessageReceived = Now;

                while (!ShutdownNow && !_firmwareUpdateInProgress)
                {
                    if (Task.CurrentId != messageThread.Id)
                    {
                        WriteToLog("Old message thread shutting down");
                        return;
                    }
                    try
                    {
                        Trace();
                        var incomingMessage = ReadIncomingMessage();

                        _lastMessageReceived = Now;

                        MessageReceived?.Invoke(null, incomingMessage.RawResponse);

                        if (incomingMessage.MsgType == msgCommCheck)
                        {
                            WriteToLog("Received CHEK message");
                        }
                        else if (incomingMessage.MsgType == msgSystemShutdownReport)
                        {
                            WriteToLog(
                                $"Received {incomingMessage.MsgType} message - {incomingMessage.UnparsedString}");

                            var localMessage = incomingMessage;

                            Task.Run(() =>
                            {

                                try
                                {
                                    OnCommandError(CommandErrorType.Shutdown, $"Error: {localMessage.UnparsedString}");
                                }
                                catch (Exception ex)
                                {
                                    WriteToLog($"ERROR: problem with OnCommandError Shutdown: {ex.Message}");
                                    WriteExceptionToPhxLog(ex);
                                }
                            });

                        }
                        else if (incomingMessage.MsgType == msgErrorReport || incomingMessage.MsgType== msgSpontaneousErrorReport)
                        {
                            WriteToLog($"ERROR: {incomingMessage} - {incomingMessage.UnparsedString}");

                            string type = incomingMessage.Parameters.ContainsKey("TYPE")
                                ? incomingMessage.Parameters["TYPE"].ToString()
                                : null;

                            int code = incomingMessage.Parameters.ContainsKey("CODE")
                                ? int.Parse(incomingMessage.Parameters["CODE"].ToString())
                                : -1;

                            if (!string.IsNullOrWhiteSpace(type))
                            {
                                Trace();
                                errors[type] = new KeyValuePair<DateTime, int>(Now, code);
                            }

                            var localMessage = incomingMessage;

                            Task.Run(() =>
                            {
                                try
                                {
                                    OnCommandError(CommandErrorType.Message, $"Error: {GetErrorMessage(localMessage)}");
                                }
                                catch (Exception ex)
                                {
                                    WriteToLog($"ERROR: problem with OnCommandError: {ex.Message}");
                                    WriteExceptionToPhxLog(ex);
                                }
                            });

                            if (type == msgStartAutoIgnitionSequence)
                            {
                                Task.Run(() =>
                                {
                                    try
                                    {
                                        OnCommandError(CommandErrorType.AutoIgnitionSequence, $"Error: {localMessage}");
                                    }
                                    catch (Exception ex)
                                    {
                                        WriteToLog(
                                            $"ERROR: problem with OnCommandError AutoIgnitionSequence: {ex.Message}");
                                        WriteExceptionToPhxLog(ex);
                                    }
                                });

                            }
                        }
                        else
                        {
                            if (incomingMessage.MsgType == msgReadings || incomingMessage.MsgType == msgDriveLevels ||
                                incomingMessage.MsgType == msgFIDReadings ||
                                incomingMessage.MsgType == msgBatteryStatus)
                            {
                                ParseReadings(incomingMessage);

                                var localMessage = incomingMessage;

                                Task.Run(() =>
                                {
                                    if (localMessage.MsgType == msgFIDReadings ||
                                        (!FidReadingsReportingEnabled && localMessage.MsgType == msgReadings) ||
                                        (!FidReadingsReportingEnabled && !ReadingsReportingEnabled &&
                                         localMessage.MsgType == msgDriveLevels) ||
                                        (!FidReadingsReportingEnabled && !ReadingsReportingEnabled && !DriveLevelsReportingEnabled &&
                                         localMessage.MsgType == msgBatteryStatus))
                                    {
                                        try
                                        {
                                            var status = lockedStatus.ToDictionary(entry => entry.Key,
                                                entry => entry.Value);

                                            float ppm;
                                            if (!float.TryParse(status["PPM"], out ppm))
                                            {
                                                ppm = 0.0F;
                                            }

                                            OnDataPolled(new DataPolledEventArgs(status, ppm));
                                        }
                                        catch (Exception ex)
                                        {
                                            WriteToLog($"ERROR: problem preparing polled data: {ex.Message}");
                                            WriteExceptionToPhxLog(ex);
                                        }
                                    }
                                });
                            }
                            else
                            {
                                WriteToLog($"Received {incomingMessage.MsgType} message");
                            }

                            Trace();
                            if (incomingMessage.MsgType == msgReadLogs)
                            {
                                _receivedLogMessages.Enqueue(incomingMessage);
                            }
                            else
                            {
                                receivedMessages.Add(incomingMessage);
                            }
                        }

                        Task.Delay(10).Wait(10);

                        errorcount = 0;
                    }
                    catch (Exception ex)
                    {

                        WriteToLog($"ERROR: Message thread error #{errorcount}");

                        WriteExceptionToPhxLog(ex);

                        errorcount++;

                        Task.Run(() => { OnError(new ErrorEventArgs(ex)); });

                        if (errorcount > 10)
                        {
                            WriteToLog("ERROR: Message thread shutting down because of errors");

                            Task.Run(() => { OnError(new ErrorEventArgs(new ReconnectNeededException())); });

                            ShutdownNow = true;

                            return;
                        }
                    }
                }

                WriteToLog("Message thread shutting down");

            });

            messageThread.Start();
            WriteToLog("Message thread started");
        }

        private string GetErrorMessage(CommMessage incomingMessage)
        {
            incomingMessage.Parameters.TryGetValue("CODE", out var code);

            var codeStr = code.ToString();

            switch (codeStr)
            {
                case "5":
                    return "Too many calibration points. What are you using me for?";
                case "18":
                    return  "This application failed to set date and time. I really like to know at least when I am!" ;
                case "19":
                    return "This calibration cannot be deleted. Contact LDARtools Support.";
                case "20":
                    return "This calibration doesn't make sense. I'm reading this pA lower than the last gas you applied. Check your gases and retry calibration.";
                case "21":
                    int time = GetWarmupTime();
                    return $"No can do. I need to warm up for at least {time} seconds. Depending on your application, you may need to warm me up for longer than this requirement.";
                case "22":
                    return "I can't run on H2 this low! Feed ME!";
                case "24":
                    return "Remove probe tip filter, wait 5 seconds and reinstall same filter.";
            }

            return incomingMessage.ToString();
        }
        
        private DateTime lastParseTime;

        private void ParseReadings(CommMessage readings)
        {
            foreach (var parameter in readings.Parameters)
            {
                try
                {
                    switch (parameter.Key)
                    {
                        case "H2HP":
                            lockedStatus[Phx42PropNames.HPH2] = parameter.Value.ToString();
                            break;
                        case "H2LP":
                            lockedStatus[Phx42PropNames.LPH2] = parameter.Value.ToString();
                            break;
                        case "CHMBR":
                            lockedStatus[Phx42PropNames.InternalTemp] = parameter.Value.ToString();
                            break;
                        case "VOLTS":
                            lockedStatus[Phx42PropNames.Volts] = parameter.Value.ToString();
                            break;
                        case "P1OUT":
                            lockedStatus[Phx42PropNames.SamplePressure] = parameter.Value.ToString();
                            break;
                        case "P2OUT":
                            lockedStatus[Phx42PropNames.CombustionPressure] = parameter.Value.ToString();
                            break;
                        case "P2IN":
                            lockedStatus[Phx42PropNames.Vacuum] = parameter.Value.ToString();
                            break;
                        case "CASE":
                            lockedStatus["CaseTemp"] = parameter.Value.ToString();
                            break;
                        case "AMB":
                            lockedStatus[Phx42PropNames.ExternalTemp] = parameter.Value.ToString();
                            break;
                        case "AMPS":
                            if (readings.MsgType == msgBatteryStatus)
                            {
                                var amps = double.Parse(parameter.Value.ToString());
                                lockedStatus["BatteryStatus"] = amps > 0 ? "Charging" : "Discharging";
                            }
                            else
                            {
                                lockedStatus[Phx42PropNames.Current] =
                                    (decimal.Parse(parameter.Value.ToString())).ToString();
                            }

                            break;
                        case "NDLV":
                            lockedStatus[Phx42PropNames.NeedleValve] =
                                (decimal.Parse(parameter.Value.ToString())).ToString();
                            break;
                        case "P1DRV":
                            lockedStatus[Phx42PropNames.SamplePpl] =
                                Math.Round(float.Parse(parameter.Value.ToString()), 3).ToString();
                            break;
                        case "HTR":
                            lockedStatus[Phx42PropNames.Heater] =
                                (decimal.Parse(parameter.Value.ToString())).ToString();
                            break;
                        case "GPLG":
                            lockedStatus[Phx42PropNames.GlowPlug] =
                                (decimal.Parse(parameter.Value.ToString())).ToString();
                            break;
                        case "SOL":
                            lockedStatus[Phx42PropNames.Solenoid] =
                                (decimal.Parse(parameter.Value.ToString())).ToString();
                            break;
                        case "P2DRV":
                            lockedStatus[Phx42PropNames.CombustionPpl] =
                                Math.Round(float.Parse(parameter.Value.ToString()), 3).ToString();
                            break;
                        case "CALPPM":
                            lockedStatus["PPM"] = parameter.Value.ToString();

                            LastPpms.Add(decimal.Parse(lockedStatus["PPM"]));

                            while (LastPpms.Count > 250)
                            {
                                LastPpms.RemoveAt(0);
                            }

                            break;
                        case "PA":
                            lockedStatus[Phx42PropNames.PicoAamps] = parameter.Value.ToString();
                            break;
                        case "PAADJ":
                            lockedStatus[Phx42PropNames.PaOffset] = parameter.Value.ToString();
                            break;
                        case "PCT":
                            lockedStatus[Phx42PropNames.BatteryCharge] = parameter.Value.ToString();
                            break;
                        case "P1TGT":
                            lockedStatus[Phx42PropNames.P1Target] =
                                (decimal.Parse(parameter.Value.ToString())).ToString();
                            break;
                        case "P2TGT":
                            lockedStatus[Phx42PropNames.P2Target] =
                                (decimal.Parse(parameter.Value.ToString())).ToString();
                            break;
                        case "H2TGT":
                            lockedStatus[Phx42PropNames.H2Target] =
                                (decimal.Parse(parameter.Value.ToString())).ToString();
                            break;
                        case "ALT":
                            lockedStatus[Phx42PropNames.Altimeter] =
                                (decimal.Parse(parameter.Value.ToString())).ToString();
                            break;

                    }
                }
                catch (Exception ex)
                {
                    WriteExceptionToPhxLog(ex);
                    WriteToLog($"Troublesome message {readings}");
                }
            }

            if (readings.MsgType == msgBatteryStatus && !string.IsNullOrWhiteSpace(readings.UnparsedString))
            {
                _batterySerial = readings.UnparsedString;
            }

            if (readings.Parameters.ContainsKey("CALPPM"))
            {
                if (lockedStatus[Phx42PropNames.PPM].StartsWith("-"))
                {
                    IsRunning = false;
                    lockedStatus["IsIgnited"] = bool.FalseString;
                }
                else
                {
                    IsRunning = true;
                    lockedStatus["IsIgnited"] = bool.TrueString;
                }
            }

            if (readings.MsgType == msgReadings)
            {
                WriteToLog($"Received {readings.MsgType} message, current at {lockedStatus[Phx42PropNames.Current]}");
            }
            else if (readings.MsgType == msgFIDReadings)
            {
                WriteToLog(
                    $"Received {readings.MsgType} message, ppm at {lockedStatus[Phx42PropNames.PPM]}, pa at {lockedStatus[Phx42PropNames.PicoAamps]}");
            }
            else
            {
                WriteToLog($"Received {readings.MsgType} message");
            }

            try
            {
                lockedStatus["Timestamp"] = Now.ToString("HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

                if (readings.MsgType == msgBatteryStatus)
                {
                    if (double.Parse(lockedStatus[Phx42PropNames.BatteryCharge]) >= 100)
                        lockedStatus["BatteryStatus"] = "Charged";
                }
            }
            catch (Exception ex)
            {
                WriteExceptionToPhxLog(ex);
            }

            lastParseTime = Now;
        }

        public Dictionary<string, string> GetData()
        {
            if (!IsPolling)
            {
                GetReadings();
                GetDriveLevels();
                GetFIDReadings();
            }

            return CurrentStatus;
        }

        private bool firstTime = true;
        private bool isInPollingAction = false;

        private void InitPollingAndLoggingActions()
        {
            loggingAction = () =>
            {
                if (ShutdownNow)
                {
                    LoggingTimer?.Dispose();
                }

                if (CurrentStatus == null)
                    return;

                try
                {
                    string line = GetLineForLog(CurrentStatus);

                    try
                    {
                        if (firstTime)
                        {
                            _fileManager.AppendToFile(LogFilePath,
                                "Log Time,Received Time,picoamps,ppm,pa offset,sample pressure,sample PPL,combustion pressure,combustion PPL,lph2,hph2,vac,internal temp.,external temp.,case temp.,needle valve,heater,glow plug,solenoid,battery status,battery charge,current,is ignited,p1 tgt,p2 tgt,h2 tgt,altimeter,volts,reporting status,message");
                            firstTime = false;
                        }

                        _fileManager.AppendToFile(LogFilePath, $"{Now.ToString("MM/dd/yyyy HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)},{line}");
                    }
                    catch (IOException ex)
                    {
                        WriteToLog("Could not write log contents");
                        WriteExceptionToPhxLog(ex);
                    }
                }
                catch (Exception)
                {
                    // we can't really log it now, can we?
                }

            };
        }

        private void OnDataPolled(DataPolledEventArgs dataPolledEventArgs)
        {
            DataPolled?.Invoke(this, dataPolledEventArgs);
        }


        public void UseScheduler(RepeatedTaskScheduler scheduler)
        {
            //this.scheduler = scheduler;
        }

        public void StartPollingData()
        {
            StartPollingData(200);
        }

        public void StartPollingData(int intervalInMilliseconds, bool pollBattery = true, bool pollReadings = true,
            bool pollDriveLevels = true, bool pollFIDReadings = true)
        {
            if (_isLoggingConfigured != true)
            {
                throw new Exception("Logging is not configured.  Please call ConfigureLogging before polling data.");
            }

            SetPeriodicReportingInterval(intervalInMilliseconds);
            Task.Delay(200).Wait(200);

            if (pollReadings)
            {
                EnablePeriodicReadings(msgReadings, true);
                ReadingsReportingEnabled = true;
            }

            if (pollDriveLevels)
            {
                EnablePeriodicReadings(msgDriveLevels, true);
                DriveLevelsReportingEnabled = true;
            }

            if (pollFIDReadings)
            {
                EnablePeriodicReadings(msgFIDReadings, true);
                FidReadingsReportingEnabled = true;
            }

            if (pollBattery)
            {
                EnablePeriodicReadings(msgBatteryStatus, true);
                BatteryReportingEnabled = true;
            }
        }

        public void StopPeriodicReporting(bool battery = false, bool readings = false, bool driveLevels = false,
            bool fidReadings = false)
        {
            if (readings)
            {
                EnablePeriodicReadings(msgReadings, false);
                ReadingsReportingEnabled = false;
            }

            if (driveLevels)
            {
                EnablePeriodicReadings(msgDriveLevels, false);
                DriveLevelsReportingEnabled = false;
            }

            if (fidReadings)
            {
                EnablePeriodicReadings(msgFIDReadings, false);
                FidReadingsReportingEnabled = false;
            }

            if (battery)
            {
                EnablePeriodicReadings(msgBatteryStatus, false);
                BatteryReportingEnabled = false;
            }
        }

        public void StartPeriodicReporting(bool battery = false, bool readings = false, bool driveLevels = false,
            bool fidReadings = false)
        {
            if (readings)
            {
                EnablePeriodicReadings(msgReadings, true);
                ReadingsReportingEnabled = true;
            }

            if (driveLevels)
            {
                EnablePeriodicReadings(msgDriveLevels, true);
                DriveLevelsReportingEnabled = true;
            }

            if (fidReadings)
            {
                EnablePeriodicReadings(msgFIDReadings, true);
                FidReadingsReportingEnabled = true;
            }

            if (battery)
            {
                EnablePeriodicReadings(msgBatteryStatus, true);
                BatteryReportingEnabled = true;
            }
        }

        private void LoggingTimerCallback(object stateInfo)
        {
            loggingAction();
        }

        public void StopPollingSpecificData(string dataType)
        {
            EnablePeriodicReadings(dataType, false);

            if(dataType == "BATS")
                BatteryReportingEnabled = false;
        }

        public void StopPollingData()
        {
            EnablePeriodicReadings(msgReadings, false);
            EnablePeriodicReadings(msgDriveLevels, false);
            EnablePeriodicReadings(msgFIDReadings, false);
            EnablePeriodicReadings(msgBatteryStatus, false);

            ReadingsReportingEnabled = false;
            BatteryReportingEnabled = false;
            DriveLevelsReportingEnabled = false;
            FidReadingsReportingEnabled = false;

            while (isInPollingAction)
                Task.Delay(10).Wait(10);
        }

        public void ConfigureLogging()
        {
            ConfigureLogging(LoggingInterval);
        }

        public void ConfigureLogging(int intervalInMilliseconds)
        {
            ConfigureLogging(_fileManager.LogDirectory, intervalInMilliseconds);
        }

        public void ConfigureLogging(string loggingDirectory)
        {
            ConfigureLogging(loggingDirectory, LoggingInterval);
        }

        public string LoggingDirectory { get; set; }

        public int LoggingInterval
        {
            get { return _loggingInterval; }
            set { _loggingInterval = value; }
        }


        private DateTime lastDiagnosticLog;
        private DateTime _lastCheckSent;

        public void ConfigureLogging(string loggingDirectory, int intervalInMilliseconds)
        {
            _isLoggingConfigured = true;
            LoggingDirectory = loggingDirectory;

            string newFilePath = Path.Combine(loggingDirectory, GetFileName());
            string newDebugFilePath = Path.Combine(loggingDirectory, GetStatusFileName());

            CreateDirectory(loggingDirectory);
            CreateFile(newFilePath);
            CreateFile(newDebugFilePath);

            LogFilePath = newFilePath;
            DebugLogFilePath = newDebugFilePath;
            LoggingInterval = intervalInMilliseconds;

            lastDiagnosticLog = Now;
            _lastCheckSent = Now;

            if (logWriteTimer == null)
            {
                logWriteTimer = new Timer(state =>
                {
                    if (ShutdownNow)
                    {
                        logWriteTimer.Dispose();
                        logWriteTimer = null;
                    }

                    if (inWriteLog) return;

                    try
                    {

                        try
                        {
                            inWriteLog = true;
                            WriteLogQueue();
                        }
                        catch (Exception ex)
                        {
                            WriteToLog("Could not write log contents!!!! Requeueing messages");
                            WriteExceptionToPhxLog(ex);
                        }

                        var now = Now;

                        if ((now - lastDiagnosticLog).TotalSeconds > 5)
                        {
                            WriteToLog(
                                $"Running for {_inputStream.ConnectedTime}, sent {_outputStream.SendByteCount} b, received {_inputStream.ReceiveByteCount} b, memory {GC.GetTotalMemory(false)} b");
                            lastDiagnosticLog = Now;
                        }

                        if (IsPolling && (now - lastParseTime).TotalMilliseconds > PeriodicInterval + 2000)
                        {
                            WriteToLog(
                                $"ERROR: Last parse time was {(now - lastParseTime)} ago!!! Something's not right!");
                        }

                        if ((now - _lastMessageReceived).TotalSeconds > 3 && !_firmwareUpdateInProgress)
                        {
                            WriteToLog(
                                $"ERROR: Last message time was {(now - _lastMessageReceived)} ago!!! Something's not right! {messageThreadLastLine} - {messageThreadTaskId} - {messageThread?.Status}");

                            if (messageThread != null && (messageThread.Status != TaskStatus.Running && messageThread.Status != TaskStatus.WaitingToRun) && !ShutdownNow)
                            {
                                WriteToLog("ERROR: Attempting to restart message handler");
                                StartMessageHandler();
                            }

                            if ((now - _lastMessageReceived).TotalSeconds > 30 && !ShutdownNow)
                            {
                                WriteToLog($"ERROR: No messages received for {(now - _lastMessageReceived).TotalSeconds} seconds. Assuming disconnected. Shutting down.");

                                ShutdownNow = true;

                                OnError(new ErrorEventArgs(new ReconnectNeededException()));
                            }
                        }

                        try
                        {
                            if ((now - _lastCheckSent).TotalMilliseconds > 900 && !_firmwareUpdateInProgress)
                            {
                                var message = new CommMessage { MsgType = msgCommCheck };
                                SendOutgoingMessage(message);
                                _lastCheckSent = Now;
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteToLog($"Could not send check: {ex.Message}");
                        }
                    }
                    finally
                    {
                        inWriteLog = false;
                    }
                }, null, 250, 250);
            }
        }

        private string lastSetMessage = null;

        public void WriteToPhxLog(string contents)
        {
            lastSetMessage = contents;
            WriteToLog(contents);
        }

        private void WriteToLog(string contents)
        {
            logQueue.Enqueue($"~{Now.ToString("yyyy-MM-dd HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)}\t\t{User}\t{Site}\t{contents}");
        }

        private void WriteLogQueue()
        {
            List<string> messages = new List<string>();

            while (!logQueue.IsEmpty)
            {
                string item;
                if (logQueue.TryDequeue(out item))
                {
                    messages.Add(item);
                }
                else
                {
                    break;
                }
            }

            if (!messages.Any()) return;

            string batch = string.Join("\n", messages);

            try
            {
                _fileManager.AppendToFile(DebugLogFilePath, batch);
            }
            catch (IOException ex)
            {
                WriteToLog("Could not write log contents!!!! Requeueing messages");
                WriteExceptionToPhxLog(ex);

                logQueue.Enqueue(batch);
            }
        }

        private string GetValueOrDefault(Dictionary<string, string> status, string key, string defaultValue = "N/A")
        {
            if (status.ContainsKey(key))
            {
                return status[key];
            }
            else
            {
                return defaultValue;
            }
        }

        private string GetLineForLog(Dictionary<string, string> status)
        {
            List<string> lines = new List<string>();


            lines.Add(GetValueOrDefault(status, Phx42PropNames.Timestamp));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.PicoAamps));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.PPM));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.PaOffset));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.SamplePressure));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.SamplePpl));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.CombustionPressure));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.CombustionPpl));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.LPH2));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.HPH2));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.Vacuum));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.InternalTemp));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.ExternalTemp));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.CaseTemp));

            lines.Add(GetValueOrDefault(status, Phx42PropNames.NeedleValve));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.Heater));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.GlowPlug));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.Solenoid));

            lines.Add(GetValueOrDefault(status, Phx42PropNames.BatteryStatus));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.BatteryCharge));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.Current));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.IsIgnited));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.P1Target));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.P2Target));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.H2Target));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.Altimeter));
            lines.Add(GetValueOrDefault(status, Phx42PropNames.Volts));

            if (!IsPolling)
            {
                lines.Add("Periodic Not Enabled");
            }
            else
            {
                List<string> parts = new List<string>();

                if (BatteryReportingEnabled) parts.Add("Battery");
                if (DriveLevelsReportingEnabled) parts.Add("Drive Levels");
                if (FidReadingsReportingEnabled) parts.Add("FID");
                if (ReadingsReportingEnabled) parts.Add("Readings");

                lines.Add($"Enabled[{PeriodicInterval}]: {string.Join(" ", parts)}");
            }

            lines.Add(lastSetMessage);

            lastSetMessage = string.Empty;

            return string.Join(",", lines);
        }

        private void CreateFile(string filePath)
        {
            if (!_fileManager.FileExists(filePath))
            {
                _fileManager.CreateFile(filePath);
            }
        }

        private void CreateDirectory(string loggingDirectory)
        {
            if (!_fileManager.DirectoryExists(loggingDirectory))
            {
                _fileManager.CreateDirectory(loggingDirectory);
            }
        }

        private string GetFileName()
        {
            return $"{Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}_{Name}_{Application}Readings.csv";
        }

        private string GetStatusFileName()
        {
            return $"{Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}_{Name}_{Application}Status.log";
        }

        private CommMessage SendAndReceive(CommMessage commMessage, int timeout = 2000)
        {
            try
            {
                DateTime sendTime = Now;
                SendOutgoingMessage(commMessage);


                string messageResponseType = commMessage.MsgType;

                if (commMessage.MsgType == msgRequestSingleReport)
                {
                    messageResponseType = commMessage.Parameters["TYPE"].ToString();
                }
                else if (commMessage.MsgType == msgGenerateCalTableEntry ||
                         commMessage.MsgType == msgDeleteCalTableEntry)
                {
                    messageResponseType = msgCalibrationTable;
                }

                Stopwatch timer = Stopwatch.StartNew();

                while (timer.ElapsedMilliseconds < timeout)
                {
                    CommMessage response = null;

                    {
                        response = receivedMessages.FirstOrDefault(m => m.MsgType == messageResponseType && !m.Handled);
                        if (response != null) response.Handled = true;


                        if (response != null) return response;

                        if (errors.ContainsKey(messageResponseType))
                        {
                            if (errors[messageResponseType].Key > sendTime)
                            {
                                string message =
                                    $"ERROR: received error message from {Name} for message {messageResponseType}: code {errors[messageResponseType].Value}";
                                WriteToLog(message);
                                timer.Stop();
                                throw new Exception(message);
                            }
                        }

                        Task.Delay(20).Wait(20);
                    }
                }

                timer.Stop();
                string timeoutMessage =
                    $"ERROR: {Name} Receive timed out after {timer.Elapsed} waiting for a {messageResponseType} message";
                WriteToLog(timeoutMessage);
                throw new Exception(timeoutMessage);
            }
            catch (Exception ex)
            {
                WriteExceptionToPhxLog(ex);
                throw ex;
            }
        }

        private CommMessage[] SendAndReceiveMultiple(CommMessage commMessage, int responseCount, int timeout = 20000)
        {
            try
            {
                List<CommMessage> commMessages = new List<CommMessage>();

                DateTime sendTime = Now;
                SendOutgoingMessage(commMessage);

                string messageResponseType = commMessage.MsgType;

                int count = 0;

                Stopwatch timer = Stopwatch.StartNew();

                while (timer.ElapsedMilliseconds < timeout)
                {
                    CommMessage response = null;
                    
                    response = receivedMessages.FirstOrDefault(m => m.MsgType == messageResponseType && !m.Handled);

                    if (response != null) response.Handled = true;

                    if (response != null)
                    {
                        commMessages.Add(response);
                        count++;

                        timer.Restart();

                        if (count == responseCount)
                            return commMessages.ToArray();

                        if (string.IsNullOrEmpty(response.UnparsedString))
                            return commMessages.ToArray();

                        if (response.Parameters.ContainsKey("PART") && response.Parameters["PART"].ToString() == "END")
                            return commMessages.ToArray();
                    }

                    if (errors.ContainsKey(messageResponseType))
                    {
                        if (errors[messageResponseType].Key > sendTime)
                        {
                            string message =
                                $"ERROR: received error message from phx42 for message {messageResponseType}";
                            WriteToLog(message);
                            timer.Stop();
                            throw new Exception(message);
                        }
                    }

                    Task.Delay(10).Wait(10);

                }

                timer.Stop();

                if (commMessages.Count > 0)
                    return commMessages.ToArray();

                string timeoutMessage =
                    $"ERROR: Receive timed out after {timer.Elapsed} waiting for a {messageResponseType} message";
                WriteToLog(timeoutMessage);
                throw new Exception(timeoutMessage);
            }
            catch (Exception ex)
            {
                WriteExceptionToPhxLog(ex);
                throw ex;
            }
        }

        private void EnablePeriodicReadings(string msgType, bool enable)
        {
            lastParseTime = Now;
            PrintTrace(message: $"{msgType} - {enable} - {PeriodicInterval}");
            CommMessage message = new CommMessage();
            message.MsgType = msgEnablePeriodicReports;

            message.Parameters["TYPE"] = msgType;
            message.Parameters["EN"] = enable ? "1" : "0";

            if (msgType == msgReadings)
            {
                message.Parameters["TGT"] = "1";
            }
            else if (msgType == msgFIDReadings)
            {
                message.Parameters["EXT"] = "1";
            }

            SendOutgoingMessage(message);
        }

        public float GetPpm()
        {
            PrintTrace();
            var fidReadings = GetFIDReadings();

            float.TryParse(fidReadings["PPM"], out var result);

            return result;
        }

        public void SetBluetoothName(string name)
        {
            PrintTrace();

            CommMessage message = new CommMessage();
            message.MsgType = msgSetBluetoothName;

            message.Parameters["NAME"] = name;

            SendOutgoingMessage(message);
        }

        public void SetWarmupTime(int seconds)
        {
            PrintTrace();

            CommMessage message = new CommMessage();
            message.MsgType = msgWarmupTime;

            message.Parameters["SEC"] = seconds;

            SendAndReceive(message);
        }

        public int GetWarmupTime()
        {
            PrintTrace();

            CommMessage message = new CommMessage();
            message.MsgType = msgWarmupTime;

            var response = SendAndReceive(message);

            return int.Parse(response.Parameters["SEC"].ToString());
        }

        public List<decimal> LastPpms = new List<decimal>();

        public Dictionary<string, string> GetFIDReadings()
        {
            CommMessage message = new CommMessage();
            message.MsgType = msgRequestSingleReport;

            message.Parameters["TYPE"] = msgFIDReadings;
            message.Parameters["EXT"] = "1";

            var response = SendAndReceive(message);

            Dictionary<string, string> properties = new Dictionary<string, string>();

            foreach (var parameter in response.Parameters)
            {
                switch (parameter.Key)
                {
                    case "CALPPM":
                        properties["PPM"] = parameter.Value.ToString();
                        LastPpms.Add(decimal.Parse(properties["PPM"]));
                        while (LastPpms.Count > 250)
                        {
                            LastPpms.RemoveAt(0);
                        }

                        break;
                    case "PA":
                        properties[Phx42PropNames.PicoAamps] = parameter.Value.ToString();
                        break;
                    case "PAADJ":
                        properties[Phx42PropNames.PaOffset] = parameter.Value.ToString();
                        break;
                }
            }

            return properties;
        }


        public BatteryInfo GetBatteryStatus()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgBatteryStatus;

            message.Parameters["PCT"] = 1;
            message.Parameters["VOLTS"] = 1;
            message.Parameters["AMPS"] = 1;
            message.Parameters["SER"] = 1;

            var response = SendAndReceive(message);

            BatteryInfo info = new BatteryInfo();

            foreach (var parameter in response.Parameters)
            {
                switch (parameter.Key)
                {
                    case "PCT":
                        info.Charge = double.Parse(parameter.Value.ToString());
                        break;
                    /* case "VOLTS":
                         break;*/
                    case "AMPS":
                        var amps = double.Parse(parameter.Value.ToString());
                        info.Status = amps > 0 ? "Charging" : "Discharging";
                        break;
                }
            }

            if (info.Charge >= 100)
                info.Status = "Charged";

            return info;
        }


        public string GetBatterySerial()
        {
            PrintTrace();

            if (!string.IsNullOrWhiteSpace(_batterySerial)) return _batterySerial;
            //return "batSer";
            CommMessage message = new CommMessage();
            message.MsgType = msgBatteryStatus;

            message.Parameters["PCT"] = 1;
            message.Parameters["VOLTS"] = 1;
            message.Parameters["AMPS"] = 1;
            message.Parameters["SER"] = 1;

            var response = SendAndReceive(message);

            int count = 0;

            while (string.IsNullOrWhiteSpace(_batterySerial) && count < 10)
            {
                count++;
                Task.Delay(100).Wait(100);
            }

            return _batterySerial;
        }

        /// <summary>
        /// Do you know what you're doing? If no then don't use this function
        /// </summary>
        /// <param name="command"></param>
        public void SendRawCommand(string command)
        {
            if (!command.EndsWith(endOfMessage))
            {
                command += endOfMessage;
            }

            Transmit(Encoding.UTF8.GetBytes(command));
        }

        public Dictionary<string, string> GetDriveLevels()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgRequestSingleReport;

            message.Parameters["TYPE"] = msgDriveLevels;

            var response = SendAndReceive(message, longTimeout);

            Dictionary<string, string> properties = new Dictionary<string, string>();

            foreach (var parameter in response.Parameters)
            {
                switch (parameter.Key)
                {
                    //Incomplete
                    case "P1DRV":
                        properties[Phx42PropNames.SamplePpl] =
                            Math.Round(float.Parse(parameter.Value.ToString()), 3).ToString();
                        break;
                    case "P2DRV":
                        properties[Phx42PropNames.CombustionPpl] =
                            Math.Round(float.Parse(parameter.Value.ToString()), 3).ToString();
                        break;
                    case "SOL":
                        properties[Phx42PropNames.Solenoid] = (decimal.Parse(parameter.Value.ToString())).ToString();
                        break;
                    case "NDLV":
                        properties[Phx42PropNames.NeedleValve] =
                            (decimal.Parse(parameter.Value.ToString())).ToString();
                        break;

                }
            }

            return properties;
        }

        public Dictionary<string, string> GetReadings()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgRequestSingleReport;

            message.Parameters["TYPE"] = msgReadings;

            message.Parameters["TGT"] = '1';

            var response = SendAndReceive(message);

            Dictionary<string, string> properties = new Dictionary<string, string>();

            foreach (var parameter in response.Parameters)
            {
                try
                {
                    switch (parameter.Key)
                    {
                        case "H2HP":
                            properties[Phx42PropNames.HPH2] = parameter.Value.ToString();
                            break;
                        case "H2LP":
                            properties[Phx42PropNames.LPH2] = parameter.Value.ToString();
                            break;
                        case "CHMBR":
                            properties[Phx42PropNames.InternalTemp] = parameter.Value.ToString();
                            break;
                        case "VOLTS":
                            properties[Phx42PropNames.Volts] = parameter.Value.ToString();
                            break;
                        case "P1OUT":
                            properties[Phx42PropNames.SamplePressure] = parameter.Value.ToString();
                            break;
                        case "P2OUT":
                            properties[Phx42PropNames.CombustionPressure] = parameter.Value.ToString();
                            break;
                        case "CASE":
                            properties["CaseTemp"] = parameter.Value.ToString();
                            break;
                        case "AMB":
                            properties[Phx42PropNames.ExternalTemp] = parameter.Value.ToString();
                            break;
                        case "AMPS":
                            properties[Phx42PropNames.Current] =
                                (decimal.Parse(parameter.Value.ToString()) * 1000).ToString();
                            break;
                        case "P1TGT":
                            properties[Phx42PropNames.P1Target] =
                                (decimal.Parse(parameter.Value.ToString())).ToString();
                            break;
                        case "P2TGT":
                            properties[Phx42PropNames.P2Target] =
                                (decimal.Parse(parameter.Value.ToString())).ToString();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    WriteExceptionToPhxLog(ex);
                }
            }

            return properties;
        }

        public void SetPeriodicReportingInterval(int milliseconds)
        {
            PrintTrace($"Interval is {milliseconds}");
            PeriodicInterval = milliseconds;
            CommMessage message = new CommMessage();
            message.MsgType = msgPeriodicReportinInterval;

            message.Parameters["MS"] = milliseconds.ToString();

            SendOutgoingMessage(message);

            Task.Delay(500).Wait(500);
        }

        public FIDFilteringParams SetFIDOutputFilterParams(double iir, int avg, int riseCount = -1, int riseDelta = -1)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetFidOutputFiltering;

            message.Parameters.Add("IIR", iir);
            message.Parameters.Add("AVG", avg);

            if (riseCount >= 0)
            {
                message.Parameters.Add("RCNT", riseCount);
            }

            if (riseDelta >= 0)
            {
                message.Parameters.Add("RDELTA", riseDelta);
            }

            var rcv = SendAndReceive(message, longTimeout);

            var ret = new FIDFilteringParams();

            ret.IIR = double.Parse(rcv.Parameters["IIR"].ToString());
            ret.Average = (int)double.Parse(rcv.Parameters["AVG"].ToString());

            if (rcv.Parameters.ContainsKey("RCNT"))
            {
                ret.RiseCount = (int)double.Parse(rcv.Parameters["RCNT"].ToString());
            }

            if (rcv.Parameters.ContainsKey("RDELTA"))
            {
                ret.RiseDelta = (int)double.Parse(rcv.Parameters["RDELTA"].ToString());
            }

            return ret;
        }

        public DriftInfo GetDriftInfo()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgDriftInfo;

            var rcv = SendAndReceive(message, longTimeout);

            var ret = new DriftInfo();

            ret.AdjZ = rcv.Parameters["ADJZ"].ToString() == "1";
            ret.AdjA = rcv.Parameters["ADJA"].ToString() == "1";
            ret.TSec = double.Parse(rcv.Parameters["TSEC"].ToString());
            ret.PaAdj = double.Parse(rcv.Parameters["PAADJ"].ToString());

            return ret;
        }

        public int GetLogCount()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgLogCount;

            var rcv = SendAndReceive(message, longTimeout);

            return int.Parse(rcv.Parameters["COUNT"].ToString());
        }

        public void SetDriftInfo(bool adjz, bool adja, double? timeNeg = null, double? paAdj = null)
        {
            PrintTrace($"adjz = {adjz}, adja = {adja}, timeNeg = {timeNeg}, paAdj = {paAdj}");
            CommMessage message = new CommMessage();
            message.MsgType = msgDriftInfo;

            message.Parameters.Add("ADJZ", adjz ? "1" : "0");
            message.Parameters.Add("ADJA", adja ? "1" : "0");

            if (timeNeg.HasValue)
            {
                message.Parameters.Add("TSEC", timeNeg.Value);
            }

            if (paAdj.HasValue)
            {
                message.Parameters.Add("PAADJ", paAdj.Value);
            }

            SendOutgoingMessage(message);
        }

        public DDC114Params SetDDC114Params(int? range, int? integusec, bool? subtraction, bool? testPin)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetDDC114Params;

            if(range != null)
                message.Parameters.Add("RANGE", range);

            if (integusec != null)
                message.Parameters.Add("INTEGUSEC", integusec);

            if(subtraction != null)
                message.Parameters.Add("SUBTRACTION", subtraction.Value ? 1 : 0);

            if(testPin != null)
                message.Parameters.Add("TESTPIN", testPin.Value ? 1 : 0);

            var rcv = SendAndReceive(message, longTimeout);

            var ret = new DDC114Params();

            ret.Range = (int)double.Parse(rcv.Parameters["RANGE"].ToString());
            ret.Integusec = (int)double.Parse(rcv.Parameters["INTEGUSEC"].ToString());
            ret.Subtraction = (int)double.Parse(rcv.Parameters["SUBTRACTION"].ToString()) == 1;
            ret.TestPin = (int)double.Parse(rcv.Parameters["TESTPIN"].ToString()) == 1;

            return ret;
        }

        public (string, bool) GetLogsFor(TimeSpan time, bool append, int maxLog = 500, int delayBetweenMessages = 300)
        {
            PrintTrace($"{time}, {append}, {maxLog}, {delayBetweenMessages}");

            Stopwatch watch = Stopwatch.StartNew();

            int totalCount = GetLogCount();

            string filename = Path.Combine(LoggingDirectory, $"{Now:yyyyMMdd}_{Name}_Firmware.log");

            bool finished = true;

            try
            {
                if (!append)
                {
                    try
                    {
                        _fileManager.DeleteFile(filename);
                    }
                    catch (Exception)
                    {
                        //ignore errors deleting the old file
                    }
                }

                var loopMax = (int)Math.Ceiling((double)totalCount / maxLog);

                WriteToLog($"There are {totalCount} log messages to retrieve, breaking that up into {loopMax} chunks of {maxLog}");

                for (int i = 0; i < loopMax; i++)
                {
                    if (watch.Elapsed > time)
                    {
                        finished = false;
                        break;
                    }

                    OnGetLogsProgress((decimal)i / (decimal)loopMax);

                    CommMessage message = new CommMessage();
                    message.MsgType = msgReadLogs;
                    message.Parameters.Add("COUNT", maxLog);
                    message.Parameters.Add("COMBINE", true);

                    WriteToLog($"Requesting log chunk {i + 1} of {loopMax} ({maxLog} per chunk)");

                    SendOutgoingMessage(message);

                    bool receivedEnd = false;

                    StringBuilder builder = new StringBuilder();

                    Stopwatch receiveTimer = Stopwatch.StartNew();
                    while (receiveTimer.Elapsed < TimeSpan.FromSeconds(2) && !receivedEnd)
                    {
                        while (_receivedLogMessages.Any())
                        {
                            if (_receivedLogMessages.TryDequeue(out var mess))
                            {
                                if (mess.Parameters.ContainsKey("PART") && mess.Parameters["PART"].ToString() == "END")
                                {
                                    receivedEnd = true;
                                    WriteToLog($"Received end of log chunk {i + 1}");
                                }

                                builder.AppendLine(mess.UnparsedString);
                            }
                            else
                            {
                                break;
                            }
                        }

                        Task.Delay(5).Wait(5);
                    }

                    if (!receivedEnd)
                    {
                        WriteToLog($"Timed out waiting for end of log chunk {i + 1}");
                    }

                    _fileManager.AppendToFile(filename, builder.ToString());

                    message = new CommMessage { MsgType = msgCommCheck };
                    SendOutgoingMessage(message);
                    _lastCheckSent = Now;

                    Task.Delay(delayBetweenMessages).Wait(delayBetweenMessages);
                }
            }
            catch (Exception ex)
            {
                WriteExceptionToPhxLog(ex);
                throw ex;
            }

            watch.Stop();

            OnGetLogsProgress(1);

            WriteToPhxLog($"Done with this go around of writing to {filename}.{(finished? " All done getting logs" : " Did not finish getting logs (time's up)")}");

            return (filename, finished);
        }

        public string GetLogs(int maxLog = 500, int delayBetweenMessages = 300)
        {
            PrintTrace();

            Stopwatch watch = Stopwatch.StartNew();

            int totalCount = 0;

            try
            {
                totalCount = GetLogCount();
            }
            catch (Exception)
            {
                return GetLogsTheOldWay();
            }

            string filename = Path.Combine(LoggingDirectory, $"{Now:yyyyMMdd}_{Name}_Firmware.log");

            try
            {
                try
                {
                    _fileManager.DeleteFile(filename);
                }
                catch (Exception)
                {
                    //ignore errors deleting the old file
                }

                var loopMax = (int)Math.Ceiling((double)totalCount / maxLog);

                for (int i = 0; i < loopMax; i++)
                {
                    OnGetLogsProgress((decimal)i / (decimal)loopMax);

                    CommMessage message = new CommMessage();
                    message.MsgType = msgReadLogs;
                    message.Parameters.Add("COUNT", maxLog);
                    message.Parameters.Add("COMBINE", true);

                    SendOutgoingMessage(message);

                    bool receivedEnd = false;

                    StringBuilder builder = new StringBuilder();

                    Stopwatch receiveTimer = Stopwatch.StartNew();
                    while (receiveTimer.Elapsed < TimeSpan.FromSeconds(2) && !receivedEnd)
                    {
                        while (_receivedLogMessages.Any())
                        {
                            if (_receivedLogMessages.TryDequeue(out var mess))
                            {
                                if (mess.Parameters.ContainsKey("PART") && mess.Parameters["PART"].ToString() == "END")
                                {
                                    receivedEnd = true;
                                }

                                builder.AppendLine(mess.UnparsedString);
                            }
                            else
                            {
                                break;
                            }
                        }

                        Task.Delay(5).Wait(5);
                    }

                    _fileManager.AppendToFile(filename, builder.ToString());

                    message = new CommMessage { MsgType = msgCommCheck };
                    SendOutgoingMessage(message);
                    _lastCheckSent = Now;

                    Task.Delay(delayBetweenMessages).Wait(delayBetweenMessages);
                }
            }
            catch (Exception ex)
            {
                WriteExceptionToPhxLog(ex);
            }

            watch.Stop();

            OnGetLogsProgress(1);

            return filename;
        }

        public string GetLogsTheOldWay()
        {
            PrintTrace();
            Stopwatch watch = Stopwatch.StartNew();
            
            int maxLog = 500;

            CommMessage lastMessage = null;


            //clear queue
            while (_receivedLogMessages.Any())
            {
                CommMessage m;
                _receivedLogMessages.TryDequeue(out m);
            }

            string filename = Path.Combine(LoggingDirectory, $"{Now:yyyyMMdd}_{Name}_Firmware.log");

            try
            {
                _fileManager.DeleteFile(filename);
            }
            catch (Exception)
            {
                //ignore errors deleting the old file
            }

            do
            {
                int receivedThisTime = 0;
                lastMessage = null;
                CommMessage message = new CommMessage();
                message.MsgType = msgReadLogs;
                message.Parameters.Add("COUNT", maxLog);

                SendOutgoingMessage(message);
                Stopwatch innerTimer = Stopwatch.StartNew();

                StringBuilder builder = new StringBuilder();

                while (receivedThisTime < maxLog && innerTimer.ElapsedMilliseconds < 2000)
                {
                    while (_receivedLogMessages.Any())
                    {
                        if (_receivedLogMessages.TryDequeue(out lastMessage))
                        {
                            if (string.IsNullOrWhiteSpace(lastMessage.UnparsedString))
                            {
                                break;
                            }

                            receivedThisTime++;
                            builder.AppendLine(lastMessage.UnparsedString);
                            //fileManager.AppendToFile(filename, lastMessage.UnparsedString);
                        }
                        else
                        {
                            break;
                        }
                    }

                    Task.Delay(10).Wait(10);
                }

                _fileManager.AppendToFile(filename, builder.ToString());

                message = new CommMessage { MsgType = msgCommCheck };
                SendOutgoingMessage(message);
                _lastCheckSent = Now;

                Task.Delay(300).Wait(300);
            } while (!string.IsNullOrWhiteSpace(lastMessage?.UnparsedString));

            watch.Stop();

            return filename;
        }

        public void LogAllReadingsNow()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgLogAllReadings;

            SendOutgoingMessage(message);
        }

        public void SetCaseThermConstants(int rp, int r25, int b)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetNtcConstantsCase;

            message.Parameters.Add("RP", rp);
            message.Parameters.Add("R25", r25);
            message.Parameters.Add("B", b);

            SendOutgoingMessage(message);
        }

        public void SetChamberThermConstants(int rp, int r25, int b)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetNTCConstantsChamber;

            message.Parameters.Add("RP", rp);
            message.Parameters.Add("R25", r25);
            message.Parameters.Add("B", b);

            SendOutgoingMessage(message);
        }

        public void SetPumpDrive(int pumpNum, double powerPercent)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetPumpDrive;
            message.Parameters.Add("P" + pumpNum, powerPercent * 0.01);

            SendOutgoingMessage(message);
        }

        public void SetLowRangeMode(bool enable)
        {
            PrintTrace();

            CommMessage message = new CommMessage();
            message.MsgType = msgSetLowRangeMode;

            message.Parameters.Add("EN", enable ? "1" : "0");

            SendOutgoingMessage(message);
        }

        public bool GetLowRangeMode()
        {
            PrintTrace();

            CommMessage message = new CommMessage();
            message.MsgType = msgSetLowRangeMode;

            var response = SendAndReceive(message);

            if (response.Parameters.ContainsKey("EN") && response.Parameters["EN"].ToString() == "1")
            {
                return true;
            }

            return false;
        }

        public string GetFirmwareVersion()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgFwVersion;

            var response = SendAndReceive(message);

            WriteToLog($"Firmware Version: {response.Parameters["MAJOR"]}.{response.Parameters["MINOR"]}");

            return $"{response.Parameters["MAJOR"]}.{response.Parameters["MINOR"]}";
        }

        public AutoIgnitionParameters GetAutoIgnitionParameters()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgAutoIgnitionParameters;

            var receivedMsg = SendAndReceive(message, longTimeout * 5);

            List<string> lines = new List<string>();

            lines.Add($"Received {receivedMsg.Parameters.Count} Auto ignition props");

            foreach (var key in receivedMsg.Parameters.Keys)
            {
                lines.Add($"{key} - {receivedMsg.Parameters[key]}");
            }

            WriteToLog(string.Join("\n\t", lines));

            AutoIgnitionParameters p = new AutoIgnitionParameters();

            p.SampleSetpoint = double.Parse(receivedMsg.Parameters["ASET"].ToString());
            p.CombustionSetpoint = double.Parse(receivedMsg.Parameters["BSET"].ToString());
            p.LPH2Setpoint = double.Parse(receivedMsg.Parameters["HSET"].ToString());
            p.PressureStableTimeout = double.Parse(receivedMsg.Parameters["PRTIME"].ToString());
            p.SampleStablePressureTol = double.Parse(receivedMsg.Parameters["ATOL"].ToString());
            p.CombustionStablePressureTol = double.Parse(receivedMsg.Parameters["BTOL"].ToString());
            p.LPH2StableTol = double.Parse(receivedMsg.Parameters["HTOL"].ToString());
            p.GlowPlugPowerLevel = double.Parse(receivedMsg.Parameters["GPPWR"].ToString());
            p.CombustionOffTime = double.Parse(receivedMsg.Parameters["BPAUSE"].ToString());
            p.GlowPlugDuration = double.Parse(receivedMsg.Parameters["GPDUR"].ToString());
            p.IgnitionTimeout = double.Parse(receivedMsg.Parameters["IGTIME"].ToString());
            p.MinTempRise = double.Parse(receivedMsg.Parameters["MINRISE"].ToString());
            p.CombustionSlope = double.Parse(receivedMsg.Parameters["BSLOPE"].ToString());

            return p;
        }

        public void SetAutoIgniteParameters(double aset, double bset, double hset, double prtime,
            double atol, double btol, double htol, double gppwr, double bpause, double gpdur,
            double igtime, double minrise, double bslope)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgAutoIgnitionParameters;
            message.Parameters.Add("ASET", String.Format("{0:0.00}", aset));
            message.Parameters.Add("BSET", String.Format("{0:0.00}", bset));
            message.Parameters.Add("HSET", String.Format("{0:0.0000}", hset));
            message.Parameters.Add("PRTIME", String.Format("{0:0.000}", prtime));
            message.Parameters.Add("ATOL", String.Format("{0:0.00}", atol));
            message.Parameters.Add("BTOL", String.Format("{0:0.00}", btol));
            message.Parameters.Add("HTOL", String.Format("{0:0.00}", htol));
            message.Parameters.Add("GPPWR", String.Format("{0:0.00}", gppwr));
            message.Parameters.Add("BPAUSE", String.Format("{0:0.0}", bpause));
            message.Parameters.Add("GPDUR", String.Format("{0:0.0}", gpdur));
            message.Parameters.Add("IGTIME", String.Format("{0:0.0}", igtime));
            message.Parameters.Add("MINRISE", String.Format("{0:0.0}", minrise));
            message.Parameters.Add("BSLOPE", String.Format("{0:0.00}", bslope));

            SendOutgoingMessage(message);
        }

        public void SetAutoIgniteParameters(double sampleSetpoint, double combustionSetpoint, double lph2Setpoint)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgAutoIgnitionParameters;
            message.Parameters.Add("ASET", String.Format("{0:0.00}", sampleSetpoint));
            message.Parameters.Add("BSET", String.Format("{0:0.00}", combustionSetpoint));
            message.Parameters.Add("HSET", String.Format("{0:0.0000}", lph2Setpoint));

            SendOutgoingMessage(message);
        }

        public void SetSamplePumpClosedLoopControl(bool enable)
        {
            PrintTrace($"enable {enable}");
            CommMessage message = new CommMessage();
            message.MsgType = msgSetPump1ClosedLoopCtrl;
            message.Parameters.Add("EN", enable ? 1 : 0);

            SendOutgoingMessage(message);
        }

        public void WriteToBatteryFuelGaugeChip(int address, string hexData)
        {
            PrintTrace();
            CommMessage message = new CommMessage();

            message.MsgType = msgWriteToBatteryFuelGaugeChip;
            message.Parameters.Add("SLVADR", "52");
            message.Parameters.Add("ADDR", address.ToString());
            message.Parameters.Add("COUNT", hexData.Length / 2);

            for (int i = 2; i < hexData.Length; i += 3)
            {
                hexData = hexData.Insert(i, " ");
            }

            message.UnparsedString = hexData;

            WriteToLog("Sending - " + message.ToString());
            var retMessage = SendAndReceive(message, longTimeout);
            WriteToLog("Received - " + retMessage.ToString());
        }

        public string ReadFromBatteryFuelGaugeChip(int address, int count)
        {
            PrintTrace();
            CommMessage message = new CommMessage();

            message.MsgType = msgReadFromBatteryFuelGaugeChip;
            message.Parameters.Add("SLVADR", "52");
            message.Parameters.Add("ADDR", address.ToString());
            message.Parameters.Add("COUNT", count);

            var retMsg = SendAndReceive(message, longTimeout);

            return retMsg.UnparsedString;
        }

        public ClosedLoopControlParams SetSamplePumpClosedLoopControl(bool? enable, double? target, int p, int i, int d,
            double zo, int ff)
        {
            PrintTrace($"enable {enable}, target {target}, p {p}, i {i}, d {d}, zo {zo}, ff {ff}");
            CommMessage message = new CommMessage();
            message.MsgType = msgSetPump1ClosedLoopCtrl;

            if(enable != null)
                message.Parameters.Add("EN", enable.Value ? 1 : 0);

            if (target != null)
                message.Parameters.Add("TGT", target);

            message.Parameters.Add("P", p);
            message.Parameters.Add("I", i);
            message.Parameters.Add("D", d);
            message.Parameters.Add("ZO", zo);
            message.Parameters.Add("FF", ff);

            var rcv = SendAndReceive(message, longTimeout * 3);

            return GetClcParamsFromMessage(rcv);
        }

        private static ClosedLoopControlParams GetClcParamsFromMessage(CommMessage message)
        {
            var ret = new ClosedLoopControlParams();

            if (message.Parameters["TGT"] != null)
            {
                ret.Target = double.Parse(message.Parameters["TGT"].ToString());
            }

            ret.P = (int)double.Parse(message.Parameters["P"].ToString());
            ret.I = (int)double.Parse(message.Parameters["I"].ToString());
            ret.D = (int)double.Parse(message.Parameters["D"].ToString());
            ret.ZO = double.Parse(message.Parameters["ZO"].ToString());
            ret.FF = (int)double.Parse(message.Parameters["FF"].ToString());

            return ret;
        }

        public void SetCombustionPumpClosedLoopControl(bool enable)
        {
            PrintTrace($"enable {enable}");
            CommMessage message = new CommMessage();
            message.MsgType = msgSetPump2ClosedLoopCtrl;
            message.Parameters.Add("EN", enable ? 1 : 0);

            SendOutgoingMessage(message);
        }

        public ClosedLoopControlParams SetCombustionPumpClosedLoopControl(bool? enable, double? target, int p, int i,
            int d, double zo, int ff)
        {
            PrintTrace($"enable {enable}, target {target}, p {p}, i {i}, d {d}, zo {zo}, ff {ff}");
            CommMessage message = new CommMessage();
            message.MsgType = msgSetPump2ClosedLoopCtrl;

            if(enable != null)
                message.Parameters.Add("EN", enable.Value ? 1 : 0);

            if (target != null)
                message.Parameters.Add("TGT", target);

            message.Parameters.Add("P", p);
            message.Parameters.Add("I", i);
            message.Parameters.Add("D", d);
            message.Parameters.Add("ZO", zo);
            message.Parameters.Add("FF", ff);

            var rcv = SendAndReceive(message, longTimeout * 3);

            return GetClcParamsFromMessage(rcv);
        }

        public void SetHeaterDrive(double percent)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetHeaterDrive;
            message.Parameters.Add("DRV", percent);

            SendOutgoingMessage(message);
        }

        public void SetSolenoid(bool enabled)
        {
            PrintTrace($"enable {enabled}");
            CommMessage message = new CommMessage();
            message.MsgType = msgSolenoid;
            message.Parameters.Add("EN", enabled ? 1 : 0);

            SendOutgoingMessage(message);
        }

        public void SetH2ClosedLoopControl(bool enable)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetH2LpCloseLoopCtrl;
            message.Parameters.Add("EN", enable ? 1 : 0);

            SendAndReceive(message, longTimeout * 3);
        }

        public void SetH2ClosedLoopControl(bool? enable, double? low, double? high, int? distance, int? speed, double? tolerance)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetH2LpCloseLoopCtrl;

            if (enable.HasValue)
            {
                message.Parameters.Add("EN", enable.Value ? 1 : 0);
            }

            if (low.HasValue)
            {
                message.Parameters.Add("LO", low.Value);
            }

            if (high.HasValue)
            {
                message.Parameters.Add("HI", high.Value);
            }

            if (distance.HasValue)
            {
                message.Parameters.Add("STEP", distance.Value);
            }

            if (speed.HasValue)
            {
                message.Parameters.Add("RATE", speed.Value);
            }

            if (tolerance.HasValue)
            {
                message.Parameters.Add("TOL", tolerance.Value);
            }

            SendAndReceive(message, longTimeout);
        }

        public void SetH2ClosedLoopControl(int distance, int speed)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetH2LpCloseLoopCtrl;
            message.Parameters.Add("STEP", distance);
            message.Parameters.Add("RATE", speed);

            SendAndReceive(message, longTimeout);
        }

        public void MoveNeedleValve(int distance, int speed)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgMoveNeedleValve;
            message.Parameters.Add("STEP", distance);
            message.Parameters.Add("RATE", speed);

            SendOutgoingMessage(message);
        }

        public void SetH2TargetComp(bool isCase, double refNum, double pos, double neg)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetH2TargetComp;
            message.Parameters.Add("SENS", isCase ? 1 : 0);
            message.Parameters.Add("REF", refNum);
            message.Parameters.Add("POS", pos);
            message.Parameters.Add("NEG", neg);

            SendOutgoingMessage(message);
        }

        public void SetP1TargetComp(bool isCase, double refNum, double pos, double neg)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetP1TargetComp;
            message.Parameters.Add("SENS", isCase ? 1 : 0);
            message.Parameters.Add("REF", refNum);
            message.Parameters.Add("POS", pos);
            message.Parameters.Add("NEG", neg);

            SendOutgoingMessage(message);
        }

        public void SetP2TargetComp(bool isCase, double refNum, double pos, double neg)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetP2TargetComp;
            message.Parameters.Add("SENS", isCase ? 1 : 0);
            message.Parameters.Add("REF", refNum);
            message.Parameters.Add("POS", pos);
            message.Parameters.Add("NEG", neg);

            SendOutgoingMessage(message);
        }

        public void Ignite()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgStartAutoIgnitionSequence;

            //Set GO=1 to start, GO=0 to abort
            message.Parameters.Add("GO", 1);

            SendOutgoingMessage(message);
        }

        public void AbortIgnite()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgStartAutoIgnitionSequence;

            //Set GO=1 to start, GO=0 to abort
            message.Parameters.Add("GO", 0);

            SendOutgoingMessage(message);
        }

        public void TurnOff()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgPowerControl;
            
            message.Parameters.Add("MAINKILL", 1);

            SendOutgoingMessage(message);
        }

        public byte[] ReadData(int count)
        {
            return ReadData(0, count);
        }

        public byte[] ReadData(int startPos, int count)
        {
            int readData = 0;
            List<byte> data = new List<byte>();

            while (readData != count)
            {
                CommMessage message = new CommMessage();
                message.MsgType = msgExtFlashRead;
                message.Parameters.Add("ADDR", readData + startPos);
                var dataWriteLength = Math.Min(256, count - readData);
                message.Parameters.Add("COUNT", dataWriteLength);

                var rcvMessage = SendAndReceive(message);

                string[] strData = rcvMessage.UnparsedString.Split(' ');

                var newData = strData.Select(s => Convert.ToByte(s, 16));

                data.AddRange(newData);

                readData += dataWriteLength;
            }

            return data.ToArray();
        }

        public void WriteData(byte[] data)
        {
            WriteData(0, data);
        }

        public void WriteData(int startPos, byte[] data)
        {
            int writtenData = 0;

            int count = 0;

            while (writtenData != data.Length)
            {
                CommMessage message = new CommMessage();
                message.MsgType = msgExtFlashWrite;
                message.Parameters.Add("ADDR", writtenData);
                var dataWriteLength = Math.Min(256, data.Length - writtenData);
                message.Parameters.Add("COUNT", dataWriteLength);
                message.UnparsedString = string.Join(" ",
                    data.Skip(writtenData).Take(dataWriteLength).Select(d => d.ToString("X2")));

                int tries = 3;

                while (tries > 0)
                {
                    try
                    {
                        tries--;
                        SendAndReceive(message, longTimeout);
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (tries > 0)
                        {
                            WriteToLog($"Problem while writing flash data, {tries} tries left");
                            WriteExceptionToPhxLog(ex);
                        }
                        else
                        {
                            WriteToLog($"Problem while writing flash data, no tries left, throwing up!");
                            WriteExceptionToPhxLog(ex);
                            throw ex;
                        }
                    }
                }

                writtenData += dataWriteLength;

                if (count % 5 == 0)
                {
                    int progress = (100 * writtenData) / data.Length;
                    OnWriteFlashProgress(progress);
                }
            }

            OnWriteFlashComplete();

            //Potential future validation
            /* var readData = ReadData(startPos, data.Length);

             for (int i = 0; i < data.Length; i++)
             {
                 if (readData[i] != data[i])
                 {
                     //Data was not written correctly.
                     //Do something here... maybe retry instead of exception... or just handle retry in device service?

                     throw new Exception("Data was not written correctly");
                 }
             }*/
        }

        public void DeleteCalEntry(int index)
        {
            PrintTrace($"index {index}");
            CommMessage message = new CommMessage();
            message.MsgType = msgDeleteCalTableEntry;
            message.Parameters.Add("INDEX", index);

            SendOutgoingMessage(message);
        }

        public void DeleteCalEntryRange(float minPpm, float maxPpm, bool includePers)
        {
            PrintTrace($"min {minPpm}, max {maxPpm}, include pers {includePers}");
            CommMessage message = new CommMessage();
            message.MsgType = msgDeleteCalTableRange;
            message.Parameters.Add("MINPPM", minPpm);
            message.Parameters.Add("MAXPPM", maxPpm);
            message.Parameters.Add("INCLPERS", includePers ? "1" : "0");

            SendOutgoingMessage(message);
        }

        public void SetNeedleValveBacklash(int backlash)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetValveBacklash;
            message.Parameters.Add("STEP", backlash);

            SendOutgoingMessage(message);
        }

        public void PulseGlowPlug(decimal seconds, decimal powerPct = 0.75m)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgGlowPlugPulse;
            message.Parameters.Add("SEC", string.Format("{0:0.0}", seconds));
            message.Parameters.Add("PWR", string.Format("{0:0.00}", powerPct));
            SendOutgoingMessage(message);
        }

        public void FlashBulkErase()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgExtFlashBulkErase;
            message.Parameters.Add("PW", "AZAZAZAZ");

            SendOutgoingMessage(message);
        }

        public void SetSamplePumpLevel(double level)
        {
            PrintTrace($"Level {level}");
            CommMessage message = new CommMessage();
            message.MsgType = msgSetPumpDrive;
            message.Parameters.Add("P1", level);

            SendOutgoingMessage(message);
        }

        public void SetCombustionPumpLevel(double level)
        {
            PrintTrace($"Level {level}");
            CommMessage message = new CommMessage();
            message.MsgType = msgSetPumpDrive;
            message.Parameters.Add("P2", level);

            SendOutgoingMessage(message);
        }

        public bool UpdateFirmware(byte[] firmwareFileBytes)
        {
            PrintTrace();

            try
            {
                //Disable all periodic readings before updating the firmware
                EnablePeriodicReadings(msgReadings, false);
                EnablePeriodicReadings(msgDriveLevels, false);
                EnablePeriodicReadings(msgFIDReadings, false);
                EnablePeriodicReadings(msgBatteryStatus, false);

                _firmwareUpdateInProgress = true;

                //Wait for the message handler to finish
                Task.Delay(1000).Wait();

                CommMessage message = new CommMessage();
                message.MsgType = msgFlashDownload;
                message.Parameters.Add("PW", "AZAZAZAZ");

                OnUpdateFirmwareProgress("Waiting for unit");

                try
                {
                    WriteToLog("Sending the write firmware start command");
                    SendOutgoingMessage(message);

                    List<byte> rcvBytes = new List<byte>();
                    string receivedMessage = string.Empty;

                    while (!receivedMessage.EndsWith("FLDL STATE=READY" + endOfMessage))
                    {
                        var readByte = _inputStream.ReadByte();
                        rcvBytes.Add(readByte);

                        receivedMessage = new string(Encoding.UTF8.GetChars(rcvBytes.ToArray()));
                    }

                    WriteToLog("Received flash ready response, about to start");
                }
                catch (Exception ex)
                {
                    //Ignoring in hopes that we do not brick the unit.
                    WriteExceptionToPhxLog(ex);
                }

                OnUpdateFirmwareProgress("Unit Ready");

                int numBlocks = (firmwareFileBytes.Length / 4096) + (firmwareFileBytes.Length % 4096 > 0 ? 1 : 0);

                WriteToLog(
                    $"There are {numBlocks} 4096b blocks to send, the total file size is {firmwareFileBytes.Length}");

                var ff = new byte[4095];

                for (int i = 0; i < ff.Length; i++)
                {
                    ff[i] = 0xFF;
                }

                List<byte> buf = new List<byte>(firmwareFileBytes);
                buf.AddRange(ff);

                byte[] buffer;
                bool startOver = false;

                for (int block = 0; block < numBlocks; block++)
                {
                    OnUpdateFirmwareProgress($"Updating block {block + 1}/{numBlocks}");
                    WriteToLog($"Updating block {block + 1}/{numBlocks}");

                    if (startOver)
                    {
                        OnUpdateFirmwareProgress("An error occurred... starting over.");
                        WriteToLog("An error occurred... starting over.");
                        startOver = false;
                        block = 0;
                    }

                    buffer = buf.Skip(block * 4096).Take(4096).ToArray();

                    _outputStream.Write(Encoding.UTF8.GetBytes("FLBK"), 0, 4);
                    _outputStream.Write(buffer, 0, 4096);

                    List<byte> rcvBytes = new List<byte>();

                    //In the future maybe put some kind of timeout just in case 
                    //the input stream read is corrupt or unsuccessful
                    for (int i = 0; i < 4; i++)
                    {
                        var readByte = _inputStream.ReadByte();
                        rcvBytes.Add(readByte);
                    }

                    string rcvMsg = new string(Encoding.UTF8.GetChars(rcvBytes.ToArray()));

                    if (rcvMsg == "flbk")
                    {
                        WriteToLog($"Block {block + 1}/{numBlocks} acknowledged");
                        continue;
                    }
                    else
                    {
                        WriteToLog($"ERROR: received {rcvMsg} from 42, starting over");

                        //Error start process over
                        _outputStream.Write(Encoding.UTF8.GetBytes("ZERO"), 0, 4);

                        //In the future maybe put some kind of timeout just in case 
                        //the input stream read is corrupt or unsuccessful
                        rcvBytes.Clear();

                        for (int i = 0; i < 4; i++)
                        {
                            var readByte = _inputStream.ReadByte();
                            rcvBytes.Add(readByte);
                        }

                        rcvMsg = new string(Encoding.UTF8.GetChars(rcvBytes.ToArray()));

                        if (rcvMsg != "zero")
                            throw new Exception(
                                "umm not sure what to do here... should always be 'zero' as a response... maybe try again?");

                        //Start the transfer process over
                        startOver = true;
                    }
                }

                OnUpdateFirmwareProgress("Finishing up");
                WriteToLog("Sending END! command");
                _outputStream.Write(Encoding.UTF8.GetBytes("END!"), 0, 4);
                _outputStream.Flush();

                {
                    List<byte> rcvBytes = new List<byte>();
                    string receivedMessage = string.Empty;

                    while (!receivedMessage.EndsWith("end!"))
                    {
                        var readByte = _inputStream.ReadByte();
                        rcvBytes.Add(readByte);

                        receivedMessage = new string(Encoding.UTF8.GetChars(rcvBytes.ToArray()));
                    }

                    WriteToLog($"Received {receivedMessage}");
                }

                Task.Delay(20000).Wait();

                OnUpdateFirmwareProgress("Complete");
                WriteToLog($"Firmware update complete");
            }
            catch (Exception ex)
            {
                WriteExceptionToPhxLog(ex);
                OnUpdateFirmwareProgress("Error");
            }
            finally
            {
                _firmwareUpdateInProgress = false;
            }

            return true;
        }

        public void WriteParamsVersion(DateTime dateTime)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgWriteParameterVerNum;

            message.UnparsedString = dateTime.Date.ToString("MM/dd/yyyy");

            SendOutgoingMessage(message);
        }

        public DateTime ReadParamsVersion()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgReadsParameterVerNum;

            var response = SendAndReceive(message, longTimeout);

            var success = DateTime.TryParse(response.UnparsedString, out DateTime dateTime);
            
            if(!success)
            {
                var success2 =
                    DateTime.TryParseExact(response.UnparsedString, new []{"MM/dd/yyyy", "dd/MM/yyyy"}, new DateTimeFormatInfo(), DateTimeStyles.None, out dateTime);

                if (!success2)
                {
                    WriteToPhxLog("Incorrect ParmsVersion found: " + response.UnparsedString);

                    throw new Exception(response.UnparsedString + " is not a valid DateTime");
                }

                return dateTime;

            }

            return dateTime;
        }

        public void SetCalibrationAtIndex(int ppm, decimal pa, int index, string timestamp, bool persist = false)
        {
            PrintTrace($"ppm {ppm}, pa {pa}, index {index}, timestamp {timestamp}, persist {persist}");
            CommMessage message = new CommMessage();
            message.MsgType = msgCalibrationTable;
            message.Parameters.Add($"PPM{index}", ppm.ToString());
            message.Parameters.Add($"PA{index}", pa.ToString());


            if (!string.IsNullOrEmpty(timestamp))
            {
                message.Parameters[$"TS{index}"] = timestamp;
            }

            message.Parameters.Add($"PER{index}", persist ? "1" : "0");

            SendOutgoingMessage(message);
        }

        public PpmCalibrationInfo[] GenerateCalTableEntry(float ppm, DateTime timestamp)
        {
            PrintTrace($"ppm {ppm}, timestamp {timestamp}");
            CommMessage message = new CommMessage();
            message.MsgType = msgGenerateCalTableEntry;

            message.Parameters["PPM"] = ppm;
            message.Parameters["TS"] = timestamp.ToString("yyyy/MM/dd_HH:mm:ss", CultureInfo.InvariantCulture);

            var sendTime = Now;
            SendOutgoingMessage(message);

            Stopwatch timer = Stopwatch.StartNew();

            while (timer.ElapsedMilliseconds < 30000)
            {
                if (errors.ContainsKey(msgGenerateCalTableEntry))
                {
                    if (errors[msgGenerateCalTableEntry].Key > sendTime)
                    {
                        string errorMessage = "ERROR: something bad happened while trying to generate a calibration";

                        switch (errors[msgGenerateCalTableEntry].Value)
                        {
                            case 21:
                                errorMessage = "Warmup time for this device has not been met";
                                break;
                            case 5:
                                errorMessage = "No empty calibration slots";
                                break;
                            case 20:
                                errorMessage = "Calibration does not make sense";
                                break;
                        }

                        WriteToLog(errorMessage);
                        timer.Stop();
                        throw new Exception(errorMessage);
                    }
                }

                CommMessage response = null;
                
                response = receivedMessages.FirstOrDefault(m => m.MsgType == msgCalibrationTable && !m.Handled);

                if (response != null)
                {
                    timer.Stop();
                    //give it some time for errors to come in
                    Task.Delay(200).Wait();
                    if (errors.ContainsKey(msgGenerateCalTableEntry))
                    {
                        if (errors[msgGenerateCalTableEntry].Key > sendTime)
                        {
                            string errorMessage = "ERROR: something bad happened while trying to generate a calibration";

                            switch (errors[msgGenerateCalTableEntry].Value)
                            {
                                case 21:
                                    errorMessage = "Warmup time for this device has not been met";
                                    break;
                                case 5:
                                    errorMessage = "No empty calibration slots";
                                    break;
                                case 20:
                                    errorMessage = "Calibration does not make sense";
                                    break;
                            }

                            WriteToLog(errorMessage);
                            throw new Exception(errorMessage);
                        }
                    }

                    response.Handled = true;
                    return ParseCalibrationMessage(response);
                }

                Task.Delay(20).Wait(20);
            }

            timer.Stop();
            string timeoutMessage =
                $"ERROR: Receive timed out after {timer.Elapsed} waiting for a GenerateCalTableEntry message";
            WriteToLog(timeoutMessage);
            throw new Exception(timeoutMessage);
        }

        public void SetTime(DateTime dateTime)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgRealTimeClock;

            message.Parameters["TS"] = dateTime.ToString("yyyy/MM/dd_HH:mm:ss", CultureInfo.InvariantCulture);

            SendOutgoingMessage(message);
        }

        public DateTime GetTime()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgRealTimeClock;

            var response = SendAndReceive(message, longTimeout);

            DateTime dateTime = DateTime.ParseExact(response.Parameters["TS"].ToString(), "yyyy/MM/dd_HH:mm:ss", CultureInfo.InvariantCulture);
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Local);

            return dateTime;
        }

        private PpmCalibrationInfo[] ParseCalibrationMessage(CommMessage message)
        {
            List<PpmCalibrationInfo> calibrationInfos = new List<PpmCalibrationInfo>();

            foreach (var parameter in message.Parameters)
            {
                int index = int.Parse(parameter.Key.Substring(parameter.Key.Length - 1));
                string infoProp = parameter.Key.Substring(0, parameter.Key.Length - 1);

                if (calibrationInfos.Count <= index)
                    calibrationInfos.Add(new PpmCalibrationInfo());

                calibrationInfos[index].IsValid = true;
                calibrationInfos[index].Index = index;

                if (infoProp == "HP")
                {
                    calibrationInfos[index].H2Pressure = float.Parse(parameter.Value.ToString());
                }
                else if (infoProp == "PA")
                {
                    calibrationInfos[index].FidCurrent = (int)float.Parse(parameter.Value.ToString());
                }
                else if (infoProp == "PPM")
                {
                    calibrationInfos[index].Ppm = float.Parse(parameter.Value.ToString());
                }
                else if (infoProp == "TS")
                {
                    calibrationInfos[index].Timestamp = parameter.Value.ToString();
                }
                else if (infoProp == "PER")
                {
                    calibrationInfos[index].IsFactoryCal = parameter.Value.ToString() == "1";
                }
            }

            List<string> lines = new List<string>();

            foreach (var ppmCalibrationInfo in calibrationInfos)
            {
                lines.Add(
                    $"index {ppmCalibrationInfo.Index}, HP {ppmCalibrationInfo.H2Pressure}, PA {ppmCalibrationInfo.FidCurrent}, ppm {ppmCalibrationInfo.Ppm}, timestamp {ppmCalibrationInfo.Timestamp}, factory {ppmCalibrationInfo.IsFactoryCal}");
            }

            WriteToLog($"Received the following calibrations:\n\t{string.Join("\n\t", lines)}");

            return calibrationInfos.ToArray();
        }

        public PpmCalibrationInfo[] GetCalibrationTable()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgCalibrationTable;

            var response = SendAndReceive(message, 10000);

            return ParseCalibrationMessage(response);
        }

        public ShutdownThresholds SetPumpShutdownThresholds(decimal? p2InLow, decimal? p2InHigh, decimal? chamber, decimal? fDelta, decimal? fOff, bool? enableCode24)
        {
            return SetPumpShutdownThresholds(p2InLow, p2InHigh, chamber, fDelta, fOff, enableCode24, null);
        }

        public ShutdownThresholds SetPumpShutdownThresholds(decimal? p2InLow, decimal? p2InHigh, decimal? chamber, decimal? fDelta,
            decimal? fOff, bool? enableCode24, decimal? lowVoltageShutdown)
        {
            return SetPumpShutdownThresholds(p2InLow, p2InHigh, null, null, chamber, fDelta, fOff, enableCode24, lowVoltageShutdown);
        }

        public ShutdownThresholds SetPumpShutdownThresholds(decimal? p2InLow, decimal? p2InHigh, decimal? p1InHMin, decimal? p1InHMax, decimal? chamber, decimal? fDelta,
            decimal? fOff, bool? enableCode24, decimal? lowVoltageShutdown)
        {
            PrintTrace($"p2InLow {p2InLow}, p2InHigh {p2InHigh}, p1InHMin {p1InHMin}, p1InHMax {p1InHMax}, chamber {chamber}, fDelta {fDelta}, fOff {fOff}, enableCode24 {enableCode24}, lowVoltageShutdown {lowVoltageShutdown}");

            CommMessage message = new CommMessage();
            message.MsgType = msgSetShutdownThresholds;

            if (p2InLow != null)
                message.Parameters.Add("P2INL", p2InLow.ToString());

            if (p2InHigh != null)
                message.Parameters.Add("P2INH", p2InHigh.ToString());

            if (p1InHMin != null)
                message.Parameters.Add("P1INHMIN", p1InHMin.ToString());

            if (p1InHMax != null)
                message.Parameters.Add("P1INHMAX", p1InHMax.ToString());

            if (chamber != null)
                message.Parameters.Add("CHMBR", chamber.ToString());

            if (fDelta != null)
                message.Parameters.Add("FDELTA", fDelta.ToString());

            if (fOff != null)
                message.Parameters.Add("FOFF", fOff.ToString());

            if (lowVoltageShutdown != null)
                message.Parameters.Add("LOWVOLT", lowVoltageShutdown.ToString());

            if (enableCode24.HasValue)
                message.Parameters.Add("ANNOY24", enableCode24.Value ? "1" : "0");

            var response = SendAndReceive(message);

            return ParseShutdownThresholdsMessage(response);
        }

        public ShutdownThresholds SetPumpShutdownThresholds(decimal p2InLow, decimal p2InHigh, decimal chamber)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetShutdownThresholds;

            message.Parameters.Add("P2INL", p2InLow.ToString());
            message.Parameters.Add("P2INH", p2InHigh.ToString());
            message.Parameters.Add("CHMBR", chamber.ToString());

            var response = SendAndReceive(message);

            return ParseShutdownThresholdsMessage(response);
        }

        public ShutdownThresholds SetPumpShutdownThresholds(decimal p2InLow, decimal chamber)
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetShutdownThresholds;

            message.Parameters.Add("P2INL", p2InLow.ToString());
            message.Parameters.Add("CHMBR", chamber.ToString());

            var response = SendAndReceive(message);

            return ParseShutdownThresholdsMessage(response);
        }

        private ShutdownThresholds ParseShutdownThresholdsMessage(CommMessage message)
        {
            var retVal = new ShutdownThresholds();

            if (message.Parameters.ContainsKey("P2INL"))
                retVal.P2InL = decimal.Parse(message.Parameters["P2INL"].ToString());

            if (message.Parameters.ContainsKey("P1INL"))
                retVal.P2InL = decimal.Parse(message.Parameters["P1INL"].ToString());


            if (message.Parameters.ContainsKey("P2INH"))
                retVal.P2InH = decimal.Parse(message.Parameters["P2INH"].ToString());

            if (message.Parameters.ContainsKey("P1INH"))
                retVal.P2InH = decimal.Parse(message.Parameters["P1INH"].ToString());

            retVal.Chamber = decimal.Parse(message.Parameters["CHMBR"].ToString());

            if (message.Parameters.ContainsKey("P1INHRECAL"))
            {
                retVal.P2InHReCal = message.Parameters["P1INHRECAL"].ToString() == "1";
            }

            if (message.Parameters.ContainsKey("P1INHMIN"))
                retVal.P2InHMin = decimal.Parse(message.Parameters["P1INHMIN"].ToString());

            if (message.Parameters.ContainsKey("P1INHMAX"))
                retVal.P2InHMax = decimal.Parse(message.Parameters["P1INHMAX"].ToString());

            return retVal;
        }

        public ShutdownThresholds GetPumpShutdownThresholds()
        {
            PrintTrace();
            CommMessage message = new CommMessage();
            message.MsgType = msgSetShutdownThresholds;

            var retMsg = SendAndReceive(message, 3000);

            return ParseShutdownThresholdsMessage(retMsg);
        }
        
        private bool SendOutgoingMessage(CommMessage msg)
        {
            string message = $"{hostToUnit} {msg.MsgType} ";

            message += string.Join(",", msg.Parameters.Select(p => $"{p.Key}={p.Value}"));

            if (!string.IsNullOrEmpty(msg.UnparsedString))
            {
                message += " ";

                message += msg.UnparsedString;
            }

            //if (msg.MsgType == msgAutoIgnitionParameters && message.EndsWith(" "))
            //    message = message.Substring(0, message.Length - 1);

            if (msg.Parameters.Count == 0 && string.IsNullOrEmpty(msg.UnparsedString) && message.EndsWith(" "))
                message = message.Substring(0, message.Length - 1);

            message += endOfMessage;

            Transmit(Encoding.UTF8.GetBytes(message));

            var type = msg.Parameters.ContainsKey("TYPE") ? msg.Parameters["TYPE"].ToString() : msg.MsgType;

            WriteToLog($"Transmitted {type} command");

            return true;
        }

        private string _lastRawMessage = string.Empty;

        //Do not use this function! Only the message thread should use this
        private CommMessage ReadIncomingMessage(int max = 5)
        {
            string message = "";

            bool endOfIncomingMessage = false;

            List<char> readChars = new List<char>();

            try
            {
                Trace();
                while (!endOfIncomingMessage)
                {
                    Trace();
                    readChars.Add(Encoding.UTF8.GetChars(new[] { _inputStream.ReadByte() })[0]);

                    Trace();
                    try
                    {
                        if (readChars.Count > 1)
                        {
                            var last2chars = new string(readChars.Skip(readChars.Count - 2).Take(2).ToArray());

                            endOfIncomingMessage = last2chars == endOfMessage;
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteToLog(
                            $"Problem checking for end of message {ex.Message} - {new string(readChars.ToArray())}");
                    }
                }

                message = new string(readChars.ToArray());
                var untrimmed = message;

                var split = message.Trim().Split(' ');

                if (split.Length < 2 || split[0].Length > 5 || !_messageTypes.Contains(split[1]))
                {
                    if (max == 0) throw new Exception("Could not read a message off the socket!");

                    WriteToLog($"Discarded message: {message}, last message is {_lastRawMessage}");

                    MessageReceived?.Invoke(null, message);

                    return ReadIncomingMessage(max - 1);
                }

                _lastRawMessage = untrimmed;

                try
                {

                    CommMessage commMessage = new CommMessage();

                    commMessage.RawResponse = untrimmed;

                    commMessage.MsgType = split[1];

                    if (split.Length > 2)
                    {
                        if (split[2].Contains("="))
                        {
                            var paramSplit = split[2].Split(',');

                            foreach (var param in paramSplit)
                            {

                                //Sometimes the data is corrupt.  Not sure what to do except salvage what we can.  Perhaps try again?
                                try
                                {
                                    var keyValue = param.Split('=');

                                    commMessage.Parameters[keyValue[0]] = keyValue[1];
                                }
                                catch (Exception ex)
                                {
                                    WriteToLog($"Could not parse [{param}]");
                                    WriteExceptionToPhxLog(ex);
                                }
                            }
                        }
                        else
                        {
                            commMessage.UnparsedString = string.Join(" ", split.Skip(2));
                        }
                    }

                    if (string.IsNullOrWhiteSpace(commMessage.UnparsedString))
                    {
                        commMessage.UnparsedString = string.Join(" ", split.Skip(3));
                    }

                    return commMessage;
                }
                catch (Exception ex)
                {
                    WriteToLog(
                        $"ERROR: Trouble parsing message [{message}], last message is {_lastRawMessage}, last trace line {messageThreadLastLine} exception follows");
                    WriteExceptionToPhxLog(ex);
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                WriteToLog(
                    $"ERROR: Trouble parsing message [{message}], last message is {_lastRawMessage}, last trace line {messageThreadLastLine} exception follows");
                WriteExceptionToPhxLog(ex);
                throw ex;
            }
        }

        private bool Transmit(byte[] buf)
        {
            lock (_outputStream)
            {
                _outputStream.Write(buf, 0, buf.Length);

                try
                {
                    _outputStream.Flush();
                }
                catch (Exception)
                {
                    //this is ok, some underlying streams don't implement flush and throw an exception
                }
            }

            return true;
        }


        public class CommMessage
        {
            public string MsgType;
            public Dictionary<string, object> Parameters = new Dictionary<string, object>();
            public string UnparsedString;
            public string RawResponse;

            public override string ToString()
            {
                return MsgType + "  " + String.Join(",", Parameters.Select(kv => $"{kv.Key}={kv.Value}")) + " " +
                       UnparsedString;
            }

            public bool Handled { get; set; }
        }


        public void WriteExceptionToPhxLog(Exception exception)
        {
            List<string> lines = new List<string>();

            lines.Add($"ERROR: {exception.Message}");

            lines.Add(exception.StackTrace);

            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
                lines.Add("---Inner exception---");
                lines.Add($"ERROR: {exception.Message}");

                lines.Add(exception.StackTrace);
            }

            WriteToLog(string.Join("\n", lines));
        }

        private void OnCommandError(CommandErrorType errorType, string error)
        {
            CommandError?.Invoke(this, new CommandErrorEventArgs(errorType, error));
        }

        private void OnUpdateFirmwareProgress(string message)
        {
            UpdateFirmwareProgress?.Invoke(this, new UpdateFirmwareEventArgs(message));
        }


        private void OnGetLogsProgress(decimal progress)
        {
            GetLogsProgress?.Invoke(this, new GetLogsProgressEventArgs(progress));
        }
    }
}