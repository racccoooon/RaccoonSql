using System.Collections;
using System.Diagnostics;

namespace RaccoonSql.Core.Storage;

internal class ModelCollectionChunk : IEnumerable<IModel>
{
    private List<IModel> _models = new();
    private int _count = 0;

    public ModelCollectionChunk()
    {
        //TODO: load from file system
    }

    public int ModelCount => _models.Count;

    public void WriteModel(int offset, IModel model)
    {
        Debug.Assert(offset <= _models.Count, "offset <= _models.Count");
        if (_models.Count == offset)
        {
            _models.Add(model);
            _count++;
        }
        else
        {
            _models[offset] = model;
        }
    }

    public IModel GetModel(int offset)
    {
        Debug.Assert(offset < _models.Count, "offset < _models.Count");
        return _models[offset];
    }

    public void DeleteModel(int offset)
    {       
        Debug.Assert(offset < _models.Count, "offset < _models.Count");
        _models[offset] = _models[_count - 1];
        _count--;
    }

    public IEnumerator<IModel> GetEnumerator()
    {
        return _models.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}