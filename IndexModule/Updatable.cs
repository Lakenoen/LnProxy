using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule
{
    internal interface Updatable<T> where T : Serialilzable
    {
        public void Update(T item);
    }
}
