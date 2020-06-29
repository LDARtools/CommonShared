using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ldartools.Common.IO
{
    public interface IInputStream
    {
        byte ReadByte();
        void Flush();
        
        long ReceiveByteCount { get; }
        TimeSpan ConnectedTime { get; }
    }
}