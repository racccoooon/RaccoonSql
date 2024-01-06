using System.Diagnostics;

namespace RaccoonSql.Core.Storage.FileSystem;

internal class FileSystemStorageEngine : IStorageEngine
{
    private readonly Dictionary<string, ModelCollection> _collections = new();

    public FileSystemStorageEngine()
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
    
    public IStorageInfo GetStorageInfo(string collectionName, Guid id)
    {
        var collection = GetCollectionByName(collectionName);
        var storageInfo = collection.GetStorageInfo(id);
        return storageInfo;
    }

    public void Write(IStorageInfo storageInfo, IModel model)
    {
        if (storageInfo is not FileSystemStorageInfo info) throw new NotFileSystemStorageInfoException();
        var collection = GetCollectionByName(info.CollectionName);
        collection.Write(model, info.Id, info.ChunkInfo);
    }

    public IModel Read(IStorageInfo storageInfo)
    {
        if (storageInfo is not FileSystemStorageInfo info) throw new NotFileSystemStorageInfoException();
        Debug.Assert(info.ChunkInfo != null, "info.ChunkInfo != null");
        var collection = GetCollectionByName(info.CollectionName);
        return collection.Read(info.ChunkInfo.Value);
    }

    public void Delete(IStorageInfo storageInfo)
    {
        if (storageInfo is not FileSystemStorageInfo info) throw new NotFileSystemStorageInfoException();
        Debug.Assert(info.ChunkInfo != null, "info.ChunkInfo != null");
        var collection = GetCollectionByName(info.CollectionName);
        collection.Delete(info.ChunkInfo.Value);
    }
}