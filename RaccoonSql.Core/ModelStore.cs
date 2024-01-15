using System.IO.Abstractions;
using RaccoonSql.Core.Serialization.Json;
using RaccoonSql.Core.Storage;
using RaccoonSql.Core.Storage.Persistence.FileSystem;

namespace RaccoonSql.Core;

public class ModelStore(
    ModelStoreOptions options)
{
    private StorageEngine StorageEngine { get; } = new(new FileSystemPersistenceEngine(
        new FileSystem(),
        options.Root,
        new JsonSerializationEngine()));

    public ModelSet<TData> Set<TData>(string? setName = null) where TData : IModel
    {
        return new ModelSet<TData>(setName, StorageEngine, options);
    }
}