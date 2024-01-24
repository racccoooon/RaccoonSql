using System.Runtime.CompilerServices;
using RaccoonSql.CoreRework.Internal;

namespace RaccoonSql.CoreRework;

public interface IModelSet<TModel>
    where TModel : ModelBase
{
    void Add(TModel model);
    void Add(IEnumerable<TModel> models);
    void Add(params TModel[] models);
    TModel? Find(Guid id);
    void Remove(TModel model);
    void Remove(IEnumerable<TModel> models);
    void Remove(params TModel[] models);
    ChangeTracker<TModel> ChangeTracker { get; }
}

public class ModelSet<TModel> : IModelSet<TModel>, IModelSet
    where TModel : ModelBase
{
    private readonly TModel[] _singleArray = new TModel[1];
    private readonly ModelCollection<TModel> _modelCollection;
    private readonly Transaction _transaction;

    public ChangeTracker<TModel> ChangeTracker { get; } = new();

    internal ModelSet(ModelCollection<TModel> modelCollection, Transaction transaction)
    {
        _modelCollection = modelCollection;
        _transaction = transaction;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    // ReSharper disable once ReturnTypeCanBeEnumerable.Local
    private TModel[] SingleEnumerable(TModel model)
    {
        _singleArray[0] = model;
        return _singleArray;
    }
    
    public void Add(TModel model)
    {
        if (_transaction.IsCompleted) throw new TransactionClosedException();
        Add(SingleEnumerable(model));
    }

    public void Add(params TModel[] models)
    {
        if (_transaction.IsCompleted) throw new TransactionClosedException();
        Add(models as IEnumerable<TModel>);
    }

    public void Add(IEnumerable<TModel> models)
    {
        if (_transaction.IsCompleted) throw new TransactionClosedException();
        ChangeTracker.Add(models.ToList());
    }

    public TModel? Find(Guid id)
    {
        if (_transaction.IsCompleted) throw new TransactionClosedException();
        if(ChangeTracker.IsMarkedAsRemoved(id)) 
            return null;

        var result = ChangeTracker.Find(id);
        if (result is not null) 
            return result;
        
        result = _modelCollection.Find(id);
        if (result is null)
            return null;
        
        result.OnChange = ModelChangedHandler;
        ChangeTracker.StartTracking(result);

        return result;
    }

    private void ModelChangedHandler(Guid id)
    {
        ChangeTracker.MarkAsModified(id);
    }

    public void Remove(TModel model)
    {
        if (_transaction.IsCompleted) throw new TransactionClosedException();
        Remove(SingleEnumerable(model));
    }

    public void Remove(params TModel[] models)
    {
        if (_transaction.IsCompleted) throw new TransactionClosedException();
        Remove(models as IEnumerable<TModel>);
    }
    
    public void Remove(IEnumerable<TModel> models)
    {
        if (_transaction.IsCompleted) throw new TransactionClosedException();
        foreach (var model in models)
        {
            ChangeTracker.MarkAsRemoved(model.Id);
        }
    }

    ChangeSet IModelSet.GetChanges()
    {
        return ChangeTracker.GetChangeSet();
    }
}