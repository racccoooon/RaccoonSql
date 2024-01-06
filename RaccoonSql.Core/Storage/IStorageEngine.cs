using JetBrains.Annotations;

namespace RaccoonSql.Core.Storage;

[PublicAPI]
public interface IStorageEngine
{
    IStorageInfo GetStorageInfo(string collectionName, Guid id);
    
    Task<IStorageInfo> GetStorageInfoAsync(string collectionName, Guid id, CancellationToken cancellationToken = default) 
        => Task.FromResult(GetStorageInfo(collectionName, id));

    void Write(IStorageInfo storageInfo, IModel model);
    
    Task WriteAsync(IStorageInfo storageInfo, IModel model, CancellationToken cancellationToken = default)
    {
        Write(storageInfo, model);
        return Task.CompletedTask;
    }
    
    IModel Read(IStorageInfo storageInfo);
    
    Task<IModel> ReadAsync(IStorageInfo storageInfo, CancellationToken cancellationToken = default)
        => Task.FromResult(Read(storageInfo));
    
    void Delete(IStorageInfo storageInfo);
    
    Task DeleteAsync(IStorageInfo storageInfo, CancellationToken cancellationToken = default)
    {
        Delete(storageInfo);
        return Task.CompletedTask;
    }
}