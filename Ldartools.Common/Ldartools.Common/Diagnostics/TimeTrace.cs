using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Ldartools.Common.Diagnostics
{
    public class TimeTrace : IDisposable
    {
#if DEBUG
        private readonly string _caller;
        private readonly Stopwatch _stopwatch;
#endif

        public TimeTrace([CallerMemberName] string caller = null, [CallerFilePath] string callerFilePath = null)
        {
#if DEBUG
            _caller = $"{callerFilePath}.{caller}";
            Trace.WriteLine($"Trace: Started {_caller}.");
            _stopwatch = Stopwatch.StartNew();
#endif
        }

        public void Dispose()
        {
#if DEBUG
            _stopwatch.Stop();
            Trace.WriteLine($"Trace: Exited {_caller} ({_stopwatch.Elapsed}).");
#endif
        }
    }
}
