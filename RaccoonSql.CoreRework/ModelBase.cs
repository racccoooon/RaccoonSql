namespace RaccoonSql.CoreRework;

public abstract class ModelBase
{
    public Guid Id { get; internal set; } = Guid.NewGuid();

    internal Action<Guid>? OnChange;
    internal Dictionary<string, object?> Changes = default!;
    internal bool TrackChanges { get; set; }
}