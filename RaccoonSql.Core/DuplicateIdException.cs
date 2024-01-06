using JetBrains.Annotations;

namespace RaccoonSql.Core;

[PublicAPI]
public class DuplicateIdException(Type type, Guid id) 
    : ModelStoreException(type, id)
{
    public override string Message =>
        $"Duplicate id '{Id}' for model type '{ModelType.Name}'.";
}