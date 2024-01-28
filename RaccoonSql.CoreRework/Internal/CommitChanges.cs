namespace RaccoonSql.CoreRework.Internal;

internal class CommitChanges
{
    public required List<ChangeSet> Changes { get; init; }
    public required ulong Version { get; init; }
}