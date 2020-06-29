using System;
using System.Threading.Tasks;

namespace Ldartools.Common.Devices
{
    public class Timer
    {
        private Task task;
        private bool isRunning = true;
        public Timer(Action<object> pollingTimerCallback, object state, int waitTime, int pollTime)
        {
            task = new Task(
                () =>
                {
                    Task.Delay(waitTime).Wait();

                    DateTime lastRan = DateTime.Now.Subtract(TimeSpan.FromMilliseconds(pollTime));

                    while (isRunning)
                    {
                        var timeSpan = DateTime.Now.Subtract(lastRan);

                        if (timeSpan.TotalMilliseconds >= pollTime)
                        {
                            lastRan = DateTime.Now;
                            pollingTimerCallback(state);
                            Task.Delay(pollTime).Wait();
                        }
                    }
                });

            task.Start();
        }

        public void Dispose()
        {
            isRunning = false;
        }
    }
}