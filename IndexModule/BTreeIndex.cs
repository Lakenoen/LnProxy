using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class BTreeIndex : BNodeManager
{
    public BNode _root;
    public BTreeIndex(int size) : base(size)
    {
        _root = CreateNode();
    }

    public AData? Search(AData key)
    {
        return Find(key, out _, out _)?.Value;
    }
    private Element? Find(AData key, out BNode node, out BNode parent)
    {
        Element searchElem = new Element(key, key);
        BNode p = _root;
        parent = p;
        int pos = 0;

        while (!p.IsLeaf)
        {
            node = p;
            pos = BinaryFindWithoutNull(p, searchElem.Key);

            if (p[pos] is null)
                return null;

            if (p[pos]!.Equals(searchElem))
                return p[pos];

            if (p[pos]!.Links[1].Equals(-1))
                return null;

            parent = p;
            p = (searchElem < p[pos]!) ? _mem[p[pos]!.Links[0]] : p = _mem[p[pos]!.Links[1]];
        }

        node = p;
        pos = p.Search(searchElem.Key);
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

            int pos = BinaryFindWithoutNull(p, newElement.Key);

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

    public void Remove(AData key)
    {
        Element? el = Find(key, out BNode node, out BNode parent);
        if (el is null)
            return;

        if (node.IsLeaf)
        {
            int parentPos = BinaryFindWithoutNull(parent, el.Key);
            RemoveFromLeaf(key, parent, node, parentPos);
            return;
        }
        else
        {
            parent = node;
            BNode next = _mem[ el.Links[0] ];
            while (!next.IsLeaf)
            {
                parent = next;
                next = _mem[next[next.LastIndex()]!.Links[1]];
            }
            Swap(el, next[next.LastIndex()]!);

            int parentPos = BinaryFindWithoutNull(parent, el.Key);
            RemoveFromLeaf(next[next.LastIndex()]!.Key, parent, next, parentPos);
        }
    }
    private void RemoveFromLeaf(AData key, BNode parent, BNode node, int pos)
    {
        if(node == this._root)
        {
            node.Remove(key);
        }
        else if (node != this._root && node.Count >= node.Min + 1)
        {
            node.Remove(key);
        }
        else
        {
            if (_mem[parent[pos]!.Links[0]] == node)
            {
                if (_mem[parent[pos]!.Links[1]].Count > parent.Min)
                    RotateLeft(key, parent, node, pos);
                else if (pos > 0 && _mem[parent[pos - 1]!.Links[0]].Count > parent.Min)
                    RotateRight(key, parent, node, pos - 1);
                else
                {
                    Merge(parent, node);
                    node.Remove(key);
                }
            }
            else
            {
                if (_mem[parent[pos]!.Links[0]].Count > parent.Min)
                    RotateRight(key, parent, node, pos);
                else
                {
                    Merge(parent, node);
                    node.Remove(key);
                }
            }
        }
    }
    private void RotateLeft(AData key, BNode parent, BNode node, int pos)
    {
        Element el = parent[pos]!;

        if (el.Links[1] < 0)
            return;

        BNode right = _mem[el.Links[1]];

        var clone = (Element)el.Clone();
        clone.Links[0] = -1;
        clone.Links[1] = -1;
        node.Add(clone);

        Swap(parent[pos]!, right[0]!);
        right.Remove(0);

        node.Remove(key);
    }
    private void RotateRight(AData key, BNode parent, BNode node, int pos)
    {
        Element el = parent[pos]!;

        if (el.Links[0] < 0)
            return;

        BNode left = _mem[el.Links[0]];

        var clone = (Element)el.Clone();
        clone.Links[0] = -1;
        clone.Links[1] = -1;
        node.Add(clone);

        Swap(parent[pos]!, left[left.LastIndex()]!);
        left.Remove(left.LastIndex());

        node.Remove(key);
    }
    private int BinaryFindWithoutNull(BNode node, AData key)
    {
        int pos = node.BinaryFind(key);
        if (node[pos] is null && pos != 0)
            --pos;
        return pos;
    }
    private void Swap(Element el1, Element el2)
    {
        var tempKey = el1.Key;
        el1.Key = el2.Key;
        el2.Key = tempKey;

        var tempVal = el1.Value;
        el1.Value = el2.Value;
        el2.Value = tempVal;
    }
}
