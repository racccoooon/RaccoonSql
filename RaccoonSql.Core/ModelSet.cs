using System.Runtime.CompilerServices;
using RaccoonSql.Core.Storage;

namespace RaccoonSql.Core;

public class ModelSet<TModel>
    where TModel : ModelBase
{
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
        
        var cloned = (TModel) RuntimeHelpers.GetUninitializedObject(typeof(TModel));
        AutoMapper.Map(model, cloned);
        
        var storageInfo = ModelCollection.GetStorageInfo(cloned.Id);
        if (conflictBehavior.Value.ShouldThrow(storageInfo.ChunkInfo.HasValue))
            throw new DuplicateIdException(typeof(TModel), cloned.Id);
        
        ModelCollection.Write(cloned, storageInfo.ChunkInfo);
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
            : ModelProxyFactory.GenerateProxy(ModelCollection.Read(storageInfo.ChunkInfo!.Value));
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

        var writeModel = ModelCollection.Read(storageInfo.ChunkInfo.Value);
        AutoMapper.ApplyChanges(writeModel, model.Changes);

        ModelCollection.Write(writeModel, storageInfo.ChunkInfo);
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
            writeModel = ModelCollection.Read(storageInfo.ChunkInfo.Value);
            AutoMapper.ApplyChanges(writeModel, model.Changes);
        }
        else
        {
            writeModel = (TModel) RuntimeHelpers.GetUninitializedObject(typeof(TModel));
            AutoMapper.Map(model, writeModel);
        }
        
        ModelCollection.Write(writeModel, storageInfo.ChunkInfo);
    }

    public void Remove(Guid id, ConflictBehavior? conflictBehavior = null)
    {
        var storageInfo = ModelCollection.GetStorageInfo(id);
        if ((conflictBehavior ?? _modelStoreOptions.DefaultRemoveConflictBehavior).ShouldThrow(!storageInfo.ChunkInfo.HasValue)) 
            throw new IdNotFoundException(typeof(TModel), id);
        if (storageInfo.ChunkInfo != null) 
            ModelCollection.Delete(storageInfo.ChunkInfo.Value);
    }
}