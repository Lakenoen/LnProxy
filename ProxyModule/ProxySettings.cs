using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyModule;
public class ProxySettings : ISettings
{
    public string ProxyCrtPath => "../../../../../socks.pfx";

    public string ProxyCrtPasswd => "Pass1";

    public bool IsTlsProxy => false;
}
