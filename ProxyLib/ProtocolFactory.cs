using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyLib;

internal class ProtocolFactory : AbstractProtocolFactory
{
    public override AbstractResult<IProtocol> Create(IStream stream)
    {
        //TODO
        var firstPack = stream.ReadAvailable();
        int type = firstPack.Item[0];
        string check = Encoding.ASCII.GetString(firstPack.Item);
        Console.WriteLine(check);
        if (Encoding.ASCII.GetString(firstPack.Item).IndexOf("HTTP/") >= 0)
        {
            stream.Write(Encoding.UTF8.GetBytes("dfgg"));
            stream.Close();
        }
        return new SuccessResult<IProtocol>();
    }
}
