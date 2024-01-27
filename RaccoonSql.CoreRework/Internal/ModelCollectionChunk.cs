using System.Diagnostics;
using RaccoonSql.CoreRework.Internal.Persistence;

namespace RaccoonSql.CoreRework.Internal;

internal class ModelCollectionChunk<TModel>()
    where TModel : ModelBase
{
    private readonly List<TModel> _models = [];
    private readonly Dictionary<Guid, int> _modelIndexes = [];
    private int _operationCount;

    // ReSharper disable once ConvertToAutoPropertyWhenPossible
    internal int OperationCount
    {
        get => _operationCount;
        set => _operationCount = value;
    }

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

    public TModel? Find(Guid id)
    {
        if (!_modelIndexes.TryGetValue(id, out var modelIndex))
            return null;

        var model = _models[modelIndex];
        var proxy = ModelProxyFactory.GenerateProxy(model);
        return proxy;
    }

    public int Remove(Guid id)
    {
        var modelIndex = _modelIndexes[id];

        var lastIndex = _models.Count - 1;
        Debug.Assert(modelIndex <= lastIndex);
        if (modelIndex != lastIndex)
        {
            (_models[modelIndex], _models[lastIndex]) = (_models[lastIndex], _models[modelIndex]);
        }

        _models.RemoveAt(lastIndex);
        _modelIndexes.Remove(id);
        _operationCount++;

        return modelIndex;
    }
    
    public int ApplyChanges(Guid id, Dictionary<string, object?> modelChanges)
    {
        var modelIndex = _modelIndexes[id];
        var model = _models[modelIndex];
        AutoMapper.ApplyChanges(model, modelChanges);
        _operationCount++;
        return modelIndex;
    }

    public void Add(TModel model)
    {
        _modelIndexes[model.Id] = _models.Count;
        var clone = AutoMapper.Clone(model);
        _models.Add(clone);
        _operationCount++;
    }

    internal void Set(TModel? model, int index)
    {
        if (index == -1)
        {
            _models.Add(model!);
            return;
        }

        if (model is null)
        {
            var lastIndex = _models.Count - 1;
            (_models[lastIndex], _models[index]) = (_models[index], _models[lastIndex]);
            _models.RemoveAt(lastIndex);
        }
        else
        {
            _models[index] = model;
        }
    }
}