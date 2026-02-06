using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SocksModule;
using static NetworkModule.TcpServer;

namespace NetworkModule;
public class UdpRedirector : AStartableAsync, IDisposable
{
    private byte[] _buffer = new byte[0xffff];
    private EndPoint _remote = new IPEndPoint(IPAddress.Any, 0);
    public int Port { get; init; }

    private Socket _udp;
    public IPAddress Client { get; init; }

    private object _lock = new object();

    private readonly WaitableDict<IPEndPoint, IPEndPoint> _history = new();

    public UdpRedirector(IPEndPoint client, int port)
    {
        Client = client.Address;

        _udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _udp.ReceiveBufferSize = 0xffff;
        _udp.SendBufferSize = 0xffff;

        _udp.Bind( new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0) );
        Port = ((IPEndPoint)_udp.LocalEndPoint!).Port;
    }

    public async Task Invoke()
    {
        try
        {
            while (!base._cancel!.Token.IsCancellationRequested)
            {
                await Read();
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    private async Task Read()
    {
        try
        {
            var res = await _udp.ReceiveFromAsync(_buffer, _remote, _cancel!.Token);

            HandleDgram(_buffer.AsMemory().Slice(0, res.ReceivedBytes).ToArray(), (IPEndPoint)res.RemoteEndPoint);
        }
        catch (OperationCanceledException) when (!_cancel!.Token.IsCancellationRequested)
        {
            return;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Error(ex);
        }
    }

    private void HandleDgram(byte[] data, IPEndPoint from)
    {
        if (_history.TryGetValue(from, out var client))
        {
            var packet = new SocksContext.UdpPacket();
            packet.Rsv = 0;
            packet.Frag = 0;
            packet.Data = data;
            packet.DstAddr = from.Address.GetAddressBytes();
            packet.DstPort = (ushort)from.Port;
            packet.Atyp_ = from.AddressFamily switch
            {
                AddressFamily.InterNetwork => SocksContext.Atyp.IpV4,
                AddressFamily.InterNetworkV6 => SocksContext.Atyp.IpV6,
            };

            Send(client, packet.ToByteArray());
            return;
        }
        else if (from.Address.Equals(Client))
        {
            var packet = SocksContext.UdpPacket.Parse(data);
            var where = new IPEndPoint(new IPAddress(packet.DstAddr!), packet.DstPort);
            Send(where, packet.Data);
            _history.Add(where, from);
            return;
        }

        return;
    }

    private void Send(IPEndPoint where, byte[]? data)
    {
        if (data is null)
            return;
        lock (_lock)
        {
            _udp.SendTo(data, where);
        }
    }

    public void Dispose()
    {
        this._udp.Close();
    }

    protected override void Init() { }
    protected override void Start() { }
    protected override void End() { }
    protected override void Error(Exception ex) {
        OnError?.Invoke(ex);
    }

    public event Action<Exception>? OnError;
}
