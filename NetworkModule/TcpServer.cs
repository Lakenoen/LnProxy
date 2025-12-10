using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Data.SqlTypes;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;

namespace NetworkModule;

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
    private async void Wrapper_OnDisconnect(TcpClientWrapper tcpClientWrapper)
    {
        OnClientDisconnect?.Invoke(tcpClientWrapper);
        await DropClient(tcpClientWrapper);
    }

    public async Task DropClient(TcpClientWrapper tcpClientWrapper)
    {
        if (_clients.TryRemove(tcpClientWrapper, out ClientInfo? clientInfo))
        {
            clientInfo._clientCancel.Cancel();
            await clientInfo.clientTask;
            clientInfo.Dispose();
        }
    }
    private async Task Read(TcpClientWrapper client, CancellationToken clientCancel)
    {
        if(_cancel == null)
            throw new ArgumentNullException(nameof(clientCancel));

        CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(clientCancel, _cancel.Token);
        await Task.Run(async () =>
        {
            try
            {
                while (!source.Token.IsCancellationRequested)
                {
                    byte[] data = await client.ReadAvailableAsync(source.Token);
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
                OnError?.Invoke(ex, client);
                await DropClient(client);
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
        while (!_cancel!.Token.IsCancellationRequested)
        {
            TcpClient client = _listener.AcceptTcpClientAsync(_cancel!.Token).Result;
            var wrapper = new TcpClientWrapper(client);
            wrapper.OnDisconnect += Wrapper_OnDisconnect;

            CancellationTokenSource clientCancel = new CancellationTokenSource();
            var readTask = Read(wrapper, clientCancel.Token);
            _clients.TryAdd(wrapper, new ClientInfo(wrapper, DateTime.Now, clientCancel, readTask));

            OnConnected?.Invoke(wrapper);
        }
    }

    protected override void End()
    {
        _listener.Stop();
    }

    protected override void Error(Exception ex)
    {
        OnError?.Invoke(ex, null);
    }

    protected override void Init()
    {
        _listener.Start();
    }

    public class ClientInfo : IDisposable
    {
        public TcpClientWrapper Client { get; init; }
        public DateTime connectionTime { get; init; }
        public CancellationTokenSource _clientCancel { get; init; }
        public Task clientTask { get; init; }
        public ClientInfo(TcpClientWrapper client, DateTime connectionTime, CancellationTokenSource _clientCancel, Task clientTask)
        {
            this._clientCancel = _clientCancel;
            this.connectionTime = connectionTime;
            this.Client = client;
            this.clientTask = clientTask;
        }

        public void Dispose()
        {
            _clientCancel?.Dispose();
            Client.Dispose();
        }
    }

    public delegate void Readed(TcpClientWrapper client, byte[] data);

    public event Readed? OnReaded;
    public event Action<TcpClientWrapper>? OnConnected;
    public event Action<TcpClientWrapper>? OnClientDisconnect;
    public event Action<Exception, TcpClientWrapper?>? OnError;
}
