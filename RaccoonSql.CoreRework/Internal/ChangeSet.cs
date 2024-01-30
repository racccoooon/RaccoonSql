using System.Reflection;

namespace RaccoonSql.CoreRework.Internal;

internal class ChangeSet(
    Type modelType, 
    List<ModelBase> added,
    HashSet<Guid> removed, 
    List<(Guid id, Dictionary<PropertyInfo, object?> changes)> changed)
{
    internal Type ModelType => modelType;
    public string ModelName => modelType.FullName!;
    public List<ModelBase> Added => added;
    public HashSet<Guid> Removed => removed;
    public List<(Guid id, Dictionary<PropertyInfo, object> changes)> Changed => changed;
    internal bool HasChanges => added.Count > 0 || removed.Count > 0 || changed.Count > 0;
}