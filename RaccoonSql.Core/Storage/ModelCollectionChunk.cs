using System.Diagnostics;

namespace RaccoonSql.Core.Storage;

public class ModelCollectionChunk
{
    public List<IModel> Models { get; set; } = new();

    public uint ModelCount => (uint)Models.Count;

    public ChunkChange WriteModel(uint offset, IModel model)
    {
        Debug.Assert(offset <= Models.Count, "offset <= _models.Count");
        var add = Models.Count == offset; 
        if (add)
        {
            Models.Add(model);
        }
        else
        {
            Models[(int)offset] = model;
        }

        return new ChunkChange
        {
            Model = model,
            Add = add,
            Offset = offset,
        };
    }

    public IModel GetModel(uint offset)
    {
        Debug.Assert(offset < Models.Count, "offset < _models.Count");
        return Models[(int)offset];
    }

    public Guid? DeleteModel(uint offset)
    {       
        Debug.Assert(offset < Models.Count, "offset < _models.Count");
        Guid? movedModelId = null;
        if (offset < Models.Count - 1)
        {
            Models[(int)offset] = Models[^1];
            movedModelId = Models[(int)offset].Id;
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
            Models[(int)change.Offset] = change.Model;
        }
    }
}