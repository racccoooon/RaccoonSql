using System.Diagnostics;
using System.IO.Abstractions;
using RaccoonSql.CoreRework.Internal.Persistence;
using RaccoonSql.CoreRework.Internal.Utils;

namespace RaccoonSql.CoreRework.Internal;

internal class ModelCollectionChunk<TModel>()
    where TModel : ModelBase
{
    private readonly List<TModel> _models = [];
    private readonly Dictionary<Guid, int> _modelIndexes = [];

    private bool _isDirty;

    internal ModelCollectionChunk(ChunkData<TModel> data)
        : this()
    {
        _models = data.Models;
        _modelIndexes = data.ModelIndexes;
    }

    internal ChunkData<TModel> GetData()
    {
        return new ChunkData<TModel>
        {
            Models = _models,
            ModelIndexes = _modelIndexes,
        };
    }

    public IEnumerable<TModel> Models => _models;
    public int ModelCount => _models.Count;

    public void Remove(Guid id)
    {
        var modelIndex = _modelIndexes[id];
        var lastIndex = _models.Count - 1;
        Debug.Assert(modelIndex <= lastIndex);
        
        if (modelIndex != lastIndex)
        {
            var lastIndexGuid = _models[lastIndex].Id;
            (_models[modelIndex], _models[lastIndex]) = (_models[lastIndex], _models[modelIndex]);
            _modelIndexes[lastIndexGuid] = modelIndex;
        }

        _models.RemoveAt(lastIndex);
        _modelIndexes.Remove(id);
        _isDirty = true;
    }

    public TModel? Find(Guid id)
    {
        if (!_modelIndexes.TryGetValue(id, out var modelIndex))
            return null;

        var model = _models[modelIndex];
        var proxy = ModelProxyFactory.GenerateProxy(model);
        return proxy;
    }

    public void ApplyChanges(Guid id, TModel modelChanges)
    {
        var modelIndex = _modelIndexes[id];
        var model = _models[modelIndex];
        AutoMapper.Map(model, modelChanges);
        _isDirty = true;
    }

    public void Add(TModel model)
    {
        _modelIndexes[model.Id] = _models.Count;
        var clone = AutoMapper.Clone(model);
        _models.Add(clone);
        _isDirty = true;
    }

    public void Persist(IFileSystem fileSystem, string rootPath, string collectionName, int chunkIndex)
    {
        if (!_isDirty) return;
        PersistenceEngine.Instance.WriteChunk(fileSystem, rootPath, collectionName, chunkIndex, this);
        _isDirty = false;
    }
}