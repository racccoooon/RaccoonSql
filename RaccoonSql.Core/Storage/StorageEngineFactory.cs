namespace RaccoonSql.Core.Storage;

public class StorageEngineFactory(
    StorageEngineOptions options)
    : IStorageEngineFactory
{
    public IStorageEngine Create()
    {
        return new StorageEngine();
    }
}