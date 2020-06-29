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
using Ldartools.Common.IO;
using Ldartools.Common.Services;
using Ldartools.Common.Util;

namespace Ldartools.Common.Devices
{
    public class DataPolledEventArgs : EventArgs
    {
        public Dictionary<string, string> PhxProperties{ get; protected set; }
        public float Ppm { get; protected set; }


        public DataPolledEventArgs(Dictionary<string, string> phxProperties, float ppm)
        {
            PhxProperties = phxProperties;
            Ppm = ppm;
        }

        public string GetPropertyOrDefault(string name)
        {
            if (PhxProperties == null || !PhxProperties.ContainsKey(name)) return null;

            return PhxProperties[name];
        }
    }

    public class ErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }

        public ErrorEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }

    public class UpdateFirmwareEventArgs : EventArgs
    {
        public string Message { get; }

        public UpdateFirmwareEventArgs(string message)
        {
            Message = message;
        }
    }

    public class GetLogsProgressEventArgs : EventArgs
    {
        public decimal Progress { get; set; }

        public GetLogsProgressEventArgs(decimal progress)
        {
            Progress = progress;
        }
    }

    public class WriteFlashProgressEventArgs : EventArgs
    {
        public int Progress { get; set; }

        public WriteFlashProgressEventArgs(int progress)
        {
            Progress = progress;
        }
    }

    public class ReadFlashProgressEventArgs : EventArgs
    {
        public int Progress { get; set; }

        public ReadFlashProgressEventArgs(int progress)
        {
            Progress = progress;
        }
    }

    public class Reconnect21NeededException : Exception
    {

    }

    /// <summary>
    /// This class handles all of the low level serial communication with a phx21.
    /// It was ported from a c++ project, which explains some of the way things
    /// are layed out and named.
    /// 
    /// To command a phx21 you have to send the correct command byte (they're all defined as CMD_FIDM_*)
    /// along with the appropriate command parameters struct. All (or most maybe?) commands will
    /// elicit a response from the phx21.
    /// 
    /// Command responses from the phx21 come back over serial and are also defined structs.
    /// All responses start with the byte SYNC_CODE_RES. The next byte is the response length and the
    /// third is the command byte (CMD_FIDM_*) that the response matches to. Subsequent data varies and is as
    /// defined in the struct that matches to the type of command sent.
    /// 
    /// For example, to get the firmware version number:
    /// Send:       Byte CMD_FIDM_CONFIGURATION_READ with a ConfigurationReadParams struct
    /// Receive:    A ConfigurationResponse struct where the first byte is SYNC_CODE_RES.
    ///             The second byte is length 41 which matches the length of the ConfigurationResponse struct
    ///             The third byte is CMD_FIDM_CONFIGURATION_READ.
    ///             The rest of the bytes are the data for the struct, one of which is the firmware version
    /// </summary>
    public sealed class Phx21
    {
        private Timer PollingTimer;
        private Timer LoggingTimer;

        private bool _goodbyeSent = false;

        private int hourOffset = 0;

        private ConcurrentQueue<BluetoothMessage> sendMessages = new ConcurrentQueue<BluetoothMessage>();

        private class MessageContainer
        {
            public DateTime Timestamp { get; set; }
            public byte[] Bytes { get; set; }
            public byte Type { get; set; }
        }

        private ConcurrentDictionary<byte, MessageContainer> receivedMessages = new ConcurrentDictionary<byte, MessageContainer>();


        #region Define Constants

        private const int FLASH_BLOCK_READ_TIMEOUT = 1000;
        private const byte MAX_CMD_LENGTH_BYTES = 255;
        private const int CMD_START_TIMEOUT_MS = 300;

        /// <summary>
        /// Sync codes, these signal the start of a new message
        /// </summary>
        private const byte SYNC_CODE_CMD = 0x5A;
        private const byte SYNC_CODE_RES = 0xA5;

        private const byte ERROR_LIST_LENGTH = 4;

        /// <summary>
        /// Field positions common to all messages received
        /// </summary>
        private const byte FIELD_SYNC_CODE = 0;
        private const byte FIELD_LENGTH_BYTES = 1;
        private const byte FIELD_CMD_ID = 2;

        /// <summary>
        /// command bytes
        /// </summary>
        private const byte CMD_FIDM_NO_OP = 0x00;
        private const byte CMD_FIDM_PUMP_CONTROL = 0x01;
        private const byte CMD_FIDM_SOLENOID_CONTROL = 0x02;
        private const byte CMD_FIDM_IGNITE_PULSE = 0x03;
        private const byte CMD_FIDM_SET_SAMPLING_PARAMETERS = 0x04;
        private const byte CMD_FIDM_READ_DATA = 0x05;
        private const byte CMD_FIDM_RESET_FIRMWARE = 0x06;
        private const byte CMD_FIDM_FLASH_WRITE = 0x07;
        private const byte CMD_FIDM_FLASH_READ = 0x08;
        private const byte CMD_FIDM_FLASH_ERASE = 0x09;
        private const byte CMD_FIDM_CONFIGURATION_READ = 0x0A;
        private const byte CMD_FIDM_DEBUG = 0x0B;
        private const byte CMD_FIDM_INTEGRATION_CONTROL = 0x0C;
        private const byte CMD_FIDM_HIGH_VOLTAGE_ON_OFF = 0x0D;
        private const byte CMD_FIDM_FIDM_CONFIGURATION_READ = 0x0E;
        private const byte CMD_FIDM_SET_BT_WATCHDOG = 0x0F;
        private const byte CMD_FIDM_SET_TC_CALIB_LO = 0x10;
        private const byte CMD_FIDM_SET_TC_CALIB_HI = 0x11;
        private const byte CMD_FIDM_SET_TM_CALIB_LO = 0x12;
        private const byte CMD_FIDM_SET_TM_CALIB_HI = 0x13;
        private const byte CMD_FIDM_FLASH_START_STREAM_WRITE = 0x14;
        private const byte CMD_FIDM_FLASH_WRITE_STREAM_DATA = 0x15;
        private const byte CMD_FIDM_FLASH_STOP_STREAM_WRITE = 0x16;
        private const byte CMD_FIDM_FLASH_START_STREAM_READ = 0x17;
        private const byte CMD_FIDM_FLASH_STOP_STREAM_READ = 0x18;
        private const byte CMD_FIDM_NEEDLE_VALVE_STEP = 0x19;
        private const byte CMD_FIDM_GET_SYSTEM_CURRENT = 0x1A;
        private const byte CMD_FIDM_PUMP_AUX_1_CONTROL = 0x1B;
        private const byte CMD_FIDM_SET_PUMPA_CTRL_PARAMS = 0x1C;
        private const byte CMD_FIDM_SET_PUMPA_CLOSED_LOOP = 0x1D;
        private const byte CMD_FIDM_SET_DEADHEAD_PARAMS = 0x1E;
        private const byte CMD_FIDM_GET_ERROR_LIST = 0x1F;
        private const byte CMD_FIDM_AUTO_IGNITION_SEQUENCE = 0x20;
        private const byte CMD_FIDM_GENERATE_PPM_CALIBRATION = 0x21;
        private const byte CMD_FIDM_SET_PPM_CALIBRATION = 0x22;
        private const byte CMD_FIDM_GET_PPM_CALIBRATION = 0x23;
        private const byte CMD_FIDM_SET_CAL_H2PRES_COMPENSATION = 0x24;
        private const byte CMD_FIDM_READ_DATA_EXTENDED = 0x25;
        private const byte CMD_FIDM_GOODBYE = 0x26;
        private const byte LAST_VALID_CMD = 0x26;

        private const byte STATUS_PUMP_A_ON = 0x01;
        private const byte STATUS_PUMP_B_ON = 0x02;
        private const byte STATUS_SOLENOID_A_ON = 0x04;
        private const byte STATUS_SOLENOID_B_ON = 0x08;
        private const byte STATUS_GLOW_PLUG_A_ON = 0x10;
        private const byte STATUS_GLOW_PLUG_B_ON = 0x20;
        private const byte STATUS_HV_ON = 0x40;
        private const byte STATUS_NEW_ERROR = 0x80;

        private const byte ERROR_NO_ERROR = 0x00;
        private const byte ERROR_UNKNOWN_CMD = 0xFF;
        private const byte ERROR_INCORRECT_NUM_PARAMS = 0xFE;
        private const byte ERROR_INVALID_PARAM = 0xFD;
        private const byte ERROR_FLASH_STREAM_SEQUENCE_LOST = 0xFC;
        private const byte ERROR_NEEDLE_VALVE_MOVING = 0xFB;
        private const byte ERROR_BATT_TOO_LOW = 0xFA;
        private const byte ERROR_NO_EMPTY_CAL_SLOTS = 0xF9;

        private const byte ERROR_DEAD_HEAD = 1;
        private const byte ERROR_IGN_SEQ_FAILED_PRES = 2;
        private const byte ERROR_IGN_SEQ_FAILED_TEMP = 3;

        private const byte RANGE_MODE_0_LO = 0;
        private const byte RANGE_MODE_1_MID = 1;
        private const byte RANGE_MODE_2_HI = 2;
        private const byte RANGE_MODE_3_MAX = 3;

        private const byte FLAG_RES_RECEIVING_RESPONSE = 0x01;
        private const byte FLAG_RES_COMPLETE = 0x02;
        private const byte FLAG_RES_CRC_VALID = 0x04;
        private const byte FLAG_RES_CORRECT_NUM_RESULTS = 0x08;
        private const byte FLAG_RES_KNOWN_RES = 0x10;

        private const byte MAX_FLASH_BYTES_PER_OP = 192;

        /// <summary>
        /// States for used while receiving data
        /// </summary>
        private const byte STATE_WAITING_FOR_SYNC_CODE = 0;
        private const byte STATE_WAITING_FOR_LENGTH = 1;
        private const byte STATE_WAITING_FOR_RESPONSE_ID = 2;
        private const byte STATE_WAITING_FOR_RESPONSE_DATA = 3;
        public const byte STATE_RESPONSE_COMPLETE = 4;

        public const byte PID_LOG_SIZE = 5;
        private const int longTimeout = 5000;
        private const int warnTime = 1000;
        private int junkDataCount = 0;

        #endregion Define Constants

        public event EventHandler<ErrorEventArgs> Error;
        public event EventHandler WriteFlashComplete;

        public string User { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string Application { get; set; } = string.Empty;

        private void OnWriteFlashComplete()
        {
            WriteFlashComplete?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler ReadFlashComplete;

        private void OnReadFlashComplete()
        {
            ReadFlashComplete?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<WriteFlashProgressEventArgs> WriteFlashProgress;

        private void OnWriteFlashProgress(int progress)
        {
            WriteFlashProgress?.Invoke(this, new WriteFlashProgressEventArgs(progress));
        }

        public event EventHandler<ReadFlashProgressEventArgs> ReadFlashProgress;

        private void OnReadFlashProgress(int progress)
        {
            ReadFlashProgress?.Invoke(this, new ReadFlashProgressEventArgs(progress));
        }

        private void OnError(ErrorEventArgs e)
        {
            Error?.Invoke(this, e);
        }

        public event EventHandler<DataPolledEventArgs> DataPolled;

        private readonly IInputStream _inputStream;
        private readonly IOutputStream _outputStream;
        private readonly IFileManager _fileManager;
        private readonly ITimeMongerService _timeMongerService;

        public DateTime Now => _timeMongerService?.Now ?? DateTime.Now;

        private bool _isLoggingConfigured;

        public int PollingInterval { get; private set; } = 250;
        private int _loggingInterval = 500;
        private bool _isPolling = false;

        public DateTime StatusDateTime { get; private set; }
        public string LogFilePath { get; private set; }
        public string StatusFilePath { get; private set; }

        public Phx21Status CurrentStatus { get; private set; }

        public string Name { get; set; }
        public bool IsRunning { get; set; }
        public string Status { get; set; }
        public int UseAvgPerc { get; set; }
        public int LongAverageCount { get; set; }
        public int ShortAverageCount { get; set; }
        public int AverageCutoffPpm { get; set; }

        private byte _currentHardwareAvg = 10;

        private static PropertyInfo[] _phx21StatusProperties;

        public IInputStream InputStream => _inputStream;
        
        public Phx21(IInputStream inputStream, IOutputStream outputStream, IFileManager fileManager, string name, int hourOffset, string loggingDirectory, ITimeMongerService timeMongerService = null)
        {
            this._inputStream = inputStream;
            this._outputStream = outputStream;
            this._fileManager = fileManager;
            this.hourOffset = hourOffset;
            _timeMongerService = timeMongerService;
            Name = name;
            ConfigureLogging(loggingDirectory);

            Initialize();
        }

        public Phx21(IInputStream inputStream, IOutputStream outputStream, IFileManager fileManager, string name, ITimeMongerService timeMongerService = null, string application = "", string user = "", string site = "")
        {
            _inputStream = inputStream;
            _outputStream = outputStream;
            _fileManager = fileManager;
            _timeMongerService = timeMongerService;
            Name = name;
            Application = application;
            User = user;
            Site = site;

            ConfigureLogging();

            Initialize();
        }

        private void Initialize()
        {
            UseAvgPerc = 10;
            LongAverageCount = 25;
            ShortAverageCount = 5;
            AverageCutoffPpm = 40;
            
            _phx21StatusProperties = typeof(Phx21Status).GetRuntimeProperties().ToArray();
            
            StartMessageHandler();

            InitPollingAndLoggingActions();
            
            try
            {
                TryUtils.Retry(() => SetSamplingParameters(RANGE_MODE_0_LO), 3, 100);
                //second to last is the # samples for hw averaging
                TryUtils.Retry(() => SetIntegrationControlParams(0, 1, 7, 50000, _currentHardwareAvg, 0), 3, 100);
                TryUtils.Retry(() => SetDeadHeadParams(true, 150, 100), 3, 100);
                TryUtils.Retry(() => SetCalH2PressureCompensation((long)(10000 * (-0.3)), (long)(10000 * 0.3)), 3, 100);
                //TryUtils.Retry(() => SetBluetoothWatchdog(true, 10, 10), 3, 100);

                WriteToLog("Initialization complete");
            }
            catch (Exception ex)
            {
                WriteToLog("Problem during inilization");
                WriteExceptionToPhxLog(ex);
                throw ex;
            }
        }

        public bool ShutdownNow { get; set; }

        private Task _sendTask = null;
        private Task _receiveTask = null;
        private bool _receiveAlive = false;

        private void StartMessageHandler()
        {
            if (_sendTask == null)
            {
                _sendTask = new Task(() =>
                {
                    int errorcount = 0;

                    while (!ShutdownNow || (ShutdownNow && _receiveAlive))
                    {
                        try
                        {
                            while (sendMessages.Any())
                            {
                                BluetoothMessage message;
                                if (sendMessages.TryDequeue(out message))
                                {
                                    //if (message.Bytes.Length > 2)
                                    //    WriteToLog($"Sending message with type {message.Bytes[2]} and size {message.Bytes.Length}");
                                    _outputStream.Flush();
                                    _outputStream.Write(message.Bytes, message.Offest, message.Length);
                                    _outputStream.Flush();
                                }
                            }

                            Task.Delay(10).Wait();

                            errorcount = 0;
                        }
                        catch (Exception ex)
                        {
                            if (_goodbyeSent) break;

                            WriteToLog("Message thread error #" + errorcount);
                            WriteExceptionToPhxLog(ex);
                            errorcount++;

                            Task.Run(() => { OnError(new ErrorEventArgs(ex)); });

                            if (errorcount > 10)
                            {
                                WriteToLog("Message thread shutting down because of errors");
                                Task.Run(() => { OnError(new ErrorEventArgs(new Reconnect21NeededException())); });
                                ShutdownNow = true;
                                return;
                            }
                        }
                    }

                    WriteToLog("Message thread shutting down");

                    WriteLogQueue();
                });

                _sendTask.Start();
                WriteToLog("Send thread started");
            }

            if (_receiveTask == null)
            {
                _receiveTask = new Task(() =>
                {
                    _receiveAlive = true;
                    int errorcount = 0;

                    try
                    {
                        while (!ShutdownNow || (ShutdownNow && inPollingAction))
                        {
                            try
                            {
                                var messageBytes = GetNextResponse();

                                if (messageBytes.Length > 2)
                                {
                                    byte type = messageBytes[2];

                                    //WriteToLog($"Received message with type {type} and length {messageBytes.Length}");

                                    var container = new MessageContainer
                                    {
                                        Bytes = messageBytes,
                                        Type = type,
                                        Timestamp = Now
                                    };

                                    receivedMessages[type] = container;
                                }

                                Task.Delay(10).Wait();

                                errorcount = 0;
                            }
                            catch (Exception ex)
                            {
                                if (_goodbyeSent) break;

                                WriteToLog("Receive thread error #" + errorcount);
                                WriteExceptionToPhxLog(ex);
                                errorcount++;

                                Task.Run(() => { OnError(new ErrorEventArgs(ex)); });

                                if (errorcount > 10)
                                {
                                    WriteToLog("Receive thread shutting down because of errors");
                                    Task.Run(() => { OnError(new ErrorEventArgs(new Reconnect21NeededException())); });
                                    ShutdownNow = true;
                                    return;
                                }
                            }
                        }

                        WriteToLog("Receive thread shutting down");
                        WriteLogQueue();
                    }
                    finally
                    {
                        _receiveAlive = false;
                    }
                });

                _receiveTask.Start();
                WriteToLog("receive thread started");
            }
        }

        private byte[] GetResponse(byte key, int waitTime, DateTime sendTime)
        {
            DateTime start = DateTime.Now;

            while ((DateTime.Now - start).TotalMilliseconds < waitTime)
            {
                if (receivedMessages.ContainsKey(key) && receivedMessages[key].Timestamp >= sendTime)
                    return receivedMessages[key].Bytes;

                Task.Delay(20).Wait(20);
            }


            throw new Exception($"GetResponse timed out after {DateTime.Now - start} waiting for a response with code {key}");
        }

        private int messageThreadLastLine = 0;

        private string messageThreadLastMethod = "";

        private int? messageThreadTaskId = null;

        private void Trace([CallerMemberName] string callingMethod = "", [CallerLineNumber] int callingFileLineNumber = 0)
        {
            messageThreadLastLine = callingFileLineNumber;
            messageThreadLastMethod = callingMethod;
            messageThreadTaskId = Task.CurrentId;
        }

        private void PrintTrace(string message = null, [CallerMemberName] string callingMethod = "")
        {
            WriteToLog(string.IsNullOrEmpty(message) ? $"Entered {callingMethod}" : $"Entered {callingMethod} - {message}");
        }

        private bool inPollingAction = false;

        private DateTime _lastDiagnosticTime;

        private void InitPollingAndLoggingActions()
        {
            pollingAction = () =>
            {
                if (inPollingAction) return;

                inPollingAction = true;

                try
                {
                    float ppm;
                    if (ShutdownNow) return;

                    Trace();
                    CurrentStatus = ReadDataExtended();
                    ppm = (float) CurrentStatus.Ppm;

                    StatusDateTime = Now;

                    if (!IsRunning)
                    {
                        if (CurrentStatus.IsIgnited)
                        {
                            IsRunning = true;
                            Status = "Ignited";
                        }
                    }
                    else
                    {
                        if (!CurrentStatus.IsIgnited)
                        {
                            IsRunning = false;
                            Status = "Connected";
                        }
                    }

                    Dictionary<string, string> properties = new Dictionary<string, string>();

                    Trace();
                    foreach (var phx21StatusProperty in _phx21StatusProperties)
                    {
                        properties[phx21StatusProperty.Name] = phx21StatusProperty.GetValue(CurrentStatus).ToString();
                    }

                    Task.Run(() =>
                    {
                        OnDataPolled(new DataPolledEventArgs(properties, ppm));
                    });
                }
                catch (Exception ex)
                {
                    Trace();
                    WriteExceptionToPhxLog(ex);
                }
                finally
                {
                    inPollingAction = false;
                }
            };

            _fileManager.AppendToFile(LogFilePath, GetHeaderLineForLog());

            loggingAction = () =>
            {
                if (ShutdownNow) return;

                if (CurrentStatus == null)
                    return;

                try
                {
                    _fileManager.AppendToFile(LogFilePath, GetLineForLog(CurrentStatus));
                }
                catch (Exception)
                {
                    // we can't really log it now, can we?
                }

                DateTime now = Now;

                if ((now - _lastDiagnosticTime).TotalSeconds > 5)
                {
                    WriteToLog($"Running for {_inputStream.ConnectedTime}, sent {_outputStream.SendByteCount} b, received {_inputStream.ReceiveByteCount} b");
                    _lastDiagnosticTime = now;
                }

                if (_isPolling && (now - StatusDateTime).TotalMilliseconds > PollingInterval + 2000)
                {
                    WriteToLog($"ERROR: Last parse time was {(now - StatusDateTime)} ago!!! Something's not right! {messageThreadLastMethod} - {messageThreadLastLine} - {messageThreadTaskId}");
                }

                if (_isPolling && (now - StatusDateTime).TotalSeconds > PollingInterval + 10000)
                {
                    WriteToLog($"ERROR: Polling and no status received for {(now - StatusDateTime).TotalSeconds} seconds, polling interval is {PollingInterval} ms. Assuming disconnected.");
                    Task.Run(() => { OnError(new ErrorEventArgs(new Reconnect21NeededException())); });
                    ShutdownNow = true;
                    WriteLogQueue();
                }
            };

            _lastDiagnosticTime = Now;
        }

        public void UseScheduler(RepeatedTaskScheduler scheduler)
        {
            //this.scheduler = scheduler;
        }

        public void StartPollingData()
        {
            StartPollingData(333);
        }

        private int changeCount = 0;

        public void StartPollingData(int intervalInMilliseconds)
        {
            if (_isLoggingConfigured != true)
            {
                throw new Exception("Logging is not configured.  Please call ConfigureLogging before polling data.");
            }

            PrintTrace($"interval = {intervalInMilliseconds}");

            PollingInterval = intervalInMilliseconds;

            if (LoggingInterval < PollingInterval)
            {
                LoggingInterval = PollingInterval;
            }

            PollingTimer = new Timer(PollingTimerCallback, null, PollingInterval, PollingInterval);
            LoggingTimer = new Timer(LoggingTimerCallback, null, LoggingInterval, LoggingInterval);

            _isPolling = true;
        }

        private void PollingTimerCallback(object stateInfo)
        {
            pollingAction();
        }

        private void LoggingTimerCallback(object stateInfo)
        {
            loggingAction();
        }

        /// <summary>
        /// Takes a Phx21Status and determines if the phx21 is ignited
        /// </summary>
        /// <param name="status">The status to check</param>
        /// <returns>A bool representing whether or not the phx21 is ignited</returns>
        private bool CheckIfIgnited(Phx21Status status)
        {
            return status.ThermoCouple > 75 && status.IsSolenoidAOn && status.IsPumpAOn;
        }

        public void StopPollingData()
        {
            PrintTrace();
            try
            {
                if (PollingTimer != null)
                {
                    PollingTimer.Dispose();
                    PollingTimer = null;
                }

                if (LoggingTimer != null)
                {
                    LoggingTimer.Dispose();
                    LoggingTimer = null;
                }
            }
            catch (Exception)
            {
            }

            _isPolling = false;
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

        private bool inWriteLog = false;
        private Timer logWriteTimer = null;

        public void ConfigureLogging(string loggingDirectory, int intervalInMilliseconds)
        {
            _isLoggingConfigured = true;
            LoggingDirectory = loggingDirectory;

            string readingsFilePath = Path.Combine(loggingDirectory, GetFileName());
            string statusFilePath = Path.Combine(loggingDirectory, GetStatusFileName());

            CreateDirectory(loggingDirectory);
            CreateFile(readingsFilePath);
            CreateFile(statusFilePath);

            LogFilePath = readingsFilePath;
            StatusFilePath = statusFilePath;
            LoggingInterval = intervalInMilliseconds;

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
                        inWriteLog = true;
                        WriteLogQueue();
                    }
                    catch (Exception ex)
                    {
                        WriteToLog("Could not write log contents!!!! Requeueing messages");
                        WriteExceptionToPhxLog(ex);
                    }
                    finally
                    {
                        inWriteLog = false;
                    }
                }, null, 250, 250);
            }
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

        private ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> externalMessages = new ConcurrentQueue<string>();

        public void WriteToPhxLog(string contents)
        {
            WriteToLog(contents);
            externalMessages.Enqueue(contents);
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
                _fileManager.AppendToFile(StatusFilePath, batch);
            }
            catch (IOException ex)
            {
                WriteToLog("Could not write log contents!!!! Requeueing messages");
                WriteExceptionToPhxLog(ex);

                logQueue.Enqueue(batch);
            }
        }

        private string GetHeaderLineForLog()
        {
            return "time logged,time received,lph2,BatteryVoltage,ChamberOuterTemp,IsPumpAOn,PpmStr,SamplePressure,TankPressure,ThermoCouple,PumpPower,FIDRange,PicoAmps,RawPpm,IsSolenoidAOn,IsSolenoidBOn,Messages";
        }

        /// <summary>
        /// Takes a Phx21Status and returns some of the parameters in a comma delimites string.
        /// Here's the format:
        /// {0} - current date
        /// {1} - AirPressure
        /// {2} - BatteryVoltage
        /// {3} - ChamberOuterTemp
        /// {4} - IsPumpAOn
        /// {5} - PpmStr
        /// {6} - SamplePressure
        /// {7} - TankPressure
        /// {8} - ThermoCouple
        /// {9} - PumpPower
        /// {10} - FIDRange 
        /// {11} - PicoAmps
        /// </summary>
        /// <param name="status">The status you want broken out into a string</param>
        /// <returns>A comma delimited string of some status fields</returns>
        private string GetLineForLog(Phx21Status status)
        {
            var message =  $"{Now.ToString("MM/dd/yyyy HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)},{status.Timestamp},{status.AirPressure},{status.BatteryVoltage},{status.ChamberOuterTemp},{status.IsPumpAOn},{status.PpmStr},{status.SamplePressure},{status.TankPressure},{status.ThermoCouple},{status.PumpPower},{status.FIDRange},{status.PicoAmps},{status.RawPpm},{status.IsSolenoidAOn},{status.IsSolenoidBOn},{string.Join(",", externalMessages)}";
            
            while (externalMessages.Any())
            {
                externalMessages.TryDequeue(out string a);
            }

            return message;
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

        /// <summary>
        /// Ignites the Phx21
        /// </summary>
        public void IgniteOn()
        {
            Ignite(true);
        }

        public void IgniteOn(bool useSecondaryGlowPlug)
        {
            Ignite(true, useSecondaryGlowPlug ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// Extinguishes the flame of the Phx21
        /// Turns pump A and solenoid A off
        /// </summary>
        public void IgniteOff()
        {
            SetPumpACtrlLoop(false, 0);
            ControlPumpAux1(0, 0, 0);
            ControlSolenoid(0, 0);
        }

        public void SetSolenoidAOn()
        {
            ControlSolenoid(0, 100);
        }

        public void SetSolenoidAOff()
        {
            ControlSolenoid(0, 0);
        }

        public void SetSolenoidBOn()
        {
            ControlSolenoid(1, 100);
        }

        public void SetSolenoidBOff()
        {
            ControlSolenoid(1, 0);
        }

        private void OnDataPolled(DataPolledEventArgs e)
        {
            var handler = DataPolled;
            if (handler != null) handler(this, e);
        }
        
        private Action loggingAction;
        private Action pollingAction;

        /// <summary>
        /// Receives the current status of the Phx21
        /// 
        /// SENDS: CMD_FIDM_READ_DATA_EXTENDED with READ_DATA_PARAMS
        /// RECEIVES: DEFAULT_RESPONSE_EXTENDED which is then passed to GetStatusFromFidmStatusExtended()
        /// to get the Phx21Status that is returned
        /// </summary>
        /// <returns>The current status from a phx21</returns>
        public Phx21Status ReadDataExtended()
        {
            if (!_isLoggingConfigured)
            {
                throw new Exception("Must call StartLogging before reading data.");
            }

            READ_DATA_PARAMS pCmd = new READ_DATA_PARAMS();
            DEFAULT_RESPONSE_EXTENDED Rsp = new DEFAULT_RESPONSE_EXTENDED();

            byte nLength = (byte)Marshal.SizeOf(typeof(READ_DATA_PARAMS));
            byte nCmd = CMD_FIDM_READ_DATA_EXTENDED;

            Trace();

            int tries = 10;

            while (tries > 0)
            {
                tries--;

                try
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    Trace();

                    Rsp = SendAndReceive<DEFAULT_RESPONSE_EXTENDED>(nCmd, GetBytes(pCmd), nLength, nLength, true);

                    Trace();
                    sw.Stop();

                    if (sw.ElapsedMilliseconds > warnTime)
                    {
                        WriteToLog("Warning: ReadDataExtended took " + sw.ElapsedMilliseconds + " milliseconds");
                    }

                    return GetStatusFromFidmStatusExtended(Rsp.status);
                }
                catch (Exception ex)
                {
                    Trace();
                    if (tries == 0) throw;

                    Trace();
                    WriteToLog("Error getting status, retrying");
                    WriteExceptionToPhxLog(ex);
                    Task.Delay(50).Wait();
                }
            }

            throw new Exception("Unable to read status");
        }

        private void SetIntegrationControlParams(byte nMode, byte nChargeMultiplier, byte nRange,
            uint nIntegrationTimeUs,
            byte nSamplesToAvg, byte nReportMode)
        {
            PrintTrace(nMode + ", " + nChargeMultiplier + ", " + nRange + ", " + nIntegrationTimeUs + ", " + nSamplesToAvg + ", " + nReportMode);
            IntegrationControlParams pCmd = new IntegrationControlParams();

            byte nLength = (byte)Marshal.SizeOf(typeof(IntegrationControlParams));
            byte nCmd = CMD_FIDM_INTEGRATION_CONTROL;

            pCmd.nMode = nMode;
            pCmd.nChargeMultiplier = nChargeMultiplier;
            pCmd.nRange = nRange;
            pCmd.nIntegrationTimeUs0 = DwordToByte0(nIntegrationTimeUs);
            pCmd.nIntegrationTimeUs1 = DwordToByte1(nIntegrationTimeUs);
            pCmd.nSamplesToAvg = nSamplesToAvg;
            pCmd.nReportMode = nReportMode;
            
            Stopwatch sw = Stopwatch.StartNew();

            SendAndReceive<FIDM_STATUS>(nCmd, GetBytes(pCmd), nLength, nLength, true);
            
            if (sw.ElapsedMilliseconds > warnTime)
            {
                WriteToLog("Warning: SetIntegrationControlParams took " + sw.ElapsedMilliseconds + " milliseconds");
            }
        }



        /// <summary>
        /// Gets the PPM value calibrated at the given index.
        /// 
        /// SENDS: CMD_FIDM_GET_PPM_CALIBRATION with GetCalibration args
        /// RECEIVES: GetCalibrationResponse
        /// 
        /// The GetCalibrationResponse object is used to create PpmCalibrationInfo that is returned
        /// </summary>
        /// <param name="index">the calibration index you wish to receive</param>
        /// <returns>Calibration info</returns>
        public PpmCalibrationInfo GetPpmCalibration(int index)
        {
            PrintTrace();
            GetCalibration Cmd = new GetCalibration();
            GetCalibrationResponse Rsp;

            byte nLength = (byte)Marshal.SizeOf(typeof(GetCalibration));
            byte nCmd = CMD_FIDM_GET_PPM_CALIBRATION;

            Cmd.index_number = (byte)index;
            
            Stopwatch sw = Stopwatch.StartNew();

            Rsp = SendAndReceive<GetCalibrationResponse>(nCmd, GetBytes(Cmd), nLength, nLength, true);

            if (sw.ElapsedMilliseconds > warnTime)
            {
                WriteToLog("Warning: GetPpmCalibration took " + sw.ElapsedMilliseconds + " milliseconds");
            }

            return new PpmCalibrationInfo
            {
                Index = Rsp.index_number,
                Ppm = (int)(0.1 * Rsp.ppm_tenths),
                FidCurrent = Rsp.fid_current_tPa,
                H2Pressure = Rsp.H2_pressure_hPSI / 100.0f,
                IsValid = Rsp.valid > 0
            };
        }

        /// <summary>
        /// Convienience function for SetPpmCalibration with more args.
        /// Gets the pico amps and pressure from the current status, so if you
        /// use this make sure CurrentStatus is up to date.
        /// </summary>
        /// <param name="indexNumber">This is the calibration index, there are 6</param>
        /// <param name="ppmTenths">Tenth of ppm</param>
        public void SetPpmCalibration(int indexNumber, int ppmTenths)
        {
            SetPpmCalibration(indexNumber, ppmTenths, (int)(CurrentStatus.PicoAmps * 10),
                (ushort)(CurrentStatus.TankPressure * 10), true);
        }

        /// <summary>
        /// Sets a specific calibration slot. In general it is easier to use GeneratePpmCalibration() 
        /// or the SetPpmCalibration with less args.
        /// 
        /// SENDS: CMD_FIDM_SET_PPM_CALIBRATION with SetCalibration args
        /// RECEIVES: FIDM_STATUS. Response is ignored.
        /// </summary>
        /// <param name="indexNumber">This is the calibration index, there are 6</param>
        /// <param name="ppmTenths">Tenth of ppm</param>
        /// <param name="picoampsTenths">Tenth of pico amps</param>
        /// <param name="H2Pressure"></param>
        /// <param name="overwrite"></param>
        public void SetPpmCalibration(int indexNumber, int ppmTenths, int picoampsTenths, ushort H2Pressure,
            bool overwrite)
        {
            PrintTrace();
            SetCalibration cmd = new SetCalibration();

            byte nLength = (byte)Marshal.SizeOf(typeof(SetCalibration));
            byte nCmd = CMD_FIDM_SET_PPM_CALIBRATION;

            cmd.index_number = (byte)indexNumber;
            cmd.ppm_tenths = ppmTenths;
            cmd.fid_current_tPa = picoampsTenths;
            cmd.H2_pressure_hPSI = H2Pressure;
            cmd.overwrite = overwrite ? (byte)1 : (byte)0;
            
            Stopwatch sw = Stopwatch.StartNew();

            SendAndReceive<FIDM_STATUS>(nCmd, GetBytes(cmd), nLength, nLength, true);

            if (sw.ElapsedMilliseconds > warnTime)
            {
                WriteToLog("Warning: SetPpmCalibration took " + sw.ElapsedMilliseconds + " milliseconds");
            }
        }

        //This does not work in the firmware...
        public void SetBluetoothWatchdog(bool enabled, int supervisionTimeout, int trafficLostTimeout)
        {
            PrintTrace();
            BlueToothWatchdogSettings cmd = new BlueToothWatchdogSettings();

            byte cmdSize = (byte)Marshal.SizeOf(typeof(BlueToothWatchdogSettings));
            byte cmdId = CMD_FIDM_SET_BT_WATCHDOG;

            cmd.nSupervisionTimeoutS = (byte)supervisionTimeout;
            cmd.nTrafficLostForcedDisconnectEnable = (enabled ? (byte) 1 : (byte) 0);
            cmd.nTrafficLostForcedDisconnectTimeoutS = (byte)trafficLostTimeout;
            
            TransmitSerialCmd(cmdId, GetBytes(cmd), cmdSize, cmdSize, true);
        }

        //This is a new command in v109, 108 seems to ignore it
        public void SendGoodbye()
        {
            PrintTrace();
            READ_DATA_PARAMS cmd = new READ_DATA_PARAMS();

            byte cmdSize = (byte)Marshal.SizeOf(typeof(READ_DATA_PARAMS));
            byte cmdId = CMD_FIDM_GOODBYE;

            _goodbyeSent = true;
            TransmitSerialCmd(cmdId, GetBytes(cmd), cmdSize, cmdSize, true);


            //This was a failed attempt to put the bt chip on the 21 in command mode and tell it to reset.... didn't work
            //byte[] cmdMode = new []{(byte)'+', (byte)'+' , (byte)'+' };
            //byte[] reset = new[] { (byte)'A', (byte)'T', (byte)'Z', (byte)'\r' };

            //TransmitSerialData(0, cmdMode, (byte)cmdMode.Length, false);
            //Task.Delay(500).Wait();
            //TransmitSerialData(0, reset, (byte)reset.Length, false);
        }

        /// <summary>
        /// Generates a PPM Calibration in the Phx given the ppm of the gas that is currently being processed.
        /// General use is to clear the calibrations, then use this function to generate the calibration when
        /// the different gasses are applied.
        /// 
        /// SENDS: A CMD_FIDM_GENERATE_PPM_CALIBRATION command with GenerateCalibration params
        /// RECEIVES: A FIDM_STATUS response. The response is ignored.
        /// </summary>
        /// <param name="ppmtenths">The PPM of the gas currently being processed</param>
        public void GeneratePpmCalibration(int ppmtenths)
        {
            PrintTrace(ppmtenths.ToString());
            GenerateCalibration cmd = new GenerateCalibration();

            byte nLength = (byte)Marshal.SizeOf(typeof(GenerateCalibration));
            byte nCmd = CMD_FIDM_GENERATE_PPM_CALIBRATION;

            cmd.ppm_tenths = ppmtenths; //to get tenths
            cmd.spare_for_alignment = 0;
            
            Stopwatch sw = Stopwatch.StartNew();

            SendAndReceive<FIDM_STATUS>(nCmd, GetBytes(cmd), nLength, nLength, true);

            if (sw.ElapsedMilliseconds > warnTime)
            {
                WriteToLog("Warning: GeneratePpmCalibration took " + sw.ElapsedMilliseconds + " milliseconds");
            }
        }

        public void TurnOnPumpToTargetPressure(double targetPressure)
        {
            SetPumpACtrlLoop(true, (long)(targetPressure * 100));
        }

        public void TurnOffPump()
        {
            SetPumpACtrlLoop(false, 0);
            ControlPumpAux1(0, 0, 0);
        }

        public void IgniteGlowPlug1(int durationInSeconds)
        {
            IgniteGlowPlug(0, (byte)durationInSeconds);
        }

        public void IgniteGlowPlug2(int durationInSeconds)
        {
            IgniteGlowPlug(1, (byte)durationInSeconds);
        }

        public void SetDeadHeadParams(bool enabled, ushort pressureLimit, ushort timeout)
        {
            PrintTrace();
            DeadheadParams cmd = new DeadheadParams();

            byte nLength = (byte)Marshal.SizeOf(typeof(DeadheadParams));
            byte nCmd = CMD_FIDM_SET_DEADHEAD_PARAMS;

            cmd.enable = enabled ? (byte)1 : (byte)0;
            cmd.pressure_low_limit_hPSI = pressureLimit;
            cmd.max_duration_msec = timeout;
            
            Stopwatch sw = Stopwatch.StartNew();

            SendAndReceive<FIDM_STATUS>(nCmd, GetBytes(cmd), nLength, nLength, true);
            
            if (sw.ElapsedMilliseconds > warnTime)
            {
                WriteToLog("Warning: SetDeadHeadParams took " + sw.ElapsedMilliseconds + " milliseconds");
            }
        }

        /// <summary>
        /// Sets the LPH2 compensation.
        /// 
        /// SENDS: CMD_FIDM_SET_CAL_H2PRES_COMPENSATION with CalH2PressureCompensation params
        /// RECEIVES: FIDM_STATUS. Response is ignored.
        /// </summary>
        /// <param name="h2CompensationPos">LPH2 positive compensation</param>
        /// <param name="h2CompensationNeg">LPH2 negative compensation</param>
        public void SetCalH2PressureCompensation(long h2CompensationPos, long h2CompensationNeg)
        {
            PrintTrace();
            CalH2PressureCompensation cmd = new CalH2PressureCompensation();

            byte nLength = (byte) Marshal.SizeOf(typeof(CalH2PressureCompensation));
            byte nCmd = CMD_FIDM_SET_CAL_H2PRES_COMPENSATION;

            cmd.H2_compensation_pos = h2CompensationPos;
            cmd.H2_compensation_neg = h2CompensationNeg;
            cmd.spare_for_alignment = 0;
            
            Stopwatch sw = Stopwatch.StartNew();

            SendAndReceive<FIDM_STATUS>(nCmd, GetBytes(cmd), nLength, nLength, true);

            if (sw.ElapsedMilliseconds > warnTime)
            {
                WriteToLog("Warning: SetCalH2PressureCompensation took " + sw.ElapsedMilliseconds + " milliseconds");
            }
        }

        public void ReadConfiguration()
        {
            throw new NotImplementedException();
        }

        private void ControlPumpAux1(byte nId, uint nPowerLevelTenthsPercent, byte nKickStartDurationSec)
        {
            PumpAux1ControlParams pCmd = new PumpAux1ControlParams();

            byte nLength = (byte)Marshal.SizeOf(typeof(PumpAux1ControlParams));
            byte nCmd = CMD_FIDM_PUMP_AUX_1_CONTROL;

            pCmd.nID = nId;
            pCmd.nPowerTenthsPercent0 = DwordToByte0(nPowerLevelTenthsPercent);
            pCmd.nPowerTenthsPercent1 = DwordToByte1(nPowerLevelTenthsPercent);
            pCmd.nKickStartDurationSec = nKickStartDurationSec;
            
            WriteToLog("ControlPumpAux1");
            TransmitSerialCmd(nCmd, GetBytes(pCmd), nLength, nLength, true);
        }

        /// <summary>
        /// This function gets the firmware version from the phx21.
        /// 
        /// SENDS: CMD_FIDM_CONFIGURATION_READ command with ConfigurationReadParams
        /// RECEIVES: a ConfigurationResponse. The version is a field in ConfigurationResponse
        /// </summary>
        /// <returns>The firmware version number</returns>
        public string GetFirmwareVersion()
        {
            PrintTrace();
            ConfigurationReadParams pCmd = new ConfigurationReadParams();

            byte nLength = (byte)Marshal.SizeOf(typeof(ConfigurationReadParams));
            byte nCmd = CMD_FIDM_CONFIGURATION_READ;

            int sanity = 0;

            ConfigurationResponse response = new ConfigurationResponse();
            
            while (sanity < 10)
            {
                WriteToLog("GetFirmwareVersion try #" + sanity);
                sanity++;
                Stopwatch sw = Stopwatch.StartNew();

                response = SendAndReceive<ConfigurationResponse>(nCmd, GetBytes(pCmd), nLength, nLength, true, longTimeout);

                if (sw.ElapsedMilliseconds > warnTime)
                {
                    WriteToLog("Warning: GetFirmwareVersion took " + sw.ElapsedMilliseconds + " milliseconds");
                }

                WriteToLog($"Firmware version: {response.nVersion.ToString()}");

                return response.nVersion.ToString();
            }

            throw new Exception("Unable to read version");
        }

        /// <summary>
        /// This function is used to control the 2 solenoids
        /// 
        /// SENDS: CMD_FIDM_SOLENOID_CONTROL with SolenoidControlParams
        /// 
        /// Does not wait for a response, but methinks maybe it should?
        /// </summary>
        /// <param name="nId">Valid values are 0 for solenoid A and 1 for solenoid B</param>
        /// <param name="nPowerLevelPercent">The power level is usually set to 0 (off) or 100 (for all the way)</param>
        private void ControlSolenoid(byte nId, byte nPowerLevelPercent)
        {
            SolenoidControlParams pCmd = new SolenoidControlParams();

            byte nLength = (byte)Marshal.SizeOf(typeof(SolenoidControlParams));
            byte nCmd = CMD_FIDM_SOLENOID_CONTROL;

            pCmd.nID = nId;
            pCmd.nPower = nPowerLevelPercent;
            
            WriteToLog($"ControlSolenoid #{nId}, level {nPowerLevelPercent}");
            TransmitSerialCmd(nCmd, GetBytes(pCmd), nLength, nLength, true);
        }

        private void SetPumpACtrlLoop(bool enable, long target)
        {
            PumpClosedLoop Cmd = new PumpClosedLoop();

            byte nLength = (byte)Marshal.SizeOf(typeof(PumpClosedLoop));
            byte nCmd = CMD_FIDM_SET_PUMPA_CLOSED_LOOP;

            Cmd.enable = enable ? (byte)1 : (byte)0;
            Cmd.target_hPSI = (short)target;
            
            WriteToLog("SetPumpACtrlLoop");
            TransmitSerialCmd(nCmd, GetBytes(Cmd), nLength, nLength, true);
        }

        private void WriteFlash(uint startingAddress, uint nCount, byte[] data)
        {
            FlashWriteParams pCmd = new FlashWriteParams();

            byte nCmdLength = (byte)(Marshal.SizeOf(typeof(FlashWriteParams)) + MAX_FLASH_BYTES_PER_OP);
            byte nHeaderLength = (byte)Marshal.SizeOf(typeof(FlashWriteParams));

            byte nCRC = 0;
            byte nCmd = CMD_FIDM_FLASH_WRITE;

            uint nMaxNumCycles = nCount / MAX_FLASH_BYTES_PER_OP;
            byte nLastNumBytesToWrite = (byte)(nCount - (MAX_FLASH_BYTES_PER_OP * nMaxNumCycles));

            int currentProgress = 0;
            int progressInterval = nMaxNumCycles == 0 ? 0 : 100 / (int)nMaxNumCycles;

            FIDM_STATUS pStatus = new FIDM_STATUS();
            bool receiveSuccess = false;
            int tryCount = 0;

            for (int ii = 0; ii < nMaxNumCycles; ii++)
            {
                pCmd.nStartingAddress0 = DwordToByte0((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * ii)));
                pCmd.nStartingAddress1 = DwordToByte1((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * ii)));
                pCmd.nStartingAddress2 = DwordToByte2((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * ii)));
                pCmd.nStartingAddress3 = DwordToByte3((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * ii)));
                pCmd.nCount = MAX_FLASH_BYTES_PER_OP;

                tryCount = 0;
                    
                try
                {
                    TryUtils.Retry(() =>
                    {
                        WriteToLog($"WriteFlash {ii} of {nMaxNumCycles} try {tryCount}");
                        DateTime sendTime = Now;
                        nCRC = TransmitSerialCmd(nCmd, GetBytes(pCmd), nCmdLength, nHeaderLength, false);
                        // Don't send the CRC.

                        byte[] datatosend = data.Skip(MAX_FLASH_BYTES_PER_OP * ii).ToArray();
                        TransmitSerialData(nCRC, datatosend, MAX_FLASH_BYTES_PER_OP, true); // Send the CRC.

                        pStatus = ReceiveCmdResponse<FIDM_STATUS>(nCmd, 2000, sendTime, out receiveSuccess);

                        tryCount++;

                        if (receiveSuccess)
                        {
                            WriteToLog($"WriteFlash {ii} of {nMaxNumCycles} try {tryCount} write success");
                            byte[] readdata = ReadFlash((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * ii)), MAX_FLASH_BYTES_PER_OP);

                            for (int j = 0; j < MAX_FLASH_BYTES_PER_OP; j++)
                            {
                                if (readdata[j] != datatosend[j])
                                {
                                    receiveSuccess = false;
                                    string message = $"WriteFlash {ii} of {nMaxNumCycles} try {tryCount} data read does not match data written";
                                    WriteToLog(message);
                                    throw new Exception(message);
                                }
                            }

                            WriteToLog($"WriteFlash {ii} of {nMaxNumCycles} try {tryCount} data verified");
                        }

                        if (!receiveSuccess)
                        {
                            throw new Exception("Unable to write flash data to phx21");
                        }
                    }, 10, 100);
                }
                catch (Exception)
                {
                    WriteToLog($"WriteFlash {ii} of {nMaxNumCycles} failed after {tryCount} times");
                    throw;
                }

                currentProgress = (int)((100.00 / (double)nMaxNumCycles) * ii);
                OnWriteFlashProgress(currentProgress);
            }

            nCmdLength = (byte)(Marshal.SizeOf(typeof(FlashWriteParams)) + nLastNumBytesToWrite);

            pCmd.nStartingAddress0 = DwordToByte0((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * nMaxNumCycles)));
            pCmd.nStartingAddress1 = DwordToByte1((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * nMaxNumCycles)));
            pCmd.nStartingAddress2 = DwordToByte2((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * nMaxNumCycles)));
            pCmd.nStartingAddress3 = DwordToByte3((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * nMaxNumCycles)));
            pCmd.nCount = nLastNumBytesToWrite;

            receiveSuccess = false;
            tryCount = 0;

            try
            {
                TryUtils.Retry(() =>
                {
                    WriteToLog("WriteFlash remainder try " + tryCount);
                    nCRC = TransmitSerialCmd(nCmd, GetBytes(pCmd), nCmdLength, nHeaderLength, false);
                    // Don't send the CRC.
                    byte[] datatosend = data.Skip((int)(MAX_FLASH_BYTES_PER_OP * nMaxNumCycles)).ToArray();
                    DateTime sendTime = Now;
                    TransmitSerialData(nCRC, datatosend, nLastNumBytesToWrite, true); // Send the CRC.

                    pStatus = ReceiveCmdResponse<FIDM_STATUS>(nCmd, longTimeout, sendTime, out receiveSuccess);
                    if (!receiveSuccess) Task.Delay(25).Wait();
                    tryCount++;

                    if (receiveSuccess)
                    {
                        WriteToLog($"WriteFlash remainder  try {tryCount} write success");
                        byte[] readdata = ReadFlash((startingAddress + (MAX_FLASH_BYTES_PER_OP * nMaxNumCycles)), nLastNumBytesToWrite);

                        for (int j = 0; j < nLastNumBytesToWrite; j++)
                        {
                            if (readdata[j] != datatosend[j])
                            {
                                string message = $"WriteFlash remainder data read does not match data written try {tryCount}";
                                WriteToLog(message);
                                receiveSuccess = false;
                                throw new Exception(message);
                            }
                        }

                        WriteToLog($"WriteFlash remainder try {tryCount} data verified");
                    }

                    if (!receiveSuccess)
                    {
                        throw new Exception("Unable to write flash data to phx21");
                    }
                }, 10, 100);
            }
            catch (Exception)
            {
                WriteToLog($"WriteFlash remainder failed after trying {tryCount} times");
                throw;
            }

            OnWriteFlashProgress(100);
            OnWriteFlashComplete();
        }

        public byte[] ReadDataFromStoredLength(uint startingAddress)
        {
            byte[] lenBytes = ReadFlash(startingAddress, 4);

            int len = BytesToDword(lenBytes[3], lenBytes[2], lenBytes[1], lenBytes[0]);

            if (len < 0 || len > 100000) throw new Exception($"Bad message size: {len}"); 

            return ReadData(startingAddress + 4, (uint)len);
        }

        public byte[] ReadData(uint startingAddress, uint count)
        {
            byte[] compressedBytes = ReadFlash(startingAddress, count).ToArray();

            return compressedBytes;
        }
        
        private byte[] ReadFlash(uint startingAddress, uint nCount)
        {
            FlashReadParams pCmd = new FlashReadParams();

            byte nLength = (byte)Marshal.SizeOf(typeof(FlashReadParams));
            byte nCmd = CMD_FIDM_FLASH_READ;

            List<byte> data = new List<byte>();

            uint nMaxNumCycles = nCount / MAX_FLASH_BYTES_PER_OP;
            byte nLastNumBytesToWrite = (byte)(nCount - (MAX_FLASH_BYTES_PER_OP * nMaxNumCycles));

            DefaultResponse defResponse = new DefaultResponse();
            int defResponseSize = Marshal.SizeOf(typeof(DefaultResponse));

            int currentProgress = 0;
            int trycount = 0;
            
            for (int ii = 0; ii < nMaxNumCycles; ii++)
            {
                pCmd.nStartingAddress0 = DwordToByte0((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * ii)));
                pCmd.nStartingAddress1 = DwordToByte1((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * ii)));
                pCmd.nStartingAddress2 = DwordToByte2((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * ii)));
                pCmd.nStartingAddress3 = DwordToByte3((uint)(startingAddress + (MAX_FLASH_BYTES_PER_OP * ii)));
                pCmd.nCount = MAX_FLASH_BYTES_PER_OP;

                trycount = 0;

                try
                {
                    TryUtils.Retry(() =>
                    {
                        defResponse = SendAndReceive<DefaultResponse>(nCmd, GetBytes(pCmd), nLength, nLength, true, longTimeout);
                    }, 5, 100);
                }
                catch (Exception ex)
                {
                    WriteToLog($"Error getting flash contents after {trycount} tries");
                    WriteExceptionToPhxLog(ex);
                    throw;
                }

                byte[] bytes = receivedMessages[nCmd].Bytes;

                data.AddRange(bytes.Skip(defResponseSize).Take(MAX_FLASH_BYTES_PER_OP));
                
                currentProgress = (int)((100.00 / (double)nMaxNumCycles) * ii);
                OnReadFlashProgress(currentProgress);
            }

            if (nLastNumBytesToWrite > 0)
            {
                pCmd.nStartingAddress0 = DwordToByte0((uint) (startingAddress + (MAX_FLASH_BYTES_PER_OP * nMaxNumCycles)));
                pCmd.nStartingAddress1 = DwordToByte1((uint) (startingAddress + (MAX_FLASH_BYTES_PER_OP * nMaxNumCycles)));
                pCmd.nStartingAddress2 = DwordToByte2((uint) (startingAddress + (MAX_FLASH_BYTES_PER_OP * nMaxNumCycles)));
                pCmd.nStartingAddress3 = DwordToByte3((uint) (startingAddress + (MAX_FLASH_BYTES_PER_OP * nMaxNumCycles)));
                pCmd.nCount = nLastNumBytesToWrite;

                trycount = 0;

                try
                {
                    TryUtils.Retry(() =>
                    {
                        defResponse = SendAndReceive<DefaultResponse>(nCmd, GetBytes(pCmd), nLength, nLength, true, longTimeout);
                    }, 5, 100);
                }
                catch (Exception ex)
                {
                    WriteToLog($"Error getting flash contents after {trycount} tries");
                    WriteExceptionToPhxLog(ex);
                    throw;
                }

                byte[] bytes = receivedMessages[nCmd].Bytes;

                data.AddRange(bytes.Skip(defResponseSize).Take(nLastNumBytesToWrite));
            }

            OnReadFlashProgress(100);
            OnReadFlashComplete();

            return data.ToArray();
        }

        private void TransmitSerialData(byte nCurrChkSum, byte[] pBuffer, byte nLength, bool bSendCRC)
        {
            byte nCRC = ReComputeCRC(nCurrChkSum, pBuffer, nLength);

            sendMessages.Enqueue(new BluetoothMessage
            {
                Bytes = pBuffer,
                Offest = 0,
                Length = nLength
            });

            if (bSendCRC)
            {
                sendMessages.Enqueue(new BluetoothMessage
                {
                    Bytes = new byte[] { nCRC },
                    Offest = 0,
                    Length = 1
                });
            }
        }

        private byte ReComputeCRC(byte nCurrChkSum, byte[] pStream, byte nLength)
        {
            for (int ii = 0; ii < nLength; ii++)
            {
                nCurrChkSum = (byte)((nCurrChkSum << 1) | (nCurrChkSum >> 7));
                nCurrChkSum += pStream[ii];
            }

            return nCurrChkSum;
        }

        private void IgniteGlowPlug(byte nId, byte nDurationSec)
        {
            IgniteParams pCmd = new IgniteParams();

            byte nLength = (byte)Marshal.SizeOf(typeof(IgniteParams));
            byte nCmd = CMD_FIDM_IGNITE_PULSE;

            pCmd.nID = nId;
            pCmd.nDurationSec = nDurationSec;
            
            WriteToLog("IgniteGlowPlug");
            TransmitSerialCmd(nCmd, GetBytes(pCmd), nLength, nLength, true);
        }

        private void SetSamplingParameters(byte nFIDMRange)
        {
            PrintTrace(nFIDMRange.ToString());
            SetSamplingParams pCmd = new SetSamplingParams();

            byte nLength = (byte)Marshal.SizeOf(typeof(SetSamplingParams));
            byte nCmd = CMD_FIDM_SET_SAMPLING_PARAMETERS;

            pCmd.nRange = nFIDMRange;
            
            Stopwatch sw = Stopwatch.StartNew();

            SendAndReceive<FIDM_STATUS>(nCmd, GetBytes(pCmd), nLength, nLength, true);

            if (sw.ElapsedMilliseconds > warnTime)
            {
                WriteToLog("Warning: SetSamplingParameters took " + sw.ElapsedMilliseconds + " milliseconds");
            }
        }

        private int num0s = 0;
        private int ignitedChagedCount = 0;
        private bool prevIgnite = false;
        private bool firstIgniteCheck = true;

        /// <summary>
        /// Takes a FIDM_STATUS_EXTENDED and creates a Phx21Status from it.
        /// 
        /// This is where junk data is filtered:
        /// if ((phx21Status.BatteryVoltage &gt; 15 || phx21Status.PicoAmps &lt; -10000 || phx21Status.ThermoCouple &lt; -400) and junkDataCount &lt; 10)
        /// its junk and will try to read status again.
        /// 
        /// This is also where it is determined if a phx21 is ignited if CheckIfIgnited() for this status and the last 3 status indicate ignition.
        /// 
        /// PPM ranging also happens in this function (wow, there's lots of fun stuff in here, huh?)
        /// 
        /// And ppm value averaging happens here as well
        /// 
        /// Check for pump power level > 85% is here too, shuts off pump
        /// 
        /// </summary>
        /// <param name="status">The status to convert</param>
        /// <returns>a Phx21Status from the status passed in</returns>
        private Phx21Status GetStatusFromFidmStatusExtended(FIDM_STATUS_EXTENDED status)
        {
            double ppm =
                Math.Round(
                    0.1f *
                    BytesToDword(status.nFIDTenthsPPM3, status.nFIDTenthsPPM2, status.nFIDTenthsPPM1,
                        status.nFIDTenthsPPM0), 1);

            if (ppm >= 100)
                ppm = Math.Round(ppm, 0);

            if (ppm < 0)
                ppm = 0;

            if (ppm == 0)
            {
                num0s++;

                if (num0s > 5)
                {
                    num0s = -5;
                }

                if (num0s < 0)
                {
                    ppm = 0.1;
                }
            }

            Trace();
            var phx21Status = new Phx21Status
            {
                IsPumpAOn = (status.nStatusFlags & STATUS_PUMP_A_ON) > 0,
                AirPressure = BytesToWord(status.nAirPressure_HPSI1, status.nAirPressure_HPSI0) / 100.0f,
                BatteryVoltage = BytesToWord(status.nBatt_mV1, status.nBatt_mV0) / 1000.0f,
                ChamberOuterTemp =
                    ConvertKelvinToFahrenheit(BytesToWord(status.nChamberOuterTemp_TK1, status.nChamberOuterTemp_TK0) /
                                              10.0f),
                RawPpm = ppm,
                SamplePressure = BytesToWord(status.nSamplePressure_HPSI1, status.nSamplePressure_HPSI0) / 100.0f,
                TankPressure = 10.0f * (BytesToWord(status.nH2Pressure_PSI1, status.nH2Pressure_PSI0) / 10),
                //this is copied... losing a fraction looks intentional
                ThermoCouple =
                    ConvertKelvinToFahrenheit(BytesToWord(status.nThermocouple_TK1, status.nThermocouple_TK0) / 10.0f),
                PicoAmps =
                    (double)
                    BytesToDword(status.nFIDTenthsPicoA_In13, status.nFIDTenthsPicoA_In12,
                        status.nFIDTenthsPicoA_In11, status.nFIDTenthsPicoA_In10) / (double)10.0,
                SystemCurrent = BytesToWord(status.nSystemCurrentMa1, status.nSystemCurrentMa0),
                PumpPower = status.nPumpA_power_pct,
                IsSolenoidAOn = (status.nStatusFlags & STATUS_SOLENOID_A_ON) > 0,
                IsSolenoidBOn = (status.nStatusFlags & STATUS_SOLENOID_B_ON) > 0,
                FIDRange = status.nFIDRange,
                Timestamp = Now.ToString("MM/dd/yyyy HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)
            };

            Trace();
            //check for ignition
            bool isIgnited = CheckIfIgnited(phx21Status);

            Trace();
            if (!firstIgniteCheck)
            {
                if (isIgnited != prevIgnite)
                {
                    ignitedChagedCount++;

                    if (ignitedChagedCount >= 3)
                    {
                        WriteToLog((isIgnited ? "Igited!" : "Extinguished"));
                        prevIgnite = isIgnited;
                    }
                }
                else
                {
                    ignitedChagedCount = 0;
                }

                phx21Status.IsIgnited = prevIgnite;
            }
            else
            {
                WriteToLog($"First status: {(isIgnited ? "Igited!" : "Not Ignited")}");
                firstIgniteCheck = false;
                prevIgnite = isIgnited;
                phx21Status.IsIgnited = isIgnited;
            }

            Trace();
            //Check for junk data
            //Reread if junk data
            if ((phx21Status.BatteryVoltage > 15 || phx21Status.PicoAmps < -10000 || phx21Status.ThermoCouple < -400 || phx21Status.PumpPower > 100) && junkDataCount < 10)
            {
                WriteToLog("Suspect data #" + junkDataCount + " received. Suspect status follows. Retrying.");
                WriteToLog(GetLineForLog(phx21Status));
                Task.Delay(10).Wait();
                junkDataCount++;
                Trace();
                throw new Exception("Suspect data received!");
            }

            junkDataCount = 0;

            Trace();
            if (phx21Status.IsIgnited && phx21Status.PumpPower >= 85.0)
            {
                WriteToLog("Pump power is above 85% (" + phx21Status.PumpPower + "%), shutting off pump!");
                TurnOffPump();
                OnError(new ErrorEventArgs(new Exception("Pump power is above 85% (" + phx21Status.PumpPower + "%)")));
            }

            //This is where the ppm range is switched
            if (phx21Status.FIDRange == RANGE_MODE_0_LO && phx21Status.PicoAmps >= 6500)
            {
                changeCount++;

                if (changeCount >= 1)
                {
                    changeCount = 0;
                    SetSamplingParameters(RANGE_MODE_3_MAX);
                    Task.Delay(250).Wait();
                }

            }
            else if (phx21Status.FIDRange == RANGE_MODE_3_MAX && phx21Status.PicoAmps <= 6000)
            {
                changeCount++;

                if (changeCount >= 1)
                {
                    changeCount = 0;
                    SetSamplingParameters(RANGE_MODE_0_LO);
                    Task.Delay(250).Wait();
                }
            }

            pastPpms.Enqueue(phx21Status.RawPpm);

            double disregard;
            while (pastPpms.Count > maxPastPpms)
                pastPpms.TryDequeue(out disregard);

            //apply averaging to the ppm value
            phx21Status.LongAveragePpm = pastPpms.Skip(Math.Max(pastPpms.Count - LongAverageCount, 0)).Average();

            phx21Status.LongAveragePpm = phx21Status.LongAveragePpm >= 100
                ? Math.Round(phx21Status.LongAveragePpm, 1)
                : Math.Round(phx21Status.LongAveragePpm, 0);

            Trace();
            var shortAveragePpms = pastPpms.Skip(Math.Max(pastPpms.Count - ShortAverageCount, 0)).ToArray();

            phx21Status.ShortAveragePpm = shortAveragePpms.Average();

            phx21Status.ShortAveragePpm = phx21Status.ShortAveragePpm >= 100
                ? Math.Round(phx21Status.ShortAveragePpm, 0)
                : Math.Round(phx21Status.ShortAveragePpm, 1);

            phx21Status.UseAverage = shortAveragePpms
                .All(p => ((p / phx21Status.LongAveragePpm) * 100 >= 100 - UseAvgPerc
                           && (p / phx21Status.LongAveragePpm) * 100 <= 100 + UseAvgPerc));

            Trace();
            if (phx21Status.UseAverage)
            {
                phx21Status.Ppm = phx21Status.FIDRange == RANGE_MODE_3_MAX ? phx21Status.LongAveragePpm : phx21Status.ShortAveragePpm;
            }
            else
            {
                phx21Status.Ppm = phx21Status.RawPpm;
            }

            phx21Status.PpmStr = phx21Status.IsIgnited ? phx21Status.Ppm.ToString() : "N/A";

            Trace();
            if (phx21Status.PicoAmps <= 100 && _currentHardwareAvg == 10)
            {
                _currentHardwareAvg = 50;
                SetIntegrationControlParams(0, 1, 7, 50000, _currentHardwareAvg, 0);

            }
            else if (phx21Status.PicoAmps > 100 && _currentHardwareAvg == 50)
            {
                _currentHardwareAvg = 10;
                SetIntegrationControlParams(0, 1, 7, 50000, _currentHardwareAvg, 0);
            }

            Trace();
            WriteToLog($"Received a status message: ppm {phx21Status.PpmStr}, raw ppm {phx21Status.RawPpm}, pA {phx21Status.PicoAmps}, {(phx21Status.IsIgnited ? "ignited" : "not ignited")}");
            Trace();
            return phx21Status;
        }

        private ConcurrentQueue<double> pastPpms = new ConcurrentQueue<double>();
        private int maxPastPpms = 50;

        private float ConvertKelvinToFahrenheit(float kelvin)
        {
            return (float)Math.Round((kelvin - 273.15f) * 1.8f + 32, 1);
        }

        private byte DwordToByte0(uint dword)
        {
            return (byte)(0xFF & (dword));
        }

        private byte DwordToByte1(uint dword)
        {
            return (byte)(0xFF & ((dword) >> 8));
        }

        private byte DwordToByte2(uint dword)
        {
            return (byte)(0xFF & ((dword) >> 16));
        }

        private byte DwordToByte3(uint dword)
        {
            return (byte)(0xFF & ((dword) >> 24));
        }

        private int BytesToWord(byte b1, byte b0)
        {
            return (0xFFFF & ((int)((b1) << 8)) | ((int)(b0)));
        }

        private int BytesToDword(byte b3, byte b2, byte b1, byte b0)
        {
            return (int)(0xFFFFFFFF & (((int)((b3) << 24)) | ((int)((b2) << 16)) | ((int)((b1) << 8)) | (int)(b0)));
        }

        private void Ignite(bool onOff)
        {
            Ignite(onOff, 0);
        }

        /// <summary>
        /// Ignites the phx21
        /// SENDS: CMD_FIDM_AUTO_IGNITION_SEQUENCE with AUTO_IGNITION_SEQUENCE args
        /// RECEIVES: FIDM_STATUS. Response is ignored.
        /// </summary>
        /// <param name="onOff">true to ignite, false to extinguish - extinguish doesn't seem to be used, call IgniteOff() instead</param>
        /// <param name="glowplug">true to use glow plug B, false to use glow plug A</param>
        private void Ignite(bool onOff, byte glowplug)
        {
            PrintTrace(onOff + ", " + glowplug);
            externalMessages.Enqueue($"Ignite {(onOff ? "ON" : "OFF")} glowplug {glowplug}");
            byte nCmd = CMD_FIDM_AUTO_IGNITION_SEQUENCE;

            var ignition = BuildAutoIgnitionSequence();
            ignition.use_glow_plug_b = glowplug;
            ignition.start_stop = (byte)(onOff ? 1 : 0);

            var bytes = GetBytes(ignition);
            byte nLength = (byte)bytes.Length;
            
            Stopwatch sw = Stopwatch.StartNew();

            TransmitSerialCmd(nCmd, bytes, nLength, nLength, true);
            //this could be send and receive since the phx does send a response, but we don't care about it and it takes a really long time
                
            if (sw.ElapsedMilliseconds > warnTime)
            {
                WriteToLog("Warning: Ignite took " + sw.ElapsedMilliseconds + " milliseconds");
            }
        }

        public void WriteData(byte[] bytes)
        {
            WriteData(0, bytes);
        }

        public void WriteDataWithLength(int startingAddress, byte[] bytes)
        {
            // hmm, doesn't really seem compressed...
            byte[] compressedBytes;

            compressedBytes = bytes;

            // get file length
            uint len = (uint)compressedBytes.Length;

            if (len == 0)
            {
                return;
            }

            // allocate buffer
            byte[] buffer = new byte[4 + len]; // add 4 bytes for length prefix
            buffer[0] = DwordToByte0(len);
            buffer[1] = DwordToByte1(len);
            buffer[2] = DwordToByte2(len);
            buffer[3] = DwordToByte3(len);

            Array.Copy(compressedBytes, 0, buffer, 4, (int)len);

            WriteData(startingAddress, buffer);
        }

        public void WriteData(int startingAddress, byte[] bytes)
        {
            for (uint i = 0; i < (bytes.Length / 4096) + 1; i++)
            {
                TryUtils.Retry(() => EraseFlash(i), 10, 100);
                Task.Delay(50).Wait();
            }

            WriteFlash(0, (uint)bytes.Length, bytes);

        }

        public void EraseFlash(uint nSector)
        {
            FlashEraseParams pCmd = new FlashEraseParams();

            byte nLength = (byte)Marshal.SizeOf(typeof(FlashEraseParams));
            byte nCmd = CMD_FIDM_FLASH_ERASE;

            pCmd.nSectorNum0 = DwordToByte0(nSector);
            pCmd.nSectorNum1 = DwordToByte1(nSector);
            pCmd.nSectorNum2 = DwordToByte2(nSector);
            pCmd.nSectorNum3 = DwordToByte3(nSector);

            SendAndReceive<FIDM_STATUS>(nCmd, GetBytes(pCmd), nLength, nLength, true, longTimeout);
        }

        /// <summary>
        /// This is used to build the AUTO_IGNITION_SEQUENCE arguments for igniting a phx21
        /// </summary>
        /// <returns>A fully built AUTO_IGNITION_SEQUENCE</returns>
        private static AUTO_IGNITION_SEQUENCE BuildAutoIgnitionSequence()
        {
            AUTO_IGNITION_SEQUENCE ignition = new AUTO_IGNITION_SEQUENCE();
            ignition.start_stop = 1;
            ignition.target_hPSI = 175;
            ignition.tolerance_hPSI = 5;
            ignition.max_pressure_wait_msec = 10000;
            ignition.min_temperature_rise_tK = 10;
            ignition.max_ignition_wait_msec = 5000;
            ignition.sol_b_delay_msec = 1000;
            ignition.use_glow_plug_b = 0;
            ignition.pre_purge_pump_msec = 5000;
            ignition.pre_purge_sol_A_msec = 5000;

            ignition.param1 = 0;
            ignition.param2 = 0;
            return ignition;
        }

        private T SendAndReceive<T>(byte key, byte[] message, byte commandLength, byte headerLength, bool sendCRC, int timeout = 2000) where T
            : new()
        {
            DateTime sendTime = Now;
            TransmitSerialCmd(key, message, commandLength, headerLength, sendCRC);

            bool success;
            var result = ReceiveCmdResponse<T>(key, timeout, sendTime, out success);

            if (!success) throw new Exception($"Could not get response for command type {key}");

            return result;
        }

        /// <summary>
        /// A wrapper around GetResponse() that formats the response bytes as a message of type T
        /// </summary>
        /// <typeparam name="T">The type of response you wish to receive</typeparam>
        /// <param name="key">The command byte that was just sent</param>
        /// <param name="waitTime">how long to wait in milliseconds</param>
        /// <param name="sendTime">the time the command was sent</param>
        /// <param name="success">out param indicating if the response was received in the specified waitTime</param>
        /// <returns>A response message of type T</returns>
        private T ReceiveCmdResponse<T>(byte key, int waitTime, DateTime sendTime, out bool success) where T
            : new()
        {
            success = false;

            var bytes = GetResponse(key, waitTime, sendTime);

            try
            {
                T rsp = FromBytes<T>(bytes);

                success = true;
                return rsp;
            }
            catch (Exception ex)
            {
                throw new Exception("Error parsing CmdResponse.", ex);
            }
        }

        private byte[] GetNextResponse()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            byte currentState = STATE_WAITING_FOR_SYNC_CODE;
            byte numBytesReceived = 0;
            List<byte> responseBytes = new List<byte>();
            bool continueReading = true;

            while (continueReading)
            {
                byte readByte;

                readByte = _inputStream.ReadByte();

                switch (currentState)
                {
                    case STATE_WAITING_FOR_SYNC_CODE:
                        if (readByte == SYNC_CODE_RES)
                        {
                            numBytesReceived = 0;
                            numBytesReceived++;
                            if (responseBytes.Any()) responseBytes = new List<byte>();
                            responseBytes.Add(readByte);
                            currentState = STATE_WAITING_FOR_LENGTH;
                        }
                        break;

                    case STATE_WAITING_FOR_LENGTH:
                        numBytesReceived++;
                        responseBytes.Add(readByte);
                        currentState = STATE_WAITING_FOR_RESPONSE_ID;
                        
                        if (readByte < 3)
                        {
                            WriteToLog("GetResponse: Could not parse message: Length is " + readByte);
                            WriteToLog("GetResponse: Waiting for sync code");
                            currentState = STATE_WAITING_FOR_SYNC_CODE;
                        }

                        break;

                    case STATE_WAITING_FOR_RESPONSE_ID:
                        numBytesReceived++;

                        responseBytes.Add(readByte);
                        currentState = STATE_WAITING_FOR_RESPONSE_DATA;
                        break;

                    case STATE_WAITING_FOR_RESPONSE_DATA:
                        numBytesReceived++;
                        responseBytes.Add(readByte);

                        if (numBytesReceived >= responseBytes[FIELD_LENGTH_BYTES])
                            // Receive all the command data as indicated by the count field.
                        {
                            currentState = STATE_WAITING_FOR_SYNC_CODE; // Go back to waiting for the sync code.
                            continueReading = false;
                        }
                        break;

                    default:
                        WriteToLog("GetResponse: Could not parse message: bad state");
                        WriteToLog("GetResponse: Waiting for sync code");
                        currentState = STATE_WAITING_FOR_SYNC_CODE;
                        break;
                }
            }

            return responseBytes.ToArray();
        }

        /// <summary>
        /// This is the workhorse function used to send serial commands.
        /// Sets the first 3 bytes to SYNC_CODE_CMD, message length, and cmd id
        /// and optionally adds a crc to the end
        /// </summary>
        /// <param name="nCmd">The command byte to be send, should be one of CMD_FIDM_*</param>
        /// <param name="pStream">The struct defining the data to send</param>
        /// <param name="nTotalCmdLength"></param>
        /// <param name="nHeaderLength">usually the same as nTotalCmdLength</param>
        /// <param name="bSendCrc">true to send the crc at the end of the message</param>
        /// <returns>The crc of the message sent</returns>
        private byte TransmitSerialCmd(byte nCmd, byte[] pStream, byte nTotalCmdLength,
            byte nHeaderLength, bool bSendCrc)
        {
            try
            {
                byte nCRC = 0;
                byte[] pData = new byte[nHeaderLength + 1];

                pStream[FIELD_SYNC_CODE] = SYNC_CODE_CMD;
                pStream[FIELD_LENGTH_BYTES] = (byte)(nTotalCmdLength + 1);
                pStream[FIELD_CMD_ID] = nCmd;

                nCRC = ComputeCRC(pStream, nHeaderLength);

                Array.Copy(pStream, pData, nHeaderLength);

                if (bSendCrc)
                {
                    pData[nHeaderLength] = nCRC;

                    sendMessages.Enqueue(new BluetoothMessage
                    {
                        Bytes = pData,
                        Offest = 0,
                        Length = nHeaderLength + 1
                    });
                }
                else
                {
                    sendMessages.Enqueue(new BluetoothMessage
                    {
                        Bytes = pData,
                        Offest = 0,
                        Length = nHeaderLength
                    });
                }

                return nCRC;
            }
            catch (Exception ex)
            {
                OnError(new ErrorEventArgs(ex));
            }

            return 0;
        }

        private byte[] GetBytes<T>(T str)
        {
            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }

#if COMPACT_FRAMEWORK

        private T FromBytes<T>(byte[] bytes)
        {
            bytesIndex = 0;
            Stopwatch watch = new Stopwatch();
            watch.Start();
            T retVal = (T)FromBytes(typeof(T), bytes);
            watch.Stop();
            return retVal;
        }

        private int bytesIndex = 0;


        private object FromBytes(Type type, byte[] bytes)
        {
            object reconstruct = Activator.CreateInstance(type);

            foreach (FieldInfo field in type.GetFields())
            {
                object obj;

                if (field.FieldType.IsValueType && !field.FieldType.IsPrimitive)
                {
                    if (bytesIndex % 4 != 0)
                        bytesIndex += 4 - (bytesIndex % 4);

                    obj = FromBytes(field.FieldType, bytes);
                }
                else if (field.FieldType.IsArray)
                {
                    break;
                }
                else
                {
                    int size = Marshal.SizeOf(field.FieldType);
                    obj = Convert.ChangeType(GetVal(bytes, bytesIndex, size), field.FieldType, null);
                    bytesIndex += size;
                }

                field.SetValue(reconstruct, obj);


            }

            return reconstruct;
        }

        private static object GetVal(byte[] bytes, int bytesIndex, int size)
        {
            int mult = (size - 1) * 8;

            long l2 = 0;

            if (bytesIndex + size >= bytes.Length)
                return 0;

            try
            {
                for (int i = bytesIndex + size - 1; i >= bytesIndex; i--)
                {
                    l2 = l2 | bytes[i] << mult;
                    mult -= 8;
                }
            }
            catch (Exception ex)
            {
                throw;
            }



            return 0xFFFFFFFF & l2;
        }
#else
        private T FromBytes<T>(byte[] arr) where T : new()
        {
            GCHandle pinnedPacket = GCHandle.Alloc(arr, GCHandleType.Pinned);

            T obj = (T)Marshal.PtrToStructure(pinnedPacket.AddrOfPinnedObject(), typeof(T));
            pinnedPacket.Free();

            return obj;
        }
#endif


        private byte ComputeCRC(byte[] pStream, byte nLengthBytes)
        {
            byte chksum;
            byte one = 1;
            byte seven = 7;
            chksum = 0xD5;

            for (int i = 0; i < nLengthBytes; i++)
            {

                chksum = (byte)((chksum << one) | (chksum >> seven));
                chksum += pStream[i];
            }

            return chksum;
        }


    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct DEFAULT_RESPONSE_EXTENDED
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;

        [MarshalAs(UnmanagedType.Struct)]
        public FIDM_STATUS_EXTENDED status;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct READ_DATA_PARAMS
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct FIDM_STATUS_EXTENDED
    {
        public byte nStatusFlags;
        public byte nReadingNumber;
        public byte nError;
        public byte nThermocouple_TK0; // Tenths of Degrees Kelvin, unsigned.
        public byte nThermocouple_TK1;
        public byte nBatt_mV0; // Millivolts, unsigned.
        public byte nBatt_mV1;
        public byte nChamberOuterTemp_TK0; // Tenths of Degrees Kelvin, unsigned.
        public byte nChamberOuterTemp_TK1;
        public byte nSamplePressure_HPSI0; // Hundredths of PSI, unsigned.
        public byte nSamplePressure_HPSI1;
        public byte nAirPressure_HPSI0; // Hundredths of PSI, unsigned.
        public byte nAirPressure_HPSI1;
        public byte nH2Pressure_PSI0; // PSI, unsigned.
        public byte nH2Pressure_PSI1;
        public byte nFIDRange;
        public byte nFIDTenthsPicoA_Sat0; // Saturation, Tenths of Pico Amps, signed.
        public byte nFIDTenthsPicoA_Sat1;
        public byte nFIDTenthsPicoA_Sat2;
        public byte nFIDTenthsPicoA_Sat3;
        public byte nFIDTenthsPicoA_In10; // Input1, Tenths of Pico Amps, signed.
        public byte nFIDTenthsPicoA_In11;
        public byte nFIDTenthsPicoA_In12;
        public byte nFIDTenthsPicoA_In13;
        public byte nFIDTenthsPicoA_In20; // Input2, Tenths of Pico Amps, signed.
        public byte nFIDTenthsPicoA_In21;
        public byte nFIDTenthsPicoA_In22;
        public byte nFIDTenthsPicoA_In23;
        // ---- new additions below this mark ----
        public byte nFIDTenthsPPM0;
        public byte nFIDTenthsPPM1;
        public byte nFIDTenthsPPM2;
        public byte nFIDTenthsPPM3;
        public byte nSystemCurrentMa0;
        public byte nSystemCurrentMa1;
        public byte nPumpA_power_pct;
        public byte spare;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = Phx21.PID_LOG_SIZE)]
        public PID_LOG_ENTRY[] pid_log;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct PID_LOG_ENTRY
    {
        public ushort millisecond;
        public short derivative;
        public short p_error;
        public short err_acc;
        public short pump_pwr;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct AUTO_IGNITION_SEQUENCE
    {

        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte start_stop; // 0 = abort if running, 1 = start
        public short target_hPSI;
        public short tolerance_hPSI;
        public ushort min_temperature_rise_tK;
        public ushort max_pressure_wait_msec;
        public ushort max_ignition_wait_msec;
        public ushort sol_b_delay_msec;
        public ushort pre_purge_pump_msec;
        public ushort pre_purge_sol_A_msec;
        public ushort param1;
        public ushort param2;
        public byte use_glow_plug_b; // 0 or 1

    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct FIDM_STATUS // Status
    {
        public byte nStatusFlags;
        public byte nReadingNumber;
        public byte nError;
        public byte nThermocouple_TK0; // Tenths of Degrees Kelvin, unsigned.
        public byte nThermocouple_TK1;
        public byte nBatt_mV0; // Millivolts, unsigned.
        public byte nBatt_mV1;
        public byte nChamberOuterTemp_TK0; // Tenths of Degrees Kelvin, unsigned.
        public byte nChamberOuterTemp_TK1;
        public byte nSamplePressure_HPSI0; // Hundredths of PSI, unsigned.
        public byte nSamplePressure_HPSI1;
        public byte nAirPressure_HPSI0; // Hundredths of PSI, unsigned.
        public byte nAirPressure_HPSI1;
        public byte nH2Pressure_PSI0; // PSI, unsigned.
        public byte nH2Pressure_PSI1;
        public byte nFIDRange;
        public byte nFIDTenthsPicoA_Sat0; // Saturation, Tenths of Pico Amps, signed.
        public byte nFIDTenthsPicoA_Sat1;
        public byte nFIDTenthsPicoA_Sat2;
        public byte nFIDTenthsPicoA_Sat3;
        public byte nFIDTenthsPicoA_In10; // Input1, Tenths of Pico Amps, signed.
        public byte nFIDTenthsPicoA_In11;
        public byte nFIDTenthsPicoA_In12;
        public byte nFIDTenthsPicoA_In13;
        public byte nFIDTenthsPicoA_In20; // Input2, Tenths of Pico Amps, signed.
        public byte nFIDTenthsPicoA_In21;
        public byte nFIDTenthsPicoA_In22;
        public byte nFIDTenthsPicoA_In23;
    } // 28 bytes

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct SetCalibration
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte index_number;
        public int ppm_tenths;
        public int fid_current_tPa;
        public ushort H2_pressure_hPSI;
        public byte overwrite; // 1 = overwrite; 0 = erase
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct GenerateCalibration
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte spare_for_alignment;
        public int ppm_tenths;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct GetCalibration
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte index_number;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
#endif
    public struct GetCalibrationResponse
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;

        [MarshalAs(UnmanagedType.Struct, SizeConst = 28)]
        public FIDM_STATUS status;

        public byte index_number;
        public int ppm_tenths;
        public int fid_current_tPa;
        public ushort H2_pressure_hPSI;
        public byte valid; // 1 = data valid; 0 = empty slot
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct PumpAux1ControlParams
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte nID;
        public byte nPowerTenthsPercent0;
        public byte nPowerTenthsPercent1;
        public byte nKickStartDurationSec;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct DefaultResponse                                       // Responses
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        FIDM_STATUS status;         // 28 bytes
    }            // 31 bytes

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct SolenoidControlParams
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte nID;
        public byte nPower;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct PumpClosedLoop
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte enable; // 0 or 1
        public short target_hPSI;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct IgniteParams
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte nID;
        public byte nDurationSec;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct SetSamplingParams
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte nRange; // 0, 1, 2 or 3
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct DeadheadParams
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte enable; // 0 or 1
        public ushort pressure_low_limit_hPSI;
        public ushort max_duration_msec;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct IntegrationControlParams
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte nMode; // 0 or 1
        public byte nChargeMultiplier;
        public byte nRange;
        public byte nIntegrationTimeUs0;
        public byte nIntegrationTimeUs1;
        public byte nSamplesToAvg;
        public byte nReportMode;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct BlueToothWatchdogSettings
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte nSupervisionTimeoutS;                      // Sets the supervision timeout on the BT module.
        public byte nTrafficLostForcedDisconnectEnable;        // Enables/disables the traffic lost disconnect.
        public byte nTrafficLostForcedDisconnectTimeoutS;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct FlashWriteParams
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte nStartingAddress0;
        public byte nStartingAddress1;
        public byte nStartingAddress2;
        public byte nStartingAddress3;
        public byte nCount;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct FlashReadParams
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte nStartingAddress0;
        public byte nStartingAddress1;
        public byte nStartingAddress2;
        public byte nStartingAddress3;
        public byte nCount;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct FlashEraseParams
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte nSectorNum0;
        public byte nSectorNum1;
        public byte nSectorNum2;
        public byte nSectorNum3;
    }

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct CalH2PressureCompensation
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        public byte spare_for_alignment;
        public long H2_compensation_pos; // (fraction * 10^6) per LPH2 hPSI that PPM will be adjusted
        public long H2_compensation_neg; // (fraction * 10^6) per LPH2 hPSI that PPM will be adjusted
    };

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct ConfigurationReadParams
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
    };

#if COMPACT_FRAMEWORK
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
#endif
    public struct ConfigurationResponse
    {
        public byte nSyncCode;
        public byte nLengthBytes;
        public byte nCmdID;
        // start fidm status
        // done this way because a struct within a struct doesn't work...
        public byte nStatusFlags;
        public byte nReadingNumber;
        public byte nError;
        public byte nThermocouple_TK0; // Tenths of Degrees Kelvin, unsigned.
        public byte nThermocouple_TK1;
        public byte nBatt_mV0; // Millivolts, unsigned.
        public byte nBatt_mV1;
        public byte nChamberOuterTemp_TK0; // Tenths of Degrees Kelvin, unsigned.
        public byte nChamberOuterTemp_TK1;
        public byte nSamplePressure_HPSI0; // Hundredths of PSI, unsigned.
        public byte nSamplePressure_HPSI1;
        public byte nAirPressure_HPSI0; // Hundredths of PSI, unsigned.
        public byte nAirPressure_HPSI1;
        public byte nH2Pressure_PSI0; // PSI, unsigned.
        public byte nH2Pressure_PSI1;
        public byte nFIDRange;
        public byte nFIDTenthsPicoA_Sat0; // Saturation, Tenths of Pico Amps, signed.
        public byte nFIDTenthsPicoA_Sat1;
        public byte nFIDTenthsPicoA_Sat2;
        public byte nFIDTenthsPicoA_Sat3;
        public byte nFIDTenthsPicoA_In10; // Input1, Tenths of Pico Amps, signed.
        public byte nFIDTenthsPicoA_In11;
        public byte nFIDTenthsPicoA_In12;
        public byte nFIDTenthsPicoA_In13;
        public byte nFIDTenthsPicoA_In20; // Input2, Tenths of Pico Amps, signed.
        public byte nFIDTenthsPicoA_In21;
        public byte nFIDTenthsPicoA_In22;
        public byte nFIDTenthsPicoA_In23;
        // end FIDM status
        public byte nVersion;
        public byte nSectorSizeBytes0;
        public byte nSectorSizeBytes1;
        public byte nSectorSizeBytes2;
        public byte nSectorSizeBytes3;
        public byte nNumberSectors0;
        public byte nNumberSectors1;
        public byte nNumberSectors2;
        public byte nNumberSectors3;
    };       // 40 bytes
}
