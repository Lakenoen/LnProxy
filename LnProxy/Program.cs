// See https://aka.ms/new-console-template for more information
using System.Net.Sockets;
using System.Text;
using ProxyLib;

TcpClient client = new TcpClient();

ProxyServer proxy = new ProxyServer(new ProxySettings());
var runTask = proxy.RunAsync();

//client.Connect("127.0.0.1", 80);
//client.GetStream().Write(Encoding.ASCII.GetBytes("testMsg"));
//Thread.Sleep(10000);
//client.Close();
//proxy.Stop();
runTask.Wait();