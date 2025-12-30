using System.Net;
using System.Net.Sockets;
using System.Text;
using ProxyModule;
using SocksModule;
using NetworkModule;
using static SocksModule.SocksContext;

public class Progrma
{
    private static async Task DebugEntryPoint()
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

    private static void ReleaseEntryPoint(string[] args)
    {

    }
    public static void Main(string[] args)
    {
#if DEBUG
        DebugEntryPoint().Wait();
#else
        ReleaseEntryPoint(args);
#endif
    }
}