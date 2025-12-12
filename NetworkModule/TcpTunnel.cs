using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static NetworkModule.TcpServer;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NetworkModule;

public class TcpTunnel : AStartableAsync, IDisposable
{
    public TcpClientWrapper Source { get; private set; }
    public TcpClientWrapper Target { get; private set; }
    public TcpTunnel(TcpClientWrapper source, TcpClientWrapper target) : base()
    {
        this.Source = source;
        this.Target = target;
        Source.OnReaded += SourceReaded;
        Target.OnReaded += TargetReaded;
    }
    protected override void Init()
    {

    }
    protected override void End()
    {

    }

    protected override void Error(Exception ex)
    {
        OnError?.Invoke(this, ex);
    }
    protected override void Start()
    {
        try
        {
            while (!_cancel!.Token.IsCancellationRequested)
            {
                if (!Source.CheckConnection() || !Target.CheckConnection())
                    return;

                Target.ReadAvailableAsync(_cancel!.Token).Wait();
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }
    protected async virtual void TargetReaded(byte[] data)
    {
        await Source.WriteAsync(data);
    }
    protected async virtual void SourceReaded(byte[] data)
    {
        await Target.WriteAsync(data);
    }
    public void Dispose()
    {
        Target?.Dispose();
    }

    public event Action<TcpTunnel, Exception>? OnError;
}
