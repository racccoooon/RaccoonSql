using System.IO.Abstractions;
using System.Runtime.CompilerServices;

namespace RaccoonSql.CoreRework.Internal.Persistence;

internal interface IPersistenceEngine
{
    ModelCollectionMetadata? ReadMetadata(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName);

    void WriteMetadata(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        ModelCollectionMetadata metadata);

    ModelCollectionChunk<TModel>? ReadChunk<TModel>(
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
}

internal class PersistenceEngine : IPersistenceEngine
{
    public static readonly IPersistenceEngine Instance = new PersistenceEngine();

    //TODO: refactor this whole class to take the filesystem in its constructor and not be static
    private AppendFileStreamCache _appendFileStreamCache = new(new FileSystem());
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetMetadataPath(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName)
    {
        return fileSystem.Path.Combine(rootPath, $"{collectionName}.meta");
    }
    
    public ModelCollectionMetadata? ReadMetadata(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName)
    {
        var path = GetMetadataPath(fileSystem, rootPath, collectionName);

        if (!fileSystem.File.Exists(path))
            return null;

        using var stream = fileSystem.File.OpenRead(path);
        return SerialisationEngine.Instance.DeserializeMetadata(stream);
    }

    public void WriteMetadata(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        ModelCollectionMetadata metadata)
    {
        var path = GetMetadataPath(fileSystem, rootPath, collectionName);

        using var stream = fileSystem.File.Open(path, FileMode.Create);
        SerialisationEngine.Instance.SerializeMetadata(stream, metadata);
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

    public ModelCollectionChunk<TModel>? ReadChunk<TModel>(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        int chunkIndex)
        where TModel : ModelBase
    {
        var path = GetChunkPath(fileSystem, rootPath, collectionName, chunkIndex);

        if (!fileSystem.File.Exists(path))
            return null;

        using var stream = fileSystem.File.OpenRead(path);
        var chunkData = SerialisationEngine.Instance.DeserializeChunkData<TModel>(stream);

        return new ModelCollectionChunk<TModel>(chunkData);
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
        
        collectionChunk.OperationCount = 0;
    }
}