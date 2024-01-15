namespace RaccoonSql.Core.Storage.Persistence;

public interface IPersistenceEngine
{
    ModelIndex LoadIndex(string setName);
    void WriteIndex(string setName, ModelIndex index, IndexChange change);
    void FlushIndex(string setName, ModelIndex index);
    
    ModelCollectionChunk<TModel> LoadChunk<TModel>(string setName, uint chunkId, Type type)
        where TModel : IModel;
    void WriteChunk<TModel>(string setName, uint chunkId, ModelCollectionChunk<TModel> chunk, ChunkChange change)
        where TModel : IModel;
}