using NetworkModule;
using SocksModule;
using System;
using System.Net;
using ProxyModule;
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
            Proxy server = new Proxy();
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
            Proxy server = new Proxy();
            var task = server.StartAsync();

            TcpClientWrapper client = new TcpClientWrapper(IPEndPoint.Parse("192.168.0.103:8888"));
            SocksContext.TcpGreetingClientRequest request = new SocksContext.TcpGreetingClientRequest()
            {
                Ver = 0x5,
                Size = 0x1,
                Methods = new byte[] { 0x0 }
            };
            await client.WriteAsync(request.ToByteArray());

            byte[] respData = await client.ReadAvailableAsync();
            var serverResp = SocksModule.SocksContext.TcpGreetingServerResponce.Parse(respData);

            var clientReq = new SocksContext.TcpConnectionClientRequest()
            {
                Ver = 0x5,
                Smd = SocksContext.ConnectType.BIND,
                Atyp = Atyp.IpV4,
                DstAddr = IPAddress.Any.GetAddressBytes(),
                DstPort = 8889
            };
            await client.WriteAsync(clientReq.ToByteArray());

            var firstServResp = TcpConnectionServerResponse.Parse( await client.ReadAvailableAsync());
            TcpClientWrapper endServer = new TcpClientWrapper(new IPEndPoint(new IPAddress(firstServResp.BndAddr), firstServResp.BndPort));
            var secondServResp = TcpConnectionServerResponse.Parse( await client.ReadAvailableAsync());

            await endServer.WriteAsync(Encoding.UTF8.GetBytes("External server Hello message"));
            string testResp = Encoding.UTF8.GetString(await client.ReadAvailableAsync());
            Assert.Equal("External server Hello message", testResp);

            await client.WriteAsync(Encoding.UTF8.GetBytes("Client Hello message"));
            testResp = Encoding.UTF8.GetString(await endServer.ReadAvailableAsync());
            Assert.Equal("Client Hello message", testResp);

            server.Dispose();
        }
    }
}
