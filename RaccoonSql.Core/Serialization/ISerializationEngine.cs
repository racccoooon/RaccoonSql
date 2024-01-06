using JetBrains.Annotations;

namespace RaccoonSql.Core.Serialization;

[PublicAPI]
public interface ISerializationEngine
{
    Stream Serialize<TData>(TData data);
    
    Task<Stream> SerializeAsync<TData>(TData data, CancellationToken cancellationToken = default)
        => Task.FromResult(Serialize(data));
    
    TData Deserialize<TData>(Stream stream);

    Task<TData> DeserializeAsync<TData>(Stream stream, CancellationToken cancellationToken = default)
        => Task.FromResult(Deserialize<TData>(stream));
}