using System.Diagnostics;
using System.Text;

namespace RaccoonSql.Core.Storage;

public class BPlusTree<TKey, TValue>(int t)
    where TKey : IComparable<TKey>, IEquatable<TKey>
{
    private BPlusTreeNode<TKey, TValue> _root = new BPlusTreeNode<TKey, TValue>(t);

    public void Insert(TKey key, TValue value)
    {
        var newRoot = _root.Insert(key, value);
        if (newRoot != null)
        {
            _root = newRoot;
        }

        //Console.WriteLine(_root);
        _root.CheckConsistency();
    }

    public IEnumerable<TValue> Range(TKey from, TKey to, bool fromExclusive, bool toExclusive)
    {
        var reverse = to.CompareTo(from) < 0;
        foreach (var (key, values) in RangeFrom(from, reverse))
        {
            if (fromExclusive && key.Equals(from)) continue;
            if (toExclusive && key.Equals(to)) break;
            if (!reverse && key.CompareTo(to) > 0) break;
            if (reverse && key.CompareTo(to) < 0) break;
            foreach (var value in values)
            {
                yield return value;
            }
        }
    }

    private IEnumerable<(TKey key, IEnumerable<TValue> values)> RangeFrom(TKey from, bool backwards)
    {
        var leaf = _root.FindLeaf(from);
        int idx;
        if (!backwards)
        {
            // TODO: binary search? this is an ordered list
            idx = leaf.Keys.FindIndex(x => x.CompareTo(from) >= 0);
            if (idx == -1)
            {
                idx = leaf.Keys.Count;
            }
        }
        else
        {
            idx = leaf.Keys.FindLastIndex(x => x.CompareTo(from) <= 0);
        }

        do
        {
            Debug.Assert(leaf.Values != null);
            Debug.Assert(leaf.Keys.Count == leaf.Values.Count);
            if (!backwards)
            {
                for (; idx < leaf.Keys.Count; idx++)
                {
                    yield return (leaf.Keys[idx], leaf.Values[idx]);
                }

                leaf = leaf.NextLeaf;
                idx = 0;
            }
            else
            {
                for (; idx >= 0; idx--)
                {
                    yield return (leaf.Keys[idx], leaf.Values[idx]);
                }

                leaf = leaf.PreviousLeaf;
                idx = leaf?.Keys.Count - 1 ?? 0;
            }
        } while (leaf != null);
    }

    /*public override string ToString()
    {
        return _root.ToString();
    }*/
}

internal class BPlusTreeNode<TKey, TValue>(int t)
    where TKey : IComparable<TKey>, IEquatable<TKey>
{
    private readonly int _t = t;
    private List<BPlusTreeNode<TKey, TValue>>? Children { get; set; }
    public List<TKey> Keys { get; set; } = [];
    public List<List<TValue>>? Values { get; set; } = [];

    private bool IsLeaf => Children == null;

    public BPlusTreeNode<TKey, TValue>? NextLeaf { get; set; }
    public BPlusTreeNode<TKey, TValue>? PreviousLeaf { get; set; }


    /*
    public override string ToString()
    {
        StringBuilder builder = new();
        builder.Append('(');
        if (IsLeaf)
        {
            Debug.Assert(Values != null, nameof(Values) + " != null");
            builder.AppendJoin(", ", Keys.Select((key, i) => $"{key}=({string.Join(",", Values[i])})"));
        }
        else
        {
            Debug.Assert(Children != null, nameof(Children) + " != null");
            for (var i = 0; i < Keys.Count; i++)
            {
                builder.Append(Children[i]);
                builder.Append(',');
                builder.Append(Keys[i]);
                builder.Append(',');
            }

            builder.Append(Children[^1]);
        }

        builder.Append(')');
        return builder.ToString();
    }
    */

    public BPlusTreeNode<TKey, TValue>? Insert(TKey key, TValue value)
    {
        var idx = Keys.FindLastIndex(x => key.CompareTo(x) >= 0);

        if (IsLeaf)
        {
            Debug.Assert(Values != null);
            if (idx != -1 && Keys[idx].Equals(key))
            {
                Values[idx].Add(value);
                return null;
            }

            Keys.Insert(idx + 1, key);
            Values.Insert(idx + 1, [value]);
        }
        else
        {
            var promoted = Children![idx + 1].Insert(key, value);
            if (promoted != null)
            {
                Debug.Assert(promoted.Keys.Count == 1);
                Debug.Assert(promoted.Values == null);
                Debug.Assert(promoted.Children != null);
                Debug.Assert(promoted.Children.Count == 2);

                var promotedIdx = Keys.FindLastIndex(x => promoted.Keys[0].CompareTo(x) >= 0);

                Keys.Insert(promotedIdx + 1, promoted.Keys[0]);

                Children[promotedIdx + 1] = promoted.Children[0];
                Children.Insert(promotedIdx + 2, promoted.Children[1]);
            }
        }

        if (Keys.Count <= 2 * _t) return null;

        var leftNode = new BPlusTreeNode<TKey, TValue>(_t)
        {
            Children = null,
            Values = null,
        };
        var rightNode = new BPlusTreeNode<TKey, TValue>(_t)
        {
            Children = null,
            Values = null,
        };
        leftNode.PreviousLeaf = PreviousLeaf;
        rightNode.NextLeaf = NextLeaf;
        leftNode.NextLeaf = rightNode;
        rightNode.PreviousLeaf = leftNode;
        if (PreviousLeaf != null) PreviousLeaf.NextLeaf = leftNode;
        if (NextLeaf != null) NextLeaf.PreviousLeaf = rightNode;

        var splitPos = Keys.Count / 2;


        if (IsLeaf)
        {
            Debug.Assert(Values != null);

            leftNode.Keys = Keys[..splitPos];
            rightNode.Keys = Keys[splitPos..];

            leftNode.Values = Values[..splitPos];
            rightNode.Values = Values[splitPos..];
        }
        else
        {
            Debug.Assert(Children != null);

            leftNode.Keys = Keys[..splitPos];
            rightNode.Keys = Keys[(splitPos + 1)..];

            leftNode.Children = Children[..(splitPos + 1)];
            rightNode.Children = Children[(splitPos + 1)..];
        }


        return new BPlusTreeNode<TKey, TValue>(_t)
        {
            Children = [leftNode, rightNode],
            Keys = [Keys[splitPos]],
            Values = null,
        };
    }


    [Conditional("DEBUG")]
    public void CheckConsistency()
    {
        Debug.Assert(Keys.Count <= 2 * _t);
        if (IsLeaf)
        {
            Debug.Assert(Children == null);
            Debug.Assert(Values != null);
            Debug.Assert(Keys.Count == Values.Count);
        }
        else
        {
            Debug.Assert(Children != null);
            Debug.Assert(Values == null);
            Debug.Assert(Children.Count == Keys.Count + 1);

            Debug.Assert(Children.Select(x => x.IsLeaf)
                .Distinct().Count() == 1);

            foreach (var child in Children)
            {
                Debug.Assert(child._t == _t);
                if (!child.IsLeaf)
                {
                    Debug.Assert(child.Keys.Count >= _t);
                }

                child.CheckConsistency();
            }
        }
    }

    public BPlusTreeNode<TKey, TValue> FindLeaf(TKey from)
    {
        if (IsLeaf) return this;
        var idx = Keys.FindLastIndex(x => from.CompareTo(x) >= 0);
        Debug.Assert(Children != null);
        return Children[idx + 1].FindLeaf(from);
    }
}