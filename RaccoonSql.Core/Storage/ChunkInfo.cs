using MemoryPack;

namespace RaccoonSql.Core.Storage;

[MemoryPackable]
public readonly partial struct ChunkInfo : IComparable<ChunkInfo>
{
    public required uint ChunkId { get; init; }
    public required uint Offset { get; init; }

    public int CompareTo(ChunkInfo other)
    {
        var chunkIdComparison = ChunkId.CompareTo(other.ChunkId);
        if (chunkIdComparison != 0) return chunkIdComparison;
        return Offset.CompareTo(other.Offset);
    }
}