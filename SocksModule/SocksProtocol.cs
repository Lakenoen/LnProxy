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
            try
            {
                PriorityQueue<byte, short> methods = new(3);

                for (byte i = 0; i < req.Size; ++i)
                {
                    short priority = req.Methods[i] switch
                    {
                        0x0 => 1,
                        0x1 => 2,
                        0x2 => 3,
                        _ => throw new ApplicationException("Method not supported")
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
            catch (ApplicationException)
            {
                Context.Method = 0xff;
                var resp = new TcpGreetingServerResponce { Ver = req.Ver, Method = 0xff };
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

            if (res.Status != 0x0)
            {
                CloseClient?.Invoke(Context, res.ToByteArray());
                return Array.Empty<byte>();
            }

            return res.ToByteArray();
        }
        private byte[] Connection(byte[] data)
        {
            var req = TcpConnectionClientRequest.Parse(data);
            try
            {
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
                    _ => throw new ApplicationException("Command not supported")
                };
            }
            catch (ApplicationException e) when (e.Message.Equals("Command not supported"))
            {
                TcpConnectionServerResponse response = new TcpConnectionServerResponse()
                {
                    Ver = req.Ver,
                    Rep = (byte)RepType.COMMAND_NOT_SUPPORTED,
                    Atyp = Context.ServerUdpEndPoint!.AddressFamily.Equals(AddressFamily.InterNetwork) ? Atyp.IpV4 : Atyp.IpV6,
                    BndAddr = Context.ServerUdpEndPoint.Address.GetAddressBytes(),
                    BndPort = (short)Context.ServerUdpEndPoint.Port,
                };
                return response.ToByteArray();
            }
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
                BndPort = (short)Context.ServerUdpEndPoint.Port,
            };
            Context.ConnectionType = ConnectType.UDP;
            EndInit?.Invoke(Context, resp);
            return Array.Empty<byte>();
        }
        private byte[] HandleBind(TcpConnectionClientRequest req)
        {
            var Atyp_ = Context.BindServerEndPoint!.AddressFamily.Equals(AddressFamily.InterNetwork) ? Atyp.IpV4 : Atyp.IpV6;
            var resp = new TcpConnectionServerResponse()
            {
                Ver = req.Ver,
                Rep = (byte)RepType.SUCCESS,
                Atyp = Atyp_,
                BndAddr = Context.BindServerEndPoint.Address.GetAddressBytes(),
                BndPort = (short)Context.BindServerEndPoint.Port,
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
                BndPort = (short)Context.ServerTcpEndPoint.Port,
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
        public event Action<SocksContext, byte[]>? CloseClient;
        public event Action<SocksContext, TcpConnectionServerResponse>? Bind;
        public event Action<SocksContext, Exception>? OnError;
    }

}