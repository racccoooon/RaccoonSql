namespace RaccoonSql.CoreRework.Internal;

internal class ChangeSet(Type modelType, List<ModelBase> added, HashSet<Guid> removed, List<ModelBase> changed)
{
    internal Type ModelType => modelType;
    public string ModelName => modelType.FullName!;
    public List<ModelBase> Added => added;
    public HashSet<Guid> Removed => removed;
    public List<ModelBase> Changed => changed;
    internal bool HasChanges => added.Count > 0 || removed.Count > 0 || changed.Count > 0;
}