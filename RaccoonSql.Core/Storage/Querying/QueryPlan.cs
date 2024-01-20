using System.Linq.Expressions;

namespace RaccoonSql.Core.Storage.Querying;

public interface IQueryPlanNode<TModel> where TModel : IModel
{
    internal IEnumerable<Row<TModel>> Execute(ModelCollection<TModel> collection, IQueryPlanParameterSource parameters);
}

public interface IQueryPlanParameterSource
{
    public T RetrieveNamed<T>(string name);
    public T RetrievePositional<T>(int position);
}

public class QueryPlanParameterBag(
    IReadOnlyList<object> positionalParameters,
    IReadOnlyDictionary<string, object> namedParameters) : IQueryPlanParameterSource
{
    public T RetrieveNamed<T>(string name)
    {
        return (T)namedParameters[name];
    }

    public T RetrievePositional<T>(int position)
    {
        return (T)positionalParameters[position];
    }
}

public interface IQueryPlanParameter
{
    public object? Retrieve(IQueryPlanParameterSource source);
}

public interface IQueryPlanParameter<out T> : IQueryPlanParameter
{
    public new T Retrieve(IQueryPlanParameterSource source);

    object? IQueryPlanParameter.Retrieve(IQueryPlanParameterSource source)
    {
        return Retrieve(source);
    }
}

public readonly struct NamedQueryPlanParameter<T>(string name) : IQueryPlanParameter<T>
{
    public T Retrieve(IQueryPlanParameterSource source)
    {
        return source.RetrieveNamed<T>(name);
    }
}

public readonly struct PositionalQueryPlanParameter<T>(int position) : IQueryPlanParameter<T>
{
    public T Retrieve(IQueryPlanParameterSource source)
    {
        return source.RetrievePositional<T>(position);
    }
}

public readonly struct ConstantQueryPlanParameter<T>(T value) : IQueryPlanParameter<T>
{
    public T Retrieve(IQueryPlanParameterSource source)
    {
        return value;
    }
}

public class QueryPlanSortComparer<TModel, T> : IComparer<Row<TModel>> where T : IComparable<T> where TModel : IModel
{
    private readonly Func<TModel, T> _accessor;

    public QueryPlanSortComparer(Func<TModel, T> accessor)
    {
        _accessor = accessor;
    }

    public int Compare(Row<TModel> x, Row<TModel> y)
    {
        var cmp = _accessor(x.Model).CompareTo(_accessor(y.Model));
        if (cmp != 0) return cmp;
        return x.ChunkInfo.CompareTo(y.ChunkInfo);
    }
}

public class QueryPlanMergeSorted<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    public required IReadOnlyList<IQueryPlanNode<TModel>> Children { get; init; }
    public required IComparer<Row<TModel>> Comparer { get; init; }

    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection,
        IQueryPlanParameterSource parameters)
    {
        var enumerators = new IEnumerator<Row<TModel>>[Children.Count];
        var validCount = 0;

        for (var i = 0; i < enumerators.Length; i++)
        {
            // ReSharper disable once NotDisposedResource
            var enumerator = Children[i].Execute(collection, parameters).GetEnumerator();
            if (enumerator.MoveNext())
            {
                enumerators[validCount++] = enumerator;
            }
            else
            {
                enumerators[enumerators.Length - 1 - (i - validCount)] = enumerator;
            }
        }

        if (validCount > 0)
        {
            var currentEnumeratorIdx = enumerators.Take(validCount)
                .Select((enumerator, idx) => (idx, enumerator.Current))
                .MinBy(x => x.Current, Comparer).idx;

            Guid? lastId = null;
            while (validCount > 0)
            {
                int nextEnumeratorIdx;
                Row<TModel> nextEnumeratorValue = default;
                if (validCount > 1)
                {
                    (nextEnumeratorIdx, nextEnumeratorValue) = enumerators.Take(validCount)
                        .Select((enumerator, idx) => (idx, enumerator.Current))
                        .Where(x => x.idx != currentEnumeratorIdx)
                        .MinBy(x => x.Current, Comparer);
                }
                else
                {
                    nextEnumeratorIdx = -1;
                }

                var currentEnumerator = enumerators[currentEnumeratorIdx];
                var valid = true;
                while (valid && currentEnumerator.Current.Model.Id == lastId)
                {
                    valid = currentEnumerator.MoveNext();
                }

                if (valid)
                {
                    do
                    {
                        var row = currentEnumerator.Current;
                        lastId = row.Model.Id;
                        yield return row;

                        valid = currentEnumerator.MoveNext();
                    } while (valid && (nextEnumeratorIdx == -1 ||
                                       Comparer.Compare(currentEnumerator.Current, nextEnumeratorValue) <= 0));
                }

                if (!valid)
                {
                    if (currentEnumeratorIdx != validCount - 1)
                    {
                        // not the last valid enumerator, swap with the last one

                        if (nextEnumeratorIdx == validCount - 1)
                        {
                            // the one we swap with is the next one so we need to adjust the next idx
                            nextEnumeratorIdx = currentEnumeratorIdx;
                        }

                        // swap enumerators so that the current (freshly invalid) one is at the end of the valid ones
                        (enumerators[currentEnumeratorIdx], enumerators[validCount - 1]) =
                            (enumerators[validCount - 1], enumerators[currentEnumeratorIdx]);
                    }

                    validCount--;
                }

                currentEnumeratorIdx = nextEnumeratorIdx;
            }
        }

        foreach (var enumerator in enumerators)
        {
            enumerator.Dispose();
        }
    }
}

public class QueryPlanMergeUnsorted<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    public required IReadOnlyList<IQueryPlanNode<TModel>> Children { get; init; }

    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection,
        IQueryPlanParameterSource parameters)
    {
        var seenIds = new HashSet<Guid>();
        foreach (var child in Children)
        {
            foreach (var row in child.Execute(collection, parameters))
            {
                if (seenIds.Add(row.Model.Id))
                {
                    yield return row;
                }
            }
        }
    }
}

public class ParameterizedPredicate<T>(
    IEnumerable<(ConstantExpression Expr, IQueryPlanParameter Param)> parameters,
    Expression<Func<T, bool>> expr)
{
    public Func<T, bool> Realize(IQueryPlanParameterSource parameterSource)
    {
        var resolvedParameters = parameters
            .Select(x => (x.Expr, x.Param.Retrieve(parameterSource)))
            .ToDictionary();

        var realizedExpr = new Visitor(resolvedParameters).VisitAndConvert(expr, null);
        return realizedExpr.Compile();
    }

    private class Visitor(IReadOnlyDictionary<ConstantExpression, object?> parameters) : ExpressionVisitor
    {
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (parameters.TryGetValue(node, out var value))
            {
                return Expression.Constant(value, node.Type);
            }

            return base.VisitConstant(node);
        }
    }
}

public class QueryPlanPredicateFilter<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    public required ParameterizedPredicate<TModel> Predicate { get; init; }
    public required IQueryPlanNode<TModel> Child { get; init; }

    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection,
        IQueryPlanParameterSource parameters)
    {
        var predicate = Predicate.Realize(parameters);
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var row in Child.Execute(collection, parameters))
        {
            if (predicate(row.Model))
            {
                yield return row;
            }
        }
    }
}

public class QueryPlanLimit<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    public required IQueryPlanParameter<int> Skip { get; init; }
    public required IQueryPlanParameter<int>? Take { get; init; }
    public required IQueryPlanNode<TModel> Child { get; init; }

    public IEnumerable<Row<TModel>> Execute(ModelCollection<TModel> collection, IQueryPlanParameterSource parameters)
    {
        var skip = Skip.Retrieve(parameters);
        var take = Take?.Retrieve(parameters);
        using var enumerator = Child.Execute(collection, parameters).GetEnumerator();
        var valid = enumerator.MoveNext();
        for (var i = 0; valid && i < skip; i++)
        {
            valid = enumerator.MoveNext();
        }

        if (take.HasValue)
        {
            for (var i = 0; valid && i < take; i++)
            {
                yield return enumerator.Current;
                valid = enumerator.MoveNext();
            }
        }
        else
        {
            while (valid)
            {
                yield return enumerator.Current;
                valid = enumerator.MoveNext();
            }
        }
    }
}

public class QueryPlanIndexScan<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    public required string Name { get; init; }
    public required bool Descending { get; init; }
    public ScanRanges? Ranges { get; init; }

    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection,
        IQueryPlanParameterSource parameters)
    {
        var index = collection.GetIndex(Name);

        if (Ranges != null)
        {
            var ranges = Ranges.Realize(parameters, Descending);
            foreach (var range in ranges)
            {
                foreach (var model in index.Scan(range.Start,
                             range.End,
                             range.StartSet,
                             range.EndSet,
                             range.StartInclusive,
                             range.EndInclusive,
                             Descending))
                {
                    yield return new Row<TModel>
                    {
                        ChunkInfo = collection.GetChunkInfo(model.Id),
                        Model = (TModel)model,
                    };
                }
            }
        }
        else
        {
            foreach (var model in index.Scan(default!,
                         default!,
                         false,
                         false,
                         false,
                         false,
                         Descending))
            {
                yield return new Row<TModel>
                {
                    ChunkInfo = collection.GetChunkInfo(model.Id),
                    Model = (TModel)model,
                };
            }
        }
    }
}

public class ScanRanges
{
    public IReadOnlyList<ParameterizedScanRange> Ranges { get; init; } = [];

    public IReadOnlyList<ScanRange> Realize(IQueryPlanParameterSource paramSource, bool backwards)
    {
        var realizedRanges = Ranges.Select(r => new ScanRange
        {
            Start = r.Start.Retrieve(paramSource),
            End = r.End.Retrieve(paramSource),
            StartSet = r.StartSet,
            EndSet = r.EndSet,
            StartInclusive = r.StartInclusive,
            EndInclusive = r.EndInclusive,
        }).ToList();

        var remaining = new Stack<ScanRange>(realizedRanges);
        List<ScanRange> mergedRanges = [];
        while (remaining.Count > 0)
        {
            var range = remaining.Pop();
            var merged = false;
            
            foreach (var mergedRange in mergedRanges)
            {
                if (mergedRange.Overlaps(range, backwards))
                {
                    mergedRanges.Remove(mergedRange);
                    remaining.Push(mergedRange.Merge(range, backwards));
                    merged = true;
                    break;
                }
            }
            if (!merged)
            {
                mergedRanges.Add(range);
            }
        }

        if (!backwards)
        {
            return mergedRanges.Order().ToList();
        }
        else
        {
            return mergedRanges.OrderDescending().ToList();
        }
    }
}

public readonly struct ParameterizedScanRange
{
    public required IQueryPlanParameter<IComparable> Start { get; init; }
    public required IQueryPlanParameter<IComparable> End { get; init; }
    public required bool StartSet { get; init; }
    public required bool EndSet { get; init; }
    public required bool StartInclusive { get; init; }
    public required bool EndInclusive { get; init; }
}

public readonly struct ScanRange : IComparable<ScanRange>
{
    public required IComparable Start { get; init; }
    public required IComparable End { get; init; }
    public required bool StartSet { get; init; }
    public required bool EndSet { get; init; }
    public required bool StartInclusive { get; init; }
    public required bool EndInclusive { get; init; }

    public bool Overlaps(ScanRange other, bool backwards)
    {
        var c1 = (StartSet, other.EndSet) switch
        {
            (true, true) => backwards ? Start.CompareTo(other.End) >= 0 : Start.CompareTo(other.End) <= 0,
            _ => true,
        };

        var c2 = (other.StartSet, EndSet) switch
        {
            (true, true) => backwards ? other.StartSet.CompareTo(End) >= 0 : other.StartSet.CompareTo(End) <= 0,
            _ => true,
        };
        return c1 && c2;
    }

    public ScanRange Merge(ScanRange other, bool backwards)
    {
        var startSet = StartSet && other.StartSet;
        var endSet = EndSet && other.EndSet;
        var startCmp = 0;
        var endCmp = 0;
        if (startSet)
        {
            startCmp = Start.CompareTo(other.Start);
        }

        if (endSet)
        {
            endCmp = End.CompareTo(other.End);
        }

        IComparable start, end;
        bool startInclusive, endInclusive;
        switch (startCmp)
        {
            case > 0 when !backwards:
            case < 0 when backwards:
                start = other.Start;
                startInclusive = other.StartInclusive;
                break;
            case < 0 when !backwards:
            case > 0 when backwards:
                start = Start;
                startInclusive = StartInclusive;
                break;
            default:
                start = Start;
                startInclusive = StartInclusive || other.StartInclusive;
                break;
        }

        switch (endCmp)
        {
            case > 0 when !backwards:
            case < 0 when backwards:
                end = End;
                endInclusive = EndInclusive;
                break;
            case < 0 when !backwards:
            case > 0 when backwards:
                end = other.End;
                endInclusive = other.EndInclusive;
                break;
            default:
                end = End;
                endInclusive = EndInclusive || other.EndInclusive;
                break;
        }

        return new ScanRange
        {
            Start = start,
            End = end,
            StartSet = startSet,
            EndSet = endSet,
            StartInclusive = startInclusive,
            EndInclusive = endInclusive,
        };
    }

    public int CompareTo(ScanRange other)
    {
        var startSetComparison = StartSet.CompareTo(other.StartSet);
        if (startSetComparison != 0) return startSetComparison;
        var startComparison = Start.CompareTo(other.Start);
        if (startComparison != 0) return startComparison;
        return StartInclusive.CompareTo(other.StartInclusive);
    }
}

public class QueryPlanSort<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    public required IComparer<Row<TModel>> Comparer { get; init; }
    public required IQueryPlanNode<TModel> Child { get; init; }

    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection,
        IQueryPlanParameterSource parameters)
    {
        var results = Child.Execute(collection, parameters).ToList();
        results.Sort(Comparer);
        return results;
    }
}

public class QueryPlanFullScan<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection,
        IQueryPlanParameterSource parameters)
    {
        return collection.GetAllRows();
    }
}

public class QueryPlan<TModel> where TModel : IModel
{
    public required IQueryPlanNode<TModel> Root { get; init; }


    public IEnumerable<Row<TModel>> Execute(ModelSet<TModel> collection, IQueryPlanParameterSource parameters)
    {
        return Root.Execute(collection._modelCollection, parameters);
    }
}