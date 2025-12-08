using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TcpModule;

namespace ProxyModule;
public class SocksTunnel : TcpTunnel
{
    public SocksTunnel(TcpClientWrapper source, TcpClientWrapper target) : base(source, target)
    {

    }

    protected async override void SourceReaded(byte[] data)
    {
        await Target.WriteAsync(data);
    }

}
