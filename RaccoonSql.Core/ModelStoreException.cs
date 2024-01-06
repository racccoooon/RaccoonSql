// ReSharper disable MemberCanBeProtected.Global

using JetBrains.Annotations;

namespace RaccoonSql.Core;

[PublicAPI]
public abstract class ModelStoreException(Type type, Guid id) : Exception
{
    public Guid Id { get; } = id;
    public Type ModelType { get; } = type;
}