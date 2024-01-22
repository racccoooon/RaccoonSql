using System.Text.Json.Serialization;
using MemoryPack;
using RaccoonSql.Core.Serialization;
using RaccoonSql.Core.Storage.Persistence;

namespace RaccoonSql.Core.Storage;

public class ModelCollectionChunk<TModel> where TModel : ModelBase
{
    public List<TModel> Models { get; set; } = new();

    [JsonIgnore]
    public int ModelCount => Models.Count;

    private int _changes;
    private string _setName;
    private int _chunkId;
    private ModelStoreOptions _options;
    private FileSystemPersistenceEngine _persistenceEngine;

    [JsonIgnore] public Dictionary<Guid, int> ModelIndexes { get; set; } = new();

    internal void Init(string setName,
        int chunkId,
        ModelStoreOptions options,
        FileSystemPersistenceEngine persistenceEngine)
    {
        for (int i = 0; i < Models.Count; i++)
        {
            ModelIndexes[Models[i].Id] = i;
        }
        
        _setName = setName;
        _chunkId = chunkId;
        _options = options;
        _persistenceEngine = persistenceEngine;
    }

    public TModel GetModel(Guid id)
    {
        return Models[ModelIndexes[id]];
    }

    public void InsertModel(TModel model)
    {
        ModelIndexes[model.Id] = Models.Count;
        Models.Add(model);

        _changes++;

        if (_changes >= _options.ChunkFlushThreshold)
        {
            _changes = 0;
            _persistenceEngine.WriteChunk(_setName, _chunkId, this);
        }
        else
        {
            using var ms = new MemoryStream();
            _persistenceEngine.SerializationEngine.Serialize(ms, model, typeof(TModel));
            var change = new ChunkAddChange
            {
                SerializedModel = ms.ToArray(),
            };
            _persistenceEngine.AppendChunk(_setName, _chunkId, change);
        }
    }

    public void UpdateModel(int index, TModel model)
    {
        _changes++;

        if (_changes >= _options.ChunkFlushThreshold)
        {
            _changes = 0;
            _persistenceEngine.WriteChunk(_setName, _chunkId, this);
        }
        else
        {
            using var ms = new MemoryStream();
            _persistenceEngine.SerializationEngine.Serialize(ms, model, typeof(TModel));
            var change = new ChunkUpdateChange
            {
                SerializedModel = ms.ToArray(),
                Index = index,
            };
            _persistenceEngine.AppendChunk(_setName, _chunkId, change);   
        }
    }

    public void DeleteModel(Guid id)
    {
        var index = ModelIndexes[id];
        if (index < Models.Count - 1)
        {
            Models[index] = Models[^1];
        }

        Models.RemoveAt(Models.Count - 1);
        ModelIndexes.Remove(id);

        _changes++;
        
        if (_changes >= _options.ChunkFlushThreshold)
        {
            _changes = 0;
            _persistenceEngine.WriteChunk(_setName, _chunkId, this);
        }
        else
        {
            var change = new ChunkDeleteChange
            {
                Index = index,
            };
            _persistenceEngine.AppendChunk(_setName, _chunkId, change);   
        }
    }

    public void Apply(ChunkAddChange change)
    {
        using var ms = new MemoryStream(change.SerializedModel);
        var model = (TModel)_persistenceEngine.SerializationEngine.Deserialize(ms, typeof(TModel));
        ModelIndexes[model.Id] = Models.Count;
        Models.Add(model);
    }

    public void Apply(ChunkUpdateChange change)
    {
        using var ms = new MemoryStream(change.SerializedModel);
        var model = (TModel)_persistenceEngine.SerializationEngine.Deserialize(ms, typeof(TModel));
        Models[change.Index] = model;
    }

    public void Apply(ChunkDeleteChange change)
    {
        ModelIndexes.Remove(Models[change.Index].Id);
        Models[change.Index] = Models[^1];
    }
}