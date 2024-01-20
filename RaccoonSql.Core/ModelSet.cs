using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Castle.DynamicProxy;
using RaccoonSql.Core.Storage;

namespace RaccoonSql.Core;

public class ModelSet<TModel>
    where TModel : ModelBase
{
    private readonly ModelStoreOptions _modelStoreOptions;

    internal readonly ModelCollection<TModel> _modelCollection;

    internal ModelSet(ModelCollection<TModel> modelCollection,
        ModelStoreOptions modelStoreOptions)
    {
        _modelCollection = modelCollection;
        _modelStoreOptions = modelStoreOptions;
    }
    
    public void Insert(TModel model, ConflictBehavior? conflictBehavior = null)
    {
        conflictBehavior ??= _modelStoreOptions.DefaultInsertConflictBehavior;
        if (!_modelCollection.ExecuteChecksConstraints(model, conflictBehavior == ConflictBehavior.Throw))
            return;
        
        var cloned = (TModel) RuntimeHelpers.GetUninitializedObject(typeof(TModel));
        AutoMapper.Map(model, cloned);
        
        var storageInfo = _modelCollection.GetStorageInfo(cloned.Id);
        if (conflictBehavior.Value.ShouldThrow(storageInfo.Exists))
            throw new DuplicateIdException(typeof(TModel), cloned.Id);
        
        _modelCollection.Write(cloned, storageInfo.ChunkInfo);
    }

    public bool Exists(Guid id)
    {
        var storageInfo = _modelCollection.GetStorageInfo(id);
        return storageInfo.Exists;
    }

    public TModel? Find(Guid id, ConflictBehavior? conflictBehavior = null)
    {
        var storageInfo = _modelCollection.GetStorageInfo(id);
        if ((conflictBehavior ?? _modelStoreOptions.FindDefaultConflictBehavior).ShouldThrow(!storageInfo.Exists))
            throw new IdNotFoundException(typeof(TModel), id);
        return !storageInfo.ChunkInfo.HasValue 
            ? default 
            : ModelProxyFactory.GenerateProxy(_modelCollection.Read(storageInfo.ChunkInfo!.Value));
    }

    public IEnumerable<TModel> All()
    {
        return _modelCollection.GetAllRows().Select(x => ModelProxyFactory.GenerateProxy(x.Model));
    }

    public void Update(TModel model, ConflictBehavior? conflictBehavior = null)
    {
        var storageInfo = _modelCollection.GetStorageInfo(model.Id);
        conflictBehavior ??= _modelStoreOptions.DefaultUpdateConflictBehavior;
        if (conflictBehavior.Value.ShouldThrow(!storageInfo.Exists))
            throw new IdNotFoundException(typeof(TModel), model.Id);

        if (!storageInfo.ChunkInfo.HasValue) 
            return;

        if (!_modelCollection.ExecuteChecksConstraints(model, conflictBehavior == ConflictBehavior.Throw))
            return;

        var writeModel = _modelCollection.Read(storageInfo.ChunkInfo.Value);
        AutoMapper.ApplyChanges(writeModel, model.Changes);

        _modelCollection.Write(writeModel, storageInfo.ChunkInfo);
    }

    public void Upsert(TModel model, ConflictBehavior? conflictBehavior = null)
    {
        conflictBehavior ??= _modelStoreOptions.DefaultUpsertConflictBehavior;
        if (!_modelCollection.ExecuteChecksConstraints(model, conflictBehavior == ConflictBehavior.Throw))
            return;

        var storageInfo = _modelCollection.GetStorageInfo(model.Id);

        TModel writeModel;
        if (storageInfo.ChunkInfo.HasValue)
        {
            writeModel = _modelCollection.Read(storageInfo.ChunkInfo.Value);
            AutoMapper.ApplyChanges(writeModel, model.Changes);
        }
        else
        {
            writeModel = (TModel) RuntimeHelpers.GetUninitializedObject(typeof(TModel));
            AutoMapper.Map(model, writeModel);
        }
        
        _modelCollection.Write(writeModel, storageInfo.ChunkInfo);
    }

    public void Remove(Guid id, ConflictBehavior? conflictBehavior = null)
    {
        var storageInfo = _modelCollection.GetStorageInfo(id);
        if ((conflictBehavior ?? _modelStoreOptions.DefaultRemoveConflictBehavior).ShouldThrow(!storageInfo.Exists)) 
            throw new IdNotFoundException(typeof(TModel), id);
        if (storageInfo.ChunkInfo != null) 
            _modelCollection.Delete(storageInfo.ChunkInfo.Value);
    }
}