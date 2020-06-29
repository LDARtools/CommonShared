using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace Ldartools.Common.Diagnostics
{
    public class TcpTraceListener : TraceListener
    {
        private TcpClient _client;
        private StreamWriter _streamWriter;
        private readonly string _ipAddress;
        private readonly int _port;
        private int _remainingConnectionAttempts = 4;

        public TcpTraceListener(string ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        private void CheckConnection()
        {
            if (_client == null)
            {
                if (_remainingConnectionAttempts < 0) return;
                try
                {
                    _client = new TcpClient();
                    _client.Connect(_ipAddress, _port);
                    var stream = _client.GetStream();
                    _streamWriter = new StreamWriter(stream);
                }
                catch
                {
                    CleanUp();
                    _remainingConnectionAttempts--;
                }
            }
            else if(!_client.Connected)
            {
                CleanUp();
                // ReSharper disable once TailRecursiveCall
                CheckConnection();
            }
        }

        public override void Write(string message)
        {
            try
            {
                CheckConnection();
                if (_client == null) return;
                _streamWriter.Write(message);
                _streamWriter.Flush();
            }
#pragma warning disable 168
            catch(Exception e)
#pragma warning restore 168
            {
                //do nothing
            }
        }

        public override void WriteLine(string message)
        {
            Write(message + Environment.NewLine);
        }

        protected override void Dispose(bool disposing)
        {
            CleanUp();
            base.Dispose(disposing);
        }

        private void CleanUp()
        {
            _client?.Dispose();
            _streamWriter?.Dispose();
            _client = null;
            _streamWriter = null;
        }

    }
}
