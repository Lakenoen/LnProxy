using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public interface Serialilzable : ICloneable
{
    public byte[] ToByteArray();
    public abstract Serialilzable FromByteArray(byte[] data);
    public abstract int Size { get; }
}
