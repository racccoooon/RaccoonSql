using System.IO.Abstractions;
using MemoryPack;
using RaccoonSql.Core.Serialization;

namespace RaccoonSql.Core.Storage.Persistence.FileSystem;

public class FileSystemPersistenceEngine(
    IFileSystem fileSystem,
    string rootPath,
    ISerializationEngine serializationEngine) : IPersistenceEngine
{
    private IEnumerable<IndexChange> LoadChanges(string setName)
    {
        var path = Path.Join(rootPath, $"{setName}.idxlog");
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
                catch(EndOfStreamException)
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
        var path = Path.Join(rootPath, $"{setName}.index");
        
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

        var changes = LoadChanges(setName);
        foreach (var change in changes)
        {
            index.Apply(change);
        }

        WriteIndex(setName, index);

        return index;
    }

    public void AppendIndexChange(string setName, IndexChange indexChange)
    {
        var path = Path.Join(rootPath, $"{setName}.idxlog");
        using var stream = fileSystem.File.Open(path, FileMode.Append);
        stream.Write(MemoryPackSerializer.Serialize(indexChange));
    }

    public void WriteIndex(string setName, ModelIndex index)
    {
        var path = Path.Join(rootPath, $"{setName}.index");
        fileSystem.File.WriteAllBytes(path, MemoryPackSerializer.Serialize(index));

        var changeFile = Path.Join(rootPath, $"{setName}.idxlog");
        fileSystem.File.Delete(changeFile);
    }

    public ModelCollectionChunk LoadChunk(string setName, int chunkId)
    {
        var path = Path.Join(rootPath, $"{setName}.{chunkId}.chunk");
        try
        {
            using var indexFileStream = fileSystem.File.OpenRead(path);
            return serializationEngine.Deserialize<ModelCollectionChunk>(indexFileStream);
        }
        catch (FileNotFoundException)
        {
            return new ModelCollectionChunk();
        }
    }

    public void WriteChunk(string setName, int chunkId, ModelCollectionChunk chunk)
    {
        var path = Path.Join(rootPath, $"{setName}.{chunkId}.chunk");
        using var indexFileStream = fileSystem.File.OpenWrite(path);
        serializationEngine.Serialize(chunk).CopyTo(indexFileStream);
    }
}