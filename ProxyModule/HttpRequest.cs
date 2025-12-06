using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SqlTypes;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ProxyModule;

public class HttpParseException : ApplicationException
{
    public HttpParseException() : base() { }
}
public class HttpRequest : IHttpPacket
{
    public IHttpPacket.Methods Method { get; set; } = IHttpPacket.Methods.GET;
    public IHttpPacket.Protocols Protocol { get; set; } = IHttpPacket.Protocols.HTTP;
    public Uri? Uri { get; set; }
    public float Ver { get; set; } = 1.1f;
    public Dictionary<string, string> Headers { get; set; } = new();
    public Dictionary<string, string?> Params { get; set; } = new();
    public List<byte> Data { get; set; } = new();
    private HttpHelper helper { get; init; }

    public HttpRequest()
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
        this.Method = (IHttpPacket.Methods)Enum.Parse(typeof(IHttpPacket.Methods), firstLineElems[0]);
        this.Uri = new Uri(firstLineElems[1].Trim());
        string[] proto_ver = firstLineElems[2].Split("/");
        this.Protocol = (IHttpPacket.Protocols)Enum.Parse(typeof(IHttpPacket.Protocols), proto_ver[0]);
        this.Ver = float.Parse(proto_ver[1]);

        ParseQuery(this, this.Uri.Query);
    }

    private byte[] FirstLineToByteArray()
    {
        using MemoryStream memStream = new MemoryStream();
        using StreamWriter writer = new StreamWriter(memStream);
        writer.Write(this.Method.ToString());
        writer.Write(" ");
        writer.Write(this.Uri.AbsoluteUri);
        writer.Write(" ");
        writer.Write(this.Protocol.ToString());
        writer.Write("/");
        writer.Write(this.Ver.ToString());
        writer.Write("\r\n");
        writer.Flush();
        return memStream.ToArray();
    }
    public static HttpRequest Parse(byte[] data)
    {
        HttpRequest request = new HttpRequest();
        request.ParseFirstLine(data);
        long pos = request.helper.FillHeaders(data);
        request.helper.FillData(data, pos);
        return request;
    }
    public static HttpRequest Parse(string data)
    {
        HttpRequest request = new HttpRequest();
        byte[] byteData = Encoding.UTF8.GetBytes(data);
        return Parse(byteData);
    }
    public byte[] ToByteArray()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(FirstLineToByteArray());
        bytes.AddRange( helper.ToByteArrayHeaders() );
        bytes.AddRange(Data);
        return bytes.ToArray();
    }
    public override string ToString()
    {
        return Encoding.UTF8.GetString(ToByteArray());
    }
    private static void ParseQuery(HttpRequest res, string? query)
    {
        if (query == null)
            return;

        NameValueCollection qp = HttpUtility.ParseQueryString(query);
        foreach (string? key in qp.AllKeys)
        {
            string? value = qp[key!];
            if (key is null)
                res.Params.Add(value!, key!);
            else
                res.Params.Add(key!, value!);
        }
    }
    public static HttpRequest ParseHeader(byte[] data)
    {
        var res = new HttpRequest();
        res.ParseFirstLine(data);
        res.helper.FillHeaders(data);
        return res;
    }
    
};

