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
    protected void Merge(BNode parent, BNode child)
    {
        int pos = parent.BinaryFind(child[0]!.Key);
        if (parent[pos] is null)
            --pos;

        if(_mem[parent[pos]!.Links[0]] == child)
        {
            if (_mem[parent[pos]!.Links[1]].Count + child.Count < parent.Max)
                MergeRight(parent, child, pos);
            else if (pos > 0 && _mem[parent[pos - 1]!.Links[0]].Count + child.Count < parent.Max)
                MergeLeft(parent, child, pos - 1);
            else
                throw new ApplicationException("Merge error");
        }
        else
        {
            if(_mem[parent[pos]!.Links[0]].Count + child.Count < parent.Max)
                MergeLeft(parent, child, pos);
            else
                throw new ApplicationException("Merge error");
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

    protected void Update(BNode node)
    {
        if (this._mem is Updatable<BNode> updatable)
            updatable.Update(node);
    }
}
