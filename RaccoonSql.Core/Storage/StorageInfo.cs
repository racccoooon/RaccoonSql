namespace RaccoonSql.Core.Storage;

public readonly struct StorageInfo
{
    public required string CollectionName { get; init; }
    public required ChunkInfo? ChunkInfo { get; init; }
}