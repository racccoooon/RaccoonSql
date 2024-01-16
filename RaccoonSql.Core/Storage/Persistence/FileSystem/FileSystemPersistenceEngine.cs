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
    private readonly FileManager _fileManager = new(fileSystem);
    private static readonly Dictionary<string, int> FileWrites = new();
    private readonly Dictionary<(uint, string), string> _chunkNames = new();
    private readonly Dictionary<(uint, string), string> _chunkLogNames = new();

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
    private string GetChunkName(Dictionary<(uint, string), string> names, string setName, uint chunkId, string ending)
    {
        if (!names.TryGetValue((chunkId, setName), out var result))
        {
            result = Path.Join(rootPath, $"{setName}.{chunkId}.{ending}");
            names[(chunkId, setName)] = result;
        }

        return result;
    }

    private static bool ShouldWriteFile(string fileName)
    {
        var fileWrites = FileWrites.GetValueOrDefault(fileName, 0);

        fileWrites++;

        var shouldWrite = false;
        if (fileWrites == 100)
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

        _fileManager.Delete(path);
        return changes;
    }

    public uint GetChunkCount(string setName)
    {
        var length = (uint)fileSystem.Directory.GetFiles(rootPath, $"{setName}.*.chunk").Length;
        return length == 0 ? 16 : length;
    }

    public ModelCollectionChunk<TModel> LoadChunk<TModel>(string setName, uint chunkId, Type type)
        where TModel : IModel
    {
        var path = GetChunkName(setName, chunkId);

        ModelCollectionChunk<TModel> chunk;
        if (fileSystem.File.Exists(path))
        {
            using var chunkFileStream = fileSystem.File.OpenRead(path);
            chunk = (ModelCollectionChunk<TModel>) serializationEngine.Deserialize(chunkFileStream, typeof(ModelCollectionChunk<TModel>));
        }
        else
        {
            chunk = new ModelCollectionChunk<TModel>();
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
        
        _fileManager.Delete(path);
        return changes;
    }

    public void WriteChunk<TModel>(string setName, uint chunkId, ModelCollectionChunk<TModel> chunk, ChunkChange change)
        where TModel : IModel
    {
        var path = GetChunkName(setName, chunkId);
        var changeFile = GetChunkLogName(setName, chunkId);
        if (ShouldWriteFile(path))
        {
            WriteChunkInternal(path, changeFile, chunk);
        }
        else
        {
            AppendChunkChange(changeFile, change);
        }
    }

    private void WriteChunkInternal<TModel>(string path, string changeFile, ModelCollectionChunk<TModel> chunk)
        where TModel : IModel
    {
        using var chunkFileStream = fileSystem.File.Open(path, FileMode.Create);
        serializationEngine.Serialize(chunkFileStream, chunk, chunk.GetType());
        _fileManager.Delete(changeFile);
    }

    private void AppendChunkChange(string path, ChunkChange chunkChange)
    { 
        var stream = _fileManager.GetAppend(path);
        
        using var ms = new MemoryStream();
        serializationEngine.Serialize(ms, chunkChange.Model, chunkChange.Model.GetType());

        var changeModel = new ChunkChangeModel
        {
            SerializedModel = ms.ToArray(),
            Add = chunkChange.Add,
            Offset = chunkChange.Offset,
        };
        
        var buffer = MemoryPackSerializer.Serialize(changeModel);
        stream.Write(BitConverter.GetBytes(buffer.Length));
        stream.Write(buffer);
        
        stream.Flush();
    }
}

public class FileManager(IFileSystem fileSystem)
{
    private readonly Dictionary<string, Stream> _streams = new();

    ~FileManager()
    {
        foreach (var stream in _streams.Values)
        {
            stream.Flush();
            stream.Close();
        }
    }
    
    public Stream GetAppend(string path)
    {
        if (!_streams.TryGetValue(path, out var stream))
        {
            stream = fileSystem.File.Open(path, FileMode.Append);
            _streams[path] = stream;
        }

        return stream;
    }

    public void Delete(string path)
    {
        if (_streams.Remove(path, out var stream))
        {
            stream.Dispose();
            fileSystem.File.Delete(path);
        }
    }
}