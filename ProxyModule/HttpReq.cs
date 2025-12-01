using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SqlTypes;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;

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
    public Dictionary<string, string> Header { get; set; } = new();
    public Dictionary<string, string?> Params { get; set; } = new();
    public UnionData Data { get; private set; } = new UnionData();
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(Method.ToString()).Append(" ");

        if (Uri == null)
            throw new ApplicationException("Uri was be null");

        sb.Append(Uri.AbsoluteUri);

        if(Uri.Query.Length == 0 && Params.Count != 0)
        {
            sb.Append("?");
            foreach(KeyValuePair<string, string> pair in Params)
            {
                sb.Append($"{pair.Key}={pair.Value}&");
            }
            sb.Remove(sb.Length - 1, 1);
        }

        sb.Append(" ").Append(Protocol.ToString())
        .Append('/').Append(Ver.ToString()).Append("\r\n");
        
        foreach(KeyValuePair<string, string> pair in Header)
        {
            sb.Append($"{pair.Key}: {pair.Value}").Append("\r\n");
        }

        sb.Append("\r\n\r\n");
        sb.Append(Data.ToString());
        return sb.ToString();
    }
    public static HttpReq Parse(in string data)
    {
        var res = new HttpReq();
        var lines = data.Split("\r\n");
        var firstLine = lines[0];
        var firstLineElems = firstLine.Split(" ");
        res.Method = (Methods)Enum.Parse(typeof(Methods), firstLineElems[0]);
        res.Uri = new Uri(firstLineElems[1].Trim());
        var proto_ver = firstLineElems[2].Split("/");
        res.Protocol = (Protocols)Enum.Parse(typeof(Protocols), proto_ver[0]);
        res.Ver = float.Parse(proto_ver[1]);

        ParseQuery(res, res.Uri.Query);

        for (int i = 1; i < lines.Length; ++i)
        {
            var headerLine = lines[i].Split(":");
            if (headerLine.Length == 0
                || headerLine[0].Length == 0 
                || headerLine[1].Length == 0)
                continue;
            res.Header.Add(headerLine[0].Trim(), headerLine[1].Trim());
        }

        res.Data.charData = lines.Last().ToList();
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
    public enum Methods{
        GET, POST, PUT, DELETE, TRACE, CONNECT
    }
    public enum Protocols { HTTP, HTTPS }
};

