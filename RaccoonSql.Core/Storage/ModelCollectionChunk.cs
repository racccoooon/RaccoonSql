using System.Diagnostics;

namespace RaccoonSql.Core.Storage;

public class ModelCollectionChunk
{
    public List<IModel> Models { get; set; } = new();

    public int ModelCount => Models.Count;

    public ChunkChange WriteModel(int offset, IModel model)
    {
        Debug.Assert(offset <= Models.Count, "offset <= _models.Count");
        var add = Models.Count == offset; 
        if (add)
        {
            Models.Add(model);
        }
        else
        {
            Models[offset] = model;
        }

        return new ChunkChange
        {
            Model = model,
            Add = add,
            Offset = offset,
        };
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

    public void Apply(ChunkChange change)
    {
        if (change.Add)
        {
            Models.Add(change.Model);
        }
        else
        {
            Models[change.Offset] = change.Model;
        }
    }
}