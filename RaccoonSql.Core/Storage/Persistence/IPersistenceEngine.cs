namespace RaccoonSql.Core.Storage.Persistence;

public interface IPersistenceEngine
{
    ModelIndex LoadIndex(string setName);
    void WriteIndex(string setName, ModelIndex index);
    void AppendIndexChange(string setName, IndexChange indexChange);
    
    ModelCollectionChunk LoadChunk(string setName, int chunkId);
    void WriteChunk(string setName, int chunkId, ModelCollectionChunk chunk);
}