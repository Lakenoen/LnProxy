using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyLib;
interface IStream
{
    public AbstractResult<byte[]> ReadAvailable();
    public AbstractResult<Empty> Write(byte[] bytes);
    public AbstractResult<int> Available();
    public bool isClosed();
    public byte[] Buffer { get; }
    public AbstractResult<Empty> Close();
}
