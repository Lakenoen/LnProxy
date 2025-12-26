using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyModule;
public class Ref<T>()
{
    public T? Value { get; set; } = default(T);

    public Ref(T val) : this()
    {
        this.Value = val;
    }

    public static implicit operator Ref<T>(T param)
    {
        return new Ref<T>(param);
    }

    public static implicit operator T?(Ref<T> param)
    {
        return param.Value;
    }

}
