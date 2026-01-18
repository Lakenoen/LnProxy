using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class BTreeIndex : BNodeManager
{
    private BNode _root;
    public BTreeIndex(int size) : base(size)
    {
        _root = CreateNode();
    }

    public AData? Search(AData key)
    {
        return Find(key)?.Value;
    }
    private Element? Find(AData key)
    {
        Element searchElem = new Element(key, key);
        BNode p = _root;
        int pos = 0;
        while (!p.IsLeaf)
        {
            pos = p.BinaryFind(searchElem);
            if (p[pos] is null && pos != 0)
                --pos;

            if (p[pos] is null)
                return null;

            if (p[pos]!.Equals(searchElem))
                return p[pos];

            if (p[pos]!.Links[1].Equals(-1))
                return null;

            p = (searchElem < p[pos]!) ? _mem[p[pos]!.Links[0]] : p = _mem[p[pos]!.Links[1]];
        }

        pos = p.Search(searchElem);
        if (pos < 0) return null;
        return p[pos];
    }
    public void Insert(AData key, AData value)
    {
        Element newElement = new Element(key, value);
        
        if(_root.Count == _root.Max)
        {
            var newRoot = CreateNode();
            Split(newRoot, _root);
            _root = newRoot;
            InsertIntoLeaf(newElement);
        }
        else
        {
            InsertIntoLeaf(newElement);
        }

    }

    private void InsertIntoLeaf(Element newElement)
    {
        BNode p = _root;
        BNode parent = p;

        while (!p.IsLeaf)
        {
            parent = p;

            int pos = p.BinaryFind(newElement);
            if (p[pos] is null && pos != 0)
                --pos;

            if (p[pos] is null || p[pos]!.Links[1].Equals(-1))
            {
                p.IsLeaf = true;
                p.Add(newElement);
                return;
            }

            p = (newElement < p[pos]!) ? _mem[p[pos]!.Links[0]] : p = _mem[p[pos]!.Links[1]];

            if (p.Count == p.Max)
            {
                Split(parent, p).IsLeaf = true;
                p.IsLeaf = true;
                parent.IsLeaf = false;
                p = parent;
            }
        }

        p.Add(newElement);
    }
}
