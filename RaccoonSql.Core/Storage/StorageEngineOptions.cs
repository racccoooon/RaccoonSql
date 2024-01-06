using RaccoonSql.Core.Storage.Persistence;

namespace RaccoonSql.Core.Storage;

public record StorageEngineOptions
{
    public required IPersistenceEngineFactory PersistenceProviderFactory { get; init; }
}