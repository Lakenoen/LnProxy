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
public class HttpReq
{
    public Methods Method { get; set; } = Methods.GET;
    public Protocols Protocol { get; set; } = Protocols.HTTP;
    public Uri? Uri { get; set; }
    public float Ver { get; set; } = 1.1f;
    public Dictionary<string, string> Headers { get; set; } = new();
    public Dictionary<string, string?> Params { get; set; } = new();
    public List<byte> Data { get; set; } = new();

    private static string ToStringHeader(in HttpReq req)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(req.Method.ToString()).Append(" ");
        
        if (req.Uri == null)
            throw new ApplicationException("Uri was be null");

        sb.Append(req.Uri.AbsoluteUri);

        if (req.Uri.Query.Length == 0 && req.Params.Count != 0)
        {
            sb.Append("?");
            foreach (KeyValuePair<string, string> pair in req.Params)
            {
                sb.Append($"{pair.Key}={pair.Value}&");
            }
            sb.Remove(sb.Length - 1, 1);
        }

        sb.Append(" ").Append(req.Protocol.ToString())
        .Append('/').Append(req.Ver.ToString()).Append("\r\n");

        foreach (KeyValuePair<string, string> pair in req.Headers)
        {
            sb.Append($"{pair.Key}: {pair.Value}").Append("\r\n");
        }

        sb.Append("\r\n\r\n");
        return sb.ToString();
    }
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(ToStringHeader(this));
        sb.Append(Encoding.UTF8.GetString(Data.ToArray()));
        return sb.ToString();
    }
    public byte[] ToByteArray()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(Encoding.UTF8.GetBytes( ToStringHeader(this) )); // add Header
        bytes.AddRange(Data.ToArray());
        return bytes.ToArray();
    }
    public static HttpReq Parse(in string data)
    {
        var res = Parse(Encoding.UTF8.GetBytes(data));
        return res;
    }
    public static HttpReq Parse(byte[] data)
    {
        var res = new HttpReq();
        using MemoryStream memStream = new MemoryStream(data);
        using StreamReader reader = new StreamReader(memStream);

        string firstLine = reader.ReadLine();
        string[] firstLineElems = firstLine.Split(" ");
        res.Method = (Methods)Enum.Parse(typeof(Methods), firstLineElems[0]);
        res.Uri = new Uri(firstLineElems[1].Trim());
        string[] proto_ver = firstLineElems[2].Split("/");
        res.Protocol = (Protocols)Enum.Parse(typeof(Protocols), proto_ver[0]);
        res.Ver = float.Parse(proto_ver[1]);

        ParseQuery(res, res.Uri.Query);

        string? line = string.Empty;
        while( (line = reader.ReadLine() ) != null && line != string.Empty)
        {
            var headerLine = line.Split(":", 2);
            res.Headers.Add(headerLine[0].Trim(), headerLine[1].Trim());
        }

        using BinaryReader bReader = new BinaryReader(memStream);
        int bodySize = data.Length - (int)memStream.Position;
        res.Data = bReader.ReadBytes(bodySize).ToList();
        return res;
    }

    private static void ParseQuery(HttpReq res, string? query)
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

    public static HttpReq ParseHeader(byte[] data)
    {
        var res = new HttpReq();
        using MemoryStream memStream = new MemoryStream(data);
        using StreamReader reader = new StreamReader(memStream);

        string firstLine = reader.ReadLine();
        string[] firstLineElems = firstLine.Split(" ");
        res.Method = (Methods)Enum.Parse(typeof(Methods), firstLineElems[0]);
        res.Uri = new Uri(firstLineElems[1].Trim());
        string[] proto_ver = firstLineElems[2].Split("/");
        res.Protocol = (Protocols)Enum.Parse(typeof(Protocols), proto_ver[0]);
        res.Ver = float.Parse(proto_ver[1]);

        ParseQuery(res, res.Uri.Query);

        string? line = string.Empty;
        while ((line = reader.ReadLine()) != null && line != string.Empty)
        {
            var headerLine = line.Split(":", 2);
            res.Headers.Add(headerLine[0].Trim(), headerLine[1].Trim());
        }

        return res;
    }
    public enum Methods{
        GET, POST, PUT, DELETE, TRACE, CONNECT
    }
    public enum Protocols { HTTP, HTTPS }
};

