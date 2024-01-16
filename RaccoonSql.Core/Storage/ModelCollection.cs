using RaccoonSql.Core.Storage.Persistence;
using RaccoonSql.Core.Utils;

namespace RaccoonSql.Core.Storage;

internal class ModelCollection<TModel>
    where TModel : IModel
{
    private const int ModelsPerChunk = 64;
    private const int RehashThreshold = 66;

    private readonly string _name;
    private readonly IPersistenceEngine _persistenceEngine;
    private ModelCollectionChunk<TModel>[] _chunks;
    private uint _modelCount;

    public ModelCollection(string name, IPersistenceEngine persistenceEngine)
    {
        _name = name;
        _persistenceEngine = persistenceEngine;

        var chunkCount = _persistenceEngine.GetChunkCount(name);

        _chunks = new ModelCollectionChunk<TModel>[chunkCount];
        for (uint i = 0; i < _chunks.Length; i++)
        {
            var chunk = persistenceEngine.LoadChunk<TModel>(name, i, typeof(TModel));
            _chunks[i] = chunk;
            _modelCount += chunk.ModelCount;
        }
    }

    public StorageInfo GetStorageInfo(Guid id)
    {
        unsafe
        {
            var guidBuffer = new GuidBuffer(id);
            var chunkId = guidBuffer.Int[3] % (uint)_chunks.Length;
            var chunk = _chunks[chunkId];
            var hasModel = chunk.ModelOffset.ContainsKey(id);
            ChunkInfo? chunkInfo = hasModel
                ? new ChunkInfo
                {
                    ChunkId = chunkId,
                    Offset = chunk.ModelOffset[id],
                }
                : null;
            return new StorageInfo
            {
                CollectionName = _name,
                ChunkInfo = chunkInfo,
            };
        }
    }

    public void Write(TModel data, ChunkInfo? chunkInfo)
    {
        if (chunkInfo is null)
        {
            _modelCount++;
            RehashIfNeeded();
            chunkInfo = DetermineChunk(data.Id);
        }

        var chunk = _chunks[chunkInfo.Value.ChunkId];

        var change = chunk.WriteModel(chunkInfo.Value.Offset, data);
        _persistenceEngine.WriteChunk(_name, chunkInfo.Value.ChunkId, chunk, change);
    }

    private ChunkInfo DetermineChunk(Guid id)
    {
        unsafe
        {
            var guidBuffer = new GuidBuffer(id);
            var chunkId = guidBuffer.Int[3] % (uint)_chunks.Length;
            var chunk = _chunks[chunkId];
            return new ChunkInfo { ChunkId = chunkId, Offset = chunk.ModelCount };
        }
    }

    private void RehashIfNeeded()
    {
        if (_modelCount * 100 / (_chunks.Length * ModelsPerChunk) <= RehashThreshold) return;

        var newChunkCount = _chunks.Length * 2;
        var newChunks = new ModelCollectionChunk<TModel>[newChunkCount];
        for (var i = 0; i < newChunks.Length; i++)
        {
            newChunks[i] = new ModelCollectionChunk<TModel>();
        }

        var oldChunks = _chunks;

        _chunks = newChunks;

        foreach (var oldChunk in oldChunks)
        {
            foreach (var model in oldChunk.Models)
            {
                var chunkInfo = DetermineChunk(model.Id);
                var chunk = _chunks[chunkInfo.ChunkId];
                chunk.WriteModel(chunkInfo.Offset, model);
            }
        }
    }

    public TModel Read(ChunkInfo chunkInfo)
    {
        var chunk = _chunks[chunkInfo.ChunkId];
        return chunk.GetModel(chunkInfo.Offset);
    }

    public void Delete(ChunkInfo chunkInfo)
    {
        var chunk = _chunks[chunkInfo.ChunkId];
        var model = chunk.GetModel(chunkInfo.Offset);

        var movedModelId = chunk.DeleteModel(chunkInfo.Offset);
    }

    public IEnumerable<StorageInfo> GetStorageInfos()
    {
        return _chunks.SelectMany(x => x.Models)
            .Select(x => GetStorageInfo(x.Id));
    }
}