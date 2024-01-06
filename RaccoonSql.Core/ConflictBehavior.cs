namespace RaccoonSql.Core;

public enum ConflictBehavior
{
    Ignore,
    Throw,
}

public static class ConflictBehaviorExtensions
{
    public static bool ShouldThrow(this ConflictBehavior conflictBehavior, bool isConflict)
        => conflictBehavior switch
        {
            ConflictBehavior.Ignore => false,
            ConflictBehavior.Throw => isConflict,
            _ => throw new ArgumentOutOfRangeException(nameof(conflictBehavior), conflictBehavior, null)
        };
}