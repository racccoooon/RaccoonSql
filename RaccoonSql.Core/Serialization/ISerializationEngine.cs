using JetBrains.Annotations;

namespace RaccoonSql.Core.Serialization;

[PublicAPI]
public interface ISerializationEngine
{
    Stream Serialize(object data, Type type);
    
    Task<Stream> SerializeAsync(object data, Type type, CancellationToken cancellationToken = default)
        => Task.FromResult(Serialize(data, type));
    
    object Deserialize(Stream stream, Type type);

    Task<object> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
        => Task.FromResult(Deserialize(stream, type));
}