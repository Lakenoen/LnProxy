using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static NetworkModule.TcpServer;

namespace NetworkModule;
public class TcpClientWrapper : IDisposable
{
    private readonly SemaphoreSlim _readSemaphore = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
    public byte[] Buffer { get; } = new byte[0xffff];
    private readonly TcpClient _client;
    private Stream _stream;
    public Stream Stream { get => _stream; set => _stream = value; }
    public IPEndPoint? EndPoint { get; init; }
    public TcpClientWrapper(TcpClient client)
    {
        EndPoint = client.Client.RemoteEndPoint as IPEndPoint;
        _client = client;
        InitClient();

        _stream = client.GetStream();
    }

    public TcpClientWrapper(IPEndPoint ip)
    {
        _client = new TcpClient(ip.AddressFamily);
        InitClient();

        _client.Connect(ip);
        EndPoint = ip;
        _stream = _client.GetStream();
    }

    private void InitClient()
    {
        _client.NoDelay = true;
        _client.ReceiveBufferSize = 256 * 1024;
        _client.SendBufferSize = 256 * 1024;
    }
    public bool Reconnect()
    {
        try
        {
            if (this.CheckConnection())
                return false;
            if (this.EndPoint == null)
                return false;
            _client.Connect(this.EndPoint);
            _stream = _client.GetStream();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
    public async Task<byte[]> ReadAvailableAsync(CancellationToken? cancel = null)
    {
        int readed = 0;
        Func<Task<int>>? read = null;

        if (cancel is not null)
        {
            await _readSemaphore.WaitAsync(cancel.Value);
            read = async () => await _stream.ReadAsync(Buffer, cancel.Value);
        }
        else
        {
            await _readSemaphore.WaitAsync();
            read = async () => await _stream.ReadAsync(Buffer);
        }

        try
        {
            if (!CheckConnection())
            {
                OnDisconnect?.Invoke(this);
                return Array.Empty<byte>();
            }

            readed = await read.Invoke();

            if (readed == 0)
            {
                OnDisconnect?.Invoke(this);
                return Array.Empty<byte>();
            }

            return GetReaded(readed);

        }
        catch (OperationCanceledException)
        {
            if(readed != 0)
                return GetReaded(readed);
        }
        catch (ObjectDisposedException)
        {
            OnDisconnect?.Invoke(this);
            return Array.Empty<byte>();
        }
        catch (IOException)
        {
            OnDisconnect?.Invoke(this);
            return Array.Empty<byte>();
        }
        finally
        {
            _readSemaphore.Release();
        }

        return Array.Empty<byte>();
    }
    private byte[] GetReaded(int readed)
    {
        var data = Buffer.AsMemory(0, readed).ToArray();
        OnReaded?.Invoke(data);
        return data;
    }
    public async Task WriteAsync(byte[] data, CancellationToken? cancel = null)
    {
        Func<Task>? write = null;
        if (cancel is not null)
        {
            await _writeSemaphore.WaitAsync(cancel.Value);
            write = async () => await _stream.WriteAsync(data, 0, data.Length, (CancellationToken)cancel);
        }
        else
        {
            await _writeSemaphore.WaitAsync();
            write = async () => await _stream.WriteAsync(data, 0, data.Length);
        }

        try
        {
            if (!CheckConnection())
            {
                OnDisconnect?.Invoke(this);
                return;
            }

            await write.Invoke();

            _stream.Flush();
        }
        catch (OperationCanceledException)
        {

        }
        catch (ObjectDisposedException)
        {
            OnDisconnect?.Invoke(this);
        }
        catch (IOException)
        {
            OnDisconnect?.Invoke(this);
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public int Available()
    {
        try
        {
            return _client.Available;
        }
        catch (ObjectDisposedException)
        {
            OnDisconnect?.Invoke(this);
        }
        catch (SocketException)
        {
            OnDisconnect?.Invoke(this);
        }
        return 0;
    }
    public bool CheckConnection()
    {
        try
        {
            if (_client.Client == null)
                return false;

            if (_client.Client.Poll(0, SelectMode.SelectRead) && _client.Client.Available == 0)
                return false;
        }
        catch (NullReferenceException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }

        return true;
    }
    public void Disconnect()
    {
        OnDisconnect?.Invoke(this);
    }
    public void Dispose()
    {
        _client?.Close();
    }

    public event Action<TcpClientWrapper>? OnDisconnect;
    public event Action<byte[]>? OnReaded;

}
