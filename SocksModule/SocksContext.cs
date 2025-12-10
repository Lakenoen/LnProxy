using System.Net;
using NetworkModule;

namespace SocksModule;

public partial class SocksContext
{
    public IPEndPoint? ServerEndPoint {  get; set; }
    public IPEndPoint? BindServerEndPoint {  get; set; }
    public TcpServer? BindServer { get; set; }
    public string TargetAddress { get; private set; } = string.Empty;
    public int TargetPort { get; private set; }
    public Atyp TargetType {  get; private set; }
    public byte Method { get; private set; }
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
        public short DstPort = 0;
        public static TcpConnectionClientRequest Parse(byte[] data)
        {
            TcpConnectionClientRequest res = new TcpConnectionClientRequest();
            res.Ver = data[0];
            res.Smd = (ConnectType)data[1];
            res.Rsv = data[2];
            res.Atyp = (Atyp)data[3];
            byte len = data[4];
            res.DstAddr = new byte[len];
            byte shift = sizeof(byte) * 4;
            for (byte i = 0; i < len; i++)
            {
                res.DstAddr[i] = data[i + shift + 1];
            }
            var portSpan = data.AsSpan(res.DstAddr.Length + shift + 1, sizeof(short));
            portSpan.Reverse();
            res.DstPort = BitConverter.ToInt16(portSpan);
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
            writer.Write((byte)DstAddr!.Length);
            writer.Write(DstAddr);
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
        public short BndPort { get; set; } = 0;

        public static TcpConnectionServerResponse Parse(byte[] data)
        {
            TcpConnectionServerResponse res = new TcpConnectionServerResponse();
            res.Ver = data[0];
            res.Rep = data[1];
            res.Rsv = data[2];
            res.Atyp = (Atyp)data[3];
            byte shift = sizeof(byte) * 4;
            res.BndAddr = new byte[data.Length - sizeof(short) - shift];
            for (byte i = shift; i < data.Length - sizeof(short); i++)
            {
                res.BndAddr[i - shift] = data[i];
            }
            var portSpan = data.AsSpan(res.BndAddr.Count() + shift, sizeof(short));
            portSpan.Reverse();
            res.BndPort = BitConverter.ToInt16(portSpan);
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
    public enum Rep : byte
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

};
