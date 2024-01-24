namespace RaccoonSql.CoreRework.Internal;

internal class ChangeSet(Type modelType, List<ModelBase> added, HashSet<Guid> removed, List<ModelBase> changed)
{
    internal Type ModelType => modelType;
    internal List<ModelBase> Added => added;
    internal HashSet<Guid> Removed => removed;
    internal List<ModelBase> Changed => changed;
    internal bool HasChanges => added.Count > 0 || removed.Count > 0 || changed.Count > 0;
}