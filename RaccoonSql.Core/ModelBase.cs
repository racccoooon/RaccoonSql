namespace RaccoonSql.Core;

public abstract class ModelBase
{
    public Guid Id { get; internal set; } = Guid.NewGuid();

    internal Dictionary<string, object?> Changes = null!;
    internal bool TrackChanges = false;
}