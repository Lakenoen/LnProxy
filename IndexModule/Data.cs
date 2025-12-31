using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace IndexModule;
public class Data
{
    public const short SIZE = 0xff;
    private byte[] _data = new byte[SIZE];
    public int Size { get; private set; } = 0;
    public Data(byte[] data)
    {
        Copy(data,_data);
    }
    public Data(int value)
    {
        Copy(BitConverter.GetBytes(value), _data);
    }
    public Data(short value)
    {
        Copy(BitConverter.GetBytes(value), _data);
    }
    public Data(bool value)
    {
        Copy(BitConverter.GetBytes(value), _data);
    }
    public Data(string str)
    {
        Copy(Encoding.UTF8.GetBytes(str), _data);
    }

    public static explicit operator int(Data param)
    {
        return BitConverter.ToInt32(param._data, 0);
    }

    public static explicit operator short(Data param)
    {
        return BitConverter.ToInt16(param._data, 0);
    }

    public static explicit operator bool(Data param)
    {
        return BitConverter.ToBoolean(param._data, 0);
    }

    public static explicit operator string(Data param)
    {
        return Encoding.UTF8.GetString(param._data);
    }

    public static explicit operator byte[](Data param)
    {
        var temp = new byte[param.Size];
        CopyArray(param._data, temp, param.Size);
        return temp;
    }

    public static explicit operator Data(int param)
    {
        return new Data(param);
    }

    public static explicit operator Data(short param)
    {
        return new Data(param);
    }

    public static explicit operator Data(bool param)
    {
        return new Data(param);
    }

    public static explicit operator Data(string param)
    {
        return new Data(param);
    }

    public static explicit operator Data(byte[] param)
    {
        return new Data(param);
    }
    public void SetVal(int value)
    {
        Copy(BitConverter.GetBytes(value), _data);
    }
    public void SetVal(short value)
    {
        Copy(BitConverter.GetBytes(value), _data);
    }
    public void SetVal(bool value)
    {
        Copy(BitConverter.GetBytes(value), _data);
    }
    public void SetVal(string value)
    {
        Copy(Encoding.UTF8.GetBytes(value), _data);
    }
    public void SetVal(byte[] value)
    {
        Copy(value, _data);
    }
    private void Copy(byte[] source, byte[] target)
    {
        CopyArray(source, target, source.Length);
        this.Size = source.Length;
    }
    private static void CopyArray(byte[] source, byte[] target, int len)
    {
        if (source.Length > SIZE)
            throw new OverflowException();
        Array.Copy(source, target, len);
    }
}
