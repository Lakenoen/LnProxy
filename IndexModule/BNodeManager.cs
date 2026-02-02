using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class BNodeManager
{
    protected readonly IList<BNode> _mem;

    private int _index = 0;

    protected int T { get; init; }

    public IList<BNode> Memory => _mem;

    protected (Types key, Types value) _types;
    public BNodeManager(int t, IList<BNode> memory, Types key, Types value)
    {
        _types = (key,value);
        this._mem = memory;
        this.T = t;
        _index = memory.Count;
    }
    public BNode CreateNode()
    {
        var node = new BNode(T, _index++, _types.key, _types.value);
        _mem.Add(node);
        return node;
    }
    public BNode Get(int index)
    {
        return _mem[index];
    }
    protected void Remove(BNode elem)
    {
        elem.Dead = true;
    }
    private void MergeRight(BNode parent, BNode child, int pos)
    {
        Element mid = parent[pos]!;
        BNode sibling = _mem[mid.Links[1]];

        child.Add(sibling.GetRangeElements(0, sibling.Count));
        int insertPos = child.Add(mid);
        mid.Links[0] = (insertPos > 0) ? child[insertPos - 1]!.Links[1] : -1;
        mid.Links[1] = (insertPos < child.Count) ? child[insertPos + 1]!.Links[0] : -1;

        if (pos + 1 < parent.Count && parent[pos + 1] is not null)
            parent[pos + 1]!.Links[0] = child;

        parent.Remove(pos);
        Remove(sibling);

        Update(sibling);
        Update(parent);
        Update(child);
    }

    private void MergeLeft(BNode parent, BNode child, int pos)
    {
        Element mid = parent[pos]!;
        BNode sibling = _mem[mid.Links[0]];

        child.Add(sibling.GetRangeElements(0, sibling.Count));
        int insertPos = child.Add(mid);
        mid.Links[0] = child[insertPos - 1]!.Links[1];
        mid.Links[1] = child[insertPos + 1]!.Links[0];

        if (pos - 1 >= 0 && parent[pos - 1] is not null)
            parent[pos - 1]!.Links[1] = child;

        parent.Remove(pos);
        Remove(sibling);

        Update(sibling);
        Update(parent);
        Update(child);
    }
    protected bool Merge(BNode parent, BNode child)
    {
        var right = GetRightSibling(parent, child, out var locationR);
        var left = GetLeftSibling(parent, child, out var locationL);

        if (right is not null && right.Count + child.Count < parent.Max)
        {
            MergeRight(parent, child, locationR.pos);
            return true;
        }
        else if (left is not null && left.Count + child.Count < parent.Max)
        {
            MergeLeft(parent, child, locationL.pos);
            return true;
        }
        else
        {
            return false;
        }
    }
    protected BNode Split(BNode parent, BNode node)
    {
        Element? mid = node.GetMidElem();
        if (mid is null)
            throw new ApplicationException("Split error");

        BNode newNode = this.CreateNode();

        newNode.Add(node.GetRangeElements(node.GetMidIndex() + 1, node.LastIndex()));
        node.Remove(node.GetMidIndex(), node.LastIndex());

        mid.Links[0] = node;
        mid.Links[1] = newNode;

        int pos = parent.Add(mid);

        if (pos + 1 < parent.Count && parent[pos + 1] is not null)
        {
            parent[pos + 1]!.Links[0] = newNode;
        }

        Update(parent);
        Update(node);
        Update(newNode);

        return newNode;
    }

    protected int GetPosition(BNode parent, AData key)
    {
        if (parent.Count == 0)
            return 0;

        int pos = parent.BinaryFind(key);
        if (parent[pos] is null)
            --pos;

        if (pos < 0)
            throw new ApplicationException("BinaryFind error");

        return pos;
    }
    protected (int pos, int link) GetPosition(BNode parent, BNode node)
    {
        if (node.Count == 0)
            throw new ArgumentException("Node is empty");

        int pos =  GetPosition(parent, node[0]!.Key);

        int link = 0;
        if (parent[pos]!.Links[0] == node.Address)
            link = 0;
        else if (parent[pos]!.Links[1] == node.Address)
            link = 1;

        return (pos, link);
    }

    protected BNode? GetRightSibling(BNode parent, BNode node, out (int pos, int link) location)
    {
        location = GetPosition(parent, node);

        if (location.pos + 1 >= parent.Count && location.link == 1)
            return null;

        if (location.link == 0)
        {
            location = (location.pos, 0);
            return _mem[parent[location.pos]!.Links[1]];
        }
        else if (location.link == 1 && location.pos + 1 < parent.Count)
        {
            location = (location.pos + 1, 0);
            return _mem[parent[location.pos + 1]!.Links[1]];
        }

        return null;
    }

    protected BNode? GetLeftSibling(BNode parent, BNode node, out (int pos, int link) location)
    {
        location = GetPosition(parent, node);

        if (location.pos - 1 < 0 && location.link == 0)
            return null;

        if (location.link == 1)
        {
            location = (location.pos, 0);
            return _mem[parent[location.pos]!.Links[0]];
        }
        else if (location.link == 0 && location.pos - 1 > 0)
        {
            location = (location.pos - 1, 1);
            return _mem[parent[location.pos - 1]!.Links[0]];
        }

        return null;
    }
    protected bool Rotate(BNode parent, BNode node)
    {
        bool res = false;

        var left = GetLeftSibling(parent, node, out var locationL);
        var right = GetRightSibling(parent, node, out var locationR);

        if(left is not null && left.Count > left.Min)
        {
            Element clone = (Element)parent[locationL.pos]!.Clone();
            clone.Links[0] = node[0]!.Links[0];
            clone.Links[1] = node[0]!.Links[1];
            node.Add(clone);

            Swap(parent[locationL.pos]!, left[left.LastIndex()]!);
            left.Remove(left.LastIndex());

            Update(left);
            res = true;
        }
        else if(right is not null && right.Count > right.Min)
        {
            Element clone = (Element)parent[locationR.pos]!.Clone();
            clone.Links[0] = node[node.LastIndex()]!.Links[0];
            clone.Links[1] = node[node.LastIndex()]!.Links[1];
            node.Add(clone);

            Swap(parent[locationR.pos]!, right[0]!);
            right.Remove(0);

            Update(right);
            res = true;
        }

        Update(parent);
        Update(node);
        return res;
    }

    protected void Swap(Element el1, Element el2)
    {
        var tempKey = el1.Key;
        el1.Key = el2.Key;
        el2.Key = tempKey;

        var tempVal = el1.Value;
        el1.Value = el2.Value;
        el2.Value = tempVal;
    }

    protected void Update(BNode node)
    {
        if (this._mem is Updatable<BNode> updatable)
            updatable.Update(node);
    }
}
