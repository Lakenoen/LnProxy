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

        //TODO
        _proxyCert = X509CertificateLoader.LoadPkcs12FromFile(_settings.ProxyCrtPath, _settings.ProxyCrtPasswd);
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
                await HandleHttps(client, data);
            else if (firstLine.Contains("HTTP/"))
                await HandleHttp(client, data);
            else
            {
                if (context.TcpTunnel is not null || context.UdpTunnel is not null)
                    return;
                await HandleSocks(client, data);
            }
        }
        catch (Exception ex)
        {
            Server_OnError(ex, client);
        }
    }

    private async Task HandleSocks(TcpClientWrapper client, byte[] data)
    {
        _context.TryGetValue(client.EndPoint!, out ProxyClientContext? proxyContext);

        if (proxyContext is null)
            return;

        SocksProtocol? protocol = proxyContext.SocksProtocol;

        if (protocol is null)
        {
            var context = new SocksContext()
            {
                ServerTcpEndPoint = IPEndPoint.Parse("0.0.0.0:0"),
                BindServerEndPoint = IPEndPoint.Parse("192.168.0.103:8889"),
                ServerUdpEndPoint = IPEndPoint.Parse("192.168.0.103:8890"),
                CheckAddrType = b => true,
                CheckRule = req => true,
                CheckAuth = req => true,
                CheckCommandType = b => true,
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

    private async Task HandleHttps(TcpClientWrapper client, byte[] data)
    {
        try
        {
            HttpRequest req = HttpRequest.ParseHeader(data);
            var host_port = req.Headers["Host"].Split(":");
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

    private async Task HandleHttp(TcpClientWrapper client, byte[] data)
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
        if (client.EndPoint is null)
            return;

        _context.TryAdd(client.EndPoint, new ProxyClientContext());


        if (_settings.IsTlsProxy)
        {
            SslStream sslStream = new SslStream(client.Stream, false);
            sslStream.AuthenticateAsServer(_proxyCert, clientCertificateRequired: false, checkCertificateRevocation: true);
            client.Stream = sslStream;
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

    private class ProxyClientContext()
    {
        public TcpTunnel? TcpTunnel { get; set; } = null;
        public UdpTunnel? UdpTunnel { get; set; } = null;
        public SocksProtocol? SocksProtocol { get; set; } = null;
    }

}
