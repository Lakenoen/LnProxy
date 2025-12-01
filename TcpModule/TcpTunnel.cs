using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static TcpModule.TcpServer;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TcpModule;

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
        while (!_cancel!.Token.IsCancellationRequested)
        {
            if (!Source.CheckConnection() || !Target.CheckConnection())
                return;

            Target.ReadAvailableAsync(_cancel!.Token).Wait();
        }
    }
    private async void TargetReaded(byte[] data)
    {
        await Source.WriteAsync(data);
    }
    private async void SourceReaded(byte[] data)
    {
        await Target.WriteAsync(data);
    }
    public void Dispose()
    {
        Target?.Dispose();
    }

    public event Action<TcpTunnel, Exception>? OnError;
}
