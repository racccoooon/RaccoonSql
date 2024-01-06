using System.IO.Abstractions;
using RaccoonSql.Core.Serialization;

namespace RaccoonSql.Core.Storage.Persistence.FileSystem;

public class FileSystemPersistenceEngine(
    IFileSystem fileSystem,
    string rootPath,
    ISerializationEngine serializationEngine) : IPersistenceEngine
{
    public ModelIndex LoadIndex(string setName)
    {
        var path = Path.Join(rootPath, $"{setName}.index");
        try
        {
            using var indexFileStream = fileSystem.File.OpenRead(path);
            return serializationEngine.Deserialize<ModelIndex>(indexFileStream);
        }
        catch (FileNotFoundException)
        {
            return new ModelIndex();
        }
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

    public void WriteIndex(string setName, ModelIndex index)
    {
        var path = Path.Join(rootPath, $"{setName}.index");
        using var indexFileStream = fileSystem.File.OpenWrite(path);
        serializationEngine.Serialize(index).CopyTo(indexFileStream);
    }

    public void WriteChunk(string setName, int chunkId, ModelCollectionChunk chunk)
    {
        var path = Path.Join(rootPath, $"{setName}.{chunkId}.chunk");
        using var indexFileStream = fileSystem.File.OpenWrite(path);
        serializationEngine.Serialize(chunk).CopyTo(indexFileStream);
    }
}