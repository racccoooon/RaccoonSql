namespace RaccoonSql.Core.Storage.FileSystem;

internal class ModelCollection
{
    private readonly string _name;
    private readonly ModelIndex _index = new();
    private readonly List<ModelCollectionChunk> _chunks = new();

    public ModelCollection(string name)
    {
        _name = name;
        
        //TODO: load from file
        
        if (_chunks.Count == 0)
        {
            _chunks.AddRange(Enumerable.Range(0, 16).Select(_ => new ModelCollectionChunk()));
        }
    }

    public FileSystemStorageInfo GetStorageInfo(Guid id)
    {
        var chunkInfo = _index.GetChunkInfo(id);
        return new FileSystemStorageInfo
        {
            CollectionName = _name,
            ChunkInfo = chunkInfo,
            Id = id,
        };
    }

    public void Write(IModel data, Guid id, ChunkInfo? chunkInfo)
    {
        var chunkInfoValue = chunkInfo ?? DetermineChunk(id);
        var chunk = _chunks[chunkInfoValue.chunkId];
        chunk.WriteModel(chunkInfoValue.offset, data);
        _index.Add(id, chunkInfoValue);
    }

    private ChunkInfo DetermineChunk(Guid id)
    {
        //TODO: rehash
        var value = BitConverter.ToUInt32(id.ToByteArray(), 12);
        var chunkId = value % _chunks.Count;
        var chunk = _chunks[(int)chunkId];
        return new ChunkInfo { chunkId = (int)chunkId, offset = chunk.ModelCount };
    }

    public IModel Read(ChunkInfo chunkInfo)
    {
        var chunk = _chunks[chunkInfo.chunkId];
        return chunk.GetModel(chunkInfo.offset);
    }

    public void Delete(ChunkInfo chunkInfo)
    {
        var chunk = _chunks[chunkInfo.chunkId];
        var model = chunk.GetModel(chunkInfo.offset);
        _index.Delete(model.Id);
        chunk.DeleteModel(chunkInfo.offset);
    }
}