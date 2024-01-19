namespace RaccoonSql.Core.Storage.Querying;

public interface IQueryPlanNode<TModel> where TModel : IModel
{
    internal IEnumerable<Row<TModel>> Execute(ModelCollection<TModel> collection);
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
        
        return _accessor(x.Model).CompareTo(_accessor(y.Model));
    }
}

public class QueryPlanMergeSorted<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    public required IReadOnlyList<IQueryPlanNode<TModel>> Children { get; init; }
    public required IComparer<Row<TModel>> Comparer { get; init; }

    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection)
    {
        var enumerators = new IEnumerator<Row<TModel>>[Children.Count];
        var validCount = 0;

        for (var i = 0; i < enumerators.Length; i++)
        {
            // ReSharper disable once NotDisposedResource
            var enumerator = Children[i].Execute(collection).GetEnumerator();
            if (enumerator.MoveNext())
            {
                enumerators[validCount++] = enumerator;
            }
            else
            {
                enumerators[enumerators.Length - 1 - (i - validCount)] = enumerator;
            }
        }

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

        foreach (var enumerator in enumerators)
        {
            enumerator.Dispose();
        }
    }
}

public class QueryPlanMergeUnsorted<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    public required IReadOnlyList<IQueryPlanNode<TModel>> Children { get; init; }

    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection)
    {
        var seenIds = new HashSet<Guid>();
        foreach (var child in Children)
        {
            foreach (var row in child.Execute(collection))
            {
                if (seenIds.Add(row.Model.Id))
                {
                    yield return row;
                }
            }
        }
    }
}

public class QueryPlanPredicateFilter<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    public required Predicate<TModel> Predicate { get; init; }
    public required IQueryPlanNode<TModel> Child { get; init; }

    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var row in Child.Execute(collection))
        {
            if (Predicate(row.Model))
            {
                yield return row;
            }
        }
    }
}

public class QueryPlanLimit<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    public required int Skip { get; init; }
    public required int? Take { get; init; }
    public required IQueryPlanNode<TModel> Child { get; init; }
    
    public IEnumerable<Row<TModel>> Execute(ModelCollection<TModel> collection)
    {
        using var enumerator = Child.Execute(collection).GetEnumerator();
        var valid = enumerator.MoveNext();
        for (var i = 0; valid && i < Skip; i++)
        {
            valid = enumerator.MoveNext();
        }

        if (Take.HasValue)
        {
            for (var i = 0; valid && i < Take; i++)
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
    public IReadOnlyList<ScanRange> Ranges { get; init; } = [];

    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection)
    {
        var index = collection.GetIndex(Name);
        foreach (var range in Ranges)
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
}

public readonly struct ScanRange
{
    public required object Start { get; init; }
    public required object End { get; init; }
    public required bool StartSet { get; init; }
    public required bool EndSet { get; init; }
    public required bool StartInclusive { get; init; }
    public required bool EndInclusive { get; init; }
}

public class QueryPlanSort<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    public required IComparer<Row<TModel>> Comparer { get; init; }
    public required IQueryPlanNode<TModel> Child { get; init; }

    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection)
    {
        var results = Child.Execute(collection).ToList();
        results.Sort(Comparer);
        return results;
    }
}

public class QueryPlanFullScan<TModel> : IQueryPlanNode<TModel> where TModel : IModel
{
    IEnumerable<Row<TModel>> IQueryPlanNode<TModel>.Execute(ModelCollection<TModel> collection)
    {
        return collection.GetAllRows();
    }
}

public class QueryPlan<TModel> where TModel : IModel
{
    public required IQueryPlanNode<TModel> Root { get; init; }


    public IEnumerable<Row<TModel>> Execute(ModelSet<TModel> collection)
    {
        return Root.Execute(collection._modelCollection);
    }
}