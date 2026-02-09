//#define ENABLEAPP

using System.Net;
using System.Net.Sockets;
using System.Text;
using ProxyModule;
using SocksModule;
using NetworkModule;
using static SocksModule.SocksContext;
using Serilog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    private static async Task AppEntryPoint()
    {
        Directory.CreateDirectory("log");
        var fatalLogger = new LoggerConfiguration()
            .WriteTo.File("log/AppLog.txt", rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger();
        try
        {
            var token = new CancellationTokenSource();
            var settings = new ProxySettings("Settings.txt");
            Proxy server = new Proxy(settings);

            AppDomain.CurrentDomain.ProcessExit += (object? sender, EventArgs e) =>
            {
                token.Cancel();
                server?.Dispose();
                settings?.Dispose();
            };

            settings.Changed += () =>
            {
                settings.Dispose();
                settings = new ProxySettings("Settings.txt");
                fatalLogger.Information("Settings changed");
                server.Dispose();
            };
            await server.StartAsync();
            do
            {
                server = new Proxy(settings);
                await server.StartAsync();
            } while (!token.IsCancellationRequested);
        }
        catch (Exception ex)
        {
            fatalLogger.Fatal("Server crash - {@Msg}, StackTrace: {@Trace}", ex.Message, ex.StackTrace);
        }
    }

    private static async Task DaemonEntryPoint(string[] args)
    {
        IHost? host = Host.CreateDefaultBuilder(args)
            .UseSystemd()
            .ConfigureServices((HostBuilderContext context, IServiceCollection services) =>
            {
                services.AddHostedService<Worker>();
            }).Build();

        await host.RunAsync();
    }
    public static async Task Main(string[] args)
    {
#if (DEBUG || ENABLEAPP)
        await AppEntryPoint();
#else
        await DaemonEntryPoint(args);
#endif
    }
}

class Worker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory("log");
        var fatalLogger = new LoggerConfiguration()
            .WriteTo.File("log/AppLog.txt", rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger();
        try
        {
            var settings = new ProxySettings("Settings.txt");
            Proxy server = new Proxy(settings);
            CancelChecker(stoppingToken, async () =>
            {
                await server.StopAsync();
            });
            settings.Changed += () =>
            {
                settings.Dispose();
                settings = new ProxySettings("Settings.txt");
                fatalLogger.Information("Settings changed");
                server.Dispose();
            };
            await server.StartAsync();

            do
            {
                server = new Proxy(settings);
                await server.StartAsync();
            } while (!stoppingToken.IsCancellationRequested);

            settings.Dispose();
        }
        catch (OperationCanceledException)
        {
            fatalLogger.Fatal("Service stopped");
        }
        catch (Exception ex)
        {
            fatalLogger.Fatal("Server crash - {@Msg}, StackTrace: {@Trace}", ex.Message, ex.StackTrace);
        }
    }

    private async void CancelChecker(CancellationToken stoppingToken, Action stop)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(10);
            }
        }
        catch (OperationCanceledException)
        {

        }
        stop.Invoke();
    }
}