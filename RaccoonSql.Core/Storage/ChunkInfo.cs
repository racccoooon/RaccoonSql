using MemoryPack;

namespace RaccoonSql.Core.Storage;

[MemoryPackable]
public partial struct ChunkInfo
{
    public required int ChunkId { get; init; }
    public required int Offset { get; init; }
}