using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RaccoonSql.CoreRework.Internal.Utils;

public class BPlusTree<TKey, TValue>
    where TKey : IComparable<TKey>, IEquatable<TKey>
{
    private BPlusTreeNode<TKey, TValue> _root;
    private readonly BPlusTreeNode<TKey, TValue> _firstLeaf;
    private readonly BPlusTreeNode<TKey, TValue> _lastLeaf;

    public BPlusTree(int t)
    {
        _firstLeaf = _lastLeaf = _root = new BPlusTreeNode<TKey, TValue>(t);
    }

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

    public void Remove(TKey key, TValue value)
    {
        var (leaf, idx) = FindPosition(key, true, false);
        if (leaf == null) return;
        if (idx == -1 || idx == leaf.Keys.Count) return;
        leaf.Values![idx].Remove(value);
    }

    public IEnumerable<TValue> FunkyRange(TKey from, TKey to, bool fromSet, bool toSet, bool fromExclusive, bool toExclusive, bool backwards)
    {
        FunkyRangeValidate(from, to, fromSet, toSet, backwards);
        
        var fromLeaf = backwards ? _lastLeaf : _firstLeaf;
        var toLeaf = backwards ? _firstLeaf : _lastLeaf;
        var fromIdx = backwards ? _lastLeaf.Keys.Count : 0;
        var toIdx = backwards ? 0 : _lastLeaf.Keys.Count;
        if (fromSet)
        {
            var fromPos = FindPosition(from, !fromExclusive, backwards);
            if (fromPos.leaf != null)
            {
                fromLeaf = fromPos.leaf;
                fromIdx = fromPos.idx;
            }
        }

        if (toSet)
        {
            var toPos = FindPosition(to, !toExclusive, !backwards);
            if (toPos.leaf != null)
            {
                toLeaf = toPos.leaf;
                toIdx = toPos.idx;
            }
        }

        var currentLeaf = fromLeaf;
            
        while (true)
        {
            var start = backwards ? currentLeaf!.Keys.Count : 0;
            var end = backwards ? 0 : currentLeaf!.Keys.Count;
            if (currentLeaf == fromLeaf)
            {
                start = fromIdx;
            }

            if (currentLeaf == toLeaf)
            {
                end = toIdx;
            }

            for (var i = start; i < end; i += (backwards ? -1 : 1))
            {
                foreach (var value in currentLeaf!.Values![i])
                {
                    yield return value;
                }
            }

            if (currentLeaf == toLeaf) break;
            currentLeaf = backwards ? currentLeaf!.PreviousLeaf : currentLeaf!.NextLeaf;
        }
        
    }

    [Conditional("DEBUG")]
    private static void FunkyRangeValidate(TKey from, TKey to, bool fromSet, bool toSet, bool backwards)
    {
        if (!fromSet || !toSet) return;
        if (backwards)
        {
            Debug.Assert(from.CompareTo(to) >= 0);
        }
        else
        {
            Debug.Assert(from.CompareTo(to) <= 0);
        }
    }

    public IEnumerable<TValue> Range(TKey? from, TKey? to, bool fromExclusive, bool toExclusive)
    {
        var reverse = to.CompareTo(from) < 0;
        using var enumerator = RangeFrom(from, reverse).GetEnumerator();
        var valid = enumerator.MoveNext();
        if (fromExclusive)
        {
            while (valid && enumerator.Current.key.Equals(from))
            {
                valid = enumerator.MoveNext();
            }
        }

        while (valid && (!toExclusive || enumerator.Current.key.Equals(to)))
        {
            var values = enumerator.Current.values;

            valid = enumerator.MoveNext();
        }

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

    private static int BinarySearch(IReadOnlyList<TKey> list, TKey value, bool inclusive, bool less)
    {
        var start = 0;
        var end = list.Count;
        int index = default;
        while (true)
        {
            index = (end + start) / 2;
            var cmp = value.CompareTo(list[index]);
            if (cmp == 0) break;
            if (cmp > 0)
            {
                start = index;
            }
            else
            {
                end = index;
            }

            if (start == end || start - end == 1 || end - start == 1)
            {
                break;
            }
        }

        var cmp3 = value.CompareTo(list[index]);
        return cmp3 switch
        {
            0 when inclusive => index,

            0 when less => index - 1,
            0 when !less => index + 1,

            > 0 when less => index,
            > 0 when !less => index + 1,

            < 0 when less => index - 1,
            < 0 when !less => index,

            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private (BPlusTreeNode<TKey, TValue>? leaf, int idx) FindPosition(TKey key, bool inclusive, bool less)
    {
        var leaf = _root.FindLeaf(key);
        var idx = BinarySearch(leaf.Keys, key, inclusive, less);
        if (idx == -1)
        {
            leaf = leaf.PreviousLeaf;
            idx = (leaf?.Keys.Count ?? 0) - 1;
        }
        else if (idx == leaf.Keys.Count)
        {
            leaf = leaf.NextLeaf;
            idx = 0;
        }

        return (leaf, idx);
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

                var promotedIdx = FindLastIndex(promoted);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindLastIndex(BPlusTreeNode<TKey, TValue> promoted)
    {
        return Keys.FindLastIndex(x => promoted.Keys[0].CompareTo(x) >= 0);
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