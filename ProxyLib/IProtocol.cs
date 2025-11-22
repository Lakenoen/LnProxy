using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyLib;
internal interface IProtocol
{
    public void Run(CancellationToken cancel);
}
