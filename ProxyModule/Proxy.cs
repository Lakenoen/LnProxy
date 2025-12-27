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

namespace ProxyModule;
public class Proxy : IDisposable
{
    private static int _defaultHttpPort = 80;
    private X509Certificate2 _proxyCert { get; init; }
    private ConcurrentDictionary<IPEndPoint, ProxyClientContext> _context = new();
    private readonly TcpServer _server;
    private readonly ISettings _settings;
    public Proxy(ISettings settings)
    {
        _settings = settings;

        _server = new TcpServer(IPEndPoint.Parse("0.0.0.0:443"));
        _server.OnConnected += Server_OnConnected;
        _server.OnReaded += Server_OnReaded;
        _server.OnClientDisconnect += Server_OnClientDisconnect;
        _server.OnError += Server_OnError;

        _proxyCert = X509CertificateLoader.LoadPkcs12FromFile(
            _settings.ProxyCrtPath,
            _settings.ProxyCrtPasswd,
            X509KeyStorageFlags.MachineKeySet |
            X509KeyStorageFlags.PersistKeySet |
            X509KeyStorageFlags.Exportable);
        checkProxyCA();
    }

    private bool checkProxyCA()
    {
        if (!_proxyCert.Verify())
        {
            return false;
        }
        if (!_proxyCert.HasPrivateKey)
        {
            Console.WriteLine("Сертификат не имеет приватного ключа!");
        }

        Console.WriteLine($"HasPrivateKey: {_proxyCert.HasPrivateKey}");
        Console.WriteLine($"PrivateKey: {_proxyCert.PrivateKey}");
        Console.WriteLine($"GetRSAPrivateKey(): {_proxyCert.GetRSAPrivateKey() != null}");


        var san = _proxyCert.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .FirstOrDefault();

        if (san != null)
        {
            foreach (var name in san.EnumerateDnsNames())
                Console.WriteLine($"SAN DNS: {name}");
            foreach (var ip in san.EnumerateIPAddresses())
                Console.WriteLine($"SAN IP: {ip}");
        }
        return true;
    }
    public async Task StartAsync()
    {
        await _server.StartAsync();
    }

    public async Task StopAsync()
    {
        await _server.StopAsync();
    }

    private async void Server_OnError(Exception ex, TcpClientWrapper? client)
    {
        try
        {
            if (client is null || client.EndPoint is null)
                return;

            if (_context!.Remove(client.EndPoint, out var context))
            {
                var TcpTask = context.TcpTunnel?.StopAsync();
                var UdpTask = context.UdpTunnel?.StopAsync();

                if(TcpTask != null)
                    await TcpTask;

                if (UdpTask != null)
                    await UdpTask;

                context.SocksProtocol?.Context?.BindServer?.Dispose();
                context.TcpTunnel?.Dispose();
                context.UdpTunnel?.Dispose();
            }
        }
        catch (Exception)
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

            if (_context!.Remove(tunnel.Source.EndPoint, out var context))
            {
                var TcpTask = context.TcpTunnel?.StopAsync();
                var UdpTask = context.UdpTunnel?.StopAsync();

                if (TcpTask != null)
                    await TcpTask;

                if (UdpTask != null)
                    await UdpTask;

                context.SocksProtocol?.Context?.BindServer?.Dispose();
                context.TcpTunnel?.Dispose();
                context.UdpTunnel?.Dispose();
            }
        }
        catch (Exception)
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

            if (_context!.Remove(client.EndPoint, out var context))
            {
                var TcpTask = context.TcpTunnel?.StopAsync();
                var UdpTask = context.UdpTunnel?.StopAsync();

                if (TcpTask != null)
                    await TcpTask;

                if (UdpTask != null)
                    await UdpTask;

                context.SocksProtocol?.Context?.BindServer?.Dispose();
                context.TcpTunnel?.Dispose();
                context.UdpTunnel?.Dispose();
            }
        }
        catch (Exception)
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
                if (context.TcpTunnel is not null || context.UdpTunnel is not null)
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
                ServerTcpEndPoint = _settings.SocksExternalTcpEndPoint,
                BindServerEndPoint = _settings.SocksExternalBindEndPoint,
                ServerUdpEndPoint = _settings.SocksExternalUdpEndPoint,
                CheckAddrType = b =>
                {
                    Atyp type = (Atyp)b;
                    return _settings.CheckAllowAddrType(type.ToString());
                },
                CheckRule = req => {
                    return _settings.CheckRule(new ISettings.RuleInfo
                    {
                        TargetAddr = (req.Atyp.Equals(Atyp.Domain)) ? Encoding.UTF8.GetString(req.DstAddr) : new IPAddress(req.DstAddr).ToString(),
                        SourceAddr = client.EndPoint.Address.ToString(),
                        SourcePort = client.EndPoint.Port.ToString(),
                        TargetPort = req.DstPort.ToString(),
                        Proto = "socks5",
                        Username = (_settings.AuthEnable && proxyContext.Username != null) ? proxyContext.Username : string.Empty!
                    });
                },
                CheckAuth = req =>
                {
                    string un = Encoding.UTF8.GetString(req.Username);
                    string? pass = null;
                    if( (pass = _settings.GetPassword(un)) != null 
                    && pass.Equals(Encoding.UTF8.GetString(req.Password)))
                    {
                        proxyContext.Username = un;
                        return true;
                    }
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
                                };break;
                            case ConnectType.UDP:
                                {
                                    CreateUdpTunnel(targetEndpoint, proxyContext).StartAsync();
                                };break;
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
                                };break;
                            case ConnectType.UDP:
                                {
                                    IPEndPoint sourceUdpEndPoint = new IPEndPoint(entry.AddressList.First(), context.TargetPort);
                                    CreateUdpTunnel(sourceUdpEndPoint, proxyContext).StartAsync();
                                };break;
                        }
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

    private IPEndPoint UdpTunnel_OnRecv(SocksContext context,IPEndPoint from, IPEndPoint clientEndPoint, byte[] data)
    {
        if ( (from.Address.Equals(clientEndPoint.Address) && clientEndPoint.Port == 0)
            || from.Equals(clientEndPoint))
        {
            var packet = SocksContext.UdpPacket.Parse(data);
            return new IPEndPoint(new IPAddress(packet.DstAddr), packet.DstPort);
        }
        return clientEndPoint;
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
                proxyContext.Auth = new DigestAuth(_settings.GetPassword, proxyContext);
                var authResp = proxyContext.Auth.Next(req) as HttpResponce;
                string s = authResp!.ToString();
                await client.WriteAsync(authResp!.ToByteArray());
                return false;
            }
            else if (!proxyContext.Auth.IsEnd())
            {
                return proxyContext.Auth.Next(req) as Ref<bool>;
            }
            return true;
        }
        catch
        {
            await client.WriteAsync(HttpServerResponses.Forbidden.ToByteArray());
        }
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

            if (!client.CheckConnection() || target.client is null)
                return;

            CreateTcpTunnel(client, target.client, AllowProtocols.HTTPS).StartAsync();

            await client.WriteAsync(HttpServerResponses.Connected.ToByteArray());
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
            var entry = await Dns.GetHostEntryAsync(req.Uri!.Host);
            var target = await CreateTargetConnection(entry, _defaultHttpPort);

            if (!client.CheckConnection() || target.client is null)
                return;

            CreateTcpTunnel(client, target.client, AllowProtocols.HTTP).StartAsync();
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

            _context.TryAdd(client.EndPoint, new ProxyClientContext());


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
            var win32Ex = ex.InnerException as System.ComponentModel.Win32Exception;
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
            var UdpTask = item.Value.UdpTunnel?.StopAsync();

            if (TcpTask != null)
                TcpTask.Wait();

            if (UdpTask != null)
                UdpTask.Wait();

            item.Value.SocksProtocol?.Context?.BindServer?.Dispose();
            item.Value.TcpTunnel?.Dispose();
            item.Value.UdpTunnel?.Dispose();
        }

        _server.Dispose();
    }
    private UdpTunnel CreateUdpTunnel(IPEndPoint sourceEndPoint, ProxyClientContext proxyContext)
    {
        SocksContext context = proxyContext.SocksProtocol!.Context;
        proxyContext.UdpTunnel = new UdpTunnel(context.ServerUdpEndPoint!.Port);

        proxyContext.UdpTunnel.OnRecv += (from, data) => UdpTunnel_OnRecv(context, from, sourceEndPoint, data);
        return proxyContext.UdpTunnel;
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
        var connected = new ConcurrentQueue<(TcpClientWrapper client, IPEndPoint addr)>();
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
                    cts.Cancel();
                    connected.Enqueue((res, enpoint));
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

        if(connected.TryPeek(out var res))
        {
            return res;
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

    internal class ProxyClientContext()
    {
        public TcpTunnel? TcpTunnel { get; set; } = null;
        public UdpTunnel? UdpTunnel { get; set; } = null;
        public SocksProtocol? SocksProtocol { get; set; } = null;
        public DigestAuth? Auth { get; set; } = null;
        public string? Username { get; set; } = null;
    }

}
