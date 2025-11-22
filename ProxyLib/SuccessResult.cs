using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyLib;
public class SuccessResult<T> : AbstractResult<T>
{
    public SuccessResult() : base() { }
    public SuccessResult(T item) : base(item) { }
    public SuccessResult(string message) : base(message) { }
    public SuccessResult(T? item, string message) : base() { }
    public override bool IsError => false;
}
