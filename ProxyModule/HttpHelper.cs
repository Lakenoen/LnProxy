using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyModule;
internal class HttpHelper(IHttpPacket packet)
{
    private IHttpPacket packet = packet;

    public long FillHeaders(byte[] data)
    {
        long pos = CreateHeaders(data, out var dict);
        packet.Headers = dict;
        return pos;
    }
    private static long CreateHeaders(byte[] data, out Dictionary<string, string> header)
    {
        header = new Dictionary<string, string>();
        using MemoryStream memStream = new MemoryStream(data);
        using StreamReader reader = new StreamReader(memStream);
        var firstLine = reader.ReadLine();

        ushort countEmptyLine = 0;
        string? line = string.Empty;
        while ((line = reader.ReadLine()) != null && countEmptyLine < 2)
        {
            if (line == string.Empty)
            {
                ++countEmptyLine;
                continue;
            }
            var headerLine = line.Split(":", 2);
            header.Add(headerLine[0].Trim(), headerLine[1].Trim());
        }
        return memStream.Position;
    }

    public void FillData(byte[] data, long pos = -1)
    {
        using MemoryStream memStream = new MemoryStream(data);
        using StreamReader reader = new StreamReader(memStream);

        long startOfData = pos;
        if (pos < 0)
            startOfData = CreateHeaders(data, out var dict);

        memStream.Seek(startOfData, SeekOrigin.Begin);
        var packetData = data.AsMemory((int)memStream.Position, data.Length - (int)memStream.Position).ToArray();
        packet.Data = new List<byte>(packetData);
    }

    public string ToStringHeaders()
    {
        StringBuilder sb = new StringBuilder();
        foreach (KeyValuePair<string, string> pair in packet.Headers)
        {
            sb.Append($"{pair.Key}: {pair.Value}").Append("\r\n");
        }

        sb.Append("\r\n\r\n");
        return sb.ToString();
    }

    public byte[] ToByteArrayHeaders()
    {
        return Encoding.UTF8.GetBytes(ToStringHeaders());
    }
    public string ToStringData()
    {
        return Encoding.UTF8.GetString(packet.Data.ToArray());
    }
}
