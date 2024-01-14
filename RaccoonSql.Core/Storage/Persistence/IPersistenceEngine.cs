namespace RaccoonSql.Core.Storage.Persistence;

public interface IPersistenceEngine
{
    ModelIndex LoadIndex(string setName);
    void WriteIndex(string setName, ModelIndex index, IndexChange change);
    void FlushIndex(string setName, ModelIndex index);
    
    ModelCollectionChunk LoadChunk(string setName, uint chunkId, Type type);
    void WriteChunk(string setName, uint chunkId, ModelCollectionChunk chunk, ChunkChange change);
}