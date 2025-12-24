using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyModule
{
    internal class Digest
    {
        public string Nonce { get; private set; } = string.Empty;
        public string Opaque { get; init; } = Convert.ToBase64String(Encoding.UTF8.GetBytes("LnProxySession"));
        private Stack<Func<HttpRequest, HttpResponce>> stack = new();
        public Digest()
        {
            stack.Push(InitAuth);
        }
        private HttpResponce InitAuth(HttpRequest req)
        {
            this.Nonce = MakeNonce();

            var res = HttpServerResponses.Authentication;
            res.Headers.Add("Proxy-Authenticate", $"Digest realm=\"LnProxy\", nonce=\"{this.Nonce}\", opaque=\"{Opaque}\", qop=\"auth\", algorithm=SHA-256");
            return res;
        }

        private string MakeNonce()
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string random = Guid.NewGuid().ToString("N").Substring(0, 8);
            string nonce = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{timestamp}:{random}")
            );
            return nonce;
        }
        public bool IsEnd()
        {
            return stack.Count == 0;
        }
        public HttpResponce? Next(HttpRequest req)
        {
            if (IsEnd())
                return null;
            return stack.Pop().Invoke(req);
        }

    }
}
