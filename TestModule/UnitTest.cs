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
            UdpClient serverUdp = new UdpClient(8000);


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

            var clientReq = new SocksContext.TcpConnectionClientRequest(){
                Ver = 0x5,
                Smd = SocksContext.ConnectType.UDP,
                Atyp = Atyp.IpV6,
                DstAddr = IPAddress.Parse("192.168.0.103").GetAddressBytes(),
                DstPort = 6000
            };

            await client.WriteAsync(clientReq.ToByteArray());
            respData = await client.ReadAvailableAsync();
            var serverConnectionResp = SocksContext.TcpConnectionServerResponse.Parse(respData);

            IPEndPoint local = new IPEndPoint(IPAddress.Parse("192.168.0.103"), 6000);
            UdpClient udp = new UdpClient();
            udp.Client.Bind(local);

            IPEndPoint where = new IPEndPoint( new IPAddress(serverConnectionResp.BndAddr), serverConnectionResp.BndPort);

            var packetData = new SocksContext.UdpPacket()
            {
                DstAddr = IPAddress.Parse("192.168.0.103").GetAddressBytes(),
                DstPort = 8000,
                Data = Encoding.UTF8.GetBytes("Test Udp Message")
            };
            udp.Send(packetData.ToByteArray(), where);


            IPEndPoint? remote = null;
            var udpPacket = SocksContext.UdpPacket.Parse(serverUdp.Receive(ref remote));

            var result = Encoding.UTF8.GetString(udpPacket.Data);
            Assert.Equal("Test Udp Message", result);

            server.Dispose();
        }
    }
}
