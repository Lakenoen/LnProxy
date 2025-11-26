// See https://aka.ms/new-console-template for more information
using System.Net;
using System.Net.Sockets;
using System.Text;
using ProxyModule;
using TcpModule;

HttpsProxy server = new HttpsProxy();
var task = server.StartAsync();
task.Wait();
server.Dispose();