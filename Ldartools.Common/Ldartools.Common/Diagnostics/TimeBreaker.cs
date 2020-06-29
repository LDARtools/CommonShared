using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Ldartools.Common.Diagnostics
{
    public class TimeBreaker : IDisposable
    {
#if DEBUG
        private readonly BreakResponse _breakResponse;
        private readonly Timer _timer;
        private readonly string _preparedMessage;
#endif

        [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
        public TimeBreaker(int breakTime,
            BreakResponse breakResponse = BreakResponse.Exception,
            [CallerMemberName] string memberName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int sourceLineNumber = 0)
            : this(TimeSpan.FromMilliseconds(breakTime), breakResponse, memberName, filePath, sourceLineNumber)
        {
        }

        public TimeBreaker(TimeSpan breakTime,
            BreakResponse breakResponse = BreakResponse.Exception,
            [CallerMemberName] string memberName = null,
            [CallerFilePath] string filePath = null,
            [CallerLineNumber] int sourceLineNumber = 0)
        {
#if DEBUG
            _breakResponse = breakResponse;
            _preparedMessage = $"Time limit of {breakTime} exceeded at {memberName}[{sourceLineNumber}]({filePath}).";
            _timer = new Timer(OnTick, null, breakTime, TimeSpan.FromMilliseconds(-1));
#endif
        }

#if DEBUG
        private void OnTick(object state)
        {
            var message = _preparedMessage;
            switch (_breakResponse)
            {
                case BreakResponse.Break:
                    Debugger.Break();
                    break;
                case BreakResponse.Exception:
                    throw new OverTimeLimitException(message);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
#endif


        #region Implementation of IDisposable

        public void Dispose()
        {
#if DEBUG
            _timer.Dispose();
#endif
        }

        #endregion

        public enum BreakResponse
        {
            Break,
            Exception
        }

        public class OverTimeLimitException : Exception
        {
            public OverTimeLimitException(string message) : base(message) { }
        }
    }
}
