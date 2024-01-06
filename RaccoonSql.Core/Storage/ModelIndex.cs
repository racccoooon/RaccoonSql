namespace RaccoonSql.Core.Storage;

public class ModelIndex
{
    public Dictionary<Guid, ChunkInfo> Index { get; set; } = new();
    public int ChunkCount { get; set; } = 16;

    public ChunkInfo? GetChunkInfo(Guid id)
    {
        if (!Index.TryGetValue(id, out var chunkInfo))
            return null;
        return chunkInfo;
    }

    public void Delete(Guid modelId)
    {
        Index.Remove(modelId);
    }

    public void Set(Guid modelId, ChunkInfo chunkInfo)
    {
        Index[modelId] = chunkInfo;
    }
}