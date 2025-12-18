using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static SocksModule.SocksContext;

namespace SocksModule;
public partial class SocksContext
{
    public class SocksProtocol : IEnumerator
    {
        public SocksContext Context { get; init; }
        private Node _first = new Node("First");
        private Node _current;
        public object Current => _current;
        public SocksProtocol(SocksContext context)
        {
            Context = context;
            _current = _first;
            Init();
        }
        protected virtual void Init()
        {
            Node iter = _first.Next = new Node(Greeting, "Greeting");
            iter.Next = new Node(Connection, "Connection");
        }
        private byte[] Greeting(byte[] data)
        {
            var req = TcpGreetingClientRequest.Parse(data);
            Context.Ver = req.Ver;
            try
            {
                PriorityQueue<byte, short> methods = new(3);

                for (byte i = 0; i < req.Size; ++i)
                {
                    short priority = req.Methods[i] switch
                    {
                        0x0 => 1,
                        0x2 => 2,
                        _ => throw new SocksMethodNotSupported()
                    };
                    methods.Enqueue(req.Methods[i], priority);
                }

                switch (methods.Peek()) {
                    case 0x2: InsertNode(_current, new Node(PasswordAuth, "PasswordAuth"));break;
                }

                Context.Method = methods.Peek();
                var resp = new TcpGreetingServerResponce { Ver = req.Ver, Method = methods.Peek() };
                return resp.ToByteArray();
            }
            catch (SocksMethodNotSupported e)
            {
                Context.Method = 0xff;
                var resp = new TcpGreetingServerResponce { Ver = req.Ver, Method = Context.Method };
                Context.Error = e;
                return resp.ToByteArray();
            }
        }

        private void InsertNode(Node current, Node node)
        {
            var temp = current.Next;
            current.Next = node;
            current.Next.Next = temp;
        }
        private byte[] PasswordAuth(byte[] data)
        {
            PasswordAuthClientRequest req = PasswordAuthClientRequest.Parse(data);
            string username = Encoding.UTF8.GetString(req.Username);
            string password = Encoding.UTF8.GetString(req.Password);

            PasswordAuthServerResponce res = new() { Ver = 0x1, Status = 0x0 };

            if (!Context.CheckAuth!(req))
            {
                Context.Error = new SocksAuthReject();
                res.Status = 0x1;
            }

            return res.ToByteArray();
        }
        private byte[] Connection(byte[] data)
        {
            var req = TcpConnectionClientRequest.Parse(data);
            TcpConnectionServerResponse errorResp = new TcpConnectionServerResponse()
            {
                Ver = req.Ver,
                Atyp = req.Atyp,
                BndAddr = req.DstAddr,
                BndPort = req.DstPort,
            };
            try
            {
                if (!Context.CheckAddrType!((byte)req.Atyp))
                    throw new SocksAddrTypeNotSupported();
                if (!Context.CheckCommandType!((byte)req.Smd))
                    throw new SocksCommandNotSupported();
                if (!Context.CheckRule!(req))
                    throw new SocksConnectionRejectByRule();

                Context.TargetType = req.Atyp;
                Context.TargetAddress = req.Atyp switch
                {
                    SocksContext.Atyp.Domain => Encoding.UTF8.GetString(req.DstAddr.ToArray()),
                    _ => new IPAddress(req.DstAddr).ToString()
                };

                Context.TargetPort = req.DstPort;

                return req.Smd switch
                {
                    ConnectType.CONNECT => HandleConnect(req),
                    ConnectType.BIND => HandleBind(req),
                    ConnectType.UDP => HandleUdp(req),
                    _ => throw new SocksCommandNotSupported()
                };
            }
            catch (SocksCommandNotSupported e)
            {
                errorResp.Rep = (byte)RepType.COMMAND_NOT_SUPPORTED;
                Context.Error = e;
            }
            catch (SocksAddrTypeNotSupported e)
            {
                errorResp.Rep = (byte)RepType.ADDRESS_TYPE_NOT_SUPPORTED;
                Context.Error = e;
            }
            catch (SocksConnectionRejectByRule e)
            {
                errorResp.Rep = (byte)RepType.NOT_ALLOW;
                Context.Error = e;
            }
            return errorResp.ToByteArray();
        }

        private byte[] HandleUdp(TcpConnectionClientRequest req)
        {
            var Atyp_ = Context.ServerUdpEndPoint!.AddressFamily.Equals(AddressFamily.InterNetwork) ? Atyp.IpV4 : Atyp.IpV6;
            var resp = new TcpConnectionServerResponse()
            {
                Ver = req.Ver,
                Rep = (byte)RepType.SUCCESS,
                Atyp = Atyp_,
                BndAddr = Context.ServerUdpEndPoint.Address.GetAddressBytes(),
                BndPort = (ushort)Context.ServerUdpEndPoint.Port,
            };
            Context.ConnectionType = ConnectType.UDP;
            EndInit?.Invoke(Context, resp);
            return Array.Empty<byte>();
        }

        private IPEndPoint MakeServerEndPoint(IPAddress addr, ushort port)
        {
            var ip = addr;
            if (ip.Equals(IPAddress.Any))
                ip = Context.BindServerEndPoint!.Address;
            var resultPort = port;
            if (port == 0)
                resultPort = (ushort)Context.BindServerEndPoint!.Port;
            return new IPEndPoint(ip, resultPort);
        }
        private byte[] HandleBind(TcpConnectionClientRequest req)
        {
            Context.BindServerEndPoint = MakeServerEndPoint(new IPAddress(req.DstAddr!), req.DstPort);
            var Atyp_ = Context.BindServerEndPoint!.AddressFamily.Equals(AddressFamily.InterNetwork) ? Atyp.IpV4 : Atyp.IpV6;
            var resp = new TcpConnectionServerResponse()
            {
                Ver = req.Ver,
                Rep = (byte)RepType.SUCCESS,
                Atyp = Atyp_,
                BndAddr = Context.BindServerEndPoint.Address.GetAddressBytes(),
                BndPort = (ushort)Context.BindServerEndPoint.Port,
            };
            Context.ConnectionType = ConnectType.BIND;
            Bind?.Invoke(Context, resp);
            return Array.Empty<byte>();
        }
        private byte[] HandleConnect(TcpConnectionClientRequest req)
        {
            var Atyp_ = Context.ServerTcpEndPoint!.AddressFamily.Equals(AddressFamily.InterNetwork) ? Atyp.IpV4 : Atyp.IpV6;
            var resp = new TcpConnectionServerResponse()
            {
                Ver = req.Ver,
                Rep = (byte)RepType.SUCCESS,
                Atyp = Atyp_,
                BndAddr = Context.ServerTcpEndPoint.Address.GetAddressBytes(),
                BndPort = (ushort)Context.ServerTcpEndPoint.Port,
            };
            Context.ConnectionType = ConnectType.CONNECT;
            EndInit?.Invoke(Context, resp);
            return Array.Empty<byte>();
        }
        public byte[] InitAsServer(byte[] data)
        {
            try
            {
                if (MoveNext())
                {
                    Node node = (Node)Current;
                    return node.Stage!.Invoke(data);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this.Context, ex);
            }
            return Array.Empty<byte>();
        }
        public bool MoveNext()
        {
            if (_current.Next == null)
                return false;
            _current = _current.Next;
            return true;
        }

        public void Reset()
        {
            _current = _first;
        }

        public class Node()
        {
            public string Name { get; init; } = string.Empty;
            public Node(Stage stage) : this()
            {
                this.Stage = stage;
            }
            public Node(string name) : this()
            {
                this.Name = name;
            }
            public Node(Stage stage, string name) : this(stage)
            {
                this.Name = name;
            }
            public Stage? Stage { get; set; }
            public Node? Next { get; set; }
        }

        public delegate byte[] Stage(byte[] data);
        public event Action<SocksContext, TcpConnectionServerResponse>? EndInit;
        public event Action<SocksContext, TcpConnectionServerResponse>? Bind;
        public event Action<SocksContext, Exception>? OnError;
    }

}