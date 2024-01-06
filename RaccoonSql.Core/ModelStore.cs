using RaccoonSql.Core.Storage;

namespace RaccoonSql.Core;

public class ModelStore(
    ModelStoreOptions options,
    IStorageEngineFactory storageEngineFactory)
    : IModelStore
{
    public ModelStore(IStorageEngineFactory storageEngineFactory)
        : this(new ModelStoreOptions(), storageEngineFactory)
    {
    }
    
    private IStorageEngine StorageEngine { get; } = storageEngineFactory.Create();
    
    public IModelSet<TData> Set<TData>(string? setName = null) where TData : IModel
    {
        return new ModelSet<TData>(StorageEngine, options);
    }
}