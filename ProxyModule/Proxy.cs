using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SocksModule;
using NetworkModule;
using static SocksModule.SocksContext;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.IO;
using System.Security.Authentication;
using System.Data;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Net.Mail;

namespace ProxyModule;
public class Proxy : IDisposable
{
    private X509Certificate2? _proxyCert { get; init; }
    private WaitableDict<IPEndPoint, ProxyClientContext> _context = new();
    private WaitableDict<string, HashSet<IPAddress>> _users = new();

    private readonly TcpServer _server;
    private readonly ISettings _settings;
    private readonly Logger _logger;
    public Proxy(ISettings settings)
    {
        _settings = settings;

        _server = new TcpServer(_settings.InternalTcpEndPoint);
        _server.OnConnected += Server_OnConnected;
        _server.OnReaded += Server_OnReaded;
        _server.OnClientDisconnect += Server_OnClientDisconnect;
        _server.OnError += Server_OnError;

        _logger = _settings.Logger!;

        if (_settings.IsTlsProxy)
        {
            _proxyCert = X509CertificateLoader.LoadPkcs12FromFile(
                _settings.ProxyCrtPath,
                _settings.ProxyCrtPasswd,
                X509KeyStorageFlags.MachineKeySet |
                X509KeyStorageFlags.PersistKeySet |
                X509KeyStorageFlags.Exportable);

            if (!checkProxyCA())
                _logger.Error("Invalid proxy certificate");
        }
    }

    internal void RemoveUserConnection(string username, TcpClientWrapper client)
    {
        try
        {
            if (_users.TryGetValue(username, out HashSet<IPAddress>? users))
            {
                if (users.Count == 0)
                {
                    _users.TryRemove(username, out _);
                    return;
                }
                users.Remove(client.EndPoint!.Address);
            }
        }
        catch
        {
            _users.TryRemove(username, out _);
        }
    }
    internal bool AddUserConnectionIfNeeded(string username, TcpClientWrapper client)
    {
        try
        {
            if (_users.TryGetValue(username, out HashSet<IPAddress>? users))
            {
                if (users.Count >= _settings.MaxUserConnection)
                    return false;
                users.Add(client.EndPoint!.Address);
            }
            else
            {
                var userConnections = new HashSet<IPAddress>();
                userConnections.Add(client.EndPoint!.Address);
                _users.Add(username, userConnections);
            }

            _logger.Information("Authorization success client: {@Client} username: {@User}"
                , client!.EndPoint!.ToString()
                , username);
            return true;
        }
        catch
        {
            return false;
        }
    }
    private bool checkProxyCA()
    {
        try
        {
            if (!_proxyCert.HasPrivateKey)
                return false;

            if (string.IsNullOrEmpty(_proxyCert.Thumbprint))
                return false;

            if (string.IsNullOrEmpty(_proxyCert.SerialNumber))
                return false;

            if (string.IsNullOrEmpty(_proxyCert.SubjectName.Name))
                return false;

            var san = _proxyCert.Extensions.OfType<X509SubjectAlternativeNameExtension>().First();
            if (san.EnumerateIPAddresses().Count() == 0 && san.EnumerateDnsNames().Count() == 0)
                return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch
        {
            return false;
        }

        return true;
    }
    public async Task StartAsync()
    {
        _logger.Information("Starting proxy server {@Address}", _server.EndPoint.ToString());

        await _server.StartAsync();
    }

    public async Task StopAsync()
    {
        _logger.Information("Stopping proxy server {@Address}", _server.EndPoint.ToString());

        await _server.StopAsync();
    }

    private async void Server_OnError(Exception ex, TcpClientWrapper? client)
    {
        try
        {
            if (client is null || client.EndPoint is null)
                return;

            if (_context!.TryRemove(client.EndPoint, out var context))
            {
                _logger.Error("{@Msg} client: {@Client} username: {@User}"
                , ex.Message
                , client!.EndPoint!.ToString()
                , context.Username);

                var TcpTask = context.TcpTunnel?.StopAsync();
                var UdpTask = context.UdpRedirector?.StopAsync();

                if(TcpTask != null)
                    await TcpTask;

                if(UdpTask != null)
                    await UdpTask;

                context.SocksProtocol?.Context?.BindServer?.Dispose();
                context.TcpTunnel?.Dispose();
                context.UdpRedirector?.Dispose();

                if (context.Username != null)
                    RemoveUserConnection(context.Username, client);
            }
        }
        catch
        {
            return;
        }
    }
    private async void Tunnel_OnError(TcpTunnel tunnel, Exception ex)
    {
        try
        {
            if (tunnel.Source.EndPoint is null)
                return;

            if (_context!.TryRemove(tunnel.Source.EndPoint, out var context))
            {
                _logger.Error("{@Msg} client: {@Client} username: {@User}"
                , ex.Message
                , tunnel.Source.EndPoint.ToString()
                , context.Username);

                var TcpTask = context.TcpTunnel?.StopAsync();
                var UdpTask = context.UdpRedirector?.StopAsync();

                if (TcpTask != null)
                    await TcpTask;

                if (UdpTask != null)
                    await UdpTask;

                context.SocksProtocol?.Context?.BindServer?.Dispose();
                context.TcpTunnel?.Dispose();
                context.UdpRedirector?.Dispose();

                if (context.Username != null)
                    RemoveUserConnection(context.Username, tunnel.Source);
            }
        }
        catch
        {
            return;
        }
    }
    private async void Server_OnClientDisconnect(TcpClientWrapper client)
    {
        try
        {
            if (client.EndPoint == null)
                return;

            if (_context!.TryRemove(client.EndPoint, out var context))
            {
                _logger.Information("Disconnect client: {@Client} username: {@User}"
                , client.EndPoint.ToString()
                , context.Username);

                var TcpTask = context.TcpTunnel?.StopAsync();
                var UdpTask = context.UdpRedirector?.StopAsync();

                if (TcpTask != null)
                    await TcpTask;

                if (UdpTask != null)
                    await UdpTask;

                context.SocksProtocol?.Context?.BindServer?.Dispose();
                context.TcpTunnel?.Dispose();
                context.UdpRedirector?.Dispose();

                if (context.Username != null)
                    RemoveUserConnection(context.Username, client);
            }
        }
        catch
        {
            return;
        }
    }
    private async void Server_OnReaded(TcpClientWrapper client, byte[] data)
    {
        ProxyClientContext? context;
        if (client.EndPoint == null || !_context.TryGetValue(client.EndPoint, out context))
            return;

        string? firstLine = GetFirstLine(data);

        if (firstLine == null)
            return;

        try
        {
            if (firstLine.StartsWith("CONNECT"))
                await HandleHttps(client, context, data);
            else if (firstLine.Contains("HTTP/"))
                await HandleHttp(client, context, data);
            else
            {
                if (context.TcpTunnel is not null || context.UdpRedirector is not null)
                    return;
                await HandleSocks(client, context, data);
            }
        }
        catch (Exception ex)
        {
            Server_OnError(ex, client);
        }
    }

    private async Task HandleSocks(TcpClientWrapper client, ProxyClientContext proxyContext, byte[] data)
    {
        SocksProtocol? protocol = proxyContext.SocksProtocol;

        if (protocol is null)
        {
            var context = new SocksContext()
            {
                AuthEnabled = _settings.AuthEnable,
                ServerTcpEndPoint = _settings.ExternalTcpEndPoint,
                BindServerEndPoint = _settings.SocksExternalBindEndPoint,
                ServerUdpAddress = _settings.SocksExternalUdpAddress,
                CheckAddrType = b =>
                {
                    Atyp type = (Atyp)b;
                    return _settings.CheckAllowAddrType(type.ToString());
                },
                CheckRule = req => {
                    return _settings.CheckRule(new RuleManager.RuleInfo
                    {
                        Target = (req.Atyp.Equals(Atyp.Domain)) ? Encoding.UTF8.GetString(req.DstAddr) : new IPAddress(req.DstAddr).ToString(),
                        Source = client.EndPoint.Address.ToString(),
                        SourcePort = client.EndPoint.Port.ToString(), 
                        TargetPort = req.DstPort.ToString(),
                        Proto = "socks5",
                        Username = (_settings.AuthEnable && proxyContext.Username != null) ? proxyContext.Username : string.Empty!,
                        Command = req.Smd.ToString()
                    });
                },
                CheckAuth = req =>
                {
                    if (!_settings.AuthEnable)
                        return true;

                    string un = Encoding.UTF8.GetString(req.Username);
                    string? pass = null;
                    if( (pass = _settings.GetPassword(un)) != null 
                    && pass.Equals(Encoding.UTF8.GetString(req.Password)))
                    {
                        if (!AddUserConnectionIfNeeded(un, client))
                        {
                            _logger.Information("Authorization failed client: {@Client} username: {@User}"
                                , client.EndPoint!.ToString()
                                , proxyContext.Username);
                            return false;
                        }

                        proxyContext.Username = un;
                        return true;
                    }
                    _logger.Information("Authorization failed client: {@Client} username: {@User}"
                                , client.EndPoint!.ToString()
                                , proxyContext.Username);
                    return false;
                },
                CheckCommandType = b =>
                {
                    ConnectType type = (ConnectType)b;
                    return _settings.SocksCheckAllowCommand(type);
                }
            };
            protocol = proxyContext.SocksProtocol = new SocksProtocol(context);

            protocol.EndInit += async (SocksContext context, TcpConnectionServerResponse resp) =>
            {
                try
                {
                    if (context.TargetType.Equals(SocksContext.Atyp.IpV4)
                    || context.TargetType.Equals(SocksContext.Atyp.IpV6))
                    {
                        IPEndPoint targetEndpoint = IPEndPoint.Parse(context.TargetAddress + ":" + context.TargetPort.ToString());
                        switch (context.ConnectionType)
                        {
                            case ConnectType.CONNECT: {
                                    var target = new TcpClientWrapper(targetEndpoint);
                                    CreateTcpTunnel(client, target, AllowProtocols.SOCKS).StartAsync();

                                    _logger.Information("Connect client: {@Client} username: {@User} to {@Server} Protocol: Socks5"
                                        , client.EndPoint!.ToString()
                                        , proxyContext.Username
                                        , target.EndPoint!.ToString());
                                }
                                ;break;
                            case ConnectType.UDP:
                                {
                                    proxyContext.UdpRedirector = new UdpRedirector(client.EndPoint!, 0);
                                    resp.BndPort = (ushort)proxyContext.UdpRedirector.Port;
                                    proxyContext.UdpRedirector.Invoke();

                                    _logger.Information("Connect client: {@Client} username: {@User} to {@Server} Protocol: Socks5"
                                        , client.EndPoint!.ToString()
                                        , proxyContext.Username
                                        , targetEndpoint.ToString());
                                }
                                ;break;
                        }
                    }
                    else if (context.TargetType.Equals(SocksContext.Atyp.Domain))
                    {
                        var entry = await Dns.GetHostEntryAsync(context.TargetAddress);
                        switch (context.ConnectionType)
                        {
                            case ConnectType.CONNECT:
                                {
                                    var target = await CreateTargetConnection(entry, context.TargetPort);
                                    CreateTcpTunnel(client, target.client, AllowProtocols.SOCKS).StartAsync();
                                }
                                ;break;
                            case ConnectType.UDP:
                                {
                                    proxyContext.UdpRedirector = new UdpRedirector(client.EndPoint!, 0);
                                    resp.BndPort = (ushort)proxyContext.UdpRedirector.Port;
                                    proxyContext.UdpRedirector.Invoke();
                                }
                                ;break;
                        }

                        _logger.Information("Connect client: {@Client} username: {@User} to {@Server} Protocol: Socks5"
                                        , client.EndPoint!.ToString()
                                        , proxyContext.Username
                                        , context.TargetAddress);
                    }
                    await client.WriteAsync(resp.ToByteArray());
                    return;
                }
                catch (ApplicationException ex) when (ex.Message.Equals("Could not connect or find IP address"))
                {
                    resp.Rep = (byte)SocksContext.RepType.NETWORK_UNAVAILABLE;
                }
                catch (SocketException)
                {
                    resp.Rep = (byte)SocksContext.RepType.HOST_UNAVAILABLE;
                }
                catch (Exception)
                {
                    resp.Rep = (byte)SocksContext.RepType.PROXY_ERROR;
                }

                await client.WriteAsync(resp.ToByteArray());
                client.Disconnect();
            };

            protocol.OnError += (sender, e) =>
            {
                this.Server_OnError(e, client);
            };

            protocol.Bind += async (SocksContext context, TcpConnectionServerResponse resp) => 
            {
                try
                {
                    if (context.BindServerEndPoint is null)
                        throw new ApplicationException("The bind server address is not assigned");

                    context.BindServer = new TcpServer(context.BindServerEndPoint);
                    Task StartTcpServer = context.BindServer.StartAsync();

                    if (StartTcpServer.Status == TaskStatus.Canceled || StartTcpServer.Status == TaskStatus.Faulted)
                    {
                        resp.Rep = (byte)SocksContext.RepType.PROXY_ERROR;
                        await client.WriteAsync(resp.ToByteArray());
                        client.Disconnect();
                        return;
                    }

                    await client.WriteAsync(resp.ToByteArray());

                    TcpClientWrapper? connectedClient = null;

                    context.BindServer.OnConnected += externalClinet => connectedClient = externalClinet;
                    await Task.Delay(TimeSpan.FromSeconds(2));

                    if (connectedClient is null || !connectedClient.CheckConnection())
                    {
                        resp.Rep = (byte)SocksContext.RepType.CONNECTION_REFUSAL;
                        await client.WriteAsync(resp.ToByteArray());
                        client.Disconnect();
                        return;
                    }

                    resp.Rep = 0x0;
                    resp.BndAddr = connectedClient!.EndPoint!.Address.GetAddressBytes();
                    resp.BndPort = (ushort)connectedClient.EndPoint.Port;

                    CreateTcpTunnel(client, connectedClient, AllowProtocols.SOCKS);

                    await client.WriteAsync(resp.ToByteArray());

                    _logger.Information("Connect client: {@Client} username: {@User} to {@Server} Protocol: Socks5"
                                        , client.EndPoint!.ToString()
                                        , proxyContext.Username
                                        , connectedClient.EndPoint!.ToString());
                }
                catch (ApplicationException ex) when (ex.Message.Equals("Could not connect or find IP address"))
                {
                    resp.Rep = (byte)SocksContext.RepType.NETWORK_UNAVAILABLE;
                    await client.WriteAsync(resp.ToByteArray());
                    client.Disconnect();
                }
                catch (SocketException)
                {
                    resp.Rep = (byte)SocksContext.RepType.HOST_UNAVAILABLE;
                    await client.WriteAsync(resp.ToByteArray());
                    client.Disconnect();
                }
                catch (Exception ex)
                {
                    resp.Rep = (byte)SocksContext.RepType.PROXY_ERROR;
                    await client.WriteAsync(resp.ToByteArray());
                    this.Server_OnError(ex, client);
                }
            };
        }

        var sendData = protocol?.InitAsServer(data);
        if(sendData is not null && sendData.Length > 0) 
            await client.WriteAsync(sendData);

        if(protocol is not null && protocol.Context.Error != null)
            client.Disconnect();
    }

    private async Task<bool> HttpAuth(
        TcpClientWrapper client,
        HttpRequest req,
        ProxyClientContext proxyContext)
    {
        if (!this._settings.AuthEnable)
            return true;

        try
        {
            if (proxyContext.Auth is null)
            {
                proxyContext.Auth = this._settings.MakeAuth(_settings.GetPassword, proxyContext, this, client);
                var resp = proxyContext.Auth.Next(req);

                if (resp is HttpResponce authResp)
                    await client.WriteAsync(authResp!.ToByteArray());
                else if(resp is Ref<bool> result)
                    return result;
                    
                return false;
            }
            else if (!proxyContext.Auth.IsEnd())
            {
                var resp = proxyContext.Auth.Next(req);

                switch (resp)
                {
                    case bool result: return result;
                    default: return false;
                }
            }
            return true;
        }
        catch
        {
            await client.WriteAsync(HttpServerResponses.Forbidden.ToByteArray());
        }
        await client.WriteAsync(HttpServerResponses.Forbidden.ToByteArray());
        _logger.Information("Authorization failed client: {@Client} username: {@User}"
                                , client.EndPoint!.ToString()
                                , proxyContext.Username);
        return false;
    }
    private async Task HandleHttps(TcpClientWrapper client, ProxyClientContext context, byte[] data)
    {
        try
        {
            HttpRequest req = HttpRequest.ParseHeader(data);

            bool isContinue = await HttpAuth(client, req, context!);
            if (!isContinue)
                return;

            var host_port = req.Uri.AbsoluteUri.Split(":");
            var host = host_port[0];
            var port = int.Parse(host_port[1]);

            var entry = await Dns.GetHostEntryAsync(host);
            var target = await CreateTargetConnection(entry, port);

            bool IsRuleAccess = _settings.CheckRule(new RuleManager.RuleInfo
            {
                Target = host,
                Source = client.EndPoint!.Address.ToString(),
                SourcePort = client.EndPoint.Port.ToString(),
                TargetPort = port.ToString(),
                Proto = "https",
                Username = (_settings.AuthEnable && context.Username != null) ? context.Username : string.Empty!,
                Command = "connect"
            });

            if (!IsRuleAccess)
            {
                await client.WriteAsync(HttpServerResponses.Forbidden.ToByteArray());
                _logger.Information("Reject by rule: client: {@Client} username: {@User} to {@Server} Protocol: HTTPS"
                                        , client.EndPoint!.ToString()
                                        , context.Username
                                        , target.client.EndPoint!.ToString());
                return;
            }

            if (!client.CheckConnection() || target.client is null)
                return;

            CreateTcpTunnel(client, target.client, AllowProtocols.HTTPS).StartAsync();

            await client.WriteAsync(HttpServerResponses.Connected.ToByteArray());

            _logger.Information("Connect client: {@Client} username: {@User} to {@Server} Protocol: HTTPS"
                                        , client.EndPoint!.ToString()
                                        , context.Username
                                        , target.client.EndPoint!.ToString());
        }
        catch (SocketException)
        {
            await client.WriteAsync(HttpServerResponses.GatewayTimeout.ToByteArray());
            throw;
        }
        catch (ApplicationException ex) when (ex.Message.Equals("Could not connect or find IP address"))
        {
            await client.WriteAsync(HttpServerResponses.BadGateway.ToByteArray());
            throw;
        }
        catch
        {
            await client.WriteAsync(HttpServerResponses.InternalError.ToByteArray());
            throw;
        }
    }

    private async Task HandleHttp(TcpClientWrapper client, ProxyClientContext context, byte[] data)
    {
        try
        {
            var req = HttpRequest.Parse(data);

            bool isContinue = await HttpAuth(client, req, context!);
            if (!isContinue)
                return;

            var entry = await Dns.GetHostEntryAsync(req.Uri!.Host);
            var target = await CreateTargetConnection(entry, _settings.DefaultHttpPort);

            bool IsRuleAccess = _settings.CheckRule(new RuleManager.RuleInfo
            {
                Target = req.Uri!.Host,
                Source = client.EndPoint!.Address.ToString(),
                SourcePort = client.EndPoint.Port.ToString(),
                TargetPort = target.client.EndPoint!.Port.ToString(),
                Proto = "https",
                Username = (_settings.AuthEnable && context.Username != null) ? context.Username : string.Empty!,
                Command = "connect"
            });

            if (!IsRuleAccess)
            {
                await client.WriteAsync(HttpServerResponses.Forbidden.ToByteArray());
                _logger.Information("Reject by rule client: {@Client} username: {@User} to {@Server} Protocol: HTTP"
                                        , client.EndPoint!.ToString()
                                        , context.Username
                                        , target.client.EndPoint!.ToString());
                return;
            }

            if (!client.CheckConnection() || target.client is null)
                return;

            CreateTcpTunnel(client, target.client, AllowProtocols.HTTP).StartAsync();

            _logger.Information("Connect client: {@Client} username: {@User} to {@Server} Protocol: HTTP"
                                        , client.EndPoint!.ToString()
                                        , context.Username
                                        , target.client.EndPoint!.ToString());
        }
        catch (SocketException)
        {
            await client.WriteAsync(HttpServerResponses.GatewayTimeout.ToByteArray());
            throw;
        }
        catch (ApplicationException ex) when (ex.Message.Equals("Could not connect or find IP address"))
        {
            await client.WriteAsync(HttpServerResponses.BadGateway.ToByteArray());
            throw;
        }
        catch
        {
            await client.WriteAsync(HttpServerResponses.InternalError.ToByteArray());
            throw;
        }
    }
    private void Server_OnConnected(TcpClientWrapper client)
    {
        try
        {
            if (client.EndPoint is null)
                return;

            _context.Add(client.EndPoint, new ProxyClientContext());

            _logger.Information("Try connection to proxy client: {@Client}", client.EndPoint!.ToString());

            if (_settings.IsTlsProxy)
            {
                SslStream sslStream = new SslStream(
                    client.Stream,
                    leaveInnerStreamOpen: false,
                    userCertificateValidationCallback: null,
                    userCertificateSelectionCallback: null
                );

                var sslOptions = new SslServerAuthenticationOptions
                {
                    ServerCertificate = _proxyCert,
                    ClientCertificateRequired = false,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13 | SslProtocols.Tls11 | SslProtocols.Ssl2 | SslProtocols.Ssl3,

                    ApplicationProtocols = new List<SslApplicationProtocol>
                    {
                        SslApplicationProtocol.Http11,
                        SslApplicationProtocol.Http2
                    },

                    EncryptionPolicy = EncryptionPolicy.RequireEncryption
                };

                sslStream.AuthenticateAsServer(sslOptions);
                client.Stream = sslStream;
            }
        }
        catch (AuthenticationException ex)
        {
            _logger.Error("Ssl Error - {@Msg} client: {@Client}", ex.Message, client.EndPoint!.ToString());
            this.Server_OnClientDisconnect(client);
        }
        catch
        {
            this.Server_OnClientDisconnect(client);
        }
    }
    public void Dispose()
    {
        this.StopAsync().Wait();
        foreach (var item in _context)
        {
            var TcpTask = item.Value.TcpTunnel?.StopAsync();
            var UdpTask = item.Value.UdpRedirector?.StopAsync();

            if (TcpTask != null)
                TcpTask.Wait();

            if (UdpTask != null)
                UdpTask.Wait();

            item.Value.SocksProtocol?.Context?.BindServer?.Dispose();
            item.Value.TcpTunnel?.Dispose();
            item.Value.UdpRedirector?.Dispose();
        }

        _logger.Dispose();
        _server.Dispose();
    }

    private TcpTunnel CreateTcpTunnel(TcpClientWrapper source, TcpClientWrapper target, AllowProtocols protocol)
    {
        if (source.EndPoint is null)
            throw new ArgumentNullException(nameof(source.EndPoint));

        TcpTunnel? newTunnel = null;
        if (protocol.Equals(AllowProtocols.HTTPS))
        {
            newTunnel = new HttpsTunnel(source, target);
        }
        else if (protocol.Equals(AllowProtocols.HTTP))
        {
            newTunnel = new HttpTunnel(source, target);
        }
        else if (protocol.Equals(AllowProtocols.SOCKS))
        {
             newTunnel = new SocksTunnel(source, target);
        }
        newTunnel!.OnError += Tunnel_OnError;

        if (_context.TryGetValue(source.EndPoint, out var proxyContext))
            proxyContext.TcpTunnel = newTunnel;
        else
            throw new ApplicationException("Сlient not found");

        return newTunnel;
    }
    private async Task<(TcpClientWrapper client, IPEndPoint addr)> CreateTargetConnection(IPHostEntry entry, int port)
    {
        var connected = new WaitableDict<TcpClientWrapper, IPEndPoint>();
        using var cts = new CancellationTokenSource();
        await Parallel.ForEachAsync(entry.AddressList, async (IPAddress addr, CancellationToken token) =>
        {
            try
            {
                cts.Token.ThrowIfCancellationRequested();
                IPEndPoint enpoint = new IPEndPoint(addr, port);
                var res = new TcpClientWrapper(enpoint);

                await Task.Delay(1);

                if (res.CheckConnection())
                {
                    connected.Add(res, enpoint);
                    cts.Cancel();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                return;
            }
        });

        if(connected.Count > 0)
        {
            var pair = connected.Last();
            return (pair.Key, pair.Value);
        }
        throw new ApplicationException("Could not connect or find IP address");
    }
    private string? GetFirstLine(byte[] data)
    {
        using MemoryStream stream = new MemoryStream(data);
        using StreamReader reader = new StreamReader(stream);
        return reader.ReadLine();
    }

    public enum AllowProtocols
    {
        HTTPS,
        HTTP,
        SOCKS,
    }

    public class ProxyClientContext()
    {
        public TcpTunnel? TcpTunnel { get; set; } = null;
        public UdpRedirector? UdpRedirector { get; set; } = null;
        public SocksProtocol? SocksProtocol { get; set; } = null;
        public IAuth? Auth { get; set; } = null;
        public string? Username { get; set; } = null;
    }

}
