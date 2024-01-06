namespace RaccoonSql.Core.Storage.FileSystem;

internal class FileSystemStorageInfo : IStorageInfo
{
    public bool Exists => ChunkInfo is not null;
    public required string CollectionName { get; init; }
    public required ChunkInfo? ChunkInfo { get; init; }
    public required Guid Id { get; init; }
}