using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TcpModule;

namespace ProxyModule;
public class HttpTunnel : TcpTunnel
{
    private readonly HttpTunnelConfig _config;
    public HttpTunnel(HttpTunnelConfig config) : base(config.source, config.target)
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
    public struct HttpTunnelConfig
    {
        public TcpClientWrapper source;
        public TcpClientWrapper target;
    }

}
