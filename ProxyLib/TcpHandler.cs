using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ProxyLib;
internal class TcpHandler(TcpClient client, AbstractProtocolFactory factory,CancellationToken cancel) : IStream, IExecutable
{
    public AbstractProtocolFactory Factory { get; set; } = factory;
    public TcpClient _client { get; init; } = client;
    private readonly CancellationToken _cancel = cancel;
    private CancellationTokenSource _internalCancel = new CancellationTokenSource();
    private (bool item, object locker) _started = new (false, new());
    byte[] _buffer = new byte[0xffff];
    public byte[] Buffer { get => _buffer; }
    public async Task<AbstractResult<Empty>> RunAsync()
    {

        lock (_started.locker)
        {
            if (_started.item == true)
                return new ErrorResult<Empty>("Handler alredy started");
            _started.item = true;
        }

        try
        {
            await Task.Factory.StartNew(() =>
            {
                //TODO
                CancellationTokenSource unionToken = CancellationTokenSource.CreateLinkedTokenSource(_internalCancel.Token, _cancel);
                AbstractResult<IProtocol> protocol = this.Factory.Create(this);
                if (protocol.IsError || protocol.Item is null)
                {
                    //TODO
                }
                protocol.Item!.Run(unionToken.Token);
            });
        }
        catch (OperationCanceledException)
        {
            return new SuccessResult<Empty>();
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
        }
        return new SuccessResult<Empty>();
    }
    public AbstractResult<byte[]> ReadAvailable()
    {
        if (!CheckConnection())
        {
            CloseConnection?.Invoke(this);
            return new ErrorResult<byte[]>("Connection closed");
        }

        var result = new Span<byte>(_buffer);
        int readed = 0;

        try
        {
            if ((readed = _client.GetStream().Read(result)) == 0)
            {
                CloseConnection?.Invoke(this);
                return new ErrorResult<byte[]>("Connection closed");
            }
            result.Slice(0, readed);
        }
        catch (SocketException)
        {
            CloseConnection?.Invoke(this);
            return new ErrorResult<byte[]>("Connection closed");
        }
        catch (Exception ex)
        {
            CloseConnection?.Invoke(this);
            return new ErrorResult<byte[]>(ex.Message);
        }

        return new SuccessResult<byte[]>(result.Slice(0, readed).ToArray());
    }
    public AbstractResult<Empty> Write(byte[] bytes)
    {
        if (!CheckConnection())
        {
            CloseConnection?.Invoke(this);
            return new ErrorResult<Empty>("Connection closed");
        }

        try
        {
            _client.GetStream().Write(bytes);
            _client.GetStream().Flush();
        }
        catch (SocketException)
        {
            CloseConnection?.Invoke(this);
            return new ErrorResult<Empty>("Connection closed");
        }
        catch (Exception ex)
        {
            CloseConnection?.Invoke(this);
            return new ErrorResult<Empty>(ex.Message);
        }

        return new SuccessResult<Empty>();
    }
    public AbstractResult<int> Available()
    {
        if (!CheckConnection())
        {
            CloseConnection?.Invoke(this);
            return new ErrorResult<int>("Connection closed");
        }
        return new SuccessResult<int>(_client.Available);
    }
    private void ResetCancel()
    {
        _internalCancel?.Cancel();
        _internalCancel?.Dispose();
        _internalCancel = new CancellationTokenSource();
    }
    public void Stop()
    {
        _internalCancel.Cancel();
    }

    public bool isClosed()
    {
        return CheckConnection();
    }
    public AbstractResult<Empty> Close()
    {
        try
        {
            _client.Close();
            CloseConnection?.Invoke(this);
        }
        catch (Exception ex)
        {
            return new ErrorResult<Empty>(ex.Message);
        }

        return new SuccessResult<Empty>("Client closed");
    }
    private bool CheckConnection()
    {
        try
        {
            if (_client.Client == null)
                return false;

            if (_client.Client.Poll(1000, SelectMode.SelectRead) && _client.Client.Available == 0)
                return false;
        }
        catch (SocketException)
        {
            return false;
        }
        return true;
    }
    public void Dispose()
    {
        Stop();
        _internalCancel.Dispose();
        _client.Close();
    }

    public delegate void CloseConnectionDelegate(TcpHandler sender);
    public event CloseConnectionDelegate? CloseConnection;

}
