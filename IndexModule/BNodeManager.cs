using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class BNodeManager(int t)
{
    private readonly List<BNode> _mem = new List<BNode>();
    private int _index = 0;
    private int T { get; init; } = t;
    public BNode CreateNode()
    {
        var node = new BNode(T, _index++);
        _mem.Add(node);
        return node;
    }

    public void Split(BNode parent, BNode node)
    {
        Element? mid = node.GetMidElem();
        if (mid is null)
            throw new ApplicationException("Split error");

        BNode newNode = this.CreateNode();

        newNode.Add(node.GetRangeElements(node.GetMidIndex(), node.LastIndex()));
        node.Remove(node.GetMidIndex(), node.LastIndex());

        mid.Links[0] = node;
        mid.Links[1] = newNode;

        //TODO
        parent.Add(mid);
    }
}
