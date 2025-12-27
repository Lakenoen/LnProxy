using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static ProxyModule.ISettings;
using static SocksModule.SocksContext;

namespace ProxyModule;
public class ProxySettings : ISettings
{
    private string _proxyCrtPath = "../../../../../proxy.pfx";
    private string _proxyCrtPasswd = "pass1";
    private bool _isTlsProxy = false;
    private bool _authEnable = true;
    private IPEndPoint _socksExternalTcpEndPoint = IPEndPoint.Parse("0.0.0.0:0");
    private IPEndPoint _socksExternalBindEndPoint = IPEndPoint.Parse("192.168.0.103:8889");
    private IPEndPoint _socksExternalUdpEndPoint = IPEndPoint.Parse("192.168.0.103:8890");
    public string ProxyCrtPath => _proxyCrtPath;
    public string ProxyCrtPasswd => _proxyCrtPasswd;
    public bool IsTlsProxy => _isTlsProxy;
    public bool AuthEnable => _authEnable;
    public IPEndPoint SocksExternalTcpEndPoint => _socksExternalTcpEndPoint;
    public IPEndPoint SocksExternalBindEndPoint => _socksExternalBindEndPoint;
    public IPEndPoint SocksExternalUdpEndPoint => _socksExternalUdpEndPoint;
    public bool SocksCheckAllowCommand(ConnectType type)
    {
        return true;
    }
    public bool CheckAllowAddrType(string type)
    {
        return true;
    }
    public bool CheckRule(RuleInfo info)
    {
        return true;
    }
    public string? GetPassword(string userName)
    {
        return "pass";
    }

    public ProxySettings(string path)
    {
        ParseSettings(path);
    }

    private void ParseSettings(string path)
    {

    }
}
