using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyModule;
public class ProxySettings : ISettings
{
    public string ProxyCrtPath => "../../../../../proxy.pfx";
    public string ProxyCrtPasswd => "pass1";
    public bool IsTlsProxy => true;
    public bool AuthEnable => true;
    public string? GetPassword(string userName)
    {
        return "pass";
    }
}
