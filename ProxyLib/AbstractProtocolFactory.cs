using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyLib;

internal abstract class AbstractProtocolFactory
{
    public abstract AbstractResult<IProtocol> Create(IStream stream);
}
