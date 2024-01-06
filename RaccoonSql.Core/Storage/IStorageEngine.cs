using JetBrains.Annotations;

namespace RaccoonSql.Core.Storage;

[PublicAPI]
public interface IStorageEngine
{
    IStorageInfo GetStorageInfo(Type type, Guid id);
    
    Task<IStorageInfo> GetStorageInfoAsync(Type type, Guid id, CancellationToken cancellationToken = default) 
        => Task.FromResult(GetStorageInfo(type, id));

    void Write(IStorageInfo storageInfo, object data);
    
    Task WriteAsync(IStorageInfo storageInfo, object data, CancellationToken cancellationToken = default)
    {
        Write(storageInfo, data);
        return Task.CompletedTask;
    }
    
    object Read(IStorageInfo storageInfo);
    
    Task<object> ReadAsync(IStorageInfo storageInfo, CancellationToken cancellationToken = default)
        => Task.FromResult(Read(storageInfo));
    
    void Delete(IStorageInfo storageInfo);
    
    Task DeleteAsync(IStorageInfo storageInfo, CancellationToken cancellationToken = default)
    {
        Delete(storageInfo);
        return Task.CompletedTask;
    }
}