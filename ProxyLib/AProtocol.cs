using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyLib;
internal abstract class AProtocol : IProtocol
{
    protected readonly IStream _stream;
    public AProtocol(CancellationToken cancel, IStream stream)
    {
        this._stream = stream;
    }
    public abstract void Run(CancellationToken cancel);

}
