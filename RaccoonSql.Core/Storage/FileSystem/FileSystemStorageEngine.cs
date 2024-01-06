namespace RaccoonSql.Core.Storage.FileSystem;

internal class FileSystemStorageEngine : IStorageEngine
{
    public IStorageInfo GetStorageInfo(Type type, Guid id)
    {
        throw new NotImplementedException();
    }

    public void Write(IStorageInfo storageInfo, object data)
    {
        throw new NotImplementedException();
    }

    public object Read(IStorageInfo storageInfo)
    {
        throw new NotImplementedException();
    }

    public void Delete(IStorageInfo storageInfo)
    {
        throw new NotImplementedException();
    }
}