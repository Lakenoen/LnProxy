using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class Element : IComparable<Element>, IEquatable<Element>
{
    public AData Key { get; init; }
    public AData Value { get; init; }
    public int[] Links { get; set; } = new int[2] { -1, -1 };

    public Element(AData key, AData value)
    {
        this.Key = key;
        this.Value = value;
    }
    public Element(AData key, AData value, int left, int right) : this(key, value)
    {
        Links[0] = left;
        Links[1] = right;
    }
    public int CompareTo(Element? other)
    {
        if (other is null)
            return 1;
        return Key.CompareTo(other.Key);
    }
    public static bool operator<(Element first, Element second)
    {
        return first.Key < second.Key;
    }
    public static bool operator>(Element first, Element second)
    {
        return first.Key > second.Key;
    }
    public static bool operator<=(Element first, Element second)
    {
        return first.Key <= second.Key;
    }
    public static bool operator >=(Element first, Element second)
    {
        return first.Key >= second.Key;
    }
    public bool Equals(Element? other)
    {
        if (other is null)
            return false;
        return Key.Equals(other.Key);
    }
}
