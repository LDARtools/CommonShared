using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Ldartools.Common.Devices.Services
{
    public interface ITvaDeviceService : IPhxDeviceService
    {
        void PressSelectButton();
        void PressNextButton();
        event EventHandler<TvaErrorEventArgs> TvaError;
    }

    public class TvaErrorEventArgs : EventArgs
    {
        public string Error { get; }

        public TvaErrorEventArgs(string error)
        {
            Error = error;
        }
    }
}
