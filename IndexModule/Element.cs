using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class Element : IComparable<Element>
{
    private Data Key { get; init; }
    private Data Value { get; init; }
    public int[] Links { get; set; } = new int[2];

    public Element(Data key, Data value)
    {
        this.Key = key;
        this.Value = value;
    }
    public Element(Data key, Data value, int left, int right) : this(key, value)
    {
        Links[0] = left;
        Links[1] = right;
    }
    public int CompareTo(Element? other)
    {
        if (other is null)
            return 1;

        byte[] thisBytes = (byte[])this.Key;
        byte[] otherBytes = (byte[])other.Key;

        for (int i = 0; i < 0xff; ++i)
        {
            if (thisBytes[i] == otherBytes[i])
                continue;
            if (thisBytes[i] > otherBytes[i])
                return 1;
            if (thisBytes[i] < otherBytes[i])
                return -1;
        }
        return 0;
    }
}
