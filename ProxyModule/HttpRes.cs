using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ProxyModule.HttpReq;

namespace ProxyModule;
public class HttpRes()
{
    public Protocols Protocol { get; set; } = Protocols.HTTP;
    public float Ver { get; set; } = 1.1f;
    public short Status { get; set; } = 0;
    public string Msg { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public List<byte> Data { get; private set; } = new List<byte>();

    public byte[] ToByteArray()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(Encoding.UTF8.GetBytes(ToStringHeader(this))); // add Header
        bytes.AddRange(Data.ToArray());
        return bytes.ToArray();
    }
    public static HttpRes Parse(byte[] data)
    {
        HttpRes res = new HttpRes();
        using MemoryStream memStream = new MemoryStream(data);
        using StreamReader reader = new StreamReader(memStream);

        string firstLine = reader.ReadLine();
        string[] firstLineElems = firstLine.Split(" ");
        res.Protocol = (Protocols)Enum.Parse(typeof(Protocols), firstLineElems[0]);
        res.Ver = float.Parse(firstLineElems[1]);
        res.Status = short.Parse(firstLineElems[2]);
        res.Msg = firstLineElems[3];

        string? line = string.Empty;
        while ((line = reader.ReadLine()) != null && line != string.Empty)
        {
            var headerLine = line.Split(":", 2);
            res.Headers.Add(headerLine[0].Trim(), headerLine[1].Trim());
        }

        using BinaryReader bReader = new BinaryReader(memStream);
        int bodySize = data.Length - (int)memStream.Position;
        res.Data = bReader.ReadBytes(bodySize).ToList();
        return res;
    }
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(ToStringHeader(this));
        sb.Append(Encoding.UTF8.GetString(Data.ToArray()));
        return sb.ToString();
    }
    private static string ToStringHeader(in HttpRes res)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(res.Protocol.ToString()).Append("/").Append(res.Ver.ToString()).Append(" ");
        sb.Append(res.Status.ToString()).Append(" ");
        sb.Append(res.Msg).Append("\r\n");
        foreach (KeyValuePair<string, string> pair in res.Headers)
        {
            sb.Append($"{pair.Key}: {pair.Value}\r\n");
        }
        sb.Append("\r\n");
        return sb.ToString();
    }

    public static HttpRes ParseHeader(byte[] data)
    {
        HttpRes res = new HttpRes();
        using MemoryStream memStream = new MemoryStream(data);
        using StreamReader reader = new StreamReader(memStream);

        string firstLine = reader.ReadLine();
        string[] firstLineElems = firstLine.Split(" ");
        res.Protocol = (Protocols)Enum.Parse(typeof(Protocols), firstLineElems[0]);
        res.Ver = float.Parse(firstLineElems[1]);
        res.Status = short.Parse(firstLineElems[2]);
        res.Msg = firstLineElems[3];

        string? line = string.Empty;
        while ((line = reader.ReadLine()) != null && line != string.Empty)
        {
            var headerLine = line.Split(":", 2);
            res.Headers.Add(headerLine[0].Trim(), headerLine[1].Trim());
        }

        return res;
    }

}
