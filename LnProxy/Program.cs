using System.Net;
using System.Net.Sockets;
using System.Text;
using ProxyModule;
using SocksModule;
using NetworkModule;
using static SocksModule.SocksContext;
using Serilog;

public class Progrma
{
    private static async Task DebugEntryPoint()
    {
        Directory.CreateDirectory("log");
        var fatalLogger = new LoggerConfiguration()
            .WriteTo.File("log/FatalLog.txt", rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger();
        try
        {
            var settings = new ProxySettings("Settings.txt");
            Proxy server = new Proxy(settings);
            settings.Changed += () =>
            {
                server.Dispose();
            };
            await server.StartAsync();
            do
            {
                server = new Proxy(settings);
                await server.StartAsync();
            } while (true);
        }
        catch (Exception ex)
        {
            fatalLogger.Fatal("Server crash - {@Msg}, StackTrace: {@Trace}", ex.Message, ex.StackTrace);
        }
    }

    private static async Task ReleaseEntryPoint(string[] args)
    {
        
    }
    public static async Task Main(string[] args)
    {
#if DEBUG
        await DebugEntryPoint();
#else
        await ReleaseEntryPoint(args);
#endif
    }
}