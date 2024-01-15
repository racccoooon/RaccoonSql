using JetBrains.Annotations;

namespace RaccoonSql.Core.Serialization;

[PublicAPI]
public interface ISerializationEngine
{
    void Serialize(Stream stream, object data, Type type);

    Task SerializeAsync(Stream stream, object data, Type type, CancellationToken cancellationToken = default)
    {
        Serialize(stream, data, type);
        return Task.CompletedTask;
    }

    object Deserialize(Stream stream, Type type);

    Task<object> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
        => Task.FromResult(Deserialize(stream, type));
}