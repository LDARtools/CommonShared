using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Ldartools.Common.Util;

namespace Ldartools.Common.IO
{
    public class StreamAdapter : IInputStream, IOutputStream
    {
        private readonly Stream _stream;
        private readonly DateTime _startTime;

        public long SendByteCount { get; protected set; }
        public long ReceiveByteCount { get; protected set; }
        public TimeSpan ConnectedTime => TimeProvider.Current.UtcNow - _startTime;

        public StreamAdapter(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _startTime = TimeProvider.Current.UtcNow;
        }

        public byte ReadByte()
        {
            ReceiveByteCount++;
            return (byte)_stream.ReadByte();
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
            SendByteCount += count;
        }

        public void Flush()
        {
            _stream.Flush();
        }
    }
}