using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyModule;
public interface IHttpPacket
{
    public Dictionary<string, string> Headers { get; set; }
    public List<byte> Data { get; set; }
    public enum Protocols { HTTP, HTTPS }
    public enum Methods
    {
        GET, POST, PUT, DELETE, TRACE, CONNECT
    }
}
