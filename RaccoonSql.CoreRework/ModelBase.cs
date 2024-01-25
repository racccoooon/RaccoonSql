using System.Text.Json.Serialization;

namespace RaccoonSql.CoreRework;

public abstract class ModelBase
{
    private Guid _id;

    public Guid Id
    {
        get
        {
            if (_id == default)
                _id = Guid.NewGuid();
            
            return _id;
        }
        internal set => _id = value;
    }

    internal Action<Guid>? OnChange;
    internal Dictionary<string, object?> Changes = default!;
    internal bool TrackChanges { get; set; }
}