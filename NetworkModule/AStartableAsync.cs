using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static NetworkModule.TcpServer;

namespace NetworkModule;
public abstract class AStartableAsync
{
    protected CancellationTokenSource? _cancel;
    protected (bool item, object locker) _started = new(false, new());

    public AStartableAsync()
    {
        _cancel = new CancellationTokenSource();
    }
    public virtual async Task StartAsync()
    {
        await Task.Run(() =>
        {
            lock (_started.locker)
            {
                if (_started.item == true) return;
                _started.item = true;
            }
            try
            {
                Init();
                Start();
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                Error(ex);
            }
            finally
            {
                ResetCancel();
                lock (_started.locker)
                {
                    _started.item = false;
                }
                End();
            }
        });
    }

    protected abstract void Init();
    protected abstract void Start();
    protected abstract void End();
    protected abstract void Error(Exception ex);
    public async Task StopAsync()
    {
        Stop();
        await Task.Run(() =>
        {
            while (true)
            {
                lock (_started.locker)
                {
                    if (!_started.item)
                        break;
                }
                Task.Delay(1);
            }
        });
    }
    public void Stop()
    {
        if (_cancel != null && _cancel?.Token.IsCancellationRequested == false)
            _cancel?.Cancel();
    }
    protected void ResetCancel()
    {
        if (_cancel != null)
        {
            _cancel?.Cancel();
            _cancel?.Dispose();
        }
        _cancel = new CancellationTokenSource();
    }

}
