using RaccoonSql.Core.Serialization;

namespace RaccoonSql.Core.Storage.FileSystem;

public record FileSystemStorageEngineOptions
{
    public required string StoragePath { get; init; }
    public required ISerializationEngineFactory SerializationEngineFactory { get; init; }
}