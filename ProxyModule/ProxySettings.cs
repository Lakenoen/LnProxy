using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using static ProxyModule.ISettings;
using static SocksModule.SocksContext;

namespace ProxyModule;
public class ProxySettings : ISettings
{
    private string _proxyCrtPath = string.Empty;
    private string _proxyCrtPasswd = string.Empty;
    private bool _isTlsProxy = false;
    private bool _authEnable = false;
    private IPEndPoint? _socksExternalTcpEndPoint = IPEndPoint.Parse("0.0.0.0:0");
    private IPEndPoint? _socksExternalBindEndPoint;
    private IPEndPoint? _socksExternalUdpEndPoint;
    private string[] _allowAddrTypes = { "domain", "ipv4", "ipv6" };
    private string[] _socksAllowCommand = { "connect" };
    private string _pathToRuleFile = string.Empty;
    private string _pathToAuthFile = string.Empty;
    private int _defaultHttpPort = 80;
    private int _maxUserConnection = 4;
    private RuleManager? _ruleManager = null;
    public string ProxyCrtPath => _proxyCrtPath;
    public string ProxyCrtPasswd => _proxyCrtPasswd;
    public bool IsTlsProxy => _isTlsProxy;
    public bool AuthEnable => _authEnable;
    public IPEndPoint SocksExternalTcpEndPoint => _socksExternalTcpEndPoint!;
    public IPEndPoint SocksExternalBindEndPoint => _socksExternalBindEndPoint!;
    public IPEndPoint SocksExternalUdpEndPoint => _socksExternalUdpEndPoint!;
    public int DefaultHttpPort => _defaultHttpPort;
    public int MaxUserConnection => _maxUserConnection;
    public Logger? _logger;
    public Logger? Logger => _logger;
    public bool SocksCheckAllowCommand(ConnectType type) => _socksAllowCommand!.Contains(type.ToString().ToLower());
    public bool CheckAllowAddrType(string type) => _allowAddrTypes!.Contains(type.ToLower());
    public bool CheckRule(RuleManager.RuleInfo info)
    {
        if(_ruleManager is null)
        {
            return true;
        }
        return _ruleManager.Check(info);
    }
    public string? GetPassword(string userName)
    {
        return "pass";
    }

    private FileSystemWatcher _watcher = new FileSystemWatcher();
    public ProxySettings(string path)
    {
        ParseSettings(path);

        _watcher.Path = Path.GetDirectoryName( Path.GetFullPath(path) );
        _watcher.NotifyFilter = NotifyFilters.LastWrite;
        _watcher.Filter = Path.GetFileName(path);

        _watcher.Changed += _watcher_Changed;
        _watcher.EnableRaisingEvents = true;
    }

    private void _watcher_Changed(object sender, FileSystemEventArgs e)
    {
        this.Changed?.Invoke();
    }

    private void ParseSettings(string path)
    {
        using FileStream stream = new FileStream(path, FileMode.OpenOrCreate);
        using StreamReader reader = new StreamReader(stream);
        string[]? keyValue = null;
        while(!reader.EndOfStream)
        {
            keyValue = reader.ReadLine()?.Split(":", 2);
            if (keyValue is null || keyValue[0].StartsWith("#"))
                continue;

            switch (keyValue[0].Trim())
            {
                case "MaxUserConnection": this._maxUserConnection = Convert.ToInt32(keyValue[1].Trim()); break;
                case "DefaultHttpPort": this._defaultHttpPort = Convert.ToInt32(keyValue[1].Trim()); break;
                case "ProxyCertPath": this._proxyCrtPath = keyValue[1].Trim(); break;
                case "ProxyCertPasswd": this._proxyCrtPasswd = keyValue[1].Trim(); break;
                case "EnableTls": this._isTlsProxy = bool.Parse(keyValue[1].Trim()); break;
                case "EnableAuth": this._authEnable = bool.Parse(keyValue[1].Trim()); break;
                case "SocksExternalTcpAddress": this._socksExternalTcpEndPoint = GetEndPointFromString(keyValue[1].Trim()); break;
                case "SocksExternalBindAddress": this._socksExternalBindEndPoint = GetEndPointFromString(keyValue[1].Trim()); break;
                case "SocksExternalUdpAddress": this._socksExternalUdpEndPoint = GetEndPointFromString(keyValue[1].Trim()); break;
                case "AllowAddressTypes": _allowAddrTypes = keyValue[1].Split(' ', ',').Select( (el, i)=> el.ToLower().Trim()).ToArray(); break;
                case "SocksAllowCommand": _socksAllowCommand = keyValue[1].Split(' ', ',').Select((el, i) => el.ToLower().Trim()).ToArray(); break;
                case "RulePath": _pathToRuleFile = keyValue[1].Trim(); break;
                case "AuthPath": _pathToAuthFile = keyValue[1].Trim(); break;
            }
        }
        Directory.CreateDirectory("log");
        this._logger = new LoggerConfiguration()
            .WriteTo.File("log/Log.txt", rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger();
        Verefy();
        try
        {
            if (_pathToRuleFile != string.Empty)
                this._ruleManager = new RuleManager(_pathToRuleFile);
        }
        catch
        {
            throw new SettingsException("Rule parse error");
        }
        _logger.Information("Settings loaded");
    }

    private void Verefy()
    {
        if (this.IsTlsProxy && (this._proxyCrtPath == string.Empty || this._proxyCrtPasswd == string.Empty))
            throw new SettingsException("Certificate or password is missing");
        if (this.AuthEnable && this._pathToAuthFile == string.Empty)
            throw new SettingsException("The path to the authentication file is missing");
        if (this.SocksCheckAllowCommand(ConnectType.BIND) && this._socksExternalBindEndPoint is null)
            throw new SettingsException("Bind connection address missing");
        if (this.SocksCheckAllowCommand(ConnectType.UDP) && this._socksExternalUdpEndPoint is null)
            throw new SettingsException("Bind connection address missing");
    }
    private IPEndPoint GetEndPointFromString(string source)
    {
        var hostPort = source.Split(" ", 2, StringSplitOptions.RemoveEmptyEntries);
        var type = Uri.CheckHostName(hostPort[0]);
        if (type == UriHostNameType.Dns)
        {
            IPAddress addr = Dns.GetHostEntry(hostPort[0]).AddressList.ElementAt(0);
            return new IPEndPoint(addr, int.Parse(hostPort[1]));
        }
        else
        {
            return IPEndPoint.Parse($"{hostPort[0]}:{hostPort[1]}");
        }
    }
    public class SettingsException : ApplicationException
    {
        public SettingsException(string msg) : base(msg) { }
    }

    public event Action? Changed;
}
