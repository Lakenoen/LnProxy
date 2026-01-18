using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace IndexModule;
public abstract class AData : IEquatable<AData>, IComparable<AData>
{
    public bool Equals(AData? other)
    {
        if (other is null)
            return false;

        return CompareTo(other).Equals(0);
    }
    public abstract int CompareTo(AData? other);
    public abstract byte[] ToByteArray();
    public abstract int Size { get;}
    public static bool operator <(AData first, AData second)
    {
        return (first.CompareTo(second) < 0) ? true : false;
    }
    public static bool operator >(AData first, AData second)
    {
        return !(first < second) && !first.Equals(second);
    }
    public static bool operator <=(AData first, AData second)
    {
        return (first.CompareTo(second) < 0 || first.CompareTo(second) == 0);
    }
    public static bool operator >=(AData first, AData second)
    {
        return (first.CompareTo(second) > 0 || first.CompareTo(second) == 0);
    }

}
