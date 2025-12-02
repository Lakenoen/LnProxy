using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Net;
using System.Text;
using TcpModule;

namespace ProxyModule;
public class HttpsProxy : IDisposable
{
    private ConcurrentDictionary<IPEndPoint, TcpTunnel> _tunnels = new ConcurrentDictionary<IPEndPoint, TcpTunnel>();
    private readonly TcpServer server;
    public HttpsProxy()
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

        using MemoryStream stream = new MemoryStream(data);
        using StreamReader reader = new StreamReader(stream);

        string? firstLine = reader.ReadLine();

        if (firstLine == null)
            return;

        if (firstLine.StartsWith("CONNECT"))
        {
            HttpReq req = HttpReq.ParseHeader(data);
            var host_port = req.Headers["Host"].Split(":");
            var host = host_port[0];
            var port = int.Parse(host_port[1]);

            var entry = await Dns.GetHostEntryAsync(host);
            var target = CreateTargetConnection(entry, port);

            if (!client.CheckConnection() || target is null)
                return;

            CreateTunnel(client, target);

            var response = new HttpRes { Status = 200, Msg = "Ok", Headers = 
                { 
                    {"Connection","close"},
                    {"Cache-Control","no-cache, no-store, must-revalidate"},
                    {"Pragma","no-cache"},
                    {"Expires","0"}
                }
            };
            client.WriteAsync(response.ToByteArray()).Wait();
        }
        else if (firstLine.Contains("HTTP/"))
        {
            var req = HttpReq.Parse(data);
            var entry = await Dns.GetHostEntryAsync(req.Uri.Host);
            var target = CreateTargetConnection(entry, 80);

            if (!client.CheckConnection() || target is null)
                return;

            CreateTunnel(client, target);
        }
        else
        {

        }
    }
    private void Server_OnConnected(TcpClientWrapper client)
    {
        
    }
    public void Dispose()
    {
        server.Dispose();
    }

    private void CreateTunnel(TcpClientWrapper source, TcpClientWrapper target)
    {
        TcpTunnel newTunel = new TcpTunnel(source, target);
        newTunel.OnError += Tunnel_OnError;
        var tunnelTask = newTunel.StartAsync();
        _tunnels.TryAdd(source.EndPoint, newTunel);
    }
    private TcpClientWrapper? CreateTargetConnection(IPHostEntry entry, int port)
    {
        foreach (var address in entry.AddressList)
        {
            IPEndPoint enpoint = new IPEndPoint(address, port);
            var res = new TcpClientWrapper(enpoint);
            if(res.CheckConnection()) return res;
        }

        Task.Delay(1).Wait();
        return null;
    }

}
