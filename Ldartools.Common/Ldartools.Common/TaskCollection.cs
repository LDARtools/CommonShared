using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ldartools.Common
{
    public class TaskCollection
    {
        private readonly List<Task> _tasks = new List<Task>();

        public Task Add(Action action)
        {
            var task = Task.Run(action);
            _tasks.Add(task);
            return task;
        }

        public void Add(Task task)
        {
            _tasks.Add(task);
        }

        public void WaitAll()
        {
            Task.WaitAll(_tasks.ToArray());
        }

        public Task WhenAll()
        {
            return Task.WhenAll(_tasks.ToArray());
        }
    }

    public class TaskCollection<TResult>
    {
        private readonly List<Task<TResult>> _tasks = new List<Task<TResult>>();

        public Task Add(Func<TResult> action)
        {
            var task = Task.Run(action);
            _tasks.Add(task);
            return task;
        }

        public void Add(Task<TResult> task)
        {
            _tasks.Add(task);
        }

        public TResult[] WaitAll()
        {
            var mainTask = WhenAll();
            mainTask.Wait();
            return mainTask.Result;
        }

        public Task<TResult[]> WhenAll()
        {
            return Task.WhenAll(_tasks.ToArray());
        }
    }
}
