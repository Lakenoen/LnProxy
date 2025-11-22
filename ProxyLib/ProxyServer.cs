using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProxyLib;
public class ProxyServer : IExecutable
{
    private TcpListener? _listener;
    private (bool item, object locker) _started = new(false, new());
    private CancellationTokenSource? _cancel;
    private readonly Dictionary<IExecutable, IPEndPoint?> _connectedClients = new Dictionary<IExecutable, IPEndPoint?>();
    private ISettingsProxy SettingsProxy { get;set; }
    public ProxyServer(ISettingsProxy settings)
    {
        this.SettingsProxy = settings;
        Init();
    }

    private void Init()
    {
        _listener = new TcpListener(SettingsProxy.ServerIpEndPoint);
        _cancel = new CancellationTokenSource();
    }
    public async Task<AbstractResult<Empty>> RunAsync()
    {
        lock (_started.locker)
        {
            if (_started.item == true)
                return new ErrorResult<Empty>("The server is alredy running");
            _started.item = true;
        }

        try
        {
            if (_listener is null || _cancel is null)
                return new ErrorResult<Empty>("Server initialization error");

            _listener.Start();

            List<Task> connectionTasks = new List<Task>();

            while (!_cancel.Token.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_cancel.Token);
                connectionTasks.Add( ClientConnect(client) );
            }

            Task.WaitAll(connectionTasks);
        }
        catch (OperationCanceledException)
        {
            return new SuccessResult<Empty>("The server was successfully completed");
        }
        catch (Exception ex)
        {
            return new ErrorResult<Empty>(ex.Message);
        }
        finally
        {
            ResetCancel();
            lock (_started.locker)
            {
                _started.item = false;
            }
            _listener?.Stop();
        }

        return new SuccessResult<Empty>("The server was successfully completed");
    }
    private async Task ClientConnect(TcpClient client)
    {
        //TODO

        TcpHandler handler = new TcpHandler(client, _cancel!.Token);
        var handlerTask = handler.RunAsync();
        _connectedClients.Add(handler, client.Client.RemoteEndPoint as IPEndPoint);
        handler.CloseConnection += CloseConnection;
        AbstractResult<Empty> result =  await handlerTask;
        if(result is ErrorResult<Empty> && _connectedClients.ContainsKey(handler))
            CloseConnection(handler);
    }

    private void CloseConnection(IExecutable sender)
    {
        sender.Stop();
        _connectedClients?.Remove(sender);
        sender.Dispose();
    }

    public void Stop()
    {
        _cancel?.Cancel();
    }

    private void ResetCancel()
    {
        _cancel?.Cancel();
        _cancel?.Dispose();
        _cancel = new CancellationTokenSource();
    }
    public void Dispose()
    {
        Stop();
        _listener?.Dispose();
        _cancel?.Dispose();
    }

}
