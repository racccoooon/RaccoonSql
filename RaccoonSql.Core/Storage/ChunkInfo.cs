using MemoryPack;

namespace RaccoonSql.Core.Storage;

[MemoryPackable]
public readonly partial struct ChunkInfo
{
    public required uint ChunkId { get; init; }
    public required uint Offset { get; init; }
}