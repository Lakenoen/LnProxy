using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static TcpModule.TcpServer;

namespace TcpModule;

public class TcpTunel(TcpClientWrapper source, TcpClientWrapper target) : AStartableAsync
{
    public TcpClientWrapper Source { get; private set; } = source;
    public TcpClientWrapper Target { get; private set; } = target;
    protected override void End()
    {
        
    }

    protected override void Error(Exception ex)
    {
        OnError?.Invoke(ex);
    }

    protected override void Start()
    {
        while (!_cancel!.Token.IsCancellationRequested)
        {
            if (!Source.CheckConnection() || !Target.CheckConnection())
                return;
            var data = Target.ReadAvailable();
            if (data.Length != 0)
                Source.Write(data);
        }
    }

    public event Action<Exception>? OnError;
}
