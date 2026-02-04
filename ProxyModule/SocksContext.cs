using System.Drawing;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Text.RegularExpressions;
using NetworkModule;

namespace SocksModule;

public partial class SocksContext
{
    public bool AuthEnabled { get; set; } = false;
    public Exception? Error { get; set; } = null;
    public Predicate<PasswordAuthClientRequest>? CheckAuth { get; set; } = null;
    public Predicate<byte>? CheckAddrType { get; set; } = null;
    public Predicate<byte>? CheckCommandType { get; set; } = null;
    public Predicate<TcpConnectionClientRequest>? CheckRule {get; set;} = null;
    public byte Ver { get; private set; } = 0x5;
    public ConnectType ConnectionType { get; private set; } = ConnectType.CONNECT;
    public IPEndPoint? ServerTcpEndPoint {  get; set; }
    public IPAddress? ServerUdpAddress { get; set; }
    public IPEndPoint? BindServerEndPoint {  get; set; }
    public TcpServer? BindServer { get; set; }
    public string TargetAddress { get; private set; } = string.Empty;
    public int TargetPort { get; private set; }
    public Atyp TargetType {  get; private set; }
    public byte Method { get; private set; }

    public static int IpV6Size = 16;
    public static int IpV4Size = 4;
    public static short PortSize = 2;
    public class TcpGreetingClientRequest
    {
        public byte Ver { get; set; } = 0;
        public byte Size { get; set; } = 0;
        public byte[]? Methods { get; set; }
        public static TcpGreetingClientRequest Parse(byte[] data)
        {
            TcpGreetingClientRequest res = new TcpGreetingClientRequest();
            res.Ver = data[0];
            res.Size = data[1];
            res.Methods = new byte[res.Size];
            byte shift = sizeof(byte) * 2;
            for (byte i = 0; i < res.Size; i++)
            {
                res.Methods[i] = data[i + shift];
            }
            return res;
        }
        public byte[] ToByteArray()
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Ver);
            writer.Write(Size);
            writer.Write(Methods.AsSpan(0,Size));
            writer.Flush();
            return stream.ToArray();
        }
    };
    public class TcpGreetingServerResponce
    {
        public byte Ver { get; set; } = 0;
        public byte Method { get; set; } = 0;
        public static TcpGreetingServerResponce Parse(byte[] data)
        {
            TcpGreetingServerResponce res = new TcpGreetingServerResponce();
            res.Ver = data[0];
            res.Method = data[1];
            return res;
        }
        public byte[] ToByteArray()
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Ver);
            writer.Write(Method);
            writer.Flush();
            return stream.ToArray();
        }
    }
    public class TcpConnectionClientRequest
    {
        public byte Ver { get; set; } = 0;
        public ConnectType Smd { get; set; } = ConnectType.CONNECT;
        public byte Rsv { get; set; } = 0;
        public Atyp Atyp { get; set; } = 0;
        public byte[]? DstAddr;
        public ushort DstPort = 0;
        public static TcpConnectionClientRequest Parse(byte[] data)
        {
            TcpConnectionClientRequest res = new TcpConnectionClientRequest();
            res.Ver = data[0];
            res.Smd = (ConnectType)data[1];
            res.Rsv = data[2];
            res.Atyp = (Atyp)data[3];
            byte shift = (res.Atyp == Atyp.Domain) ? (byte)5 : (byte)4;

            if(res.Atyp == Atyp.Domain)
                res.DstAddr = new byte[data[4]];
            else
                res.DstAddr = new byte[data.Length - shift - sizeof(short)];

            for (byte i = 0; i < res.DstAddr!.Length; i++)
            {
                res.DstAddr[i] = data[i + shift];
            }

            var portSpan = data.AsSpan(res.DstAddr.Length + shift, sizeof(short));
            portSpan.Reverse();
            res.DstPort = BitConverter.ToUInt16(portSpan);
            return res;
        }
        public byte[] ToByteArray()
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);

            var port = BitConverter.GetBytes(DstPort).AsSpan();
            port.Reverse();

            writer.Write(Ver);
            writer.Write((byte)Smd);
            writer.Write(Rsv);
            writer.Write((byte)Atyp);

            if(this.Atyp == Atyp.Domain)
                writer.Write((byte)DstAddr!.Length);

            writer.Write(DstAddr!);
            writer.Write(port);
            writer.Flush();
            return stream.ToArray();
        }

    }

    public class TcpConnectionServerResponse
    {
        public byte Ver { get; set; } = 0;
        public byte Rep { get; set; } = 0;
        public byte Rsv { get; set; } = 0;
        public Atyp Atyp { get; set; } = 0;
        public byte[]? BndAddr { get; set; }
        public ushort BndPort { get; set; } = 0;

        public static TcpConnectionServerResponse Parse(byte[] data)
        {
            TcpConnectionServerResponse res = new TcpConnectionServerResponse();
            res.Ver = data[0];
            res.Rep = data[1];
            res.Rsv = data[2];
            res.Atyp = (Atyp)data[3];

            byte shift = (res.Atyp == Atyp.Domain) ? (byte)5 : (byte)4;

            if (res.Atyp == Atyp.Domain)
                res.BndAddr = new byte[data[4]];
            else
                res.BndAddr = new byte[data.Length - shift - sizeof(short)];

            for (byte i = 0; i < res.BndAddr.Length; i++)
            {
                res.BndAddr[i] = data[i + shift];
            }

            var portSpan = data.AsSpan(res.BndAddr.Length + shift, sizeof(short));
            portSpan.Reverse();
            res.BndPort = BitConverter.ToUInt16(portSpan);
            return res;
        }
        public byte[] ToByteArray()
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);

            var port = BitConverter.GetBytes(BndPort).AsSpan();
            port.Reverse();

            writer.Write(Ver);
            writer.Write(Rep);
            writer.Write(Rsv);
            writer.Write((byte)Atyp);

            if (this.Atyp == Atyp.Domain)
                writer.Write((byte)BndAddr!.Length);

            writer.Write(BndAddr!);
            writer.Write(port);
            writer.Flush();
            return stream.ToArray();
        }
    }

    public class PasswordAuthClientRequest
    {
        public byte Ver { get; set; } = 0;
        public byte Ulen { get; set; } = 0;
        public byte[]? Username { get;set; }
        public byte Plen { get; set; } = 0;
        public byte[]? Password { get; set; }
        public static PasswordAuthClientRequest Parse(byte[] data)
        {
            PasswordAuthClientRequest res = new();
            res.Ver = data[0];
            res.Ulen = data[1];

            res.Username = new byte[res.Ulen];
            int shift = sizeof(byte) * 2;
            for( byte i = 0; i < res.Ulen; i++ ) 
                res.Username[i] = data[i + shift];

            res.Plen = data[res.Username.Length + shift];

            res.Password = new byte[res.Plen]; 
            shift += res.Username.Length + sizeof(byte);
            for (byte i = 0; i < res.Plen; i++)
                res.Password[i] = data[i + shift];
            return res;
        }
        public byte[] ToByteArray()
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Ver); 
            writer.Write(Username!.Length);
            writer.Write(Username);
            writer.Write(Password!.Length);
            writer.Write(Password);
            writer.Flush();
            return stream.ToArray();
        }
    }
    public class PasswordAuthServerResponce
    {
        public byte Ver { get; set; } = 0;
        public byte Status {  get; set; } = 0;
        public static PasswordAuthServerResponce Parse(byte[] data)
        {
            PasswordAuthServerResponce res = new();
            res.Ver = data[0];
            res.Status = data[1];
            return res;
        }
        public byte[] ToByteArray()
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Ver);
            writer.Write(Status);
            writer.Flush();
            return stream.ToArray();
        }
    }
    public class UdpPacket
    {
        public short Rsv { get; set; } = 0;
        public byte Frag { get; set; } = 0;
        public Atyp Atyp_ { get; set; } = Atyp.IpV4;
        public byte[]? DstAddr { get; set; }
        public ushort DstPort { get; set; } = 0;
        public byte[]? Data { get; set; }
        public static UdpPacket Parse(byte[] data)
        {
            UdpPacket res = new();
            res.Frag = data[2];
            res.Atyp_ = (Atyp)data[3];

            byte shift = (res.Atyp_ == Atyp.Domain) ? (byte)5 : (byte)4;

            if (res.Atyp_ == Atyp.Domain)
                res.DstAddr = new byte[data[4]];
            else if (res.Atyp_ == Atyp.IpV6)
                res.DstAddr = new byte[IpV6Size];
            else
                res.DstAddr = new byte[IpV4Size];

            for (byte i = 0; i < res.DstAddr.Length; i++)
            {
                res.DstAddr[i] = data[i + shift];
            }

            var portSpan = data.AsSpan(res.DstAddr.Length + shift, PortSize);
            portSpan.Reverse();
            res.DstPort = BitConverter.ToUInt16(portSpan);

            res.Data = new byte[data.Length - res.DstAddr.Length - shift - PortSize];
            for(int i = 0; i < res.Data.Length; i++)
            {
                res.Data[i] = data[i + res.DstAddr.Length + shift + PortSize];
            }

            return res;
        }
        public byte[] ToByteArray()
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            var port = BitConverter.GetBytes(DstPort).AsSpan();
            port.Reverse();
            writer.Write(this.Rsv);
            writer.Write(this.Frag);
            writer.Write((byte)this.Atyp_);

            if (this.Atyp_ == Atyp.Domain)
                writer.Write((byte)DstAddr!.Length);

            writer.Write(this.DstAddr!);
            writer.Write(port);
            writer.Write(this.Data!);
            writer.Flush();
            return stream.ToArray();
        }
    }
    public enum Atyp : byte
    {
        IpV4 = 0x1,
        Domain = 0x3,
        IpV6 = 0x4,
    }

    public enum ConnectType : byte
    {
        CONNECT = 0x1,
        BIND = 0x2,
        UDP = 0x3
    };
    public enum RepType : byte
    {
        SUCCESS = 0x0,
        PROXY_ERROR = 0X1,
        NOT_ALLOW = 0X2,
        NETWORK_UNAVAILABLE = 0X3,
        HOST_UNAVAILABLE = 0X4,
        CONNECTION_REFUSAL = 0X5,
        TTL_ERROR = 0X6,
        COMMAND_NOT_SUPPORTED = 0X7,
        ADDRESS_TYPE_NOT_SUPPORTED = 0X8
    }
    public class SocksCommandNotSupported : ApplicationException
    {
        public SocksCommandNotSupported() : base("Command not supported") { }
    }
    public class SocksAddrTypeNotSupported : ApplicationException
    {
        public SocksAddrTypeNotSupported() : base("Address type not supported") { }
    }
    public class SocksConnectionRejectByRule : ApplicationException
    {
        public SocksConnectionRejectByRule() : base("Error by rule") { }
    }
    public class SocksMethodNotSupported : ApplicationException
    {
        public SocksMethodNotSupported() : base("Authentication method not supported") { }
    }

    public class SocksAuthReject: ApplicationException
    {
        public SocksAuthReject() : base("Reject by authentication") { }
    }
};
