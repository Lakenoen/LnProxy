using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class FileIndex<T> : Updatable<T>, IList<T> where T : Serialilzable
{
    private FileStream _stream;

    private BinaryWriter _writer;

    private BinaryReader _reader;

    private int _blockSize = 0;

    private int _count = 0;

    private int startOffset = sizeof(int);

    public int Count => _count;

    public bool IsReadOnly => false;

    public T this[int index] { 
        get => (T)Get(index);
        set => Set(index, value);
    }

    public FileIndex(string path, int blockSize)
    {
        _blockSize = blockSize;
        _stream = new FileStream(path,FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        _writer = new BinaryWriter(_stream);
        _reader = new BinaryReader(_stream);
        if (_stream.Length == 0)
            _writer.Write(0);
        else
            _count = _reader.ReadInt32();
    }
    private void MoveTo(int index)
    {
        _stream.Seek(index * _blockSize + startOffset, SeekOrigin.Begin);
    }
    private Serialilzable Get(int index)
    {
        MoveTo(index);
        return new BNode(3,-1, Types.UNKNOWN, Types.UNKNOWN).FromByteArray(_reader.ReadBytes(_blockSize));
    }
    private void Set(int index, Serialilzable node)
    {
        MoveTo(index);
        _writer.Write(node.ToByteArray());
        _stream.Flush();
    }

    public int IndexOf(T item)
    {
        for(int i = 0; i < _count; i++)
            if (this[i].Equals(item))
                return i;
        return -1;
    }

    public void Insert(int index, T item)
    {
        if(index > _count)
            throw new IndexOutOfRangeException();

        for (int i = index; i < _count; i++)
            this[i + 1] = this[i];

        Set(index, item);
        ++_count;
    }

    public void RemoveAt(int index)
    {
        if (index >= _count)
            throw new IndexOutOfRangeException();

        for (int i = index; i < _count - 1; i++)
            this[i] = this[i + 1];

        --_count;
        Cut(_count);
    }

    private void Cut(int newSize)
    {
        this._stream.SetLength(newSize * this._blockSize + this.startOffset);
    }
    public void Add(T item)
    {
        Set(_count++, item);
    }

    public void Clear()
    {
        for( int i = 0; i < _count; ++i )
            RemoveAt(i);
    }

    public bool Contains(T item)
    {
        int pos = this.IndexOf(item);
        return (pos >= 0) ? true : false;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        for (int i = arrayIndex; i < _count; ++i)
            array[i] = (T)this[i].Clone();
    }

    public bool Remove(T item)
    {
        int pos = this.IndexOf(item);
        if(pos < 0)
            return false;
        RemoveAt(pos);
        return true;
    }

    public void Update(T item)
    {
        Set((item as BNode).Address, item);
    }

    public class FileEnumerator<T> : IEnumerator<T> where T : Serialilzable
    {
        private FileIndex<T> _values;
        private int _index = -1;
        public FileEnumerator(FileIndex<T> values)
        {
            _values = values;
        }
        public T Current => _values[this._index];

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            
        }

        public bool MoveNext()
        {
            if (_index < _values.Count - 1)
            {
                ++this._index;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Reset()
        {
            _index = -1;
        }
    }
    public IEnumerator<T> GetEnumerator()
    {
        return new FileEnumerator<T>(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
