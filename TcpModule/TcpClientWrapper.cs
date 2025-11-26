using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TcpModule;
public class TcpClientWrapper : IDisposable
{
    public byte[] Buffer { get; } = new byte[0xffff];
    private readonly TcpClient _client;
    private NetworkStream _stream;

    public IPEndPoint? EndPoint
    {
        get => _client.Client.RemoteEndPoint as IPEndPoint;
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
        catch (SocketException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
    public byte[] ReadAvailable()
    {
        var result = new Span<byte>(Buffer);
        int readed = 0;
        try
        {
           if( !CheckConnection() || (readed = _stream.Read(Buffer)) == 0)
           {
                OnDisconnect?.Invoke(this);
           }
        }
        catch (ObjectDisposedException)
        {
            OnDisconnect?.Invoke(this);
        }
        catch (IOException)
        {
            OnDisconnect?.Invoke(this);
        }
        return result.Slice(0, readed).ToArray();
    }

    public void Write(byte[] data)
    {
        try
        {
            if (!CheckConnection())
            {
                OnDisconnect?.Invoke(this);
                return;
            }
            _stream.Write(data, 0, data.Length);
        }
        catch (ObjectDisposedException)
        {

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
        catch (IOException)
        {
            OnDisconnect?.Invoke(this);
        }
        return 0;
    }
    public bool CheckConnection()
    {
        try
        {
            if (_client.Client == null || _client.Client.Poll(1000, SelectMode.SelectRead) && _client.Client.Available == 0)
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

    public void Dispose()
    {
        _client?.Close();
    }

    public delegate void Disconnect(TcpClientWrapper tcpClientWrapper);
    public event Disconnect? OnDisconnect;

}
