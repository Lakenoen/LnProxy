using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TcpModule;
using static ProxyModule.IHttpPacket;

namespace ProxyModule;
internal class HttpsTunnel : TcpTunnel
{
    private readonly HttpsTunnelConfig _config;
    public HttpsTunnel(HttpsTunnelConfig config) : base(config.source, config.target)
    {
        _config = config;
    }
    protected async override void TargetReaded(byte[] data)
    {
        await Source.WriteAsync(data);
    }
    protected async override void SourceReaded(byte[] data)
    {
        await Target.WriteAsync(data);
    }
    public struct HttpsTunnelConfig
    {
        public TcpClientWrapper source;
        public TcpClientWrapper target;
    }

}