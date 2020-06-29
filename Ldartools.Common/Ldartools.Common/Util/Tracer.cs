using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Ldartools.Common.Services;

namespace Ldartools.Common.Util
{
    public class Tracer : IDisposable
    {
        private readonly ILoggingService _logger;
        private readonly LogLevel _level = LogLevel.TRACE;
        private readonly string _blockName;

        public Tracer(ILoggingService logger, LogLevel level, string blockName)
        {
            _logger = logger;
            _level = level;
            _blockName = blockName;
            logger.LogMessage(blockName + " entered", level);
        }

        public Tracer(ILoggingService logger, object caller, string blockname)
        {
            if (logger.Level != LogLevel.TRACE) return;
            _logger = logger;
            _blockName = caller.GetType().FullName + "." + blockname;
            logger.LogMessage(_blockName + " entered", _level);
        }

        public Tracer(ILoggingService logger, MethodBase methodBase)
        {
            if (logger.Level != LogLevel.TRACE) return;
            _logger = logger;
            _blockName = methodBase.GetType().Name + "." + methodBase.Name;
            logger.LogMessage(_blockName + " entered", _level);
        }

        public Tracer(ILoggingService logger, string blockName)
        {
            if (logger.Level != LogLevel.TRACE) return;
            _logger = logger;
            _blockName = blockName;
            logger.LogMessage(blockName + " entered", _level);
        }

        public Tracer(ILoggingService logger, [CallerFilePath] string caller = null, [CallerMemberName] string callingMethod = null)
        {
            if (logger.Level != LogLevel.TRACE) return;
            _logger = logger;
            _blockName = $"{caller}.{callingMethod}";
            logger.LogMessage(_blockName + " entered", _level);
        }

        public void Dispose()
        {
            if (_logger == null || _logger.Level > _level) return;
            _logger.LogMessage(_blockName + " exited", _level);
        }
    }
}
