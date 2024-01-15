using System.Diagnostics;
using RaccoonSql.Core.Storage.Persistence;

namespace RaccoonSql.Core.Storage;

internal class StorageEngine(
    IPersistenceEngine persistenceEngine) 
{
    private readonly Dictionary<string, object> _collections = new();

    private ModelCollection<TModel> GetCollectionByName<TModel>(string collectionName)
        where TModel : IModel
    {
        if (!_collections.TryGetValue(collectionName, out var collection))
        {
            collection = new ModelCollection<TModel>(collectionName, persistenceEngine);
            _collections[collectionName] = collection;
        }

        return (ModelCollection<TModel>)collection;
    }

    public IEnumerable<StorageInfo> QueryStorageInfo<TModel>(string collectionName)
        where TModel : IModel
    {
        var collection = GetCollectionByName<TModel>(collectionName);
        return collection.GetStorageInfos();
    }

    public StorageInfo GetStorageInfo<TModel>(string collectionName, Guid id)
        where TModel : IModel
    {
        var collection = GetCollectionByName<TModel>(collectionName);
        var storageInfo = collection.GetStorageInfo(id);
        return storageInfo;
    }

    public void Write<TModel>(StorageInfo storageInfo, TModel model)
        where TModel : IModel
    {
        var collection = GetCollectionByName<TModel>(storageInfo.CollectionName);
        collection.Write(model, storageInfo.ChunkInfo);
    }

    public TModel Read<TModel>(StorageInfo storageInfo)
        where TModel : IModel
    {
        Debug.Assert(storageInfo.ChunkInfo != null, "storageInfo.ChunkInfo != null");
        var collection = GetCollectionByName<TModel>(storageInfo.CollectionName);
        return collection.Read(storageInfo.ChunkInfo.Value);
    }

    public void Delete<TModel>(StorageInfo storageInfo)
        where TModel : IModel
    {
        Debug.Assert(storageInfo.ChunkInfo != null, "storageInfo.ChunkInfo != null");
        var collection = GetCollectionByName<TModel>(storageInfo.CollectionName);
        collection.Delete(storageInfo.ChunkInfo.Value);
    }
}