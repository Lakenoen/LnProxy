using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using NetworkModule;

namespace ProxyModule;
public class HttpTunnel : TcpTunnel
{
    public HttpTunnel(TcpClientWrapper source, TcpClientWrapper target) : base(source, target)
    {
        
    }

}
