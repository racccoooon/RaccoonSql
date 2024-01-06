namespace RaccoonSql.Core.Storage.FileSystem;

public class FileSystemStorageEngineFactory(
    FileSystemStorageEngineOptions options)
    : IStorageEngineFactory
{
    public IStorageEngine Create()
    {
        return new FileSystemStorageEngine();
    }
}