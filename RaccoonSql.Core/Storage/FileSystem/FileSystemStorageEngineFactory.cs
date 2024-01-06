namespace RaccoonSql.Core.Storage.FileSystem;

public class FileSystemStorageEngineFactory(
    FileSystemStorageEngineOptions options)
    : IStorageEngineFactory
{
    public IStorageEngine Create()
    {
        throw new NotImplementedException();
    }
}