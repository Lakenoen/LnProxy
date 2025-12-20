using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyModule;
static class HttpServerResponses
{
    public static HttpResponce Connected { get; } = new HttpResponce
    {
        Status = 200,
        Msg = "Connection Established",
        Headers =
                {
                    {"Connection","close"},
                    {"Cache-Control","no-cache, no-store, must-revalidate"},
                    {"Pragma","no-cache"},
                    {"Expires","0"}
                }
    };

    public static HttpResponce InternalError { get; } = new HttpResponce
    {
        Status = 500,
        Msg = "Internal Server Error",
        Headers =
        {
            {"Connection","close"},
            {"Proxy-Agent", "LnProxy" },
            {"X-Proxy-Error", "Proxy error"}
        }
    };

    public static HttpResponce BadGateway { get; } = new HttpResponce
    {
        Status = 502,
        Msg = "Bad Gateway",
        Headers =
        {
            {"Connection","close"},
            {"Proxy-Agent", "LnProxy" },
            {"X-Proxy-Error", "DNS resolution failed"}
        }
    };
    public static HttpResponce GatewayTimeout { get; } = new HttpResponce
    {
        Status = 504,
        Msg = "Gateway Timeout",
        Headers =
        {
            {"Connection","close"},
            {"Proxy-Agent", "LnProxy" },
            {"X-Proxy-Error", "Connection timeout"}
        }
    };
}
