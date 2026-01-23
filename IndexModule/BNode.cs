using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class BNode
{
    public bool Dead { get; set; } = false;
    public bool IsLeaf { get; set; } = false;
    public int Max { get; init; }
    public int Min { get; init; }
    public int T {  get; init; }
    private readonly Element?[] _values;
    public int Count { get; private set; } = 0;
    public int Address { get; private set; } = 0;

    public static implicit operator int(BNode param)
    {
        return param.Address;
    }
    public Element? this[int i]
    {
        get => _values[i];
        set => _values[i] = value;
    }
    public BNode(int t, int address)
    {
        this.Address = address;
        this.T = t;
        this.Max = 2 * t - 1;
        this.Min = t - 1;
        _values = new Element[this.Max];
    }
    public void Add(Element[] elems)
    {
        foreach (Element e in elems)
            if(e is not null)
                Add(e);
    }

    public Element[] GetRangeElements(int start, int end) => new Memory<Element>(_values!, start, end - start + 1).ToArray();
    public void Fill(Element? el, int start, int end)
    {
        for(int i = start; i <= end; i++)
            _values![i] = el;
    }
    public int Add(Element el)
    {
        int newPos = BinaryFind(el.Key);
        return Insert(el, newPos);
    }
    public void Sort() => Array.Sort(this._values, 0, Count);
    public int Insert(Element el, int i)
    {
        if (Count + 1 > Max)
            throw new IndexOutOfRangeException();

        for(int j = Count; j > i; --j)
        {
            _values[j] = _values[j - 1];
        }
        _values[i] = el;
        ++Count;
        return i;
    }
    public int Search(AData key)
    {
        int pos = BinaryFind(key);
        if (this[pos] != null && this[pos]!.Key.Equals(key))
            return pos;
        return -1;
    }
    public int BinaryFind(AData el)
    {
        int mid = 0;
        int left = 0;
        int right = LastIndex();
        int insertPos = Math.Min(this.Count, this._values.Length - 1);

        if (left > right)
            return 0;

        while (left <= right)
        {
            mid = (left + right) / 2;
            if (this[mid] is null)
                return mid;
            else if (el > this[mid]!.Key)
                left = mid + 1;
            else
            {
                right = mid - 1;
                insertPos = mid;
            }
        }

        return insertPos;
    }

    public void Remove(AData key)
    {
        Remove(Search(key));
    }
    public void Remove(int start, int end)
    {
        for (int i = start; i <= end; i++)
            Remove(start);
    }
    public void Remove(int i)
    {
        if (i > LastIndex())
            throw new IndexOutOfRangeException();

        for (int j = i; j < Count - 1; ++j)
        {
            _values[j] = _values[j + 1];
        }
        _values[LastIndex()] = null;
        --Count;
    }
    public int GetMidIndex()
    {
        return LastIndex() / 2;
    }
    public Element? GetMidElem()
    {
        return _values[GetMidIndex()];
    }
    public int LastIndex() => Count - 1;

}
