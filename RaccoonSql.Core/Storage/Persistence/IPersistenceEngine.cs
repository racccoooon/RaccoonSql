namespace RaccoonSql.Core.Storage.Persistence;

public interface IPersistenceEngine
{
    uint GetChunkCount(string setName);
    ModelCollectionChunk<TModel> LoadChunk<TModel>(string setName, uint chunkId, Type type)
        where TModel : ModelBase;
    void WriteChunk<TModel>(string setName, uint chunkId, ModelCollectionChunk<TModel> chunk, ChunkChange change)
        where TModel : ModelBase;
}