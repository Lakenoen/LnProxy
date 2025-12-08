using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SocksModule;
using TcpModule;
using static SocksModule.SocksContext;

namespace ProxyModule;
public class Proxy : IDisposable
{
    private ConcurrentDictionary<IPEndPoint, SocksProtocol> _socksMap = new();
    private ConcurrentDictionary<IPEndPoint, TcpTunnel> _tunnels = new();
    private readonly TcpServer server;
    public Proxy()
    {
        server = new TcpServer(IPEndPoint.Parse("0.0.0.0:8888"));
        server.OnConnected += Server_OnConnected;
        server.OnReaded += Server_OnReaded;
        server.OnClientDisconnect += Server_OnClientDisconnect;
        server.OnError += Server_OnError;
    }

    public async Task StartAsync()
    {
        await server.StartAsync();
    }

    public async Task StopAsync()
    {
        await server.StopAsync();
    }

    private async void Server_OnError(Exception ex, TcpClientWrapper? client)
    {
        if (client == null)
            return;

        if (_tunnels!.Remove(client.EndPoint, out var tunnel))
        {
            await tunnel.StopAsync();
            tunnel.Dispose();
        }
    }
    private async void Tunnel_OnError(TcpTunnel tunnel, Exception ex)
    {
        if (tunnel.Source.EndPoint is not null)
            _tunnels.Remove(tunnel.Source.EndPoint, out _);

        await tunnel.StopAsync();
        tunnel.Dispose();
    }

    private async void Server_OnClientDisconnect(TcpClientWrapper tcpClientWrapper)
    {
        if (tcpClientWrapper.EndPoint == null)
            return;

        if (_tunnels.Remove(tcpClientWrapper.EndPoint, out var tunnel))
        {
            await tunnel.StopAsync();
            tunnel.Dispose();
        }
    }
    private async void Server_OnReaded(TcpClientWrapper client, byte[] data)
    {
        if (client.EndPoint == null)
            throw new ApplicationException("EndPoint is null");

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
                await HandleSocks(client, data);
        }
        catch (Exception ex)
        {
            Server_OnError(ex, client);
        }
    }

    private async Task HandleSocks(TcpClientWrapper client, byte[] data)
    {
        if (client.EndPoint is null || _tunnels.ContainsKey(client.EndPoint))
            return;

        SocksProtocol? protocol;

        if (!_socksMap.TryGetValue(client.EndPoint, out protocol))
        {
            var context = new SocksContext() { ServerEndPoint = IPEndPoint.Parse("0.0.0.0:0") };
            _socksMap.TryAdd(client.EndPoint, protocol = new SocksProtocol(context));

            protocol.EndInit += async (SocksContext context, byte[] resp) =>
            {
                if (context.TargetType.Equals(SocksContext.Atyp.IpV4)
                || context.TargetType.Equals(SocksContext.Atyp.IpV6))
                {
                    IPEndPoint targetEndpoint = IPEndPoint.Parse(context.TargetAddress + context.TargetPort.ToString());
                    var target = await CreateTargetConnection(targetEndpoint);
                    CreateTunnel(client, target, AllowProtocols.SOCKS5).StartAsync();
                } 
                else if (context.TargetType.Equals(SocksContext.Atyp.Domain))
                {
                    var entry = await Dns.GetHostEntryAsync(context.TargetAddress);
                    var target = await CreateTargetConnection(entry, context.TargetPort);
                    CreateTunnel(client, target, AllowProtocols.SOCKS5).StartAsync();
                }
                await client.WriteAsync(resp);
            };
        }

        var sendData = protocol.InitAsServer(data);
        if(sendData.Length > 0) 
            await client.WriteAsync(sendData);
    }
    private async Task HandleHttps(TcpClientWrapper client, byte[] data)
    {
        if (_tunnels.ContainsKey(client.EndPoint))
            return;

        HttpRequest req = HttpRequest.ParseHeader(data);
        var host_port = req.Headers["Host"].Split(":");
        var host = host_port[0];
        var port = int.Parse(host_port[1]);

        var entry = await Dns.GetHostEntryAsync(host);
        var target = await CreateTargetConnection(entry, port);

        if (!client.CheckConnection() || target is null)
            return;

        CreateTunnel(client, target, AllowProtocols.HTTPS).StartAsync();

        var response = new HttpResponce
        {
            Status = 200,
            Msg = "Connection Established",
            Headers =
                {
                    {"Connection","close"},
                    {"Cache-Control","no-cache, no-store, must-revalidate"},
                    {"Pragma","no-cache"},
                    {"Expires","0"}
                }
        };
        await client.WriteAsync(response.ToByteArray());
    }

    private async Task HandleHttp(TcpClientWrapper client, byte[] data)
    {
        var req = HttpRequest.Parse(data);
        var entry = await Dns.GetHostEntryAsync(req.Uri!.Host);
        var target = await CreateTargetConnection(entry, 80);

        if (!client.CheckConnection() || target is null)
            return;

        CreateTunnel(client, target, AllowProtocols.HTTP).StartAsync();
    }
    private void Server_OnConnected(TcpClientWrapper client)
    {
        
    }
    public void Dispose()
    {
        server.Dispose();
    }
    private TcpTunnel CreateTunnel(TcpClientWrapper source, TcpClientWrapper target, AllowProtocols protocol)
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
        else if (protocol.Equals(AllowProtocols.SOCKS5))
        {
            newTunnel = new SocksTunnel(source, target);
        }
        newTunnel!.OnError += Tunnel_OnError;
        _tunnels.TryAdd(source.EndPoint, newTunnel);
        return newTunnel;
    }
    private async Task<TcpClientWrapper> CreateTargetConnection(IPHostEntry entry, int port)
    {
        ConcurrentQueue<TcpClientWrapper> connected = new ConcurrentQueue<TcpClientWrapper>();
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
                    connected.Enqueue(res);
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
    private async Task<TcpClientWrapper> CreateTargetConnection(IPEndPoint addr)
    {
        return new TcpClientWrapper(addr);
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
        SOCKS5,
        SOCKS4
    }

}
