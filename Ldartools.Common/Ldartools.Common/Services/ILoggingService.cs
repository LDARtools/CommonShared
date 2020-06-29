using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Ldartools.Common.Services
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum LogLevel
    {
        TRACE,
        DEBUG,
        INFO,
        WARN,
        ERROR,
        FATAL,
        OFF
    }

    public interface ILoggingService
    {
        bool Trace(string message);
        bool Debug(string message);
        bool Info(string message);
        bool Warn(string message);
        bool Error(string message);
        bool Error(Exception exception);
        bool Error(string message, Exception exception);
        bool Fatal(string message);
        [Obsolete("Use Error(Exception) instead")]
        bool LogException(Exception exception);
        bool LogMessage(string message, LogLevel level);
        bool LogObject(object obj, string message = null, LogLevel level = LogLevel.INFO);
        Task<bool> LogObjectAsync(object obj, string message = null, LogLevel level = LogLevel.INFO);

        void Flush();

        LogLevel Level { get; set; }
    }
}
