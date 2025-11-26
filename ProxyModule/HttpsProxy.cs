using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Net;
using System.Text;
using TcpModule;

namespace ProxyModule;
public class HttpsProxy : IDisposable
{
    private ConcurrentDictionary<IPEndPoint, TcpTunel> _tunnels = new ConcurrentDictionary<IPEndPoint, TcpTunel>();
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
    private void Server_OnError(Exception ex)
    {
        throw new NotImplementedException();
    }

    private void Server_OnClientDisconnect(TcpClientWrapper tcpClientWrapper)
    {
        if (tcpClientWrapper.EndPoint == null)
            return;
        _tunnels.Remove(tcpClientWrapper.EndPoint, out _);
    }

    private void Server_OnReaded(TcpClientWrapper client, byte[] data)
    {
        if (client.EndPoint == null)
            throw new ApplicationException("EndPoint is null");

        IPEndPoint remoteEndPoint = client.EndPoint;
        string strReaded = Encoding.UTF8.GetString(data);

        if (strReaded.StartsWith("CONNECT"))
        {
            string targetAddr = strReaded.Split(' ')[1];
            var host = targetAddr.Split(':')[0];
            var port = int.Parse(targetAddr.Split(':')[1]);

            client.Write(Encoding.UTF8.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n"));

            var target = CreateTargetConnection(Dns.GetHostEntry(host), port);
            TcpTunel newTunel = new TcpTunel(client, target);
            _tunnels.TryAdd(client.EndPoint, newTunel);
            var tast = newTunel.StartAsync();
        }
        else if(strReaded.Contains("HTTP/"))
        {
             
        }
        else
        {
            TcpClientWrapper targetClient = _tunnels[client.EndPoint].Target;
            targetClient?.Write(data);
        }
    }

    private void Server_OnConnected(TcpClientWrapper client)
    {
        
    }
    public void Dispose()
    {
        server.Dispose();
    }
    private TcpClientWrapper CreateTargetConnection(IPHostEntry Entry, int port)
    {
        IPEndPoint enpoint = new IPEndPoint(Entry.AddressList.Last(), port);
        return new TcpClientWrapper(enpoint);
    }

}
