using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkModule;

public class WaitableDict<TKey, TValue>() : IDictionary<TKey, TValue> where TKey : notnull
{
    private Dictionary<TKey, TValue> _dict = new Dictionary<TKey, TValue>();
    private object _locker = new object();

    public TValue this[TKey key]
    {
        get
        {
            lock (_locker) { return _dict[key]; }
        }
        set
        {
            lock (_locker) { _dict[key] = value; }
        }
    }

    public ICollection<TKey> Keys
    {
        get
        {
            lock (_locker)
            {
                return _dict.Keys;
            }
        }
    }

    public ICollection<TValue> Values
    {
        get
        {
            lock (_locker)
            {
                return _dict.Values;
            }
        }
    }

    public int Count {
        get
        {
            lock (_locker)
            {
                return _dict.Count;
            }
        }
    }

    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
    {
        lock (_locker)
        {
            if(!_dict.ContainsKey(key))
                _dict.Add(key, value);
        }
    }

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        lock (_locker)
        {
            if (!_dict.ContainsKey(item.Key))
                _dict.Add(item.Key, item.Value);
        }
    }

    public void Clear()
    {
        lock (_locker)
        {
            _dict.Clear();
        }
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        lock (_locker)
        {
            if (_dict.TryGetValue(item.Key, out var value))
            {
                if (value == null && item.Value == null)
                    return true;
                else if (value == null || item.Value == null)
                    return false;
                else if(value.Equals(item.Value))
                    return true;

                return false;
            }
            else
                return false;
        }
    }

    public bool ContainsKey(TKey key)
    {
        lock (_locker)
        {
            return _dict.ContainsKey(key);
        }
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        lock (_locker)
        {
            for(int i = 0; i < _dict.Count && i < arrayIndex; i++)
            {
                array[i] = _dict.ElementAt(i);
            }
        }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return new WaitableEnumerator(_dict, this._locker);
    }

    public bool Remove(TKey key)
    {
        lock (_locker)
        {
            return _dict.Remove(key);
        }
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        bool isExist = Contains(item);
        lock (_locker)
        {
            if (!isExist)
                return false;

            _dict.Remove(item.Key);
            return true;
        }
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        lock (_locker)
        {
            return _dict.TryGetValue(key, out value);
        }
    }

    public bool TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        lock (_locker)
        {
            return _dict.Remove(key, out value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private class WaitableEnumerator(Dictionary<TKey,TValue> dict, object locker) : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly Dictionary<TKey, TValue> _localDictCopy = dict;
        private readonly object _locker = locker;
        private int _current = -1;
        public KeyValuePair<TKey, TValue> Current
        {
            get
            {
                lock (_locker)
                {
                    return _localDictCopy.ElementAt(_current);
                }
            }
        }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            
        }

        public bool MoveNext()
        {
            lock (_locker)
            {
                if (_current < _localDictCopy.Count - 1)
                {
                    ++_current;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public void Reset()
        {
            lock (_locker)
            {
                _current = -1;
            }
        }
    }
}
