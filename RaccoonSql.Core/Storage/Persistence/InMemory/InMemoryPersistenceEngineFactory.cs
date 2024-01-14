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

    public void FlushIndex(string setName, ModelIndex index)
    {
        // no op
    }

    public ModelCollectionChunk LoadChunk(string setName, uint chunkId, Type type)
    {
        return new ModelCollectionChunk();
    }

    public void WriteIndex(string setName, ModelIndex index, IndexChange change)
    {
        // no op
    }

    public void WriteChunk(string setName, uint chunkId, ModelCollectionChunk chunk, ChunkChange change)
    {
        // no op
    }
}