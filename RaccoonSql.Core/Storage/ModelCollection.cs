namespace RaccoonSql.Core.Storage;

internal class ModelCollection
{
    private const int ModelsPerChunk = 64;
    private const int InitialChunkCount = 32;
    private const int RehashThreshold = 66;
    
    private readonly string _name;
    private ModelIndex _index = new();
    private List<ModelCollectionChunk> _chunks = new();
    private long _modelCount;

    public ModelCollection(string name)
    {
        _name = name;
        
        //TODO: load from file
        
        if (_chunks.Count == 0)
        {
            _chunks.AddRange(Enumerable.Range(0, InitialChunkCount).Select(_ => new ModelCollectionChunk()));
        }
    }

    public StorageInfo GetStorageInfo(Guid id)
    {
        var chunkInfo = _index.GetChunkInfo(id);
        return new StorageInfo
        {
            CollectionName = _name,
            ChunkInfo = chunkInfo,
        };
    }

    public void Write(IModel data, ChunkInfo? chunkInfo)
    {
        if (chunkInfo is null)
        {
            RehashIfNeeded();
            chunkInfo = DetermineChunk(data.Id);
        }
        
        var chunk = _chunks[chunkInfo.Value.chunkId];
        chunk.WriteModel(chunkInfo.Value.offset, data);
        _index.Add(data.Id, chunkInfo.Value);
    }

    private ChunkInfo DetermineChunk(Guid id)
    {
        var value = BitConverter.ToUInt32(id.ToByteArray(), 12);
        var chunkId = value % _chunks.Count;
        var chunk = _chunks[(int)chunkId];
        return new ChunkInfo { chunkId = (int)chunkId, offset = chunk.ModelCount };
    }

    private void RehashIfNeeded()
    {
        if (_modelCount * 100 / (_chunks.Count * ModelsPerChunk) <= RehashThreshold) return;
        
        var newChunkCount = _chunks.Count * 2;
        var newChunks = new List<ModelCollectionChunk>(newChunkCount);
        newChunks.AddRange(Enumerable.Range(0, newChunkCount).Select(_ => new ModelCollectionChunk()));

        var oldChunks = _chunks;
            
        _index = new ModelIndex();
        _chunks = newChunks;
        _modelCount = 0;
            
        foreach (var oldChunk in oldChunks)
        {
            foreach (var model in oldChunk)
            {
                Write(model, null);
            }
        }
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

    public IEnumerable<IStorageInfo> GetStorageInfos()
    {
        return _chunks.SelectMany(x => x)
            .Select(x => GetStorageInfo(x.Id));
    }
}