namespace RaccoonSql.Core;

public record ModelStoreOptions
{
    public ConflictBehavior DefaultInsertConflictBehavior { get; init; } = ConflictBehavior.Throw;
    public ConflictBehavior FindDefaultConflictBehavior { get; init; } = ConflictBehavior.Throw;
    public ConflictBehavior DefaultUpdateConflictBehavior { get; init; } = ConflictBehavior.Throw;
    public ConflictBehavior DefaultRemoveConflictBehavior { get; init; } = ConflictBehavior.Ignore;
}