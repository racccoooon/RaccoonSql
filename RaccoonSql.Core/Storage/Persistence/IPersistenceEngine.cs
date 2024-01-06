namespace RaccoonSql.Core.Storage.Persistence;

public interface IPersistenceEngine
{
    ModelIndex LoadIndex(string setName);
    ModelCollectionChunk LoadChunk(string setName, int chunkId);
    void WriteIndex(string setName, ModelIndex index);
    void WriteChunk(string setName, int chunkId, ModelCollectionChunk chunk);
}