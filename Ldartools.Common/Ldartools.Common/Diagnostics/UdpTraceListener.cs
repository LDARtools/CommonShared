using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Ldartools.Common.Diagnostics
{
    public class UdpTraceListener : TraceListener
    {
        private UdpClient _client;
        private IPEndPoint _endPoint;

        public UdpTraceListener(string ipAddress, int port)
        {
            _client = new UdpClient();
            _client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            _client.JoinMulticastGroup(IPAddress.Parse(ipAddress));
            _endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
        }

        public override void Write(string message)
        {
            if (message == null) return;
            var bytes = Encoding.UTF8.GetBytes(message);
            _client.Send(bytes, bytes.Length, _endPoint);
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
            _client = null;
            _endPoint = null;
        }
    }
}
