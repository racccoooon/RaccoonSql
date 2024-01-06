namespace RaccoonSql.Core.Storage.FileSystem;

internal class ModelIndex
{
    private readonly Dictionary<Guid, ChunkInfo> _index = new();

    public ChunkInfo? GetChunkInfo(Guid id)
    {
        if (!_index.TryGetValue(id, out var chunkInfo))
            return null;
        return chunkInfo;
    }

    public void Delete(Guid modelId)
    {
        _index.Remove(modelId);
    }

    public void Add(Guid modelId, ChunkInfo chunkInfo)
    {
        _index[modelId] = chunkInfo;
    }
}