using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IndexModule;

namespace Ureg
{
    public class Methods : IDisposable
    {
        private static readonly Mutex _mutex = new Mutex(false, "SharedIndexMutex");
        protected readonly FileIndex<BNode> _file;
        protected readonly BTreeIndex _index;
        private readonly string _path = string.Empty;
        public Methods(string path)
        {
            this._path = path;
            this._file = new FileIndex<BNode>(path, new BNode(2, -1, Types.STRING32, Types.STRING32).Size);
            this._index = new BTreeIndex(2, _file, Types.STRING32, Types.STRING32);
        }
        public void Create(params string[] args)
        {
            _mutex.WaitOne();
            try
            {
                if (args.Length != 2)
                    throw new ArgumentException("Must be 2 parameters");

                if (args[0].Length > 10)
                    throw new ArgumentException("Login must be less then 11 characters");

                if (args[0].Length > 10 && args[0].Length < 4)
                    throw new ArgumentException("Password must be less then 11 characters and more then 3");

                if (!_index.Insert((String32)args[0], (String32)args[1]))
                    throw new ApplicationException("Login already exists");
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }
        public void Remove(params string[] args)
        {
            _mutex.WaitOne();
            try
            {
                if (args.Length != 1)
                    throw new ArgumentException("Must be 1 parameters");

                if(!_index.Remove((String32)args[0]))
                    throw new ApplicationException("Can't find login");
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        public string Search(params string[] args)
        {
            _mutex.WaitOne();
            try
            {
                if (args.Length != 1)
                    throw new ArgumentException("Must be 1 parameters");

                String32? elem = (String32?)_index.Search((String32)args[0]);

                if (elem == null)
                    throw new ApplicationException("There is no such login");

                return (string)elem;
            }
            finally{
                _mutex.ReleaseMutex();
            }
        }
        public List<(string key, string value)> GetAll()
        {
            _mutex.WaitOne();
            try
            {
                List<(string key, string value)> result = new List<(string key, string value)>();
                foreach (Element el in _index)
                {
                    result.Add(((string)(String32)el.Key, (string)(String32)el.Value));
                }

                if (result.Count == 0)
                    throw new ApplicationException("Index is empty");

                return result;
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        public void ClearDeletedNote()
        {
            _mutex.WaitOne();
            try
            {
                for (int i = 0; i < _file.Count; i++)
                {
                    if (!_file[i].Dead)
                        continue;

                    _file.RemoveAt(i);

                    for(int j = i; j < _file.Count; j++)
                    {
                        var copy = (BNode)_file[j].Clone();
                        copy.Address -= 1;
                        _file[j] = copy;
                    }

                    for (int j = 0; j < _file.Count; j++)
                    {
                        var n = _file[j];
                        for(int k = 0; k < n.Count; k++)
                        {
                            if (n[k]!.Links[0] > i)
                                n[k]!.Links[0] -= 1;
                            if (n[k]!.Links[1] > i)
                                n[k]!.Links[1] -= 1;
                            _file.Update(n);
                        }
                    }
                }
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        public void Dispose()
        {
            try
            {
                _mutex.ReleaseMutex();
                _mutex.Close();
                _file.Dispose();
            }
            catch { }
        }
    }
}
