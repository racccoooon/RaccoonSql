using JetBrains.Annotations;

namespace RaccoonSql.Core.Serialization;

[PublicAPI]
public interface ISerializationEngine
{
    //TODO: change non async mehtod to use IReadonlySpan<byte>
    Stream Serialize(object data);
    
    Task<Stream> SerializeAsync(object data, CancellationToken cancellationToken = default)
        => Task.FromResult(Serialize(data));
    
    object Deserialize(Stream stream, Type type);

    Task<object> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default)
        => Task.FromResult(Deserialize(stream, type));
}