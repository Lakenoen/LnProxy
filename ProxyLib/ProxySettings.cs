using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ProxyLib;

public class ProxySettings : ISettingsProxy
{
    public IPEndPoint ServerIpEndPoint => IPEndPoint.Parse("0.0.0.0:443");
}
