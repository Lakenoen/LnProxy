using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static SocksModule.SocksContext;

namespace ProxyModule;
public interface ISettings
{
    public string ProxyCrtPath { get; }
    public string ProxyCrtPasswd { get; }
    public bool IsTlsProxy { get; }
    public bool AuthEnable { get; }
    public IPEndPoint SocksExternalTcpEndPoint { get; }
    public IPEndPoint SocksExternalBindEndPoint { get; }
    public IPEndPoint SocksExternalUdpEndPoint { get; }
    public bool SocksCheckAllowCommand(ConnectType type);
    public bool CheckAllowAddrType(string type);
    public bool CheckRule(RuleInfo info);
    public string? GetPassword(string userName);

    public class RuleInfo()
    {
        public string Proto { get; set; } = string.Empty;
        public string TargetAddr { get; set; } = string.Empty;
        public string SourceAddr { get; set; } = string.Empty;
        public string SourcePort { get; set; } = string.Empty;
        public string TargetPort { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }
}
