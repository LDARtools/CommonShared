using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ldartools.Common.Services
{
    public interface IAppInfoService
    {
        string DeviceId { get; }
        string PackageName { get; }
        string AppVersionName { get; }
        int AppVersionCode { get; }
        double DeviceScreenWidth { get; }
        double DeviceScreenHeight { get; }
    }
}
