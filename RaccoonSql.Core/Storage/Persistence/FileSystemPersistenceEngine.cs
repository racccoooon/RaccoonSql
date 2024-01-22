using System.Diagnostics;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using MemoryPack;
using RaccoonSql.Core.Serialization;

namespace RaccoonSql.Core.Storage.Persistence;

public class FileSystemPersistenceEngine(
    IFileSystem fileSystem,
    ModelStoreOptions options,
    ISerializationEngine serializationEngine)
{
    private readonly AppendFileStreamCache _appendFileStreamCache = new(fileSystem);
    private readonly Dictionary<(int, string), string> _chunkNames = new();
    private readonly Dictionary<(int, string), string> _chunkLogNames = new();
    public ISerializationEngine SerializationEngine { get; set; } = serializationEngine;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetChunkName(string setName, int chunkId)
    {
        return GetChunkName(_chunkNames, setName, chunkId, "chunk");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetChunkLogName(string setName, int chunkId)
    {
        return GetChunkName(_chunkLogNames, setName, chunkId, "chnklog");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetChunkName(Dictionary<(int, string), string> names, string setName, int chunkId, string ending)
    {
        if (!names.TryGetValue((chunkId, setName), out var result))
        {
            result = Path.Join(options.StoragePath, $"{setName}.{chunkId}.{ending}");
            names[(chunkId, setName)] = result;
        }

        return result;
    }

    public int GetChunkCount(string setName)
    {
        var length = fileSystem.Directory.GetFiles(options.StoragePath, $"{setName}.*.chunk").Length;
        return length == 0 ? 16 : length;
    }

    public ModelCollectionChunk<TModel> LoadChunk<TModel>(string setName, int chunkId, Type type)
        where TModel : ModelBase
    {
        var path = GetChunkName(setName, chunkId);

        ModelCollectionChunk<TModel> chunk;
        if (fileSystem.File.Exists(path))
        {
            using var chunkFileStream = fileSystem.File.OpenRead(path);
            chunk = (ModelCollectionChunk<TModel>) SerializationEngine.Deserialize(chunkFileStream, typeof(ModelCollectionChunk<TModel>));
        }
        else
        {
            chunk = new ModelCollectionChunk<TModel>();
        }
        chunk.Init(setName, chunkId, options, this);

        var changeFile = GetChunkLogName(setName, chunkId);
        var changes = LoadChunkChanges(changeFile, type);
        foreach (var change in changes)
        {
            switch (change)
            {
                case ChunkAddChange addChange:
                    chunk.Apply(addChange);
                    break;
                case ChunkUpdateChange addChange:
                    chunk.Apply(addChange);
                    break;
                case ChunkDeleteChange addChange:
                    chunk.Apply(addChange);
                    break;
                default:
                    throw new UnreachableException("unknown chunk change type");
            }
        }
        
        WriteChunkInternal(setName, chunkId, chunk);
        

        return chunk;
    }

    private IEnumerable<object> LoadChunkChanges(string path, Type type)
    {
        if (!fileSystem.File.Exists(path)) return Enumerable.Empty<object>();

        var changes = new List<object>();

        using (var stream = fileSystem.File.OpenRead(path))
        {
            var lengthBuffer = BitConverter.GetBytes(0);
            var buffer = Array.Empty<byte>();
            while (true)
            {
                var changeDiscriminator = stream.ReadByte();
                if (changeDiscriminator == -1) break;
                
                try
                {
                    stream.ReadExactly(lengthBuffer);
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                
                var changeSize = BitConverter.ToInt32(lengthBuffer);

                if (buffer.Length < changeSize)
                {
                    buffer = new byte[changeSize];
                }
                
                stream.ReadExactly(buffer, 0, changeSize);

                var changeType = changeDiscriminator switch
                {
                    0 => typeof(ChunkAddChange),
                    1 => typeof(ChunkUpdateChange),
                    2 => typeof(ChunkDeleteChange),
                    _ => throw new UnreachableException("unknown chunk change discriminator"),
                };
                
                var change = MemoryPackSerializer.Deserialize(changeType, buffer);

                if (change is null) 
                    throw new UnreachableException("deserializing chunk change failed");
                
                changes.Add(change);
            }
        }
        
        _appendFileStreamCache.DeleteFile(path);
        return changes;
    }

    public void WriteChunk<TModel>(string setName, int chunkId, ModelCollectionChunk<TModel> chunk)
        where TModel : ModelBase
    {
        WriteChunkInternal(setName, chunkId, chunk);
    }

    private void WriteChunkInternal<TModel>(string setName, int chunkId, ModelCollectionChunk<TModel> chunk)
        where TModel : ModelBase
    {
        var path = GetChunkName(setName, chunkId);
        var changeFile = GetChunkLogName(setName, chunkId);
        using var chunkFileStream = fileSystem.File.Open(path, FileMode.Create);
        SerializationEngine.Serialize(chunkFileStream, chunk, chunk.GetType());
        _appendFileStreamCache.DeleteFile(changeFile);
    }

    public void AppendChunk(string setName, int chunkId, ChunkAddChange change)
    {
        var buffer = MemoryPackSerializer.Serialize(change);
        AppendChunkChange(setName, chunkId, 0, buffer);
    }

    public void AppendChunk(string setName, int chunkId, ChunkUpdateChange change)
    {
        var buffer = MemoryPackSerializer.Serialize(change);
        AppendChunkChange(setName, chunkId, 1, buffer);
    }

    public void AppendChunk(string setName, int chunkId, ChunkDeleteChange change)
    {
        var buffer = MemoryPackSerializer.Serialize(change);
        AppendChunkChange(setName, chunkId, 2, buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendChunkChange(string setName, int chunkId, byte changeDiscriminator, byte[] serializedChange)
    {
        var path = GetChunkLogName(setName, chunkId);
        var stream = _appendFileStreamCache.GetAppendStream(path);
        
        stream.WriteByte(changeDiscriminator);
        stream.Write(BitConverter.GetBytes(serializedChange.Length));
        stream.Write(serializedChange);
        
        stream.Flush();
    }
}