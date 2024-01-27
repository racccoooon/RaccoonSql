using JetBrains.Annotations;
using RaccoonSql.CoreRework.Internal;

namespace RaccoonSql.CoreRework;

/// <summary>
/// Tracks the changes in a <see cref="IModelSet{TModel}"/>.
/// The tracked changes will be applied to the database when the transaction is committed.
/// </summary>
/// <typeparam name="TModel">The type of model.</typeparam>
public sealed class ChangeTracker<TModel> 
    where TModel : ModelBase
{
    private readonly Dictionary<Guid, TModel> _addedModels = [];
    private readonly Dictionary<Guid, TModel> _loadedModels = [];
    private readonly List<Guid> _modifiedModelIds = [];
    private readonly HashSet<Guid> _deletedModelIds = [];

    /// <summary>
    /// Gets the models that are added to the <see cref="IModelSet{TModel}"/> in this transaction.
    /// </summary>
    /// <returns>The added models.</returns>
    [PublicAPI]
    public IEnumerable<TModel> GetAdded()
    {
        return _addedModels.Values;
    }

    /// <summary>
    /// Returns all tracked models from in this transaction for the <see cref="IModelSet{TModel}"/>.
    /// </summary>
    /// <returns>All tracked models (added and loaded).</returns>
    [PublicAPI]
    public IEnumerable<TModel> GetTracked()
    {
        foreach (var model in GetAdded())
        {
            yield return model;
        }

        foreach (var (id, model) in _loadedModels)
        {
            if(_deletedModelIds.Contains(id))
                continue;

            yield return model;
        }
    }
    
    internal ChangeSet GetChangeSet()
    {
        var changed = new List<ModelBase>(_modifiedModelIds.Count);
        foreach (var id in _modifiedModelIds)
        {
            if (_deletedModelIds.Contains(id)) continue;
            changed.Add(_loadedModels[id]);
        }
        
        var added = new List<ModelBase>(_addedModels.Count);
        foreach (var (id, model) in _addedModels)
        {
            added.Add(model);
        }

        return new ChangeSet(typeof(TModel), added, _deletedModelIds, changed);
    }
    
    internal void Add(List<TModel> models)
    {
        foreach (var model in models)
        {
            if (_deletedModelIds.Remove(model.Id))
                continue;

            _addedModels[model.Id] = model;
        }
    }

    internal void MarkAsRemoved(Guid id)
    {
        if (_addedModels.Remove(id))
            return;
        
        _deletedModelIds.Add(id);
    }

    internal bool IsMarkedAsRemoved(Guid id)
        => _deletedModelIds.Contains(id);

    internal TModel? Find(Guid id)
    {
        if (_addedModels.TryGetValue(id, out var result))
            return result;
        
        _loadedModels.TryGetValue(id, out result);
        return result;
    }

    internal void MarkAsModified(Guid id)
    {
        _modifiedModelIds.Add(id);
    }

    internal void StartTracking(TModel model)
    {
        _loadedModels[model.Id] = model;
    }
}