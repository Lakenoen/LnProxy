using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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
            Node iter = (_first.Next = new Node((byte[] data) =>
            {
                var req = TcpGreetingClientRequest.Parse(data);
                var resp = new TcpGreetingServerResponce{Ver = 0x5, Method = 0x0};
                return resp.ToByteArray();
            }));

            iter.Next = new Node((byte[] data) =>
            {
                var req = TcpConnectionClientRequest.Parse(data);
                Context.TargetType = req.Atyp;
                Context.TargetAddress = Encoding.UTF8.GetString(req.DstAddr.ToArray());
                Context.TargetPort = req.DstPort;

                if (Context.ServerEndPoint is null)
                    throw new ArgumentNullException(nameof(Context.ServerEndPoint));

                var Atyp_ = Context.ServerEndPoint.AddressFamily.Equals(AddressFamily.InterNetwork) ? Atyp.IpV4 : Atyp.IpV6;
                var resp = new TcpConnectionServerResponse()
                {
                    Ver = 0x5,
                    Rep = 0x0,
                    Atyp = Atyp_,
                    BndAddr = Context.ServerEndPoint.Address.GetAddressBytes(),
                    BndPort = (short)Context.ServerEndPoint.Port,
                };
                EndInit?.Invoke(Context, resp.ToByteArray());
                return Array.Empty<byte>();
            });
        }
        public byte[] InitAsServer(byte[] data)
        {
            if (MoveNext())
            {
                Node node = (Node)Current;
                return node.Stage!.Invoke(data);
            }
            else
            {
                
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
        public event Action<SocksContext, byte[]>? EndInit;
    }

}