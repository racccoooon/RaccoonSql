using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using RaccoonSql.CoreRework.Internal;

namespace RaccoonSql.CoreRework;

/// <summary>
/// A set of models of the same type.
/// This is comparable to a database table.
/// Allows to access, add or remove models.
/// </summary>
/// <typeparam name="TModel"></typeparam>
[PublicAPI]
public interface IModelSet<TModel>
    where TModel : ModelBase
{
    /// <summary>
    /// Adds the provided model to the change tracker.
    /// Added models will be saved to the database when the transaction is committed.
    /// </summary>
    /// <param name="model">The model to be added.</param>
    void Add(TModel model);
    
    /// <summary>
    /// Adds the provided models to the change tracker.
    /// Added models will be saved to the database when the transaction is committed.
    /// </summary>
    /// <param name="models">The models to be added.</param>
    void Add(IEnumerable<TModel> models);
    
    /// <summary>
    /// Adds the provided models to the change tracker.
    /// Added models will be saved to the database when the transaction is committed.
    /// </summary>
    /// <param name="models">The models to be added.</param>
    void Add(params TModel[] models);
    
    /// <summary>
    /// Finds a model by its id.
    /// </summary>
    /// <param name="id">The id of the model that is being searched.</param>
    /// <returns>The model with the provided id or null if no such model was found.</returns>
    TModel? Find(Guid id);
    
    /// <summary>
    /// Marks a model for removal in the change tracker.
    /// Models that are marked for removal will be removed from the database when the transaction is committed.
    /// </summary>
    /// <param name="model">The model that is to be removed.</param>
    void Remove(TModel model);
    
    /// <summary>
    /// Marks the provided models for removal in the change tracker.
    /// Models that are marked for removal will be removed from the database when the transaction is committed.
    /// </summary>
    /// <param name="models">The models that are to be removed.</param>
    void Remove(IEnumerable<TModel> models);
    
    /// <summary>
    /// Marks the provided models for removal in the change tracker.
    /// Models that are marked for removal will be removed from the database when the transaction is committed.
    /// </summary>
    /// <param name="models">The models that are to be removed.</param>
    void Remove(params TModel[] models);
    
    /// <summary>
    /// The change tracker for this set in the current transaction.
    /// </summary>
    ChangeTracker<TModel> ChangeTracker { get; }

    IQuery<TModel> Query();
}

public sealed class QuerySelectResult<TModel> {}
public sealed class QueryDeleteResult<TModel> {}
public sealed class QueryUpdateResult<TModel> {}


public sealed class ModelSet<TModel> : IModelSet<TModel>, IModelSet
    where TModel : ModelBase
{
    private readonly TModel[] _singleArray = new TModel[1];
    private readonly ModelCollection<TModel> _modelCollection;
    private readonly Transaction _transaction;

    public ChangeTracker<TModel> ChangeTracker { get; } = new();
    public IQuery<TModel> Query()
    {
        return new Query<TModel>();
    }

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
        if (_transaction.IsCompleted) throw new TransactionCompletedException();
        Add(SingleEnumerable(model));
    }

    public void Add(params TModel[] models)
    {
        if (_transaction.IsCompleted) throw new TransactionCompletedException();
        Add(models as IEnumerable<TModel>);
    }

    public void Add(IEnumerable<TModel> models)
    {
        if (_transaction.IsCompleted) throw new TransactionCompletedException();
        ChangeTracker.Add(models.ToList());
    }

    public TModel? Find(Guid id)
    {
        if (_transaction.IsCompleted) throw new TransactionCompletedException();
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
        if (_transaction.IsCompleted) throw new TransactionCompletedException();
        Remove(SingleEnumerable(model));
    }

    public void Remove(params TModel[] models)
    {
        if (_transaction.IsCompleted) throw new TransactionCompletedException();
        Remove(models as IEnumerable<TModel>);
    }
    
    public void Remove(IEnumerable<TModel> models)
    {
        if (_transaction.IsCompleted) throw new TransactionCompletedException();
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