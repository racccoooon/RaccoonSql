namespace RaccoonSql.Core.Storage.InMemory;

public class InMemoryStorageEngineFactory : IStorageEngineFactory
{
    public IStorageEngine Create()
    {
        return new InMemoryStorageEngine();
    }
}