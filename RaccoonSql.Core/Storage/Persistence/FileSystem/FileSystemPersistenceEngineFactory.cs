namespace RaccoonSql.Core.Storage.Persistence.FileSystem;

public class FileSystemPersistenceEngineFactory(FileSystemPersistenceOptions options)
    : IPersistenceEngineFactory
{
    public IPersistenceEngine Create()
    {
        return new FileSystemPersistenceEngine(options.FileSystem, options.Path, options.SerializationEngineFactory.Create());
    }
}