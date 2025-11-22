using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyLib;
public interface IExecutable : IDisposable
{
    public Task<AbstractResult<Empty>> RunAsync();
    public void Stop();
}
