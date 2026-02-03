using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyModule;
public interface IAuth
{
    public object? Init(HttpRequest req);
    public object? Valid(HttpRequest req);
    public object? Next(HttpRequest req);
    public bool IsEnd();
}
