using NetworkModule;
using SocksModule;
using System;
using System.Net;
using ProxyModule;
using IndexModule;
using static SocksModule.SocksContext;
using System.Net.Sockets;
using System.Text;

namespace TestModule
{
    public class UnitTest
    {
        [Fact]
        public async Task SocksUdpTest()
        {
            //init
            UdpClient serverUdp = new UdpClient(8000);
            Proxy server = new Proxy(new ProxySettings("Settings.txt"));
            var task = server.StartAsync();
            TcpClientWrapper client = new TcpClientWrapper(IPEndPoint.Parse("192.168.0.103:8888"));


            //socks handshake
            SocksContext.TcpGreetingClientRequest request = new SocksContext.TcpGreetingClientRequest()
            {
                Ver = 0x5,
                Size = 0x1,
                Methods = new byte[] { 0x0 }
            };
            await client.WriteAsync(request.ToByteArray());

            byte[] respData = await client.ReadAvailableAsync();
            var serverResp = SocksModule.SocksContext.TcpGreetingServerResponce.Parse(respData);

            //send external addr to proxy server
            var clientReq = new SocksContext.TcpConnectionClientRequest(){
                Ver = 0x5,
                Smd = SocksContext.ConnectType.UDP,
                Atyp = Atyp.IpV4,
                DstAddr = IPAddress.Parse("192.168.0.103").GetAddressBytes(),
                DstPort = 6000
            };

            await client.WriteAsync(clientReq.ToByteArray());
            respData = await client.ReadAvailableAsync();

            //create udp client and save server udp addr
            var serverConnectionResp = SocksContext.TcpConnectionServerResponse.Parse(respData);

            IPEndPoint local = new IPEndPoint(IPAddress.Parse("192.168.0.103"), 6000);
            UdpClient udp = new UdpClient();
            udp.Client.Bind(local);

            IPEndPoint where = new IPEndPoint( new IPAddress(serverConnectionResp.BndAddr), serverConnectionResp.BndPort);


            //make request message
            var packetData = new SocksContext.UdpPacket()
            {
                DstAddr = IPAddress.Parse("192.168.0.103").GetAddressBytes(),
                DstPort = 8000,
                Data = Encoding.UTF8.GetBytes("Test Udp Message")
            };
            udp.Send(packetData.ToByteArray(), where);

            //take request (server)
            IPEndPoint? remote = null;
            var udpPacket = SocksContext.UdpPacket.Parse(serverUdp.Receive(ref remote));

            var result = Encoding.UTF8.GetString(udpPacket.Data);
            Assert.Equal("Test Udp Message", result);

            serverUdp.Send(Encoding.UTF8.GetBytes("Test Udp responce"), remote);

            //take responce (client)
            result = Encoding.UTF8.GetString(udp.Receive(ref remote));
            Assert.Equal("Test Udp responce", result);

            server.Dispose();
        }

        [Fact]
        public async Task SocksBindTest()
        {
            TcpServer endServer = new TcpServer(IPEndPoint.Parse("0.0.0.0:10000"));
            endServer.StartAsync();
            TcpClientWrapper? c = null;
            endServer.OnReaded += (TcpClientWrapper client, byte[] data) =>
            {
                string addr = Encoding.UTF8.GetString(data);
                var endPoint = IPEndPoint.Parse(addr);
                c = new TcpClientWrapper(endPoint);
                Task.Delay(1).Wait();
            };

            Proxy server = new Proxy(new ProxySettings("Settings.txt"));
            var task = server.StartAsync();

            TcpClientWrapper connect = new TcpClientWrapper(IPEndPoint.Parse("192.168.0.103:8888"));
            TcpGreetingClientRequest request = new TcpGreetingClientRequest()
            {
                Ver = 0x5,
                Size = 0x1,
                Methods = new byte[] { 0x0 }
            };
            await connect.WriteAsync(request.ToByteArray());
            var serverResp = TcpGreetingServerResponce.Parse(await connect.ReadAvailableAsync());

            var clientConnectReq = new TcpConnectionClientRequest()
            {
                Ver = 0x5,
                Smd = SocksContext.ConnectType.CONNECT,
                Atyp = Atyp.IpV4,
                DstAddr = IPAddress.Parse("192.168.0.103").GetAddressBytes(),
                DstPort = 10000
            };
            await connect.WriteAsync(clientConnectReq.ToByteArray());
            var connectServResp = TcpConnectionServerResponse.Parse(await connect.ReadAvailableAsync());

            TcpClientWrapper bind = new TcpClientWrapper(IPEndPoint.Parse("192.168.0.103:8888"));
            request = new TcpGreetingClientRequest()
            {
                Ver = 0x5,
                Size = 0x1,
                Methods = new byte[] { 0x0 }
            };
            await bind.WriteAsync(request.ToByteArray());
            serverResp = TcpGreetingServerResponce.Parse(await bind.ReadAvailableAsync());

            var clientBindReq = new SocksContext.TcpConnectionClientRequest()
            {
                Ver = 0x5,
                Smd = SocksContext.ConnectType.BIND,
                Atyp = Atyp.IpV4,
                DstAddr = IPAddress.Any.GetAddressBytes(),
                DstPort = 8889
            };
            await bind.WriteAsync(clientBindReq.ToByteArray());
            var bindFirstResp = TcpConnectionServerResponse.Parse( await bind.ReadAvailableAsync());

            connect.WriteAsync(Encoding.UTF8.GetBytes($"{new IPAddress(bindFirstResp.BndAddr).ToString()}:{bindFirstResp.BndPort.ToString()}"));

            var secondBindResp = TcpConnectionServerResponse.Parse( await bind.ReadAvailableAsync());
            Assert.Equal(secondBindResp.Rep, 0x0);

            await c.WriteAsync(Encoding.UTF8.GetBytes("External server Hello message"));
            string testResp = Encoding.UTF8.GetString(await bind.ReadAvailableAsync());
            Assert.Equal("External server Hello message", testResp);

            await bind.WriteAsync(Encoding.UTF8.GetBytes("Client Hello message"));
            testResp = Encoding.UTF8.GetString(await c.ReadAvailableAsync());
            Assert.Equal("Client Hello message", testResp);

            endServer.Dispose();
            server.Dispose();
        }

        [Fact]
        public void BTreeTest()
        {
            BNodeManager manager = new BNodeManager(3);
            var firstNode = manager.CreateNode();
            firstNode.Add(new Element( (Data)9, (Data)0) );
            firstNode.Add(new Element( (Data)8, (Data)0) );
            firstNode.Insert(new Element((Data)10, (Data)0), 1);
            firstNode.Remove(0);
        }
    }
}
