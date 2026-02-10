using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndexModule;
public class BTreeIndex : BNodeManager, IEnumerable
{
    public BNode? _root;
    public BTreeIndex(int size, IList<BNode> memory, Types key, Types value) : base(size, memory, key, value)
    {
        if (memory.Count == 0)
        {
            _root = CreateNode();
            _root.isRoot = true;
            Update(_root);
        }
        else
        {
            UpdateRoot(memory);
        }
    }

    private void FixRoot()
    {
        if (this._root is not null && !_mem[this._root.Address].isRoot)
            UpdateRoot(_mem);
    }
    private void UpdateRoot(IList<BNode> memory)
    {
        foreach (BNode node in memory)
        {
            if (!node.isRoot)
                continue;
            _root = node;
            break;
        }

        if (_root is null)
            throw new ApplicationException("The index file is probably damaged");
    }

    public AData? Search(AData key)
    {
        FixRoot();
        return Find(key, out _, out _)?.Value;
    }
    private Element? Find(AData key, out BNode node, out List<BNode> parents)
    {
        Element searchElem = new Element(key, key);
        BNode p = _root!;
        int pos = 0;
        parents = new List<BNode>();

        while (!p.IsLeaf)
        {
            node = p;
            pos = GetPosition(p, searchElem.Key);

            if (p[pos] is null)
                return null;

            if (p[pos]!.Equals(searchElem))
                return p[pos];

            if (p[pos]!.Links[1].Equals(-1))
                return null;

            parents.Add(p);
            p = (searchElem < p[pos]!) ? _mem[p[pos]!.Links[0]] : p = _mem[p[pos]!.Links[1]];
        }

        node = p;
        pos = p.Search(searchElem.Key);
        if (pos < 0) return null;
        return p[pos];
    }
    public bool Insert(AData key, AData value)
    {
        if (Search(key) is not null)
            return false;

        Element newElement = new Element(key, value);
        
        if(_root!.Count == _root.Max)
        {
            var newRoot = CreateNode();
            newRoot.isRoot = true;
            _root.isRoot = false;
            BNode newNode = Split(newRoot, _root);
            newNode.IsLeaf = _root.IsLeaf;
            _root = newRoot;
            Update(newNode);
            InsertIntoLeaf(newElement);
        }
        else
        {
            InsertIntoLeaf(newElement);
        }

        return true;
    }

    private void InsertIntoLeaf(Element newElement)
    {
        BNode p = _root!;
        BNode parent = p;

        while (!p.IsLeaf)
        {
            parent = p;

            int pos = GetPosition(p, newElement.Key);

            if (p[pos] is null || p[pos]!.Links[1].Equals(-1))
            {
                p.IsLeaf = true;
                p.Add(newElement);
                Update(p);
                return;
            }

            p = (newElement < p[pos]!) ? _mem[p[pos]!.Links[0]] : p = _mem[p[pos]!.Links[1]];

            if (p.Count == p.Max)
            {
                var newNode = Split(parent, p);
                newNode.IsLeaf = true;
                p.IsLeaf = true;
                parent.IsLeaf = false;
                p = parent;
                Update(parent);
                Update(newNode);
            }
        }

        p.Add(newElement);
        Update(p);
    }

    public bool Remove(AData key)
    {
        FixRoot();
        Element? el = Find(key, out BNode node, out List<BNode> parents);
        if (el is null)
            return false;

        if (node.IsLeaf)
        {
            RemoveFromLeaf(key, parents, node);
            return true;
        }
        else
        {
            parents.Add(node);
            BNode next = _mem[ el.Links[0] ];
            while (!next.IsLeaf)
            {
                parents.Add(next);
                next = _mem[next[next.LastIndex()]!.Links[1]];
            }
            Swap(el, next[next.LastIndex()]!);

            RemoveFromLeaf(next[next.LastIndex()]!.Key, parents, next);
        }

        return true;
    }
    private void RemoveFromLeaf(AData key, List<BNode> parents, BNode node)
    {
        if(node == this._root!)
        {
            node.Remove(key);
            Update(node);
        }
        else if (node != this._root! && node.Count >= node.Min + 1)
        {
            node.Remove(key);
            Update(node);
        }
        else
        {
            var parent = parents.Last();

            if (node.Count > node.Min)
                return;

            if (!Rotate(parent, node))
                Merge(parent, node);

            node.Remove(key);

            if (parent.Count < parent.Min)
                FixUp(parents, node);

            Update(node);
            Update(parent);
        }
    }

    private void FixUp(List<BNode> parents, BNode node)
    {
        if(parents.Count == 1 && parents.First().Count == 0)
        {
            node.isRoot = true;
            parents.First().isRoot = false;
            Remove(parents.First());

            Update(node);
            Update(parents.First());
            return;
        }

        for (int i = parents.Count - 1; i > 0 ; i--)
        {
            if (parents[i].Count > parents[i].Min)
                return;

            if (!Rotate(parents[i - 1], parents[i]))
                Merge(parents[i - 1], parents[i]);

            if(parents[i - 1].isRoot && parents[i - 1].Count == 0)
            {
                parents[i].isRoot = true;
                parents[i - 1].isRoot = false;
                Remove(parents[i - 1]);
            }

            Update(parents[i - 1]);
            Update(parents[i]);
        }

    }

    public class Enumerator : IEnumerator
    {
        private BNode _root;
        private BNodeManager _manager;
        private Stack<Pair<BNode, int>> _stack = new Stack<Pair<BNode, int>>();
        private Element? _current;
        public object? Current => _current;

        public Enumerator(BNode root, BNodeManager manager)
        {
            this._root = root;
            this._manager = manager;
            _stack.Push(new Pair<BNode, int>(root, 0));
        }
        public bool MoveNext()
        {
            if (_stack.Count == 0)
                return false;

            Pair<BNode, int> el = _stack.Peek();
            while (el.Item2 >= el.Item1.Count)
            {
                _stack.Pop();
                if(_stack.Count == 0)
                    return false;
                el = _stack.Peek();
            }

            if (!el.Item1.IsLeaf && el.Item2 == 0)
            {
                int i;
                for (i = 0; i < el.Item1.Count; i++)
                {
                    _stack.Push(new Pair<BNode, int>(_manager.Get(el.Item1[i]!.Links[0]), 0));
                }
                _stack.Push(new Pair<BNode, int>(_manager.Get(el.Item1[i-1]!.Links[1]), 0));
            }
            _current = el.Item1[el.Item2++];
            return true;
        }

        public void Reset()
        {
            _stack.Clear();
            _stack.Push(new Pair<BNode, int>(_root, 0));
        }
        private class Pair<T1, T2>(T1 item1, T2 item2)
        {
            public T1 Item1 { get; set; } = item1;
            public T2 Item2 { get; set; } = item2;
        }
    }
    public IEnumerator GetEnumerator()
    {
        return new Enumerator(this._root, this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
