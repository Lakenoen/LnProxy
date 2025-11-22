using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ProxyLib;

public class Empty() { }
public abstract class AbstractResult<T>()
{
    public T? Item { get; init; }
    public string Message = string.Empty;
    public AbstractResult(T item) : this(item, string.Empty) { }
    public AbstractResult(string message) : this(default, message) { }
    public AbstractResult(T? item, string message) : this()
    {
        Item = item;
        this.Message = message;
    }
    public abstract bool IsError { get; }
}
