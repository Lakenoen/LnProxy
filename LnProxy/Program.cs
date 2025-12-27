using System.Net;
using System.Net.Sockets;
using System.Text;
using ProxyModule;
using SocksModule;
using NetworkModule;
using static SocksModule.SocksContext;

Proxy server = new Proxy(new ProxySettings("settings.txt"));
var task = server.StartAsync();
task.Wait();
server.Dispose();