using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class Element : IComparable<Element>, IEquatable<Element>, ICloneable, Serialilzable
{
    public AData Key { get; set; }
    public AData Value { get; set; }
    public int[] Links { get; set; } = new int[2] { -1, -1 };

    public int Size => ToByteArray().Length;

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

    public object Clone()
    {
        return new Element(this.Key, this.Value, this.Links[0], this.Links[1]);
    }

    public override int GetHashCode()
    {
        return Key.GetHashCode();
    }

    public byte[] ToByteArray()
    {
        using MemoryStream stream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(Links[0]);
        writer.Write(Links[1]);
        writer.Write(Key.Size);
        writer.Write(Key.ToByteArray());
        writer.Write(Value.Size);
        writer.Write(Value.ToByteArray());
        return stream.ToArray();
    }
    public Serialilzable FromByteArray(byte[] data)
    {
        using MemoryStream stream = new MemoryStream(data);
        using BinaryReader reader = new BinaryReader(stream);
        this.Links[0] = reader.ReadInt32();
        this.Links[1] = reader.ReadInt32();
        int offset = reader.ReadInt32();
        this.Key = (AData)Key.FromByteArray( reader.ReadBytes(offset));
        offset = reader.ReadInt32();
        this.Value = (AData)Value.FromByteArray( reader.ReadBytes(offset) );
        return this;
    }
}
