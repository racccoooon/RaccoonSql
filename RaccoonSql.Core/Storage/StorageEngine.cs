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

    public IEnumerable<IStorageInfo> QueryStorageInfo(string collectionName, Type type)
    {
        var collection = GetCollectionByName(collectionName, type);
        return collection.GetStorageInfos();
    }

    public IStorageInfo GetStorageInfo(string collectionName, Guid id, Type type)
    {
        var collection = GetCollectionByName(collectionName, type);
        var storageInfo = collection.GetStorageInfo(id);
        return storageInfo;
    }

    public void Write(IStorageInfo storageInfo, IModel model)
    {
        if (storageInfo is not StorageInfo info) throw new NotStorageInfoException();
        var collection = GetCollectionByName(info.CollectionName, typeof(IModel));
        collection.Write(model, info.ChunkInfo);
    }

    public IModel Read(IStorageInfo storageInfo)
    {
        if (storageInfo is not StorageInfo info) throw new NotStorageInfoException();
        Debug.Assert(info.ChunkInfo != null, "info.ChunkInfo != null");
        var collection = GetCollectionByName(info.CollectionName, typeof(IModel));
        return collection.Read(info.ChunkInfo.Value);
    }

    public void Delete(IStorageInfo storageInfo, Type type)
    {
        if (storageInfo is not StorageInfo info) throw new NotStorageInfoException();
        Debug.Assert(info.ChunkInfo != null, "info.ChunkInfo != null");
        var collection = GetCollectionByName(info.CollectionName, type);
        collection.Delete(info.ChunkInfo.Value);
    }
}