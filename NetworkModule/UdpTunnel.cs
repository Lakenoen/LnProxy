using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetworkModule;
public class UdpTunnel : AStartableAsync, IDisposable
{
    public int Port { get; init; }
    private readonly UdpClient _udp;
    public UdpTunnel(int port)
    {
        Port = port;
        _udp = new UdpClient(port);
    }
    protected override void Init()
    {
        
    }
    protected async override void Start()
    {
        try
        {
            while (!base._cancel!.Token.IsCancellationRequested)
            {
                var res = await _udp.ReceiveAsync(_cancel!.Token);
                IPEndPoint? remote = res.RemoteEndPoint;

                if (remote is null)
                    continue;

                IPEndPoint? where = OnRecv?.Invoke(remote, res.Buffer);

                if (where is null)
                    continue;

                _udp.Send(res.Buffer, where);

            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    protected override void End()
    {
        
    }

    protected override void Error(Exception ex)
    {
        OnError?.Invoke(this, ex);
    }

    public void Dispose()
    {
        this._udp.Close();
    }

    public event Action<UdpTunnel, Exception>? OnError;
    public event Func<IPEndPoint, byte[], IPEndPoint>? OnRecv;
}
