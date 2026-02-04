using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NetworkModule;
using Serilog;
using Serilog.Core;
using static SocksModule.SocksContext;

namespace ProxyModule;
public interface ISettings
{
    public string ProxyCrtPath { get; }
    public string ProxyCrtPasswd { get; }
    public bool IsTlsProxy { get; }
    public string HttpAuthType { get; }
    public bool AuthEnable { get; }
    public int DefaultHttpPort { get; }
    public int MaxUserConnection { get; }
    public Logger? Logger { get; }
    public IPEndPoint InternalTcpEndPoint { get; }
    public IPEndPoint ExternalTcpEndPoint { get; }
    public IPEndPoint SocksExternalBindEndPoint { get; }
    public IPAddress SocksExternalUdpAddress { get; }
    public bool SocksCheckAllowCommand(ConnectType type);
    public bool CheckAllowAddrType(string type);
    public bool CheckRule(RuleManager.RuleInfo info);
    public string? GetPassword(string userName);
    public IAuth MakeAuth(Func<string, string?> getPasswd, Proxy.ProxyClientContext context, Proxy proxy, TcpClientWrapper client);

}
