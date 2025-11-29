using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyModule;
internal class HttpContent
{
    public Dictionary<string, string> Header { get; set; } = new();
    public List<byte> Data { get; set; } = new();
}
