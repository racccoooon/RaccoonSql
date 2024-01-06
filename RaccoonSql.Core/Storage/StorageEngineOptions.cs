using RaccoonSql.Core.Persistance;

namespace RaccoonSql.Core.Storage;

public record StorageEngineOptions
{
    public required string StoragePath { get; init; }
    public IPersistenceEngineFactory? PersistenceProviderFactory { get; init; }
}