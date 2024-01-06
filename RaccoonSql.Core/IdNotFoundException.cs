using JetBrains.Annotations;

namespace RaccoonSql.Core;

[PublicAPI]
public class IdNotFoundException(Type type, Guid id) 
    : ModelStoreException(type, id)
{
    public override string Message =>
        $"Id '{Id}' not found for model type '{ModelType.Name}'.";
}