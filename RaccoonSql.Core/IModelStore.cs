using JetBrains.Annotations;

namespace RaccoonSql.Core;

[PublicAPI]
public interface IModelStore
{
    IModelSet<TData> Set<TData>(string? setName = null) where TData : IModel;
}