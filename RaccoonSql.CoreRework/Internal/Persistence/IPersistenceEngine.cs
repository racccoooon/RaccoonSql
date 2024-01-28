using System.Collections;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;

namespace RaccoonSql.CoreRework.Internal.Persistence;

internal interface IPersistenceEngine
{
    ModelStoreMetadata? ReadStoreMetadata(
        IFileSystem fileSystem,
        string rootPath);

    void WriteStoreMetadata(
        IFileSystem fileSystem,
        string rootPath,
        ModelStoreMetadata metadata);
    
    ModelCollectionMetadata? ReadCollectionMetadata(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName);

    void WriteCollectionMetadata(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        ModelCollectionMetadata metadata);

    ModelCollectionChunk<TModel> ReadChunk<TModel>(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        int chunkIndex
    ) where TModel : ModelBase;

    void WriteChunk<TModel>(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        int chunkIndex,
        ModelCollectionChunk<TModel> collectionChunk
    ) where TModel : ModelBase;

    IEnumerable<CommitChanges> ReadWal(
        IFileSystem fileSystem,
        string rootPath,
        Dictionary<string, Type> modelTypes);

    void WriteWal(
        IFileSystem fileSystem,
        string rootPath,
        CommitChanges commit);

    void DeleteWal(
        IFileSystem fileSystem, 
        string rootPath);
}

internal class PersistenceEngine : IPersistenceEngine
{
    public static readonly IPersistenceEngine Instance = new PersistenceEngine();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetStoreMetadataPath(
        IFileSystem fileSystem,
        string rootPath)
    {
        return fileSystem.Path.Combine(rootPath, $"meta");
    }
    
    public ModelStoreMetadata? ReadStoreMetadata(IFileSystem fileSystem, string rootPath)
    {
        var path = GetStoreMetadataPath(fileSystem, rootPath);
        
        if (!fileSystem.File.Exists(path))
            return null;
        
        using var stream = fileSystem.File.OpenRead(path);
        return SerialisationEngine.Instance.DeserializeStoreMetadata(stream);
    }

    public void WriteStoreMetadata(IFileSystem fileSystem, string rootPath, ModelStoreMetadata metadata)
    {
        var path = GetStoreMetadataPath(fileSystem, rootPath);

        using var stream = fileSystem.File.Open(path, FileMode.Create);
        SerialisationEngine.Instance.SerializeStoreMetadata(stream, metadata);
        stream.Flush();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetCollectionMetadataPath(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName)
    {
        return fileSystem.Path.Combine(rootPath, $"{collectionName}.meta");
    }

    public ModelCollectionMetadata? ReadCollectionMetadata(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName)
    {
        var path = GetCollectionMetadataPath(fileSystem, rootPath, collectionName);

        if (!fileSystem.File.Exists(path))
            return null;

        using var stream = fileSystem.File.OpenRead(path);
        return SerialisationEngine.Instance.DeserializeCollectionMetadata(stream);
    }

    public void WriteCollectionMetadata(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        ModelCollectionMetadata metadata)
    {
        var path = GetCollectionMetadataPath(fileSystem, rootPath, collectionName);

        using var stream = fileSystem.File.Open(path, FileMode.Create);
        SerialisationEngine.Instance.SerializeCollectionMetadata(stream, metadata);
        stream.Flush();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetChunkPath(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        int chunkIndex)
    {
        return fileSystem.Path.Combine(rootPath, $"{collectionName}.{chunkIndex}.chunk");
    }

    public ModelCollectionChunk<TModel> ReadChunk<TModel>(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        int chunkIndex)
        where TModel : ModelBase
    {
        var path = GetChunkPath(fileSystem, rootPath, collectionName, chunkIndex);

        ModelCollectionChunk<TModel> result;

        if (fileSystem.File.Exists(path))
        {
            using var stream = fileSystem.File.OpenRead(path);
            var chunkData = SerialisationEngine.Instance.DeserializeChunkData<TModel>(stream);
            result = new ModelCollectionChunk<TModel>(chunkData);
        }
        else
        {
            result = new ModelCollectionChunk<TModel>();
        }

        return result;
    }

    public void WriteChunk<TModel>(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        int chunkIndex,
        ModelCollectionChunk<TModel> collectionChunk)
        where TModel : ModelBase
    {
        var path = GetChunkPath(fileSystem, rootPath, collectionName, chunkIndex);

        using var stream = fileSystem.File.Open(path, FileMode.Create);
        SerialisationEngine.Instance.SerializeChunkData(stream, collectionChunk.GetData());
        stream.Flush();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetWalPath(
        IFileSystem fileSystem,
        string rootPath)
    {
        return fileSystem.Path.Combine(rootPath, $"meta.wal");
    }

    public IEnumerable<CommitChanges> ReadWal(IFileSystem fileSystem, string rootPath, Dictionary<string, Type> modelTypes)
    {
        var path = GetWalPath(fileSystem, rootPath);

        if (!fileSystem.File.Exists(path))
            yield break;
        
        using var stream = fileSystem.File.OpenRead(path);

        var lengthBuffer = Array.Empty<byte>();
        var commitBuffer = Array.Empty<byte>();
        while (SerialisationEngine.Instance.TryDeserializeWal(
                   stream,
                   modelTypes,
                   ref lengthBuffer,
                   ref commitBuffer, 
                   out var changes))
        {
            yield return changes;
        }
    }

    public void WriteWal(
        IFileSystem fileSystem,
        string rootPath,
        CommitChanges commit)
    {
        var path = GetWalPath(fileSystem, rootPath);

        using var stream = fileSystem.File.Open(path, FileMode.Append);
        SerialisationEngine.Instance.SerializeWal(stream, commit);
        
        stream.Flush();
    }

    public void DeleteWal(IFileSystem fileSystem, string rootPath)
    {      
        var path = GetWalPath(fileSystem, rootPath);
        fileSystem.File.Delete(path);
    }
}