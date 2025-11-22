using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyLib;
public class ErrorResult<T> : AbstractResult<T>
{
    public ErrorResult() : base() { }
    public ErrorResult(T item) : base(item) { }
    public ErrorResult(string message) : base(message) { }
    public ErrorResult(T? item, string message) : base() { }
    public override bool IsError => true;
}
