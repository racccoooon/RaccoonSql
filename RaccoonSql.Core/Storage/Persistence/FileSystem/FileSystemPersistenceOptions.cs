using System.IO.Abstractions;
using RaccoonSql.Core.Serialization;

namespace RaccoonSql.Core.Storage.Persistence.FileSystem;

public record FileSystemPersistenceOptions
{
    public required IFileSystem FileSystem { get; init; }
    public required string Path { get; init; }
    public required ISerializationEngineFactory SerializationEngineFactory { get; init; }
}