using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using MemoryPack;
using RaccoonSql.Core.Serialization;

namespace RaccoonSql.Core.Storage.Persistence.FileSystem;

public class FileSystemPersistenceEngine(
    IFileSystem fileSystem,
    string rootPath,
    ISerializationEngine serializationEngine) : IPersistenceEngine
{
    private static readonly Dictionary<string, int> FileWrites = new();
    private readonly Dictionary<string, string> _indexNames = new();
    private readonly Dictionary<string, string> _indexLogNames = new();
    private readonly Dictionary<string, string> _chunkNames = new();
    private readonly Dictionary<string, string> _chunkLogNames = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetIndexName(string setName)
    {
        return GetName(_indexNames, setName, "index");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetIndexLogName(string setName)
    {
        return GetName(_indexLogNames, setName, "idxlog");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetChunkName(string setName, uint chunkId)
    {
        return GetChunkName(_chunkNames, setName, chunkId, "chunk");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetChunkLogName(string setName, uint chunkId)
    {
        return GetChunkName(_chunkLogNames, setName, chunkId, "chnklog");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetName(Dictionary<string, string> names, string setName, string ending)
    {
        if (!names.TryGetValue(setName, out var result))
        {
            result = Path.Join(rootPath, $"{setName}.{ending}");
            names[setName] = result;
        }

        return result;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetChunkName(Dictionary<string, string> names, string setName, uint chunkId, string ending)
    {
        if (!names.TryGetValue(setName, out var result))
        {
            result = Path.Join(rootPath, $"{setName}.{chunkId}.{ending}");
            names[setName] = result;
        }

        return result;
    }

    private static bool ShouldWriteFile(string fileName, bool isIndex)
    {
        var fileWrites = FileWrites.GetValueOrDefault(fileName, 0);

        fileWrites++;

        var shouldWrite = false;
        if (fileWrites == (isIndex ? 10_000 : 100))
        {
            fileWrites = 0;
            shouldWrite = true;
        }

        FileWrites[fileName] = fileWrites;

        return shouldWrite;
    }

    private IEnumerable<IndexChange> LoadIndexChanges(string path)
    {
        if (!fileSystem.File.Exists(path)) return Enumerable.Empty<IndexChange>();

        var changes = new List<IndexChange>();

        using (var stream = fileSystem.File.OpenRead(path))
        {
            while (true)
            {
                try
                {
                    unsafe
                    {
                        var changeSize = sizeof(IndexChange);
                        var array = new byte[changeSize];
                        var buffer = new Span<byte>(array);
                        stream.ReadExactly(buffer);
                        var change = MemoryPackSerializer.Deserialize<IndexChange>(buffer);
                        changes.Add(change);
                    }
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }
        }

        fileSystem.File.Delete(path);
        return changes;
    }

    public ModelIndex LoadIndex(string setName)
    {
        var path = GetIndexName(setName);

        ModelIndex index;
        if (fileSystem.File.Exists(path))
        {
            var indexBytes = fileSystem.File.ReadAllBytes(path);
            index = MemoryPackSerializer.Deserialize<ModelIndex>(indexBytes)!;
        }
        else
        {
            index = new ModelIndex();
        }

        var changeFile = GetIndexLogName(setName);
        var changes = LoadIndexChanges(changeFile);
        foreach (var change in changes)
        {
            index.Apply(change);
        }

        WriteIndexInternal(path, changeFile, index);

        return index;
    }

    private void AppendIndexChange(string path, IndexChange indexChange)
    {
        using var stream = fileSystem.File.Open(path, FileMode.Append);
        stream.Write(MemoryPackSerializer.Serialize(indexChange));
    }

    public void FlushIndex(string setName, ModelIndex index)
    {
        var path = GetIndexName(setName);
        var changeFile = GetIndexLogName(setName);
        WriteIndexInternal(path, changeFile, index);
    }

    public void WriteIndex(string setName, ModelIndex index, IndexChange change)
    {
        var path = GetIndexName(setName);
        var changeFile = GetIndexLogName(setName);
        if (ShouldWriteFile(path, true))
        {
            WriteIndexInternal(path, changeFile, index);
        }
        else
        {
            AppendIndexChange(changeFile, change);
        }
    }

    private void WriteIndexInternal(string file, string changeFile, ModelIndex index)
    {
        fileSystem.File.WriteAllBytes(file, MemoryPackSerializer.Serialize(index));
        fileSystem.File.Delete(changeFile);
    }

    public ModelCollectionChunk LoadChunk(string setName, uint chunkId, Type type)
    {
        var path = GetChunkName(setName, chunkId);

        ModelCollectionChunk chunk;
        if (fileSystem.File.Exists(path))
        {
            using var chunkFileStream = fileSystem.File.OpenRead(path);
            chunk = (ModelCollectionChunk) serializationEngine.Deserialize(chunkFileStream, typeof(ModelCollectionChunk));
        }
        else
        {
            chunk = new ModelCollectionChunk();
        }

        var changeFile = GetChunkLogName(setName, chunkId);
        var changes = LoadChunkChanges(changeFile, type);
        foreach (var change in changes)
        {
            chunk.Apply(change);
        }

        WriteChunkInternal(path, changeFile, chunk);

        return chunk;
    }

    private IEnumerable<ChunkChange> LoadChunkChanges(string path, Type type)
    {
        if (!fileSystem.File.Exists(path)) return Enumerable.Empty<ChunkChange>();

        var changes = new List<ChunkChange>();

        using (var stream = fileSystem.File.OpenRead(path))
        {
            var lengthBuffer = BitConverter.GetBytes(0);
            var buffer = Array.Empty<byte>();
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
                
                var changeSize = BitConverter.ToInt32(lengthBuffer);

                if (buffer.Length < changeSize)
                {
                    buffer = new byte[changeSize];
                }
                
                stream.ReadExactly(buffer, 0, changeSize);
                var changeModel = MemoryPackSerializer.Deserialize<ChunkChangeModel>(buffer);
                using var ms = new MemoryStream(changeModel.SerializedModel);
                
                var change = new ChunkChange
                {
                    Add = changeModel.Add,
                    Offset = changeModel.Offset,
                    Model = (IModel) serializationEngine.Deserialize(ms, type),
                };
                changes.Add(change);
            }
        }
        
        fileSystem.File.Delete(path);
        return changes;
    }

    public void WriteChunk(string setName, uint chunkId, ModelCollectionChunk chunk, ChunkChange change)
    {
        var path = GetChunkName(setName, chunkId);
        var changeFile = GetChunkLogName(setName, chunkId);
        if (ShouldWriteFile(path, false))
        {
            WriteChunkInternal(path, changeFile, chunk);
        }
        else
        {
            AppendChunkChange(changeFile, change);
        }
    }

    private void WriteChunkInternal(string path, string changeFile, ModelCollectionChunk chunk)
    {
        using var chunkFileStream = fileSystem.File.OpenWrite(path);
        serializationEngine.Serialize(chunk).CopyTo(chunkFileStream);
        fileSystem.File.Delete(changeFile);
    }

    private void AppendChunkChange(string path, ChunkChange chunkChange)
    {
        using var stream = fileSystem.File.Open(path, FileMode.Append);
        using var serializedModel = serializationEngine.Serialize(chunkChange.Model);
        using var ms = new MemoryStream();
        serializedModel.CopyTo(ms);

        var changeModel = new ChunkChangeModel
        {
            SerializedModel = ms.ToArray(),
            Add = chunkChange.Add,
            Offset = chunkChange.Offset,
        };
        
        var buffer = MemoryPackSerializer.Serialize(changeModel);
        stream.Write(BitConverter.GetBytes(buffer.Length));
        stream.Write(buffer);
    }
}