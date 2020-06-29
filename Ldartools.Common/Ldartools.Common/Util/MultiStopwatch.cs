using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ldartools.Common.Util
{
    public class MultiStopwatch
    {
        public MultiStopwatch() { }

        public MultiStopwatch(string title)
        {
            Title = title;
        }

        public string Title { get; set; }

        private readonly Dictionary<object, Stopwatch> _watches = new Dictionary<object, Stopwatch>();

        private readonly Dictionary<object, List<long>> _averages = new Dictionary<object, List<long>>();

        public Stopwatch this[object key]
        {
            get
            {
                if (!_watches.ContainsKey(key))
                {
                    var sw = new Stopwatch();
                    _watches.Add(key, sw);
                    return sw;
                }
                return _watches[key];
            }
        }

        public void Start(object key)
        {
            this[key].Start();
        }

        public void StartAverage(object key)
        {
            this[key].Restart();
        }

        public void Stop(object key)
        {
            this[key].Stop();
        }

        public void StopAverage(object key)
        {
            this[key].Stop();

            if (!_averages.ContainsKey(key))
            {
                _averages.Add(key, new List<long>());
            }

            _averages[key].Add(this[key].ElapsedMilliseconds);
        }

        public void StopAll()
        {
            foreach (var watch in _watches.Values)
            {
                watch.Stop();
            }
        }

        public string ToString(bool stopAll)
        {
            StopAll();
            return ToString();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(Title))
            {
                sb.AppendLine($"==={Title}===");
            }

            foreach (var kvp in _watches)
            {
                sb.AppendLine($"| {kvp.Key} : {kvp.Value.Elapsed}");
            }

            if (_averages.Any())
            {
                sb.AppendLine($"---Averages---");

                foreach (var average in _averages)
                {
                    sb.AppendLine($"| {average.Key} : {TimeSpan.FromMilliseconds(average.Value.Average(l => l))}");
                }
            }

            return sb.ToString();
        }
    }
}
