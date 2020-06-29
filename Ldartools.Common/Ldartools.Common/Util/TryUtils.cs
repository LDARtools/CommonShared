using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ldartools.Common.Util
{
    public class TryUtils
    {
        public static void Retry(Action a, int times = 3, int delay = 0)
        {
            try
            {
                a();
            }
            catch (Exception)
            {
                if (times == 0) throw;

                if (delay > 0) Task.Delay(delay).Wait(delay);

                Retry(a, times - 1);
            }
        }

        public static async Task RetryAsync( Func<Task> a, int times = 3, int delay = 0)
        {
            try
            {
                await a();
            }
            catch (Exception ex)
            {
                if (times == 0) throw ex;

                if (delay > 0) Task.Delay(delay).Wait(delay);

                await RetryAsync(a, times - 1);
            }
        }

        public static void TryAndSwallow(Action a)
        {
            try
            {
                a();
            }
            catch (Exception)
            {
                //ignore
            }
        }

        public static async Task TryAndSwallowAsync(Func<Task> a)
        {
            try
            {
                await a();
            }
            catch (Exception)
            {
                //ignore
            }
        }
    }
}
