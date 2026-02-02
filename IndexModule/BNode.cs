using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class BNode : Serialilzable
{
    public bool isRoot { get; set; } = false;
    public bool Dead { get; set; } = false;
    public bool IsLeaf { get; set; } = false;
    public int Max { get; private set; }
    public int Min { get; private set; }
    public int T { get; private set; }
    private Element?[] _values;
    public int Count { get; private set; } = 0;
    public int Address { get; set; } = 0;
    public int Size => this.ToByteArray().Length;
    public (Types key, Types value) Types { get; private set; }

    public static implicit operator int(BNode param)
    {
        return param.Address;
    }

    public static bool operator==(BNode param1, BNode param2)
    {
        return param1.Address == param2.Address;
    }
    public static bool operator !=(BNode param1, BNode param2)
    {
        return param1.Address != param2.Address;
    }

    public override bool Equals(object? obj)
    {
        if(obj == null)
            return false;

        if(obj is not BNode node)
            return false;

        bool firstCheck = (this.T == node.T 
            && this.IsLeaf.Equals(node.IsLeaf) 
            && this.isRoot.Equals(node.isRoot) 
            && this.Dead.Equals(node.Dead) 
            && this.Count.Equals(node.Count)
            );

        bool secondCheck = true;
        for (int i = 0; i < this.Count; ++i)
        {
            if (this[i]!.Equals(node[i]))
                continue;
            secondCheck = false;
            break;
        }

        return secondCheck && firstCheck;
    }

    public Element? this[int i]
    {
        get => _values[i];
        set => _values[i] = value;
    }

    public BNode(int t, int address, Types key, Types value)
    {
        this.Types = (key, value);
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

    public byte[] ToByteArray()
    {
        using MemoryStream stream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(stream);
        writer.Write(this.Count);
        writer.Write(this.Dead);
        writer.Write(this.T);
        writer.Write(this.Address);
        writer.Write(this.isRoot);
        writer.Write(this.IsLeaf);

        writer.Write( (short)this.Types.key );

        writer.Write( (short)this.Types.value );

        for (int i = 0; i  < this.Count; ++i)
        {
            writer.Write(this[i]!.ToByteArray());
        }

        if( this.Count < this.Max)
        {
            var diff = this.Max - this.Count;
            var el = new Element(TypeFactory.Create(this.Types.key), TypeFactory.Create(this.Types.value));
            writer.Write(new byte[el.Size * diff]);
        }

        stream.Flush();
        return stream.ToArray();
    }

    public Serialilzable FromByteArray(byte[] data)
    {
        using MemoryStream stream = new MemoryStream(data);
        using BinaryReader reader = new BinaryReader(stream);
        this.Count = reader.ReadInt32();
        this.Dead = reader.ReadBoolean();
        this.T = reader.ReadInt32();
        this.Address = reader.ReadInt32();
        this.isRoot = reader.ReadBoolean();
        this.IsLeaf = reader.ReadBoolean();

        this.Max = 2 * this.T - 1;
        this.Min = this.T - 1;

        this.Types = ((Types)reader.ReadInt16(), (Types)reader.ReadInt16());

        this._values = new Element?[this.Max];
        for (int i = 0; i < this.Count; ++i)
        {
            this._values[i] = new Element(TypeFactory.Create(this.Types.key), TypeFactory.Create(this.Types.value));
            this._values[i]!.FromByteArray( reader.ReadBytes(this._values[i]!.Size) );
        }

        return this;
    }

    public object Clone()
    {
        var result = new BNode(this.T, this.Address, this.Types.key, this.Types.value);
        result.Dead = this.Dead;
        result.Count = this.Count;
        result.IsLeaf = this.IsLeaf;
        result.isRoot = this.isRoot;
        
        for( int i = 0; i < this.Count; ++i )
            result[i] = (Element)this[i]!.Clone();

        return result;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(this.T);
        hash.Add(this.isRoot);
        hash.Add(this.Count);
        hash.Add(this.IsLeaf);
        hash.Add(this.Dead);

        for (int i = 0; i < this.Count; ++i)
            hash.Add(this[i]);

        return hash.ToHashCode();
    }

}
