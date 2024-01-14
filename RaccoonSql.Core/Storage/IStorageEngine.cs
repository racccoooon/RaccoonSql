using JetBrains.Annotations;

namespace RaccoonSql.Core.Storage;

[PublicAPI]
public interface IStorageEngine
{
    IEnumerable<StorageInfo> QueryStorageInfo(string collectionName, Type type);
    
    StorageInfo GetStorageInfo(string collectionName, Guid id, Type type);
    
    Task<StorageInfo> GetStorageInfoAsync(string collectionName, Guid id, Type type, CancellationToken cancellationToken = default) 
        => Task.FromResult(GetStorageInfo(collectionName, id, type));

    void Write(StorageInfo storageInfo, IModel model);
    
    Task WriteAsync(StorageInfo storageInfo, IModel model, CancellationToken cancellationToken = default)
    {
        Write(storageInfo, model);
        return Task.CompletedTask;
    }
    
    IModel Read(StorageInfo storageInfo);
    
    Task<IModel> ReadAsync(StorageInfo storageInfo, CancellationToken cancellationToken = default)
        => Task.FromResult(Read(storageInfo));
    
    void Delete(StorageInfo storageInfo, Type type);
    
    Task DeleteAsync(StorageInfo storageInfo, Type type, CancellationToken cancellationToken = default)
    {
        Delete(storageInfo, type);
        return Task.CompletedTask;
    }
}