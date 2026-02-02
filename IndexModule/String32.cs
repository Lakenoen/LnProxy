using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace IndexModule;
public class String32() : AData
{
    public override int Size => ToByteArray().Length;
    public const int InternalSize = 32;
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
        using MemoryStream stream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(stream);
        byte[] data = Encoding.UTF8.GetBytes(Value);
        writer.Write(data.Length);
        writer.Flush();
        Array.Resize(ref data, InternalSize);
        writer.Write(data);
        writer.Flush();
        return stream.ToArray();
    }
    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override Serialilzable FromByteArray(byte[] data)
    {
        if (data.Length > this.Size)
            throw new ArgumentException("String32 size error");

        using MemoryStream stream = new MemoryStream(data);
        using BinaryReader reader = new BinaryReader(stream);
        int len = reader.ReadInt32();
        byte[] payload = reader.ReadBytes(len);
        return (String32) Encoding.UTF8.GetString(payload);
    }

    public override object Clone()
    {
        return new String32(Value);
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
