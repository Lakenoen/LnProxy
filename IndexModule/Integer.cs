using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class Integer() : AData
{
    public int Value { get; set; } = 0;
    public override int Size { get => sizeof(int);}

    public Integer(int value) : this()
    {
        this.Value = value;
    }
    public override int CompareTo(AData? other)
    {
        if (other is not Integer integer)
            throw new ArgumentException("Type mismatch");

        return Value.CompareTo(integer.Value);
    }
    public override byte[] ToByteArray()
    {
        return BitConverter.GetBytes(Value);
    }

    public static explicit operator Integer(int value)
    {
        return new Integer(value);
    }

    public static explicit operator int(Integer value)
    {
        return value.Value;
    }
}
