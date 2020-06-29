using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ldartools.Common.Services
{
    public interface ITimeMongerService
    {
        DateTime Now { get; }
        DateTime UtcNow { get; }
    }
}
