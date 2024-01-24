namespace RaccoonSql.CoreRework.Internal;

internal class ModelCollectionChunk<TModel>
    where TModel : ModelBase
{
    private readonly List<TModel> _models = [];
    private readonly Dictionary<Guid, int> _modelIndexes = [];

    public IEnumerable<TModel> Models => _models;

    public TModel? Find(Guid id)
    {
        if (!_modelIndexes.TryGetValue(id, out var modelIndex))
            return null;

        var model = _models[modelIndex];
        var proxy = ModelProxyFactory.GenerateProxy(model);
        return proxy;
    }

    public void Remove(Guid id)
    {
        var modelIndex = _modelIndexes[id];

        var lastIndex = _models.Count - 1;
        if (modelIndex != lastIndex)
        {
            (_models[modelIndex], _models[lastIndex]) = (_models[lastIndex], _models[modelIndex]);
        }
        
        _models.RemoveAt(lastIndex);
        _modelIndexes.Remove(id);
    }

    public void ApplyChanges(Guid id, Dictionary<string ,object?> modelChanges)
    {
        var modelIndex = _modelIndexes[id];
        var model = _models[modelIndex];
        AutoMapper.ApplyChanges(model, modelChanges);
    }

    public void Add(TModel model)
    {
        _modelIndexes[model.Id] = _models.Count;
        var clone = AutoMapper.Clone(model);
        _models.Add(clone);
    }
}