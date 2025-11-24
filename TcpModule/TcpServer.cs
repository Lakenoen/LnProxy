using System.Collections.Frozen;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpModule;

public class TcpServer : IDisposable
{
    private TcpListener _listener;
    private (bool item, object locker) _started = new(false, new());
    private CancellationTokenSource? _cancel;
    private Dictionary<TcpClientWrapper, IPEndPoint> _clients = new Dictionary<TcpClientWrapper, IPEndPoint>();
    private object _clientsLocker = new object();
    public IPEndPoint EndPoint { get; init; }
    public FrozenDictionary<TcpClientWrapper, IPEndPoint> Clients
    {
        get => _clients.ToFrozenDictionary();
        private set => _clients = value.ToDictionary();
    }
    public TcpServer(IPEndPoint endPoint)
    {
        this.EndPoint = endPoint;
        _listener = new TcpListener(endPoint);
        _cancel = new CancellationTokenSource();
    }
    public async Task StartAsync()
    {
        lock (_started.locker)
        {
            if (_started.item == true) return;
            _started.item = true;
        }
        try
        {
            _listener.Start();
            while (!_cancel!.Token.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_cancel.Token);
                var wrapper = new TcpClientWrapper(client);

                lock (_clientsLocker)
                {
                    _clients.Add(wrapper, client.Client.RemoteEndPoint as IPEndPoint);
                }

                wrapper.OnDisconnect += Wrapper_OnDisconnect;
                var readTask = Read(wrapper); 
                OnConnected?.Invoke(wrapper);
            }
        }
        catch (OperationCanceledException)
        {

        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
        finally
        {
            ResetCancel();
            lock (_started.locker)
            {
                _started.item = false;
            }
            _listener.Stop();
        }
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

    public void Stop()
    {
        if (_cancel != null && _cancel?.Token.IsCancellationRequested == false)
            _cancel?.Cancel();
    }
    public async Task StopAsync()
    {
        Stop();
        await Task.Run(() =>
        {
            while (true) {
                lock (_started.locker)
                {
                    if (!_started.item)
                        break;
                }
                Task.Delay(5);
            }
        });
    }

    private void ResetCancel()
    {
        if (_cancel != null)
        {
            _cancel?.Cancel();
            _cancel?.Dispose();
        }
        _cancel = new CancellationTokenSource();
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

    public void Dispose()
    {
        _cancel?.Cancel();
        _cancel?.Dispose();
        _listener.Stop();
        _listener.Dispose();
    }

    public delegate void Connected(TcpClientWrapper client);
    public event Connected? OnConnected;

    public delegate void Readed(TcpClientWrapper client, byte[] data);
    public event Readed? OnReaded;

    public event TcpClientWrapper.Disconnect? OnClientDisconnect;

    public delegate void Error(Exception ex);
    public event Error? OnError;
}
