using System.Collections;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace RaccoonSql.Core.Storage;

public class ModelCollectionChunk
{
    public List<IModel> Models { get; set; } = new();

    public int ModelCount => Models.Count;

    public void WriteModel(int offset, IModel model)
    {
        Debug.Assert(offset <= Models.Count, "offset <= _models.Count");
        if (Models.Count == offset)
        {
            Models.Add(model);
        }
        else
        {
            Models[offset] = model;
        }
    }

    public IModel GetModel(int offset)
    {
        Debug.Assert(offset < Models.Count, "offset < _models.Count");
        return Models[offset];
    }

    public Guid? DeleteModel(int offset)
    {       
        Debug.Assert(offset < Models.Count, "offset < _models.Count");
        Guid? movedModelId = null;
        if (offset < Models.Count - 1)
        {
            Models[offset] = Models[^1];
            movedModelId = Models[offset].Id;
        }
        Models.RemoveAt(Models.Count - 1);
        return movedModelId;
    }

}