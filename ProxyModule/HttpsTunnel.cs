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
using NetworkModule;
using static ProxyModule.IHttpPacket;

namespace ProxyModule;
internal class HttpsTunnel : TcpTunnel
{
    public HttpsTunnel(TcpClientWrapper source, TcpClientWrapper target) : base(source, target)
    {
        
    }

}