namespace RaccoonSql.Core.Storage.InMemory;

internal class InMemoryStorageEngine 
    : IStorageEngine
{
    private readonly Dictionary<string, Dictionary<Guid, IModel>> _collections = new();

    private Dictionary<Guid, IModel> GetNamedCollection(string collectionName)
    {
        if (!_collections.TryGetValue(collectionName, out var models))
        {
            models = new Dictionary<Guid, IModel>();
            _collections[collectionName] = models;
        }

        return models;
    }
    
    public IStorageInfo GetStorageInfo(string collectionName, Guid id) 
        => new InMemoryStorageInfo(GetNamedCollection(collectionName).ContainsKey(id), collectionName, id);

    public void Write(IStorageInfo storageInfo, IModel model)
    {
        if (storageInfo is not InMemoryStorageInfo info) throw new NotInMemoryStorageInfoException();
        GetNamedCollection(info.CollectionName)[info.Id] = model;
    }

    public IModel Read(IStorageInfo storageInfo)
    {
        if (storageInfo is not InMemoryStorageInfo info) throw new NotInMemoryStorageInfoException();
        return GetNamedCollection(info.CollectionName)[info.Id];
    }

    public void Delete(IStorageInfo storageInfo)
    {
        if (storageInfo is not InMemoryStorageInfo info) throw new NotInMemoryStorageInfoException();
        GetNamedCollection(info.CollectionName).Remove(info.Id);
    }
}