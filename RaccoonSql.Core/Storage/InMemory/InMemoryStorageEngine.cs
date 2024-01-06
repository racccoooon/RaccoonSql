namespace RaccoonSql.Core.Storage.InMemory;

internal class InMemoryStorageEngine 
    : IStorageEngine
{
    private readonly Dictionary<Type, Dictionary<Guid, object>> _data = new();

    private Dictionary<Guid, object> GetTyped(Type type)
    {
        if (!_data.TryGetValue(type, out var objects))
        {
            objects = new Dictionary<Guid, object>();
            _data[type] = objects;
        }

        return objects;
    }
    
    public IStorageInfo GetStorageInfo(Type type, Guid id) 
        => new InMemoryStorageInfo(GetTyped(type).ContainsKey(id), type, id);

    public void Write(IStorageInfo storageInfo, object data)
    {
        if (storageInfo is not InMemoryStorageInfo info) throw new NotInMemoryStorageInfoException();
        GetTyped(info.Type)[info.Id] = data;
    }

    public object Read(IStorageInfo storageInfo)
    {
        if (storageInfo is not InMemoryStorageInfo info) throw new NotInMemoryStorageInfoException();
        return GetTyped(info.Type)[info.Id];
    }

    public void Delete(IStorageInfo storageInfo)
    {
        if (storageInfo is not InMemoryStorageInfo info) throw new NotInMemoryStorageInfoException();
        GetTyped(info.Type).Remove(info.Id);
    }
}