using System.Runtime.CompilerServices;
using System.Linq.Expressions;
using System.Reflection;
using RaccoonSql.Core.Storage;
using RaccoonSql.Core.Storage.Querying;
using RaccoonSql.Core.Utils;
using ParameterExpression = System.Linq.Expressions.ParameterExpression;

namespace RaccoonSql.Core;

public class QueryableModelSet<TModel>(ModelCollection<TModel> modelCollection)
    where TModel : ModelBase
{
    private Expression<Func<TModel, bool>>? _whereClause;
    private PropertyInfo? _orderByProperty;
    private bool _orderByDescending;
    private int? _take;
    private int _skip;

    public QueryableModelSet<TModel> Where(Expression<Func<TModel, bool>> predicate)
    {
        _whereClause = ExpressionUtils.RenameParams(ExpressionUtils.ExecutePartially(predicate), 
            new Dictionary<ParameterExpression, string>([
                new KeyValuePair<ParameterExpression, string>(predicate.Parameters[0], typeof(TModel).Name)
            ]));


        return this;
    }

    public QueryableModelSet<TModel> OrderBy<TProperty>(Expression<Func<TModel, TProperty>> accessor)
        where TProperty : IComparable, IComparable<TProperty>
    {
        return OrderBy(accessor, false);
    }

    public QueryableModelSet<TModel> OrderBy<TProperty>(Expression<Func<TModel, TProperty>> accessor, bool descending)
        where TProperty : IComparable, IComparable<TProperty>
    {
        _orderByDescending = descending;
        _orderByProperty = ExpressionUtils.GetPropertyFromAccessor(accessor);
        return this;
    }

    public QueryableModelSet<TModel> OrderByDescending<TProperty>(Expression<Func<TModel, TProperty>> accessor)
        where TProperty : IComparable, IComparable<TProperty>
    {
        return OrderBy(accessor, true);
    }

    public QueryableModelSet<TModel> Take(int take)
    {
        _take = take;
        return this;
    }

    public QueryableModelSet<TModel> Skip(int skip)
    {
        _skip = skip;
        return this;
    }
    

    public QueryPlan<TModel> Plan()
    {
        IQueryPlanNode<TModel> rootNode = new QueryPlanFullScan<TModel>();

        if (_whereClause != null)
        {
            var dnf = ExpressionUtils.NormalizeDnf(_whereClause.Body);
            var dnfExpr = dnf.Select(andItems =>andItems.Aggregate((x1, x2) => Expression.And(x1, x2)))
                .Aggregate((x1, x2) => Expression.Or(x1, x2));
            var dnfFunc = Expression.Lambda<Func<TModel, bool>>(dnfExpr, _whereClause.Parameters);
            
            rootNode = new QueryPlanPredicateFilter<TModel>
            {
                Child = rootNode,
                Predicate = new ParameterizedPredicate<TModel>([], dnfFunc),
            };
        }

        if (_orderByProperty != null)
        {
            var param = Expression.Parameter(typeof(TModel), typeof(TModel).Name);
            var accessor = Expression.MakeMemberAccess(param, _orderByProperty);
            var func = Expression.Lambda<Func<TModel, IComparable>>(accessor, [param]);
            rootNode = new QueryPlanSort<TModel>
            {
                Child = rootNode,
                Comparer = new QueryPlanSortComparer<TModel>(func, _orderByDescending),
            };
        }

        if (_take.HasValue || _skip > 0)
        {
            rootNode = new QueryPlanLimit<TModel>
            {
                Child = rootNode,
                Skip = new ConstantQueryPlanParameter<int>(_skip),
                Take = _take.HasValue ? new ConstantQueryPlanParameter<int>(_take.Value) : null,
            };
        }

        return new QueryPlan<TModel>
        {
            Root = rootNode
        };
    }
}

public class ModelSet<TModel>
    where TModel : ModelBase
{
    public QueryableModelSet<TModel> AsQueryable()
    {
        return new QueryableModelSet<TModel>(ModelCollection);
    }

    private readonly ModelStoreOptions _modelStoreOptions;

    internal readonly ModelCollection<TModel> ModelCollection;

    internal ModelSet(ModelCollection<TModel> modelCollection,
        ModelStoreOptions modelStoreOptions)
    {
        ModelCollection = modelCollection;
        _modelStoreOptions = modelStoreOptions;
    }

    public void Insert(TModel model, ConflictBehavior? conflictBehavior = null)
    {
        conflictBehavior ??= _modelStoreOptions.DefaultInsertConflictBehavior;
        if (!ModelCollection.ExecuteChecksConstraints(model, conflictBehavior == ConflictBehavior.Throw))
            return;

        var cloned = (TModel)RuntimeHelpers.GetUninitializedObject(typeof(TModel));
        AutoMapper.Map(model, cloned);

        var storageInfo = ModelCollection.GetStorageInfo(cloned.Id);
        if (conflictBehavior.Value.ShouldThrow(storageInfo.ChunkInfo.HasValue))
            throw new DuplicateIdException(typeof(TModel), cloned.Id);

        ModelCollection.Insert(cloned);
    }

    public bool Exists(Guid id)
    {
        var storageInfo = ModelCollection.GetStorageInfo(id);
        return storageInfo.ChunkInfo.HasValue;
    }

    public TModel? Find(Guid id, ConflictBehavior? conflictBehavior = null)
    {
        var storageInfo = ModelCollection.GetStorageInfo(id);
        if ((conflictBehavior ?? _modelStoreOptions.FindDefaultConflictBehavior).ShouldThrow(!storageInfo.ChunkInfo.HasValue))
            throw new IdNotFoundException(typeof(TModel), id);
        return !storageInfo.ChunkInfo.HasValue
            ? default
            : ModelProxyFactory.GenerateProxy(ModelCollection.Read(id));
    }

    public IEnumerable<TModel> All()
    {
        return ModelCollection.GetAllRows().Select(x => ModelProxyFactory.GenerateProxy(x.Model));
    }

    public void Update(TModel model, ConflictBehavior? conflictBehavior = null)
    {
        var storageInfo = ModelCollection.GetStorageInfo(model.Id);
        conflictBehavior ??= _modelStoreOptions.DefaultUpdateConflictBehavior;
        if (conflictBehavior.Value.ShouldThrow(!storageInfo.ChunkInfo.HasValue))
            throw new IdNotFoundException(typeof(TModel), model.Id);

        if (!storageInfo.ChunkInfo.HasValue)
            return;

        if (!ModelCollection.ExecuteChecksConstraints(model, conflictBehavior == ConflictBehavior.Throw))
            return;

        ModelCollection.Update(model, model.Changes);
    }

    public void Upsert(TModel model, ConflictBehavior? conflictBehavior = null)
    {
        conflictBehavior ??= _modelStoreOptions.DefaultUpsertConflictBehavior;
        if (!ModelCollection.ExecuteChecksConstraints(model, conflictBehavior == ConflictBehavior.Throw))
            return;

        var storageInfo = ModelCollection.GetStorageInfo(model.Id);

        TModel writeModel;
        if (storageInfo.ChunkInfo.HasValue)
        {
            writeModel = ModelCollection.Read(model.Id);
            AutoMapper.ApplyChanges(writeModel, model.Changes);
        }
        else
        {
            writeModel = (TModel)RuntimeHelpers.GetUninitializedObject(typeof(TModel));
            AutoMapper.Map(model, writeModel);
        }

        ModelCollection.Insert(writeModel);
    }

    public void Remove(Guid id, ConflictBehavior? conflictBehavior = null)
    {
        var storageInfo = ModelCollection.GetStorageInfo(id);
        if ((conflictBehavior ?? _modelStoreOptions.DefaultRemoveConflictBehavior).ShouldThrow(!storageInfo.ChunkInfo.HasValue))
            throw new IdNotFoundException(typeof(TModel), id);
        if (storageInfo.ChunkInfo != null)
            ModelCollection.Delete(id);
    }
}