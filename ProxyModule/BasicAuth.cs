using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetworkModule;

namespace ProxyModule;
internal class BasicAuth : DigestAuth
{
    private string Realm { get; init; } = "LnProxy";

    public BasicAuth(
        Func<string, string?> getPasswd,
        Proxy.ProxyClientContext context,
        Proxy proxy,
        TcpClientWrapper client
    ) : base(getPasswd, context, proxy, client)
    {
        base.stack.Clear();

        base.stack.Push(Valid);
        base.stack.Push(Init);
    }

    public override object? Init(HttpRequest req)
    {
        var res = HttpServerResponses.Authentication.Clone() as HttpResponce;
        if (req.Headers.ContainsKey("Proxy-Authorization"))
        {
            res!.Headers["stale"] = "true";
            if (!req.Headers["Proxy-Authorization"].StartsWith("Basic"))
            {
                res!.Headers.Add("Proxy-Authenticate", $"Basic realm=\"{Realm}\", charset=\"UTF-8\"");
                return res;
            }

            return base.Next(req);
        }

        res!.Headers.Add("Proxy-Authenticate", $"Basic realm=\"{Realm}\", charset=\"UTF-8\"");
        return res;
    }
    public override object? Valid(HttpRequest req)
    {
        var basicLine = req.Headers["Proxy-Authorization"].Replace("Basic ", "").Trim();
        var rawLine = Encoding.UTF8.GetString( Convert.FromBase64String(basicLine) );
        var keyValue = rawLine.Split(':');

        string? passwd = base._getPasswd(keyValue[0]);
        if(passwd is null)
            return new Ref<bool>(false);

        if (passwd.Equals(keyValue[1]))
            return new Ref<bool>(true);

        return new Ref<bool>(false);
    }
}
