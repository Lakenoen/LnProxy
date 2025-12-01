using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ProxyModule;

[StructLayout(LayoutKind.Explicit)]
public class UnionData()
{
    [FieldOffset(0)]
    public List<byte>? byteData;

    [FieldOffset(0)]
    public List<char>? charData;

    [FieldOffset(0)]
    public List<int>? intData;

    public UnionData(IEnumerable<byte> byteData) : this()
    {
        this.byteData = byteData.ToList();
    }
    public UnionData(IEnumerable<char> charData) : this()
    {
        this.charData = charData.ToList();
    }
    public UnionData(IEnumerable<int> intData) : this()
    {
        this.intData = intData.ToList();
    }
    public override string ToString()
    {
        return new string(charData?.ToArray());
    }
}
