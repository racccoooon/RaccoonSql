namespace RaccoonSql.Core.Storage;

public readonly struct StorageInfo
{
    public bool Exists => ChunkInfo is not null;
    public required string CollectionName { get; init; }
    public required ChunkInfo? ChunkInfo { get; init; }
}