namespace RaccoonSql.Core.Storage;

internal class StorageInfo : IStorageInfo
{
    public bool Exists => ChunkInfo is not null;
    public required string CollectionName { get; init; }
    public required ChunkInfo? ChunkInfo { get; init; }
}