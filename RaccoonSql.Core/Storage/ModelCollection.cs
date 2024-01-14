using RaccoonSql.Core.Storage.Persistence;

namespace RaccoonSql.Core.Storage;

internal class ModelCollection
{
    private const int ModelsPerChunk = 64;
    private const int RehashThreshold = 66;
    
    private readonly string _name;
    private readonly IPersistenceEngine _persistenceEngine;
    private ModelIndex _index;
    private ModelCollectionChunk[] _chunks;

    public ModelCollection(string name, IPersistenceEngine persistenceEngine, Type type)
    {
        _name = name;
        _persistenceEngine = persistenceEngine;

        _index = _persistenceEngine.LoadIndex(name);

        _chunks = Enumerable.Range(0, _index.ChunkCount)
            .Select(i => persistenceEngine.LoadChunk(name, i, type))
            .ToArray();
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
        var writeNew = chunkInfo is null;
        if (chunkInfo is null)
        {
            RehashIfNeeded();
            chunkInfo = DetermineChunk(data.Id);
        }
        
        var chunk = _chunks[chunkInfo.Value.ChunkId];
        
        var change = chunk.WriteModel(chunkInfo.Value.Offset, data);
        _persistenceEngine.WriteChunk(_name, chunkInfo.Value.ChunkId, chunk, change);

        if (!writeNew) return;
        
        _index.Set(data.Id, chunkInfo.Value);
        _persistenceEngine.WriteIndex(_name, _index, new IndexChange
        {
            Id = data.Id,
            ChangeType = IndexChangeType.Set,
            ChunkInfo = chunkInfo.Value,
        });
    }

    private ChunkInfo DetermineChunk(Guid id)
    {
        var value = BitConverter.ToUInt32(id.ToByteArray(), 12);
        var chunkId = value % _index.ChunkCount;
        var chunk = _chunks[(int)chunkId];
        return new ChunkInfo { ChunkId = (int)chunkId, Offset = chunk.ModelCount };
    }

    private void RehashIfNeeded()
    {
        if (_index.Index.Count * 100 / (_index.ChunkCount * ModelsPerChunk) <= RehashThreshold) return;
        
        var newChunkCount = _index.ChunkCount * 2;
        var newChunks = Enumerable.Range(0, newChunkCount)
            .Select(_ => new ModelCollectionChunk())
            .ToArray();

        var oldChunks = _chunks;
            
        _index = new ModelIndex
        {
            ChunkCount = newChunkCount
        };
        _chunks = newChunks;
            
        foreach (var oldChunk in oldChunks)
        {
            foreach (var model in oldChunk.Models)
            {
                var chunkInfo = DetermineChunk(model.Id);
                var chunk = _chunks[chunkInfo.ChunkId];
                chunk.WriteModel(chunkInfo.Offset, model);
                _index.Set(model.Id, chunkInfo);
            }
        }

        _persistenceEngine.FlushIndex(_name, _index);
    }

    public IModel Read(ChunkInfo chunkInfo)
    {
        var chunk = _chunks[chunkInfo.ChunkId];
        return chunk.GetModel(chunkInfo.Offset);
    }

    public void Delete(ChunkInfo chunkInfo)
    {
        var chunk = _chunks[chunkInfo.ChunkId];
        var model = chunk.GetModel(chunkInfo.Offset);
        
        _index.Delete(model.Id);
        _persistenceEngine.WriteIndex(_name, _index, new IndexChange
        {
            Id = model.Id,
            ChangeType = IndexChangeType.Remove,
            ChunkInfo = chunkInfo,
        });
        
        var movedModelId = chunk.DeleteModel(chunkInfo.Offset);
        if (movedModelId != null)
        {
            _index.Set(movedModelId.Value, chunkInfo);
            _persistenceEngine.WriteIndex(_name, _index, new IndexChange
            {
                Id = model.Id,
                ChangeType = IndexChangeType.Set,
                ChunkInfo = chunkInfo,
            });
        }
    }

    public IEnumerable<IStorageInfo> GetStorageInfos()
    {
        return _chunks.SelectMany(x => x.Models)
            .Select(x => GetStorageInfo(x.Id));
    }
}