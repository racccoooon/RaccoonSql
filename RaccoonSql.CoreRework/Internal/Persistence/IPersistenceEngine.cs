using System.Buffers;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using MemoryPack;
using RaccoonSql.CoreRework.Internal.Utils;

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

    void WriteChunkChanges<TModel>(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        int chunkIndex,
        LinkedList<TModel> added,
        LinkedList<(TModel model, int index)> updated,
        LinkedList<int> deleted
    ) where TModel : ModelBase;
}

internal class PersistenceEngine : IPersistenceEngine
{
    public static readonly IPersistenceEngine Instance = new PersistenceEngine();

    private readonly AppendFileStreamCache _appendFileStreamCache = new();

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

    public ModelCollectionChunk<TModel> ReadChunk<TModel>(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        int chunkIndex)
        where TModel : ModelBase
    {
        var path = GetChunkPath(fileSystem, rootPath, collectionName, chunkIndex);
        var walPath = GetChunkWalPath(fileSystem, rootPath, collectionName, chunkIndex);

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

        // ReSharper disable once InvertIf
        if (fileSystem.File.Exists(walPath))
        {
            using var stream = fileSystem.File.OpenRead(walPath);
            var lengthBuffer = new byte[4];
            var dataBuffer = new byte[1024];

            while (true)
            {
                try
                {
                    stream.ReadExactly(lengthBuffer);
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                var length = BitConverter.ToInt32(lengthBuffer, 0);
                if (dataBuffer.Length < length)
                {
                    dataBuffer = new byte[length * 2];
                }

                stream.ReadExactly(dataBuffer, 0, length);

                var readOnlySequence = new ReadOnlySequence<byte>(dataBuffer, 0, length);
                var change = MemoryPackSerializer.Deserialize<IChange>(readOnlySequence)!;

                switch (change)
                {
                    case AddChange addChange:
                    {
                        using var ms = new MemoryStream(addChange.Serialized);
                        var addModel = SerialisationEngine.Instance.DeserializeModel<TModel>(ms);
                        result.Set(addModel, -1);
                        break;
                    }

                    case UpdateChange updateChange:
                    {
                        using var ms = new MemoryStream(updateChange.Serialized);
                        var addModel = SerialisationEngine.Instance.DeserializeModel<TModel>(ms);
                        result.Set(addModel, updateChange.Index);
                        break;
                    }

                    case DeleteChange deleteChange:
                    {
                        result.Set(null, deleteChange.Index);
                        break;
                    }
                }
            }
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

        var walPath = GetChunkWalPath(fileSystem, rootPath, collectionName, chunkIndex);
        _appendFileStreamCache.DeleteFile(fileSystem, walPath);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetChunkWalPath(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        int chunkIndex)
    {
        return fileSystem.Path.Combine(rootPath, $"{collectionName}.{chunkIndex}.chunk.wal");
    }

    public void WriteChunkChanges<TModel>(
        IFileSystem fileSystem,
        string rootPath,
        string collectionName,
        int chunkIndex,
        LinkedList<TModel> added,
        LinkedList<(TModel model, int index)> updated,
        LinkedList<int> deleted)
        where TModel : ModelBase
    {
        var path = GetChunkWalPath(fileSystem, rootPath, collectionName, chunkIndex);

        var stream = _appendFileStreamCache.GetAppendStream(fileSystem, path);

        foreach (var model in added)
        {
            AppendAddChange(stream, model);
        }

        foreach (var modelAndIndex in updated)
        {
            AppendUpdateChange(stream, modelAndIndex);
        }

        foreach (var index in deleted)
        {
            AppendDeleteChange(stream, index);
        }

        stream.Flush();
    }

    private static void AppendAddChange<TModel>(Stream stream, TModel model)
        where TModel : ModelBase
    {
        using var ms = new MemoryStream();
        SerialisationEngine.Instance.SerializeModel(ms, model);
        var change = new AddChange
        {
            Serialized = ms.ToArray(),
        };

        AppendChange(stream, change);
    }

    private static void AppendUpdateChange<TModel>(Stream stream, (TModel model, int index) modelAndIndex)
        where TModel : ModelBase
    {
        using var ms = new MemoryStream();
        SerialisationEngine.Instance.SerializeModel(ms, modelAndIndex.model);
        var change = new UpdateChange
        {
            Serialized = ms.ToArray(),
            Index = modelAndIndex.index,
        };

        AppendChange(stream, change);
    }

    private static void AppendDeleteChange(Stream stream, int index)
    {
        var change = new DeleteChange
        {
            Index = index,
        };

        AppendChange(stream, change);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendChange(Stream stream, IChange change)
    {
        var buffer = MemoryPackSerializer.Serialize(change);
        stream.Write(BitConverter.GetBytes(buffer.Length));
        stream.Write(buffer, 0, buffer.Length);
    }
}