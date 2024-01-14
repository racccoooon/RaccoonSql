using MemoryPack;

namespace RaccoonSql.Core.Storage.Persistence.FileSystem;

[MemoryPackable]
public partial struct ChunkChangeModel
{
    public required byte[] SerializedModel { get; init; }
    public required bool Add { get; init; }
    public required uint Offset { get; init; }
}