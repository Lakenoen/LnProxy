using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class String32() : AData
{
    public override int Size => 32;
    public string Value { get; set; } = string.Empty;
    public String32(string value) : this()
    {
        this.Value = value;
    }
    public override int CompareTo(AData? other)
    {
        if (other is not String32 str)
            throw new ArgumentException("Type mismatch");

        return Value.CompareTo(str.Value);
    }
    public override byte[] ToByteArray()
    {
        return Encoding.UTF8.GetBytes(Value);
    }

    public static explicit operator String32(string value)
    {
        return new String32(value);
    }

    public static explicit operator string(String32 value)
    {
        return value.Value;
    }
}
