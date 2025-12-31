using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class BNode
{
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
            AddWithoutSort(e);
        Array.Sort(this._values, 0, Count);
    }

    public Element[] GetRangeElements(int start, int end) => new Memory<Element>(_values!, start, end).ToArray();
    public void Fill(Element? el, int start, int end)
    {
        for(int i = start; i <= end; i++)
            _values![i] = el;
    }
    public void Add(Element el)
    {
        AddWithoutSort(el);
        Array.Sort(this._values,0, Count);
    }
    private void AddWithoutSort(Element el)
    {
        if (Count + 1 > Max)
            throw new IndexOutOfRangeException();

        _values[Count] = el;
        ++Count;
    }
    public void Insert(Element el, int i)
    {
        if (i > LastIndex() || Count + 1 > Max)
            throw new IndexOutOfRangeException();

        for(int j = Count; j > i; --j)
        {
            _values[j] = _values[j - 1];
        }
        _values[i] = el;
        ++Count;
    }

    public (int link, int pos) Search(BNode child) {
        int link = -1;
        int pos = -1;

        //TODO

        return (link, pos);
    }
    public void Remove(int start, int end)
    {
        for (int i = start; i <= end; i++)
            Remove(i);
    }
    public void Remove(int i)
    {
        if (i > LastIndex() || Count - 1 < Min)
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
        return LastIndex() / 2 + 1;
    }
    public Element? GetMidElem()
    {
        return _values[GetMidIndex()];
    }
    public int LastIndex() => Count - 1;

}
