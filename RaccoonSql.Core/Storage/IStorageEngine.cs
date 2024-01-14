using JetBrains.Annotations;

namespace RaccoonSql.Core.Storage;

[PublicAPI]
public interface IStorageEngine
{
    IEnumerable<IStorageInfo> QueryStorageInfo(string collectionName, Type type);
    
    IStorageInfo GetStorageInfo(string collectionName, Guid id, Type type);
    
    Task<IStorageInfo> GetStorageInfoAsync(string collectionName, Guid id, Type type, CancellationToken cancellationToken = default) 
        => Task.FromResult(GetStorageInfo(collectionName, id, type));

    void Write(IStorageInfo storageInfo, IModel model);
    
    Task WriteAsync(IStorageInfo storageInfo, IModel model, CancellationToken cancellationToken = default)
    {
        Write(storageInfo, model);
        return Task.CompletedTask;
    }
    
    IModel Read(IStorageInfo storageInfo);
    
    Task<IModel> ReadAsync(IStorageInfo storageInfo, CancellationToken cancellationToken = default)
        => Task.FromResult(Read(storageInfo));
    
    void Delete(IStorageInfo storageInfo, Type type);
    
    Task DeleteAsync(IStorageInfo storageInfo, Type type, CancellationToken cancellationToken = default)
    {
        Delete(storageInfo, type);
        return Task.CompletedTask;
    }
}