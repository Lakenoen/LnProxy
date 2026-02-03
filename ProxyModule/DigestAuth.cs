using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NetworkModule;

namespace ProxyModule
{
    public class DigestAuth : IAuth
    {
        private string Nonce { get; set; } = string.Empty;
        private string Opaque { get; init; } = Convert.ToBase64String(Encoding.UTF8.GetBytes("LnProxySession"));
        private string Realm { get; init; } = "LnProxy";

        protected Stack<Func<HttpRequest, object?>> stack = new();
        protected Func<string, string?> _getPasswd;
        protected readonly Proxy.ProxyClientContext _context;
        protected readonly Proxy _parentProxy;
        protected readonly TcpClientWrapper _client;
        public DigestAuth(
            Func<string, string?> getPasswd,
            Proxy.ProxyClientContext context,
            Proxy proxy,
            TcpClientWrapper client)
        {
            this._client = client;
            this._parentProxy = proxy;
            this._context = context;
            this.Nonce = MakeNonce();
            this._getPasswd = getPasswd;
            stack.Push(Valid);
            stack.Push(Init);
        }
        public virtual object? Init(HttpRequest req)
        {
            var res = HttpServerResponses.Authentication.Clone() as HttpResponce;
            if (req.Headers.ContainsKey("Proxy-Authorization"))
                res!.Headers["stale"] = "true";
            
            res!.Headers.Add("Proxy-Authenticate", $"Digest realm=\"{Realm}\", nonce=\"{this.Nonce}\", opaque=\"{Opaque}\", qop=\"auth\", algorithm=SHA-256");
            return res;
        }

        private Dictionary<string,string> ParseDigestHeader(HttpRequest req)
        {
            var res = new Dictionary<string,string>();
            string[] pairs = req.Headers["Proxy-Authorization"].Split(",");
            foreach(var pair in pairs)
            {
                string[] keyValue = pair.Replace("\"", "").Split("=", 2);
                res.Add(keyValue[0].Replace("Digest ","").Replace(" ", "").ToLower(), keyValue[1].Replace(" ",""));
            }
            return res;
        }
        public virtual object? Valid(HttpRequest req)
        {
            var parameters = ParseDigestHeader(req);

            if(this.Nonce != parameters["nonce"] || this.Realm != parameters["realm"] || this.Opaque != parameters["opaque"])
                return false;

            string? pass = _getPasswd(parameters["username"]);
            string ha1 = getHashSha256($"{parameters["username"]}:{Realm}:{pass}");
            string ha2 = getHashSha256($"{req.Method}:{req.Uri!.AbsoluteUri}");
            string reconstructHash = getHashSha256($"{ha1}:{this.Nonce}:{parameters["nc"]}:{parameters["cnonce"]}:auth:{ha2}");

            if (reconstructHash.Equals(parameters["response"]))
            {
                if (!_parentProxy.AddUserConnectionIfNeeded(parameters["username"], _client))
                    return false;
                _context.Username = parameters["username"];
                return true;
            }
            return false;   
        }
        private static string getHashSha256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using var hash = SHA256.Create();
            byte[] hashed = hash.ComputeHash(bytes);
            var sb = new StringBuilder();
            foreach (byte b in hashed)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
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
        public object? Next(HttpRequest req)
        {
            if (IsEnd())
                return null;
            return stack.Pop().Invoke(req);
        }

    }
}
