using System.Collections.Frozen;
using System.Net;
using System.Text;
using TcpModule;

namespace ProxyModule;
public class HttpsProxy
{
    private readonly TcpServer server;
    private Dictionary<IPEndPoint, TcpClientWrapper> clients = new Dictionary<IPEndPoint, TcpClientWrapper>();
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
    private void Server_OnError(Exception ex)
    {
        throw new NotImplementedException();
    }

    private void Server_OnClientDisconnect(TcpClientWrapper tcpClientWrapper)
    {
        int i = 0;
    }

    private void Server_OnReaded(TcpClientWrapper client, byte[] data)
    {
        IPEndPoint remoteEndPoint = server.Clients[client];
        FrozenDictionary<TcpClientWrapper, IPEndPoint> d = server.Clients;
        var enums = d.Where((KeyValuePair<TcpClientWrapper, IPEndPoint> el) =>
        {
            if (el.Value.Equals(remoteEndPoint))
                return true;
            else
                return false;
        });
        string strData = Encoding.UTF8.GetString(data);
    }

    private void Server_OnConnected(TcpClientWrapper client)
    {
        IPEndPoint remoteEndPoint = server.Clients[client];
        clients.Add(remoteEndPoint, client);
    }
}
