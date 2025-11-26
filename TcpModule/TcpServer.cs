using System.Collections.Frozen;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpModule;

public class TcpServer : AStartableAsync, IDisposable
{
    private TcpListener _listener;
    private Dictionary<TcpClientWrapper, ClientInfo> _clients = new Dictionary<TcpClientWrapper, ClientInfo>();
    private object _clientsLocker = new object();
    public IPEndPoint EndPoint { get; init; }
    public FrozenDictionary<TcpClientWrapper, ClientInfo> Clients
    {
        get => _clients.ToFrozenDictionary();
        private set => _clients = value.ToDictionary();
    }
    public TcpServer(IPEndPoint endPoint) : base()
    {
        this.EndPoint = endPoint;
        _listener = new TcpListener(this.EndPoint);
    }
    private void Wrapper_OnDisconnect(TcpClientWrapper tcpClientWrapper)
    {
        OnClientDisconnect?.Invoke(tcpClientWrapper);
        lock (_clientsLocker)
        {
            _clients.Remove(tcpClientWrapper);
        }
        tcpClientWrapper.Dispose();
    }
    private async Task Read(TcpClientWrapper client)
    {
        await Task.Run(() =>
        {
            try
            {
                while (!_cancel!.Token.IsCancellationRequested)
                {
                    if (!client.CheckConnection())
                        return;
                    var data = client.ReadAvailable();

                    if (data.Length != 0)
                        OnReaded?.Invoke(client, data);
                }
            }
            catch (ObjectDisposedException ex)
            {

            }
            catch (IOException ex) {
                
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        });
    }
    public override int GetHashCode()
    {
        return HashCode.Combine(_listener.GetHashCode(), this.EndPoint.GetHashCode());
    }
    public void Dispose()
    {
        StopAsync().Wait();
        _cancel?.Dispose();
        _listener.Stop();
        _listener.Dispose();
        foreach (var item in _clients)
        {
            item.Key.Dispose();
        }
    }

    protected override void Start()
    {
        _listener.Start();
        while (!_cancel!.Token.IsCancellationRequested)
        {
            TcpClient client = _listener.AcceptTcpClientAsync(_cancel.Token).Result;
            var wrapper = new TcpClientWrapper(client);

            lock (_clientsLocker)
            {
                _clients.Add(wrapper, new ClientInfo(wrapper, DateTime.Now));
            }

            wrapper.OnDisconnect += Wrapper_OnDisconnect;
            var readTask = Read(wrapper);
            OnConnected?.Invoke(wrapper);
        }
    }

    protected override void End()
    {
        _listener.Stop();
    }

    protected override void Error(Exception ex)
    {
        OnError?.Invoke(ex);
    }

    public class ClientInfo(TcpClientWrapper client)
    {
        public TcpClientWrapper Client { get; private set; } = client;
        public DateTime connectionTime { get; set; }
        public ClientInfo(TcpClientWrapper client, DateTime connectionTime) : this(client)
        {
            this.connectionTime = connectionTime;
        }
    }

    public delegate void Connected(TcpClientWrapper client);
    public event Connected? OnConnected;

    public delegate void Readed(TcpClientWrapper client, byte[] data);
    public event Readed? OnReaded;

    public event TcpClientWrapper.Disconnect? OnClientDisconnect;

    public event Action<Exception>? OnError;
}
