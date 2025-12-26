using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyModule;
public interface ISettings
{
    public string ProxyCrtPath { get; }
    public string ProxyCrtPasswd { get; }
    public bool IsTlsProxy { get; }
    public bool AuthEnable { get; }
    public string? GetPassword(string userName);
}
