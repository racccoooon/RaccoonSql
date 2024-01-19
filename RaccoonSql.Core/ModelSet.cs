using System.Linq.Expressions;
using RaccoonSql.Core.Storage;

namespace RaccoonSql.Core;

public class ModelSet<TModel>
    where TModel : IModel
{
    private readonly ModelStoreOptions _modelStoreOptions;

    private readonly ModelCollection<TModel> _modelCollection;

    private Dictionary<string, IIndex> _indices = [];

    internal ModelSet(ModelCollection<TModel> modelCollection,
        ModelStoreOptions modelStoreOptions)
    {
        _modelCollection = modelCollection;
        _modelStoreOptions = modelStoreOptions;
    }

    public ModelSet<TModel> WithIndex<T>(Expression<Func<TModel,T>> func, string? name = null)
    where T : IComparable<T>, IEquatable<T>
    {
        if (name == null)
        {
            name = func.ToString();
        }

        if (_indices.ContainsKey(name))
        {
            throw new ArgumentException($"index with name {name} already exists");
        }
        _indices[name] = new Index();
        return this;
    }
    
    public void Insert(TModel data, ConflictBehavior? conflictBehavior = null)
    {
        var storageInfo = _modelCollection.GetStorageInfo(data.Id);
        if ((conflictBehavior ?? _modelStoreOptions.DefaultInsertConflictBehavior).ShouldThrow(storageInfo.Exists))
            throw new DuplicateIdException(typeof(TModel), data.Id);
        _modelCollection.Write(data, storageInfo.ChunkInfo);
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
            : _modelCollection.Read(storageInfo.ChunkInfo!.Value);
    }

    public IEnumerable<TModel> All()
    {
        return _modelCollection.GetAllRows().Select(x => x.Model);
    }

    public void Update(TModel data, ConflictBehavior? conflictBehavior = null)
    {
        var storageInfo = _modelCollection.GetStorageInfo(data.Id);
        if ((conflictBehavior ?? _modelStoreOptions.DefaultUpdateConflictBehavior).ShouldThrow(!storageInfo.Exists))
            throw new IdNotFoundException(typeof(TModel), data.Id);
        _modelCollection.Write(data, storageInfo.ChunkInfo);
    }

    public void Upsert(TModel data)
    {
        var storageInfo = _modelCollection.GetStorageInfo(data.Id);
        _modelCollection.Write(data, storageInfo.ChunkInfo);
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