using MemoryPack;

namespace RaccoonSql.Core.Storage;

[MemoryPackable]
public partial struct IndexChange
{
    public required IndexChangeType ChangeType { get; init; }
    public required Guid Id { get; init; }
    public required ChunkInfo ChunkInfo { get; init; }
}

public enum IndexChangeType : byte
{
    Set,
    Remove,
}