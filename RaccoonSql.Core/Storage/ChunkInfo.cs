using MemoryPack;

namespace RaccoonSql.Core.Storage;

[MemoryPackable]
public readonly partial struct ChunkInfo : IComparable<ChunkInfo>
{
    public required int ChunkId { get; init; }
    public required int Offset { get; init; }

    public int CompareTo(ChunkInfo other)
    {
        var chunkIdComparison = ChunkId.CompareTo(other.ChunkId);
        return chunkIdComparison != 0 
            ? chunkIdComparison 
            : Offset.CompareTo(other.Offset);
    }
}