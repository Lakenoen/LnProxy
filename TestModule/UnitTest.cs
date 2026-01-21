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
            ProxySettings settings = new ProxySettings("Settings.txt");
            UdpClient serverUdp = new UdpClient(8000);
            Proxy server = new Proxy(settings);
            var task = server.StartAsync();
            TcpClientWrapper client = new TcpClientWrapper(settings.ExternalTcpEndPoint);


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

            var settings = new ProxySettings("Settings.txt");
            Proxy server = new Proxy(settings);
            var task = server.StartAsync();

            TcpClientWrapper connect = new TcpClientWrapper(settings.ExternalTcpEndPoint);
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

            TcpClientWrapper bind = new TcpClientWrapper(settings.ExternalTcpEndPoint);
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
            BTreeIndex index = new BTreeIndex(2);
            index.Insert((Integer)4, (String32)"4");
            index.Insert((Integer)3, (String32)"3");
            index.Insert((Integer)2, (String32)"2");
            index.Insert((Integer)6, (String32)"6");
            index.Insert((Integer)5, (String32)"5");
            index.Insert((Integer)7, (String32)"7");
            index.Insert((Integer)1, (String32)"1");
            index.Insert((Integer)0, (String32)"0");
            index.Insert((Integer)11, (String32)"11");
            index.Insert((Integer)12, (String32)"12");
            index.Insert((Integer)13, (String32)"13");
            index.Insert((Integer)10, (String32)"10");

            String32? el = (String32?)index.Search((Integer)7);

            index.Remove((Integer)4);

            BNode root = index.CreateNode();
            root.Add(new Element((Integer)3, (String32)"3"));
            root.Add(new Element((Integer)4, (String32)"4"));
            root[0].Links[0] = index.CreateNode();
            root[0].Links[1] = index.CreateNode();
            root[1].Links[0] = index.CreateNode();
        }
    }
}
