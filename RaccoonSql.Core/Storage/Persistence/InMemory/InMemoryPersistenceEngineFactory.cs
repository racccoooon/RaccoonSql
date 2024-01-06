namespace RaccoonSql.Core.Storage.Persistence.InMemory;

public class InMemoryPersistenceEngineFactory : IPersistenceEngineFactory
{
    public IPersistenceEngine Create()
    {
        return new InMemoryPersistenceEngine();
    }
}

internal class InMemoryPersistenceEngine : IPersistenceEngine
{
    public ModelIndex LoadIndex(string setName)
    {
        return new ModelIndex();
    }

    public void AppendIndexChange(string setName, IndexChange indexChange)
    {
        // no op
    }

    public ModelCollectionChunk LoadChunk(string setName, int chunkId)
    {
        return new ModelCollectionChunk();
    }

    public void WriteIndex(string setName, ModelIndex index)
    {
        // no op
    }

    public void WriteChunk(string setName, int chunkId, ModelCollectionChunk chunk)
    {
        // no op
    }
}