using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using static ProxyModule.HttpRequest;
using static ProxyModule.IHttpPacket;

namespace ProxyModule;
public class HttpResponce : IHttpPacket, ICloneable
{
    public IHttpPacket.Protocols Protocol { get; set; } = IHttpPacket.Protocols.HTTP;
    public float Ver { get; set; } = 1.1f;
    public short Status { get; set; } = 0;
    public string Msg { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public List<byte> Data { get; set; } = new List<byte>();
    private HttpHelper helper { get; init; }
    public HttpResponce()
    {
        helper = new HttpHelper(this);
    }
    private void ParseFirstLine(byte[] data)
    {
        using MemoryStream memStream = new MemoryStream(data);
        using StreamReader reader = new StreamReader(memStream);

        string? firstLine = reader.ReadLine();

        if (firstLine is null)
            throw new ArgumentNullException("Data was be empty");

        string[] firstLineElems = firstLine.Split(" ");
        this.Protocol = (Protocols)Enum.Parse(typeof(Protocols), firstLineElems[0]);
        this.Ver = float.Parse(firstLineElems[1]);
        this.Status = short.Parse(firstLineElems[2]);
        this.Msg = firstLineElems[3];
    }
    private byte[] FirstLineToByteArray()
    {
        using MemoryStream memStream = new MemoryStream();
        using StreamWriter writer = new StreamWriter(memStream);
        writer.Write(this.Protocol.ToString());
        writer.Write("/");
        writer.Write(this.Ver.ToString());
        writer.Write(" ");
        writer.Write(this.Status.ToString());
        writer.Write(" ");
        writer.Write(this.Msg);
        writer.Write("\r\n");
        writer.Flush();
        return memStream.ToArray();
    }
    public static HttpResponce Parse(byte[] data)
    {
        HttpResponce request = new HttpResponce();
        request.ParseFirstLine(data);
        long pos = request.helper.FillHeaders(data);
        request.helper.FillData(data, pos);
        return request;
    }
    public static HttpResponce Parse(string data)
    {
        HttpResponce request = new HttpResponce();
        byte[] byteData = Encoding.UTF8.GetBytes(data);
        return Parse(byteData);
    }
    public byte[] ToByteArray()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(FirstLineToByteArray());
        bytes.AddRange(helper.ToByteArrayHeaders());
        bytes.AddRange(Data);
        return bytes.ToArray();
    }
    public override string ToString()
    {
        return Encoding.UTF8.GetString(ToByteArray());
    }
    public static HttpResponce ParseHeader(byte[] data)
    {
        var res = new HttpResponce();
        res.ParseFirstLine(data);
        res.helper.FillHeaders(data);
        return res;
    }

    public object Clone()
    {
        return new HttpResponce
        {
            Status = this.Status,
            Msg = this.Msg,
            Headers = new Dictionary<string,string>(this.Headers),
        };
    }
}
