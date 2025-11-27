using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpModule;

public class TcpServer : AStartableAsync, IDisposable
{
    private TcpListener _listener;
    private ConcurrentDictionary<TcpClientWrapper, ClientInfo> _clients = new ConcurrentDictionary<TcpClientWrapper, ClientInfo>();
    public IPEndPoint EndPoint { get; init; }
    public FrozenDictionary<TcpClientWrapper, ClientInfo> Clients
    {
        get => _clients.ToFrozenDictionary();
    }
    public TcpServer(IPEndPoint endPoint) : base()
    {
        this.EndPoint = endPoint;
        _listener = new TcpListener(this.EndPoint);
    }
    private void Wrapper_OnDisconnect(TcpClientWrapper tcpClientWrapper)
    {
        OnClientDisconnect?.Invoke(tcpClientWrapper);
        _clients.TryRemove(tcpClientWrapper, out _);
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

                    byte[] data = client.ReadAvailableAsync(_cancel.Token).Result;
                    if (data.Length != 0)
                        OnReaded?.Invoke(client, data);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (IOException)
            {
                return;
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

            _clients.TryAdd(wrapper, new ClientInfo(wrapper, DateTime.Now));

            wrapper.OnDisconnect += Wrapper_OnDisconnect;
            OnConnected?.Invoke(wrapper);
            var readTask = Read(wrapper);
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

    public event Action<TcpClientWrapper>? OnClientDisconnect;

    public event Action<Exception>? OnError;
}
