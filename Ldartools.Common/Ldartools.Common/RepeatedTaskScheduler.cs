using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ldartools.Common
{
    public class RepeatedTaskScheduler
    {
        internal class ScheduledTask : IDisposable
        {
            internal string Name { get; set; }
            internal Action Action { get; set; }
            internal DateTime LastRunDateTime { get; set; }
            internal TimeSpan Interval { get; set; }
            internal Func<bool> ConditionFunc { get; set; }

            public void Dispose()
            {
                Action = null;
                ConditionFunc = null;
            }
        }

        private Task _schedulerTask;
        private bool _schedulerRunning;
        private readonly Dictionary<string, ScheduledTask> _scheduledTasks = new Dictionary<string, ScheduledTask>();
        private TimeSpan _delay = TimeSpan.FromMinutes(1);
        private readonly object _delayLock = new object();

        public bool HasScheduledTask(string name)
        {
            return _scheduledTasks.ContainsKey(name);
        }

        public void RegisterAction(string name, Action action, TimeSpan interval)
        {
            RegisterAction(name, action, interval, () => true, true);
        }

        public void RegisterAction(string name, Action action, TimeSpan interval, bool runImmediately)
        {
            RegisterAction(name, action, interval, () => true, runImmediately);
        }

        public void RegisterAction(string name, Action action, TimeSpan interval, Func<bool> conditionFunc)
        {
            RegisterAction(name, action, interval, conditionFunc, true);
        }

        public void RegisterAction(string name, Action action, TimeSpan interval, Func<bool> conditionFunc, bool runImmediately)
        {
            if (_scheduledTasks.ContainsKey(name)) throw new Exception($"A scheduled task with the name '{name}' already exists.");

            var scheduledTask = new ScheduledTask
            {
                Name = name,
                Action = action,
                Interval = interval,
                ConditionFunc = conditionFunc,
                LastRunDateTime = DateTime.UtcNow
            };

            if (!runImmediately)
            {
                scheduledTask.LastRunDateTime -= interval;
            }

            lock (_delayLock)
            {
                _scheduledTasks.Add(name, scheduledTask);
                SetDelayTime();
            }
        }

        public void UnregisterAction(string name)
        {
            //make this method safe to call multiple times
            if (!_scheduledTasks.ContainsKey(name)) return;
            lock (_delayLock)
            {
                var scheduledTask = _scheduledTasks[name];
                _scheduledTasks.Remove(name);
                SetDelayTime();
                scheduledTask.Dispose();
            }
        }

        public void ResetAction(string name)
        {
            var task = _scheduledTasks[name];

            if (task == null)
                return;

            task.LastRunDateTime = DateTime.UtcNow;
        }

        public void Start()
        {
            if (_schedulerRunning)
                return;

            _schedulerRunning = true;

            _schedulerTask = new Task(() =>
            {
                while (_schedulerRunning)
                {
                    lock (_delayLock)
                    {
                        foreach (var scheduledTask in _scheduledTasks.Values)
                        {
                            var now = DateTime.UtcNow;

                            if (scheduledTask == null || (now - scheduledTask.LastRunDateTime) < scheduledTask.Interval
                                || !scheduledTask.ConditionFunc())
                                continue;

                            scheduledTask.LastRunDateTime = now;
                            scheduledTask.Action();
                        }

                        //VERY important, need to have a delay
                        Monitor.Wait(_delayLock, _delay);
                    }
                }
            });

            _schedulerTask.Start();
        }

        public void Stop()
        {
            _schedulerRunning = false;
        }

        private void SetDelayTime()
        {
            _delay = _scheduledTasks.Any() ? _scheduledTasks.Values.Select(st => st.Interval).Min() : TimeSpan.FromMinutes(1);
            Monitor.PulseAll(_delayLock);
        }
    }
}