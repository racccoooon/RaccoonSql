using RaccoonSql.Core.Storage;

namespace RaccoonSql.Core;

internal class ModelSet<TData>
    : IModelSet<TData>
    where TData : IModel
{
    private readonly IStorageEngine _storageEngine;
    private readonly ModelStoreOptions _modelStoreOptions;

    internal ModelSet(string? setName, 
        IStorageEngine storageEngine,
        ModelStoreOptions modelStoreOptions)
    {
        _storageEngine = storageEngine;
        _modelStoreOptions = modelStoreOptions;
        
        SetName = typeof(TData).FullName! + "$" + (setName ?? "");
    }

    private string SetName { get; }
    
    public void Insert(TData data, ConflictBehavior? conflictBehavior)
    {
        var storageInfo = _storageEngine.GetStorageInfo(SetName, data.Id);
        if ((conflictBehavior ?? _modelStoreOptions.DefaultInsertConflictBehavior).ShouldThrow(storageInfo.Exists))
            throw new DuplicateIdException(typeof(TData), data.Id);
        _storageEngine.Write(storageInfo, data);
    }

    public bool Exists(Guid id)
    {
        var storageInfo = _storageEngine.GetStorageInfo(SetName, id);
        return storageInfo.Exists;
    }

    public TData? Find(Guid id, ConflictBehavior? conflictBehavior)
    {
        var storageInfo = _storageEngine.GetStorageInfo(SetName, id);
        if ((conflictBehavior ?? _modelStoreOptions.FindDefaultConflictBehavior).ShouldThrow(!storageInfo.Exists))
            throw new IdNotFoundException(typeof(TData), id);
        if (!storageInfo.Exists)
            return default;
        return (TData?)_storageEngine.Read(storageInfo);
    }

    public void Update(TData data, ConflictBehavior? conflictBehavior)
    {
        var storageInfo = _storageEngine.GetStorageInfo(SetName, data.Id);
        if ((conflictBehavior ?? _modelStoreOptions.DefaultUpdateConflictBehavior).ShouldThrow(!storageInfo.Exists))
            throw new IdNotFoundException(typeof(TData), data.Id);
        _storageEngine.Write(storageInfo, data);
    }

    public void Upsert(TData data)
    {
        var storageInfo = _storageEngine.GetStorageInfo(SetName, data.Id);
        _storageEngine.Write(storageInfo, data);
    }

    public void Remove(Guid id, ConflictBehavior? conflictBehavior)
    {
        var storageInfo = _storageEngine.GetStorageInfo(SetName, id);
        if ((conflictBehavior ?? _modelStoreOptions.DefaultRemoveConflictBehavior).ShouldThrow(!storageInfo.Exists)) 
            throw new IdNotFoundException(typeof(TData), id);
        _storageEngine.Delete(storageInfo);
    }
}