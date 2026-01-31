using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public enum Types : short
{
    UNKNOWN = -1,
    STRING32 = 0,
    INTEGER = 1
}

public class TypeFactory
{
    public static AData Create(Types type) => type switch
    {
        Types.INTEGER => new Integer(),
        Types.STRING32 => new String32(),
        _ => throw new ArgumentException("Unknown type, create type error")
    };
}
