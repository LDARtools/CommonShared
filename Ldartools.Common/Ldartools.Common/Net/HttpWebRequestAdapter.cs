using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Ldartools.Common.Net
{
    [ExcludeFromCodeCoverage]
    public abstract class HttpWebRequestAdapter
    {
        // ReSharper disable UnusedParameter.Local
        protected HttpWebRequestAdapter(Uri uri){}
        protected HttpWebRequestAdapter(string url){}
        // ReSharper restore UnusedParameter.Local

        public abstract string Method { get; set; }
        public abstract WebHeaderCollection Headers { get; set; }
        public abstract bool PreAuthenticate { get; set; }
        public abstract bool SendChunked { get; set; }
        public abstract bool AllowWriteStreamBuffering { get; set; }
        public abstract bool AllowReadStreamBuffering { get; set; }
        public abstract string ContentType { get; set; }

        public abstract Task<Stream> GetRequestStreamAsync();
        public abstract Task<HttpWebResponse> GetResponseAsync();
    }
}
