using System.Diagnostics;

namespace RaccoonSql.Core.Storage;

internal class StorageEngine : IStorageEngine
{
    private readonly Dictionary<string, ModelCollection> _collections = new();

    public StorageEngine()
    {
        //TODO: load from file system
    }
    
    private ModelCollection GetCollectionByName(string collectionName)
    {
        if (!_collections.TryGetValue(collectionName, out var collection))
        {
            collection = new ModelCollection(collectionName);
            _collections[collectionName] = collection;
        }

        return collection;
    }

    public IEnumerable<IStorageInfo> QueryStorageInfo(string collectionName)
    {
        var collection = GetCollectionByName(collectionName);
        return collection.GetStorageInfos();
    }

    public IStorageInfo GetStorageInfo(string collectionName, Guid id)
    {
        var collection = GetCollectionByName(collectionName);
        var storageInfo = collection.GetStorageInfo(id);
        return storageInfo;
    }

    public void Write(IStorageInfo storageInfo, IModel model)
    {
        if (storageInfo is not StorageInfo info) throw new NotStorageInfoException();
        var collection = GetCollectionByName(info.CollectionName);
        collection.Write(model, info.ChunkInfo);
    }

    public IModel Read(IStorageInfo storageInfo)
    {
        if (storageInfo is not StorageInfo info) throw new NotStorageInfoException();
        Debug.Assert(info.ChunkInfo != null, "info.ChunkInfo != null");
        var collection = GetCollectionByName(info.CollectionName);
        return collection.Read(info.ChunkInfo.Value);
    }

    public void Delete(IStorageInfo storageInfo)
    {
        if (storageInfo is not StorageInfo info) throw new NotStorageInfoException();
        Debug.Assert(info.ChunkInfo != null, "info.ChunkInfo != null");
        var collection = GetCollectionByName(info.CollectionName);
        collection.Delete(info.ChunkInfo.Value);
    }
}