namespace RaccoonSql.Core;

public record ModelStoreOptions
{
    public required string StoragePath { get; init; }
    public int ModelsPerChunk { get; init; } = 512;
    public int RehashThreshold { get; init; } = 66;
    public ConflictBehavior DefaultInsertConflictBehavior { get; init; } = ConflictBehavior.Throw;
    public ConflictBehavior FindDefaultConflictBehavior { get; init; } = ConflictBehavior.Throw;
    public ConflictBehavior DefaultUpdateConflictBehavior { get; init; } = ConflictBehavior.Throw;
    public ConflictBehavior DefaultUpsertConflictBehavior { get; init; } = ConflictBehavior.Throw;
    public ConflictBehavior DefaultRemoveConflictBehavior { get; init; } = ConflictBehavior.Ignore;
}