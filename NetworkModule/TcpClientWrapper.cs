using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetworkModule;
public class TcpClientWrapper : IDisposable
{
    public byte[] Buffer { get; } = new byte[0xffff];
    private readonly TcpClient _client;
    private Stream _stream;
    public Stream Stream { get => _stream; set => _stream = value; }
    public IPEndPoint? EndPoint
    {
        get => _client?.Client?.RemoteEndPoint as IPEndPoint;
    }
    public TcpClientWrapper(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
    }

    public TcpClientWrapper(IPEndPoint ip)
    {
        _client = new TcpClient();
        _client.Connect(ip);
        _stream = _client.GetStream();
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
        try
        {
            if (!CheckConnection())
            {
                OnDisconnect?.Invoke(this);
                return Array.Empty<byte>();
            }

            readed = (cancel == null) ? await _stream.ReadAsync(Buffer)
                : await _stream.ReadAsync(Buffer, cancel.Value);
            if (readed == 0)
            {
                OnDisconnect?.Invoke(this);
                return Array.Empty<byte>();
            }
        }
        catch (OperationCanceledException)
        {
            
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
        var data = Buffer.AsMemory(0, readed).ToArray();
        OnReaded?.Invoke(data);
        return data;
    }

    public async Task WriteAsync(byte[] data, CancellationToken? cancel = null)
    {
        try
        {
            if (!CheckConnection())
            {
                OnDisconnect?.Invoke(this);
                return;
            }
            if(cancel == null)
                await _stream.WriteAsync(data, 0, data.Length);
            else
                await _stream.WriteAsync(data, 0, data.Length, (CancellationToken)cancel);
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
