using MemoryPack;

namespace RaccoonSql.Core.Storage;

[MemoryPackable]
public readonly partial struct ChunkAddChange
{
    public required byte[] SerializedModel { get; init; }
}

[MemoryPackable]
public readonly partial struct ChunkUpdateChange
{
    public required int Index { get; init; }
    
    public required byte[] SerializedModel { get; init; }
    //public required Dictionary<string, object?> Changes { get; init; }
}

[MemoryPackable]
public readonly partial struct ChunkDeleteChange
{
    public required int Index { get; init; }
}