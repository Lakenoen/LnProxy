using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class BNodeManager(int t)
{
    protected readonly List<BNode> _mem = new List<BNode>();
    protected readonly List<int> _removed = new List<int>();
    private int _index = 0;
    protected int T { get; init; } = t;
    public BNode CreateNode()
    {
        var node = new BNode(T, _index++);
        _mem.Add(node);
        return node;
    }
    public BNode Get(int addr) => _mem[addr];
    public void Remove(BNode elem)
    {
        _removed.Add(elem.Address);
    }
    public void Remove(int address)
    {
        _removed.Add(address);
    }
    private void MergeRight(BNode parent, BNode child, int pos)
    {
        Element mid = parent[pos]!;
        BNode sibling = _mem[mid.Links[1]];

        child.Add(sibling.GetRangeElements(0, sibling.Count));
        int insertPos = child.Add(mid);
        mid.Links[0] = child[insertPos - 1]!.Links[1];
        mid.Links[1] = child[insertPos + 1]!.Links[0];

        if (pos + 1 < parent.Count && parent[pos + 1] is not null)
            parent[pos + 1]!.Links[0] = child;

        parent.Remove(pos);
        Remove(sibling);
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
    }
    public void Merge(BNode parent, BNode child)
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
    public BNode Split(BNode parent, BNode node)
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

        return newNode;
    }
}
