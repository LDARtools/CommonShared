using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ldartools.Common.Devices.Services;
using Ldartools.Common.IO;
using Ldartools.Common.Services;

namespace Ldartools.Common.Devices
{
    public class Tva
    {
        public class TvaMessage
        {
            public float Ppm { get; set; }
            public string RawPpm { get; set; }
            public bool IsPpmDecimal { get; set; }
            public bool FromFid { get; set; }
            public bool FromPid { get; set; }
            public bool IsPercent { get; set; }
            public string Status { get; set; }
            public bool IsCommandResponse { get; set; }
            public DateTime ReceivedAt { get; }

            public TvaMessage()
            {
                ReceivedAt = DateTime.Now;
            }
        }

        private readonly StreamContainer _streams;
        private readonly IFileManager _fileManager;
        private readonly string _name;
        private readonly ITimeMongerService _timeMongerService;
        private Task _receiveTask = null;
        private readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private TvaMessage _receivedCommandResponse = null;
        private readonly object _receivedSyncObject = new object();
        private bool inWriteLog = false;
        private Timer logWriteTimer = null;
        private Timer readingsLogWriteTimer = null;
        private TvaMessage _lastTvaMessage = null;
        private string _lastCommand = null;
        private string _lastStatus = null;
        private string _lastReadingStatus = null;
        private bool _ppmErr = false;
        private bool _ppmOff = false;
        private string _tvaWarning = null;



        public DateTime Now => _timeMongerService?.Now ?? DateTime.Now;
        public string LoggingDirectory { get; set; }
        public DateTime StatusDateTime { get; private set; }
        public string LogFilePath { get; private set; }
        public string StatusFilePath { get; private set; }
        public int LoggingInterval { get; private set; }
        public event EventHandler<DataPolledEventArgs> DataPolled;
        public string User { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string Application { get; set; } = string.Empty;

        public event EventHandler<TvaErrorEventArgs> TvaError;

        public TvaMessage LastTvaMessage
        {
            get => _lastTvaMessage;
            set
            {
                _lastTvaMessage = value;
                Dictionary<string, string> args = new Dictionary<string, string>();
                args["Status"] = _lastTvaMessage.Status;
                args["PPM"] = _lastTvaMessage.Ppm.ToString("F2");
                args["IsIgnited"] = (_lastTvaMessage.Ppm >= 0.0 ? bool.TrueString : bool.FalseString);
                DataPolled?.Invoke(this, new DataPolledEventArgs(args, _lastTvaMessage.Ppm));

                
            }
        }

        public event EventHandler<ErrorEventArgs> Error;

        public bool ShutdownNow { get; set; }

        public Tva(StreamContainer streams, IFileManager fileManager, string name, ITimeMongerService timeMongerService, string application = "", string user = "", string site = "")
        {
            _streams = streams;
            _fileManager = fileManager;
            _name = name;
            _timeMongerService = timeMongerService;
            Application = application;
            User = user;
            Site = site;

            ConfigureLogging(_fileManager.LogDirectory, 1000);
            StartMessageHandler();
        }

        public void WriteExceptionToLog(Exception exception)
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

        public void WriteToLog(string contents)
        {
            _logQueue.Enqueue($"~{Now.ToString("yyyy-MM-dd HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)}\t\t{User}\t{Site}\t{contents}");
        }

        private void WriteLogQueue()
        {
            List<string> messages = new List<string>();

            while (!_logQueue.IsEmpty)
            {
                string item;
                if (_logQueue.TryDequeue(out item))
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
                WriteExceptionToLog(ex);

                _logQueue.Enqueue(batch);
            }
        }

        private void StartMessageHandler()
        {
            if (_receiveTask == null)
            {
                _receiveTask = new Task(() =>
                {
                    int errorcount = 0;

                    while (!ShutdownNow)
                    {
                        try
                        {
                            var message = GetNextMessage();

                            if (!message.IsCommandResponse)
                            {
                                LastTvaMessage = message;
                            }
                            else
                            {
                                lock (_receivedSyncObject)
                                {
                                    _receivedCommandResponse = message;
                                    Monitor.PulseAll(_receivedSyncObject);
                                }
                            }

                            Task.Delay(10).Wait();

                            errorcount = 0;
                        }
                        catch (Exception ex)
                        {
                            if (!ShutdownNow)
                            {
                                WriteToLog("Receive thread error #" + errorcount);
                                WriteExceptionToLog(ex);
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
                    }

                    WriteToLog("Receive thread shutting down");
                    WriteLogQueue();
                });

                _receiveTask.Start();
                WriteToLog("receive thread started");
            }
        }

        private void OnError(ErrorEventArgs e)
        {
            Error?.Invoke(this, e);
        }

        private void OnTvaError(string error)
        {
            TvaError?.Invoke(this, new TvaErrorEventArgs(error));
        }

        public void ConfigureLogging(string loggingDirectory, int intervalInMilliseconds)
        {
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
                        WriteExceptionToLog(ex);
                    }
                    finally
                    {
                        inWriteLog = false;
                    }
                }, null, 250, 250);
            }

            if (readingsLogWriteTimer == null)
            {
                _fileManager.AppendToFile(LogFilePath, $"Received At,PPM,Raw PPM,PPM is Decimal,From Fid,From Pid,Is Percent,status");

                readingsLogWriteTimer = new Timer(state =>
                {
                    if (ShutdownNow)
                    {
                        readingsLogWriteTimer.Dispose();
                        readingsLogWriteTimer = null;
                    }

                    if (LastTvaMessage == null) return;

                    try
                    {
                        var status = (string.Equals(_lastTvaMessage.Status, _lastReadingStatus) ? "" : _lastTvaMessage.Status.Replace("\n", ""));
                        _lastReadingStatus = _lastTvaMessage.Status;
                        _fileManager.AppendToFile(LogFilePath, $"{_lastTvaMessage.ReceivedAt.ToString("yyyy-MM-dd HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)},{_lastTvaMessage.Ppm},{_lastTvaMessage.RawPpm},{_lastTvaMessage.IsPpmDecimal},{_lastTvaMessage.FromFid},{_lastTvaMessage.FromPid},{_lastTvaMessage.IsPercent},{status}");
                    }
                    catch (Exception)
                    {
                        // we can't really log it now, can we?
                    }
                }, null, 2000, 2000);
            }
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
            return $"{Now:yyyyMMdd}_{_name}_{Application}Readings.csv";
        }

        private string GetStatusFileName()
        {
            return $"{Now:yyyyMMdd}_{_name}_{Application}Status.log";
        }

        private bool EnterCommandMode()
        {
            //_inCommandMode = true;

            var response = SendAndWaitForCommandResponse("$$$", sendCrLf:false);

            return response != null && response.Status.Contains("CMD\r\n");
        }

        private TvaMessage SendAndWaitForCommandResponse(string command, int milliseconds = 2000, bool sendCrLf = true)
        {
            Stopwatch sw = Stopwatch.StartNew();
            lock (_receivedSyncObject)
            {
                _receivedCommandResponse = null;
                SendString(command, sendCrLf);

                while (_receivedCommandResponse == null && sw.Elapsed < TimeSpan.FromMilliseconds(2000))
                {
                    Monitor.Wait(_receivedSyncObject, 200);
                }

                return _receivedCommandResponse;
            }
        }

        private bool ExitCommandMode()
        {
            var response = SendAndWaitForCommandResponse("---");

            //_inCommandMode = false;

            return response != null && response.Status.Contains("END\r\n");
        }

        private TvaMessage GetNextMessage()
        {
            List<byte> buffer = new List<byte>();
            List<char> cbuffer = new List<char>();

            while (true)
            {
                byte b = _streams.InputStream.ReadByte();

                buffer.Add(b);
                cbuffer.Add((char)b);

                if (buffer.Count >= 130 && b == 0x3 && buffer[buffer.Count - 130] == 0x2)
                {
                    if (buffer.Count > 130)
                    {
                        var discard = string.Join("", buffer.Select(by => (char)by).ToArray());
                        WriteToLog($"Discarding: [{discard}]");
                    }


                    return ParseBuffer(buffer.Skip(buffer.Count - 130).Take(130));
                }

                //var message = _encoding.GetString(buffer.ToArray());
                var message = string.Join("", cbuffer);

                string response = null;

                if (message.Contains("CMD\r\n"))
                {
                    response = "CMD (entered command mode)";
                }
                else if (message.Contains("END\r\n"))
                {
                    response = "END (exited command mode)";
                }
                else if (message.Contains("AOK\r\n"))
                {
                    response = "AOK (command success)";
                }
                else if (message.Contains("!\r\n"))
                {
                    response = "! (problem with command)";
                }
                else if (message.Contains("?\r\n"))
                {
                    response = "? (problem with )";
                }

                if (response != null)
                {
                    WriteToLog($"Received response {response} to command {_lastCommand.Trim()}");

                    return new TvaMessage
                    {
                        IsCommandResponse = true,
                        Status = message
                    };
                }
            }
        }

        private TvaMessage ParseBuffer(IEnumerable<byte> buffer)
        {
            var message = new TvaMessage();

            char[] ppm = buffer.Skip(2).Take(4).Select(b => (char) b).ToArray();
            string ppmString = string.Join("", ppm).Trim();

            message.RawPpm = ppmString;

            bool decFormat = buffer.ElementAt(6) == 0x84;

            message.IsPpmDecimal = decFormat;

            if (float.TryParse(ppmString, out float ppmValue))
            {
                if (decFormat)
                {
                    ppmValue /= 100;
                }

                message.Ppm = ppmValue < 0 ? 0 : ppmValue;
                _ppmOff = false;
                _ppmErr = false;
            }
            else if (ppmString.Contains("OFF") && !_ppmOff)
            {
                _ppmOff = true;
                OnTvaError("TVA is off");
            }
            else if (ppmString.Contains("ERR") && !_ppmErr)
            {
                _ppmErr = true;
                OnTvaError("TVA has an error");
            }

            switch (buffer.ElementAt(8))
            {
                case 0x94:
                {
                    message.FromFid = true;
                    break;
                }

                case 0xA4:
                {
                    message.FromPid = true;
                    break;
                }

                case 0x98:
                {
                    message.FromFid = true;
                    message.IsPercent = true;
                    break;
                }

                case 0xA8:
                {
                    message.FromPid = true;
                    message.IsPercent = true;
                    break;
                }
            }

            char[] status = buffer.Skip(9).Take(120).Select(b => (char)b).ToArray();

            message.Status = string.Empty;

            List<string> statusLines = new List<string>();

            for (int i = 0; i < 6; i++)
            {
                statusLines.Add(string.Join("", status.Skip(i * 20).Take(20)));
            }

            message.Status = string.Join("\n", statusLines);

            if (!string.Equals(message.Status, _lastStatus))
            {
                WriteToLog($"Received PPM: {message.Ppm}, Status:\n{message.Status}");
                _lastStatus = message.Status;
            }
            else
            {
                WriteToLog($"Received PPM: {message.Ppm}, no status change");
            }

            if (message.Status.Contains("WARNING!") && !string.Equals(message.Status, _tvaWarning))
            {
                _tvaWarning = message.Status;
                OnTvaError(message.Status);
            }
            else if (!message.Status.Contains("WARNING!"))
            {
                _tvaWarning = null;
            }

            return message;
        }

        public void PressSelectButton()
        {
            EnterCommandMode();
            SendAndWaitForCommandResponse("s*,0808");
            Task.Delay(500).Wait();
            SendAndWaitForCommandResponse("s*,0800");
            ExitCommandMode();
        }

        public void PressNextButton()
        {
            EnterCommandMode();
            SendAndWaitForCommandResponse("s*,0404");
            Task.Delay(500).Wait();
            SendAndWaitForCommandResponse("s*,0400");
            ExitCommandMode();
        }

        private void SendString(string message, bool sendCrLf = true)
        {
            if (sendCrLf)
            {
                message = message + "\r\n";
            }

            List<byte> bytes = new List<byte>();

            foreach (var c in message)
            {
                bytes.Add((byte)c);
            }

            _lastCommand = message;

            _streams.OutputStream.Write(bytes.ToArray(), 0, bytes.Count);
            
            _streams.OutputStream.Flush();
        }
    }
}
