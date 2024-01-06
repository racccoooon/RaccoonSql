namespace RaccoonSql.Core.Persistance.FileSystem;

public class FileSystemPersistenceEngineFactory
    : IPersistenceEngineFactory
{
    public IPersistenceEngine Create()
    {
        return new FileSystemPersistenceEngine();
    }
}