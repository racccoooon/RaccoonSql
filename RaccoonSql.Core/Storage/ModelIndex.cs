using MemoryPack;

namespace RaccoonSql.Core.Storage;

[MemoryPackable]
public partial class ModelIndex
{
    public Dictionary<Guid, ChunkInfo> Index { get; set; } = new();
    public uint ChunkCount { get; set; } = 16;

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

    public void Apply(IndexChange change)
    {
        switch (change.ChangeType)
        {
            case IndexChangeType.Set:
                Index[change.Id] = change.ChunkInfo;
                break;
            case IndexChangeType.Remove:
                Index.Remove(change.Id);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}