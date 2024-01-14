using System.Diagnostics;
using RaccoonSql.Core.Storage.Persistence;

namespace RaccoonSql.Core.Storage;

internal class StorageEngine(
    IPersistenceEngine persistenceEngine) 
    : IStorageEngine
{
    private readonly Dictionary<string, ModelCollection> _collections = new();

    private ModelCollection GetCollectionByName(string collectionName, Type type)
    {
        if (!_collections.TryGetValue(collectionName, out var collection))
        {
            collection = new ModelCollection(collectionName, persistenceEngine, type);
            _collections[collectionName] = collection;
        }

        return collection;
    }

    public IEnumerable<StorageInfo> QueryStorageInfo(string collectionName, Type type)
    {
        var collection = GetCollectionByName(collectionName, type);
        return collection.GetStorageInfos();
    }

    public StorageInfo GetStorageInfo(string collectionName, Guid id, Type type)
    {
        var collection = GetCollectionByName(collectionName, type);
        var storageInfo = collection.GetStorageInfo(id);
        return storageInfo;
    }

    public void Write(StorageInfo storageInfo, IModel model)
    {
        var collection = GetCollectionByName(storageInfo.CollectionName, typeof(IModel));
        collection.Write(model, storageInfo.ChunkInfo);
    }

    public IModel Read(StorageInfo storageInfo)
    {
        Debug.Assert(storageInfo.ChunkInfo != null, "storageInfo.ChunkInfo != null");
        var collection = GetCollectionByName(storageInfo.CollectionName, typeof(IModel));
        return collection.Read(storageInfo.ChunkInfo.Value);
    }

    public void Delete(StorageInfo storageInfo, Type type)
    {
        Debug.Assert(storageInfo.ChunkInfo != null, "storageInfo.ChunkInfo != null");
        var collection = GetCollectionByName(storageInfo.CollectionName, type);
        collection.Delete(storageInfo.ChunkInfo.Value);
    }
}